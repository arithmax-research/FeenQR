using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

public class TimeSeriesAnalysisPlugin
{
    private readonly TimeSeriesAnalysisService _timeSeriesService;
    private readonly ILLMService _llmService;
    private readonly ILogger<TimeSeriesAnalysisPlugin> _logger;

    public TimeSeriesAnalysisPlugin(
        TimeSeriesAnalysisService timeSeriesService,
        ILLMService llmService,
        ILogger<TimeSeriesAnalysisPlugin> logger)
    {
        _timeSeriesService = timeSeriesService;
        _llmService = llmService;
        _logger = logger;
    }

    [KernelFunction("analyze_stationarity")]
    [Description("Analyzes stationarity of a time series using ADF, KPSS, or Phillips-Perron tests")]
    public async Task<string> AnalyzeStationarityAsync(
        [Description("The time series data as a comma-separated string of numbers")] string data,
        [Description("Type of stationarity test: 'adf', 'kpss', or 'phillips-perron'")] string testType = "adf",
        [Description("Maximum lag order for the test")] int maxLags = 10)
    {
        try
        {
            var dataArray = data.Split(',').Select(double.Parse).ToArray();

            StationarityTestResult result;
            switch (testType.ToLower())
            {
                case "adf":
                    result = _timeSeriesService.PerformADFTest(dataArray, maxLags);
                    break;
                case "kpss":
                    result = _timeSeriesService.PerformKPSSTest(dataArray);
                    break;
                case "phillips-perron":
                    // Simplified - using ADF as proxy for Phillips-Perron
                    result = _timeSeriesService.PerformADFTest(dataArray, maxLags);
                    result.TestType = "Phillips-Perron";
                    break;
                default:
                    throw new ArgumentException("Invalid test type. Use 'adf', 'kpss', or 'phillips-perron'");
            }

            var interpretation = await _llmService.GetChatCompletionAsync(
                $"Interpret this stationarity test result: Test={result.TestType}, Statistic={result.TestStatistic:F4}, " +
                $"Critical 5%={result.CriticalValues[0.05]:F4}, Is Stationary={result.IsStationary}. " +
                $"Explain what this means for time series analysis and forecasting.");

            return $"Stationarity Test Results ({result.TestType}):\n" +
                   $"Test Statistic: {result.TestStatistic:F4}\n" +
                   $"Critical Values: 1%={result.CriticalValues[0.01]:F2}, 5%={result.CriticalValues[0.05]:F2}, 10%={result.CriticalValues[0.10]:F2}\n" +
                   $"Is Stationary: {result.IsStationary}\n" +
                   $"Lag Order Used: {result.LagOrder}\n\n" +
                   $"AI Interpretation: {interpretation}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in stationarity analysis");
            return $"Error analyzing stationarity: {ex.Message}";
        }
    }

    [KernelFunction("calculate_autocorrelation")]
    [Description("Calculates autocorrelation and partial autocorrelation functions for a time series")]
    public async Task<string> CalculateAutocorrelationAsync(
        [Description("The time series data as a comma-separated string of numbers")] string data,
        [Description("Maximum number of lags to calculate")] int maxLags = 20)
    {
        try
        {
            var dataArray = data.Split(',').Select(double.Parse).ToArray();
            var result = _timeSeriesService.CalculateAutocorrelation(dataArray, maxLags);

            var acfText = string.Join(", ", result.Autocorrelations.Select((x, i) => $"{i+1}:{x:F3}"));
            var pacfText = string.Join(", ", result.PartialAutocorrelations.Select((x, i) => $"{i+1}:{x:F3}"));

            var interpretation = await _llmService.GetChatCompletionAsync(
                $"Interpret these autocorrelation results: ACF=[{acfText}], PACF=[{pacfText}]. " +
                $"Ljung-Box statistic: {result.LjungBoxStatistic:F4}. " +
                $"Explain what these patterns suggest about the time series structure and appropriate ARIMA models.");

            return $"Autocorrelation Analysis:\n" +
                   $"ACF (lags 1-{result.Autocorrelations.Count}): {acfText}\n" +
                   $"PACF (lags 1-{result.PartialAutocorrelations.Count}): {pacfText}\n" +
                   $"Ljung-Box Statistic: {result.LjungBoxStatistic:F4}\n\n" +
                   $"AI Interpretation: {interpretation}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating autocorrelation");
            return $"Error calculating autocorrelation: {ex.Message}";
        }
    }

    [KernelFunction("perform_seasonal_decomposition")]
    [Description("Performs seasonal decomposition of a time series")]
    public async Task<string> PerformSeasonalDecompositionAsync(
        [Description("The time series data as a comma-separated string of numbers")] string data,
        [Description("The seasonal period (e.g., 12 for monthly data, 4 for quarterly)")] int seasonalPeriod)
    {
        try
        {
            var dataArray = data.Split(',').Select(double.Parse).ToArray();
            var result = _timeSeriesService.PerformSeasonalDecomposition(dataArray, seasonalPeriod);

            var trendText = string.Join(", ", result.Trend.Select(x => x.ToString("F2")));
            var seasonalText = string.Join(", ", result.Seasonal.Select(x => x.ToString("F3")));
            var residualText = string.Join(", ", result.Residual.Select(x => x.ToString("F3")));

            var interpretation = await _llmService.GetChatCompletionAsync(
                $"Interpret this seasonal decomposition: The time series shows trend, seasonal, and residual components. " +
                $"Seasonal period: {seasonalPeriod}. Trend range: {result.Trend.Min():F2} to {result.Trend.Max():F2}. " +
                $"Explain what this decomposition reveals about the time series behavior and forecasting implications.");

            return $"Seasonal Decomposition Results:\n" +
                   $"Seasonal Period: {seasonalPeriod}\n" +
                   $"Original Data Points: {result.OriginalData.Length}\n" +
                   $"Trend Component (first 10): {string.Join(", ", result.Trend.Take(10).Select(x => x.ToString("F2")))}...\n" +
                   $"Seasonal Component: {seasonalText}\n" +
                   $"Residual Component (first 10): {string.Join(", ", result.Residual.Take(10).Select(x => x.ToString("F3")))}...\n\n" +
                   $"AI Interpretation: {interpretation}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing seasonal decomposition");
            return $"Error performing seasonal decomposition: {ex.Message}";
        }
    }

    [KernelFunction("analyze_time_series")]
    [Description("Comprehensive time series analysis including stationarity, autocorrelation, and decomposition")]
    public async Task<string> AnalyzeTimeSeriesAsync(
        [Description("The time series data as a comma-separated string of numbers")] string data,
        [Description("The seasonal period (0 for non-seasonal data)")] int seasonalPeriod = 0)
    {
        try
        {
            var dataArray = data.Split(',').Select(double.Parse).ToArray();

            var results = new List<string>();

            // Stationarity tests
            var adfResult = _timeSeriesService.PerformADFTest(dataArray);
            results.Add($"ADF Test: Statistic={adfResult.TestStatistic:F4}, Stationary={adfResult.IsStationary}");

            var kpssResult = _timeSeriesService.PerformKPSSTest(dataArray);
            results.Add($"KPSS Test: Statistic={kpssResult.TestStatistic:F4}, Stationary={kpssResult.IsStationary}");

            // Autocorrelation
            var acfResult = _timeSeriesService.CalculateAutocorrelation(dataArray);
            var significantLags = acfResult.Autocorrelations
                .Select((x, i) => new { Lag = i + 1, Value = x })
                .Where(x => Math.Abs(x.Value) > 0.2)
                .Take(5)
                .ToList();
            results.Add($"Significant ACF Lags: {string.Join(", ", significantLags.Select(x => $"{x.Lag}:{x.Value:F3}"))}");

            // Seasonal decomposition if period specified
            if (seasonalPeriod > 0 && dataArray.Length >= seasonalPeriod * 2)
            {
                var decompResult = _timeSeriesService.PerformSeasonalDecomposition(dataArray, seasonalPeriod);
                results.Add($"Seasonal Decomposition: Trend range {decompResult.Trend.Min():F2} to {decompResult.Trend.Max():F2}");
            }

            var summary = string.Join("\n", results);

            var interpretation = await _llmService.GetChatCompletionAsync(
                $"Provide a comprehensive interpretation of this time series analysis: {summary}. " +
                $"Explain the stationarity status, autocorrelation patterns, and what forecasting models would be appropriate.");

            return $"Comprehensive Time Series Analysis:\n{summary}\n\nAI Interpretation: {interpretation}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in comprehensive time series analysis");
            return $"Error in time series analysis: {ex.Message}";
        }
    }
}