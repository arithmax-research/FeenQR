using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Xml;
using System.Text.RegularExpressions;

namespace QuantResearchAgent.Services;

/// <summary>
/// RedditScrapingService using public endpoints (no OAuth API key needed):
///  1. .json trick - https://reddit.com/r/subreddit/hot.json
///  2. RSS fallback - https://reddit.com/r/subreddit/hot/.rss
///  3. Playwright/Puppeteer (optional) - for headless browser extraction
///
/// Reddit removed free OAuth API access in 2023, but public .json/.rss endpoints
/// remain available with throttling. Respect robots.txt and terms of service.
/// </summary>
public class RedditScrapingService
{
    private readonly ILogger<RedditScrapingService> _logger;
    private readonly HttpClient _httpClient;
    private static readonly Random _rng = new();
    private const int MinDelayMs = 1500;
    private const int MaxDelayMs = 3500;

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
        _logger.LogInformation("RedditScrapingService initialized — using public .json / .rss endpoints (no API key)");
    }

    /// <summary>Scrape hot posts from a public subreddit via .json (primary) or RSS (fallback).</summary>
    public async Task<List<RedditPost>> ScrapeSubredditAsync(string subreddit, int limit = 25)
    {
        try
        {
            await ThrottleAsync();
            var posts = await ScrapeViaJsonAsync(subreddit, limit);
            if (posts.Count > 0) return posts;

            _logger.LogInformation(".json returned empty for r/{Subreddit}, trying RSS fallback", subreddit);
            await ThrottleAsync();
            posts = await ScrapeViaRssAsync(subreddit, limit);
            if (posts.Count > 0) return posts;

            _logger.LogWarning("Both .json and RSS failed for r/{Subreddit}", subreddit);
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

    // ── JSON trick ────────────────────────────────────────────

    private async Task<List<RedditPost>> ScrapeViaJsonAsync(string subreddit, int limit)
    {
        var url = $"https://www.reddit.com/r/{subreddit}/hot.json?limit={limit}&raw_json=1";
        _logger.LogDebug("Fetching r/{Subreddit} via .json", subreddit);

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("User-Agent", GetNextUserAgent());
        req.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var resp = await _httpClient.SendAsync(req, cts.Token);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning(".json for r/{Subreddit} returned {Status}", subreddit, resp.StatusCode);
            return [];
        }

        var json = await resp.Content.ReadAsStringAsync();
        return ParseJsonResponse(json, subreddit);
    }

    /// <summary>Search a single subreddit for posts mentioning a symbol or keyword.</summary>
    public async Task<List<RedditPost>> SearchSubredditAsync(string subreddit, string searchTerm, int limit = 5)
    {
        var posts = await ScrapeSubredditAsync(subreddit, limit);
        return posts.Where(p => p.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                             || p.Content.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
    }

    private List<RedditPost> ParseJsonResponse(string json, string subreddit)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("children", out var children))
                return [];

            var posts = new List<RedditPost>();
            foreach (var child in children.EnumerateArray())
            {
                if (!child.TryGetProperty("data", out var postData)) continue;

                posts.Add(new RedditPost
                {
                    Id = postData.GetProperty("id").GetString() ?? "",
                    Title = postData.GetProperty("title").GetString() ?? "",
                    Author = postData.TryGetProperty("author", out var a) ? a.GetString() ?? "" : "",
                    Score = postData.TryGetProperty("score", out var s) ? s.GetInt32() : 0,
                    Upvotes = postData.TryGetProperty("ups", out var u) ? u.GetInt32() : 0,
                    Downvotes = postData.TryGetProperty("downs", out var d) ? d.GetInt32() : 0,
                    Comments = postData.TryGetProperty("num_comments", out var nc) ? nc.GetInt32() : 0,
                    Content = postData.TryGetProperty("selftext", out var st) ? st.GetString() ?? "" : "",
                    Url = postData.TryGetProperty("permalink", out var perm) ? "https://reddit.com" + perm.GetString() : "",
                    Subreddit = subreddit,
                    CreatedUtc = postData.TryGetProperty("created_utc", out var cu)
                        ? DateTimeOffset.FromUnixTimeSeconds(cu.GetInt64()).UtcDateTime
                        : DateTime.MinValue,
                    Flair = postData.TryGetProperty("link_flair_text", out var fl) ? fl.GetString() ?? "" : ""
                });
            }
            return posts;
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "JSON parse error for r/{Subreddit}", subreddit);
            return [];
        }
    }

    // ── RSS fallback ─────────────────────────────────────────

    private async Task<List<RedditPost>> ScrapeViaRssAsync(string subreddit, int limit)
    {
        var url = $"https://www.reddit.com/r/{subreddit}/hot/.rss";
        _logger.LogDebug("Fetching r/{Subreddit} via RSS", subreddit);

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("User-Agent", GetNextUserAgent());
        req.Headers.TryAddWithoutValidation("Accept", "application/xml, text/xml");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var resp = await _httpClient.SendAsync(req, cts.Token);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("RSS for r/{Subreddit} returned {Status}", subreddit, resp.StatusCode);
            return [];
        }

        var xml = await resp.Content.ReadAsStringAsync();
        return ParseRssResponse(xml, subreddit, limit);
    }

    private static List<RedditPost> ParseRssResponse(string xml, string subreddit, int limit)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var entries = doc.SelectNodes("//entry") ?? doc.SelectNodes("//item");
            if (entries == null || entries.Count == 0) return [];

            var posts = new List<RedditPost>();
            foreach (System.Xml.XmlNode entry in entries)
            {
                if (posts.Count >= limit) break;

                var titleNode = entry.SelectSingleNode("title");
                var authorNode = entry.SelectSingleNode("author/name") ?? entry.SelectSingleNode("author");
                var linkNode = entry.SelectSingleNode("link");
                var idNode = entry.SelectSingleNode("id");
                var contentNode = entry.SelectSingleNode("content") ?? entry.SelectSingleNode("description");
                var updatedNode = entry.SelectSingleNode("updated") ?? entry.SelectSingleNode("published");

                var href = linkNode?.Attributes?["href"]?.Value ?? linkNode?.InnerText ?? "";
                var postId = ExtractRssId(idNode?.InnerText ?? href);

                posts.Add(new RedditPost
                {
                    Id = postId,
                    Title = titleNode?.InnerText.Trim() ?? "",
                    Author = authorNode?.InnerText.Trim() ?? "unknown",
                    Url = href.StartsWith("/") ? "https://reddit.com" + href : href,
                    Subreddit = subreddit,
                    Content = StripHtml(contentNode?.InnerText ?? ""),
                    CreatedUtc = updatedNode != null && DateTime.TryParse(updatedNode.InnerText, out var dt) ? dt : DateTime.UtcNow,
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