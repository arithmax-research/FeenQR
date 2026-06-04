using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;

namespace QuantResearchAgent.Services;

public class RedditScrapingService
{
    private readonly ILogger<RedditScrapingService> _logger;
    private readonly HttpClient _httpClient;
    private static readonly Random _rng = new();

    private static readonly string[] UserAgents = {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:120.0) Gecko/20100101 Firefox/120.0",
    };

    public RedditScrapingService(ILogger<RedditScrapingService> logger, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;
        _logger.LogInformation("Reddit scraping service initialized (public HTTP/JSON + RSS fallback)");
    }

    public async Task<List<RedditPost>> ScrapeSubredditAsync(string subreddit, int limit = 25)
    {
        try
        {
            // Try JSON first (with browser UA)
            await Task.Delay(_rng.Next(500, 1500));
            var posts = await ScrapeViaJsonAsync(subreddit, limit);
            if (posts.Count > 0) return posts;

            // JSON failed (likely 403), try RSS instead
            _logger.LogInformation("JSON failed for r/{Subreddit}, trying RSS fallback", subreddit);
            await Task.Delay(_rng.Next(1000, 2500));
            var rss = await ScrapeViaRssAsync(subreddit, limit);
            if (rss.Count > 0) return rss;

            _logger.LogWarning("All methods failed for r/{Subreddit}", subreddit);
            return new List<RedditPost>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape r/{Subreddit}", subreddit);
            return new List<RedditPost>();
        }
    }

    private async Task<List<RedditPost>> ScrapeViaJsonAsync(string subreddit, int limit)
    {
        var url = $"https://www.reddit.com/r/{subreddit}/hot.json?limit={limit}&raw_json=1";
        _logger.LogInformation("Scraping r/{Subreddit} via JSON", subreddit);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", GetUa());
            req.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var resp = await _httpClient.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return new List<RedditPost>();

            var json = await resp.Content.ReadAsStringAsync();
            return ParseJson(json, subreddit);
        }
        catch { return new List<RedditPost>(); }
    }

    private async Task<List<RedditPost>> ScrapeViaRssAsync(string subreddit, int limit)
    {
        var url = $"https://www.reddit.com/r/{subreddit}/hot/.rss";
        _logger.LogInformation("Scraping r/{Subreddit} via RSS", subreddit);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", GetUa());
            req.Headers.TryAddWithoutValidation("Accept", "application/rss+xml");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var resp = await _httpClient.SendAsync(req, cts.Token);
            if (!resp.IsSuccessStatusCode) return new List<RedditPost>();

            var xml = await resp.Content.ReadAsStringAsync();
            return ParseRss(xml, subreddit, limit);
        }
        catch { return new List<RedditPost>(); }
    }

    private static List<RedditPost> ParseJson(string json, string subreddit)
    {
        var posts = new List<RedditPost>();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("children", out var children))
            return posts;

        foreach (var child in children.EnumerateArray())
        {
            if (!child.TryGetProperty("data", out var p)) continue;
            if (p.TryGetProperty("stickied", out var st) && st.GetBoolean()) continue;

            posts.Add(new RedditPost
            {
                Id = p.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                Title = p.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                Author = p.TryGetProperty("author", out var a) ? a.GetString() ?? "[deleted]" : "[deleted]",
                Score = p.TryGetProperty("score", out var s) ? s.GetInt32() : 0,
                Upvotes = p.TryGetProperty("ups", out var u) ? u.GetInt32() : 0,
                Downvotes = p.TryGetProperty("downs", out var d) ? d.GetInt32() : 0,
                Comments = p.TryGetProperty("num_comments", out var nc) ? nc.GetInt32() : 0,
                CreatedUtc = p.TryGetProperty("created_utc", out var cr) && cr.ValueKind == JsonValueKind.Number
                    ? DateTimeOffset.FromUnixTimeSeconds((long)cr.GetDouble()).DateTime : DateTime.UtcNow,
                Url = p.TryGetProperty("permalink", out var perm) ? $"https://reddit.com{perm.GetString()}" : "",
                Subreddit = subreddit,
                Content = p.TryGetProperty("selftext", out var stx) ? stx.GetString() ?? "" : "",
                Flair = p.TryGetProperty("link_flair_text", out var fl) ? fl.GetString() ?? "" : ""
            });
        }
        return posts;
    }

    private static List<RedditPost> ParseRss(string xml, string subreddit, int limit)
    {
        var posts = new List<RedditPost>();
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var entries = doc.GetElementsByTagName("entry");
        foreach (XmlNode entry in entries)
        {
            if (posts.Count >= limit) break;

            var title = entry.SelectSingleNode("*[local-name()='title']")?.InnerText ?? "";
            var author = entry.SelectSingleNode("*[local-name()='author']/*[local-name()='name']")?.InnerText ?? "[deleted]";
            var pub = entry.SelectSingleNode("*[local-name()='published']")?.InnerText ?? "";
            var content = entry.SelectSingleNode("*[local-name()='content']")?.InnerText ?? "";
            var linkNode = entry.SelectSingleNode("*[local-name()='link']") as XmlElement;
            var link = linkNode?.GetAttribute("href") ?? linkNode?.Attributes?["href"]?.Value ?? string.Empty;
            var guid = entry.SelectSingleNode("*[local-name()='id']")?.InnerText ?? entry.SelectSingleNode("*[local-name()='guid']")?.InnerText ?? string.Empty;

            DateTime.TryParse(pub, out var created);

            int score = 0, comments = 0;
            var sc = Regex.Match(content, @"score[:\s]*(\d+)", RegexOptions.IgnoreCase);
            if (sc.Success) int.TryParse(sc.Groups[1].Value, out score);
            var cm = Regex.Match(content, @"comments[:\s]*(\d+)", RegexOptions.IgnoreCase);
            if (cm.Success) int.TryParse(cm.Groups[1].Value, out comments);

            var cleaned = Regex.Replace(content ?? "", @"<[^>]+>", " ").Trim();

            posts.Add(new RedditPost
            {
                Id = guid,
                Title = HttpUtility.HtmlDecode(title),
                Author = author,
                Score = score,
                Upvotes = score,
                Downvotes = 0,
                Comments = comments,
                CreatedUtc = created == default ? DateTime.UtcNow : created,
                Url = string.IsNullOrWhiteSpace(link) ? $"https://www.reddit.com/r/{subreddit}/" : link,
                Subreddit = subreddit,
                Content = HttpUtility.HtmlDecode(cleaned),
                Flair = ""
            });
        }
        return posts;
    }

    public async Task<List<RedditPost>> SearchSubredditAsync(string subreddit, string query, int limit = 25)
    {
        try
        {
            await Task.Delay(_rng.Next(1000, 2000));

            // Try JSON search first
            var url = $"https://www.reddit.com/r/{subreddit}/search.json?q={Uri.EscapeDataString(query)}&restrict_sr=1&limit={limit}&sort=relevance&raw_json=1";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", GetUa());
            req.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var resp = await _httpClient.SendAsync(req, cts.Token);

            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                var posts = ParseJson(json, subreddit);
                foreach (var p in posts) p.SearchQuery = query;
                if (posts.Count > 0)
                {
                    _logger.LogInformation("Found {Count} posts matching '{Query}' in r/{Subreddit} via JSON search", posts.Count, query, subreddit);
                    return posts;
                }
            }

            // RSS search is publicly accessible and usually returns better term-specific results than hot-feed filtering.
            var rssSearchUrl = $"https://www.reddit.com/r/{subreddit}/search.rss?q={Uri.EscapeDataString(query)}&restrict_sr=1&sort=relevance";
            using (var rssReq = new HttpRequestMessage(HttpMethod.Get, rssSearchUrl))
            {
                rssReq.Headers.TryAddWithoutValidation("User-Agent", GetUa());
                rssReq.Headers.TryAddWithoutValidation("Accept", "application/rss+xml");

                using var rssCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var rssResp = await _httpClient.SendAsync(rssReq, rssCts.Token);
                if (rssResp.IsSuccessStatusCode)
                {
                    var xml = await rssResp.Content.ReadAsStringAsync();
                    var rssSearchPosts = ParseRss(xml, subreddit, limit)
                        .Take(limit)
                        .ToList();

                    foreach (var p in rssSearchPosts) p.SearchQuery = query;
                    if (rssSearchPosts.Count > 0)
                    {
                        _logger.LogInformation("Found {Count} posts matching '{Query}' in r/{Subreddit} via RSS search", rssSearchPosts.Count, query, subreddit);
                        return rssSearchPosts;
                    }
                }
            }

            // Fallback: scrape hot posts via RSS and filter by search term locally
            _logger.LogInformation("JSON search failed for r/{Subreddit}, falling back to RSS + local filtering", subreddit);
            var rssPosts = await ScrapeViaRssAsync(subreddit, Math.Max(limit * 2, 50));
            var filtered = rssPosts
                .Where(p => p.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            p.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();

            foreach (var p in filtered) p.SearchQuery = query;
            _logger.LogInformation("Found {Count} posts matching '{Query}' in r/{Subreddit} via RSS filter", filtered.Count, query, subreddit);
            return filtered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search r/{Subreddit} for '{Query}'", subreddit, query);
            return new List<RedditPost>();
        }
    }

    public async Task<RedditSentimentAnalysis> AnalyzeSubredditSentimentAsync(string subreddit, string? symbol = null, int limit = 50)
    {
        try
        {
            List<RedditPost> posts;
            
            if (!string.IsNullOrEmpty(symbol))
            {
                // Search for specific symbol
                posts = await SearchSubredditAsync(subreddit, symbol, limit);
            }
            else
            {
                // Get general posts
                posts = await ScrapeSubredditAsync(subreddit, limit);
            }

            var analysis = new RedditSentimentAnalysis
            {
                Subreddit = subreddit,
                Symbol = symbol,
                AnalysisDate = DateTime.UtcNow,
                TotalPosts = posts.Count,
                Posts = posts
            };

            if (posts.Any())
            {
                analysis.AverageScore = posts.Average(p => p.Score);
                analysis.TotalUpvotes = posts.Sum(p => p.Upvotes);
                analysis.TotalDownvotes = posts.Sum(p => p.Downvotes);
                analysis.TotalComments = posts.Sum(p => p.Comments);
                analysis.TopPost = posts.OrderByDescending(p => p.Score).First();
                
                // Basic sentiment analysis based on scores and keywords
                var positiveWords = new[] { "bull", "bullish", "buy", "moon", "rocket", "gains", "profit", "pump", "long", "calls" };
                var negativeWords = new[] { "bear", "bearish", "sell", "crash", "dump", "loss", "short", "puts", "red", "down" };
                
                int positiveCount = 0;
                int negativeCount = 0;
                
                foreach (var post in posts)
                {
                    var text = (post.Title + " " + post.Content).ToLower();
                    positiveCount += positiveWords.Count(word => text.Contains(word));
                    negativeCount += negativeWords.Count(word => text.Contains(word));
                }
                
                analysis.PositiveKeywordCount = positiveCount;
                analysis.NegativeKeywordCount = negativeCount;
                analysis.SentimentScore = (double)(positiveCount - negativeCount) / Math.Max(positiveCount + negativeCount, 1);
                
                if (analysis.SentimentScore > 0.2)
                    analysis.OverallSentiment = "Bullish";
                else if (analysis.SentimentScore < -0.2)
                    analysis.OverallSentiment = "Bearish";
                else
                    analysis.OverallSentiment = "Neutral";
            }

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze sentiment for r/{Subreddit}", subreddit);
            return new RedditSentimentAnalysis
            {
                Subreddit = subreddit,
                Symbol = symbol,
                AnalysisDate = DateTime.UtcNow,
                TotalPosts = 0,
                Posts = new List<RedditPost>()
            };
        }
    }

    /// <summary>
    /// Monitor multiple subreddits simultaneously and return aggregated results
    /// </summary>
    public async Task<MultiSubredditResult> MonitorMultipleSubredditsAsync(IEnumerable<string> subreddits, int postsPerSubreddit = 25)
    {
        try
        {
            var result = new MultiSubredditResult
            {
                MonitoredSubreddits = subreddits.ToList(),
                MonitoringDate = DateTime.UtcNow,
                SubredditResults = new Dictionary<string, List<RedditPost>>()
            };

            var tasks = subreddits.Select(async subreddit =>
            {
                try
                {
                    var posts = await ScrapeSubredditAsync(subreddit, postsPerSubreddit);
                    return new { Subreddit = subreddit, Posts = posts };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to scrape r/{Subreddit} during multi-subreddit monitoring", subreddit);
                    return new { Subreddit = subreddit, Posts = new List<RedditPost>() };
                }
            });

            var results = await Task.WhenAll(tasks);

            foreach (var subredditResult in results)
            {
                result.SubredditResults[subredditResult.Subreddit] = subredditResult.Posts;
            }

            // Calculate aggregate statistics
            var allPosts = result.SubredditResults.Values.SelectMany(posts => posts).ToList();
            result.TotalPosts = allPosts.Count;
            
            if (allPosts.Any())
            {
                result.AverageScore = allPosts.Average(p => p.Score);
                result.TotalEngagement = allPosts.Sum(p => p.Upvotes + p.Comments);
                result.TopPostOverall = allPosts.OrderByDescending(p => p.Score).First();
                result.MostDiscussedPost = allPosts.OrderByDescending(p => p.Comments).First();
            }

            _logger.LogInformation("Successfully monitored {Count} subreddits with {TotalPosts} total posts", 
                subreddits.Count(), result.TotalPosts);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to monitor multiple subreddits");
            return new MultiSubredditResult
            {
                MonitoredSubreddits = subreddits.ToList(),
                MonitoringDate = DateTime.UtcNow,
                SubredditResults = new Dictionary<string, List<RedditPost>>()
            };
        }
    }

    /// <summary>
    /// Search for a specific term across multiple subreddits
    /// </summary>
    public async Task<MultiSubredditSearchResult> SearchAcrossSubredditsAsync(IEnumerable<string> subreddits, string searchTerm, int resultsPerSubreddit = 10)
    {
        try
        {
            var result = new MultiSubredditSearchResult
            {
                SearchTerm = searchTerm,
                SearchDate = DateTime.UtcNow,
                SearchedSubreddits = subreddits.ToList(),
                Results = new Dictionary<string, List<RedditPost>>()
            };

            var tasks = subreddits.Select(async subreddit =>
            {
                try
                {
                    var posts = await SearchSubredditAsync(subreddit, searchTerm, resultsPerSubreddit);
                    return new { Subreddit = subreddit, Posts = posts };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to search r/{Subreddit} for '{SearchTerm}'", subreddit, searchTerm);
                    return new { Subreddit = subreddit, Posts = new List<RedditPost>() };
                }
            });

            var searchResults = await Task.WhenAll(tasks);

            foreach (var subredditResult in searchResults)
            {
                result.Results[subredditResult.Subreddit] = subredditResult.Posts;
            }

            // Calculate aggregate statistics
            var allPosts = result.Results.Values.SelectMany(posts => posts).ToList();
            result.TotalResultsFound = allPosts.Count;
            
            if (allPosts.Any())
            {
                result.MostRelevantPost = allPosts.OrderByDescending(p => p.Score).First();
                result.SubredditsWithResults = result.Results.Where(kvp => kvp.Value.Any()).Select(kvp => kvp.Key).ToList();
            }

            _logger.LogInformation("Search for '{SearchTerm}' across {Count} subreddits found {TotalResults} results", 
                searchTerm, subreddits.Count(), result.TotalResultsFound);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search across multiple subreddits for '{SearchTerm}'", searchTerm);
            return new MultiSubredditSearchResult
            {
                SearchTerm = searchTerm,
                SearchDate = DateTime.UtcNow,
                SearchedSubreddits = subreddits.ToList(),
                Results = new Dictionary<string, List<RedditPost>>()
            };
        }
    }
    private static string GetUa() => UserAgents[_rng.Next(UserAgents.Length)];
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