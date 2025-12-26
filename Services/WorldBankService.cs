using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace QuantResearchAgent.Services
{
    public class WorldBankService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WorldBankService> _logger;
        private readonly IConfiguration _configuration;

        public WorldBankService(HttpClient httpClient, ILogger<WorldBankService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;

            // Set base address for World Bank API
            _httpClient.BaseAddress = new Uri("http://api.worldbank.org/v2/");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "QuantResearchAgent/1.0");
        }

        public async Task<List<WorldBankIndicator>> GetPopularIndicatorsAsync()
        {
            try
            {
                _logger.LogInformation("Fetching popular World Bank indicators");

                // Get a list of popular economic indicators
                var popularIndicators = new List<string>
                {
                    "NY.GDP.MKTP.CD",    // GDP (current US$)
                    "NY.GDP.PCAP.CD",    // GDP per capita (current US$)
                    "FP.CPI.TOTL.ZG",    // Inflation, consumer prices (annual %)
                    "SL.UEM.TOTL.ZS",    // Unemployment, total (% of total labor force)
                    "PA.NUS.FCRF",       // Official exchange rate (LCU per US$, period average)
                    "BX.KLT.DINV.WD.GD.ZS", // Foreign direct investment, net inflows (% of GDP)
                    "GC.DOD.TOTL.GD.ZS", // Central government debt, total (% of GDP)
                    "NE.EXP.GNFS.ZS",    // Exports of goods and services (% of GDP)
                    "NE.IMP.GNFS.ZS",    // Imports of goods and services (% of GDP)
                    "SP.POP.TOTL"        // Population, total
                };

                var indicators = new List<WorldBankIndicator>();

                foreach (var indicatorCode in popularIndicators)
                {
                    try
                    {
                        var indicator = await GetIndicatorInfoAsync(indicatorCode);
                        if (indicator != null)
                        {
                            indicators.Add(indicator);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to fetch indicator {indicatorCode}: {ex.Message}");
                    }
                }

                _logger.LogInformation($"Successfully fetched {indicators.Count} World Bank indicators");
                return indicators;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching popular indicators: {ex.Message}");
                throw;
            }
        }

        public async Task<WorldBankIndicator> GetIndicatorInfoAsync(string indicatorCode)
        {
            try
            {
                var url = $"indicator/{indicatorCode}?format=json";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"World Bank API returned {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonArray = JsonDocument.Parse(content).RootElement;

                if (jsonArray.GetArrayLength() < 2 || jsonArray[1].GetArrayLength() == 0)
                {
                    return null;
                }

                var indicatorData = jsonArray[1][0];
                return new WorldBankIndicator
                {
                    Id = indicatorData.GetProperty("id").GetString(),
                    Name = indicatorData.GetProperty("name").GetString(),
                    SourceNote = indicatorData.TryGetProperty("sourceNote", out var note) ? note.GetString() : null,
                    SourceOrganization = indicatorData.TryGetProperty("sourceOrganization", out var org) ? org.GetString() : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching indicator info for {indicatorCode}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<WorldBankDataPoint>> GetSeriesDataAsync(string indicatorCode, string countryCode = "USA", int? startYear = null, int? endYear = null)
        {
            try
            {
                _logger.LogInformation($"Fetching World Bank data for indicator {indicatorCode}, country {countryCode}");

                var url = $"country/{countryCode}/indicator/{indicatorCode}?format=json&per_page=1000";

                if (startYear.HasValue)
                    url += $"&date={startYear}";
                if (endYear.HasValue && startYear.HasValue)
                    url += $":{endYear}";
                else if (endYear.HasValue)
                    url += $"&date={endYear}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"World Bank API returned {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonArray = JsonDocument.Parse(content).RootElement;

                if (jsonArray.GetArrayLength() < 2)
                {
                    return new List<WorldBankDataPoint>();
                }

                var dataArray = jsonArray[1];
                var dataPoints = new List<WorldBankDataPoint>();

                foreach (var item in dataArray.EnumerateArray())
                {
                    if (item.TryGetProperty("value", out var valueElement) &&
                        item.TryGetProperty("date", out var dateElement))
                    {
                        var value = valueElement.ValueKind == JsonValueKind.Null ? (decimal?)null :
                                   valueElement.TryGetDecimal(out var decimalValue) ? decimalValue : (decimal?)null;

                        if (value.HasValue && int.TryParse(dateElement.GetString(), out var year))
                        {
                            dataPoints.Add(new WorldBankDataPoint
                            {
                                Year = year,
                                Value = value.Value,
                                CountryCode = countryCode,
                                IndicatorCode = indicatorCode
                            });
                        }
                    }
                }

                // Sort by year descending (most recent first)
                dataPoints = dataPoints.OrderByDescending(dp => dp.Year).ToList();

                _logger.LogInformation($"Successfully fetched {dataPoints.Count} data points for {indicatorCode}");
                return dataPoints;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching series data for {indicatorCode}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<WorldBankIndicator>> SearchIndicatorsAsync(string query, int maxResults = 20)
        {
            try
            {
                _logger.LogInformation($"Searching World Bank indicators for: {query}");

                // World Bank API doesn't have a direct search endpoint, so we'll get all indicators
                // and filter them client-side. This is a limitation of the free API.
                var url = $"indicator?format=json&per_page=1000";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"World Bank API returned {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonArray = JsonDocument.Parse(content).RootElement;

                if (jsonArray.GetArrayLength() < 2)
                {
                    return new List<WorldBankIndicator>();
                }

                var dataArray = jsonArray[1];
                var indicators = new List<WorldBankIndicator>();
                var queryLower = query.ToLowerInvariant();

                foreach (var item in dataArray.EnumerateArray())
                {
                    var name = item.GetProperty("name").GetString();
                    var id = item.GetProperty("id").GetString();

                    if ((name != null && name.ToLowerInvariant().Contains(queryLower)) ||
                        (id != null && id.ToLowerInvariant().Contains(queryLower)))
                    {
                        indicators.Add(new WorldBankIndicator
                        {
                            Id = id,
                            Name = name,
                            SourceNote = item.TryGetProperty("sourceNote", out var note) ? note.GetString() : null,
                            SourceOrganization = item.TryGetProperty("sourceOrganization", out var org) ? org.GetString() : null
                        });

                        if (indicators.Count >= maxResults)
                            break;
                    }
                }

                _logger.LogInformation($"Found {indicators.Count} indicators matching '{query}'");
                return indicators;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error searching indicators: {ex.Message}");
                throw;
            }
        }

        public async Task<List<string>> GetAvailableCountriesAsync()
        {
            try
            {
                var url = "country?format=json&per_page=300";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"World Bank API returned {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonArray = JsonDocument.Parse(content).RootElement;

                if (jsonArray.GetArrayLength() < 2)
                {
                    return new List<string>();
                }

                var dataArray = jsonArray[1];
                var countries = new List<string>();

                foreach (var item in dataArray.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetString();
                    var name = item.GetProperty("name").GetString();

                    if (id != null && name != null && !id.Contains("region") && !id.Contains("income"))
                    {
                        countries.Add($"{id} - {name}");
                    }
                }

                return countries.OrderBy(c => c).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching countries: {ex.Message}");
                throw;
            }
        }
    }

    public class WorldBankIndicator
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? SourceNote { get; set; }
        public string? SourceOrganization { get; set; }
    }

    public class WorldBankDataPoint
    {
        public int Year { get; set; }
        public decimal Value { get; set; }
        public string? CountryCode { get; set; }
        public string? IndicatorCode { get; set; }
    }
}