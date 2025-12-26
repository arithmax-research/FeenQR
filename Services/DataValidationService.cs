using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services;

/// <summary>
/// Service for data validation and quality assurance
/// </summary>
public class DataValidationService
{
    private readonly ILogger<DataValidationService> _logger;

    public DataValidationService(ILogger<DataValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Comprehensive data validation result
    /// </summary>
    public class DataValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
        public List<OutlierInfo> Outliers { get; set; } = new();
        public double CompletenessScore { get; set; }
        public double AccuracyScore { get; set; }
    }

    /// <summary>
    /// Information about detected outliers
    /// </summary>
    public class OutlierInfo
    {
        public string Column { get; set; } = string.Empty;
        public int Index { get; set; }
        public double Value { get; set; }
        public double ZScore { get; set; }
        public string OutlierType { get; set; } = string.Empty; // "univariate", "multivariate"
    }

    /// <summary>
    /// Validate market data completeness and quality
    /// </summary>
    public async Task<DataValidationResult> ValidateMarketDataAsync(
        Dictionary<string, List<double>> data,
        string symbol,
        DateTime startDate,
        DateTime endDate)
    {
        var result = new DataValidationResult();

        try
        {
            // Check data completeness
            await CheckDataCompletenessAsync(data, startDate, endDate, result);

            // Check for outliers
            await DetectOutliersAsync(data, result);

            // Validate price relationships
            await ValidatePriceRelationshipsAsync(data, result);

            // Check for data gaps
            await CheckDataGapsAsync(data, result);

            // Calculate quality scores
            CalculateQualityScores(result);

            result.IsValid = result.Errors.Count == 0;

            _logger.LogInformation($"Data validation completed for {symbol}. Valid: {result.IsValid}, Errors: {result.Errors.Count}, Warnings: {result.Warnings.Count}");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error validating data for {symbol}");
            result.Errors.Add($"Validation failed: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }

    /// <summary>
    /// Check data completeness across expected time periods
    /// </summary>
    private async Task CheckDataCompletenessAsync(
        Dictionary<string, List<double>> data,
        DateTime startDate,
        DateTime endDate,
        DataValidationResult result)
    {
        await Task.Run(() =>
        {
            var expectedDays = (endDate - startDate).TotalDays;
            var expectedDataPoints = expectedDays * 24 * 60; // Assuming minute data

            foreach (var kvp in data)
            {
                var columnName = kvp.Key;
                var values = kvp.Value;

                // Check for null/NaN values
                var nullCount = values.Count(v => double.IsNaN(v) || double.IsInfinity(v));
                var completeness = 1.0 - (double)nullCount / values.Count;

                if (completeness < 0.95)
                {
                    result.Warnings.Add($"{columnName}: Low completeness ({completeness:P2})");
                }

                // Check data length
                if (values.Count < expectedDataPoints * 0.8)
                {
                    result.Errors.Add($"{columnName}: Insufficient data points ({values.Count} vs expected {expectedDataPoints})");
                }

                result.Metrics[$"{columnName}_Completeness"] = completeness;
                result.Metrics[$"{columnName}_DataPoints"] = values.Count;
            }
        });
    }

    /// <summary>
    /// Detect statistical outliers in the data
    /// </summary>
    private async Task DetectOutliersAsync(
        Dictionary<string, List<double>> data,
        DataValidationResult result)
    {
        await Task.Run(() =>
        {
            foreach (var kvp in data)
            {
                var columnName = kvp.Key;
                var values = kvp.Value.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();

                if (values.Count < 10) continue;

                // Calculate mean and standard deviation
                var mean = values.Average();
                var stdDev = Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1));

                // Detect outliers using Z-score > 3
                for (int i = 0; i < values.Count; i++)
                {
                    var zScore = Math.Abs((values[i] - mean) / stdDev);
                    if (zScore > 3.0)
                    {
                        result.Outliers.Add(new OutlierInfo
                        {
                            Column = columnName,
                            Index = i,
                            Value = values[i],
                            ZScore = zScore,
                            OutlierType = "univariate"
                        });
                    }
                }

                result.Metrics[$"{columnName}_OutlierCount"] = result.Outliers.Count(o => o.Column == columnName);
            }
        });
    }

    /// <summary>
    /// Validate logical relationships between prices
    /// </summary>
    private async Task ValidatePriceRelationshipsAsync(
        Dictionary<string, List<double>> data,
        DataValidationResult result)
    {
        await Task.Run(() =>
        {
            // Check OHLC relationships if available
            if (data.ContainsKey("Open") && data.ContainsKey("High") &&
                data.ContainsKey("Low") && data.ContainsKey("Close"))
            {
                var opens = data["Open"];
                var highs = data["High"];
                var lows = data["Low"];
                var closes = data["Close"];

                var minLength = new[] { opens.Count, highs.Count, lows.Count, closes.Count }.Min();

                for (int i = 0; i < minLength; i++)
                {
                    if (!double.IsNaN(opens[i]) && !double.IsNaN(highs[i]) &&
                        !double.IsNaN(lows[i]) && !double.IsNaN(closes[i]))
                    {
                        // High should be >= max(Open, Close)
                        var expectedHigh = Math.Max(opens[i], closes[i]);
                        if (highs[i] < expectedHigh)
                        {
                            result.Errors.Add($"OHLC violation at index {i}: High ({highs[i]}) < max(Open,Close) ({expectedHigh})");
                        }

                        // Low should be <= min(Open, Close)
                        var expectedLow = Math.Min(opens[i], closes[i]);
                        if (lows[i] > expectedLow)
                        {
                            result.Errors.Add($"OHLC violation at index {i}: Low ({lows[i]}) > min(Open,Close) ({expectedLow})");
                        }
                    }
                }
            }

            // Check volume is non-negative
            if (data.ContainsKey("Volume"))
            {
                var negativeVolumes = data["Volume"].Count(v => v < 0);
                if (negativeVolumes > 0)
                {
                    result.Errors.Add($"Found {negativeVolumes} negative volume values");
                }
            }
        });
    }

    /// <summary>
    /// Check for significant data gaps
    /// </summary>
    private async Task CheckDataGapsAsync(
        Dictionary<string, List<double>> data,
        DataValidationResult result)
    {
        await Task.Run(() =>
        {
            foreach (var kvp in data)
            {
                var columnName = kvp.Key;
                var values = kvp.Value;

                // Count consecutive missing values
                int maxGap = 0;
                int currentGap = 0;

                foreach (var value in values)
                {
                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        currentGap++;
                        maxGap = Math.Max(maxGap, currentGap);
                    }
                    else
                    {
                        currentGap = 0;
                    }
                }

                if (maxGap > 60) // More than 1 hour of missing data
                {
                    result.Warnings.Add($"{columnName}: Large data gap detected ({maxGap} consecutive missing values)");
                }

                result.Metrics[$"{columnName}_MaxGap"] = maxGap;
            }
        });
    }

    /// <summary>
    /// Calculate overall quality scores
    /// </summary>
    private void CalculateQualityScores(DataValidationResult result)
    {
        // Completeness score based on data availability
        var completenessMetrics = result.Metrics.Where(m => m.Key.Contains("_Completeness")).ToList();
        if (completenessMetrics.Any())
        {
            result.CompletenessScore = completenessMetrics.Average(m => (double)m.Value);
        }

        // Accuracy score based on validation errors
        var errorPenalty = Math.Min(result.Errors.Count * 0.1, 0.5); // Max 50% penalty
        var warningPenalty = Math.Min(result.Warnings.Count * 0.05, 0.25); // Max 25% penalty
        result.AccuracyScore = Math.Max(0, 1.0 - errorPenalty - warningPenalty);
    }

    /// <summary>
    /// Generate data quality report
    /// </summary>
    public async Task<string> GenerateQualityReportAsync(DataValidationResult result, string symbol)
    {
        return await Task.Run(() =>
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"=== Data Quality Report for {symbol} ===");
            report.AppendLine($"Overall Status: {(result.IsValid ? "PASS" : "FAIL")}");
            report.AppendLine($"Completeness Score: {result.CompletenessScore:P2}");
            report.AppendLine($"Accuracy Score: {result.AccuracyScore:P2}");
            report.AppendLine();

            if (result.Errors.Any())
            {
                report.AppendLine("Errors:");
                foreach (var error in result.Errors)
                {
                    report.AppendLine($"  - {error}");
                }
                report.AppendLine();
            }

            if (result.Warnings.Any())
            {
                report.AppendLine("Warnings:");
                foreach (var warning in result.Warnings)
                {
                    report.AppendLine($"  - {warning}");
                }
                report.AppendLine();
            }

            if (result.Outliers.Any())
            {
                report.AppendLine($"Outliers Detected: {result.Outliers.Count}");
                var outliersByColumn = result.Outliers.GroupBy(o => o.Column);
                foreach (var group in outliersByColumn)
                {
                    report.AppendLine($"  {group.Key}: {group.Count()} outliers");
                }
                report.AppendLine();
            }

            report.AppendLine("Key Metrics:");
            foreach (var metric in result.Metrics)
            {
                report.AppendLine($"  {metric.Key}: {metric.Value}");
            }

            return report.ToString();
        });
    }
}