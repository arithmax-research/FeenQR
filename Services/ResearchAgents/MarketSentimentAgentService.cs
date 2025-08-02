using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
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
    private readonly RestClient _redditClient;
    private readonly List<SentimentAnalysis> _sentimentHistory = new();

    public MarketSentimentAgentService(
        ILogger<MarketSentimentAgentService> logger,
        IConfiguration configuration,
        Kernel kernel,
        NewsScrapingService newsScrapingService)
    {
        _logger = logger;
        _configuration = configuration;
        _kernel = kernel;
        _newsScrapingService = newsScrapingService;
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
            // Use web scraping for real news data
            var ticker = string.IsNullOrWhiteSpace(specificAsset) ? "AAPL" : specificAsset;
            _logger.LogInformation($"Scraping real news data for ticker: {ticker}");
            
            // Try multiple news sources for better coverage
            var allArticles = new List<NewsArticle>();
            var sources = new[] { "Yahoo Finance", "Bloomberg", "Google Finance" };
            
            foreach (var source in sources)
            {
                try
                {
                    var articles = await _newsScrapingService.GetNewsArticlesAsync(ticker, source, 5);
                    allArticles.AddRange(articles);
                    _logger.LogInformation($"Scraped {articles.Count} articles from {source} for {ticker}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to scrape from {source} for {ticker}");
                }
            }
            
            if (allArticles.Count == 0)
            {
                _logger.LogWarning($"No real news data found for ticker {ticker} from web scraping.");
                return new SentimentData { Source = "News", Score = 0, Confidence = 0, Analysis = "No news data available from web scraping" };
            }

            _logger.LogInformation($"Successfully scraped {allArticles.Count} news articles for {ticker}");

            // Create content for sentiment analysis - include both titles and content
            var newsContent = string.Join("\n\n", allArticles.Select(article => 
                $"**{article.Title}** (Source: {article.Source})\n{article.Summary}\nPublished: {article.PublishedAt:yyyy-MM-dd HH:mm}"));

            var prompt = $@"
Analyze the sentiment of these REAL financial news articles for {ticker}:

{newsContent}

You must provide your analysis in this EXACT format:

SENTIMENT_SCORE: [number between -1.0 and 1.0]
CONFIDENCE: [number between 0.0 and 1.0]

KEY_THEMES: [main themes from the news]
SENTIMENT_DRIVERS: [what's driving the sentiment]
OUTLOOK: [short-term outlook]

Instructions:
- SENTIMENT_SCORE: -1.0 = very negative, 0.0 = neutral, +1.0 = very positive
- CONFIDENCE: 0.0 = no confidence, 1.0 = very confident
- Focus on market-moving news and investor sentiment
- Consider recency (more recent = higher weight)
- Analyze both headlines and content

Example format:
SENTIMENT_SCORE: 0.3
CONFIDENCE: 0.8
";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            var sentimentData = ParseSentimentResult(result.ToString(), "News");
            
            // Add metadata about the real data source
            var sourceBreakdown = allArticles.GroupBy(a => a.Source)
                .Select(g => $"{g.Key}: {g.Count()} articles")
                .ToList();
            
            sentimentData.Analysis = $"Analysis based on {allArticles.Count} real news articles from web scraping.\n" +
                                   $"Sources: {string.Join(", ", sourceBreakdown)}\n\n" + sentimentData.Analysis;
            
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
            // Mock technical data - in production, integrate with your MarketDataService
            var rsi = new Random().NextDouble() * 100;
            var macdSignal = (new Random().NextDouble() - 0.5) * 2;
            var trend = new Random().NextDouble() > 0.5 ? "Uptrend" : "Downtrend";
            var support = new Random().NextDouble() * 1000 + 40000;
            var resistance = support + (new Random().NextDouble() * 5000 + 1000);
            
            var prompt = $@"
Analyze technical sentiment for {assetClass} {specificAsset} based on technical indicators:

Technical Indicators:
- RSI: {rsi:F1} (Oversold <30, Overbought >70)
- MACD Signal: {macdSignal:F2} (Positive = Bullish, Negative = Bearish)
- Trend: {trend}
- Support Level: ${support:F0}
- Resistance Level: ${resistance:F0}

You must provide your analysis in this EXACT format:

SENTIMENT_SCORE: [number between -1.0 and 1.0]
CONFIDENCE: [number between 0.0 and 1.0]

TECHNICAL_BIAS: [Bullish/Bearish/Neutral technical bias]
KEY_LEVELS: [important support/resistance levels]
MOMENTUM_ANALYSIS: [momentum building or weakening]
BREAKOUT_POTENTIAL: [potential breakout scenarios]

Instructions:
- SENTIMENT_SCORE: -1.0 = very bearish, 0.0 = neutral, +1.0 = very bullish
- CONFIDENCE: Based on signal clarity and alignment
- Consider price action relative to support/resistance
- Assess momentum indicators alignment

Example format:
SENTIMENT_SCORE: 0.1
CONFIDENCE: 0.6
";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            return ParseSentimentResult(result.ToString(), "Technical");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze technical sentiment");
            return new SentimentData { Source = "Technical", Score = 0, Confidence = 0 };
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
Based on this comprehensive sentiment analysis, predict the market direction:

Overall Sentiment: {report.OverallSentiment.Score:F2} ({report.OverallSentiment.Label})
Confidence: {report.OverallSentiment.Confidence:F2}

Component Analysis:
- News Sentiment: {report.NewsSentiment.Score:F2}
- Social Media: {report.SocialMediaSentiment.Score:F2}  
- Fear & Greed: {report.FearGreedIndex.Score:F2}
- Technical: {report.TechnicalSentiment.Score:F2}

Asset Class: {report.AssetClass} {report.SpecificAsset}

Provide market direction prediction:
1. DIRECTION: Bullish/Bearish/Sideways for next 1-7 days
2. PROBABILITY: Confidence in direction (0-100%)
3. KEY_CATALYSTS: What could drive the predicted movement
4. RISK_FACTORS: What could invalidate the prediction
5. TIME_HORIZON: How long this direction might persist
6. VOLATILITY_EXPECTATION: Expected volatility level

Be specific and actionable for trading decisions.
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
Generate specific trading recommendations based on this sentiment analysis:

Overall Sentiment: {report.OverallSentiment.Score:F2} ({report.OverallSentiment.Label})
Market Direction: {report.MarketDirection}
Asset Class: {report.AssetClass} {report.SpecificAsset}

Consider:
- Sentiment strength and confidence levels
- Contrarian vs momentum strategies
- Risk management based on sentiment extremes
- Position sizing based on confidence
- Entry/exit timing based on sentiment shifts

Provide actionable recommendations:
1. Position bias (Long/Short/Neutral)
2. Position size recommendation (% of portfolio)
3. Entry strategy and timing
4. Risk management and stop losses
5. Profit taking levels
6. Hedge recommendations if applicable

Format as clear, actionable bullet points.
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
