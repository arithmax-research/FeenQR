using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;
using System.Text.Json;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for Federal Reserve Economic Data (FRED) operations
/// Provides access to 800,000+ economic time series from various sources
/// </summary>
public class FREDEconomicPlugin
{
    private readonly FREDService _fredService;

    public FREDEconomicPlugin(FREDService fredService)
    {
        _fredService = fredService;
    }

    [KernelFunction, Description("Get economic data series from FRED by series ID")]
    public async Task<string> GetEconomicSeriesAsync(
        [Description("The FRED series ID (e.g., GDP, UNRATE, FEDFUNDS, CPIAUCSL)")] string seriesId,
        [Description("Start date in YYYY-MM-DD format (optional)")] string? startDate = null,
        [Description("End date in YYYY-MM-DD format (optional)")] string? endDate = null)
    {
        try
        {
            DateTime? start = null;
            DateTime? end = null;

            if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var sDate))
                start = sDate;
            if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var eDate))
                end = eDate;

            var series = await _fredService.GetSeriesAsync(seriesId, start, end);

            if (series == null || !series.DataPoints.Any())
            {
                return $"ERROR: No data found for FRED series '{seriesId}'. Please check the series ID or try a different date range.";
            }

            var latestPoint = series.DataPoints.Last();
            var dataPoints = series.DataPoints.Count;
            var dateRange = $"{series.DataPoints.First().Date:yyyy-MM-dd} to {latestPoint.Date:yyyy-MM-dd}";

            return $"ECONOMIC DATA: {series.Title} ({series.SeriesId})\n" +
                   $"Units: {series.Units}\n" +
                   $"Frequency: {series.Frequency}\n" +
                   $"Data Points: {dataPoints}\n" +
                   $"Date Range: {dateRange}\n" +
                   $"Latest Value: {latestPoint.Value:F4} (as of {latestPoint.Date:yyyy-MM-dd})\n" +
                   $"Raw Data: {JsonSerializer.Serialize(series.DataPoints.TakeLast(10), new JsonSerializerOptions { WriteIndented = false })}";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to retrieve FRED data for series '{seriesId}': {ex.Message}";
        }
    }

    [KernelFunction, Description("Search for economic series in FRED database")]
    public async Task<string> SearchEconomicSeriesAsync(
        [Description("Search text to find relevant economic series")] string searchText,
        [Description("Maximum number of results to return (default: 10)")] int limit = 10)
    {
        try
        {
            var results = await _fredService.SearchSeriesAsync(searchText, Math.Min(limit, 25));

            if (!results.Any())
            {
                return $"No FRED series found matching '{searchText}'. Try different search terms like 'GDP', 'unemployment', 'inflation', etc.";
            }

            var response = $"FRED SEARCH RESULTS for '{searchText}':\n\n";
            foreach (var series in results.Take(limit))
            {
                response += $"• {series.Id}: {series.Title}\n" +
                           $"  Frequency: {series.Frequency} | Units: {series.Units}\n" +
                           $"  Date Range: {series.ObservationStart:yyyy-MM-dd} to {series.ObservationEnd:yyyy-MM-dd}\n\n";
            }

            response += $"Total results found: {results.Count}. Use GetEconomicSeriesAsync with a specific series ID to get data.";
            return response;
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to search FRED series: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get popular economic indicators from FRED")]
    public async Task<string> GetPopularEconomicIndicatorsAsync()
    {
        try
        {
            var indicators = await _fredService.GetPopularIndicatorsAsync();

            if (!indicators.Any())
            {
                return "ERROR: Unable to retrieve popular economic indicators from FRED.";
            }

            var response = "POPULAR ECONOMIC INDICATORS from FRED:\n\n";

            foreach (var (name, series) in indicators)
            {
                var latest = series.DataPoints.LastOrDefault();
                if (latest != null)
                {
                    response += $"{name}: {series.Title}\n" +
                               $"  Latest: {latest.Value:F4} {series.Units} (as of {latest.Date:yyyy-MM-dd})\n" +
                               $"  Series ID: {series.SeriesId}\n\n";
                }
            }

            response += "Use GetEconomicSeriesAsync with any Series ID above to get full historical data.";
            return response;
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to retrieve popular economic indicators: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get economic data for a specific date range")]
    public async Task<string> GetEconomicDataInRangeAsync(
        [Description("The FRED series ID")] string seriesId,
        [Description("Start date in YYYY-MM-DD format")] string startDate,
        [Description("End date in YYYY-MM-DD format")] string endDate)
    {
        try
        {
            if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
            {
                return "ERROR: Invalid date format. Please use YYYY-MM-DD format for dates.";
            }

            var series = await _fredService.GetSeriesInRangeAsync(seriesId, start, end);

            if (series == null || !series.DataPoints.Any())
            {
                return $"ERROR: No data found for FRED series '{seriesId}' in the specified date range.";
            }

            var dataPoints = series.DataPoints;
            var avgValue = dataPoints.Average(dp => dp.Value);
            var minValue = dataPoints.Min(dp => dp.Value);
            var maxValue = dataPoints.Max(dp => dp.Value);

            return $"ECONOMIC DATA: {series.Title} ({series.SeriesId})\n" +
                   $"Date Range: {startDate} to {endDate}\n" +
                   $"Data Points: {dataPoints.Count}\n" +
                   $"Average: {avgValue:F4} {series.Units}\n" +
                   $"Min: {minValue:F4} {series.Units} (on {dataPoints.First(dp => dp.Value == minValue).Date:yyyy-MM-dd})\n" +
                   $"Max: {maxValue:F4} {series.Units} (on {dataPoints.First(dp => dp.Value == maxValue).Date:yyyy-MM-dd})\n" +
                   $"Raw Data: {JsonSerializer.Serialize(dataPoints, new JsonSerializerOptions { WriteIndented = false })}";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to retrieve economic data for series '{seriesId}': {ex.Message}";
        }
    }
}