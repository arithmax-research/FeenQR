using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;
using QuantResearchAgent.Services.ResearchAgents;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SentimentController : ControllerBase
{
    private readonly NewsSentimentAnalysisService _sentimentService;
    private readonly MarketSentimentAgentService _marketSentimentService;
    private readonly RedditScrapingService _redditService;
    private readonly ILogger<SentimentController> _logger;

    public SentimentController(
        NewsSentimentAnalysisService sentimentService,
        MarketSentimentAgentService marketSentimentService,
        RedditScrapingService redditService,
        ILogger<SentimentController> logger)
    {
        _sentimentService = sentimentService;
        _marketSentimentService = marketSentimentService;
        _redditService = redditService;
        _logger = logger;
    }

    [HttpGet("symbol/{symbol}")]
    public async Task<IActionResult> GetSymbolSentiment(string symbol, [FromQuery] int newsLimit = 10)
    {
        try
        {
            _logger.LogInformation($"Getting sentiment analysis for {symbol}");
            var analysis = await _sentimentService.AnalyzeSymbolSentimentAsync(symbol, newsLimit);
            
            return Ok(new
            {
                symbol = symbol,
                timestamp = DateTime.UtcNow,
                overallSentiment = analysis.OverallSentiment,
                sentimentScore = analysis.SentimentScore,
                sentimentLabel = analysis.OverallSentiment,
                confidence = analysis.Confidence,
                newsCount = analysis.NewsItems?.Count ?? 0,
                sourcesAnalyzed = analysis.NewsItems?.Select(n => n.Source).Distinct().ToArray() ?? Array.Empty<string>(),
                keyTopics = analysis.KeyThemes,
                sentimentBreakdown = new Dictionary<string, double>
                {
                    { "Positive", analysis.SentimentScore > 0 ? analysis.SentimentScore : 0 },
                    { "Negative", analysis.SentimentScore < 0 ? Math.Abs(analysis.SentimentScore) : 0 },
                    { "Neutral", Math.Abs(analysis.SentimentScore) < 0.1 ? 1.0 : 0 }
                },
                recentNews = analysis.NewsItems?.Take(5).Select(n => new
                {
                    title = n.Title,
                    source = n.Source,
                    publishedDate = n.PublishedDate,
                    sentiment = n.SentimentLabel,
                    sentimentScore = n.SentimentScore,
                    url = n.Link
                }),
                aiInsights = analysis.Summary,
                risks = analysis.RiskFactors,
                opportunities = new List<string>()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error analyzing sentiment for {symbol}");
            return StatusCode(500, new { error = "Failed to analyze sentiment", details = ex.Message });
        }
    }

    [HttpGet("market")]
    public async Task<IActionResult> GetMarketSentiment(
        [FromQuery] string assetClass = "stocks", 
        [FromQuery] string? specificAsset = null)
    {
        try
        {
            _logger.LogInformation($"Getting market sentiment for {assetClass} {specificAsset}");
            var report = await _marketSentimentService.AnalyzeMarketSentimentAsync(assetClass, specificAsset ?? "");
            
            return Ok(new
            {
                assetClass = assetClass,
                specificAsset = specificAsset,
                timestamp = report.AnalysisDate,
                overallSentiment = new
                {
                    label = report.OverallSentiment.Label,
                    score = report.OverallSentiment.Score,
                    confidence = report.OverallSentiment.Confidence,
                    analysis = report.OverallSentiment.Analysis
                },
                breakdown = new
                {
                    news = new
                    {
                        score = report.NewsSentiment.Score,
                        confidence = report.NewsSentiment.Confidence,
                        label = report.NewsSentiment.Label,
                        analysis = report.NewsSentiment.Analysis
                    },
                    socialMedia = new
                    {
                        score = report.SocialMediaSentiment.Score,
                        confidence = report.SocialMediaSentiment.Confidence,
                        label = report.SocialMediaSentiment.Label,
                        analysis = report.SocialMediaSentiment.Analysis
                    },
                    fearGreed = new
                    {
                        score = report.FearGreedIndex.Score,
                        confidence = report.FearGreedIndex.Confidence,
                        label = report.FearGreedIndex.Label,
                        analysis = report.FearGreedIndex.Analysis
                    },
                    technical = new
                    {
                        score = report.TechnicalSentiment.Score,
                        confidence = report.TechnicalSentiment.Confidence,
                        label = report.TechnicalSentiment.Label,
                        analysis = report.TechnicalSentiment.Analysis
                    }
                },
                keySignals = new List<string> { report.MarketDirection },
                risks = new List<string>(),
                tradingImplications = report.TradingRecommendations
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error analyzing market sentiment for {assetClass}");
            return StatusCode(500, new { error = "Failed to analyze market sentiment", details = ex.Message });
        }
    }

    [HttpGet("reddit/{symbol}")]
    public async Task<IActionResult> GetRedditSentiment(
        string symbol, 
        [FromQuery] int postsToAnalyze = 50)
    {
        try
        {
            _logger.LogInformation($"Getting Reddit sentiment for {symbol}");
            
            // Analyze across multiple financial subreddits
            var subreddits = new[] { "wallstreetbets", "stocks", "investing", "options" };
            var analyses = new List<object>();

            foreach (var subreddit in subreddits)
            {
                try
                {
                    var analysis = await _redditService.AnalyzeSubredditSentimentAsync(
                        subreddit, symbol, postsToAnalyze);
                    
                    if (analysis != null)
                    {
                        analyses.Add(new
                        {
                            subreddit = subreddit,
                            sentiment = analysis.OverallSentiment,
                            sentimentScore = analysis.SentimentScore,
                            postsAnalyzed = analysis.TotalPosts,
                            bullishPercentage = analysis.PositiveKeywordCount > 0 
                                ? (double)analysis.PositiveKeywordCount / (analysis.PositiveKeywordCount + analysis.NegativeKeywordCount) 
                                : 0.5,
                            bearishPercentage = analysis.NegativeKeywordCount > 0 
                                ? (double)analysis.NegativeKeywordCount / (analysis.PositiveKeywordCount + analysis.NegativeKeywordCount) 
                                : 0.5,
                            neutralPercentage = 0.0,
                            topPosts = analysis.Posts?.Take(3).Select(p => new
                            {
                                title = p.Title,
                                score = p.Score,
                                comments = p.NumComments,
                                sentiment = analysis.SentimentScore > 0 ? "Bullish" : analysis.SentimentScore < 0 ? "Bearish" : "Neutral",
                                url = p.Url
                            }),
                            keyThemes = new List<string>(),
                            insights = $"Analysis based on {analysis.TotalPosts} posts"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to analyze {subreddit} for {symbol}");
                }
            }

            // Calculate aggregate sentiment
            var avgScore = analyses.Any() 
                ? analyses.Average(a => ((dynamic)a).sentimentScore) 
                : 0;
            
            var overallSentiment = avgScore > 0.2 ? "Bullish" 
                : avgScore < -0.2 ? "Bearish" 
                : "Neutral";

            return Ok(new
            {
                symbol = symbol,
                timestamp = DateTime.UtcNow,
                overallSentiment = overallSentiment,
                averageSentimentScore = avgScore,
                subredditAnalyses = analyses,
                totalPostsAnalyzed = analyses.Sum(a => ((dynamic)a).postsAnalyzed)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error analyzing Reddit sentiment for {symbol}");
            return StatusCode(500, new { error = "Failed to analyze Reddit sentiment", details = ex.Message });
        }
    }

    [HttpGet("reddit/trending")]
    public async Task<IActionResult> GetTrendingRedditSymbols([FromQuery] int limit = 20)
    {
        try
        {
            _logger.LogInformation("Getting trending symbols from Reddit");
            
            var subreddits = new[] { "wallstreetbets", "stocks" };
            var symbolMentions = new Dictionary<string, int>();

            foreach (var subreddit in subreddits)
            {
                try
                {
                    var posts = await _redditService.ScrapeSubredditAsync(subreddit, 50);
                    
                    // Extract ticker symbols (simple regex pattern)
                    foreach (var post in posts)
                    {
                        var text = $"{post.Title} {post.Content}".ToUpper();
                        var matches = System.Text.RegularExpressions.Regex.Matches(
                            text, @"\b[A-Z]{1,5}\b");
                        
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            var symbol = match.Value;
                            // Filter out common words
                            if (symbol.Length >= 2 && symbol.Length <= 5 
                                && !new[] { "THE", "AND", "FOR", "ARE", "BUT", "NOT", "YOU", "ALL", "CAN", "HAS", "HAD", "WAS", "FROM" }.Contains(symbol))
                            {
                                symbolMentions[symbol] = symbolMentions.GetValueOrDefault(symbol, 0) + 1;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to scrape {subreddit}");
                }
            }

            var trending = symbolMentions
                .OrderByDescending(kvp => kvp.Value)
                .Take(limit)
                .Select(kvp => new
                {
                    symbol = kvp.Key,
                    mentions = kvp.Value
                });

            return Ok(new
            {
                timestamp = DateTime.UtcNow,
                trending = trending,
                totalSymbolsFound = symbolMentions.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trending Reddit symbols");
            return StatusCode(500, new { error = "Failed to get trending symbols", details = ex.Message });
        }
    }

    [HttpGet("compare")]
    public async Task<IActionResult> CompareSentiment([FromQuery] string[] symbols)
    {
        try
        {
            if (symbols == null || symbols.Length == 0)
            {
                return BadRequest(new { error = "At least one symbol is required" });
            }

            _logger.LogInformation($"Comparing sentiment for: {string.Join(", ", symbols)}");
            
            var comparisons = new List<object>();

            foreach (var symbol in symbols)
            {
                try
                {
                    var analysis = await _sentimentService.AnalyzeSymbolSentimentAsync(symbol, 5);
                    comparisons.Add(new
                    {
                        symbol = symbol,
                        sentiment = analysis.OverallSentiment,
                        score = analysis.SentimentScore,
                        confidence = analysis.Confidence,
                        newsCount = analysis.NewsItems?.Count ?? 0
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to analyze {symbol}");
                    comparisons.Add(new
                    {
                        symbol = symbol,
                        error = "Analysis failed"
                    });
                }
            }

            return Ok(new
            {
                timestamp = DateTime.UtcNow,
                symbolsCompared = symbols.Length,
                comparisons = comparisons
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing sentiment");
            return StatusCode(500, new { error = "Failed to compare sentiment", details = ex.Message });
        }
    }

    [HttpGet("market-pulse")]
    public async Task<IActionResult> GetMarketPulse()
    {
        try
        {
            _logger.LogInformation("Getting overall market pulse");
            
            // Analyze major indices and asset classes
            var analyses = new List<object>();
            
            var assetClasses = new[] 
            { 
                ("stocks", "SPY"), 
                ("crypto", "BTC"), 
                ("bonds", "TLT") 
            };

            foreach (var (assetClass, symbol) in assetClasses)
            {
                try
                {
                    var report = await _marketSentimentService.AnalyzeMarketSentimentAsync(assetClass, symbol);
                    analyses.Add(new
                    {
                        assetClass = assetClass,
                        symbol = symbol,
                        sentiment = report.OverallSentiment.Label,
                        score = report.OverallSentiment.Score,
                        confidence = report.OverallSentiment.Confidence
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to analyze {assetClass}");
                }
            }

            // Calculate overall market sentiment
            var avgScore = analyses.Any() 
                ? analyses.Average(a => ((dynamic)a).score) 
                : 0;
            
            var marketMood = avgScore > 0.3 ? "Risk-On" 
                : avgScore < -0.3 ? "Risk-Off" 
                : "Neutral";

            return Ok(new
            {
                timestamp = DateTime.UtcNow,
                marketMood = marketMood,
                overallScore = avgScore,
                assetClasses = analyses
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting market pulse");
            return StatusCode(500, new { error = "Failed to get market pulse", details = ex.Message });
        }
    }
}
