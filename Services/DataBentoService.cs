using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// DataBento API service for high-quality market data and futures
    /// </summary>
    public class DataBentoService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DataBentoService> _logger;
        private readonly string _apiKey;
        private readonly string _userId;
        private readonly string _prodName;
        private const string BaseUrl = "https://hist.databento.com";

        public DataBentoService(HttpClient httpClient, ILogger<DataBentoService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["DataBento:ApiKey"] ?? throw new ArgumentException("DataBento API key not configured");
            _userId = configuration["DataBento:UserId"] ?? throw new ArgumentException("DataBento UserId not configured");
            _prodName = configuration["DataBento:ProdName"] ?? throw new ArgumentException("DataBento ProdName not configured");
            // Removed default Authorization header to set it per request
        }

        private AuthenticationHeaderValue GetAuthHeader()
        {
            var byteArray = Encoding.ASCII.GetBytes($"{_apiKey}:");  // API key as username, empty password
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        /// <summary>
        /// Get historical trades for a symbol
        /// </summary>
        public async Task<List<DataBentoTrade>> GetHistoricalTradesAsync(
            string symbol,
            DateTime start,
            DateTime end,
            string dataset = "XNAS.ITCH",
            int limit = 1000)
        {
            try
            {
                var startStr = start.ToString("yyyy-MM-dd");
                var endStr = end.ToString("yyyy-MM-dd");
                
                var url = $"{BaseUrl}/v0/timeseries.get_range?" +
                         $"dataset={dataset}&" +
                         $"symbols={symbol}&" +
                         $"schema=trades&" +
                         $"start={startStr}&" +
                         $"end={endStr}&" +
                         $"limit={limit}&" +
                         $"encoding=json"; // Request JSON format instead of default CSV

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = GetAuthHeader();
                
                var httpResponse = await _httpClient.SendAsync(request);
                var responseBody = await httpResponse.Content.ReadAsStringAsync();
                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorMsg = $"DataBento API error: StatusCode={httpResponse.StatusCode}, Body={responseBody}";
                    _logger.LogError(errorMsg);
                    Console.WriteLine(errorMsg);
                    return new List<DataBentoTrade>();
                }
                var trades = JsonSerializer.Deserialize<List<DataBentoTrade>>(responseBody);
                return trades ?? new List<DataBentoTrade>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting historical trades for {Symbol}", symbol);
                return new List<DataBentoTrade>();
            }
        }

        /// <summary>
        /// Get OHLCV bars for a symbol (focus on US Equities - free tier)
        /// </summary>
        public async Task<List<DataBentoOHLCV>> GetOHLCVAsync(
            string symbol,
            DateTime start,
            DateTime end,
            string dataset = "XNAS.ITCH", // US Equities NASDAQ
            string schema = "ohlcv-1d")
        {
            try
            {
                var startStr = start.ToString("yyyy-MM-dd");
                var endStr = end.ToString("yyyy-MM-dd");
                var url = $"{BaseUrl}/v0/timeseries.get_range?" +
                         $"dataset={dataset}&" +
                         $"symbols={symbol}&" +
                         $"schema={schema}&" +
                         $"start={startStr}&" +
                         $"end={endStr}&" +
                         $"encoding=json"; // Request JSON format instead of default CSV
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = GetAuthHeader();
                
                var httpResponse = await _httpClient.SendAsync(request);
                var responseBody = await httpResponse.Content.ReadAsStringAsync();
                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorMsg = $"DataBento API error: StatusCode={httpResponse.StatusCode}, Body={responseBody}";
                    _logger.LogError(errorMsg);
                    Console.WriteLine(errorMsg);
                    return new List<DataBentoOHLCV>();
                }
                
                // Parse NDJSON (Newline Delimited JSON) - each line is a separate JSON object
                var bars = new List<DataBentoOHLCV>();
                var lines = responseBody.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var bar = JsonSerializer.Deserialize<DataBentoOHLCV>(line);
                        if (bar != null)
                        {
                            bars.Add(bar);
                        }
                    }
                }
                return bars;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting OHLCV for {Symbol}. Note: Free tier limited to US Equities historical data.", symbol);
                Console.WriteLine($"DataBento API exception: {ex.Message}");
                return new List<DataBentoOHLCV>();
            }
        }

        /// <summary>
        /// Get futures symbols for CME/CBOT (available with subscription)
        /// </summary>
        public async Task<List<DataBentoSymbol>> GetFuturesSymbolsAsync(string dataset = "GLBX.MDP3")
        {
            try
            {
                // Try to get symbols dynamically from DataBento Symbology API
                var symbols = await GetFuturesSymbolsFromAPIAsync();
                if (symbols.Any())
                {
                    return symbols;
                }

                // Fallback to predefined list if API fails
                _logger.LogWarning("Failed to fetch symbols from DataBento API, using fallback list");
                return GetFallbackFuturesSymbols();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting futures symbols, using fallback list");
                return GetFallbackFuturesSymbols();
            }
        }

        /// <summary>
        /// Get futures symbols from DataBento Symbology API
        /// </summary>
        private async Task<List<DataBentoSymbol>> GetFuturesSymbolsFromAPIAsync()
        {
            try
            {
                // Use DataBento's symbology resolve API to get all futures root symbols
                var url = $"{BaseUrl}/v0/symbology.resolve?" +
                         $"stype_in=parent&" +
                         $"instrument_class=future";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = GetAuthHeader();

                var httpResponse = await _httpClient.SendAsync(request);
                var responseBody = await httpResponse.Content.ReadAsStringAsync();

                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("DataBento symbology API returned {StatusCode}: {ResponseBody}",
                        httpResponse.StatusCode, responseBody);
                    return new List<DataBentoSymbol>();
                }

                // Parse the response - DataBento returns NDJSON format
                var symbols = new List<DataBentoSymbol>();
                var lines = responseBody.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        try
                        {
                            var symbolData = JsonSerializer.Deserialize<DataBentoSymbologyResult>(line);
                            if (symbolData != null && !string.IsNullOrEmpty(symbolData.Symbol))
                            {
                                // Extract root symbol (e.g., "ES" from "ES.FUT")
                                var rootSymbol = symbolData.Symbol;
                                if (rootSymbol.EndsWith(".FUT", StringComparison.OrdinalIgnoreCase))
                                {
                                    rootSymbol = rootSymbol.Substring(0, rootSymbol.Length - 4);
                                }

                                symbols.Add(new DataBentoSymbol
                                {
                                    RawSymbol = rootSymbol,
                                    Description = symbolData.Description ?? $"{rootSymbol} Futures",
                                    StartDate = DateTime.MinValue, // Not available in this API
                                    EndDate = DateTime.MaxValue     // Not available in this API
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing symbology line: {Line}", line);
                        }
                    }
                }

                _logger.LogInformation("Fetched {Count} futures symbols from DataBento API", symbols.Count);
                return symbols;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching futures symbols from DataBento API");
                return new List<DataBentoSymbol>();
            }
        }

        /// <summary>
        /// Get fallback list of common futures symbols
        /// </summary>
        private List<DataBentoSymbol> GetFallbackFuturesSymbols()
        {
            return new List<DataBentoSymbol>
            {
                // Equity Index Futures
                new DataBentoSymbol { RawSymbol = "ES", Description = "E-mini S&P 500 Futures" },
                new DataBentoSymbol { RawSymbol = "NQ", Description = "E-mini NASDAQ-100 Futures" },
                new DataBentoSymbol { RawSymbol = "YM", Description = "E-mini Dow Jones Futures" },
                new DataBentoSymbol { RawSymbol = "RTY", Description = "E-mini Russell 2000 Futures" },
                new DataBentoSymbol { RawSymbol = "EMD", Description = "E-mini S&P MidCap 400 Futures" },
                new DataBentoSymbol { RawSymbol = "NKD", Description = "Nikkei 225 Futures" },
                new DataBentoSymbol { RawSymbol = "DAX", Description = "DAX Futures" },
                new DataBentoSymbol { RawSymbol = "STOXX50E", Description = "Euro STOXX 50 Futures" },

                // Energy Futures
                new DataBentoSymbol { RawSymbol = "CL", Description = "Crude Oil Futures" },
                new DataBentoSymbol { RawSymbol = "NG", Description = "Natural Gas Futures" },
                new DataBentoSymbol { RawSymbol = "HO", Description = "Heating Oil Futures" },
                new DataBentoSymbol { RawSymbol = "RB", Description = "RBOB Gasoline Futures" },
                new DataBentoSymbol { RawSymbol = "BZ", Description = "Brent Crude Oil Futures" },
                new DataBentoSymbol { RawSymbol = "QG", Description = "E-mini Natural Gas Futures" },
                new DataBentoSymbol { RawSymbol = "QCL", Description = "E-mini Crude Oil Futures" },

                // Metals Futures
                new DataBentoSymbol { RawSymbol = "GC", Description = "Gold Futures" },
                new DataBentoSymbol { RawSymbol = "SI", Description = "Silver Futures" },
                new DataBentoSymbol { RawSymbol = "HG", Description = "Copper Futures" },
                new DataBentoSymbol { RawSymbol = "PL", Description = "Platinum Futures" },
                new DataBentoSymbol { RawSymbol = "PA", Description = "Palladium Futures" },

                // Treasury Futures
                new DataBentoSymbol { RawSymbol = "ZB", Description = "Treasury Bond Futures" },
                new DataBentoSymbol { RawSymbol = "ZN", Description = "Treasury Note Futures" },
                new DataBentoSymbol { RawSymbol = "ZF", Description = "5-Year Treasury Note Futures" },
                new DataBentoSymbol { RawSymbol = "ZT", Description = "2-Year Treasury Note Futures" },
                new DataBentoSymbol { RawSymbol = "GE", Description = "Eurodollar Futures" },
                new DataBentoSymbol { RawSymbol = "ED", Description = "Eurodollar Futures" },
                new DataBentoSymbol { RawSymbol = "2YY", Description = "2-Year Treasury Futures" },
                new DataBentoSymbol { RawSymbol = "5YY", Description = "5-Year Treasury Futures" },
                new DataBentoSymbol { RawSymbol = "10Y", Description = "10-Year Treasury Futures" },
                new DataBentoSymbol { RawSymbol = "30Y", Description = "30-Year Treasury Futures" },

                // Agricultural Futures
                new DataBentoSymbol { RawSymbol = "ZS", Description = "Soybean Futures" },
                new DataBentoSymbol { RawSymbol = "ZC", Description = "Corn Futures" },
                new DataBentoSymbol { RawSymbol = "ZW", Description = "Wheat Futures" },
                new DataBentoSymbol { RawSymbol = "ZM", Description = "Soybean Meal Futures" },
                new DataBentoSymbol { RawSymbol = "ZL", Description = "Soybean Oil Futures" },
                new DataBentoSymbol { RawSymbol = "ZO", Description = "Oat Futures" },
                new DataBentoSymbol { RawSymbol = "ZK", Description = "Soybean Futures" },
                new DataBentoSymbol { RawSymbol = "LE", Description = "Live Cattle Futures" },
                new DataBentoSymbol { RawSymbol = "HE", Description = "Lean Hog Futures" },
                new DataBentoSymbol { RawSymbol = "GF", Description = "Feeder Cattle Futures" },
                new DataBentoSymbol { RawSymbol = "KC", Description = "Coffee Futures" },
                new DataBentoSymbol { RawSymbol = "SB", Description = "Sugar Futures" },
                new DataBentoSymbol { RawSymbol = "CT", Description = "Cotton Futures" },
                new DataBentoSymbol { RawSymbol = "CC", Description = "Cocoa Futures" },

                // Currency Futures
                new DataBentoSymbol { RawSymbol = "EUR", Description = "Euro Futures" },
                new DataBentoSymbol { RawSymbol = "GBP", Description = "British Pound Futures" },
                new DataBentoSymbol { RawSymbol = "JPY", Description = "Japanese Yen Futures" },
                new DataBentoSymbol { RawSymbol = "CHF", Description = "Swiss Franc Futures" },
                new DataBentoSymbol { RawSymbol = "CAD", Description = "Canadian Dollar Futures" },
                new DataBentoSymbol { RawSymbol = "AUD", Description = "Australian Dollar Futures" },

                // Crypto Futures
                new DataBentoSymbol { RawSymbol = "BTC", Description = "Bitcoin Futures" },
                new DataBentoSymbol { RawSymbol = "ETH", Description = "Ethereum Futures" },
                new DataBentoSymbol { RawSymbol = "LTC", Description = "Litecoin Futures" },
                new DataBentoSymbol { RawSymbol = "BCH", Description = "Bitcoin Cash Futures" }
            };
        }

        /// <summary>
        /// Get available datasets
        /// </summary>
        public async Task<List<DataBentoDataset>> GetDatasetsAsync()
        {
            try
            {
                var url = $"{BaseUrl}/v0/metadata.list_datasets";
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = GetAuthHeader();
                
                var httpResponse = await _httpClient.SendAsync(request);
                var responseBody = await httpResponse.Content.ReadAsStringAsync();
                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorMsg = $"DataBento API error: StatusCode={httpResponse.StatusCode}, Body={responseBody}";
                    _logger.LogError(errorMsg);
                    Console.WriteLine(errorMsg);
                    return new List<DataBentoDataset>();
                }
                var datasets = JsonSerializer.Deserialize<DataBentoDatasetsResponse>(responseBody);
                return datasets?.Datasets ?? new List<DataBentoDataset>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting datasets");
                return new List<DataBentoDataset>();
            }
        }

        /// <summary>
        /// Get futures contracts for a specific root symbol (e.g., ES for S&P 500)
        /// </summary>
        public async Task<List<DataBentoFuturesContract>> GetFuturesContractsAsync(
            string rootSymbol,
            DateTime? expiration = null)
        {
            try
            {
                var symbols = await GetFuturesSymbolsAsync();
                
                // Check if this is a valid futures root symbol by looking it up in the fetched symbols
                var validSymbol = symbols.FirstOrDefault(s => 
                    s.RawSymbol.Equals(rootSymbol, StringComparison.OrdinalIgnoreCase));
                
                if (validSymbol == null)
                {
                    _logger.LogWarning("Symbol {RootSymbol} is not a valid futures contract root. Futures contracts are available for commodities, indices, currencies, etc., not individual stocks.", rootSymbol);
                    return new List<DataBentoFuturesContract>();
                }

                // For now, return the root symbol as a contract with placeholder dates
                // In a full implementation, you would fetch actual contract details
                var contracts = new List<DataBentoFuturesContract>
                {
                    new DataBentoFuturesContract
                    {
                        Symbol = validSymbol.RawSymbol,
                        Description = validSymbol.Description,
                        StartDate = DateTime.Today.AddMonths(-1), // Placeholder
                        EndDate = DateTime.Today.AddMonths(6)     // Placeholder
                    }
                };

                if (expiration.HasValue)
                {
                    contracts = contracts
                        .Where(c => c.EndDate >= expiration.Value)
                        .ToList();
                }

                return contracts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting futures contracts for {RootSymbol}", rootSymbol);
                return new List<DataBentoFuturesContract>();
            }
        }

        /// <summary>
        /// Get market data statistics
        /// </summary>
        public async Task<DataBentoStats?> GetStatsAsync(
            string symbol,
            DateTime start,
            DateTime end,
            string dataset = "XNAS.ITCH")
        {
            try
            {
                var startStr = start.ToString("yyyy-MM-dd");
                var endStr = end.ToString("yyyy-MM-dd");
                
                var url = $"{BaseUrl}/v0/timeseries.get_range?" +
                         $"dataset={dataset}&" +
                         $"symbols={symbol}&" +
                         $"schema=statistics&" +
                         $"start={startStr}&" +
                         $"end={endStr}&" +
                         $"limit=1";
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = GetAuthHeader();
                
                var httpResponse = await _httpClient.SendAsync(request);
                var responseBody = await httpResponse.Content.ReadAsStringAsync();
                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorMsg = $"DataBento API error: StatusCode={httpResponse.StatusCode}, Body={responseBody}";
                    _logger.LogError(errorMsg);
                    Console.WriteLine(errorMsg);
                    return null;
                }
                var stats = JsonSerializer.Deserialize<List<DataBentoStats>>(responseBody);
                return stats?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stats for {Symbol}", symbol);
                return null;
            }
        }
    }

    // DataBento data models (unchanged)
    public class DataBentoTrade
    {
        [JsonPropertyName("ts_event")]
        public long TsEvent { get; set; }
        
        [JsonPropertyName("ts_recv")]
        public long TsRecv { get; set; }
        
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;
        
        [JsonPropertyName("price")]
        public decimal Price { get; set; }
        
        [JsonPropertyName("size")]
        public long Size { get; set; }
        
        public DateTime EventTime => DateTimeOffset.FromUnixTimeMilliseconds(TsEvent / 1_000_000).DateTime;
        public DateTime ReceiveTime => DateTimeOffset.FromUnixTimeMilliseconds(TsRecv / 1_000_000).DateTime;
    }

    public class DataBentoOHLCV
    {
        [JsonPropertyName("hd")]
        public DataBentoHeader Hd { get; set; } = new();
        
        [JsonPropertyName("open")]
        public string OpenStr { get; set; } = string.Empty;
        
        [JsonPropertyName("high")]
        public string HighStr { get; set; } = string.Empty;
        
        [JsonPropertyName("low")]
        public string LowStr { get; set; } = string.Empty;
        
        [JsonPropertyName("close")]
        public string CloseStr { get; set; } = string.Empty;
        
        [JsonPropertyName("volume")]
        public string VolumeStr { get; set; } = string.Empty;
        
        // Calculated properties
        public DateTime EventTime => DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(Hd.TsEvent) / 1_000_000).DateTime; // Convert nanoseconds to milliseconds
        public decimal Open => decimal.Parse(OpenStr) / 1_000_000_000m; // Convert from nanos to dollars
        public decimal High => decimal.Parse(HighStr) / 1_000_000_000m;
        public decimal Low => decimal.Parse(LowStr) / 1_000_000_000m;
        public decimal Close => decimal.Parse(CloseStr) / 1_000_000_000m;
        public long Volume => long.Parse(VolumeStr);
    }

    public class DataBentoHeader
    {
        [JsonPropertyName("ts_event")]
        public string TsEvent { get; set; } = string.Empty;
        
        [JsonPropertyName("rtype")]
        public int Rtype { get; set; }
        
        [JsonPropertyName("publisher_id")]
        public int PublisherId { get; set; }
        
        [JsonPropertyName("instrument_id")]
        public int InstrumentId { get; set; }
    }

    public class DataBentoSymbol
    {
        [JsonPropertyName("raw_symbol")]
        public string RawSymbol { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("start_date")]
        public DateTime StartDate { get; set; }
        
        [JsonPropertyName("end_date")]
        public DateTime EndDate { get; set; }
    }

    public class DataBentoSymbolsResponse
    {
        [JsonPropertyName("symbols")]
        public List<DataBentoSymbol>? Symbols { get; set; }
    }

    public class DataBentoDataset
    {
        [JsonPropertyName("dataset")]
        public string Dataset { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("schema")]
        public List<string>? Schema { get; set; }
    }

    public class DataBentoDatasetsResponse
    {
        [JsonPropertyName("datasets")]
        public List<DataBentoDataset>? Datasets { get; set; }
    }

    public class DataBentoFuturesContract
    {
        public string Symbol { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsExpired => EndDate < DateTime.UtcNow;
        public int DaysToExpiry => (EndDate - DateTime.UtcNow).Days;
    }

    public class DataBentoStats
    {
        [JsonPropertyName("ts_event")]
        public long TsEvent { get; set; }
        
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;
        
        [JsonPropertyName("open_price")]
        public decimal OpenPrice { get; set; }
        
        [JsonPropertyName("high_price")]
        public decimal HighPrice { get; set; }
        
        [JsonPropertyName("low_price")]
        public decimal LowPrice { get; set; }
        
        [JsonPropertyName("close_price")]
        public decimal ClosePrice { get; set; }
        
        [JsonPropertyName("volume")]
        public long Volume { get; set; }
        
        public DateTime EventTime => DateTimeOffset.FromUnixTimeMilliseconds(TsEvent / 1_000_000).DateTime;
    }

    public class DataBentoSymbologyResult
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;
        
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        
        [JsonPropertyName("instrument_class")]
        public string? InstrumentClass { get; set; }
        
        [JsonPropertyName("exchange")]
        public string? Exchange { get; set; }
        
        [JsonPropertyName("start_date")]
        public DateTime? StartDate { get; set; }
        
        [JsonPropertyName("end_date")]
        public DateTime? EndDate { get; set; }
    }
}
