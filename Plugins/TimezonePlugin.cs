using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Plugin for timezone handling and market calendar functions
/// </summary>
public class TimezonePlugin
{
    private readonly TimezoneService _timezoneService;

    public TimezonePlugin(TimezoneService timezoneService)
    {
        _timezoneService = timezoneService;
    }

    /// <summary>
    /// Convert timestamp between timezones
    /// </summary>
    [KernelFunction, Description("Convert a timestamp from one timezone to another")]
    public async Task<string> ConvertTimeZoneAsync(
        [Description("Source timestamp (ISO format)")] string timestamp,
        [Description("Source timezone ID (e.g., 'America/New_York', 'Europe/London')")] string sourceTimeZone,
        [Description("Target timezone ID")] string targetTimeZone)
    {
        try
        {
            var sourceTime = DateTime.Parse(timestamp);
            var convertedTime = await _timezoneService.ConvertTimeZoneAsync(sourceTime, sourceTimeZone, targetTimeZone);

            var response = new System.Text.StringBuilder();
            response.AppendLine("Timezone Conversion");
            response.AppendLine("===================");
            response.AppendLine($"Original: {sourceTime:yyyy-MM-dd HH:mm:ss} ({sourceTimeZone})");
            response.AppendLine($"Converted: {convertedTime:yyyy-MM-dd HH:mm:ss} ({targetTimeZone})");

            // Calculate offset
            var sourceOffset = await _timezoneService.GetUtcOffsetAsync(sourceTimeZone, sourceTime);
            var targetOffset = await _timezoneService.GetUtcOffsetAsync(targetTimeZone, convertedTime);

            if (sourceOffset.HasValue && targetOffset.HasValue)
            {
                var difference = targetOffset.Value - sourceOffset.Value;
                response.AppendLine($"Difference: {(difference.TotalHours >= 0 ? "+" : "")}{difference.TotalHours:F1} hours");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            return $"Error converting timezone: {ex.Message}";
        }
    }

    /// <summary>
    /// Align time series data to a common timezone
    /// </summary>
    [KernelFunction, Description("Align time series data to a common timezone and fill gaps")]
    public async Task<string> AlignTimeSeriesDataAsync(
        [Description("Time series data as JSON object with timestamps as keys")] string dataJson,
        [Description("Source timezone ID")] string sourceTimeZone,
        [Description("Target timezone ID")] string targetTimeZone,
        [Description("Data interval in minutes")] int intervalMinutes = 60)
    {
        try
        {
            var dataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(dataJson);
            if (dataDict == null)
            {
                return "Error: Invalid data format. Expected JSON object with timestamp strings as keys.";
            }

            // Convert string keys to DateTime
            var timeSeriesData = new Dictionary<DateTime, object>();
            foreach (var kvp in dataDict)
            {
                if (DateTime.TryParse(kvp.Key, out var dateTime))
                {
                    timeSeriesData[dateTime] = kvp.Value;
                }
            }

            var interval = TimeSpan.FromMinutes(intervalMinutes);
            var result = await _timezoneService.AlignTimeSeriesDataAsync(timeSeriesData, sourceTimeZone, targetTimeZone, interval);

            var response = new System.Text.StringBuilder();
            response.AppendLine("Time Series Alignment Results");
            response.AppendLine("============================");
            response.AppendLine($"Source Timezone: {sourceTimeZone}");
            response.AppendLine($"Target Timezone: {targetTimeZone}");
            response.AppendLine($"Interval: {intervalMinutes} minutes");
            response.AppendLine($"Original Data Points: {timeSeriesData.Count}");
            response.AppendLine($"Aligned Data Points: {result.DataPointsAligned}");
            response.AppendLine($"Gaps Filled: {result.GapsFilled}");

            if (result.Warnings.Any())
            {
                response.AppendLine();
                response.AppendLine("Warnings:");
                foreach (var warning in result.Warnings)
                {
                    response.AppendLine($"  - {warning}");
                }
            }

            // Show sample of aligned data
            response.AppendLine();
            response.AppendLine("Sample Aligned Data:");
            response.AppendLine("-------------------");
            var sampleData = result.AlignedData.OrderByDescending(kvp => kvp.Key).Take(5);
            foreach (var kvp in sampleData)
            {
                response.AppendLine($"{kvp.Key:yyyy-MM-dd HH:mm:ss} ({targetTimeZone}): {kvp.Value}");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            return $"Error aligning time series data: {ex.Message}";
        }
    }

    /// <summary>
    /// Check if market is open
    /// </summary>
    [KernelFunction, Description("Check if a specific market is open at a given time")]
    public async Task<string> IsMarketOpenAsync(
        [Description("Market code (NYSE, NASDAQ, LSE, TSE)")] string marketCode,
        [Description("Timestamp to check (ISO format)")] string timestamp)
    {
        try
        {
            var checkTime = DateTime.Parse(timestamp);
            var isOpen = await _timezoneService.IsMarketOpenAsync(marketCode, checkTime);

            var marketInfo = await _timezoneService.GetMarketInfoAsync(marketCode);

            var response = new System.Text.StringBuilder();
            response.AppendLine("Market Status Check");
            response.AppendLine("===================");
            response.AppendLine($"Market: {marketCode}");
            response.AppendLine($"Time: {checkTime:yyyy-MM-dd HH:mm:ss} UTC");
            response.AppendLine($"Status: {(isOpen ? "OPEN" : "CLOSED")}");

            if (marketInfo != null)
            {
                var marketTime = await _timezoneService.ConvertTimeZoneAsync(checkTime, "UTC", marketInfo.TimeZoneId);
                response.AppendLine($"Local Time: {marketTime:yyyy-MM-dd HH:mm:ss} ({marketInfo.TimeZoneId})");

                // Check if it's a holiday
                var dateString = marketTime.ToString("yyyy-MM-dd");
                var isHoliday = marketInfo.Holidays.Contains(dateString);
                if (isHoliday)
                {
                    response.AppendLine("Note: Market is closed due to holiday");
                }

                // Show today's trading hours
                var todayHours = marketInfo.TradingHours.FirstOrDefault(h => h.DayOfWeek == marketTime.DayOfWeek);
                if (todayHours != null)
                {
                    if (todayHours.IsOpen)
                    {
                        response.AppendLine($"Trading Hours: {todayHours.OpenTime:hh\\:mm} - {todayHours.CloseTime:hh\\:mm} ({marketInfo.TimeZoneId})");
                    }
                    else
                    {
                        response.AppendLine("Note: Market is closed on this day of the week");
                    }
                }
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            return $"Error checking market status: {ex.Message}";
        }
    }

    /// <summary>
    /// Get next market open time
    /// </summary>
    [KernelFunction, Description("Get the next market open time from a given timestamp")]
    public async Task<string> GetNextMarketOpenAsync(
        [Description("Market code")] string marketCode,
        [Description("Starting timestamp (ISO format)")] string fromTimestamp)
    {
        try
        {
            var fromTime = DateTime.Parse(fromTimestamp);
            var nextOpen = await _timezoneService.GetNextMarketOpenAsync(marketCode, fromTime);

            var response = new System.Text.StringBuilder();
            response.AppendLine("Next Market Open Time");
            response.AppendLine("=====================");
            response.AppendLine($"Market: {marketCode}");
            response.AppendLine($"From: {fromTime:yyyy-MM-dd HH:mm:ss} UTC");

            if (nextOpen.HasValue)
            {
                response.AppendLine($"Next Open: {nextOpen.Value:yyyy-MM-dd HH:mm:ss} UTC");

                var marketInfo = await _timezoneService.GetMarketInfoAsync(marketCode);
                if (marketInfo != null)
                {
                    var localTime = await _timezoneService.ConvertTimeZoneAsync(nextOpen.Value, "UTC", marketInfo.TimeZoneId);
                    response.AppendLine($"Local Time: {localTime:yyyy-MM-dd HH:mm:ss} ({marketInfo.TimeZoneId})");

                    var hours = marketInfo.TradingHours.FirstOrDefault(h => h.DayOfWeek == localTime.DayOfWeek);
                    if (hours != null)
                    {
                        response.AppendLine($"Trading Hours: {hours.OpenTime:hh\\:mm} - {hours.CloseTime:hh\\:mm}");
                    }
                }

                var timeUntilOpen = nextOpen.Value - fromTime;
                response.AppendLine($"Time Until Open: {timeUntilOpen.TotalHours:F1} hours");
            }
            else
            {
                response.AppendLine("No market open time found within the next 7 days");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting next market open time: {ex.Message}";
        }
    }

    /// <summary>
    /// Get market information
    /// </summary>
    [KernelFunction, Description("Get detailed information about a specific market")]
    public async Task<string> GetMarketInfoAsync(
        [Description("Market code")] string marketCode)
    {
        try
        {
            var marketInfo = await _timezoneService.GetMarketInfoAsync(marketCode);

            if (marketInfo == null)
            {
                var availableMarkets = await _timezoneService.GetAvailableMarketsAsync();
                return $"Market '{marketCode}' not found. Available markets: {string.Join(", ", availableMarkets)}";
            }

            var response = new System.Text.StringBuilder();
            response.AppendLine($"Market Information: {marketCode}");
            response.AppendLine("==========================");
            response.AppendLine($"Name: {marketInfo.Name}");
            response.AppendLine($"Country: {marketInfo.Country}");
            response.AppendLine($"Currency: {marketInfo.Currency}");
            response.AppendLine($"Timezone: {marketInfo.TimeZoneId}");
            response.AppendLine();

            response.AppendLine("Trading Hours:");
            response.AppendLine("-------------");
            foreach (var hours in marketInfo.TradingHours.Where(h => h.IsOpen))
            {
                response.AppendLine($"{hours.DayOfWeek}: {hours.OpenTime:hh\\:mm} - {hours.CloseTime:hh\\:mm}");
            }
            response.AppendLine();

            response.AppendLine("Holidays (2024):");
            response.AppendLine("---------------");
            if (marketInfo.Holidays.Any())
            {
                foreach (var holiday in marketInfo.Holidays)
                {
                    response.AppendLine(holiday);
                }
            }
            else
            {
                response.AppendLine("No holidays configured");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting market information: {ex.Message}";
        }
    }

    /// <summary>
    /// List all available markets
    /// </summary>
    [KernelFunction, Description("List all available markets and their codes")]
    public async Task<string> ListAvailableMarketsAsync()
    {
        try
        {
            var markets = await _timezoneService.GetAvailableMarketsAsync();

            var response = new System.Text.StringBuilder();
            response.AppendLine("Available Markets");
            response.AppendLine("=================");

            foreach (var marketCode in markets)
            {
                var marketInfo = await _timezoneService.GetMarketInfoAsync(marketCode);
                if (marketInfo != null)
                {
                    response.AppendLine($"{marketCode}: {marketInfo.Name} ({marketInfo.Country}) - {marketInfo.TimeZoneId}");
                }
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            return $"Error listing markets: {ex.Message}";
        }
    }

    /// <summary>
    /// Validate timezone ID
    /// </summary>
    [KernelFunction, Description("Validate if a timezone ID is valid")]
    public async Task<string> ValidateTimeZoneAsync(
        [Description("Timezone ID to validate")] string timeZoneId)
    {
        try
        {
            var isValid = await _timezoneService.IsValidTimeZoneAsync(timeZoneId);

            var response = new System.Text.StringBuilder();
            response.AppendLine("Timezone Validation");
            response.AppendLine("===================");
            response.AppendLine($"Timezone ID: {timeZoneId}");
            response.AppendLine($"Valid: {(isValid ? "YES" : "NO")}");

            if (isValid)
            {
                // Get current offset
                var now = DateTime.UtcNow;
                var offset = await _timezoneService.GetUtcOffsetAsync(timeZoneId, now);
                if (offset.HasValue)
                {
                    response.AppendLine($"Current UTC Offset: {(offset.Value.TotalHours >= 0 ? "+" : "")}{offset.Value.TotalHours:F1} hours");
                    response.AppendLine($"Current Local Time: {now + offset.Value:yyyy-MM-dd HH:mm:ss}");
                }
            }
            else
            {
                response.AppendLine("Note: This timezone ID is not recognized.");
                response.AppendLine("Common timezone IDs include:");
                response.AppendLine("  - America/New_York");
                response.AppendLine("  - Europe/London");
                response.AppendLine("  - Asia/Tokyo");
                response.AppendLine("  - Australia/Sydney");
                response.AppendLine("  - UTC");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            return $"Error validating timezone: {ex.Message}";
        }
    }

    /// <summary>
    /// Get timezone offset information
    /// </summary>
    [KernelFunction, Description("Get UTC offset information for a timezone")]
    public async Task<string> GetTimeZoneOffsetAsync(
        [Description("Timezone ID")] string timeZoneId,
        [Description("Date for offset calculation (ISO format)")] string date = "")
    {
        try
        {
            var targetDate = string.IsNullOrEmpty(date) ? DateTime.UtcNow : DateTime.Parse(date);
            var offset = await _timezoneService.GetUtcOffsetAsync(timeZoneId, targetDate);

            var response = new System.Text.StringBuilder();
            response.AppendLine("Timezone Offset Information");
            response.AppendLine("===========================");
            response.AppendLine($"Timezone: {timeZoneId}");
            response.AppendLine($"Date: {targetDate:yyyy-MM-dd HH:mm:ss} UTC");

            if (offset.HasValue)
            {
                response.AppendLine($"UTC Offset: {(offset.Value.TotalHours >= 0 ? "+" : "")}{offset.Value.TotalHours:F1} hours ({offset.Value.TotalMinutes} minutes)");
                response.AppendLine($"Local Time: {(targetDate + offset.Value):yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                response.AppendLine("Unable to determine offset for this timezone/date combination");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting timezone offset: {ex.Message}";
        }
    }
}