using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
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
    private readonly RestClient _newsApiClient;
    private readonly RestClient _redditClient;
    private readonly List<SentimentAnalysis> _sentimentHistory = new();

    public MarketSentimentAgentService(
        ILogger<MarketSentimentAgentService> logger,
        IConfiguration configuration,
        Kernel kernel)
    {
        _logger = logger;
        _configuration = configuration;
        _kernel = kernel;
        _newsApiClient = new RestClient("https://newsapi.org");
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
            // Use yfinance Flask API for real news
            var ticker = string.IsNullOrWhiteSpace(specificAsset) ? "AAPL" : specificAsset;
            var newsPlugin = new QuantResearchAgent.Plugins.MarketSentimentNewsPlugin();
            var newsItems = await newsPlugin.GetNewsAsync(ticker, 10);
            if (newsItems == null || newsItems.Count == 0)
            {
                _logger.LogWarning($"No news found for ticker {ticker}, falling back to mock data.");
                // Map NewsItem to MarketNewsItem
                newsItems = GenerateMockNewsData(ticker)
                    .Select(n => new QuantResearchAgent.Plugins.MarketNewsItem {
                        Title = n.Title,
                        Summary = n.Summary,
                        Publisher = n.Source,
                        ProviderPublishTime = n.PublishedAt
                    }).ToList();
            }

            var prompt = $@"
Analyze the sentiment of these financial news headlines and summaries:

{string.Join("\n", newsItems.Select(n => $"- {n.Title}: {n.Summary}"))}

Asset Focus: {assetClass} {specificAsset}

Provide sentiment analysis:
1. OVERALL_SENTIMENT_SCORE: -1.0 to +1.0 (negative to positive)
2. CONFIDENCE: 0.0 to 1.0 (how confident are you in this assessment)
3. KEY_THEMES: Main themes affecting sentiment
4. SENTIMENT_DRIVERS: What's driving the sentiment (positive/negative factors)
5. OUTLOOK: Short-term outlook based on news sentiment

Focus on market-moving news and investor sentiment indicators.
";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            return ParseSentimentResult(result.ToString(), "News");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze news sentiment");
            return new SentimentData { Source = "News", Score = 0, Confidence = 0 };
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

Provide social media sentiment analysis:
1. SENTIMENT_SCORE: -1.0 to +1.0
2. CONFIDENCE: 0.0 to 1.0
3. VIRAL_TRENDS: Any trending topics or viral content
4. INFLUENCER_SENTIMENT: Sentiment from key influencers/accounts
5. VOLUME_ANALYSIS: Is discussion volume increasing/decreasing?
6. EMOTIONAL_INDICATORS: Fear, greed, FOMO, panic indicators

Consider the engagement levels and reach of different posts.
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

Additional Context:
- High volatility typically indicates fear/uncertainty
- Strong momentum can indicate greed or fear depending on direction
- Consider contrarian indicators (extreme fear = buying opportunity)

Provide fear/greed sentiment analysis:
1. SENTIMENT_SCORE: Convert to -1.0 to +1.0 scale
2. CONFIDENCE: 0.0 to 1.0
3. FEAR_GREED_INTERPRETATION: What this level typically means
4. CONTRARIAN_SIGNALS: Any contrarian opportunities
5. RISK_ASSESSMENT: Current risk environment
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

Market Structure Analysis:
- Price action relative to support/resistance
- Momentum indicators alignment
- Trend strength assessment

Provide technical sentiment analysis:
1. SENTIMENT_SCORE: -1.0 to +1.0 based on technical setup
2. CONFIDENCE: 0.0 to 1.0 based on signal clarity
3. TECHNICAL_BIAS: Bullish/Bearish/Neutral technical bias
4. KEY_LEVELS: Important support/resistance levels
5. MOMENTUM_ANALYSIS: Is momentum building or weakening?
6. BREAKOUT_POTENTIAL: Any potential breakout scenarios
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
            
            // Extract sentiment score
            var scoreMatch = Regex.Match(result, @"SENTIMENT_SCORE[:\s]+(-?\d+\.?\d*)", RegexOptions.IgnoreCase);
            if (scoreMatch.Success && double.TryParse(scoreMatch.Groups[1].Value, out var score))
            {
                sentiment.Score = Math.Max(-1.0, Math.Min(1.0, score));
            }
            
            // Extract confidence
            var confidenceMatch = Regex.Match(result, @"CONFIDENCE[:\s]+(\d+\.?\d*)", RegexOptions.IgnoreCase);
            if (confidenceMatch.Success && double.TryParse(confidenceMatch.Groups[1].Value, out var confidence))
            {
                sentiment.Confidence = Math.Max(0.0, Math.Min(1.0, confidence));
            }
            
            // Extract key themes or analysis
            sentiment.Analysis = result;
            
            return sentiment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse sentiment result for {Source}", source);
            return new SentimentData { Source = source, Score = 0, Confidence = 0 };
        }
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
