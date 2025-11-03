using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using HtmlAgilityPack;
using Reddit;
using Reddit.Controllers;

namespace QuantResearchAgent.Services;

public class RedditScrapingService
{
    private readonly ILogger<RedditScrapingService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly RedditClient? _redditClient;

    public RedditScrapingService(ILogger<RedditScrapingService> logger, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
        
        // Initialize Reddit API client if credentials are available
        var clientId = _configuration["Reddit:ClientId"];
        var clientSecret = _configuration["Reddit:ClientSecret"];
        var userAgent = _configuration["Reddit:UserAgent"] ?? "QuantResearchAgent/1.0";

        if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
        {
            try
            {
                _redditClient = new RedditClient(clientId, clientSecret, userAgent);
                _logger.LogInformation("Reddit API client initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Reddit API client, falling back to web scraping");
                _redditClient = null;
            }
        }
        else
        {
            _logger.LogWarning("Reddit API credentials not found, using web scraping fallback");
        }
        
        // Set user agent for HTTP client fallback
        _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
    }

    public async Task<List<RedditPost>> ScrapeSubredditAsync(string subreddit, int limit = 25)
    {
        try
        {
            if (_redditClient != null)
            {
                return await ScrapeSubredditWithApiAsync(subreddit, limit);
            }
            else
            {
                return await ScrapeSubredditWithHttpAsync(subreddit, limit);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape r/{Subreddit}", subreddit);
            return new List<RedditPost>();
        }
    }

    private async Task<List<RedditPost>> ScrapeSubredditWithApiAsync(string subreddit, int limit)
    {
        try
        {
            var posts = new List<RedditPost>();
            _logger.LogInformation("Scraping r/{Subreddit} using Reddit API for {Limit} posts", subreddit, limit);

            var subredditController = _redditClient!.Subreddit(subreddit);
            var hotPosts = subredditController.Posts.Hot.Take(limit);

            foreach (var post in hotPosts)
            {
                var redditPost = new RedditPost
                {
                    Title = post.Title ?? "",
                    Author = post.Author ?? "",
                    Score = post.Score,
                    Upvotes = post.UpVotes,
                    Downvotes = post.DownVotes,
                    Comments = 0, // Will need to check correct property
                    CreatedUtc = post.Created,
                    Url = $"https://reddit.com{post.Permalink}",
                    Subreddit = subreddit,
                    Content = "", // Will need to check correct property
                    Flair = "" // Will need to check correct property
                };
                
                posts.Add(redditPost);
            }

            _logger.LogInformation("Successfully scraped {Count} posts from r/{Subreddit} using API", posts.Count, subreddit);
            return posts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape r/{Subreddit} using Reddit API", subreddit);
            throw;
        }
    }

    private async Task<List<RedditPost>> ScrapeSubredditWithHttpAsync(string subreddit, int limit)
    {
        try
        {
            var posts = new List<RedditPost>();
            var url = $"https://www.reddit.com/r/{subreddit}/hot.json?limit={limit}";

            _logger.LogInformation("Scraping r/{Subreddit} using HTTP fallback for {Limit} posts", subreddit, limit);

            var response = await _httpClient.GetStringAsync(url);
            _logger.LogDebug("Received response from Reddit API, length: {Length}", response.Length);

            var jsonDoc = JsonDocument.Parse(response);

            if (!jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
            {
                _logger.LogError("Reddit API response does not contain 'data' property");
                return posts;
            }

            if (!dataElement.TryGetProperty("children", out var childrenElement))
            {
                _logger.LogError("Reddit API response does not contain 'children' property in data");
                return posts;
            }

            foreach (var child in childrenElement.EnumerateArray())
            {
                if (!child.TryGetProperty("data", out var postData))
                {
                    _logger.LogWarning("Child element does not contain 'data' property, skipping");
                    continue;
                }

                var post = new RedditPost
                {
                    Title = postData.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                    Author = postData.TryGetProperty("author", out var author) ? author.GetString() ?? "" : "",
                    Score = postData.TryGetProperty("score", out var score) ? score.GetInt32() : 0,
                    Upvotes = postData.TryGetProperty("ups", out var ups) ? ups.GetInt32() : 0,
                    Downvotes = postData.TryGetProperty("downs", out var downs) ? downs.GetInt32() : 0,
                    Comments = postData.TryGetProperty("num_comments", out var comments) ? comments.GetInt32() : 0,
                    CreatedUtc = postData.TryGetProperty("created_utc", out var created) ? 
                        (created.ValueKind == JsonValueKind.Number ? 
                            DateTimeOffset.FromUnixTimeSeconds((long)created.GetDouble()).DateTime : 
                            DateTime.UtcNow) : 
                        DateTime.UtcNow,
                    Url = postData.TryGetProperty("permalink", out var permalink) ? $"https://reddit.com{permalink.GetString()}" : "",
                    Subreddit = subreddit,
                    Content = postData.TryGetProperty("selftext", out var selftext) ? selftext.GetString() ?? "" : "",
                    Flair = postData.TryGetProperty("link_flair_text", out var flair) ? flair.GetString() ?? "" : ""
                };

                posts.Add(post);
            }

            _logger.LogInformation("Successfully scraped {Count} posts from r/{Subreddit} using HTTP", posts.Count, subreddit);
            return posts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape r/{Subreddit} using HTTP fallback", subreddit);
            throw;
        }
    }

    public async Task<List<RedditPost>> SearchSubredditAsync(string subreddit, string query, int limit = 25)
    {
        try
        {
            if (_redditClient != null)
            {
                return await SearchSubredditWithApiAsync(subreddit, query, limit);
            }
            else
            {
                return await SearchSubredditWithHttpAsync(subreddit, query, limit);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search r/{Subreddit} for '{Query}'", subreddit, query);
            return new List<RedditPost>();
        }
    }

    private async Task<List<RedditPost>> SearchSubredditWithApiAsync(string subreddit, string query, int limit)
    {
        try
        {
            var posts = new List<RedditPost>();
            _logger.LogInformation("Searching r/{Subreddit} for '{Query}' using Reddit API with {Limit} results", subreddit, query, limit);

            var subredditController = _redditClient!.Subreddit(subreddit);
            var searchResults = subredditController.Search(query, limit: limit);

            foreach (var post in searchResults.Take(limit))
            {
                var redditPost = new RedditPost
                {
                    Title = post.Title ?? "",
                    Author = post.Author ?? "",
                    Score = post.Score,
                    Upvotes = post.UpVotes,
                    Downvotes = post.DownVotes,
                    Comments = 0, // Will need to check correct property
                    CreatedUtc = post.Created,
                    Url = $"https://reddit.com{post.Permalink}",
                    Subreddit = subreddit,
                    Content = "", // Will need to check correct property
                    Flair = "", // Will need to check correct property
                    SearchQuery = query
                };
                
                posts.Add(redditPost);
            }

            _logger.LogInformation("Successfully found {Count} posts matching '{Query}' in r/{Subreddit} using API", posts.Count, query, subreddit);
            return posts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search r/{Subreddit} for '{Query}' using Reddit API", subreddit, query);
            throw;
        }
    }

    private async Task<List<RedditPost>> SearchSubredditWithHttpAsync(string subreddit, string query, int limit)
    {
        try
        {
            var posts = new List<RedditPost>();
            var url = $"https://www.reddit.com/r/{subreddit}/search.json?q={Uri.EscapeDataString(query)}&restrict_sr=1&limit={limit}&sort=relevance";
            
            _logger.LogInformation("Searching r/{Subreddit} for '{Query}' using HTTP fallback with {Limit} results", subreddit, query, limit);
            
            var response = await _httpClient.GetStringAsync(url);
            var jsonDoc = JsonDocument.Parse(response);
            
            var data = jsonDoc.RootElement.GetProperty("data");
            var children = data.GetProperty("children");
            
            foreach (var child in children.EnumerateArray())
            {
                var postData = child.GetProperty("data");
                
                var post = new RedditPost
                {
                    Title = postData.GetProperty("title").GetString() ?? "",
                    Author = postData.GetProperty("author").GetString() ?? "",
                    Score = postData.GetProperty("score").GetInt32(),
                    Upvotes = postData.GetProperty("ups").GetInt32(),
                    Downvotes = postData.GetProperty("downs").GetInt32(),
                    Comments = postData.GetProperty("num_comments").GetInt32(),
                    CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(postData.GetProperty("created_utc").GetInt64()).DateTime,
                    Url = $"https://reddit.com{postData.GetProperty("permalink").GetString()}",
                    Subreddit = subreddit,
                    Content = postData.TryGetProperty("selftext", out var selftext) ? selftext.GetString() ?? "" : "",
                    Flair = postData.TryGetProperty("link_flair_text", out var flair) ? flair.GetString() ?? "" : "",
                    SearchQuery = query
                };
                
                posts.Add(post);
            }
            
            _logger.LogInformation("Successfully found {Count} posts matching '{Query}' in r/{Subreddit} using HTTP", posts.Count, query, subreddit);
            return posts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search r/{Subreddit} for '{Query}' using HTTP fallback", subreddit, query);
            throw;
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
}

public class RedditPost
{
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
