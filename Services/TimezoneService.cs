using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.TimeZones;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services;

/// <summary>
/// Service for handling multi-timezone data alignment and market calendar management
/// </summary>
public class TimezoneService
{
    private readonly ILogger<TimezoneService> _logger;
    private readonly IDateTimeZoneProvider _timeZoneProvider;

    public TimezoneService(ILogger<TimezoneService> logger)
    {
        _logger = logger;
        _timeZoneProvider = DateTimeZoneProviders.Tzdb;
    }

    /// <summary>
    /// Market information
    /// </summary>
    public class MarketInfo
    {
        public string Name { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public List<MarketHours> TradingHours { get; set; } = new();
        public List<string> Holidays { get; set; } = new();
    }

    /// <summary>
    /// Market trading hours
    /// </summary>
    public class MarketHours
    {
        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan OpenTime { get; set; }
        public TimeSpan CloseTime { get; set; }
        public bool IsOpen { get; set; }
    }

    /// <summary>
    /// Time alignment result
    /// </summary>
    public class TimeAlignmentResult
    {
        public Dictionary<DateTime, object> AlignedData { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string TargetTimeZone { get; set; } = string.Empty;
        public int GapsFilled { get; set; }
        public int DataPointsAligned { get; set; }
    }

    // Predefined market information
    private readonly Dictionary<string, MarketInfo> _markets = new()
    {
        ["NYSE"] = new MarketInfo
        {
            Name = "New York Stock Exchange",
            TimeZoneId = "America/New_York",
            Country = "USA",
            Currency = "USD",
            TradingHours = new List<MarketHours>
            {
                new() { DayOfWeek = DayOfWeek.Monday, OpenTime = new TimeSpan(9, 30, 0), CloseTime = new TimeSpan(16, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Tuesday, OpenTime = new TimeSpan(9, 30, 0), CloseTime = new TimeSpan(16, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Wednesday, OpenTime = new TimeSpan(9, 30, 0), CloseTime = new TimeSpan(16, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Thursday, OpenTime = new TimeSpan(9, 30, 0), CloseTime = new TimeSpan(16, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Friday, OpenTime = new TimeSpan(9, 30, 0), CloseTime = new TimeSpan(16, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Saturday, OpenTime = TimeSpan.Zero, CloseTime = TimeSpan.Zero, IsOpen = false },
                new() { DayOfWeek = DayOfWeek.Sunday, OpenTime = TimeSpan.Zero, CloseTime = TimeSpan.Zero, IsOpen = false }
            },
            Holidays = new List<string> { "2024-01-01", "2024-01-15", "2024-02-19", "2024-03-29", "2024-05-27", "2024-07-04", "2024-09-02", "2024-11-28", "2024-12-25" }
        },

        ["NASDAQ"] = new MarketInfo
        {
            Name = "NASDAQ",
            TimeZoneId = "America/New_York",
            Country = "USA",
            Currency = "USD",
            TradingHours = new List<MarketHours>
            {
                new() { DayOfWeek = DayOfWeek.Monday, OpenTime = new TimeSpan(9, 30, 0), CloseTime = new TimeSpan(16, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Tuesday, OpenTime = new TimeSpan(9, 30, 0), CloseTime = new TimeSpan(16, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Wednesday, OpenTime = new TimeSpan(9, 30, 0), CloseTime = new TimeSpan(16, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Thursday, OpenTime = new TimeSpan(9, 30, 0), CloseTime = new TimeSpan(16, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Friday, OpenTime = new TimeSpan(9, 30, 0), CloseTime = new TimeSpan(16, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Saturday, OpenTime = TimeSpan.Zero, CloseTime = TimeSpan.Zero, IsOpen = false },
                new() { DayOfWeek = DayOfWeek.Sunday, OpenTime = TimeSpan.Zero, CloseTime = TimeSpan.Zero, IsOpen = false }
            },
            Holidays = new List<string> { "2024-01-01", "2024-01-15", "2024-02-19", "2024-03-29", "2024-05-27", "2024-07-04", "2024-09-02", "2024-11-28", "2024-12-25" }
        },

        ["LSE"] = new MarketInfo
        {
            Name = "London Stock Exchange",
            TimeZoneId = "Europe/London",
            Country = "UK",
            Currency = "GBP",
            TradingHours = new List<MarketHours>
            {
                new() { DayOfWeek = DayOfWeek.Monday, OpenTime = new TimeSpan(8, 0, 0), CloseTime = new TimeSpan(16, 30, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Tuesday, OpenTime = new TimeSpan(8, 0, 0), CloseTime = new TimeSpan(16, 30, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Wednesday, OpenTime = new TimeSpan(8, 0, 0), CloseTime = new TimeSpan(16, 30, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Thursday, OpenTime = new TimeSpan(8, 0, 0), CloseTime = new TimeSpan(16, 30, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Friday, OpenTime = new TimeSpan(8, 0, 0), CloseTime = new TimeSpan(16, 30, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Saturday, OpenTime = TimeSpan.Zero, CloseTime = TimeSpan.Zero, IsOpen = false },
                new() { DayOfWeek = DayOfWeek.Sunday, OpenTime = TimeSpan.Zero, CloseTime = TimeSpan.Zero, IsOpen = false }
            },
            Holidays = new List<string> { "2024-01-01", "2024-03-29", "2024-05-06", "2024-05-27", "2024-08-26", "2024-12-25", "2024-12-26" }
        },

        ["TSE"] = new MarketInfo
        {
            Name = "Tokyo Stock Exchange",
            TimeZoneId = "Asia/Tokyo",
            Country = "Japan",
            Currency = "JPY",
            TradingHours = new List<MarketHours>
            {
                new() { DayOfWeek = DayOfWeek.Monday, OpenTime = new TimeSpan(9, 0, 0), CloseTime = new TimeSpan(15, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Tuesday, OpenTime = new TimeSpan(9, 0, 0), CloseTime = new TimeSpan(15, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Wednesday, OpenTime = new TimeSpan(9, 0, 0), CloseTime = new TimeSpan(15, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Thursday, OpenTime = new TimeSpan(9, 0, 0), CloseTime = new TimeSpan(15, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Friday, OpenTime = new TimeSpan(9, 0, 0), CloseTime = new TimeSpan(15, 0, 0), IsOpen = true },
                new() { DayOfWeek = DayOfWeek.Saturday, OpenTime = TimeSpan.Zero, CloseTime = TimeSpan.Zero, IsOpen = false },
                new() { DayOfWeek = DayOfWeek.Sunday, OpenTime = TimeSpan.Zero, CloseTime = TimeSpan.Zero, IsOpen = false }
            },
            Holidays = new List<string> { "2024-01-01", "2024-01-08", "2024-02-11", "2024-03-20", "2024-04-29", "2024-05-03", "2024-05-06", "2024-07-15", "2024-08-11", "2024-09-16", "2024-09-23", "2024-10-14", "2024-11-03", "2024-11-23", "2024-12-31" }
        }
    };

    /// <summary>
    /// Convert timestamp from source timezone to target timezone
    /// </summary>
    public async Task<DateTime> ConvertTimeZoneAsync(
        DateTime sourceTime,
        string sourceTimeZoneId,
        string targetTimeZoneId)
    {
        return await Task.Run(() =>
        {
            try
            {
                var sourceZone = _timeZoneProvider[sourceTimeZoneId];
                var targetZone = _timeZoneProvider[targetTimeZoneId];

                var instant = Instant.FromDateTimeUtc(DateTime.SpecifyKind(sourceTime, DateTimeKind.Utc));
                var sourceZonedDateTime = instant.InZone(sourceZone);
                var targetZonedDateTime = sourceZonedDateTime.WithZone(targetZone);

                return targetZonedDateTime.ToDateTimeUnspecified();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error converting time from {sourceTimeZoneId} to {targetTimeZoneId}");
                return sourceTime; // Return original time if conversion fails
            }
        });
    }

    /// <summary>
    /// Align time series data to a common timezone
    /// </summary>
    public async Task<TimeAlignmentResult> AlignTimeSeriesDataAsync(
        Dictionary<DateTime, object> data,
        string sourceTimeZoneId,
        string targetTimeZoneId,
        TimeSpan interval)
    {
        var result = new TimeAlignmentResult
        {
            TargetTimeZone = targetTimeZoneId
        };

        try
        {
            // Convert all timestamps to target timezone
            var convertedData = new Dictionary<DateTime, object>();

            foreach (var kvp in data)
            {
                var convertedTime = await ConvertTimeZoneAsync(kvp.Key, sourceTimeZoneId, targetTimeZoneId);
                convertedData[convertedTime] = kvp.Value;
            }

            // Sort by time
            var sortedData = convertedData.OrderBy(kvp => kvp.Key).ToList();

            // Fill gaps and align to regular intervals
            var alignedData = await FillDataGapsAsync(sortedData, interval, result);

            result.AlignedData = alignedData;
            result.DataPointsAligned = alignedData.Count;

            _logger.LogInformation($"Aligned {data.Count} data points to {targetTimeZoneId} timezone, filled {result.GapsFilled} gaps");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error aligning time series data to {targetTimeZoneId}");
            result.Warnings.Add($"Alignment failed: {ex.Message}");
            result.AlignedData = data; // Return original data if alignment fails
        }

        return result;
    }

    /// <summary>
    /// Fill gaps in time series data
    /// </summary>
    private async Task<Dictionary<DateTime, object>> FillDataGapsAsync(
        List<KeyValuePair<DateTime, object>> sortedData,
        TimeSpan interval,
        TimeAlignmentResult result)
    {
        return await Task.Run(() =>
        {
            var alignedData = new Dictionary<DateTime, object>();

            if (!sortedData.Any()) return alignedData;

            var startTime = sortedData.First().Key;
            var endTime = sortedData.Last().Key;

            // Round start time to nearest interval
            var currentTime = RoundToInterval(startTime, interval);

            for (int i = 0; i < sortedData.Count - 1; i++)
            {
                var currentPoint = sortedData[i];
                var nextPoint = sortedData[i + 1];

                // Add current data point
                alignedData[currentPoint.Key] = currentPoint.Value;

                // Fill gaps between current and next point
                var nextExpectedTime = currentPoint.Key.Add(interval);
                while (nextExpectedTime < nextPoint.Key)
                {
                    // Interpolate or forward-fill missing values
                    alignedData[nextExpectedTime] = InterpolateValue(currentPoint.Value, nextPoint.Value, currentPoint.Key, nextPoint.Key, nextExpectedTime);
                    result.GapsFilled++;
                    nextExpectedTime = nextExpectedTime.Add(interval);
                }
            }

            // Add the last data point
            if (sortedData.Any())
            {
                alignedData[sortedData.Last().Key] = sortedData.Last().Value;
            }

            return alignedData;
        });
    }

    /// <summary>
    /// Round datetime to nearest interval
    /// </summary>
    private DateTime RoundToInterval(DateTime dt, TimeSpan interval)
    {
        var ticks = dt.Ticks;
        var intervalTicks = interval.Ticks;
        var remainder = ticks % intervalTicks;
        return new DateTime(ticks - remainder, dt.Kind);
    }

    /// <summary>
    /// Simple interpolation between two values
    /// </summary>
    private object InterpolateValue(object startValue, object endValue, DateTime startTime, DateTime endTime, DateTime targetTime)
    {
        // For numeric values, do linear interpolation
        if (startValue is double start && endValue is double end)
        {
            var totalSpan = (endTime - startTime).TotalSeconds;
            var targetSpan = (targetTime - startTime).TotalSeconds;
            var ratio = targetSpan / totalSpan;
            return start + (end - start) * ratio;
        }
        else if (startValue is decimal startDec && endValue is decimal endDec)
        {
            var totalSpan = (endTime - startTime).TotalSeconds;
            var targetSpan = (targetTime - startTime).TotalSeconds;
            var ratio = (decimal)(targetSpan / totalSpan);
            return startDec + (endDec - startDec) * ratio;
        }

        // For non-numeric values, forward fill
        return startValue;
    }

    /// <summary>
    /// Check if market is open at given time
    /// </summary>
    public async Task<bool> IsMarketOpenAsync(string marketCode, DateTime time)
    {
        return await Task.Run(() =>
        {
            if (!_markets.TryGetValue(marketCode, out var market))
            {
                _logger.LogWarning($"Unknown market code: {marketCode}");
                return false;
            }

            // Convert time to market timezone
            var marketTime = ConvertTimeZoneAsync(time, "UTC", market.TimeZoneId).Result;

            // Check if it's a holiday
            var dateString = marketTime.ToString("yyyy-MM-dd");
            if (market.Holidays.Contains(dateString))
            {
                return false;
            }

            // Check trading hours
            var marketHours = market.TradingHours.FirstOrDefault(h => h.DayOfWeek == marketTime.DayOfWeek);
            if (marketHours == null || !marketHours.IsOpen)
            {
                return false;
            }

            var timeOfDay = marketTime.TimeOfDay;
            return timeOfDay >= marketHours.OpenTime && timeOfDay <= marketHours.CloseTime;
        });
    }

    /// <summary>
    /// Get next market open time
    /// </summary>
    public async Task<DateTime?> GetNextMarketOpenAsync(string marketCode, DateTime fromTime)
    {
        return await Task.Run(async () =>
        {
            if (!_markets.TryGetValue(marketCode, out var market))
            {
                return null;
            }

            var currentTime = fromTime;
            var maxDays = 7; // Look ahead maximum 7 days

            for (int i = 0; i < maxDays; i++)
            {
                var checkTime = currentTime.AddDays(i);
                var marketTime = await ConvertTimeZoneAsync(checkTime, "UTC", market.TimeZoneId);

                // Skip holidays
                var dateString = marketTime.ToString("yyyy-MM-dd");
                if (market.Holidays.Contains(dateString))
                {
                    continue;
                }

                // Check if market is open on this day
                var marketHours = market.TradingHours.FirstOrDefault(h => h.DayOfWeek == marketTime.DayOfWeek);
                if (marketHours != null && marketHours.IsOpen)
                {
                    // If we're checking today and market is already open, return current time
                    if (i == 0 && await IsMarketOpenAsync(marketCode, checkTime))
                    {
                        return checkTime;
                    }

                    // Return market open time for this day
                    var openTime = marketTime.Date.Add(marketHours.OpenTime);
                    return await ConvertTimeZoneAsync(openTime, market.TimeZoneId, "UTC");
                }
            }

            return (DateTime?)null; // No market open time found within the search window
        });
    }

    /// <summary>
    /// Get market information
    /// </summary>
    public async Task<MarketInfo?> GetMarketInfoAsync(string marketCode)
    {
        return await Task.Run(() =>
        {
            return _markets.TryGetValue(marketCode, out var market) ? market : null;
        });
    }

    /// <summary>
    /// Get all available markets
    /// </summary>
    public async Task<List<string>> GetAvailableMarketsAsync()
    {
        return await Task.Run(() =>
        {
            return _markets.Keys.ToList();
        });
    }

    /// <summary>
    /// Validate timezone ID
    /// </summary>
    public async Task<bool> IsValidTimeZoneAsync(string timeZoneId)
    {
        return await Task.Run(() =>
        {
            try
            {
                var zone = _timeZoneProvider[timeZoneId];
                return zone != null;
            }
            catch
            {
                return false;
            }
        });
    }

    /// <summary>
    /// Get timezone offset from UTC
    /// </summary>
    public async Task<TimeSpan?> GetUtcOffsetAsync(string timeZoneId, DateTime dateTime)
    {
        return await Task.Run(() =>
        {
            try
            {
                var zone = _timeZoneProvider[timeZoneId];
                var instant = Instant.FromDateTimeUtc(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
                var zonedDateTime = instant.InZone(zone);
                return (TimeSpan?)zonedDateTime.Offset.ToTimeSpan();
            }
            catch
            {
                return null;
            }
        });
    }
}