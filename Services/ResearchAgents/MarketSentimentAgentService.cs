using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using QuantResearchAgent.Plugins;
using RestSharp;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuantResearchAgent.Services.ResearchAgents;

/// <summary>
/// Market Sentiment Agent - Analyzes internet sentiment for market direction
/// </summary>
public class MarketSentimentAgentService
{
    private readonly ILogger<MarketSentimentAgentService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Kernel _kernel;
    private readonly NewsScrapingService _newsScrapingService;
    private readonly IWebSearchPlugin _webSearchPlugin;
    private readonly TechnicalAnalysisService _technicalAnalysisService;
    private readonly RestClient _redditClient;
    private readonly List<SentimentAnalysis> _sentimentHistory = new();

    public MarketSentimentAgentService(
        ILogger<MarketSentimentAgentService> logger,
        IConfiguration configuration,
        Kernel kernel,
        NewsScrapingService newsScrapingService,
        IWebSearchPlugin webSearchPlugin,
        TechnicalAnalysisService technicalAnalysisService)
    {
        _logger = logger;
        _configuration = configuration;
        _kernel = kernel;
        _newsScrapingService = newsScrapingService;
        _webSearchPlugin = webSearchPlugin;
        _technicalAnalysisService = technicalAnalysisService;
        _redditClient = new RestClient("https://www.reddit.com");
    }

    public async Task<MarketSentimentReport> AnalyzeMarketSentimentAsync(
        string assetClass = "crypto", 
        string specificAsset = "")
    {
        _logger.LogInformation("Analyzing market sentiment for {AssetClass} {SpecificAsset}", assetClass, specificAsset);

        try
        {
            var report = new MarketSentimentReport
            {
                AssetClass = assetClass,
                SpecificAsset = specificAsset,
                AnalysisDate = DateTime.UtcNow
            };

            // Gather sentiment from multiple sources
            var newsSentiment = await AnalyzeNewsSentimentAsync(assetClass, specificAsset);
            var socialSentiment = await AnalyzeSocialMediaSentimentAsync(assetClass, specificAsset);
            var fearGreedIndex = await AnalyzeFearGreedIndexAsync(assetClass);
            var technicalSentiment = await AnalyzeTechnicalSentimentAsync(assetClass, specificAsset);

            // Combine all sentiment sources
            report.NewsSentiment = newsSentiment;
            report.SocialMediaSentiment = socialSentiment;
            report.FearGreedIndex = fearGreedIndex;
            report.TechnicalSentiment = technicalSentiment;

            // Calculate overall sentiment
            report.OverallSentiment = CalculateOverallSentiment(report);
            
            // Generate market direction prediction
            report.MarketDirection = await PredictMarketDirectionAsync(report);
            
            // Generate trading recommendations
            report.TradingRecommendations = await GenerateTradingRecommendationsAsync(report);

            _sentimentHistory.Add(new SentimentAnalysis
            {
                AssetClass = assetClass,
                SpecificAsset = specificAsset,
                SentimentScore = report.OverallSentiment.Score,
                Confidence = report.OverallSentiment.Confidence,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Completed sentiment analysis. Overall sentiment: {Score:F2} ({Label})", 
                report.OverallSentiment.Score, report.OverallSentiment.Label);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze market sentiment");
            throw;
        }
    }

    private async Task<SentimentData> AnalyzeNewsSentimentAsync(string assetClass, string specificAsset)
    {
        try
        {
            // Use both web scraping AND Google Search for comprehensive news coverage
            var ticker = string.IsNullOrWhiteSpace(specificAsset) ? "AAPL" : specificAsset;
            _logger.LogInformation($"Gathering news data for ticker: {ticker} using multiple sources");
            
            var allArticles = new List<NewsArticle>();
            
            // 1. Try web scraping first (existing functionality)
            var sources = new[] { "Yahoo Finance", "Bloomberg", "Google Finance" };
            
            foreach (var source in sources)
            {
                try
                {
                    var articles = await _newsScrapingService.GetNewsArticlesAsync(ticker, source, 3);
                    allArticles.AddRange(articles);
                    _logger.LogInformation($"Scraped {articles.Count} articles from {source} for {ticker}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to scrape from {source} for {ticker}");
                }
            }
            
            // 2. Enhance with Google Search for recent news
            try
            {
                var googleQueries = new[]
                {
                    $"{ticker} stock news earnings financial",
                    $"{ticker} market analysis price target",
                    $"{ticker} quarterly results revenue profit"
                };
                
                foreach (var query in googleQueries)
                {
                    var webResults = await _webSearchPlugin.SearchAsync(query, 5);
                    var googleArticles = webResults
                        .Where(r => MarketSentimentHelpers.IsFinancialNewsSource(r.Url))
                        .Select(r => new NewsArticle
                        {
                            Title = r.Title,
                            Summary = r.Snippet,
                            Url = r.Url,
                            Source = MarketSentimentHelpers.ExtractSourceFromUrl(r.Url),
                            PublishedAt = DateTime.UtcNow.AddHours(-6) // Estimate recent
                        })
                        .Take(3)
                        .ToList();
                    
                    allArticles.AddRange(googleArticles);
                    _logger.LogInformation($"Found {googleArticles.Count} additional articles via Google Search for query: {query}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Google Search enhancement failed for {ticker}, continuing with scraped data");
            }
            
            // Remove duplicates based on title similarity
            var uniqueArticles = MarketSentimentHelpers.RemoveDuplicateArticles(allArticles);
            
            if (uniqueArticles.Count == 0)
            {
                _logger.LogWarning($"No news data found for ticker {ticker} from any source.");
                return new SentimentData { Source = "News", Score = 0, Confidence = 0, Analysis = "No news data available from any source" };
            }

            _logger.LogInformation($"Successfully gathered {uniqueArticles.Count} unique news articles for {ticker}");

            // Analyze each article individually for detailed breakdown
            var articleAnalyses = new List<ArticleAnalysis>();
            
            foreach (var article in uniqueArticles.Take(10)) // Limit to top 10 for performance
            {
                try
                {
                    var articlePrompt = $@"
Analyze the sentiment of this single financial news article for {ticker}:

Title: {article.Title}
Source: {article.Source}
Summary: {article.Summary}
Published: {article.PublishedAt:yyyy-MM-dd HH:mm}

Provide ONLY a sentiment score between -1.0 and 1.0:
SENTIMENT_SCORE: [number between -1.0 and 1.0]

Instructions:
- -1.0 = very negative for stock price
- 0.0 = neutral
- +1.0 = very positive for stock price
- Consider impact on stock price and investor confidence
";

                    var articleFunction = _kernel.CreateFunctionFromPrompt(articlePrompt);
                    var articleResult = await _kernel.InvokeAsync(articleFunction);
                    
                    var scoreMatch = Regex.Match(articleResult.ToString(), @"SENTIMENT_SCORE[:\s]*(-?\d+\.?\d*)", RegexOptions.IgnoreCase);
                    var articleScore = 0.0;
                    if (scoreMatch.Success && double.TryParse(scoreMatch.Groups[1].Value, out var parsedScore))
                    {
                        articleScore = Math.Max(-1.0, Math.Min(1.0, parsedScore));
                    }
                    
                    articleAnalyses.Add(new ArticleAnalysis
                    {
                        Title = article.Title,
                        Source = article.Source,
                        Url = article.Url,
                        SentimentScore = articleScore,
                        PublishedAt = article.PublishedAt,
                        Summary = article.Summary
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to analyze individual article: {article.Title}");
                    articleAnalyses.Add(new ArticleAnalysis
                    {
                        Title = article.Title,
                        Source = article.Source,
                        Url = article.Url,
                        SentimentScore = 0.0,
                        PublishedAt = article.PublishedAt,
                        Summary = article.Summary
                    });
                }
            }
            
            // Calculate overall sentiment from individual articles
            var avgSentiment = articleAnalyses.Any() ? articleAnalyses.Average(a => a.SentimentScore) : 0.0;
            var sentimentData = new SentimentData 
            { 
                Source = "News", 
                Score = avgSentiment,
                Confidence = articleAnalyses.Count > 0 ? Math.Min(1.0, articleAnalyses.Count * 0.1 + 0.3) : 0.0
            };
            
            // Create detailed analysis with individual article breakdowns
            var sourceBreakdown = uniqueArticles.GroupBy(a => a.Source)
                .Select(g => $"{g.Key}: {g.Count()} articles")
                .ToList();
            
            var detailedAnalysis = $"NEWS ANALYSIS BREAKDOWN for {ticker}\n" +
                                 $"Total Articles Analyzed: {articleAnalyses.Count}\n" +
                                 $"Overall Sentiment Score: {avgSentiment:F2}\n\n" +
                                 $"INDIVIDUAL ARTICLE SCORES:\n" +
                                 string.Join("\n", articleAnalyses.Select((a, i) => 
                                     $"{i+1}. [{a.SentimentScore:F2}] {a.Title}\n" +
                                     $"   Source: {a.Source} | Published: {a.PublishedAt:MM/dd HH:mm}\n" +
                                     $"   URL: {a.Url}\n" +
                                     $"   Summary: {(a.Summary.Length > 150 ? a.Summary.Substring(0, 150) + "..." : a.Summary)}\n")) +
                                 $"\nSOURCE BREAKDOWN: {string.Join(", ", sourceBreakdown)}\n" +
                                 $"SENTIMENT DISTRIBUTION:\n" +
                                 $"Positive ({articleAnalyses.Count(a => a.SentimentScore > 0.1)}): {string.Join(", ", articleAnalyses.Where(a => a.SentimentScore > 0.1).Select(a => $"{a.SentimentScore:F2}"))}\n" +
                                 $"Neutral ({articleAnalyses.Count(a => Math.Abs(a.SentimentScore) <= 0.1)}): {string.Join(", ", articleAnalyses.Where(a => Math.Abs(a.SentimentScore) <= 0.1).Select(a => $"{a.SentimentScore:F2}"))}\n" +
                                 $"Negative ({articleAnalyses.Count(a => a.SentimentScore < -0.1)}): {string.Join(", ", articleAnalyses.Where(a => a.SentimentScore < -0.1).Select(a => $"{a.SentimentScore:F2}"))}";
            
            sentimentData.Analysis = detailedAnalysis;
            
            return sentimentData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze news sentiment from web scraping");
            return new SentimentData { 
                Source = "News", 
                Score = 0, 
                Confidence = 0, 
                Analysis = $"Error accessing news via web scraping: {ex.Message}" 
            };
        }
    }

    private async Task<SentimentData> AnalyzeSocialMediaSentimentAsync(string assetClass, string specificAsset)
    {
        try
        {
            // Mock social media data - in production, you'd use Twitter API, Reddit API, etc.
            var socialData = GenerateMockSocialData(assetClass, specificAsset);
            
            var prompt = $@"
Analyze sentiment from social media discussions about {assetClass} {specificAsset}:

Recent Posts/Comments:
{string.Join("\n", socialData.Select(s => $"- {s.Platform}: {s.Content} (Engagement: {s.EngagementScore})"))}

You must provide your analysis in this EXACT format:

SENTIMENT_SCORE: [number between -1.0 and 1.0]
CONFIDENCE: [number between 0.0 and 1.0]

VIRAL_TRENDS: [trending topics or viral content]
INFLUENCER_SENTIMENT: [sentiment from key influencers]
VOLUME_ANALYSIS: [discussion volume trends]
EMOTIONAL_INDICATORS: [fear, greed, FOMO, panic indicators]

Instructions:
- SENTIMENT_SCORE: -1.0 = very bearish, 0.0 = neutral, +1.0 = very bullish
- CONFIDENCE: 0.0 = no confidence, 1.0 = very confident
- Consider engagement levels and reach of posts
- Weight high-engagement posts more heavily

Example format:
SENTIMENT_SCORE: 0.4
CONFIDENCE: 0.75
";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            return ParseSentimentResult(result.ToString(), "Social Media");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze social media sentiment");
            return new SentimentData { Source = "Social Media", Score = 0, Confidence = 0 };
        }
    }

    private async Task<SentimentData> AnalyzeFearGreedIndexAsync(string assetClass)
    {
        try
        {
            // Mock Fear & Greed Index data - in production, use real APIs
            var fearGreedValue = new Random().Next(0, 101); // 0-100 scale
            var volatilityIndex = new Random().NextDouble() * 50 + 10; // 10-60 range
            var marketMomentum = (new Random().NextDouble() - 0.5) * 20; // -10% to +10%
            
            var prompt = $@"
Analyze market sentiment based on these fear and greed indicators for {assetClass}:

Fear & Greed Index: {fearGreedValue}/100
- 0-25: Extreme Fear
- 25-45: Fear  
- 45-55: Neutral
- 55-75: Greed
- 75-100: Extreme Greed

Volatility Index: {volatilityIndex:F1}
Market Momentum (7-day): {marketMomentum:F1}%

You must provide your analysis in this EXACT format:

SENTIMENT_SCORE: [number between -1.0 and 1.0]
CONFIDENCE: [number between 0.0 and 1.0]

FEAR_GREED_INTERPRETATION: [what this level means]
CONTRARIAN_SIGNALS: [contrarian opportunities]
RISK_ASSESSMENT: [current risk environment]

Instructions:
- SENTIMENT_SCORE: Convert Fear/Greed to -1.0 to +1.0 scale
- CONFIDENCE: Based on clarity of signals
- Consider contrarian indicators (extreme fear = buying opportunity)
- High volatility typically indicates fear/uncertainty

Example format:
SENTIMENT_SCORE: -0.2
CONFIDENCE: 0.5
";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            return ParseSentimentResult(result.ToString(), "Fear & Greed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze fear & greed index");
            return new SentimentData { Source = "Fear & Greed", Score = 0, Confidence = 0 };
        }
    }

    private async Task<SentimentData> AnalyzeTechnicalSentimentAsync(string assetClass, string specificAsset)
    {
        try
        {
            // Use REAL technical analysis data instead of mock data
            var ticker = string.IsNullOrWhiteSpace(specificAsset) ? "SPY" : specificAsset;
            _logger.LogInformation($"Getting real technical analysis for {ticker}");
            
            TechnicalAnalysisResult? technicalResult = null;
            
            try
            {
                // Get real technical analysis from the TechnicalAnalysisService
                technicalResult = await _technicalAnalysisService.PerformFullAnalysisAsync(ticker, 100);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get real technical analysis for {ticker}, using fallback analysis");
                
                // Fallback to basic analysis if full analysis fails
                return new SentimentData 
                { 
                    Source = "Technical", 
                    Score = 0, 
                    Confidence = 0.3, 
                    Analysis = $"Technical analysis unavailable for {ticker}: {ex.Message}" 
                };
            }
            
            if (technicalResult == null)
            {
                return new SentimentData { Source = "Technical", Score = 0, Confidence = 0, Analysis = "No technical data available" };
            }
            
            // Convert technical analysis result to sentiment
            var sentimentScore = CalculateTechnicalSentimentScore(technicalResult, 
                GetIndicatorValue(technicalResult, "RSI"), 
                GetIndicatorValue(technicalResult, "MACD"),
                GetIndicatorValue(technicalResult, "SMA_20"));
            var confidence = CalculateTechnicalConfidence(technicalResult);
            
            // Extract indicators from the dictionary
            var rsi = GetIndicatorValue(technicalResult, "RSI");
            var macd = GetIndicatorValue(technicalResult, "MACD");
            var macdSignal = GetIndicatorValue(technicalResult, "MACD_Signal");
            var sma20 = GetIndicatorValue(technicalResult, "SMA_20");
            var sma50 = GetIndicatorValue(technicalResult, "SMA_50");
            var sma200 = GetIndicatorValue(technicalResult, "SMA_200");
            var atr = GetIndicatorValue(technicalResult, "ATR");
            var bollingerPercentB = GetIndicatorValue(technicalResult, "Bollinger_%B");
            var support = GetIndicatorValue(technicalResult, "Support");
            var resistance = GetIndicatorValue(technicalResult, "Resistance");
            
            var prompt = $@"
Analyze technical sentiment for {assetClass} {specificAsset} based on REAL technical indicators:

Technical Analysis Results:
- Current Price: ${technicalResult.CurrentPrice:F2}
- Signal: {technicalResult.OverallSignal} (Strength: {technicalResult.SignalStrength:F2})
- RSI: {rsi:F1} (Oversold <30, Neutral 30-70, Overbought >70)
- MACD: {macd:F4} (Signal: {macdSignal:F4})
- SMA 20: ${sma20:F2}
- SMA 50: ${sma50:F2}
- SMA 200: ${sma200:F2}
- Bollinger %B: {bollingerPercentB:F3}
- ATR: {atr:F2}
- Support: ${support:F2}
- Resistance: ${resistance:F2}

You must provide your analysis in this EXACT format using PLAIN TEXT:

SENTIMENT_SCORE: [number between -1.0 and 1.0]
CONFIDENCE: [number between 0.0 and 1.0]

TECHNICAL_BIAS: [Bullish/Bearish/Neutral based on actual indicators]
KEY_LEVELS: [Support: ${support:F2}, Resistance: ${resistance:F2}]
MOMENTUM_ANALYSIS: [Based on MACD and RSI readings]
BREAKOUT_POTENTIAL: [Based on Bollinger Bands and patterns]

Instructions:
- SENTIMENT_SCORE should reflect the ACTUAL technical signal: {technicalResult.OverallSignal}
- BUY signal = positive score, SELL signal = negative score, HOLD = near zero
- Signal strength {technicalResult.SignalStrength:F2} should influence the magnitude
- RSI {rsi:F1}: oversold suggests bounce potential, overbought suggests pullback
- MACD trend should align with overall sentiment
- Use actual support/resistance levels provided
- NO MARKDOWN FORMATTING

Example format:
SENTIMENT_SCORE: {sentimentScore:F1}
CONFIDENCE: {confidence:F1}
";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            var sentimentData = ParseSentimentResult(result.ToString(), "Technical");
            
            // Override AI result with calculated sentiment if parsing fails
            if (sentimentData.Score == 0 && sentimentData.Confidence == 0)
            {
                sentimentData.Score = sentimentScore;
                sentimentData.Confidence = confidence;
            }
            
            // Add real technical data to analysis
            sentimentData.Analysis = $"Real Technical Analysis for {ticker}:\n" +
                                   $"Signal: {technicalResult.OverallSignal} (Strength: {technicalResult.SignalStrength:F2})\n" +
                                   $"RSI: {rsi:F1}, MACD: {macd:F4}\n" +
                                   $"Price vs SMA20: {((technicalResult.CurrentPrice - sma20) / sma20 * 100):F1}%\n\n" +
                                   sentimentData.Analysis;
            
            return sentimentData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze technical sentiment");
            return new SentimentData { Source = "Technical", Score = 0, Confidence = 0, Analysis = $"Technical analysis error: {ex.Message}" };
        }
    }

    private SentimentData ParseSentimentResult(string result, string source)
    {
        try
        {
            var sentiment = new SentimentData { Source = source };
            
            // More flexible regex patterns to match various AI response formats
            var scorePatterns = new[]
            {
                @"(?:OVERALL_)?SENTIMENT_SCORE[:\s]*(-?\d+\.?\d*)",
                @"(?:SENTIMENT[:\s]*)?SCORE[:\s]*[:]?\s*(-?\d+\.?\d*)",
                @"Score[:\s]*(-?\d+\.?\d*)",
                @"sentiment[:\s]*(-?\d+\.?\d*)",
                @"(-?\d+\.?\d*)\s*(?:sentiment|score)"
            };
            
            foreach (var pattern in scorePatterns)
            {
                var scoreMatch = Regex.Match(result, pattern, RegexOptions.IgnoreCase);
                if (scoreMatch.Success && double.TryParse(scoreMatch.Groups[1].Value, out var score))
                {
                    sentiment.Score = Math.Max(-1.0, Math.Min(1.0, score));
                    break;
                }
            }
            
            // More flexible confidence patterns
            var confidencePatterns = new[]
            {
                @"CONFIDENCE[:\s]*(\d+\.?\d*)",
                @"confidence[:\s]*(\d+\.?\d*)",
                @"Confidence[:\s]*(\d+\.?\d*)",
                @"(\d+\.?\d*)\s*confidence"
            };
            
            foreach (var pattern in confidencePatterns)
            {
                var confidenceMatch = Regex.Match(result, pattern, RegexOptions.IgnoreCase);
                if (confidenceMatch.Success && double.TryParse(confidenceMatch.Groups[1].Value, out var confidence))
                {
                    // Handle both 0-1 and 0-100 scales
                    if (confidence > 1.0)
                        confidence = confidence / 100.0;
                    sentiment.Confidence = Math.Max(0.0, Math.Min(1.0, confidence));
                    break;
                }
            }
            
            // If no explicit values found, try to infer from text
            if (sentiment.Score == 0 && sentiment.Confidence == 0)
            {
                sentiment = InferSentimentFromText(result, source);
            }
            
            // Extract key themes or analysis
            sentiment.Analysis = result;
            
            // Ensure minimum confidence for valid analysis
            if (sentiment.Confidence == 0 && !string.IsNullOrEmpty(result))
            {
                sentiment.Confidence = 0.5; // Default moderate confidence if analysis exists
            }
            
            return sentiment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse sentiment result for {Source}", source);
            return new SentimentData { Source = source, Score = 0, Confidence = 0.3, Analysis = result };
        }
    }

    private SentimentData InferSentimentFromText(string text, string source)
    {
        var sentiment = new SentimentData { Source = source };
        
        // Keyword-based sentiment inference as fallback
        var positiveKeywords = new[] { "bullish", "positive", "optimistic", "strong", "growth", "buy", "upward", "rally", "gain" };
        var negativeKeywords = new[] { "bearish", "negative", "pessimistic", "weak", "decline", "sell", "downward", "crash", "loss" };
        
        var lowerText = text.ToLower();
        var positiveCount = positiveKeywords.Count(keyword => lowerText.Contains(keyword));
        var negativeCount = negativeKeywords.Count(keyword => lowerText.Contains(keyword));
        
        if (positiveCount > negativeCount)
        {
            sentiment.Score = Math.Min(0.6, positiveCount * 0.2);
            sentiment.Confidence = 0.6;
        }
        else if (negativeCount > positiveCount)
        {
            sentiment.Score = Math.Max(-0.6, -negativeCount * 0.2);
            sentiment.Confidence = 0.6;
        }
        else
        {
            sentiment.Score = 0;
            sentiment.Confidence = 0.4;
        }
        
        return sentiment;
    }

    private SentimentData CalculateOverallSentiment(MarketSentimentReport report)
    {
        var sentiments = new[] 
        { 
            report.NewsSentiment, 
            report.SocialMediaSentiment, 
            report.FearGreedIndex, 
            report.TechnicalSentiment 
        };
        
        // Weighted average based on confidence
        var totalWeight = sentiments.Sum(s => s.Confidence);
        if (totalWeight == 0)
        {
            return new SentimentData { Source = "Overall", Score = 0, Confidence = 0, Label = "Neutral" };
        }
        
        var weightedScore = sentiments.Sum(s => s.Score * s.Confidence) / totalWeight;
        var avgConfidence = sentiments.Average(s => s.Confidence);
        
        var label = weightedScore switch
        {
            >= 0.6 => "Very Bullish",
            >= 0.2 => "Bullish", 
            >= -0.2 => "Neutral",
            >= -0.6 => "Bearish",
            _ => "Very Bearish"
        };
        
        return new SentimentData 
        { 
            Source = "Overall", 
            Score = weightedScore, 
            Confidence = avgConfidence,
            Label = label
        };
    }

    private async Task<string> PredictMarketDirectionAsync(MarketSentimentReport report)
    {
        var prompt = $@"
Based on this comprehensive sentiment analysis, predict the market direction using PLAIN TEXT ONLY (no markdown, no asterisks, no hashtags):

Overall Sentiment: {report.OverallSentiment.Score:F2} ({report.OverallSentiment.Label})
Confidence: {report.OverallSentiment.Confidence:F2}

Component Analysis:
- News Sentiment: {report.NewsSentiment.Score:F2}
- Social Media: {report.SocialMediaSentiment.Score:F2}  
- Fear & Greed: {report.FearGreedIndex.Score:F2}
- Technical: {report.TechnicalSentiment.Score:F2}

Asset Class: {report.AssetClass} {report.SpecificAsset}

Provide market direction prediction using SIMPLE TEXT FORMAT:
1. DIRECTION: Bullish/Bearish/Sideways for next 1-7 days
2. PROBABILITY: Confidence in direction (0-100%)
3. KEY_CATALYSTS: What could drive the predicted movement
4. RISK_FACTORS: What could invalidate the prediction
5. TIME_HORIZON: How long this direction might persist
6. VOLATILITY_EXPECTATION: Expected volatility level

Use plain text with simple bullet points. Be specific and actionable for trading decisions. NO MARKDOWN FORMATTING.
";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to predict market direction");
            return "Unable to predict market direction due to analysis error.";
        }
    }

    private async Task<List<string>> GenerateTradingRecommendationsAsync(MarketSentimentReport report)
    {
        var prompt = $@"
Generate specific trading recommendations based on this sentiment analysis using PLAIN TEXT ONLY (no markdown formatting):

Overall Sentiment: {report.OverallSentiment.Score:F2} ({report.OverallSentiment.Label})
Market Direction: {report.MarketDirection}
Asset Class: {report.AssetClass} {report.SpecificAsset}

Consider:
- Sentiment strength and confidence levels
- Contrarian vs momentum strategies
- Risk management based on sentiment extremes
- Position sizing based on confidence
- Entry/exit timing based on sentiment shifts

Provide actionable recommendations using SIMPLE TEXT FORMAT:
1. Position bias (Long/Short/Neutral)
2. Position size recommendation (% of portfolio)
3. Entry strategy and timing
4. Risk management and stop losses
5. Profit taking levels
6. Hedge recommendations if applicable

Format as clear, actionable bullet points with NO MARKDOWN FORMATTING.
";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            return result.ToString()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Trim().StartsWith('-') || line.Trim().StartsWith('•') || 
                              line.Trim().StartsWith("1.") || line.Trim().StartsWith("2."))
                .Select(line => line.Trim().TrimStart('-', '•', '1', '2', '3', '4', '5', '6', '.').Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate trading recommendations");
            return new List<string> { "Unable to generate recommendations due to analysis error." };
        }
    }

    // Mock data generators for demonstration
    private List<NewsItem> GenerateMockNewsData(string keywords)
    {
        var newsItems = new List<NewsItem>
        {
            new() { Title = "Federal Reserve Signals Rate Changes", Summary = "Central bank indicates potential monetary policy shifts affecting markets" },
            new() { Title = "Major Institution Adopts Digital Assets", Summary = "Large financial institution announces cryptocurrency adoption strategy" },
            new() { Title = "Regulatory Clarity Emerges", Summary = "Government provides clearer guidelines for digital asset trading" },
            new() { Title = "Market Volatility Continues", Summary = "Trading volumes and price swings remain elevated across asset classes" },
            new() { Title = "Economic Data Shows Mixed Signals", Summary = "Latest economic indicators present conflicting market signals" }
        };
        
        return newsItems;
    }

    private List<SocialMediaPost> GenerateMockSocialData(string assetClass, string specificAsset)
    {
        var socialPosts = new List<SocialMediaPost>
        {
            new() { Platform = "Twitter", Content = $"Bullish on {assetClass} - fundamentals looking strong!", EngagementScore = 850 },
            new() { Platform = "Reddit", Content = $"Seeing some concerning patterns in {specificAsset} charts", EngagementScore = 340 },
            new() { Platform = "Discord", Content = "Whales are accumulating, could be big move coming", EngagementScore = 120 },
            new() { Platform = "Telegram", Content = "Market sentiment shifting, time to be cautious", EngagementScore = 200 },
            new() { Platform = "Twitter", Content = $"Breaking: Major news for {assetClass} sector!", EngagementScore = 1200 }
        };
        
        return socialPosts;
    }

    public List<SentimentAnalysis> GetSentimentHistory() => _sentimentHistory.ToList();

    public string GenerateSentimentTrendAnalysis(string assetClass, int days = 7)
    {
        try
        {
            var recentSentiments = _sentimentHistory
                .Where(s => s.AssetClass == assetClass && s.Timestamp >= DateTime.UtcNow.AddDays(-days))
                .OrderBy(s => s.Timestamp)
                .ToList();

            if (!recentSentiments.Any())
            {
                return $"No sentiment history available for {assetClass} in the last {days} days.";
            }

            var avgSentiment = recentSentiments.Average(s => s.SentimentScore);
            var trend = recentSentiments.Count > 1 ? 
                (recentSentiments.Last().SentimentScore - recentSentiments.First().SentimentScore) : 0;

            return $"Sentiment Trend Analysis for {assetClass} ({days} days):\n" +
                   $"Average Sentiment: {avgSentiment:F2}\n" +
                   $"Trend Direction: {(trend > 0 ? "Improving" : trend < 0 ? "Deteriorating" : "Stable")}\n" +
                   $"Trend Magnitude: {Math.Abs(trend):F2}\n" +
                   $"Data Points: {recentSentiments.Count}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate sentiment trend analysis");
            return "Error generating sentiment trend analysis.";
        }
    }

    private double GetIndicatorValue(TechnicalAnalysisResult result, string indicatorKey, double defaultValue = 0.0)
    {
        if (result.Indicators?.TryGetValue(indicatorKey, out var value) == true)
        {
            if (value is double doubleValue)
                return doubleValue;
            if (double.TryParse(value?.ToString(), out var parsedValue))
                return parsedValue;
        }
        return defaultValue;
    }

    private double CalculateTechnicalSentimentScore(TechnicalAnalysisResult result, double rsi, double macd, double sma20)
    {
        // Calculate technical sentiment based on real indicators
        double score = 0.0;
        
        // RSI component (30-70 is neutral, <30 oversold/bullish, >70 overbought/bearish)
        if (rsi < 30) score += 0.3; // Oversold = bullish
        else if (rsi > 70) score -= 0.3; // Overbought = bearish
        
        // MACD component (positive = bullish, negative = bearish)
        score += Math.Max(-0.3, Math.Min(0.3, macd * 100)); // Scale and clamp
        
        // Price vs SMA component
        var priceVsSma = (result.CurrentPrice - sma20) / sma20;
        score += Math.Max(-0.2, Math.Min(0.2, priceVsSma)); // Scale and clamp
        
        // Overall signal component (strongest weight)
        if (result.OverallSignal == SignalType.Buy || result.OverallSignal == SignalType.StrongBuy) 
            score += 0.4;
        else if (result.OverallSignal == SignalType.Sell || result.OverallSignal == SignalType.StrongSell) 
            score -= 0.4;
        
        // Normalize to 0-1 range
        return Math.Max(0.0, Math.Min(1.0, (score + 1.0) / 2.0));
    }

    private double CalculateTechnicalConfidence(TechnicalAnalysisResult result)
    {
        // Base confidence on signal strength and data availability
        double confidence = result.SignalStrength;
        
        // Boost confidence if we have valid indicators
        if (result.Indicators?.Count > 0) confidence += 0.1;
        
        return Math.Max(0.0, Math.Min(1.0, confidence));
    }
}

// Model classes for Market Sentiment Agent
public class MarketSentimentReport
{
    public string AssetClass { get; set; } = string.Empty;
    public string SpecificAsset { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; }
    public SentimentData NewsSentiment { get; set; } = new();
    public SentimentData SocialMediaSentiment { get; set; } = new();
    public SentimentData FearGreedIndex { get; set; } = new();
    public SentimentData TechnicalSentiment { get; set; } = new();
    public SentimentData OverallSentiment { get; set; } = new();
    public string MarketDirection { get; set; } = string.Empty;
    public List<string> TradingRecommendations { get; set; } = new();
}

public class SentimentData
{
    public string Source { get; set; } = string.Empty;
    public double Score { get; set; } = 0; // -1.0 to +1.0
    public double Confidence { get; set; } = 0; // 0.0 to 1.0
    public string Label { get; set; } = "Neutral";
    public string Analysis { get; set; } = string.Empty;
}

public class SentimentAnalysis
{
    public string AssetClass { get; set; } = string.Empty;
    public string SpecificAsset { get; set; } = string.Empty;
    public double SentimentScore { get; set; }
    public double Confidence { get; set; }
    public DateTime Timestamp { get; set; }
}

public class NewsItem
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = string.Empty;
}

public class SocialMediaPost
{
    public string Platform { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int EngagementScore { get; set; }
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    public string Author { get; set; } = string.Empty;
}

public class ArticleAnalysis
{
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public double SentimentScore { get; set; }
    public DateTime PublishedAt { get; set; }
    public string Summary { get; set; } = string.Empty;
}

// Helper extension for MarketSentimentAgentService
public static class MarketSentimentHelpers
{
    public static bool IsFinancialNewsSource(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        
        var financialDomains = new[]
        {
            "finance.yahoo.com", "bloomberg.com", "marketwatch.com", "reuters.com",
            "cnbc.com", "wsj.com", "ft.com", "investing.com", "seekingalpha.com",
            "morningstar.com", "fool.com", "benzinga.com", "barrons.com"
        };
        
        return financialDomains.Any(domain => url.ToLower().Contains(domain));
    }
    
    public static string ExtractSourceFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "Unknown";
        
        try
        {
            var uri = new Uri(url);
            var host = uri.Host.ToLower();
            
            if (host.Contains("yahoo")) return "Yahoo Finance";
            if (host.Contains("bloomberg")) return "Bloomberg";
            if (host.Contains("marketwatch")) return "MarketWatch";
            if (host.Contains("reuters")) return "Reuters";
            if (host.Contains("cnbc")) return "CNBC";
            if (host.Contains("wsj")) return "Wall Street Journal";
            if (host.Contains("ft.com")) return "Financial Times";
            if (host.Contains("investing")) return "Investing.com";
            if (host.Contains("seekingalpha")) return "Seeking Alpha";
            
            return host.Replace("www.", "").Split('.')[0];
        }
        catch
        {
            return "Unknown";
        }
    }
    
    public static List<QuantResearchAgent.Services.NewsArticle> RemoveDuplicateArticles(List<QuantResearchAgent.Services.NewsArticle> articles)
    {
        var uniqueArticles = new List<QuantResearchAgent.Services.NewsArticle>();
        var seenTitles = new HashSet<string>();
        
        foreach (var article in articles)
        {
            var normalizedTitle = article.Title.ToLower().Trim();
            
            // Check for exact matches or very similar titles
            bool isDuplicate = seenTitles.Any(seen => 
                seen == normalizedTitle || 
                CalculateTitleSimilarity(seen, normalizedTitle) > 0.8);
            
            if (!isDuplicate)
            {
                uniqueArticles.Add(article);
                seenTitles.Add(normalizedTitle);
            }
        }
        
        return uniqueArticles;
    }
    
    private static double CalculateTitleSimilarity(string title1, string title2)
    {
        var words1 = title1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = title2.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        var commonWords = words1.Intersect(words2).Count();
        var totalWords = Math.Max(words1.Length, words2.Length);
        
        return totalWords > 0 ? (double)commonWords / totalWords : 0;
    }
}
