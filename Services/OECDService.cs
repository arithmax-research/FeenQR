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
    public class OECDService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OECDService> _logger;
        private readonly IConfiguration _configuration;

        public OECDService(HttpClient httpClient, ILogger<OECDService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;

            // Set base address for OECD API
            _httpClient.BaseAddress = new Uri("https://stats.oecd.org/SDMX-JSON/");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "QuantResearchAgent/1.0");
        }

        public async Task<List<OECDIndicator>> GetPopularIndicatorsAsync()
        {
            try
            {
                _logger.LogInformation("Fetching popular OECD indicators");

                // Get a list of popular economic indicators from OECD
                var popularIndicators = new List<string>
                {
                    "QNA|USA.B1_GE.GYSA+GYSA_Q|OECD",    // GDP Growth (USA)
                    "EO|CPALTT01.GYSA.M|OECD",            // Consumer Price Index
                    "EO|UNEMPSA.STSA.M|OECD",             // Unemployment Rate
                    "QNA|USA.B1_GE.CQRSA+CYR|OECD",       // GDP Current Prices (USA)
                    "EO|XTNTVA01.GYSA.Q|OECD",            // Total Trade Volume
                    "EO|GGEXP.GPSA.GP|OECD",              // Government Expenditure
                    "EO|GGREV.GPSA.GP|OECD",              // Government Revenue
                    "EO|INTDSR.MA.STSA.M|OECD",           // Interest Rates
                    "EO|POP.TOT.GYSA.A|OECD",             // Population
                    "EO|EXCH.RAT.GYSA.M|OECD"             // Exchange Rates
                };

                var indicators = new List<OECDIndicator>();

                foreach (var indicatorKey in popularIndicators)
                {
                    try
                    {
                        var indicator = await GetIndicatorInfoAsync(indicatorKey);
                        if (indicator != null)
                        {
                            indicators.Add(indicator);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to fetch indicator {indicatorKey}: {ex.Message}");
                    }
                }

                _logger.LogInformation($"Successfully fetched {indicators.Count} OECD indicators");
                return indicators;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching popular indicators: {ex.Message}");
                throw;
            }
        }

        public async Task<OECDIndicator> GetIndicatorInfoAsync(string indicatorKey)
        {
            try
            {
                // Parse the indicator key to extract dataset and dimensions
                var parts = indicatorKey.Split('|');
                if (parts.Length < 3) return null;

                var dataset = parts[0];
                var dimensions = parts[1];
                var agency = parts[2];

                var url = $"data/{dataset}/{dimensions}/{agency}?json-lang=en&dimensionAtObservation=AllDimensions";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"OECD API returned {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content).RootElement;

                if (!jsonDoc.TryGetProperty("structure", out var structure))
                {
                    return null;
                }

                var name = "Unknown Indicator";
                if (structure.TryGetProperty("name", out var nameElement))
                {
                    name = nameElement.GetString() ?? "Unknown Indicator";
                }

                var description = "";
                if (structure.TryGetProperty("description", out var descElement))
                {
                    description = descElement.GetString() ?? "";
                }

                return new OECDIndicator
                {
                    Key = indicatorKey,
                    Dataset = dataset,
                    Name = name,
                    Description = description,
                    Agency = agency
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching indicator info for {indicatorKey}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<OECDDataPoint>> GetSeriesDataAsync(string indicatorKey, string countryCode = "USA", int? startYear = null, int? endYear = null)
        {
            try
            {
                _logger.LogInformation($"Fetching OECD data for indicator {indicatorKey}, country {countryCode}");

                // Parse the indicator key
                var parts = indicatorKey.Split('|');
                if (parts.Length < 3) return new List<OECDDataPoint>();

                var dataset = parts[0];
                var dimensions = parts[1];
                var agency = parts[2];

                // Replace country placeholder if it exists
                dimensions = dimensions.Replace("USA", countryCode);

                var url = $"data/{dataset}/{dimensions}/{agency}?json-lang=en&dimensionAtObservation=AllDimensions";

                if (startYear.HasValue || endYear.HasValue)
                {
                    var timeParams = new List<string>();
                    if (startYear.HasValue) timeParams.Add($"startTime={startYear}");
                    if (endYear.HasValue) timeParams.Add($"endTime={endYear}");
                    url += "&" + string.Join("&", timeParams);
                }

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"OECD API returned {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content).RootElement;

                var dataPoints = new List<OECDDataPoint>();

                if (jsonDoc.TryGetProperty("dataSets", out var dataSets) && dataSets.GetArrayLength() > 0)
                {
                    var dataSet = dataSets[0];

                    if (dataSet.TryGetProperty("observations", out var observations))
                    {
                        foreach (var observation in observations.EnumerateObject())
                        {
                            var key = observation.Name;
                            var values = observation.Value;

                            if (values.GetArrayLength() >= 1)
                            {
                                var value = values[0].ValueKind == JsonValueKind.Null ? (decimal?)null :
                                           values[0].TryGetDecimal(out var decimalValue) ? decimalValue : (decimal?)null;

                                if (value.HasValue)
                                {
                                    // Parse time period from key (format varies)
                                    var timePeriod = key.Split(':')[0];
                                    if (int.TryParse(timePeriod, out var year))
                                    {
                                        dataPoints.Add(new OECDDataPoint
                                        {
                                            Year = year,
                                            Value = value.Value,
                                            CountryCode = countryCode,
                                            IndicatorKey = indicatorKey
                                        });
                                    }
                                }
                            }
                        }
                    }
                }

                // Sort by year descending (most recent first)
                dataPoints = dataPoints.OrderByDescending(dp => dp.Year).ToList();

                _logger.LogInformation($"Successfully fetched {dataPoints.Count} data points for {indicatorKey}");
                return dataPoints;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching series data for {indicatorKey}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<OECDIndicator>> SearchIndicatorsAsync(string query, int maxResults = 20)
        {
            try
            {
                _logger.LogInformation($"Searching OECD indicators for: {query}");

                // OECD doesn't have a direct search API, so we'll use common datasets
                var commonDatasets = new List<string>
                {
                    "QNA",  // Quarterly National Accounts
                    "EO",   // Economic Outlook
                    "MEI",  // Main Economic Indicators
                    "REV",  // Revenue Statistics
                    "EXP",  // Expenditure Statistics
                    "ANA",  // Annual National Accounts
                    "SNA",  // System of National Accounts
                    "BOP",  // Balance of Payments
                    "FDI",  // Foreign Direct Investment
                    "TRADE" // Trade Statistics
                };

                var indicators = new List<OECDIndicator>();
                var queryLower = query.ToLowerInvariant();

                foreach (var dataset in commonDatasets)
                {
                    try
                    {
                        var url = $"dataflow/{dataset}/OECD?json-lang=en";
                        var response = await _httpClient.GetAsync(url);

                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            var jsonDoc = JsonDocument.Parse(content).RootElement;

                            if (jsonDoc.TryGetProperty("data", out var data) && data.TryGetProperty("dataflows", out var dataflows))
                            {
                                foreach (var dataflow in dataflows.EnumerateArray())
                                {
                                    if (dataflow.TryGetProperty("name", out var nameElement))
                                    {
                                        var name = nameElement.GetString();
                                        if (name != null && name.ToLowerInvariant().Contains(queryLower))
                                        {
                                            indicators.Add(new OECDIndicator
                                            {
                                                Key = $"{dataset}|{dataset}|OECD",
                                                Dataset = dataset,
                                                Name = name,
                                                Description = dataflow.TryGetProperty("description", out var desc) ? desc.GetString() : "",
                                                Agency = "OECD"
                                            });

                                            if (indicators.Count >= maxResults)
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to search dataset {dataset}: {ex.Message}");
                    }

                    if (indicators.Count >= maxResults)
                        break;
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
                var url = "codelist/OECD/CL_COUNTRY/OECD?json-lang=en";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"OECD API returned {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content).RootElement;

                var countries = new List<string>();

                if (jsonDoc.TryGetProperty("Codelist", out var codelist) &&
                    codelist.TryGetProperty("Code", out var codes))
                {
                    foreach (var code in codes.EnumerateArray())
                    {
                        var id = code.GetProperty("value").GetString();
                        var name = code.GetProperty("name").GetString();

                        if (id != null && name != null)
                        {
                            countries.Add($"{id} - {name}");
                        }
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

    public class OECDIndicator
    {
        public string? Key { get; set; }
        public string? Dataset { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Agency { get; set; }
    }

    public class OECDDataPoint
    {
        public int Year { get; set; }
        public decimal Value { get; set; }
        public string? CountryCode { get; set; }
        public string? IndicatorKey { get; set; }
    }
}