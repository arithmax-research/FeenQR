using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Models;
using QuantResearchAgent.Services;
using QuantResearchAgent.Services.ResearchAgents;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SentimentController : ControllerBase
{
    private readonly NewsSentimentAnalysisService _sentimentService;
    private readonly MarketSentimentAgentService _marketSentimentService;
    private readonly INewsPipelineService _newsPipelineService;
    private readonly RedditScrapingService _redditService;
    private readonly RedditPostArchiveService _redditArchiveService;
    private readonly ILogger<SentimentController> _logger;

    public SentimentController(
        NewsSentimentAnalysisService sentimentService,
        MarketSentimentAgentService marketSentimentService,
        INewsPipelineService newsPipelineService,
        RedditScrapingService redditService,
        RedditPostArchiveService redditArchiveService,
        ILogger<SentimentController> logger)
    {
        _sentimentService = sentimentService;
        _marketSentimentService = marketSentimentService;
        _newsPipelineService = newsPipelineService;
        _redditService = redditService;
        _redditArchiveService = redditArchiveService;
        _logger = logger;
    }

    [HttpGet("symbol/{symbol}")]
    public async Task<IActionResult> GetSymbolSentiment(string symbol, [FromQuery] int newsLimit = 10)
    {
        try
        {
            _logger.LogInformation($"Getting enhanced sentiment analysis with full content for {symbol}");
            var analysis = await _sentimentService.AnalyzeSymbolSentimentWithFullContentAsync(symbol, newsLimit);
            
            // Calculate sentiment percentages
            var positiveCount = analysis.NewsItems?.Count(n => n.SentimentScore > 0.1) ?? 0;
            var negativeCount = analysis.NewsItems?.Count(n => n.SentimentScore < -0.1) ?? 0;
            var totalCount = analysis.NewsItems?.Count ?? 1;
            
            var positivePercent = (positiveCount * 100.0) / totalCount;
            var negativePercent = (negativeCount * 100.0) / totalCount;
            
            return Ok(new
            {
                symbol = symbol,
                timestamp = DateTime.UtcNow,
                overallSentiment = analysis.OverallSentiment,
                sentimentScore = analysis.SentimentScore,
                confidence = analysis.Confidence,
                newsCount = totalCount,
                positivePercent = positivePercent,
                negativePercent = negativePercent,
                sources = analysis.NewsItems?.Select(n => n.Source).Distinct().ToArray() ?? Array.Empty<string>(),
                summary = analysis.Summary,
                // Enhanced analysis fields
                trendDirection = analysis.TrendDirection,
                keyThemes = analysis.KeyThemes ?? new List<string>(),
                tradingSignal = analysis.TradingSignal,
                riskFactors = analysis.RiskFactors ?? new List<string>(),
                positiveThemes = analysis.PositiveThemes ?? new List<SentimentTheme>(),
                negativeThemes = analysis.NegativeThemes ?? new List<SentimentTheme>(),
                volatilityIndicator = analysis.VolatilityIndicator,
                priceTargetBias = analysis.PriceTargetBias,
                institutionalSentiment = analysis.InstitutionalSentiment,
                retailSentiment = analysis.RetailSentiment,
                analystConsensus = analysis.AnalystConsensus,
                earningsImpact = analysis.EarningsImpact,
                sectorComparison = analysis.SectorComparison,
                momentumSignal = analysis.MomentumSignal,
                recentNews = analysis.NewsItems?.Select(n => new
                {
                    title = n.Title,
                    source = n.Source,
                    publishedDate = n.PublishedDate,
                    sentimentLabel = n.SentimentLabel,
                    sentimentScore = n.SentimentScore,
                    link = n.Link,
                    summary = string.IsNullOrEmpty(n.Summary) ? "" : (n.Summary.Length > 200 ? n.Summary.Substring(0, 200) + "..." : n.Summary),
                    keyTopics = n.KeyTopics ?? new List<string>(),
                    impact = n.Impact,
                    contentScraped = n.ContentScraped,
                    chunkCount = n.ChunkCount
                }).ToArray() ?? Array.Empty<object>()
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

            var subreddits = new[] { "wallstreetbets", "stocks", "investing", "options" };
            var analyses = new List<object>();
            var collectedPosts = new List<RedditPost>();

            foreach (var subreddit in subreddits)
            {
                try
                {
                    var analysis = await _redditService.AnalyzeSubredditSentimentAsync(subreddit, symbol, postsToAnalyze);
                    var subredditPosts = analysis.Posts ?? new List<RedditPost>();

                    if (subredditPosts.Any())
                    {
                        collectedPosts.AddRange(subredditPosts);
                    }

                    var subredditSentiment = await _newsPipelineService.AnalyzeSentimentAsync(
                        symbol,
                        subredditPosts.Select(post => ToArticleForAnalysis(post, subreddit)).ToList());

                    var scoredPosts = subredditPosts
                        .Select((post, index) => new
                        {
                            Post = post,
                            Sentiment = index < subredditSentiment.ArticleResults.Count
                                ? subredditSentiment.ArticleResults[index]
                                : new ArticleSentimentResult()
                        })
                        .ToList();

                    analyses.Add(new
                    {
                        subreddit = subreddit,
                        sentiment = MapFastEmbedSentiment(subredditSentiment.OverallSentiment),
                        sentimentScore = subredditSentiment.OverallScore,
                        postsAnalyzed = subredditSentiment.ArticleResults.Count,
                        bullishPercentage = subredditSentiment.BullishPercentage,
                        bearishPercentage = subredditSentiment.BearishPercentage,
                        neutralPercentage = subredditSentiment.NeutralPercentage,
                        bullishPosts = subredditSentiment.ArticleResults.Count(result => result.Sentiment == "Positive"),
                        bearishPosts = subredditSentiment.ArticleResults.Count(result => result.Sentiment == "Negative"),
                        neutralPosts = subredditSentiment.ArticleResults.Count(result => result.Sentiment == "Neutral"),
                        sentimentMethod = "fastembed pipeline",
                        topPosts = scoredPosts
                            .OrderByDescending(item => Math.Abs(item.Sentiment.Score))
                            .Take(3)
                            .Select(item => new
                            {
                                title = item.Post.Title,
                                content = item.Post.Content,
                                score = item.Sentiment.Score,
                                sentimentScore = item.Sentiment.Score,
                                sentimentConfidence = item.Sentiment.Confidence,
                                sentimentLabel = item.Sentiment.Sentiment,
                                keyTopics = item.Sentiment.KeyTopics,
                                impact = item.Sentiment.Impact,
                                comments = item.Post.Comments,
                                publishedDate = item.Post.CreatedUtc,
                                sentiment = MapFastEmbedSentiment(item.Sentiment.Sentiment),
                                url = item.Post.Url
                            }),
                        keyThemes = scoredPosts
                            .SelectMany(item => item.Sentiment.KeyTopics)
                            .Where(topic => !string.IsNullOrWhiteSpace(topic))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Take(6)
                            .ToList(),
                        insights = $"Analysis based on {subredditSentiment.ArticleResults.Count} FastEmbed-scored posts"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to analyze {subreddit} for {symbol}");
                }
            }

            var distinctPosts = collectedPosts
                .GroupBy(post => string.Join("|", new[]
                {
                    post.Subreddit,
                    post.Id,
                    post.Url,
                    post.Title,
                    post.CreatedUtc.ToUniversalTime().Ticks.ToString()
                }).ToLowerInvariant(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            var storedCount = await _redditArchiveService.StorePostsAsync(symbol, distinctPosts);
            var storedSentiment = await _newsPipelineService.AnalyzeSentimentAsync(
                symbol,
                distinctPosts.Select(post => ToArticleForAnalysis(post)).ToList());

            var overallBullishPosts = storedSentiment.ArticleResults.Count(result => result.Sentiment == "Positive");
            var overallBearishPosts = storedSentiment.ArticleResults.Count(result => result.Sentiment == "Negative");
            var overallNeutralPosts = storedSentiment.ArticleResults.Count(result => result.Sentiment == "Neutral");

            return Ok(new
            {
                symbol = symbol,
                timestamp = DateTime.UtcNow,
                overallSentiment = MapFastEmbedSentiment(storedSentiment.OverallSentiment),
                averageSentimentScore = storedSentiment.OverallScore,
                sentimentMethod = "fastembed pipeline",
                bullishPosts = overallBullishPosts,
                bearishPosts = overallBearishPosts,
                neutralPosts = overallNeutralPosts,
                subredditAnalyses = analyses,
                totalPostsAnalyzed = analyses.Sum(a => ((dynamic)a).postsAnalyzed),
                storedPostCount = distinctPosts.Count,
                storedPosts = distinctPosts.Select((post, index) => new
                {
                    title = post.Title,
                    content = post.Content,
                    subreddit = post.Subreddit,
                    author = post.Author,
                    score = post.Score,
                    sentimentScore = index < storedSentiment.ArticleResults.Count ? storedSentiment.ArticleResults[index].Score : 0.0,
                    sentimentConfidence = index < storedSentiment.ArticleResults.Count ? storedSentiment.ArticleResults[index].Confidence : 0.0,
                    sentimentLabel = index < storedSentiment.ArticleResults.Count ? storedSentiment.ArticleResults[index].Sentiment : "Neutral",
                    keyTopics = index < storedSentiment.ArticleResults.Count ? storedSentiment.ArticleResults[index].KeyTopics : new List<string>(),
                    impact = index < storedSentiment.ArticleResults.Count ? storedSentiment.ArticleResults[index].Impact : "Medium",
                    comments = post.Comments,
                    publishedDate = post.CreatedUtc,
                    url = post.Url,
                    searchQuery = post.SearchQuery
                }).ToArray(),
                archiveStatus = new
                {
                    qdrantStored = storedCount,
                    available = storedCount > 0 || distinctPosts.Count > 0
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error analyzing Reddit sentiment for {symbol}");
            return StatusCode(500, new { error = "Failed to analyze Reddit sentiment", details = ex.Message });
        }
    }

    private static ArticleForAnalysis ToArticleForAnalysis(RedditPost post, string source = "Reddit")
    {
        var normalizedContent = string.Join("\n", new[]
        {
            post.Title,
            post.Content,
            $"Subreddit: {post.Subreddit}",
            $"Author: {post.Author}",
            $"Search query: {post.SearchQuery ?? string.Empty}",
            $"Score: {post.Score}",
            $"Comments: {post.Comments}",
            $"Upvotes: {post.Upvotes}",
            $"Downvotes: {post.Downvotes}"
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return new ArticleForAnalysis
        {
            Id = post.Id,
            Title = post.Title,
            Content = normalizedContent,
            Source = source,
            PublishedDate = post.CreatedUtc,
            Url = post.Url
        };
    }

    private static RedditPost ToRedditPost(RedditStoredPost post)
    {
        return new RedditPost
        {
            Id = post.Id,
            Title = post.Title,
            Content = post.Content,
            Url = post.Url,
            Author = post.Author,
            Score = post.Score,
            Upvotes = post.Upvotes,
            Downvotes = post.Downvotes,
            Comments = post.Comments,
            CreatedUtc = post.CreatedUtc,
            Subreddit = post.Subreddit,
            SearchQuery = post.SearchQuery
        };
    }

    private static string MapFastEmbedSentiment(string sentiment)
    {
        return sentiment.ToUpperInvariant() switch
        {
            "POSITIVE" => "Bullish",
            "NEGATIVE" => "Bearish",
            "NEUTRAL" => "Neutral",
            "BULLISH" => "Bullish",
            "BEARISH" => "Bearish",
            _ => "Neutral"
        };
    }

    [HttpDelete("reddit/{symbol}/stored")]
    public async Task<IActionResult> ClearStoredRedditPosts(string symbol)
    {
        try
        {
            var deleted = await _redditArchiveService.ClearPostsAsync(symbol);
            return Ok(new
            {
                symbol,
                deleted,
                cleared = true,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error clearing Reddit archive for {symbol}");
            return StatusCode(500, new { error = "Failed to clear Reddit archive", details = ex.Message });
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
                        
                        // Comprehensive list of common English words to filter out
                        var commonWords = new HashSet<string> {
                            "THE", "AND", "FOR", "ARE", "BUT", "NOT", "YOU", "ALL", "CAN", "HAS", "HAD", "WAS", "FROM",
                            "THIS", "THAT", "WITH", "HAVE", "WILL", "YOUR", "WHAT", "BEEN", "MORE", "WHEN", "THEY",
                            "THAN", "THEM", "SOME", "WOULD", "COULD", "SHOULD", "ABOUT", "INTO", "JUST", "LIKE",
                            "ONLY", "OVER", "SUCH", "TAKE", "THAN", "THEN", "THESE", "THOSE", "VERY", "WELL",
                            "ALSO", "BACK", "EVEN", "GOOD", "HERE", "MUCH", "MUST", "NEED", "SAID", "SAME",
                            "SEEM", "SEEN", "SELF", "SHOW", "SURE", "TELL", "THAN", "THAT", "THEM", "THEN",
                            "THERE", "THESE", "THING", "THINK", "THIS", "THOSE", "TIME", "VERY", "WANT", "WELL",
                            "WERE", "WHAT", "WHEN", "WHERE", "WHICH", "WHILE", "WHO", "WILL", "WITH", "WOULD",
                            "YEAR", "AFTER", "AGAIN", "BEING", "BOTH", "COME", "COULD", "DOES", "DOWN", "EACH",
                            "FIND", "FIRST", "GIVE", "GOING", "GREAT", "HELP", "KNOW", "LAST", "LONG", "LOOK",
                            "MADE", "MAKE", "MANY", "MOST", "NEVER", "NEXT", "OTHER", "PEOPLE", "RIGHT", "STILL",
                            "THEIR", "THERE", "THINK", "UNDER", "UNTIL", "USING", "WANT", "WORK", "WORLD", "YEARS",
                            "HTTPS", "HTTP", "WWW", "COM", "ORG", "NET", "HTML", "AMP", "NBSP",
                            "EDIT", "TLDR", "IMHO", "IIRC", "FWIW", "YMMV", "AFAIK", "NSFW", "LMAO", "ROFL",
                            "DONT", "CANT", "WONT", "ISNT", "ARENT", "WASNT", "WERENT", "DIDNT", "DOESNT",
                            "THATS", "THERES", "HERES", "WHATS", "WHERES", "WHOS", "HOWS", "WHYS",
                            "REALLY", "MAYBE", "PROBABLY", "ACTUALLY", "LITERALLY", "BASICALLY", "HONESTLY"
                        };
                        
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            var symbol = match.Value;
                            // Filter: must be 2-5 chars, not a common word, and preferably contains a number or is all caps in context
                            if (symbol.Length >= 2 && symbol.Length <= 5 && !commonWords.Contains(symbol))
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
                    var analysis = await _sentimentService.AnalyzeSymbolSentimentWithFullContentAsync(symbol, 5);
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
            
            // Analyze major indices and asset classes IN PARALLEL for speed
            var assetClasses = new[] 
            { 
                ("stocks", "SPY"), 
                ("crypto", "BTC"), 
                ("bonds", "TLT") 
            };

            // Run all analyses in parallel with timeout to reduce wait time
            var timeout = TimeSpan.FromSeconds(10);
            var analysisTasks = assetClasses.Select(async item =>
            {
                var (assetClass, symbol) = item;
                try
                {
                    using var cts = new CancellationTokenSource(timeout);
                    var report = await _marketSentimentService.AnalyzeMarketSentimentAsync(assetClass, symbol);
                    return new
                    {
                        assetClass = assetClass,
                        symbol = symbol,
                        sentiment = report.OverallSentiment.Label,
                        score = report.OverallSentiment.Score,
                        confidence = report.OverallSentiment.Confidence
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to analyze {assetClass} - using neutral default");
                    // Return neutral sentiment as fallback
                    return new
                    {
                        assetClass = assetClass,
                        symbol = symbol,
                        sentiment = "Neutral",
                        score = 0.0,
                        confidence = 0.5
                    };
                }
            });

            var results = await Task.WhenAll(analysisTasks);
            var analyses = results.Where(r => r != null).ToList()!;

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
