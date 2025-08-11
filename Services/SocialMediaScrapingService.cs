using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using HtmlAgilityPack;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuantResearchAgent.Services;

public class SocialMediaScrapingService
{
    private readonly ILogger<SocialMediaScrapingService> _logger;
    private readonly HttpClient _httpClient;
    private readonly Kernel _kernel;

    public SocialMediaScrapingService(
        ILogger<SocialMediaScrapingService> logger,
        HttpClient httpClient,
        Kernel kernel)
    {
        _logger = logger;
        _httpClient = httpClient;
        _kernel = kernel;
        
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    }

    public async Task<SocialMediaAnalysisResult> AnalyzeSocialMediaSentimentAsync(string symbol, List<SocialMediaPlatform> platforms, int daysBack = 7)
    {
        try
        {
            _logger.LogInformation("Analyzing social media sentiment for {Symbol} across {PlatformCount} platforms", 
                symbol, platforms.Count);

            var result = new SocialMediaAnalysisResult
            {
                Symbol = symbol,
                AnalysisDate = DateTime.UtcNow,
                TimeRange = TimeSpan.FromDays(daysBack),
                PlatformAnalyses = new List<PlatformAnalysis>(),
                OverallMetrics = new SocialMediaMetrics(),
                TrendingTopics = new List<TrendingTopic>(),
                InfluencerMentions = new List<InfluencerMention>(),
                SentimentTimeline = new List<SentimentDataPoint>()
            };

            // Analyze each platform
            foreach (var platform in platforms)
            {
                var platformAnalysis = await AnalyzePlatform(symbol, platform, daysBack);
                result.PlatformAnalyses.Add(platformAnalysis);
            }

            // Generate aggregate metrics
            result.OverallMetrics = CalculateOverallMetrics(result.PlatformAnalyses);

            // Extract trending topics
            result.TrendingTopics = ExtractTrendingTopics(result.PlatformAnalyses);

            // Generate sentiment timeline
            result.SentimentTimeline = GenerateSentimentTimeline(result.PlatformAnalyses, daysBack);

            // Generate AI-powered insights
            result.AIInsights = await GenerateAIInsights(result);

            _logger.LogInformation("Completed social media analysis for {Symbol}. Found {PostCount} posts with {SentimentScore:F2} sentiment score", 
                symbol, result.OverallMetrics.TotalPosts, result.OverallMetrics.OverallSentimentScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze social media sentiment for {Symbol}", symbol);
            throw;
        }
    }

    public async Task<List<ViralContent>> DetectViralContentAsync(string symbol, SocialMediaPlatform platform, int hoursBack = 24)
    {
        try
        {
            _logger.LogInformation("Detecting viral content for {Symbol} on {Platform}", symbol, platform);

            var viralContent = new List<ViralContent>();
            var posts = await ScrapePlatformPosts(symbol, platform, hoursBack / 24.0);

            // Identify viral content based on engagement metrics
            foreach (var post in posts)
            {
                var viralScore = CalculateViralScore(post);
                
                if (viralScore > 0.7) // High threshold for viral content
                {
                    viralContent.Add(new ViralContent
                    {
                        Platform = platform,
                        Post = post,
                        ViralScore = viralScore,
                        EstimatedReach = EstimateReach(post, platform),
                        EngagementRate = CalculateEngagementRate(post),
                        VelocityScore = CalculateVelocityScore(post),
                        PotentialImpact = await AssessPotentialImpact(post, symbol)
                    });
                }
            }

            viralContent = viralContent.OrderByDescending(v => v.ViralScore).ToList();

            _logger.LogInformation("Found {ViralCount} viral content pieces for {Symbol} on {Platform}", 
                viralContent.Count, symbol, platform);

            return viralContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect viral content for {Symbol} on {Platform}", symbol, platform);
            return new List<ViralContent>();
        }
    }

    public async Task<InfluencerAnalysis> AnalyzeInfluencerMentionsAsync(string symbol, SocialMediaPlatform platform)
    {
        try
        {
            _logger.LogInformation("Analyzing influencer mentions for {Symbol} on {Platform}", symbol, platform);

            var posts = await ScrapePlatformPosts(symbol, platform, 7);
            var influencerMentions = new List<InfluencerMention>();

            foreach (var post in posts)
            {
                if (IsInfluencer(post.Author))
                {
                    var mention = new InfluencerMention
                    {
                        InfluencerHandle = post.Author.Username,
                        FollowerCount = post.Author.FollowerCount,
                        Post = post,
                        InfluenceScore = CalculateInfluenceScore(post.Author),
                        SentimentScore = await AnalyzePostSentiment(post.Content),
                        EstimatedReach = EstimateInfluencerReach(post.Author, post)
                    };

                    influencerMentions.Add(mention);
                }
            }

            var analysis = new InfluencerAnalysis
            {
                Symbol = symbol,
                Platform = platform,
                AnalysisDate = DateTime.UtcNow,
                TotalInfluencerMentions = influencerMentions.Count,
                TopInfluencers = influencerMentions
                    .OrderByDescending(i => i.InfluenceScore)
                    .Take(10)
                    .ToList(),
                AverageInfluencerSentiment = influencerMentions.Any() 
                    ? influencerMentions.Average(i => i.SentimentScore)
                    : 0,
                TotalEstimatedReach = influencerMentions.Sum(i => i.EstimatedReach),
                SentimentDistribution = CalculateSentimentDistribution(influencerMentions)
            };

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze influencer mentions for {Symbol} on {Platform}", symbol, platform);
            return new InfluencerAnalysis { Symbol = symbol, Platform = platform };
        }
    }

    private async Task<PlatformAnalysis> AnalyzePlatform(string symbol, SocialMediaPlatform platform, int daysBack)
    {
        _logger.LogInformation("Analyzing {Platform} for {Symbol}", platform, symbol);

        var posts = await ScrapePlatformPosts(symbol, platform, daysBack);
        
        var analysis = new PlatformAnalysis
        {
            Platform = platform,
            PostCount = posts.Count,
            Posts = posts,
            SentimentScore = await CalculateAverageSentiment(posts),
            EngagementMetrics = CalculateEngagementMetrics(posts),
            TopHashtags = ExtractTopHashtags(posts),
            KeyAuthors = IdentifyKeyAuthors(posts),
            PostFrequency = CalculatePostFrequency(posts, daysBack)
        };

        return analysis;
    }

    private async Task<List<SocialMediaPost>> ScrapePlatformPosts(string symbol, SocialMediaPlatform platform, double daysBack)
    {
        var posts = new List<SocialMediaPost>();
        
        try
        {
            switch (platform)
            {
                case SocialMediaPlatform.Twitter:
                    posts = await ScrapeTwitterPosts(symbol, daysBack);
                    break;
                case SocialMediaPlatform.Reddit:
                    posts = await ScrapeRedditPosts(symbol, daysBack);
                    break;
                case SocialMediaPlatform.StockTwits:
                    posts = await ScrapeStockTwitsPosts(symbol, daysBack);
                    break;
                case SocialMediaPlatform.Discord:
                    posts = await ScrapeDiscordPosts(symbol, daysBack);
                    break;
                case SocialMediaPlatform.LinkedIn:
                    posts = await ScrapeLinkedInPosts(symbol, daysBack);
                    break;
                default:
                    _logger.LogWarning("Platform {Platform} not supported", platform);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape {Platform} posts for {Symbol}", platform, symbol);
        }

        return posts;
    }

    private async Task<List<SocialMediaPost>> ScrapeTwitterPosts(string symbol, double daysBack)
    {
        // Note: Twitter API requires authentication and has rate limits
        // This is a simplified implementation
        
        var posts = new List<SocialMediaPost>();
        
        try
        {
            // Simulate Twitter scraping
            var random = new Random();
            var postCount = random.Next(50, 200);
            
            for (int i = 0; i < postCount; i++)
            {
                posts.Add(new SocialMediaPost
                {
                    Id = Guid.NewGuid().ToString(),
                    Content = GenerateMockTweetContent(symbol),
                    Author = GenerateMockAuthor(),
                    CreatedAt = DateTime.UtcNow.AddHours(-random.NextDouble() * daysBack * 24),
                    Likes = random.Next(0, 1000),
                    Retweets = random.Next(0, 100),
                    Comments = random.Next(0, 50),
                    Platform = SocialMediaPlatform.Twitter,
                    Hashtags = GenerateMockHashtags(symbol),
                    Mentions = new List<string> { $"@{symbol}" }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape Twitter posts for {Symbol}", symbol);
        }
        
        return posts;
    }

    private async Task<List<SocialMediaPost>> ScrapeRedditPosts(string symbol, double daysBack)
    {
        var posts = new List<SocialMediaPost>();
        
        try
        {
            // Reddit JSON API endpoints
            var subreddits = new[] { "wallstreetbets", "investing", "stocks", "SecurityAnalysis" };
            
            foreach (var subreddit in subreddits)
            {
                var url = $"https://www.reddit.com/r/{subreddit}/search.json?q={symbol}&sort=new&t=week";
                
                try
                {
                    var response = await _httpClient.GetStringAsync(url);
                    var redditData = JsonSerializer.Deserialize<JsonElement>(response);
                    
                    if (redditData.TryGetProperty("data", out var data) && 
                        data.TryGetProperty("children", out var children))
                    {
                        foreach (var child in children.EnumerateArray())
                        {
                            if (child.TryGetProperty("data", out var postData))
                            {
                                var post = ParseRedditPost(postData, subreddit);
                                if (post != null && post.Content.Contains(symbol, StringComparison.OrdinalIgnoreCase))
                                {
                                    posts.Add(post);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to scrape r/{Subreddit}: {Error}", subreddit, ex.Message);
                }
                
                await Task.Delay(1000); // Rate limiting
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape Reddit posts for {Symbol}", symbol);
        }
        
        return posts;
    }

    private async Task<List<SocialMediaPost>> ScrapeStockTwitsPosts(string symbol, double daysBack)
    {
        var posts = new List<SocialMediaPost>();
        
        try
        {
            // StockTwits has a public API
            var url = $"https://api.stocktwits.com/api/2/streams/symbol/{symbol}.json";
            
            var response = await _httpClient.GetStringAsync(url);
            var stockTwitsData = JsonSerializer.Deserialize<JsonElement>(response);
            
            if (stockTwitsData.TryGetProperty("messages", out var messages))
            {
                foreach (var message in messages.EnumerateArray())
                {
                    var post = ParseStockTwitsPost(message);
                    if (post != null)
                    {
                        posts.Add(post);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape StockTwits posts for {Symbol}", symbol);
        }
        
        return posts;
    }

    private async Task<List<SocialMediaPost>> ScrapeDiscordPosts(string symbol, double daysBack)
    {
        // Discord scraping requires bot access or API keys
        // This is a mock implementation
        return new List<SocialMediaPost>();
    }

    private async Task<List<SocialMediaPost>> ScrapeLinkedInPosts(string symbol, double daysBack)
    {
        // LinkedIn has strict anti-scraping measures
        // This would require LinkedIn API access
        return new List<SocialMediaPost>();
    }

    private SocialMediaPost? ParseRedditPost(JsonElement postData, string subreddit)
    {
        try
        {
            return new SocialMediaPost
            {
                Id = postData.GetProperty("id").GetString() ?? "",
                Content = postData.GetProperty("title").GetString() + " " + 
                         postData.GetProperty("selftext").GetString(),
                Author = new SocialMediaAuthor
                {
                    Username = postData.GetProperty("author").GetString() ?? "",
                    FollowerCount = 0
                },
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(
                    (long)postData.GetProperty("created_utc").GetDouble()).DateTime,
                Likes = postData.GetProperty("ups").GetInt32(),
                Comments = postData.GetProperty("num_comments").GetInt32(),
                Platform = SocialMediaPlatform.Reddit,
                Url = "https://reddit.com" + postData.GetProperty("permalink").GetString()
            };
        }
        catch
        {
            return null;
        }
    }

    private SocialMediaPost? ParseStockTwitsPost(JsonElement message)
    {
        try
        {
            return new SocialMediaPost
            {
                Id = message.GetProperty("id").GetString() ?? "",
                Content = message.GetProperty("body").GetString() ?? "",
                Author = new SocialMediaAuthor
                {
                    Username = message.GetProperty("user").GetProperty("username").GetString() ?? "",
                    FollowerCount = message.GetProperty("user").GetProperty("followers").GetInt32()
                },
                CreatedAt = DateTime.Parse(message.GetProperty("created_at").GetString() ?? ""),
                Likes = message.GetProperty("likes").GetProperty("total").GetInt32(),
                Platform = SocialMediaPlatform.StockTwits
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<double> CalculateAverageSentiment(List<SocialMediaPost> posts)
    {
        if (!posts.Any()) return 0.5;

        var sentimentSum = 0.0;
        foreach (var post in posts)
        {
            var sentiment = await AnalyzePostSentiment(post.Content);
            sentimentSum += sentiment;
        }

        return sentimentSum / posts.Count;
    }

    private async Task<double> AnalyzePostSentiment(string content)
    {
        // Simple keyword-based sentiment analysis
        var positiveKeywords = new[] { "bullish", "buy", "moon", "rocket", "gains", "up", "strong", "good", "great", "excellent" };
        var negativeKeywords = new[] { "bearish", "sell", "dump", "crash", "down", "weak", "bad", "terrible", "awful" };

        var contentLower = content.ToLower();
        var positiveCount = positiveKeywords.Count(keyword => contentLower.Contains(keyword));
        var negativeCount = negativeKeywords.Count(keyword => contentLower.Contains(keyword));

        if (positiveCount + negativeCount == 0) return 0.5; // Neutral

        return (double)positiveCount / (positiveCount + negativeCount);
    }

    private EngagementMetrics CalculateEngagementMetrics(List<SocialMediaPost> posts)
    {
        if (!posts.Any())
        {
            return new EngagementMetrics();
        }

        return new EngagementMetrics
        {
            TotalLikes = posts.Sum(p => p.Likes),
            TotalComments = posts.Sum(p => p.Comments),
            TotalShares = posts.Sum(p => p.Retweets),
            AverageEngagementRate = posts.Average(p => CalculateEngagementRate(p)),
            EngagementTrend = CalculateEngagementTrend(posts)
        };
    }

    private double CalculateEngagementRate(SocialMediaPost post)
    {
        var totalEngagement = post.Likes + post.Comments + post.Retweets;
        var followerCount = Math.Max(post.Author.FollowerCount, 100); // Minimum baseline
        return (double)totalEngagement / followerCount;
    }

    private string CalculateEngagementTrend(List<SocialMediaPost> posts)
    {
        if (posts.Count < 2) return "Stable";

        var recent = posts.Where(p => p.CreatedAt > DateTime.UtcNow.AddDays(-1)).ToList();
        var older = posts.Where(p => p.CreatedAt <= DateTime.UtcNow.AddDays(-1)).ToList();

        if (!recent.Any() || !older.Any()) return "Stable";

        var recentAvg = recent.Average(p => CalculateEngagementRate(p));
        var olderAvg = older.Average(p => CalculateEngagementRate(p));

        var change = (recentAvg - olderAvg) / olderAvg;

        return change switch
        {
            > 0.2 => "Increasing",
            < -0.2 => "Decreasing", 
            _ => "Stable"
        };
    }

    private List<string> ExtractTopHashtags(List<SocialMediaPost> posts)
    {
        var hashtagCount = new Dictionary<string, int>();
        
        foreach (var post in posts)
        {
            foreach (var hashtag in post.Hashtags)
            {
                hashtagCount[hashtag] = hashtagCount.GetValueOrDefault(hashtag, 0) + 1;
            }
        }

        return hashtagCount
            .OrderByDescending(h => h.Value)
            .Take(10)
            .Select(h => h.Key)
            .ToList();
    }

    private List<SocialMediaAuthor> IdentifyKeyAuthors(List<SocialMediaPost> posts)
    {
        return posts
            .GroupBy(p => p.Author.Username)
            .OrderByDescending(g => g.Sum(p => p.Likes + p.Comments + p.Retweets))
            .Take(10)
            .Select(g => g.First().Author)
            .ToList();
    }

    private double CalculatePostFrequency(List<SocialMediaPost> posts, int daysBack)
    {
        return posts.Count / (double)daysBack;
    }

    private SocialMediaMetrics CalculateOverallMetrics(List<PlatformAnalysis> analyses)
    {
        if (!analyses.Any())
        {
            return new SocialMediaMetrics();
        }

        return new SocialMediaMetrics
        {
            TotalPosts = analyses.Sum(a => a.PostCount),
            OverallSentimentScore = analyses.Average(a => a.SentimentScore),
            TotalEngagement = analyses.Sum(a => a.EngagementMetrics.TotalLikes + 
                                                 a.EngagementMetrics.TotalComments + 
                                                 a.EngagementMetrics.TotalShares),
            AveragePostFrequency = analyses.Average(a => a.PostFrequency),
            SentimentDistribution = new Dictionary<string, double>
            {
                ["Positive"] = analyses.Average(a => a.Posts.Count(p => p.SentimentScore > 0.6)) / Math.Max(1, analyses.Average(a => a.PostCount)),
                ["Neutral"] = analyses.Average(a => a.Posts.Count(p => p.SentimentScore >= 0.4 && p.SentimentScore <= 0.6)) / Math.Max(1, analyses.Average(a => a.PostCount)),
                ["Negative"] = analyses.Average(a => a.Posts.Count(p => p.SentimentScore < 0.4)) / Math.Max(1, analyses.Average(a => a.PostCount))
            }
        };
    }

    private List<TrendingTopic> ExtractTrendingTopics(List<PlatformAnalysis> analyses)
    {
        var topicCount = new Dictionary<string, int>();
        
        foreach (var analysis in analyses)
        {
            foreach (var hashtag in analysis.TopHashtags)
            {
                topicCount[hashtag] = topicCount.GetValueOrDefault(hashtag, 0) + 1;
            }
        }

        return topicCount
            .OrderByDescending(t => t.Value)
            .Take(10)
            .Select(t => new TrendingTopic
            {
                Topic = t.Key,
                MentionCount = t.Value,
                TrendScore = CalculateTrendScore(t.Key, analyses)
            })
            .ToList();
    }

    private double CalculateTrendScore(string topic, List<PlatformAnalysis> analyses)
    {
        // Calculate trending score based on recent mentions vs historical
        return new Random().NextDouble(); // Simplified implementation
    }

    private List<SentimentDataPoint> GenerateSentimentTimeline(List<PlatformAnalysis> analyses, int daysBack)
    {
        var timeline = new List<SentimentDataPoint>();
        
        for (int i = 0; i < daysBack; i++)
        {
            var date = DateTime.UtcNow.AddDays(-i);
            var dayPosts = analyses.SelectMany(a => a.Posts)
                .Where(p => p.CreatedAt.Date == date.Date)
                .ToList();

            if (dayPosts.Any())
            {
                timeline.Add(new SentimentDataPoint
                {
                    Date = date,
                    SentimentScore = dayPosts.Average(p => p.SentimentScore),
                    PostCount = dayPosts.Count
                });
            }
        }

        return timeline.OrderBy(t => t.Date).ToList();
    }

    private async Task<List<string>> GenerateAIInsights(SocialMediaAnalysisResult result)
    {
        var prompt = $@"Analyze social media sentiment data for {result.Symbol}:

Total Posts: {result.OverallMetrics.TotalPosts}
Overall Sentiment: {result.OverallMetrics.OverallSentimentScore:F2}
Platforms: {string.Join(", ", result.PlatformAnalyses.Select(p => p.Platform))}
Top Topics: {string.Join(", ", result.TrendingTopics.Take(5).Select(t => t.Topic))}

Provide 3-5 key insights about social media sentiment and its potential impact on stock price.";

        try
        {
            var insights = await _kernel.InvokePromptAsync(prompt);
            return insights.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        catch
        {
            return new List<string>
            {
                $"Social media sentiment for {result.Symbol} shows {(result.OverallMetrics.OverallSentimentScore > 0.6 ? "positive" : result.OverallMetrics.OverallSentimentScore > 0.4 ? "neutral" : "negative")} overall sentiment",
                $"Total of {result.OverallMetrics.TotalPosts} posts analyzed across {result.PlatformAnalyses.Count} platforms",
                $"Engagement levels appear {(result.OverallMetrics.TotalEngagement > 1000 ? "high" : "moderate")} indicating {(result.OverallMetrics.TotalEngagement > 1000 ? "strong" : "moderate")} investor interest"
            };
        }
    }

    // Utility methods
    private string GenerateMockTweetContent(string symbol) =>
        $"Thoughts on ${symbol}? Looking {(new Random().NextDouble() > 0.5 ? "bullish" : "bearish")} based on recent analysis. #stocks #investing";

    private SocialMediaAuthor GenerateMockAuthor() =>
        new SocialMediaAuthor
        {
            Username = $"investor_{new Random().Next(1000, 9999)}",
            FollowerCount = new Random().Next(100, 10000)
        };

    private List<string> GenerateMockHashtags(string symbol) =>
        new List<string> { $"#{symbol}", "#stocks", "#investing", "#trading" };

    private double CalculateViralScore(SocialMediaPost post)
    {
        var engagement = post.Likes + post.Comments + post.Retweets;
        var followerRatio = engagement / Math.Max(post.Author.FollowerCount, 1.0);
        var timeDecay = Math.Exp(-(DateTime.UtcNow - post.CreatedAt).TotalHours / 24.0);
        
        return Math.Min(1.0, followerRatio * timeDecay * 10);
    }

    private int EstimateReach(SocialMediaPost post, SocialMediaPlatform platform) =>
        platform switch
        {
            SocialMediaPlatform.Twitter => post.Retweets * 50 + post.Author.FollowerCount,
            SocialMediaPlatform.Reddit => post.Likes * 10,
            SocialMediaPlatform.StockTwits => post.Likes * 20 + post.Author.FollowerCount,
            _ => post.Likes * 5
        };

    private double CalculateVelocityScore(SocialMediaPost post)
    {
        var hoursSincePost = (DateTime.UtcNow - post.CreatedAt).TotalHours;
        var engagement = post.Likes + post.Comments + post.Retweets;
        return engagement / Math.Max(hoursSincePost, 1.0);
    }

    private async Task<string> AssessPotentialImpact(SocialMediaPost post, string symbol)
    {
        var engagementLevel = CalculateEngagementRate(post);
        return engagementLevel switch
        {
            > 0.1 => "High potential market impact",
            > 0.05 => "Medium potential market impact",
            _ => "Low potential market impact"
        };
    }

    private bool IsInfluencer(SocialMediaAuthor author) =>
        author.FollowerCount > 10000 || 
        author.Username.Contains("trader") || 
        author.Username.Contains("analyst");

    private double CalculateInfluenceScore(SocialMediaAuthor author) =>
        Math.Min(1.0, author.FollowerCount / 100000.0);

    private int EstimateInfluencerReach(SocialMediaAuthor author, SocialMediaPost post) =>
        (int)(author.FollowerCount * 0.1) + post.Retweets * 20;

    private Dictionary<string, double> CalculateSentimentDistribution(List<InfluencerMention> mentions)
    {
        if (!mentions.Any())
        {
            return new Dictionary<string, double> { ["Positive"] = 0, ["Neutral"] = 0, ["Negative"] = 0 };
        }

        var positive = mentions.Count(m => m.SentimentScore > 0.6);
        var neutral = mentions.Count(m => m.SentimentScore >= 0.4 && m.SentimentScore <= 0.6);
        var negative = mentions.Count(m => m.SentimentScore < 0.4);
        var total = mentions.Count;

        return new Dictionary<string, double>
        {
            ["Positive"] = positive / (double)total,
            ["Neutral"] = neutral / (double)total,
            ["Negative"] = negative / (double)total
        };
    }
}

// Data Models
public class SocialMediaAnalysisResult
{
    public string Symbol { get; set; } = "";
    public DateTime AnalysisDate { get; set; }
    public TimeSpan TimeRange { get; set; }
    public List<PlatformAnalysis> PlatformAnalyses { get; set; } = new();
    public SocialMediaMetrics OverallMetrics { get; set; } = new();
    public List<TrendingTopic> TrendingTopics { get; set; } = new();
    public List<InfluencerMention> InfluencerMentions { get; set; } = new();
    public List<SentimentDataPoint> SentimentTimeline { get; set; } = new();
    public List<string> AIInsights { get; set; } = new();
}

public class PlatformAnalysis
{
    public SocialMediaPlatform Platform { get; set; }
    public int PostCount { get; set; }
    public List<SocialMediaPost> Posts { get; set; } = new();
    public double SentimentScore { get; set; }
    public EngagementMetrics EngagementMetrics { get; set; } = new();
    public List<string> TopHashtags { get; set; } = new();
    public List<SocialMediaAuthor> KeyAuthors { get; set; } = new();
    public double PostFrequency { get; set; }
}

public class SocialMediaPost
{
    public string Id { get; set; } = "";
    public string Content { get; set; } = "";
    public SocialMediaAuthor Author { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public int Likes { get; set; }
    public int Retweets { get; set; }
    public int Comments { get; set; }
    public SocialMediaPlatform Platform { get; set; }
    public List<string> Hashtags { get; set; } = new();
    public List<string> Mentions { get; set; } = new();
    public string Url { get; set; } = "";
    public double SentimentScore { get; set; }
}

public class SocialMediaAuthor
{
    public string Username { get; set; } = "";
    public int FollowerCount { get; set; }
    public bool IsVerified { get; set; }
    public string Bio { get; set; } = "";
}

public class EngagementMetrics
{
    public int TotalLikes { get; set; }
    public int TotalComments { get; set; }
    public int TotalShares { get; set; }
    public double AverageEngagementRate { get; set; }
    public string EngagementTrend { get; set; } = "";
}

public class SocialMediaMetrics
{
    public int TotalPosts { get; set; }
    public double OverallSentimentScore { get; set; }
    public int TotalEngagement { get; set; }
    public double AveragePostFrequency { get; set; }
    public Dictionary<string, double> SentimentDistribution { get; set; } = new();
}

public class TrendingTopic
{
    public string Topic { get; set; } = "";
    public int MentionCount { get; set; }
    public double TrendScore { get; set; }
}

public class InfluencerMention
{
    public string InfluencerHandle { get; set; } = "";
    public int FollowerCount { get; set; }
    public SocialMediaPost Post { get; set; } = new();
    public double InfluenceScore { get; set; }
    public double SentimentScore { get; set; }
    public int EstimatedReach { get; set; }
}

public class SentimentDataPoint
{
    public DateTime Date { get; set; }
    public double SentimentScore { get; set; }
    public int PostCount { get; set; }
}

public class ViralContent
{
    public SocialMediaPlatform Platform { get; set; }
    public SocialMediaPost Post { get; set; } = new();
    public double ViralScore { get; set; }
    public int EstimatedReach { get; set; }
    public double EngagementRate { get; set; }
    public double VelocityScore { get; set; }
    public string PotentialImpact { get; set; } = "";
}

public class InfluencerAnalysis
{
    public string Symbol { get; set; } = "";
    public SocialMediaPlatform Platform { get; set; }
    public DateTime AnalysisDate { get; set; }
    public int TotalInfluencerMentions { get; set; }
    public List<InfluencerMention> TopInfluencers { get; set; } = new();
    public double AverageInfluencerSentiment { get; set; }
    public int TotalEstimatedReach { get; set; }
    public Dictionary<string, double> SentimentDistribution { get; set; } = new();
}

public enum SocialMediaPlatform
{
    Twitter,
    Reddit,
    StockTwits,
    Discord,
    LinkedIn,
    TikTok,
    YouTube
}
