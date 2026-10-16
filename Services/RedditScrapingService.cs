using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Xml;
using System.Text.RegularExpressions;

namespace QuantResearchAgent.Services;

/// <summary>
/// RedditScrapingService with two interchangeable back-ends:
///
/// 1. Chocodata (<c>https://api.chocodata.com/api/v1/reddit/...</c>) — the preferred source.
///    It is a scraping proxy, so requests leave from its residential pool instead of this
///    machine: there is no per-IP budget to stay inside, no 429 dance, and it returns the
///    engagement counters (score / upvote_ratio / num_comments) that RSS cannot. Enabled as
///    soon as CHOCODATA_API_KEY is configured; only 2xx responses are billed.
/// 2. Reddit's public Atom RSS feed (<c>https://www.reddit.com/r/{subreddit}/hot/.rss</c>) —
///    the free fallback used when no API key is set or Chocodata fails/returns nothing.
///    Reddit removed free OAuth API access in 2023 and returns HTTP 403 for the public
///    ".json" listing endpoints from most non-browser / datacenter clients, so RSS is the only
///    direct path still open. It is rate limited to roughly one request per IP per window and
///    does not expose score / upvote / comment counts (those come back as 0).
/// Respect robots.txt and Reddit's terms of service.
/// </summary>
public class RedditScrapingService
{
    private readonly ILogger<RedditScrapingService> _logger;
    private readonly HttpClient _httpClient;
    private static readonly Random _rng = new();
    private const int MinDelayMs = 1500;
    private const int MaxDelayMs = 3500;

    // ── Chocodata (preferred source) ─────────────────────────
    private const string DefaultChocodataBaseUrl = "https://api.chocodata.com/api/v1";
    private const int DefaultChocodataTimeoutSeconds = 20;

    /// <summary>
    /// Chocodata recommends 4 attempts with exponential backoff + jitter. 408 (upstream
    /// timeout), 422 (target blocked us), 429 (our key hit its RPS ceiling), 500, 502 and 503
    /// are documented as retryable; 400 / 401 / 402 / 404 / 501 are permanent, so they must not
    /// burn attempts (and are never billed).
    /// </summary>
    private const int ChocodataMaxAttempts = 4;

    /// <summary>
    /// Chocodata asks for a 60s+ delay before retrying 422/502. That is longer than a user-facing
    /// news/sentiment request can wait, so backoff is capped and the RSS fallback takes over
    /// instead — it leaves from a different network path, so it often succeeds where the proxy
    /// was blocked.
    /// </summary>
    private const int MaxChocodataBackoffMs = 20_000;

    private readonly string _chocodataApiKey;
    private readonly string _chocodataBaseUrl;
    private readonly int _chocodataTimeoutSeconds;
    private readonly string _chocodataSubredditSort;
    private readonly string _chocodataSearchSort;
    private readonly string _chocodataSearchTimeFilter;

    /// <summary>True when a Chocodata API key is configured, i.e. the proxy path is usable.</summary>
    internal bool IsChocodataEnabled => _chocodataApiKey.Length > 0;

    // Reddit's anonymous feed advertises its own budget in response headers and answers HTTP 429
    // once that budget is spent — it sends no Retry-After. The observed budget from one IP is
    // roughly "one request per window", with the window length in x-ratelimit-reset (seconds),
    // which is why firing a multi-subreddit scrape back-to-back earned exactly one 200 followed
    // by a run of 429s, leaving every subreddit after the first empty.
    // So: wait out the advertised window before the next request, and retry a 429 after it rolls
    // over, but stay bounded so a web request can never hang indefinitely.
    private const int MaxRateLimitWaitMs = 30_000;
    internal const int MaxRateLimitAttempts = 3;

    // The budget is per IP, therefore per process: the sentiment endpoints, the news pipeline and
    // the Reddit plugin all scrape through their own service instances in the same request. A
    // per-instance gate let those flows race each other for the same budget and keep the window
    // shut for one another, so the gate is static and requests are serialised through it.
    private static readonly SemaphoreSlim RateGate = new(1, 1);
    private static DateTime _nextRequestNotBeforeUtc = DateTime.MinValue;

    private static readonly string[] UserAgents = [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:120.0) Gecko/20100101 Firefox/120.0",
    ];

    public RedditScrapingService(ILogger<RedditScrapingService> logger, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;

        // The key may be saved as a plain string under the env-var style name, as a nested
        // {"ApiKey": "..."} object under that name, or inside a Chocodata section.
        _chocodataApiKey = (FirstNonEmpty(
            configuration["CHOCODATA_API_KEY"],
            configuration["CHOCODATA_API_KEY:ApiKey"],
            configuration["Chocodata:ApiKey"]) ?? string.Empty).Trim();

        _chocodataBaseUrl = (FirstNonEmpty(
            configuration["Chocodata:BaseUrl"],
            configuration["CHOCODATA_API_KEY:BaseUrl"],
            DefaultChocodataBaseUrl) ?? DefaultChocodataBaseUrl).TrimEnd('/');
        _chocodataTimeoutSeconds = int.TryParse(
            FirstNonEmpty(configuration["Chocodata:RequestTimeoutSeconds"], configuration["CHOCODATA_API_KEY:RequestTimeoutSeconds"]),
            out var timeout) && timeout > 0
            ? timeout
            : DefaultChocodataTimeoutSeconds;
        _chocodataSubredditSort = FirstNonEmpty(
            configuration["Chocodata:SubredditSort"], configuration["CHOCODATA_API_KEY:SubredditSort"], "hot")!;
        _chocodataSearchSort = FirstNonEmpty(
            configuration["Chocodata:SearchSort"], configuration["CHOCODATA_API_KEY:SearchSort"], "relevance")!;
        _chocodataSearchTimeFilter = FirstNonEmpty(
            configuration["Chocodata:SearchTimeFilter"], configuration["CHOCODATA_API_KEY:SearchTimeFilter"], "day")!;

        _logger.LogInformation(
            "RedditScrapingService initialized — source: {Source}",
            IsChocodataEnabled
                ? $"Chocodata reddit API at {_chocodataBaseUrl} (proxied, no per-IP limit)"
                : "public Atom RSS feed (no Chocodata API key configured)");
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>
    /// Scrape hot posts from a public subreddit. Uses the Chocodata proxy when an API key is
    /// configured (fast, parallel-safe, includes score/comment counts) and falls back to Reddit's
    /// public Atom RSS feed otherwise.
    /// </summary>
    public async Task<List<RedditPost>> ScrapeSubredditAsync(string subreddit, int limit = 25)
    {
        try
        {
            if (IsChocodataEnabled)
            {
                var proxied = await ScrapeViaChocodataAsync(subreddit, limit);
                if (proxied.Count > 0) return proxied;

                _logger.LogWarning(
                    "Chocodata returned no posts for r/{Subreddit} — falling back to the public RSS feed", subreddit);
            }

            // Only the direct-to-Reddit path needs the polite delay; Chocodata serves from its own pool.
            await ThrottleAsync();
            var posts = await ScrapeViaRssAsync(subreddit, limit);
            if (posts.Count == 0)
            {
                _logger.LogWarning("RSS returned no posts for r/{Subreddit}", subreddit);
            }
            return posts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape r/{Subreddit}", subreddit);
            return [];
        }
    }

    /// <summary>Analyze sentiment across posts in a subreddit.</summary>
    public async Task<RedditSentimentAnalysis> AnalyzeSubredditSentimentAsync(string subreddit, string? symbol = null, int limit = 50)
    {
        var posts = await ScrapeSubredditAsync(subreddit, limit);
        var analysis = new RedditSentimentAnalysis
        {
            Subreddit = subreddit,
            Symbol = symbol,
            AnalysisDate = DateTime.UtcNow,
            TotalPosts = posts.Count,
            Posts = posts
        };
        if (posts.Count == 0) return analysis;

        analysis.AverageScore = posts.Average(p => p.Score);
        analysis.TotalUpvotes = posts.Sum(p => p.Upvotes);
        analysis.TotalDownvotes = posts.Sum(p => p.Downvotes);
        analysis.TotalComments = posts.Sum(p => p.Comments);
        analysis.TopPost = posts.MaxBy(p => p.Score);
        return analysis;
    }

    /// <summary>Monitor multiple subreddits.</summary>
    public async Task<MultiSubredditResult> MonitorMultipleSubredditsAsync(List<string> subreddits, int postsPerSubreddit = 10)
    {
        var result = new MultiSubredditResult
        {
            MonitoredSubreddits = subreddits,
            MonitoringDate = DateTime.UtcNow
        };
        foreach (var sub in subreddits)
        {
            var posts = await ScrapeSubredditAsync(sub, postsPerSubreddit);
            result.SubredditResults[sub] = posts;
            result.TotalPosts += posts.Count;
        }
        if (result.TotalPosts > 0)
        {
            result.AverageScore = result.SubredditResults.Values.SelectMany(p => p).Average(p => p.Score);
            result.TopPostOverall = result.SubredditResults.Values.SelectMany(p => p).MaxBy(p => p.Score);
            result.MostDiscussedPost = result.SubredditResults.Values.SelectMany(p => p).MaxBy(p => p.Comments);
        }
        return result;
    }

/// <summary>Search for a term across multiple subreddits.</summary>
    public async Task<MultiSubredditSearchResult> SearchAcrossSubredditsAsync(string searchTerm, List<string> subreddits, int maxPostsPerSubreddit = 5)
    {
        var result = new MultiSubredditSearchResult
        {
            SearchTerm = searchTerm,
            SearchDate = DateTime.UtcNow,
            SearchedSubreddits = subreddits
        };
        foreach (var sub in subreddits)
        {
            var posts = await ScrapeSubredditAsync(sub, maxPostsPerSubreddit);
            var filtered = posts.Where(p => p.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) || p.Content.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            if (filtered.Count > 0) result.Results[sub] = filtered;
        }
        result.TotalResultsFound = result.Results.Values.Sum(l => l.Count);
        result.MostRelevantPost = result.Results.Values.SelectMany(p => p).MaxBy(p => p.Score);
        result.SubredditsWithResults = result.Results.Keys.ToList();
        return result;
    }

    /// <summary>Search a single subreddit for posts mentioning a symbol or keyword.</summary>
    public async Task<List<RedditPost>> SearchSubredditAsync(string subreddit, string searchTerm, int limit = 5)
    {
        // Chocodata's reddit/search runs the query on Reddit's own index (relevance-ranked) and can
        // be scoped to one subreddit, which beats pulling the hot listing and filtering it locally.
        if (IsChocodataEnabled)
        {
            var found = await SearchViaChocodataAsync(searchTerm, subreddit, limit);
            if (found.Count > 0) return found;

            _logger.LogWarning(
                "Chocodata search found nothing for '{SearchTerm}' in r/{Subreddit} — falling back to a local filter of the feed",
                searchTerm, subreddit);
        }

        var posts = await ScrapeSubredditAsync(subreddit, limit);
        return posts.Where(p => p.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                             || p.Content.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
    }

    // ── Chocodata proxy API ──────────────────────────────────
    // https://api.chocodata.com/api/v1/reddit/{subreddit|search}?api_key=...
    // Only 2xx responses are billed, so a failed retry costs nothing but time — still, attempts
    // are bounded and permanent errors are never retried.

    /// <summary>Fetch a subreddit listing through Chocodata's <c>reddit/subreddit</c> resource.</summary>
    private Task<List<RedditPost>> ScrapeViaChocodataAsync(string subreddit, int limit)
    {
        var url = $"{_chocodataBaseUrl}/reddit/subreddit{BuildChocodataQuery(
            ("subreddit", subreddit),
            ("sort", _chocodataSubredditSort),
            ("limit", limit.ToString(CultureInfo.InvariantCulture)))}";

        _logger.LogDebug("Fetching r/{Subreddit} via Chocodata (sort={Sort}, limit={Limit})",
            subreddit, _chocodataSubredditSort, limit);

        return GetChocodataPostsAsync(url, subreddit);
    }

    /// <summary>
    /// Search through Chocodata's <c>reddit/search</c> resource. <c>q</c> is required; the
    /// subreddit scope is optional, so passing null searches all of Reddit.
    /// </summary>
    private Task<List<RedditPost>> SearchViaChocodataAsync(string query, string? subreddit, int limit)
    {
        var url = $"{_chocodataBaseUrl}/reddit/search{BuildChocodataQuery(
            ("q", query),
            ("subreddit", subreddit),
            ("sort", _chocodataSearchSort),
            ("t", _chocodataSearchTimeFilter),
            ("limit", limit.ToString(CultureInfo.InvariantCulture)))}";

        _logger.LogDebug("Searching Reddit via Chocodata for '{Query}' (subreddit={Subreddit}, limit={Limit})",
            query, subreddit ?? "all", limit);

        return GetChocodataPostsAsync(url, subreddit ?? string.Empty);
    }

    /// <summary>
    /// Builds the query string for a Chocodata call: always carries the API key, skips parameters
    /// that were not supplied, and URL-escapes every value (subreddit names, keyword queries).
    /// </summary>
    private string BuildChocodataQuery(params (string Key, string? Value)[] parameters)
    {
        var parts = new List<string> { $"api_key={Uri.EscapeDataString(_chocodataApiKey)}" };
        foreach (var (key, value) in parameters)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }
        return "?" + string.Join("&", parts);
    }

    /// <summary>
    /// GET a Chocodata URL following the retry policy the API documents: retry the transient
    /// statuses (408, 422, 429, 500, 502, 503) with exponential backoff, honour <c>Retry-After</c>
    /// when present, and give up immediately on the permanent ones (400, 401, 402, 404, 501) so a
    /// bad key or an exhausted balance is surfaced instead of being retried three more times.
    /// Returns an empty list when nothing could be retrieved, which lets the caller use RSS.
    /// </summary>
    private async Task<List<RedditPost>> GetChocodataPostsAsync(string url, string subreddit)
    {
        for (var attempt = 1; attempt <= ChocodataMaxAttempts; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_chocodataTimeoutSeconds));
                response = await _httpClient.GetAsync(url, cts.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Chocodata request for r/{Subreddit} failed at the transport level (attempt {Attempt}/{MaxAttempts})",
                    subreddit, attempt, ChocodataMaxAttempts);
                if (attempt == ChocodataMaxAttempts) return [];
                await Task.Delay(ChocodataBackoff(attempt, null));
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    // A 2xx is the only response that is billed, so this is the one that must parse.
                    var json = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var posts = ParseChocodataResponse(json, subreddit);
                        _logger.LogDebug("Chocodata returned {Count} posts for r/{Subreddit}", posts.Count, subreddit);
                        return posts;
                    }
                    catch (JsonException ex)
                    {
                        // Billed but unreadable: report it and return nothing so the RSS fallback covers the gap.
                        _logger.LogWarning(ex,
                            "Chocodata returned a 2xx body that is not valid JSON for r/{Subreddit}", subreddit);
                        return [];
                    }
                }

                var detail = await DescribeChocodataErrorAsync(response);

                if (!IsRetryableChocodataStatus(response.StatusCode))
                {
                    _logger.LogWarning(
                        "Chocodata rejected the request for r/{Subreddit} with {Status} ({Detail}) — permanent, not retrying",
                        subreddit, (int)response.StatusCode, detail);
                    return [];
                }

                if (attempt == ChocodataMaxAttempts)
                {
                    _logger.LogWarning(
                        "Chocodata still failing for r/{Subreddit} after {MaxAttempts} attempts (last {Status}, {Detail})",
                        subreddit, ChocodataMaxAttempts, (int)response.StatusCode, detail);
                    return [];
                }

                var delay = ChocodataBackoff(attempt, response);
                _logger.LogWarning(
                    "Chocodata returned {Status} for r/{Subreddit} ({Detail}, attempt {Attempt}/{MaxAttempts}) — retrying in {DelayMs}ms",
                    (int)response.StatusCode, subreddit, detail, attempt, ChocodataMaxAttempts, (int)delay.TotalMilliseconds);
                await Task.Delay(delay);
            }
        }

        return [];
    }

    /// <summary>
    /// Chocodata's retryable statuses. Everything else is a client/config error that will not get
    /// better by trying again.
    /// </summary>
    private static bool IsRetryableChocodataStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.RequestTimeout => true,        // 408 upstream_timeout
        (HttpStatusCode)422 => true,                  // 422 blocked_by_target
        HttpStatusCode.TooManyRequests => true,       // 429 rate_limited
        HttpStatusCode.InternalServerError => true,   // 500 internal_error
        HttpStatusCode.BadGateway => true,            // 502 target_unreachable / extraction_failed
        HttpStatusCode.ServiceUnavailable => true,    // 503 capacity
        _ => false                                    // 400/401/402/404/501 are permanent
    };

    /// <summary>
    /// Exponential backoff with jitter, capped at <see cref="MaxChocodataBackoffMs"/>. A
    /// <c>Retry-After</c> header (sent on 429/503) takes precedence over the computed delay.
    /// </summary>
    private static TimeSpan ChocodataBackoff(int attempt, HttpResponseMessage? response)
    {
        var documentedMs = response?.StatusCode switch
        {
            HttpStatusCode.RequestTimeout => 2_000,       // 408 — docs: 2s backoff
            (HttpStatusCode)422 => 15_000,                // 422 — docs suggest 60s+, capped for a web request
            HttpStatusCode.BadGateway => 15_000,          // 502 — docs suggest 60s+, capped for a web request
            HttpStatusCode.InternalServerError => 5_000,  // 500 — docs: ~5s
            _ => 0
        };

        var exponentialMs = 1_000 * (1 << (attempt - 1)); // 1s, 2s, 4s…
        double baseMs = Math.Max(documentedMs, exponentialMs);

        var retryAfter = response?.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            baseMs = Math.Max(baseMs, delta.TotalMilliseconds);
        }
        else if (retryAfter?.Date is { } retryDate)
        {
            baseMs = Math.Max(baseMs, (retryDate - DateTimeOffset.UtcNow).TotalMilliseconds);
        }

        // Random.Shared rather than the _rng field: concurrent scrapes can reach this at once.
        var jitterMs = Random.Shared.Next(0, 1_000);
        return TimeSpan.FromMilliseconds(Math.Min(baseMs + jitterMs, MaxChocodataBackoffMs));
    }

    /// <summary>
    /// Pull <c>message</c> and <c>request_id</c> out of Chocodata's error envelope
    /// (<c>{ error, message, docs_url, request_id }</c>) so a failure can be traced with their
    /// support without dumping the whole body into the log.
    /// </summary>
    private static async Task<string> DescribeChocodataErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body)) return "no response body";

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return $"error={ReadString(root, "error") ?? "unknown"}, " +
                   $"message={ReadString(root, "message") ?? "-"}, " +
                   $"request_id={ReadString(root, "request_id") ?? "-"}";
        }
        catch (Exception)
        {
            // An error body we cannot parse must not mask the status code that we do know.
            return "unparseable error body";
        }
    }

    /// <summary>
    /// Map a Chocodata listing response onto <see cref="RedditPost"/>. The <c>reddit/subreddit</c>
    /// resource wraps its listing in <c>posts</c>, the <c>reddit/search</c> resource in
    /// <c>results</c>; both carry <c>score</c>, <c>upvote_ratio</c> and <c>num_comments</c>, which
    /// the RSS feed cannot provide. Internal so the mapping can be unit tested against a captured
    /// payload without touching the network.
    /// </summary>
    internal static List<RedditPost> ParseChocodataResponse(string json, string subreddit)
    {
        var posts = new List<RedditPost>();
        if (string.IsNullOrWhiteSpace(json)) return posts;

        using var doc = JsonDocument.Parse(json);
        if (!TryGetPostArray(doc.RootElement, out var array)) return posts;

        foreach (var element in array.EnumerateArray())
        {
            // Entries wrapped in Reddit's own {"data": {...}} shape are unwrapped first.
            var node = element;
            if (FindProperty(node, "data") is { ValueKind: JsonValueKind.Object } inner)
            {
                node = inner;
            }

            // The search resource mixes other result types into `results`; only posts are useful here.
            if (ReadString(node, "result_type") is { } resultType
                && !resultType.Equals("post", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var title = ReadString(node, "title");
            if (string.IsNullOrWhiteSpace(title)) continue;

            var permalink = ReadString(node, "permalink") ?? ReadString(node, "url") ?? string.Empty;
            var post = new RedditPost
            {
                Id = ResolvePostId(ReadString(node, "id") ?? ReadString(node, "name"), permalink),
                Title = WebUtility.HtmlDecode(title),
                Author = NormalizeAuthor(ReadString(node, "author") ?? string.Empty),
                Url = ResolvePermalink(permalink),
                Subreddit = ReadString(node, "subreddit") ?? subreddit,
                Content = StripHtml(ReadString(node, "selftext")
                                    ?? ReadString(node, "content")
                                    ?? ReadString(node, "body")
                                    ?? string.Empty),
                Flair = ReadString(node, "link_flair_text") ?? ReadString(node, "flair") ?? string.Empty,
                CreatedUtc = ReadCreatedUtc(node),
                Score = ReadInt(node, "score") ?? ReadInt(node, "ups") ?? 0,
                Comments = ReadInt(node, "num_comments") ?? ReadInt(node, "comments") ?? 0
            };

            ApplyVoteSplit(post, ReadDouble(node, "upvote_ratio"));
            posts.Add(post);
        }

        return posts;
    }

    /// <summary>
    /// Locate the listing inside the response envelope. Handles Chocodata's <c>posts</c> /
    /// <c>results</c> keys, a bare array, and Reddit's native <c>{"data":{"children":[…]}}</c>
    /// shape so a change on their side degrades instead of throwing.
    /// </summary>
    private static bool TryGetPostArray(JsonElement root, out JsonElement array)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            array = root;
            return true;
        }

        foreach (var name in new[] { "posts", "results", "items", "children" })
        {
            if (FindProperty(root, name) is { ValueKind: JsonValueKind.Array } found)
            {
                array = found;
                return true;
            }
        }

        // {"data": [...]} or {"data": {"children": [...]}}
        if (FindProperty(root, "data") is { } data && TryGetPostArray(data, out array))
        {
            return true;
        }

        array = default;
        return false;
    }

    /// <summary>
    /// Case-insensitive property lookup: Chocodata documents snake_case (<c>num_comments</c>) but
    /// a casing change should not silently zero out the engagement counters.
    /// </summary>
    private static JsonElement? FindProperty(JsonElement node, string name)
    {
        if (node.ValueKind != JsonValueKind.Object) return null;

        foreach (var property in node.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }
        return null;
    }

    private static string? ReadString(JsonElement node, string name)
    {
        if (FindProperty(node, name) is not { } value) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? ReadInt(JsonElement node, string name)
    {
        if (FindProperty(node, name) is not { } value) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return (int)number;
        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return (int)parsed;
        }
        return null;
    }

    private static double? ReadDouble(JsonElement node, string name)
    {
        if (FindProperty(node, name) is not { } value) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
        return null;
    }

    /// <summary>
    /// Reddit's canonical post id: the <c>id</c> field when present (a leading <c>t3_</c> fullname
    /// prefix is stripped), otherwise the <c>/comments/{id}/</c> segment of the permalink.
    /// </summary>
    private static string ResolvePostId(string? id, string permalink)
    {
        var raw = (id ?? string.Empty).Trim();
        if (raw.StartsWith("t3_", StringComparison.OrdinalIgnoreCase)) raw = raw[3..];
        if (Regex.IsMatch(raw, "^[A-Za-z0-9]{4,12}$")) return raw;

        // Fall back to the permalink: https://www.reddit.com/r/{sub}/comments/{id}/{slug}/
        var match = Regex.Match(permalink ?? string.Empty, @"/comments/([A-Za-z0-9]{4,12})");
        return match.Success ? match.Groups[1].Value : raw;
    }

    /// <summary>
    /// Chocodata returns absolute permalinks; Reddit's own payload can also return a path-only
    /// <c>/r/…/comments/…</c>, which is expanded against the www host.
    /// </summary>
    private static string ResolvePermalink(string permalink)
    {
        if (string.IsNullOrWhiteSpace(permalink)) return string.Empty;
        return permalink.StartsWith('/') ? "https://www.reddit.com" + permalink : permalink;
    }

    /// <summary>
    /// Timestamps arrive either as Unix seconds (<c>created_utc</c>) or as an ISO-8601 string
    /// depending on the resource. Anything unreadable falls back to "now" so a post is never
    /// dropped over a date field.
    /// </summary>
    private static DateTime ReadCreatedUtc(JsonElement node)
    {
        var value = FindProperty(node, "created_utc") ?? FindProperty(node, "created");
        if (value is not { } created)
        {
            return DateTime.UtcNow;
        }

        if (created.ValueKind == JsonValueKind.Number && created.TryGetDouble(out var unix))
        {
            // Bounded so a millisecond value (1.7e12) cannot throw out of FromUnixTimeSeconds.
            if (unix > 0 && unix < 4_102_444_800)
            {
                return DateTimeOffset.FromUnixTimeSeconds((long)unix).UtcDateTime;
            }
        }

        if (created.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(created.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return parsed.UtcDateTime;
        }

        return DateTime.UtcNow;
    }

    /// <summary>
    /// Chocodata exposes <c>score</c> and <c>upvote_ratio</c>, not the raw up/down counters this
    /// model wants. Reddit defines score = up − down and ratio = up / (up + down), which invert to
    /// up = score × ratio / (2 × ratio − 1) and down = up − score. A ratio at or below 0.5 has a
    /// non-positive denominator, so the split is skipped and only Score/Comments are reported.
    /// </summary>
    private static void ApplyVoteSplit(RedditPost post, double? upvoteRatio)
    {
        if (upvoteRatio is not { } ratio || ratio <= 0.5 || ratio > 1.0) return;

        var upvotes = Math.Round(post.Score * ratio / (2 * ratio - 1));

        // Guard against a score/ratio pair that inverts into nonsense (or a negative score).
        if (upvotes < 0 || upvotes < post.Score) return;

        post.Upvotes = (int)upvotes;
        post.Downvotes = (int)(upvotes - post.Score);
    }

    // ── RSS ──────────────────────────────────────────────────

    private async Task<List<RedditPost>> ScrapeViaRssAsync(string subreddit, int limit)
    {
        var url = $"https://www.reddit.com/r/{subreddit}/hot/.rss";

        // One Reddit request at a time, process-wide: the advertised budget is per IP, so parallel
        // callers would only get each other 429s.
        await RateGate.WaitAsync();
        try
        {
            for (var attempt = 1; attempt <= MaxRateLimitAttempts; attempt++)
            {
                // Honour the window Reddit advertised on the previous response, otherwise a
                // multi-subreddit scrape burns its whole budget on the first request.
                await WaitForRateLimitWindowAsync(subreddit);

                _logger.LogDebug("Fetching r/{Subreddit} via RSS (attempt {Attempt}/{MaxAttempts})",
                    subreddit, attempt, MaxRateLimitAttempts);

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("User-Agent", GetNextUserAgent());
                req.Headers.TryAddWithoutValidation("Accept", "application/xml, text/xml");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var resp = await _httpClient.SendAsync(req, cts.Token);

                // Read the budget back off this response so the next call knows when it may go.
                ObserveRateLimitWindow(resp);

                if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning(
                        "RSS for r/{Subreddit} returned TooManyRequests (attempt {Attempt}/{MaxAttempts}) — waiting for the rate-limit window to reset",
                        subreddit, attempt, MaxRateLimitAttempts);
                    continue;
                }

                if (!resp.IsSuccessStatusCode)
                {
                    // 403/404 and friends will not get better by retrying.
                    _logger.LogWarning("RSS for r/{Subreddit} returned {Status}", subreddit, resp.StatusCode);
                    return [];
                }

                var xml = await resp.Content.ReadAsStringAsync();
                return ParseRssResponse(xml, subreddit, limit);
            }

            _logger.LogWarning("RSS for r/{Subreddit} still rate limited after {MaxAttempts} attempts",
                subreddit, MaxRateLimitAttempts);
            return [];
        }
        finally
        {
            RateGate.Release();
        }
    }

    private static List<RedditPost> ParseRssResponse(string xml, string subreddit, int limit)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            // Reddit's public feed is Atom, so every element lives in the Atom namespace.
            // XPath 1.0 only matches namespaced elements when the prefix is supplied,
            // otherwise "//entry" silently matches nothing.
            var nsm = new XmlNamespaceManager(doc.NameTable);
            nsm.AddNamespace("atom", "http://www.w3.org/2005/Atom");

            var entries = doc.SelectNodes("//atom:entry", nsm) ?? doc.SelectNodes("//item");
            if (entries == null || entries.Count == 0) return [];

            var posts = new List<RedditPost>();
            foreach (System.Xml.XmlNode entry in entries)
            {
                if (posts.Count >= limit) break;

                var titleNode = entry.SelectSingleNode("atom:title", nsm);
                var authorNode = entry.SelectSingleNode("atom:author/atom:name", nsm) ?? entry.SelectSingleNode("atom:author", nsm);
                var linkNode = entry.SelectSingleNode("atom:link", nsm);
                var idNode = entry.SelectSingleNode("atom:id", nsm);
                var contentNode = entry.SelectSingleNode("atom:content", nsm) ?? entry.SelectSingleNode("atom:summary", nsm);
                var updatedNode = entry.SelectSingleNode("atom:updated", nsm) ?? entry.SelectSingleNode("atom:published", nsm);

                var href = linkNode?.Attributes?["href"]?.Value ?? linkNode?.InnerText ?? "";
                var postId = ExtractRssId(idNode?.InnerText ?? href);

                posts.Add(new RedditPost
                {
                    Id = postId,
                    Title = titleNode?.InnerText.Trim() ?? "",
                    Author = NormalizeAuthor(authorNode?.InnerText),
                    Url = href.StartsWith("/") ? "https://reddit.com" + href : href,
                    Subreddit = subreddit,
                    Content = StripHtml(contentNode?.InnerText ?? ""),
                    // Reddit emits Atom timestamps with an offset; DateTimeOffset keeps the instant
                    // and UtcDateTime normalises it to a real UTC DateTime (the field is CreatedUtc
                    // and callers label it "UTC").
                    CreatedUtc = updatedNode != null && DateTimeOffset.TryParse(updatedNode.InnerText, out var dto) ? dto.UtcDateTime : DateTime.UtcNow,
                    // Reddit's RSS feed does not expose score / upvote / comment counts.
                    Score = 0, Upvotes = 0, Downvotes = 0, Comments = 0
                });
            }
            return posts;
        }
        catch (XmlException ex)
        {
            Console.Error.WriteLine($"RSS parse error for r/{subreddit}: {ex.Message}");
            return [];
        }
    }

    private static string ExtractRssId(string raw)
    {
        var m = Regex.Match(raw, @"(?:t3_)?([a-z0-9]{5,10})(?:/|$)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : raw;
    }

    /// <summary>
    /// Reddit's Atom feed reports the author as "/u/username". Normalise it so callers that
    /// render "u/{Author}" don't produce a doubled "u//u/username".
    /// </summary>
    private static string NormalizeAuthor(string raw)
    {
        var author = (raw ?? "").Trim();
        if (author.StartsWith("/u/", StringComparison.OrdinalIgnoreCase))
            author = author[3..];
        else if (author.StartsWith("u/", StringComparison.OrdinalIgnoreCase))
            author = author[2..];
        return string.IsNullOrEmpty(author) ? "unknown" : author;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        return Regex.Replace(html, "<[^>]+>", "").Trim();
    }

    private static string GetNextUserAgent() => UserAgents[_rng.Next(UserAgents.Length)];

    /// <summary>Polite delay to avoid hitting Reddit's rate limits on public endpoints.</summary>
    private async Task ThrottleAsync()
    {
        await Task.Delay(_rng.Next(MinDelayMs, MaxDelayMs));
    }

    /// <summary>
    /// Reads the rate-limit budget Reddit advertises on every response
    /// (<c>x-ratelimit-remaining</c> / <c>x-ratelimit-reset</c> in seconds, with
    /// <c>Retry-After</c> accepted as a fallback) and records when the next request may be sent.
    /// Headers are absent on non-Reddit responses, which simply leaves the gate open.
    /// </summary>
    private void ObserveRateLimitWindow(HttpResponseMessage response)
    {
        var resetSeconds = ReadHeaderDouble(response, "x-ratelimit-reset")
                           ?? ReadHeaderDouble(response, "Retry-After");
        if (resetSeconds is not { } reset)
        {
            return;
        }

        // Budget still in hand means we may keep going; only an exhausted budget (or an outright
        // 429) has to sit out the window.
        var remaining = ReadHeaderDouble(response, "x-ratelimit-remaining");
        var exhausted = response.StatusCode == HttpStatusCode.TooManyRequests || remaining is null or <= 0;
        if (!exhausted)
        {
            return;
        }

        // +1s of slack so we do not race the reset boundary. An already-open window (reset == 0)
        // adds nothing, which keeps the offline tests fast.
        var waitSeconds = reset + (reset > 0 ? 1 : 0);
        _nextRequestNotBeforeUtc = DateTime.UtcNow + TimeSpan.FromSeconds(waitSeconds);
    }

    /// <summary>Sits out whatever is left of the advertised rate-limit window.</summary>
    private async Task WaitForRateLimitWindowAsync(string subreddit)
    {
        var wait = _nextRequestNotBeforeUtc - DateTime.UtcNow;
        if (wait <= TimeSpan.Zero)
        {
            return;
        }

        var cap = TimeSpan.FromMilliseconds(MaxRateLimitWaitMs);
        if (wait > cap)
        {
            // The advertised window can be longer than we are willing to hold an HTTP request
            // open; retrying after the cap re-reads the (shorter) remaining window.
            wait = cap;
        }

        _logger.LogInformation("Waiting {Seconds:F0}s for the Reddit rate-limit window before fetching r/{Subreddit}",
            wait.TotalSeconds, subreddit);
        await Task.Delay(wait);
    }

    private static double? ReadHeaderDouble(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues(name, out var values))
        {
            return null;
        }

        var raw = values.FirstOrDefault();
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}

public class RedditPost
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int Score { get; set; }
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
    public int Comments { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string Url { get; set; } = "";
    public string Subreddit { get; set; } = "";
    public string Content { get; set; } = "";
    public string Flair { get; set; } = "";
    public string? SearchQuery { get; set; }
}

public class RedditSentimentAnalysis
{
    public string Subreddit { get; set; } = "";
    public string? Symbol { get; set; }
    public DateTime AnalysisDate { get; set; }
    public int TotalPosts { get; set; }
    public double AverageScore { get; set; }
    public int TotalUpvotes { get; set; }
    public int TotalDownvotes { get; set; }
    public int TotalComments { get; set; }
    public RedditPost? TopPost { get; set; }
    public List<RedditPost> Posts { get; set; } = new();
    public int PositiveKeywordCount { get; set; }
    public int NegativeKeywordCount { get; set; }
    public double SentimentScore { get; set; }
    public string OverallSentiment { get; set; } = "Neutral";
}

public class MultiSubredditResult
{
    public List<string> MonitoredSubreddits { get; set; } = new();
    public DateTime MonitoringDate { get; set; }
    public Dictionary<string, List<RedditPost>> SubredditResults { get; set; } = new();
    public int TotalPosts { get; set; }
    public double AverageScore { get; set; }
    public int TotalEngagement { get; set; }
    public RedditPost? TopPostOverall { get; set; }
    public RedditPost? MostDiscussedPost { get; set; }
}

public class MultiSubredditSearchResult
{
    public string SearchTerm { get; set; } = "";
    public DateTime SearchDate { get; set; }
    public List<string> SearchedSubreddits { get; set; } = new();
    public Dictionary<string, List<RedditPost>> Results { get; set; } = new();
    public int TotalResultsFound { get; set; }
    public RedditPost? MostRelevantPost { get; set; }
    public List<string> SubredditsWithResults { get; set; } = new();
}