using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace QuantResearchAgent.Services;

public class FeatureEngineeringService
{
    private readonly ILogger<FeatureEngineeringService> _logger;

    public FeatureEngineeringService(ILogger<FeatureEngineeringService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate comprehensive technical indicators for time series data
    /// </summary>
    public FeatureEngineeringResult GenerateTechnicalIndicators(IEnumerable<double> prices, IEnumerable<DateTime> dates)
    {
        try
        {
            var priceArray = prices.ToArray();
            var dateArray = dates.ToArray();

            if (priceArray.Length != dateArray.Length || priceArray.Length < 20)
            {
                throw new ArgumentException("Need at least 20 data points with matching dates");
            }

            var indicators = new Dictionary<string, List<double>>();

            // Moving Averages
            indicators["SMA_20"] = CalculateSMA(priceArray, 20).ToList();
            indicators["SMA_50"] = CalculateSMA(priceArray, 50).ToList();
            indicators["EMA_12"] = CalculateEMA(priceArray, 12).ToList();
            indicators["EMA_26"] = CalculateEMA(priceArray, 26).ToList();

            // Momentum Indicators
            indicators["RSI"] = CalculateRSI(priceArray, 14).ToList();
            indicators["MACD"] = CalculateMACD(priceArray).ToList();
            indicators["MACD_Signal"] = CalculateMACDSignal(indicators["MACD"]).ToList();
            indicators["MACD_Histogram"] = CalculateMACDHistogram(indicators["MACD"], indicators["MACD_Signal"]).ToList();

            // Volatility Indicators
            indicators["ATR"] = CalculateATR(priceArray, 14).ToList();
            indicators["Bollinger_Upper"] = CalculateBollingerUpper(priceArray, 20, 2).ToList();
            indicators["Bollinger_Lower"] = CalculateBollingerLower(priceArray, 20, 2).ToList();
            indicators["Bollinger_Middle"] = CalculateSMA(priceArray, 20).ToList();

            // Volume-based indicators (using price changes as proxy for volume)
            var returns = CalculateReturns(priceArray);
            indicators["OBV"] = CalculateOBV(priceArray, returns).ToList();

            // Statistical indicators
            indicators["Price_Change"] = returns.ToList();
            indicators["Log_Returns"] = returns.Select(r => Math.Log(1 + r)).ToList();

            return new FeatureEngineeringResult
            {
                Symbol = "Unknown",
                TechnicalIndicators = indicators,
                Dates = dateArray.ToList(),
                FeatureImportance = new FeatureImportanceResult()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating technical indicators");
            throw;
        }
    }

    /// <summary>
    /// Create lagged features for time series prediction
    /// </summary>
    public Dictionary<string, List<double>> CreateLaggedFeatures(IEnumerable<double> data, int maxLag = 5)
    {
        try
        {
            var dataArray = data.ToArray();
            var laggedFeatures = new Dictionary<string, List<double>>();

            for (int lag = 1; lag <= maxLag; lag++)
            {
                var lagged = new List<double>();
                for (int i = 0; i < dataArray.Length; i++)
                {
                    if (i >= lag)
                        lagged.Add(dataArray[i - lag]);
                    else
                        lagged.Add(0); // Pad with zeros for early values
                }
                laggedFeatures[$"Lag_{lag}"] = lagged;
            }

            return laggedFeatures;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating lagged features");
            throw;
        }
    }

    /// <summary>
    /// Compute rolling statistics for time series data
    /// </summary>
    public Dictionary<string, List<double>> ComputeRollingStatistics(IEnumerable<double> data, int windowSize = 20)
    {
        try
        {
            var dataArray = data.ToArray();
            var rollingStats = new Dictionary<string, List<double>>();

            // Rolling mean
            rollingStats["Rolling_Mean"] = CalculateRollingMean(dataArray, windowSize).ToList();

            // Rolling standard deviation
            rollingStats["Rolling_Std"] = CalculateRollingStd(dataArray, windowSize).ToList();

            // Rolling skewness
            rollingStats["Rolling_Skewness"] = CalculateRollingSkewness(dataArray, windowSize).ToList();

            // Rolling kurtosis
            rollingStats["Rolling_Kurtosis"] = CalculateRollingKurtosis(dataArray, windowSize).ToList();

            // Rolling min/max
            rollingStats["Rolling_Min"] = CalculateRollingMin(dataArray, windowSize).ToList();
            rollingStats["Rolling_Max"] = CalculateRollingMax(dataArray, windowSize).ToList();

            // Rolling range
            rollingStats["Rolling_Range"] = rollingStats["Rolling_Max"]
                .Zip(rollingStats["Rolling_Min"], (max, min) => max - min).ToList();

            return rollingStats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing rolling statistics");
            throw;
        }
    }

    /// <summary>
    /// Analyze feature importance using correlation analysis
    /// </summary>
    public FeatureImportanceResult AnalyzeFeatureImportance(
        Dictionary<string, List<double>> features,
        IEnumerable<double> target,
        string method = "Correlation")
    {
        try
        {
            var targetArray = target.ToArray();
            var importanceScores = new Dictionary<string, double>();

            foreach (var feature in features)
            {
                if (feature.Value.Count == targetArray.Length)
                {
                    var correlation = Correlation.Pearson(feature.Value.ToArray(), targetArray);
                    importanceScores[feature.Key] = Math.Abs(correlation);
                }
            }

            return new FeatureImportanceResult
            {
                FeatureScores = importanceScores.OrderByDescending(x => x.Value)
                    .ToDictionary(x => x.Key, x => x.Value),
                Method = method
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing feature importance");
            throw;
        }
    }

    /// <summary>
    /// Create comprehensive feature set combining all engineering techniques
    /// </summary>
    public FeatureEngineeringResult CreateComprehensiveFeatures(
        IEnumerable<double> prices,
        IEnumerable<DateTime> dates,
        int maxLag = 5,
        int rollingWindow = 20)
    {
        try
        {
            // Generate technical indicators
            var technicalResult = GenerateTechnicalIndicators(prices, dates);

            // Create lagged features from price data
            var laggedFeatures = CreateLaggedFeatures(prices, maxLag);

            // Compute rolling statistics
            var rollingStats = ComputeRollingStatistics(prices, rollingWindow);

            // Combine all features
            var allFeatures = new Dictionary<string, List<double>>();
            foreach (var indicator in technicalResult.TechnicalIndicators)
            {
                allFeatures[indicator.Key] = indicator.Value;
            }
            foreach (var lagFeature in laggedFeatures)
            {
                allFeatures[lagFeature.Key] = lagFeature.Value;
            }
            foreach (var rollingStat in rollingStats)
            {
                allFeatures[rollingStat.Key] = rollingStat.Value;
            }

            // Analyze feature importance (using price as target for demonstration)
            var featureImportance = AnalyzeFeatureImportance(allFeatures, prices);

            return new FeatureEngineeringResult
            {
                Symbol = technicalResult.Symbol,
                TechnicalIndicators = technicalResult.TechnicalIndicators,
                LaggedFeatures = laggedFeatures,
                RollingStatistics = rollingStats,
                Dates = technicalResult.Dates,
                FeatureImportance = featureImportance
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating comprehensive features");
            throw;
        }
    }

    // Helper methods for technical indicators

    private IEnumerable<double> CalculateSMA(double[] data, int period)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (i < period - 1)
                yield return double.NaN;
            else
            {
                var sum = 0.0;
                for (int j = i - period + 1; j <= i; j++)
                    sum += data[j];
                yield return sum / period;
            }
        }
    }

    private IEnumerable<double> CalculateEMA(double[] data, int period)
    {
        var multiplier = 2.0 / (period + 1);
        var ema = new List<double>();

        // First EMA is SMA
        var sma = data.Take(period).Average();
        ema.Add(sma);

        for (int i = period; i < data.Length; i++)
        {
            var currentEMA = (data[i] - ema.Last()) * multiplier + ema.Last();
            ema.Add(currentEMA);
        }

        // Pad with NaN for early values
        while (ema.Count < data.Length)
            ema.Insert(0, double.NaN);

        return ema;
    }

    private IEnumerable<double> CalculateRSI(double[] data, int period)
    {
        var gains = new List<double>();
        var losses = new List<double>();

        for (int i = 1; i < data.Length; i++)
        {
            var change = data[i] - data[i - 1];
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? -change : 0);
        }

        for (int i = 0; i < data.Length; i++)
        {
            if (i < period)
                yield return double.NaN;
            else
            {
                var avgGain = gains.Skip(i - period).Take(period).Average();
                var avgLoss = losses.Skip(i - period).Take(period).Average();

                if (avgLoss == 0)
                    yield return 100;
                else
                {
                    var rs = avgGain / avgLoss;
                    yield return 100 - (100 / (1 + rs));
                }
            }
        }
    }

    private IEnumerable<double> CalculateMACD(double[] data)
    {
        var ema12 = CalculateEMA(data, 12).ToArray();
        var ema26 = CalculateEMA(data, 26).ToArray();

        for (int i = 0; i < data.Length; i++)
        {
            if (double.IsNaN(ema12[i]) || double.IsNaN(ema26[i]))
                yield return double.NaN;
            else
                yield return ema12[i] - ema26[i];
        }
    }

    private IEnumerable<double> CalculateMACDSignal(IEnumerable<double> macd)
    {
        return CalculateEMA(macd.Where(x => !double.IsNaN(x)).ToArray(), 9);
    }

    private IEnumerable<double> CalculateMACDHistogram(IEnumerable<double> macd, IEnumerable<double> signal)
    {
        return macd.Zip(signal, (m, s) => double.IsNaN(m) || double.IsNaN(s) ? double.NaN : m - s);
    }

    private IEnumerable<double> CalculateATR(double[] data, int period)
    {
        var trValues = new List<double>();

        for (int i = 1; i < data.Length; i++)
        {
            var tr = data[i] - data[i - 1]; // Simplified TR calculation
            trValues.Add(Math.Abs(tr));
        }

        return CalculateSMA(trValues.ToArray(), period);
    }

    private IEnumerable<double> CalculateBollingerUpper(double[] data, int period, double stdDev)
    {
        var sma = CalculateSMA(data, period).ToArray();
        var rollingStd = CalculateRollingStd(data, period).ToArray();

        for (int i = 0; i < data.Length; i++)
        {
            if (double.IsNaN(sma[i]) || double.IsNaN(rollingStd[i]))
                yield return double.NaN;
            else
                yield return sma[i] + (stdDev * rollingStd[i]);
        }
    }

    private IEnumerable<double> CalculateBollingerLower(double[] data, int period, double stdDev)
    {
        var sma = CalculateSMA(data, period).ToArray();
        var rollingStd = CalculateRollingStd(data, period).ToArray();

        for (int i = 0; i < data.Length; i++)
        {
            if (double.IsNaN(sma[i]) || double.IsNaN(rollingStd[i]))
                yield return double.NaN;
            else
                yield return sma[i] - (stdDev * rollingStd[i]);
        }
    }

    private IEnumerable<double> CalculateReturns(double[] data)
    {
        for (int i = 1; i < data.Length; i++)
        {
            yield return (data[i] - data[i - 1]) / data[i - 1];
        }
        yield return 0; // Pad to match length
    }

    private IEnumerable<double> CalculateOBV(double[] prices, IEnumerable<double> returns)
    {
        var obv = new List<double> { 0 }; // Start with 0

        foreach (var ret in returns.Skip(1)) // Skip the padding value
        {
            var lastObv = obv.Last();
            if (ret > 0)
                obv.Add(lastObv + 1); // Simplified OBV
            else if (ret < 0)
                obv.Add(lastObv - 1);
            else
                obv.Add(lastObv);
        }

        return obv;
    }

    private IEnumerable<double> CalculateRollingMean(double[] data, int window)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (i < window - 1)
                yield return double.NaN;
            else
            {
                var windowData = data.Skip(i - window + 1).Take(window);
                yield return windowData.Average();
            }
        }
    }

    private IEnumerable<double> CalculateRollingStd(double[] data, int window)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (i < window - 1)
                yield return double.NaN;
            else
            {
                var windowData = data.Skip(i - window + 1).Take(window);
                yield return Statistics.StandardDeviation(windowData);
            }
        }
    }

    private IEnumerable<double> CalculateRollingSkewness(double[] data, int window)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (i < window - 1)
                yield return double.NaN;
            else
            {
                var windowData = data.Skip(i - window + 1).Take(window).ToArray();
                yield return Statistics.Skewness(windowData);
            }
        }
    }

    private IEnumerable<double> CalculateRollingKurtosis(double[] data, int window)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (i < window - 1)
                yield return double.NaN;
            else
            {
                var windowData = data.Skip(i - window + 1).Take(window).ToArray();
                yield return Statistics.Kurtosis(windowData);
            }
        }
    }

    private IEnumerable<double> CalculateRollingMin(double[] data, int window)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (i < window - 1)
                yield return double.NaN;
            else
            {
                var windowData = data.Skip(i - window + 1).Take(window);
                yield return windowData.Min();
            }
        }
    }

    private IEnumerable<double> CalculateRollingMax(double[] data, int window)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (i < window - 1)
                yield return double.NaN;
            else
            {
                var windowData = data.Skip(i - window + 1).Take(window);
                yield return windowData.Max();
            }
        }
    }
}