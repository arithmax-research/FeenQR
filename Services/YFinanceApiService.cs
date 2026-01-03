using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Service to directly fetch data from Yahoo Finance (no Python API needed)
    /// This provides a reliable fallback for fundamental data
    /// </summary>
    public class YFinanceApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<YFinanceApiService> _logger;

        public YFinanceApiService(IHttpClientFactory httpClientFactory, ILogger<YFinanceApiService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
            
            // Set headers to mimic a browser
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        /// <summary>
        /// Get fundamental data directly from Yahoo Finance
        /// </summary>
        public async Task<YFinanceFundamentals?> GetFundamentalsAsync(string symbol)
        {
            try
            {
                // Yahoo Finance API endpoint
                var url = $"https://query2.finance.yahoo.com/v10/finance/quoteSummary/{symbol}?modules=defaultKeyStatistics,financialData,summaryDetail";
                _logger.LogInformation($"Fetching fundamentals from Yahoo Finance: {url}");

                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Yahoo Finance returned {response.StatusCode} for {symbol}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning($"Empty response from Yahoo Finance for {symbol}");
                    return null;
                }

                var jsonDoc = JsonDocument.Parse(content);
                var result = jsonDoc.RootElement.GetProperty("quoteSummary").GetProperty("result")[0];
                
                var defaultKeyStatistics = result.GetProperty("defaultKeyStatistics");
                var financialData = result.GetProperty("financialData");
                var summaryDetail = result.GetProperty("summaryDetail");

                var fundamentals = new YFinanceFundamentals
                {
                    Symbol = symbol,
                    CurrentPrice = GetDecimalValue(summaryDetail, "previousClose"),
                    MarketCap = GetLongValue(summaryDetail, "marketCap"),
                    TrailingPE = GetDecimalValue(summaryDetail, "trailingPE"),
                    ForwardPE = GetDecimalValue(summaryDetail, "forwardPE"),
                    PegRatio = GetDecimalValue(defaultKeyStatistics, "pegRatio"),
                    PriceToBook = GetDecimalValue(defaultKeyStatistics, "priceToBook"),
                    ReturnOnEquity = GetDecimalValue(financialData, "returnOnEquity"),
                    DividendYield = GetDecimalValue(summaryDetail, "dividendYield"),
                    Beta = GetDecimalValue(summaryDetail, "beta"),
                    FiftyTwoWeekHigh = GetDecimalValue(summaryDetail, "fiftyTwoWeekHigh"),
                    FiftyTwoWeekLow = GetDecimalValue(summaryDetail, "fiftyTwoWeekLow"),
                    EarningsGrowth = GetDecimalValue(financialData, "earningsGrowth"),
                    RevenueGrowth = GetDecimalValue(financialData, "revenueGrowth")
                };

                _logger.LogInformation($"Successfully fetched Yahoo Finance data for {symbol}: PE={fundamentals.TrailingPE}, PEG={fundamentals.PegRatio}, ROE={fundamentals.ReturnOnEquity}");
                return fundamentals;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching Yahoo Finance data for {symbol}");
                return null;
            }
        }

        private decimal? GetDecimalValue(JsonElement element, string propertyName)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("raw", out var raw))
                    {
                        return raw.GetDecimal();
                    }
                    else if (prop.ValueKind == JsonValueKind.Number)
                    {
                        return prop.GetDecimal();
                    }
                }
            }
            catch { }
            return null;
        }

        private long? GetLongValue(JsonElement element, string propertyName)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var prop))
                {
                    if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("raw", out var raw))
                    {
                        return raw.GetInt64();
                    }
                    else if (prop.ValueKind == JsonValueKind.Number)
                    {
                        return prop.GetInt64();
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Check if Yahoo Finance is accessible
        /// </summary>
        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("https://finance.yahoo.com");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    public class YFinanceFundamentals
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("currentPrice")]
        public decimal? CurrentPrice { get; set; }

        [JsonPropertyName("marketCap")]
        public long? MarketCap { get; set; }

        [JsonPropertyName("enterpriseValue")]
        public long? EnterpriseValue { get; set; }

        [JsonPropertyName("trailingPE")]
        public decimal? TrailingPE { get; set; }

        [JsonPropertyName("forwardPE")]
        public decimal? ForwardPE { get; set; }

        [JsonPropertyName("pegRatio")]
        public decimal? PegRatio { get; set; }

        [JsonPropertyName("priceToBook")]
        public decimal? PriceToBook { get; set; }

        [JsonPropertyName("priceToSalesTrailing12Months")]
        public decimal? PriceToSalesTrailing12Months { get; set; }

        [JsonPropertyName("debtToEquity")]
        public decimal? DebtToEquity { get; set; }

        [JsonPropertyName("returnOnEquity")]
        public decimal? ReturnOnEquity { get; set; }

        [JsonPropertyName("dividendYield")]
        public decimal? DividendYield { get; set; }

        [JsonPropertyName("payoutRatio")]
        public decimal? PayoutRatio { get; set; }

        [JsonPropertyName("freeCashflow")]
        public long? FreeCashflow { get; set; }

        [JsonPropertyName("operatingCashflow")]
        public long? OperatingCashflow { get; set; }

        [JsonPropertyName("revenueGrowth")]
        public decimal? RevenueGrowth { get; set; }

        [JsonPropertyName("earningsGrowth")]
        public decimal? EarningsGrowth { get; set; }

        [JsonPropertyName("sharesOutstanding")]
        public long? SharesOutstanding { get; set; }

        [JsonPropertyName("beta")]
        public decimal? Beta { get; set; }

        [JsonPropertyName("fiftyTwoWeekHigh")]
        public decimal? FiftyTwoWeekHigh { get; set; }

        [JsonPropertyName("fiftyTwoWeekLow")]
        public decimal? FiftyTwoWeekLow { get; set; }

        [JsonPropertyName("averageVolume")]
        public long? AverageVolume { get; set; }

        [JsonPropertyName("sector")]
        public string? Sector { get; set; }

        [JsonPropertyName("industry")]
        public string? Industry { get; set; }

        [JsonPropertyName("longBusinessSummary")]
        public string? LongBusinessSummary { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }
}


    public class YFinanceFundamentals
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("currentPrice")]
        public decimal? CurrentPrice { get; set; }

        [JsonPropertyName("marketCap")]
        public long? MarketCap { get; set; }

        [JsonPropertyName("enterpriseValue")]
        public long? EnterpriseValue { get; set; }

        [JsonPropertyName("trailingPE")]
        public decimal? TrailingPE { get; set; }

        [JsonPropertyName("forwardPE")]
        public decimal? ForwardPE { get; set; }

        [JsonPropertyName("pegRatio")]
        public decimal? PegRatio { get; set; }

        [JsonPropertyName("priceToBook")]
        public decimal? PriceToBook { get; set; }

        [JsonPropertyName("priceToSalesTrailing12Months")]
        public decimal? PriceToSalesTrailing12Months { get; set; }

        [JsonPropertyName("debtToEquity")]
        public decimal? DebtToEquity { get; set; }

        [JsonPropertyName("returnOnEquity")]
        public decimal? ReturnOnEquity { get; set; }

        [JsonPropertyName("dividendYield")]
        public decimal? DividendYield { get; set; }

        [JsonPropertyName("payoutRatio")]
        public decimal? PayoutRatio { get; set; }

        [JsonPropertyName("freeCashflow")]
        public long? FreeCashflow { get; set; }

        [JsonPropertyName("operatingCashflow")]
        public long? OperatingCashflow { get; set; }

        [JsonPropertyName("revenueGrowth")]
        public decimal? RevenueGrowth { get; set; }

        [JsonPropertyName("earningsGrowth")]
        public decimal? EarningsGrowth { get; set; }

        [JsonPropertyName("sharesOutstanding")]
        public long? SharesOutstanding { get; set; }

        [JsonPropertyName("beta")]
        public decimal? Beta { get; set; }

        [JsonPropertyName("fiftyTwoWeekHigh")]
        public decimal? FiftyTwoWeekHigh { get; set; }

        [JsonPropertyName("fiftyTwoWeekLow")]
        public decimal? FiftyTwoWeekLow { get; set; }

        [JsonPropertyName("averageVolume")]
        public long? AverageVolume { get; set; }

        [JsonPropertyName("sector")]
        public string? Sector { get; set; }

        [JsonPropertyName("industry")]
        public string? Industry { get; set; }

        [JsonPropertyName("longBusinessSummary")]
        public string? LongBusinessSummary { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }
}
