using Microsoft.SemanticKernel;
using QuantResearchAgent.Services.ResearchAgents;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Market Sentiment Plugin - Exposes sentiment analysis capabilities to Semantic Kernel
/// </summary>
public class MarketSentimentPlugin
{
    private readonly MarketSentimentAgentService _sentimentService;

    public MarketSentimentPlugin(MarketSentimentAgentService sentimentService)
    {
        _sentimentService = sentimentService;
    }

    [KernelFunction]
    [Description("Analyze market sentiment for a specific asset class or security")]
    public async Task<string> AnalyzeMarketSentimentAsync(
        [Description("Asset class (crypto, stocks, bonds, etf)")] string assetClass,
        [Description("Specific asset symbol or name (optional)")] string specificAsset = "")
    {
        try
        {
            var report = await _sentimentService.AnalyzeMarketSentimentAsync(assetClass, specificAsset);
            
            var result = $"## Market Sentiment Analysis\n\n";
            result += $"**Asset:** {assetClass} {specificAsset}\n";
            result += $"**Analysis Date:** {report.AnalysisDate:yyyy-MM-dd HH:mm} UTC\n\n";
            
            // Overall sentiment
            result += $"### Overall Sentiment: {report.OverallSentiment.Label}\n";
            result += $"**Score:** {report.OverallSentiment.Score:F2} (-1.0 to +1.0)\n";
            result += $"**Confidence:** {report.OverallSentiment.Confidence:P0}\n\n";
            
            // Component analysis
            result += "### Sentiment Breakdown\n";
            result += $"📰 **News Sentiment:** {report.NewsSentiment.Score:F2} (Confidence: {report.NewsSentiment.Confidence:P0})\n";
            result += $"📱 **Social Media:** {report.SocialMediaSentiment.Score:F2} (Confidence: {report.SocialMediaSentiment.Confidence:P0})\n";
            result += $"😰 **Fear & Greed:** {report.FearGreedIndex.Score:F2} (Confidence: {report.FearGreedIndex.Confidence:P0})\n";
            result += $"📊 **Technical:** {report.TechnicalSentiment.Score:F2} (Confidence: {report.TechnicalSentiment.Confidence:P0})\n\n";
            
            // Market direction
            result += $"### Market Direction Prediction\n";
            result += $"{report.MarketDirection}\n\n";
            
            // Trading recommendations
            if (report.TradingRecommendations.Any())
            {
                result += "### Trading Recommendations\n";
                foreach (var recommendation in report.TradingRecommendations)
                {
                    result += $"• {recommendation}\n";
                }
                result += "\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error analyzing market sentiment: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Get sentiment analysis for cryptocurrency markets")]
    public async Task<string> AnalyzeCryptoSentimentAsync(
        [Description("Specific cryptocurrency (optional, e.g., 'bitcoin', 'ethereum')")] string cryptocurrency = "")
    {
        return await AnalyzeMarketSentimentAsync("crypto", cryptocurrency);
    }

    [KernelFunction]
    [Description("Get sentiment analysis for stock markets")]
    public async Task<string> AnalyzeStockSentimentAsync(
        [Description("Specific stock symbol (optional, e.g., 'AAPL', 'TSLA')")] string stockSymbol = "")
    {
        return await AnalyzeMarketSentimentAsync("stocks", stockSymbol);
    }

    [KernelFunction]
    [Description("Get sentiment analysis for bond markets")]
    public async Task<string> AnalyzeBondSentimentAsync(
        [Description("Specific bond type (optional, e.g., '10Y Treasury', 'Corporate')")] string bondType = "")
    {
        return await AnalyzeMarketSentimentAsync("bonds", bondType);
    }

    [KernelFunction]
    [Description("Get sentiment analysis for ETF markets")]
    public async Task<string> AnalyzeETFSentimentAsync(
        [Description("Specific ETF symbol (optional, e.g., 'SPY', 'QQQ')")] string etfSymbol = "")
    {
        return await AnalyzeMarketSentimentAsync("etf", etfSymbol);
    }

    [KernelFunction]
    [Description("Compare sentiment across multiple asset classes")]
    public async Task<string> CompareSentimentAcrossAssetsAsync(
        [Description("Comma-separated list of assets to compare")] string assets = "crypto,stocks,bonds")
    {
        try
        {
            var assetList = assets.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(a => a.Trim())
                                  .ToList();

            var result = $"## Sentiment Comparison Across Assets\n\n";
            result += $"**Analysis Date:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n";

            var sentimentData = new List<(string Asset, double Score, double Confidence, string Label)>();

            foreach (var asset in assetList)
            {
                try
                {
                    var report = await _sentimentService.AnalyzeMarketSentimentAsync(asset);
                    sentimentData.Add((
                        asset.ToUpper(), 
                        report.OverallSentiment.Score, 
                        report.OverallSentiment.Confidence,
                        report.OverallSentiment.Label
                    ));
                }
                catch (Exception ex)
                {
                    result += $"⚠️ Error analyzing {asset}: {ex.Message}\n";
                }
            }

            if (sentimentData.Any())
            {
                // Sort by sentiment score
                var sortedSentiments = sentimentData.OrderByDescending(s => s.Score).ToList();

                result += "### Sentiment Rankings (Best to Worst)\n";
                for (int i = 0; i < sortedSentiments.Count; i++)
                {
                    var sentiment = sortedSentiments[i];
                    var emoji = sentiment.Score > 0.2 ? "🟢" : sentiment.Score < -0.2 ? "🔴" : "🟡";
                    result += $"{i + 1}. {emoji} **{sentiment.Asset}**: {sentiment.Label} ";
                    result += $"(Score: {sentiment.Score:F2}, Confidence: {sentiment.Confidence:P0})\n";
                }

                result += "\n### Key Insights\n";
                var mostBullish = sortedSentiments.First();
                var mostBearish = sortedSentiments.Last();
                
                result += $"• **Most Bullish:** {mostBullish.Asset} ({mostBullish.Label})\n";
                result += $"• **Most Bearish:** {mostBearish.Asset} ({mostBearish.Label})\n";
                result += $"• **Sentiment Spread:** {mostBullish.Score - mostBearish.Score:F2} points\n";
                
                var avgSentiment = sentimentData.Average(s => s.Score);
                result += $"• **Market Average:** {avgSentiment:F2} ({(avgSentiment > 0 ? "Positive" : avgSentiment < 0 ? "Negative" : "Neutral")})\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error comparing sentiment across assets: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Get sentiment trend analysis for a specific asset over time")]
    public async Task<string> GetSentimentTrendAsync(
        [Description("Asset class to analyze trend for")] string assetClass,
        [Description("Number of days to look back")] int days = 7)
    {
        try
        {
            var trendAnalysis = _sentimentService.GenerateSentimentTrendAnalysis(assetClass, days);
            
            var result = $"## Sentiment Trend Analysis\n\n";
            result += $"**Asset Class:** {assetClass.ToUpper()}\n";
            result += $"**Time Period:** Last {days} days\n\n";
            result += $"### Trend Summary\n{trendAnalysis}\n\n";
            
            // Get current sentiment for comparison
            var currentReport = await _sentimentService.AnalyzeMarketSentimentAsync(assetClass);
            result += $"### Current Sentiment\n";
            result += $"**Current Score:** {currentReport.OverallSentiment.Score:F2} ({currentReport.OverallSentiment.Label})\n";
            result += $"**Confidence:** {currentReport.OverallSentiment.Confidence:P0}\n";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error getting sentiment trend: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Generate sentiment-based trading signals")]
    public async Task<string> GenerateSentimentTradingSignalsAsync(
        [Description("Asset class for trading signals")] string assetClass,
        [Description("Risk tolerance (low, medium, high)")] string riskTolerance = "medium")
    {
        try
        {
            var report = await _sentimentService.AnalyzeMarketSentimentAsync(assetClass);
            
            var result = $"## Sentiment-Based Trading Signals\n\n";
            result += $"**Asset:** {assetClass.ToUpper()}\n";
            result += $"**Risk Tolerance:** {riskTolerance.ToUpper()}\n";
            result += $"**Analysis Time:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n";
            
            // Overall signal
            var signal = GenerateSignalFromSentiment(report.OverallSentiment.Score, report.OverallSentiment.Confidence, riskTolerance);
            result += $"### Primary Signal: {signal.Type}\n";
            result += $"**Strength:** {signal.Strength}/5\n";
            result += $"**Confidence:** {report.OverallSentiment.Confidence:P0}\n\n";
            
            // Component signals
            result += "### Component Analysis\n";
            result += GenerateComponentSignals(report);
            
            // Trading recommendations
            result += $"### Trading Recommendations\n";
            foreach (var recommendation in report.TradingRecommendations)
            {
                result += $"• {recommendation}\n";
            }
            
            // Risk warnings
            result += $"\n### Risk Considerations\n";
            result += GenerateRiskWarnings(report, riskTolerance);

            return result;
        }
        catch (Exception ex)
        {
            return $"Error generating sentiment trading signals: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Get real-time sentiment alerts for extreme market conditions")]
    public async Task<string> GetSentimentAlertsAsync(
        [Description("Comma-separated list of assets to monitor")] string assets = "crypto,stocks")
    {
        try
        {
            var assetList = assets.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(a => a.Trim())
                                  .ToList();

            var result = $"## Market Sentiment Alerts\n\n";
            result += $"**Monitoring:** {string.Join(", ", assetList.Select(a => a.ToUpper()))}\n";
            result += $"**Alert Time:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n";

            var alerts = new List<string>();

            foreach (var asset in assetList)
            {
                try
                {
                    var report = await _sentimentService.AnalyzeMarketSentimentAsync(asset);
                    
                    // Check for extreme sentiment conditions
                    if (Math.Abs(report.OverallSentiment.Score) > 0.7 && report.OverallSentiment.Confidence > 0.6)
                    {
                        var alertType = report.OverallSentiment.Score > 0.7 ? "🚨 EXTREME GREED" : "🚨 EXTREME FEAR";
                        alerts.Add($"{alertType} - {asset.ToUpper()}: {report.OverallSentiment.Label} (Score: {report.OverallSentiment.Score:F2})");
                    }
                    else if (Math.Abs(report.OverallSentiment.Score) > 0.4)
                    {
                        var alertType = report.OverallSentiment.Score > 0.4 ? "⚠️ High Greed" : "⚠️ High Fear";
                        alerts.Add($"{alertType} - {asset.ToUpper()}: {report.OverallSentiment.Label} (Score: {report.OverallSentiment.Score:F2})");
                    }
                }
                catch (Exception ex)
                {
                    alerts.Add($"❌ Error monitoring {asset}: {ex.Message}");
                }
            }

            if (alerts.Any())
            {
                result += "### Active Alerts\n";
                foreach (var alert in alerts)
                {
                    result += $"{alert}\n";
                }
            }
            else
            {
                result += "### No Active Alerts\n";
                result += "All monitored assets are showing normal sentiment levels.\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error generating sentiment alerts: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Get sentiment history and analysis")]
    public string GetSentimentHistoryAsync()
    {
        try
        {
            var history = _sentimentService.GetSentimentHistory();
            
            if (!history.Any())
            {
                return "No sentiment analysis history available.";
            }

            var result = $"## Sentiment Analysis History\n\n";
            result += $"Total analyses: {history.Count}\n\n";

            // Group by asset class
            var groupedHistory = history.GroupBy(h => h.AssetClass)
                                       .OrderByDescending(g => g.Count());

            foreach (var group in groupedHistory.Take(5))
            {
                result += $"### {group.Key.ToUpper()}\n";
                result += $"**Analyses:** {group.Count()}\n";
                
                var recentAnalyses = group.OrderByDescending(h => h.Timestamp).Take(3);
                foreach (var analysis in recentAnalyses)
                {
                    result += $"• {analysis.Timestamp:MMM dd HH:mm}: ";
                    result += $"Score {analysis.SentimentScore:F2} ";
                    result += $"(Confidence: {analysis.Confidence:P0})\n";
                }
                result += "\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error retrieving sentiment history: {ex.Message}";
        }
    }

    // Helper methods
    private (string Type, int Strength) GenerateSignalFromSentiment(double score, double confidence, string riskTolerance)
    {
        var adjustedScore = score * confidence; // Confidence-weighted score
        
        var multiplier = riskTolerance.ToLower() switch
        {
            "low" => 0.5,
            "high" => 1.5,
            _ => 1.0
        };

        adjustedScore *= multiplier;

        return adjustedScore switch
        {
            > 0.6 => ("STRONG BUY", 5),
            > 0.3 => ("BUY", 4),
            > 0.1 => ("WEAK BUY", 3),
            < -0.6 => ("STRONG SELL", 5),
            < -0.3 => ("SELL", 4),
            < -0.1 => ("WEAK SELL", 3),
            _ => ("HOLD", 2)
        };
    }

    private string GenerateComponentSignals(MarketSentimentReport report)
    {
        var result = "";
        
        var components = new[]
        {
            ("📰 News", report.NewsSentiment),
            ("📱 Social", report.SocialMediaSentiment),
            ("😰 Fear/Greed", report.FearGreedIndex),
            ("📊 Technical", report.TechnicalSentiment)
        };

        foreach (var (name, sentiment) in components)
        {
            var signal = sentiment.Score switch
            {
                > 0.3 => "Bullish",
                < -0.3 => "Bearish",
                _ => "Neutral"
            };
            
            var emoji = sentiment.Score > 0.3 ? "🟢" : sentiment.Score < -0.3 ? "🔴" : "🟡";
            result += $"{emoji} {name}: {signal} ({sentiment.Score:F2})\n";
        }

        return result;
    }

    private string GenerateRiskWarnings(MarketSentimentReport report, string riskTolerance)
    {
        var warnings = new List<string>();

        if (report.OverallSentiment.Confidence < 0.5)
        {
            warnings.Add("⚠️ Low confidence in sentiment analysis - use smaller position sizes");
        }

        if (Math.Abs(report.OverallSentiment.Score) > 0.8)
        {
            warnings.Add("⚠️ Extreme sentiment levels - consider contrarian positioning");
        }

        if (riskTolerance.ToLower() == "low" && Math.Abs(report.OverallSentiment.Score) > 0.5)
        {
            warnings.Add("⚠️ High sentiment volatility - consider avoiding positions for low risk tolerance");
        }

        // Check for divergent signals
        var sentiments = new[] { report.NewsSentiment.Score, report.SocialMediaSentiment.Score, 
                               report.FearGreedIndex.Score, report.TechnicalSentiment.Score };
        var divergence = sentiments.Max() - sentiments.Min();
        
        if (divergence > 1.0)
        {
            warnings.Add("⚠️ High divergence between sentiment sources - mixed signals detected");
        }

        return warnings.Any() ? string.Join("\n", warnings) : "✅ No significant risk warnings identified";
    }
}
