using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using HtmlAgilityPack;

namespace QuantResearchAgent.Services;

public class RedditScrapingService
{
    private readonly ILogger<RedditScrapingService> _logger;
    private readonly HttpClient _httpClient;

    public RedditScrapingService(ILogger<RedditScrapingService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        
        // Set user agent to avoid blocking
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    }

    public async Task<List<RedditPost>> ScrapeSubredditAsync(string subreddit, int limit = 25)
    {
        try
        {
            var posts = new List<RedditPost>();
            var url = $"https://www.reddit.com/r/{subreddit}/hot.json?limit={limit}";
            
            _logger.LogInformation("Scraping r/{Subreddit} for {Limit} posts", subreddit, limit);
            
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
                    Flair = postData.TryGetProperty("link_flair_text", out var flair) ? flair.GetString() ?? "" : ""
                };
                
                posts.Add(post);
            }
            
            _logger.LogInformation("Successfully scraped {Count} posts from r/{Subreddit}", posts.Count, subreddit);
            return posts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape r/{Subreddit}", subreddit);
            return new List<RedditPost>();
        }
    }

    public async Task<List<RedditPost>> SearchSubredditAsync(string subreddit, string query, int limit = 25)
    {
        try
        {
            var posts = new List<RedditPost>();
            var url = $"https://www.reddit.com/r/{subreddit}/search.json?q={Uri.EscapeDataString(query)}&restrict_sr=1&limit={limit}&sort=relevance";
            
            _logger.LogInformation("Searching r/{Subreddit} for '{Query}' with {Limit} results", subreddit, query, limit);
            
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
            
            _logger.LogInformation("Successfully found {Count} posts matching '{Query}' in r/{Subreddit}", posts.Count, query, subreddit);
            return posts;
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
