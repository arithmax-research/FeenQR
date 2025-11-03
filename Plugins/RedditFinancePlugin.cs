using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;
using System.Text.Json;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Reddit Plugin - Specialized for monitoring financial subreddits: wallstreetbets, quant, quantfinance, and crypto
/// </summary>
public class RedditFinancePlugin
{
    private readonly RedditScrapingService _redditService;
    private readonly StrategyGeneratorService _strategyGenerator;
    private readonly ILogger<RedditFinancePlugin> _logger;
    
    // Target subreddits for financial analysis
    private readonly string[] _targetSubreddits = { "wallstreetbets", "quant", "quantfinance", "crypto" };

    public RedditFinancePlugin(RedditScrapingService redditService, StrategyGeneratorService strategyGenerator, ILogger<RedditFinancePlugin> logger)
    {
        _redditService = redditService;
        _strategyGenerator = strategyGenerator;
        _logger = logger;
    }

    [KernelFunction]
    [Description("Get trending posts from financial subreddits (wallstreetbets, quant, quantfinance, crypto)")]
    public async Task<string> GetTrendingFinancialPostsAsync(
        [Description("Number of posts per subreddit (default: 10)")] int postsPerSubreddit = 10,
        [Description("Specific subreddit to focus on (optional): wallstreetbets, quant, quantfinance, or crypto")] string? targetSubreddit = null)
    {
        try
        {
            var subredditsToCheck = string.IsNullOrEmpty(targetSubreddit) 
                ? _targetSubreddits 
                : new[] { targetSubreddit };

            var results = new List<string>();
            
            foreach (var subreddit in subredditsToCheck)
            {
                if (!_targetSubreddits.Contains(subreddit.ToLower()))
                {
                    _logger.LogWarning("Skipping non-financial subreddit: {Subreddit}", subreddit);
                    continue;
                }

                var posts = await _redditService.ScrapeSubredditAsync(subreddit, postsPerSubreddit);
                
                if (posts.Any())
                {
                    results.Add($"\n=== r/{subreddit} - Top {posts.Count} Posts ===");
                    
                    foreach (var post in posts.Take(postsPerSubreddit))
                    {
                        results.Add($"\n{post.Title}");
                        results.Add($"   u/{post.Author} | {post.Score} | {post.Comments} comments");
                        results.Add($"   {post.CreatedUtc:yyyy-MM-dd HH:mm} UTC");
                        
                        if (!string.IsNullOrEmpty(post.Flair))
                            results.Add($"   {post.Flair}");
                        
                        if (!string.IsNullOrEmpty(post.Content) && post.Content.Length > 0)
                        {
                            var preview = post.Content.Length > 200 
                                ? post.Content.Substring(0, 200) + "..." 
                                : post.Content;
                            results.Add($"  {preview}");
                        }
                        
                        results.Add($"   {post.Url}");
                    }
                }
                else
                {
                    results.Add($"\n=== r/{subreddit} ===");
                    results.Add("No posts found or subreddit unavailable.");
                }
            }

            return string.Join("\n", results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get trending financial posts");
            return "Error retrieving financial posts from Reddit. Please try again later.";
        }
    }

    [KernelFunction]
    [Description("Search for specific stock symbol or cryptocurrency mentions across financial subreddits")]
    public async Task<string> SearchSymbolMentionsAsync(
        [Description("Stock symbol or cryptocurrency (e.g., AAPL, BTC, TSLA)")] string symbol,
        [Description("Number of results per subreddit (default: 5)")] int resultsPerSubreddit = 5)
    {
        try
        {
            var results = new List<string>();
            results.Add($"Searching for '{symbol.ToUpper()}' mentions across financial subreddits...\n");

            foreach (var subreddit in _targetSubreddits)
            {
                _logger.LogInformation("Searching r/{Subreddit} for symbol {Symbol}", subreddit, symbol);
                var posts = await _redditService.SearchSubredditAsync(subreddit, symbol, resultsPerSubreddit);
                _logger.LogInformation("Found {Count} posts for {Symbol} in r/{Subreddit}", posts.Count, symbol, subreddit);
                
                if (posts.Any())
                {
                    results.Add($"=== r/{subreddit} - {posts.Count} mentions found ===");
                    
                    foreach (var post in posts)
                    {
                        results.Add($"\n{post.Title}");
                        results.Add($"   u/{post.Author} | {post.Score} | {post.Comments} comments");
                        results.Add($"   {post.CreatedUtc:yyyy-MM-dd HH:mm} UTC");
                        
                        if (!string.IsNullOrEmpty(post.Content) && post.Content.Length > 0)
                        {
                            var preview = post.Content.Length > 150 
                                ? post.Content.Substring(0, 150) + "..." 
                                : post.Content;
                            results.Add($"   {preview}");
                        }
                        
                        results.Add($"   {post.Url}");
                    }
                    results.Add("");
                }
            }

            if (results.Count == 1) // Only the search header was added
            {
                results.Add("No mentions found for this symbol across the monitored subreddits.");
            }

            return string.Join("\n", results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search for symbol mentions: {Symbol}", symbol);
            return $"Error searching for '{symbol}' mentions. Please try again later.";
        }
    }

    [KernelFunction]
    [Description("Analyze sentiment for a specific symbol across all financial subreddits")]
    public async Task<string> AnalyzeSymbolSentimentAsync(
        [Description("Stock symbol or cryptocurrency to analyze")] string symbol,
        [Description("Number of posts to analyze per subreddit (default: 20)")] int postsToAnalyze = 20)
    {
        try
        {
            var results = new List<string>();
            results.Add($"Sentiment Analysis for '{symbol.ToUpper()}' across financial subreddits\n");

            var overallStats = new
            {
                TotalPosts = 0,
                TotalUpvotes = 0,
                TotalDownvotes = 0,
                TotalComments = 0,
                PositiveKeywords = 0,
                NegativeKeywords = 0,
                SubredditAnalyses = new List<RedditSentimentAnalysis>()
            };

            foreach (var subreddit in _targetSubreddits)
            {
                var analysis = await _redditService.AnalyzeSubredditSentimentAsync(subreddit, symbol, postsToAnalyze);
                
                if (analysis.TotalPosts > 0)
                {
                    results.Add($"=== r/{subreddit} Analysis ===");
                    results.Add($"Posts Analyzed: {analysis.TotalPosts}");
                    results.Add($"Average Score: {analysis.AverageScore:F1}");
                    results.Add($"Total Engagement: {analysis.TotalUpvotes} | Downvotes {analysis.TotalDownvotes} | Comments {analysis.TotalComments}");
                    results.Add($"Sentiment: {analysis.OverallSentiment} (Score: {analysis.SentimentScore:F2})");
                    results.Add($"Keyword Analysis: {analysis.PositiveKeywordCount} positive | {analysis.NegativeKeywordCount} negative");
                    
                    if (analysis.TopPost != null)
                    {
                        results.Add($"Top Post: \"{analysis.TopPost.Title}\" ({analysis.TopPost.Score} points)");
                    }
                    
                    results.Add("");

                    // Aggregate stats
                    overallStats.GetType().GetProperty("TotalPosts")?.SetValue(overallStats, 
                        ((int)overallStats.GetType().GetProperty("TotalPosts")?.GetValue(overallStats)!) + analysis.TotalPosts);
                }
            }

            // Add overall summary
            if (overallStats.TotalPosts > 0)
            {
                results.Add("=== Overall Summary ===");
                results.Add($"Total posts analyzed: {overallStats.TotalPosts}");
                results.Add($"Subreddits covered: {_targetSubreddits.Length}");
                results.Add($"Analysis completed at: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
            }
            else
            {
                results.Add($"No recent discussions found for '{symbol}' in the monitored subreddits.");
            }

            return string.Join("\n", results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze sentiment for symbol: {Symbol}", symbol);
            return $"Error analyzing sentiment for '{symbol}'. Please try again later.";
        }
    }

    [KernelFunction]
    [Description("Get a comprehensive market pulse from all financial subreddits")]
    public async Task<string> GetMarketPulseAsync(
        [Description("Number of top posts per subreddit to analyze (default: 15)")] int postsPerSubreddit = 15)
    {
        try
        {
            var results = new List<string>();
            results.Add("Market Pulse - Financial Reddit Community Overview\n");
            results.Add($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n");

            var communityStats = new Dictionary<string, object>();

            foreach (var subreddit in _targetSubreddits)
            {
                var posts = await _redditService.ScrapeSubredditAsync(subreddit, postsPerSubreddit);
                
                if (posts.Any())
                {
                    var topPost = posts.OrderByDescending(p => p.Score).First();
                    var avgScore = posts.Average(p => p.Score);
                    var totalEngagement = posts.Sum(p => p.Comments + p.Upvotes);

                    results.Add($"=== r/{subreddit} Community Pulse ===");
                    results.Add($"Posts Analyzed: {posts.Count}");
                    results.Add($"Average Score: {avgScore:F1}");
                    results.Add($"Total Engagement: {totalEngagement:N0}");
                    results.Add($"Hottest Topic: \"{topPost.Title}\" ({topPost.Score} points, {topPost.Comments} comments)");
                    
                    // Identify trending topics by looking at common keywords in titles
                    var commonWords = posts
                        .SelectMany(p => p.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        .Where(w => w.Length > 3 && !IsCommonWord(w))
                        .GroupBy(w => w.ToLower())
                        .Where(g => g.Count() > 1)
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .Select(g => $"{g.Key} ({g.Count()})")
                        .ToList();

                    if (commonWords.Any())
                    {
                        results.Add($"Trending Keywords: {string.Join(", ", commonWords)}");
                    }
                    
                    results.Add("");
                }
            }

            results.Add("=== Quick Market Insights ===");
            results.Add("Use specific search commands to dive deeper into any trending topics or symbols.");
            results.Add("Tip: Try searching for specific tickers mentioned in the trending keywords above.");

            return string.Join("\n", results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get market pulse");
            return "Error generating market pulse. Please try again later.";
        }
    }

    [KernelFunction]
    [Description("Generate trading strategy based on Reddit sentiment and discussions using DeepSeek AI")]
    public async Task<string> GenerateStrategyFromRedditAsync(
        [Description("Specific subreddit to analyze (optional): wallstreetbets, quant, quantfinance, or crypto")] string? targetSubreddit = null,
        [Description("Number of posts to analyze (default: 20)")] int postsToAnalyze = 20)
    {
        try
        {
            // Get trending posts data
            string redditData = await GetTrendingFinancialPostsAsync(postsToAnalyze, targetSubreddit);
            
            // Generate strategy using DeepSeek
            string strategy = await _strategyGenerator.GenerateStrategyAsync(redditData, "Reddit");
            
            return $"Strategy generated from Reddit analysis:\n\n{strategy}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate strategy from Reddit");
            return "Error generating strategy from Reddit data. Please try again later.";
        }
    }

    /// <summary>
    /// Helper method to filter out common English words from trending analysis
    /// </summary>
    private static bool IsCommonWord(string word)
    {
        var commonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "are", "with", "this", "that", "from", "they", "will",
            "have", "what", "when", "where", "how", "why", "can", "should", "would",
            "about", "after", "before", "during", "while", "until", "since", "into"
        };
        
        return commonWords.Contains(word);
    }
}