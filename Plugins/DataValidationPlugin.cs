using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Plugin for data validation and quality assurance functions
/// </summary>
public class DataValidationPlugin
{
    private readonly DataValidationService _dataValidationService;

    public DataValidationPlugin(DataValidationService dataValidationService)
    {
        _dataValidationService = dataValidationService;
    }

    /// <summary>
    /// Validate market data quality and completeness
    /// </summary>
    [KernelFunction, Description("Validate market data for quality, completeness, and statistical anomalies")]
    public async Task<string> ValidateMarketDataAsync(
        [Description("Stock symbol or identifier")] string symbol,
        [Description("Dictionary of data columns (JSON format with column names as keys and arrays of values as values)")] string dataJson,
        [Description("Start date for validation period (ISO format)")] string startDate,
        [Description("End date for validation period (ISO format)")] string endDate)
    {
        try
        {
            // Parse the JSON data
            var dataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<double>>>(dataJson);
            if (dataDict == null)
            {
                return "Error: Invalid data format. Expected JSON object with column names as keys and arrays of numbers as values.";
            }

            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            // Perform validation
            var result = await _dataValidationService.ValidateMarketDataAsync(dataDict, symbol, start, end);

            // Generate report
            var report = await _dataValidationService.GenerateQualityReportAsync(result, symbol);

            return report;
        }
        catch (Exception ex)
        {
            return $"Error validating market data: {ex.Message}";
        }
    }

    /// <summary>
    /// Check data completeness for a specific dataset
    /// </summary>
    [KernelFunction, Description("Check data completeness and identify missing values")]
    public async Task<string> CheckDataCompletenessAsync(
        [Description("Dictionary of data columns (JSON format)")] string dataJson,
        [Description("Minimum acceptable completeness ratio (0.0 to 1.0)")] double minCompleteness = 0.95)
    {
        try
        {
            var dataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<double>>>(dataJson);
            if (dataDict == null)
            {
                return "Error: Invalid data format.";
            }

            var completenessResults = new System.Text.StringBuilder();
            completenessResults.AppendLine("Data Completeness Analysis:");
            completenessResults.AppendLine("==========================");

            foreach (var kvp in dataDict)
            {
                var columnName = kvp.Key;
                var values = kvp.Value;

                var nullCount = values.Count(v => double.IsNaN(v) || double.IsInfinity(v));
                var completeness = 1.0 - (double)nullCount / values.Count;

                var status = completeness >= minCompleteness ? "PASS" : "FAIL";
                completenessResults.AppendLine($"{columnName}: {completeness:P2} ({status}) - {nullCount} missing values out of {values.Count} total");
            }

            return completenessResults.ToString();
        }
        catch (Exception ex)
        {
            return $"Error checking data completeness: {ex.Message}";
        }
    }

    /// <summary>
    /// Detect statistical outliers in data
    /// </summary>
    [KernelFunction, Description("Detect statistical outliers using Z-score analysis")]
    public async Task<string> DetectOutliersAsync(
        [Description("Dictionary of data columns (JSON format)")] string dataJson,
        [Description("Z-score threshold for outlier detection")] double zThreshold = 3.0)
    {
        try
        {
            var dataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<double>>>(dataJson);
            if (dataDict == null)
            {
                return "Error: Invalid data format.";
            }

            var outlierResults = new System.Text.StringBuilder();
            outlierResults.AppendLine("Outlier Detection Results:");
            outlierResults.AppendLine("=========================");

            foreach (var kvp in dataDict)
            {
                var columnName = kvp.Key;
                var values = kvp.Value.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();

                if (values.Count < 3)
                {
                    outlierResults.AppendLine($"{columnName}: Insufficient data for outlier detection");
                    continue;
                }

                var mean = values.Average();
                var stdDev = Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1));

                var outliers = new List<(int index, double value, double zScore)>();
                for (int i = 0; i < values.Count; i++)
                {
                    var zScore = Math.Abs((values[i] - mean) / stdDev);
                    if (zScore > zThreshold)
                    {
                        outliers.Add((i, values[i], zScore));
                    }
                }

                outlierResults.AppendLine($"{columnName}: {outliers.Count} outliers detected");
                foreach (var outlier in outliers.Take(5)) // Show first 5 outliers
                {
                    outlierResults.AppendLine($"  Index {outlier.index}: Value={outlier.value:F4}, Z-Score={outlier.zScore:F2}");
                }

                if (outliers.Count > 5)
                {
                    outlierResults.AppendLine($"  ... and {outliers.Count - 5} more outliers");
                }
            }

            return outlierResults.ToString();
        }
        catch (Exception ex)
        {
            return $"Error detecting outliers: {ex.Message}";
        }
    }

    /// <summary>
    /// Validate OHLC price relationships
    /// </summary>
    [KernelFunction, Description("Validate OHLC price relationships and logical constraints")]
    public async Task<string> ValidatePriceRelationshipsAsync(
        [Description("Open prices array (JSON format)")] string openPricesJson,
        [Description("High prices array (JSON format)")] string highPricesJson,
        [Description("Low prices array (JSON format)")] string lowPricesJson,
        [Description("Close prices array (JSON format)")] string closePricesJson)
    {
        try
        {
            var opens = System.Text.Json.JsonSerializer.Deserialize<List<double>>(openPricesJson);
            var highs = System.Text.Json.JsonSerializer.Deserialize<List<double>>(highPricesJson);
            var lows = System.Text.Json.JsonSerializer.Deserialize<List<double>>(lowPricesJson);
            var closes = System.Text.Json.JsonSerializer.Deserialize<List<double>>(closePricesJson);

            if (opens == null || highs == null || lows == null || closes == null)
            {
                return "Error: Invalid price data format.";
            }

            var minLength = new[] { opens.Count, highs.Count, lows.Count, closes.Count }.Min();
            var violations = new List<string>();

            for (int i = 0; i < minLength; i++)
            {
                if (double.IsNaN(opens[i]) || double.IsNaN(highs[i]) ||
                    double.IsNaN(lows[i]) || double.IsNaN(closes[i]))
                {
                    continue; // Skip NaN values
                }

                // High should be >= max(Open, Close)
                var expectedHigh = Math.Max(opens[i], closes[i]);
                if (highs[i] < expectedHigh - 0.0001) // Small tolerance for floating point
                {
                    violations.Add($"OHLC violation at index {i}: High ({highs[i]:F4}) < max(Open,Close) ({expectedHigh:F4})");
                }

                // Low should be <= min(Open, Close)
                var expectedLow = Math.Min(opens[i], closes[i]);
                if (lows[i] > expectedLow + 0.0001)
                {
                    violations.Add($"OHLC violation at index {i}: Low ({lows[i]:F4}) > min(Open,Close) ({expectedLow:F4})");
                }
            }

            var result = new System.Text.StringBuilder();
            result.AppendLine("OHLC Validation Results:");
            result.AppendLine("=======================");
            result.AppendLine($"Analyzed {minLength} price bars");

            if (violations.Any())
            {
                result.AppendLine($"{violations.Count} violations found:");
                foreach (var violation in violations.Take(10)) // Show first 10 violations
                {
                    result.AppendLine($"  - {violation}");
                }
                if (violations.Count > 10)
                {
                    result.AppendLine($"  ... and {violations.Count - 10} more violations");
                }
            }
            else
            {
                result.AppendLine("✓ No OHLC violations detected");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error validating price relationships: {ex.Message}";
        }
    }

    /// <summary>
    /// Generate data quality metrics
    /// </summary>
    [KernelFunction, Description("Generate comprehensive data quality metrics and recommendations")]
    public async Task<string> GenerateDataQualityReportAsync(
        [Description("Dictionary of data columns (JSON format)")] string dataJson,
        [Description("Dataset name or identifier")] string datasetName = "Dataset")
    {
        try
        {
            var dataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<double>>>(dataJson);
            if (dataDict == null)
            {
                return "Error: Invalid data format.";
            }

            var report = new System.Text.StringBuilder();
            report.AppendLine($"Data Quality Report for {datasetName}");
            report.AppendLine("=====================================");
            report.AppendLine();

            // Basic statistics
            report.AppendLine("Column Statistics:");
            report.AppendLine("-----------------");

            foreach (var kvp in dataDict)
            {
                var columnName = kvp.Key;
                var values = kvp.Value.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();

                if (!values.Any())
                {
                    report.AppendLine($"{columnName}: No valid values");
                    continue;
                }

                var mean = values.Average();
                var stdDev = Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1));
                var min = values.Min();
                var max = values.Max();
                var median = values.OrderBy(v => v).ElementAt(values.Count / 2);

                var nullCount = kvp.Value.Count - values.Count;
                var completeness = (double)values.Count / kvp.Value.Count;

                report.AppendLine($"{columnName}:");
                report.AppendLine($"  Count: {values.Count} (Completeness: {completeness:P2})");
                report.AppendLine($"  Mean: {mean:F4}, StdDev: {stdDev:F4}");
                report.AppendLine($"  Min: {min:F4}, Max: {max:F4}, Median: {median:F4}");
                if (nullCount > 0)
                {
                    report.AppendLine($"  Missing Values: {nullCount}");
                }
                report.AppendLine();
            }

            // Quality recommendations
            report.AppendLine("Quality Recommendations:");
            report.AppendLine("-----------------------");

            var hasIssues = false;
            foreach (var kvp in dataDict)
            {
                var columnName = kvp.Key;
                var values = kvp.Value.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
                var nullCount = kvp.Value.Count - values.Count;
                var completeness = (double)values.Count / kvp.Value.Count;

                if (completeness < 0.95)
                {
                    report.AppendLine($"⚠️  {columnName}: High missing data rate ({(1-completeness):P1}). Consider data imputation or source investigation.");
                    hasIssues = true;
                }

                if (values.Any())
                {
                    var mean = values.Average();
                    var stdDev = Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1));
                    var outliers = values.Count(v => Math.Abs((v - mean) / stdDev) > 3.0);

                    if (outliers > values.Count * 0.05) // More than 5% outliers
                    {
                        report.AppendLine($"⚠️  {columnName}: High outlier rate ({outliers}/{values.Count}). Consider outlier treatment.");
                        hasIssues = true;
                    }
                }
            }

            if (!hasIssues)
            {
                report.AppendLine("✓ Data quality appears good. No major issues detected.");
            }

            return report.ToString();
        }
        catch (Exception ex)
        {
            return $"Error generating quality report: {ex.Message}";
        }
    }
}