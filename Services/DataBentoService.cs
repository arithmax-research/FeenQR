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
                         $"limit={limit}";

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
                         $"end={endStr}";
                
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
                var bars = JsonSerializer.Deserialize<List<DataBentoOHLCV>>(responseBody);
                return bars ?? new List<DataBentoOHLCV>();
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
        public async Task<List<DataBentoSymbol>> GetFuturesSymbolsAsync(string dataset = "CME.FUT")
        {
            try
            {
                var url = $"{BaseUrl}/v0/metadata.list_symbols?dataset={dataset}";
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = GetAuthHeader();
                
                var httpResponse = await _httpClient.SendAsync(request);
                var responseBody = await httpResponse.Content.ReadAsStringAsync();
                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorMsg = $"DataBento API error: StatusCode={httpResponse.StatusCode}, Body={responseBody}";
                    _logger.LogError(errorMsg);
                    Console.WriteLine(errorMsg);
                    return new List<DataBentoSymbol>();
                }
                var symbols = JsonSerializer.Deserialize<DataBentoSymbolsResponse>(responseBody);
                return symbols?.Symbols ?? new List<DataBentoSymbol>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting futures symbols. Check subscription for CME data access.");
                return new List<DataBentoSymbol>();
            }
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
                var contracts = symbols
                    .Where(s => s.RawSymbol.StartsWith(rootSymbol, StringComparison.OrdinalIgnoreCase))
                    .Select(s => new DataBentoFuturesContract
                    {
                        Symbol = s.RawSymbol,
                        Description = s.Description,
                        StartDate = s.StartDate,
                        EndDate = s.EndDate
                    })
                    .ToList();

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
        [JsonPropertyName("ts_event")]
        public long TsEvent { get; set; }
        
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;
        
        [JsonPropertyName("open")]
        public decimal Open { get; set; }
        
        [JsonPropertyName("high")]
        public decimal High { get; set; }
        
        [JsonPropertyName("low")]
        public decimal Low { get; set; }
        
        [JsonPropertyName("close")]
        public decimal Close { get; set; }
        
        [JsonPropertyName("volume")]
        public long Volume { get; set; }
        
        public DateTime EventTime => DateTimeOffset.FromUnixTimeMilliseconds(TsEvent / 1_000_000).DateTime;
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
}
