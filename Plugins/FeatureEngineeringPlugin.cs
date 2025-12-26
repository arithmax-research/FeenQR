using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;
using System.ComponentModel;
using System.Text.Json;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for feature engineering and technical analysis
/// </summary>
public class FeatureEngineeringPlugin
{
    private readonly FeatureEngineeringService _featureEngineeringService;

    public FeatureEngineeringPlugin(FeatureEngineeringService featureEngineeringService)
    {
        _featureEngineeringService = featureEngineeringService;
    }

    [KernelFunction, Description("Generate comprehensive technical indicators for price data")]
    public async Task<string> GenerateTechnicalIndicatorsAsync(
        [Description("Array of price values")] double[] prices,
        [Description("Array of corresponding dates")] string[] dateStrings)
    {
        try
        {
            var dates = dateStrings.Select(d => DateTime.Parse(d)).ToArray();
            var result = _featureEngineeringService.GenerateTechnicalIndicators(prices, dates);

            var summary = $"Technical Indicators Generated for {result.Symbol}:\n" +
                         $"Data Points: {result.Dates.Count}\n\n" +
                         $"Moving Averages:\n" +
                         $"- SMA 20: Latest = {GetLatestValue(result.TechnicalIndicators["SMA_20"]):F2}\n" +
                         $"- SMA 50: Latest = {GetLatestValue(result.TechnicalIndicators["SMA_50"]):F2}\n" +
                         $"- EMA 12: Latest = {GetLatestValue(result.TechnicalIndicators["EMA_12"]):F2}\n" +
                         $"- EMA 26: Latest = {GetLatestValue(result.TechnicalIndicators["EMA_26"]):F2}\n\n" +
                         $"Momentum Indicators:\n" +
                         $"- RSI (14): Latest = {GetLatestValue(result.TechnicalIndicators["RSI"]):F1}\n" +
                         $"- MACD: Latest = {GetLatestValue(result.TechnicalIndicators["MACD"]):F4}\n" +
                         $"- MACD Signal: Latest = {GetLatestValue(result.TechnicalIndicators["MACD_Signal"]):F4}\n\n" +
                         $"Volatility Indicators:\n" +
                         $"- ATR (14): Latest = {GetLatestValue(result.TechnicalIndicators["ATR"]):F2}\n" +
                         $"- Bollinger Upper: Latest = {GetLatestValue(result.TechnicalIndicators["Bollinger_Upper"]):F2}\n" +
                         $"- Bollinger Lower: Latest = {GetLatestValue(result.TechnicalIndicators["Bollinger_Lower"]):F2}\n\n";

            // Add signal interpretation
            var latestRsi = GetLatestValue(result.TechnicalIndicators["RSI"]);
            var latestMacd = GetLatestValue(result.TechnicalIndicators["MACD"]);
            var latestMacdSignal = GetLatestValue(result.TechnicalIndicators["MACD_Signal"]);

            summary += "Signal Interpretation:\n";
            if (latestRsi < 30)
                summary += "- RSI indicates oversold conditions\n";
            else if (latestRsi > 70)
                summary += "- RSI indicates overbought conditions\n";
            else
                summary += "- RSI in neutral zone\n";

            if (latestMacd > latestMacdSignal)
                summary += "- MACD shows bullish momentum\n";
            else
                summary += "- MACD shows bearish momentum\n";

            return summary;
        }
        catch (Exception ex)
        {
            return $"Error generating technical indicators: {ex.Message}";
        }
    }

    [KernelFunction, Description("Create lagged features for time series prediction")]
    public async Task<string> CreateLaggedFeaturesAsync(
        [Description("Array of time series values")] double[] data,
        [Description("Maximum lag order (default: 5)")] int maxLag = 5)
    {
        try
        {
            var laggedFeatures = _featureEngineeringService.CreateLaggedFeatures(data, maxLag);

            var summary = $"Lagged Features Created (Max Lag: {maxLag}):\n\n";
            foreach (var feature in laggedFeatures)
            {
                var latestValue = feature.Value.LastOrDefault();
                summary += $"{feature.Key}: Latest = {latestValue:F4}\n";
            }

            summary += $"\nTotal Features: {laggedFeatures.Count}\n" +
                      $"Data Points: {data.Length}\n\n" +
                      "These lagged features can be used as inputs for:\n" +
                      "- Autoregressive models (AR)\n" +
                      "- Machine learning algorithms\n" +
                      "- Feature engineering pipelines";

            return summary;
        }
        catch (Exception ex)
        {
            return $"Error creating lagged features: {ex.Message}";
        }
    }

    [KernelFunction, Description("Compute rolling statistics for time series analysis")]
    public async Task<string> ComputeRollingStatisticsAsync(
        [Description("Array of time series values")] double[] data,
        [Description("Rolling window size (default: 20)")] int windowSize = 20)
    {
        try
        {
            var rollingStats = _featureEngineeringService.ComputeRollingStatistics(data, windowSize);

            var summary = $"Rolling Statistics Computed (Window: {windowSize}):\n\n";
            foreach (var stat in rollingStats)
            {
                var latestValue = stat.Value.LastOrDefault(v => !double.IsNaN(v));
                summary += $"{stat.Key}: Latest = {latestValue:F4}\n";
            }

            summary += $"\nData Points: {data.Length}\n" +
                      $"Effective Window: {windowSize}\n\n" +
                      "Rolling statistics help identify:\n" +
                      "- Local trends and volatility\n" +
                      "- Changing statistical properties\n" +
                      "- Risk metrics over time";

            return summary;
        }
        catch (Exception ex)
        {
            return $"Error computing rolling statistics: {ex.Message}";
        }
    }

    [KernelFunction, Description("Analyze feature importance using correlation analysis")]
    public async Task<string> AnalyzeFeatureImportanceAsync(
        [Description("Dictionary of feature arrays as JSON string")] string featuresJson,
        [Description("Array of target values")] double[] target)
    {
        try
        {
            var features = JsonSerializer.Deserialize<Dictionary<string, List<double>>>(featuresJson);

            if (features == null)
                return "Error: Invalid features JSON format";

            var importanceResult = _featureEngineeringService.AnalyzeFeatureImportance(features, target);

            var summary = $"Feature Importance Analysis (Method: {importanceResult.Method}):\n\n";
            summary += "| Feature | Importance Score |\n";
            summary += "|---------|------------------|\n";

            foreach (var feature in importanceResult.FeatureScores.Take(10))
            {
                summary += $"| {feature.Key} | {feature.Value:F4} |\n";
            }

            if (importanceResult.FeatureScores.Count > 10)
            {
                summary += $"| ... and {importanceResult.FeatureScores.Count - 10} more |\n";
            }

            summary += $"\nTop 3 Most Important Features:\n";
            var topFeatures = importanceResult.FeatureScores.Take(3).ToList();
            for (int i = 0; i < topFeatures.Count; i++)
            {
                summary += $"{i + 1}. {topFeatures[i].Key} (Score: {topFeatures[i].Value:F4})\n";
            }

            summary += $"\nInterpretation:\n" +
                      $"- Higher scores indicate stronger correlation with target\n" +
                      $"- Features with scores > 0.7 have strong predictive power\n" +
                      $"- Consider removing features with scores < 0.1";

            return summary;
        }
        catch (Exception ex)
        {
            return $"Error analyzing feature importance: {ex.Message}";
        }
    }

    [KernelFunction, Description("Create comprehensive feature set combining technical indicators, lags, and rolling statistics")]
    public async Task<string> CreateComprehensiveFeaturesAsync(
        [Description("Array of price values")] double[] prices,
        [Description("Array of corresponding dates")] string[] dateStrings,
        [Description("Maximum lag for lagged features (default: 5)")] int maxLag = 5,
        [Description("Window size for rolling statistics (default: 20)")] int rollingWindow = 20)
    {
        try
        {
            var dates = dateStrings.Select(d => DateTime.Parse(d)).ToArray();
            var result = _featureEngineeringService.CreateComprehensiveFeatures(prices, dates, maxLag, rollingWindow);

            var summary = $"Comprehensive Feature Engineering Completed:\n\n" +
                         $"Symbol: {result.Symbol}\n" +
                         $"Data Points: {result.Dates.Count}\n" +
                         $"Date Range: {result.Dates.First():yyyy-MM-dd} to {result.Dates.Last():yyyy-MM-dd}\n\n" +
                         $"Feature Categories:\n" +
                         $"- Technical Indicators: {result.TechnicalIndicators.Count}\n" +
                         $"- Lagged Features: {result.LaggedFeatures.Count}\n" +
                         $"- Rolling Statistics: {result.RollingStatistics.Count}\n" +
                         $"- Total Features: {result.TechnicalIndicators.Count + result.LaggedFeatures.Count + result.RollingStatistics.Count}\n\n";

            // Show top 5 most important features
            if (result.FeatureImportance.FeatureScores.Any())
            {
                summary += $"Top 5 Most Important Features ({result.FeatureImportance.Method}):\n";
                var topFeatures = result.FeatureImportance.FeatureScores.Take(5).ToList();
                for (int i = 0; i < topFeatures.Count; i++)
                {
                    summary += $"{i + 1}. {topFeatures[i].Key} (Score: {topFeatures[i].Value:F4})\n";
                }
                summary += "\n";
            }

            // Feature engineering recommendations
            summary += "Feature Engineering Recommendations:\n" +
                      "- Use technical indicators for short-term trading signals\n" +
                      "- Include lagged features for autoregressive modeling\n" +
                      "- Rolling statistics help capture changing market conditions\n" +
                      "- Consider feature selection based on importance scores\n" +
                      "- Normalize features before feeding to ML models\n\n" +
                      "Next Steps:\n" +
                      "- Validate features on out-of-sample data\n" +
                      "- Check for multicollinearity between features\n" +
                      "- Consider feature interactions and polynomial features";

            return summary;
        }
        catch (Exception ex)
        {
            return $"Error creating comprehensive features: {ex.Message}";
        }
    }

    [KernelFunction, Description("Generate trading signals based on technical indicators")]
    public async Task<string> GenerateTradingSignalsAsync(
        [Description("Array of price values")] double[] prices,
        [Description("Array of corresponding dates")] string[] dateStrings)
    {
        try
        {
            var dates = dateStrings.Select(d => DateTime.Parse(d)).ToArray();
            var technicalResult = _featureEngineeringService.GenerateTechnicalIndicators(prices, dates);

            var signals = new List<string>();
            var latestPrices = prices.TakeLast(5).ToArray();
            var latestRsi = technicalResult.TechnicalIndicators["RSI"].TakeLast(5).ToArray();
            var latestMacd = technicalResult.TechnicalIndicators["MACD"].TakeLast(5).ToArray();
            var latestMacdSignal = technicalResult.TechnicalIndicators["MACD_Signal"].TakeLast(5).ToArray();
            var latestBBUpper = technicalResult.TechnicalIndicators["Bollinger_Upper"].TakeLast(5).ToArray();
            var latestBBLower = technicalResult.TechnicalIndicators["Bollinger_Lower"].TakeLast(5).ToArray();

            // Generate signals based on recent data
            var currentPrice = latestPrices.Last();
            var currentRsi = latestRsi.Last();
            var currentMacd = latestMacd.Last();
            var currentMacdSignal = latestMacdSignal.Last();
            var currentBBUpper = latestBBUpper.Last();
            var currentBBLower = latestBBLower.Last();

            signals.Add($"Current Price: ${currentPrice:F2}");

            // RSI signals
            if (currentRsi < 30)
                signals.Add("RSI OVERSOLD: Potential buying opportunity");
            else if (currentRsi > 70)
                signals.Add("RSI OVERBOUGHT: Potential selling opportunity");
            else
                signals.Add("RSI NEUTRAL: No clear signal");

            // MACD signals
            if (currentMacd > currentMacdSignal && latestMacd[latestMacd.Length - 2] <= latestMacdSignal[latestMacdSignal.Length - 2])
                signals.Add("MACD BULLISH CROSSOVER: Potential buy signal");
            else if (currentMacd < currentMacdSignal && latestMacd[latestMacd.Length - 2] >= latestMacdSignal[latestMacdSignal.Length - 2])
                signals.Add("MACD BEARISH CROSSOVER: Potential sell signal");

            // Bollinger Band signals
            if (currentPrice <= currentBBLower)
                signals.Add("BOLLINGER BAND OVERSOLD: Price near lower band");
            else if (currentPrice >= currentBBUpper)
                signals.Add("BOLLINGER BAND OVERBOUGHT: Price near upper band");

            // Trend signals
            var sma20 = technicalResult.TechnicalIndicators["SMA_20"].Last();
            var sma50 = technicalResult.TechnicalIndicators["SMA_50"].Last();

            if (currentPrice > sma20 && sma20 > sma50)
                signals.Add("STRONG UPTREND: Price above both moving averages");
            else if (currentPrice < sma20 && sma20 < sma50)
                signals.Add("STRONG DOWNTREND: Price below both moving averages");

            var summary = "Technical Analysis Trading Signals:\n\n" +
                         string.Join("\n", signals) + "\n\n" +
                         "Important Notes:\n" +
                         "- These are technical signals only, not financial advice\n" +
                         "- Always combine with fundamental analysis\n" +
                         "- Consider risk management and position sizing\n" +
                         "- Backtest signals before live trading";

            return summary;
        }
        catch (Exception ex)
        {
            return $"Error generating trading signals: {ex.Message}";
        }
    }

    private double GetLatestValue(List<double> values)
    {
        return values.LastOrDefault(v => !double.IsNaN(v));
    }
}