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
    public class IMFService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<IMFService> _logger;
        private readonly IConfiguration _configuration;

        public IMFService(HttpClient httpClient, ILogger<IMFService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;

            // Set base address for IMF API
            _httpClient.BaseAddress = new Uri("https://www.imf.org/external/datamapper/api/v1/");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "QuantResearchAgent/1.0");
        }

        public async Task<List<IMFIndicator>> GetPopularIndicatorsAsync()
        {
            try
            {
                _logger.LogInformation("Fetching popular IMF indicators");

                // Get a list of popular economic indicators from IMF
                var popularIndicators = new List<string>
                {
                    "NGDP_RPCH",    // GDP growth rate
                    "PCPIPCH",      // Inflation rate
                    "LUR",          // Unemployment rate
                    "NGDPD",        // GDP current prices
                    "PPPGDP",       // GDP per capita, PPP
                    "GGXWDG_NGDP",  // Government debt to GDP
                    "CA",           // Current account balance
                    "BX",           // Exports of goods and services
                    "BM",           // Imports of goods and services
                    "ENDA_XDC_USD_RATE" // Exchange rate
                };

                var indicators = new List<IMFIndicator>();

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

                _logger.LogInformation($"Successfully fetched {indicators.Count} IMF indicators");
                return indicators;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching popular indicators: {ex.Message}");
                throw;
            }
        }

        public async Task<IMFIndicator> GetIndicatorInfoAsync(string indicatorCode)
        {
            try
            {
                // IMF API uses different endpoints for different data types
                // For now, we'll create indicator info based on known codes
                var indicatorNames = new Dictionary<string, string>
                {
                    ["NGDP_RPCH"] = "GDP growth rate",
                    ["PCPIPCH"] = "Inflation, average consumer prices",
                    ["LUR"] = "Unemployment rate",
                    ["NGDPD"] = "Gross domestic product, current prices",
                    ["PPPGDP"] = "Gross domestic product based on purchasing-power-parity",
                    ["GGXWDG_NGDP"] = "General government gross debt",
                    ["CA"] = "Current account balance",
                    ["BX"] = "Exports of goods and services",
                    ["BM"] = "Imports of goods and services",
                    ["ENDA_XDC_USD_RATE"] = "Exchange rates"
                };

                var descriptions = new Dictionary<string, string>
                {
                    ["NGDP_RPCH"] = "Annual percentage growth rate of GDP at market prices based on constant local currency",
                    ["PCPIPCH"] = "Annual percentage change in the cost to the average consumer of acquiring a basket of goods and services",
                    ["LUR"] = "Unemployment rate as a percentage of total labor force",
                    ["NGDPD"] = "Gross domestic product in current prices",
                    ["PPPGDP"] = "Gross domestic product converted to international dollars using purchasing power parity rates",
                    ["GGXWDG_NGDP"] = "Gross debt consists of all liabilities that require payment or payments of interest and/or principal by the debtor to the creditor",
                    ["CA"] = "Current account balance is the sum of net exports of goods and services, net primary income, and net secondary income",
                    ["BX"] = "Exports of goods and services represent the value of all goods and other market services provided to the rest of the world",
                    ["BM"] = "Imports of goods and services represent the value of all goods and other market services received from the rest of the world",
                    ["ENDA_XDC_USD_RATE"] = "Official exchange rate (local currency per U.S. dollar)"
                };

                if (indicatorNames.TryGetValue(indicatorCode, out var name))
                {
                    return new IMFIndicator
                    {
                        Code = indicatorCode,
                        Name = name,
                        Description = descriptions.TryGetValue(indicatorCode, out var desc) ? desc : "",
                        Source = "International Monetary Fund"
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching indicator info for {indicatorCode}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<IMFDataPoint>> GetSeriesDataAsync(string indicatorCode, string countryCode = "USA", int? startYear = null, int? endYear = null)
        {
            try
            {
                _logger.LogInformation($"Fetching IMF data for indicator {indicatorCode}, country {countryCode}");

                // IMF API structure varies by indicator
                // We'll use the general data endpoint
                var url = $"{indicatorCode}/{countryCode}";

                if (startYear.HasValue || endYear.HasValue)
                {
                    var timeParams = new List<string>();
                    if (startYear.HasValue) timeParams.Add($"startYear={startYear}");
                    if (endYear.HasValue) timeParams.Add($"endYear={endYear}");
                    url += "?" + string.Join("&", timeParams);
                }

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"IMF API returned {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content).RootElement;

                var dataPoints = new List<IMFDataPoint>();

                // Parse IMF data structure (varies by indicator)
                if (jsonDoc.TryGetProperty("values", out var values))
                {
                    foreach (var value in values.EnumerateObject())
                    {
                        var yearStr = value.Name;
                        var dataValue = value.Value;

                        if (int.TryParse(yearStr, out var year))
                        {
                            var decimalValue = dataValue.ValueKind == JsonValueKind.Null ? (decimal?)null :
                                             dataValue.TryGetDecimal(out var val) ? val : (decimal?)null;

                            if (decimalValue.HasValue)
                            {
                                dataPoints.Add(new IMFDataPoint
                                {
                                    Year = year,
                                    Value = decimalValue.Value,
                                    CountryCode = countryCode,
                                    IndicatorCode = indicatorCode
                                });
                            }
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

        public async Task<List<IMFIndicator>> SearchIndicatorsAsync(string query, int maxResults = 20)
        {
            try
            {
                _logger.LogInformation($"Searching IMF indicators for: {query}");

                // IMF doesn't have a comprehensive search API, so we'll search through known indicators
                var allIndicators = new Dictionary<string, string>
                {
                    ["NGDP_RPCH"] = "GDP growth rate",
                    ["PCPIPCH"] = "Inflation, average consumer prices",
                    ["LUR"] = "Unemployment rate",
                    ["NGDPD"] = "Gross domestic product, current prices",
                    ["PPPGDP"] = "Gross domestic product based on purchasing-power-parity",
                    ["GGXWDG_NGDP"] = "General government gross debt",
                    ["CA"] = "Current account balance",
                    ["BX"] = "Exports of goods and services",
                    ["BM"] = "Imports of goods and services",
                    ["ENDA_XDC_USD_RATE"] = "Exchange rates",
                    ["FMB"] = "Broad money",
                    ["FM1"] = "Money supply M1",
                    ["FM2"] = "Money supply M2",
                    ["FIR"] = "Real interest rate",
                    ["FIDR"] = "Deposit interest rate",
                    ["FILR"] = "Lending interest rate",
                    ["GGX"] = "General government expenditure",
                    ["GGXR"] = "General government revenue",
                    ["GGR"] = "General government net lending/borrowing",
                    ["GGXWDG"] = "General government gross debt",
                    ["BCA"] = "Current account balance",
                    ["BK"] = "Capital account",
                    ["BF"] = "Financial account",
                    ["BOP"] = "Balance of payments"
                };

                var indicators = new List<IMFIndicator>();
                var queryLower = query.ToLowerInvariant();

                foreach (var kvp in allIndicators)
                {
                    if ((kvp.Key.ToLowerInvariant().Contains(queryLower) ||
                         kvp.Value.ToLowerInvariant().Contains(queryLower)) &&
                        indicators.Count < maxResults)
                    {
                        indicators.Add(new IMFIndicator
                        {
                            Code = kvp.Key,
                            Name = kvp.Value,
                            Description = await GetIndicatorDescriptionAsync(kvp.Key),
                            Source = "International Monetary Fund"
                        });
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

        private async Task<string> GetIndicatorDescriptionAsync(string code)
        {
            // Return descriptions for known indicators
            var descriptions = new Dictionary<string, string>
            {
                ["NGDP_RPCH"] = "Annual percentage growth rate of GDP at market prices based on constant local currency",
                ["PCPIPCH"] = "Annual percentage change in the cost to the average consumer of acquiring a basket of goods and services",
                ["LUR"] = "Unemployment rate as a percentage of total labor force",
                ["NGDPD"] = "Gross domestic product in current prices",
                ["PPPGDP"] = "Gross domestic product converted to international dollars using purchasing power parity rates",
                ["GGXWDG_NGDP"] = "Gross debt consists of all liabilities that require payment or payments of interest and/or principal",
                ["CA"] = "Current account balance is the sum of net exports of goods and services, net primary income, and net secondary income",
                ["BX"] = "Exports of goods and services represent the value of all goods and other market services provided to the rest of the world",
                ["BM"] = "Imports of goods and services represent the value of all goods and other market services received from the rest of the world",
                ["ENDA_XDC_USD_RATE"] = "Official exchange rate (local currency per U.S. dollar)"
            };

            return descriptions.TryGetValue(code, out var desc) ? desc : "";
        }

        public async Task<List<string>> GetAvailableCountriesAsync()
        {
            try
            {
                // IMF covers most countries, return a comprehensive list
                var countries = new List<string>
                {
                    "USA - United States",
                    "GBR - United Kingdom",
                    "DEU - Germany",
                    "FRA - France",
                    "ITA - Italy",
                    "ESP - Spain",
                    "CAN - Canada",
                    "JPN - Japan",
                    "AUS - Australia",
                    "CHN - China",
                    "IND - India",
                    "BRA - Brazil",
                    "MEX - Mexico",
                    "ZAF - South Africa",
                    "RUS - Russia",
                    "KOR - Korea",
                    "IDN - Indonesia",
                    "TUR - Turkey",
                    "SAU - Saudi Arabia",
                    "ARG - Argentina"
                };

                return countries.OrderBy(c => c).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching countries: {ex.Message}");
                throw;
            }
        }
    }

    public class IMFIndicator
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Source { get; set; }
    }

    public class IMFDataPoint
    {
        public int Year { get; set; }
        public decimal Value { get; set; }
        public string? CountryCode { get; set; }
        public string? IndicatorCode { get; set; }
    }
}