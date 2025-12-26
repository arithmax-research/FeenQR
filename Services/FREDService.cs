using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Federal Reserve Economic Data (FRED) service for accessing economic indicators
    /// FRED provides 800,000+ economic time series from various sources
    /// </summary>
    public class FREDService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FREDService> _logger;
        private readonly string _apiKey;
        private const string BaseUrl = "https://api.stlouisfed.org/fred";

        public FREDService(HttpClient httpClient, ILogger<FREDService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["FRED:ApiKey"] ?? "";
        }

        /// <summary>
        /// Get economic data series by ID
        /// </summary>
        public async Task<FREDDataSeries?> GetSeriesAsync(string seriesId, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogError("FRED API key is not configured. Please add your free FRED API key to appsettings.json under 'FRED:ApiKey'. Get a free key at https://fred.stlouisfed.org/docs/api/api_key.html");
                throw new InvalidOperationException("FRED API key is not configured. Please add your free FRED API key to appsettings.json under 'FRED:ApiKey'. Get a free key at https://fred.stlouisfed.org/docs/api/api_key.html");
            }

            try
            {
                var url = $"{BaseUrl}/series/observations?series_id={seriesId}&api_key={_apiKey}&file_type=json";

                if (startDate.HasValue)
                    url += $"&observation_start={startDate.Value:yyyy-MM-dd}";
                if (endDate.HasValue)
                    url += $"&observation_end={endDate.Value:yyyy-MM-dd}";

                _logger.LogInformation("Fetching FRED data for series: {SeriesId}", seriesId);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<FREDObservationsResponse>(content);

                if (result?.Observations == null || !result.Observations.Any())
                {
                    _logger.LogWarning("No data found for FRED series: {SeriesId}", seriesId);
                    return null;
                }

                return new FREDDataSeries
                {
                    SeriesId = seriesId,
                    Title = result.Observations.FirstOrDefault()?.SeriesTitle ?? seriesId,
                    Units = result.Observations.FirstOrDefault()?.Units ?? "Unknown",
                    Frequency = result.Observations.FirstOrDefault()?.Frequency ?? "Unknown",
                    DataPoints = result.Observations
                        .Where(obs => !string.IsNullOrEmpty(obs.Value) && obs.Value != ".")
                        .Select(obs => new FREDDataPoint
                        {
                            Date = DateTime.Parse(obs.Date),
                            Value = decimal.Parse(obs.Value),
                            RealtimeStart = DateTime.Parse(obs.RealtimeStart),
                            RealtimeEnd = DateTime.Parse(obs.RealtimeEnd)
                        })
                        .OrderBy(dp => dp.Date)
                        .ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch FRED data for series: {SeriesId}", seriesId);
                return null;
            }
        }

        /// <summary>
        /// Search for economic series by text
        /// </summary>
        public async Task<List<FREDSeriesInfo>> SearchSeriesAsync(string searchText, int limit = 25)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogError("FRED API key is not configured. Please add your free FRED API key to appsettings.json under 'FRED:ApiKey'. Get a free key at https://fred.stlouisfed.org/docs/api/api_key.html");
                throw new InvalidOperationException("FRED API key is not configured. Please add your free FRED API key to appsettings.json under 'FRED:ApiKey'. Get a free key at https://fred.stlouisfed.org/docs/api/api_key.html");
            }

            try
            {
                var url = $"{BaseUrl}/series/search?search_text={Uri.EscapeDataString(searchText)}&api_key=&file_type=json&limit={limit}";

                _logger.LogInformation("Searching FRED series for: {SearchText}", searchText);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<FREDSearchResponse>(content);

                return result?.Series?.Select(s => new FREDSeriesInfo
                {
                    Id = s.Id,
                    Title = s.Title,
                    Units = s.Units,
                    Frequency = s.Frequency,
                    SeasonalAdjustment = s.SeasonalAdjustment,
                    LastUpdated = DateTime.Parse(s.LastUpdated),
                    ObservationStart = DateTime.Parse(s.ObservationStart),
                    ObservationEnd = DateTime.Parse(s.ObservationEnd),
                    Notes = s.Notes
                }).ToList() ?? new List<FREDSeriesInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search FRED series for: {SearchText}", searchText);
                return new List<FREDSeriesInfo>();
            }
        }

        /// <summary>
        /// Get popular economic indicators
        /// </summary>
        public async Task<Dictionary<string, FREDDataSeries>> GetPopularIndicatorsAsync()
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogError("FRED API key is not configured. Please add your free FRED API key to appsettings.json under 'FRED:ApiKey'. Get a free key at https://fred.stlouisfed.org/docs/api/api_key.html");
                throw new InvalidOperationException("FRED API key is not configured. Please add your free FRED API key to appsettings.json under 'FRED:ApiKey'. Get a free key at https://fred.stlouisfed.org/docs/api/api_key.html");
            }

            var popularSeries = new Dictionary<string, string>
            {
                ["GDP"] = "GDP", // Gross Domestic Product
                ["UNRATE"] = "UNRATE", // Unemployment Rate
                ["FEDFUNDS"] = "FEDFUNDS", // Federal Funds Rate
                ["CPIAUCSL"] = "CPIAUCSL", // Consumer Price Index
                ["DEXUSEU"] = "DEXUSEU", // USD/EUR Exchange Rate
                ["DGS10"] = "DGS10", // 10-Year Treasury Rate
                ["HOUST"] = "HOUST", // Housing Starts
                ["INDPRO"] = "INDPRO", // Industrial Production Index
                ["PAYEMS"] = "PAYEMS", // Nonfarm Payrolls
                ["PCE"] = "PCE" // Personal Consumption Expenditures
            };

            var results = new Dictionary<string, FREDDataSeries>();

            foreach (var (name, seriesId) in popularSeries)
            {
                var data = await GetSeriesAsync(seriesId);
                if (data != null)
                {
                    results[name] = data;
                }
            }

            return results;
        }

        /// <summary>
        /// Get economic data for a specific date range
        /// </summary>
        public async Task<FREDDataSeries?> GetSeriesInRangeAsync(string seriesId, DateTime startDate, DateTime endDate)
        {
            return await GetSeriesAsync(seriesId, startDate, endDate);
        }
    }

    // Data models for FRED API responses
    public class FREDObservationsResponse
    {
        [JsonPropertyName("observations")]
        public List<FREDObservation> Observations { get; set; } = new();
    }

    public class FREDObservation
    {
        [JsonPropertyName("realtime_start")]
        public string RealtimeStart { get; set; } = string.Empty;

        [JsonPropertyName("realtime_end")]
        public string RealtimeEnd { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("series_title")]
        public string SeriesTitle { get; set; } = string.Empty;

        [JsonPropertyName("units")]
        public string Units { get; set; } = string.Empty;

        [JsonPropertyName("frequency")]
        public string Frequency { get; set; } = string.Empty;
    }

    public class FREDSearchResponse
    {
        [JsonPropertyName("seriess")]
        public List<FREDSeries> Series { get; set; } = new();
    }

    public class FREDSeries
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("units")]
        public string Units { get; set; } = string.Empty;

        [JsonPropertyName("frequency")]
        public string Frequency { get; set; } = string.Empty;

        [JsonPropertyName("seasonal_adjustment")]
        public string SeasonalAdjustment { get; set; } = string.Empty;

        [JsonPropertyName("last_updated")]
        public string LastUpdated { get; set; } = string.Empty;

        [JsonPropertyName("observation_start")]
        public string ObservationStart { get; set; } = string.Empty;

        [JsonPropertyName("observation_end")]
        public string ObservationEnd { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }

    // Public data models
    public class FREDDataSeries
    {
        public string SeriesId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Units { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public List<FREDDataPoint> DataPoints { get; set; } = new();
    }

    public class FREDDataPoint
    {
        public DateTime Date { get; set; }
        public decimal Value { get; set; }
        public DateTime RealtimeStart { get; set; }
        public DateTime RealtimeEnd { get; set; }
    }

    public class FREDSeriesInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Units { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public string SeasonalAdjustment { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
        public DateTime ObservationStart { get; set; }
        public DateTime ObservationEnd { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}