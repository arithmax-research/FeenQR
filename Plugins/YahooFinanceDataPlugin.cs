using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Linq;

namespace QuantResearchAgent.Plugins
{
    /// <summary>
    /// Yahoo Finance implementation of IFinancialDataPlugin
    /// </summary>
    public class YahooFinanceDataPlugin : IFinancialDataPlugin
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        
        public YahooFinanceDataPlugin(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }


        /// <summary>
        /// Fetches security info for a list of tickers (from AI analysis or user input).
        /// </summary>
        public async Task<List<SecurityInfo>> GetSecuritiesForTickersAsync(IEnumerable<string> tickers, int maxResults = 10)
        {
            var validTickers = tickers.Where(IsValidTicker).ToList();
            if (!validTickers.Any()) return new List<SecurityInfo>();
            
            var baseUrl = _configuration["NewsApi:BaseUrl"] ?? "http://127.0.0.1:5000";
            var url = $"{baseUrl}/securities?tickers={string.Join(",", validTickers)}";
            var response = await _httpClient.GetStringAsync(url);
            var results = JsonSerializer.Deserialize<List<SecurityInfo>>(response) ?? new List<SecurityInfo>();
            return results.Take(maxResults).ToList();
        }

        // Legacy method for compatibility (calls new method with default tickers)
        public async Task<List<SecurityInfo>> GetSecuritiesAsync(string regionOrSector, int maxResults = 10)
        {
            var defaultTickers = new[] { "AAPL", "MSFT", "GOOGL" };
            return await GetSecuritiesForTickersAsync(defaultTickers, maxResults);
        }

        /// <summary>
        /// Helper to extract tickers from AI output (simple regex for uppercase, 1-5 chars, or known commodities)
        /// </summary>
        public static IEnumerable<string> ExtractTickersFromText(string text)
        {
            var tickers = new HashSet<string>();
            var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\b[A-Z]{1,5}\b");
            foreach (System.Text.RegularExpressions.Match m in matches)
                tickers.Add(m.Value);
            // Add common commodities if mentioned
            var commodities = new[] { "GOLD", "SILVER", "OIL", "BTC", "ETH" };
            foreach (var c in commodities)
                if (text.ToUpper().Contains(c)) tickers.Add(c);
            return tickers;
        }

        /// <summary>
        /// Basic ticker validation (alphanumeric, 1-5 chars, not a stopword)
        /// </summary>
        private static bool IsValidTicker(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return false;
            if (t.Length < 1 || t.Length > 6) return false;
            if (!System.Text.RegularExpressions.Regex.IsMatch(t, @"^[A-Z0-9.\-]+$")) return false;
            var stopwords = new[] { "THE", "AND", "FOR", "WITH", "FROM", "THIS", "THAT", "HOW", "WHAT", "WHEN", "WHERE", "WHY" };
            if (stopwords.Contains(t.ToUpper())) return false;
            return true;
        }

        public async Task<List<FinancialNewsItem>> GetNewsAsync(string query, int maxResults = 5)
        {
            // For demo: use the first word in query as ticker, or parse as needed
            var ticker = query.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "AAPL";
            var baseUrl = _configuration["NewsApi:BaseUrl"] ?? "http://127.0.0.1:5000";
            var url = $"{baseUrl}/news?ticker={ticker}";
            var response = await _httpClient.GetStringAsync(url);
            var newsList = JsonSerializer.Deserialize<List<FinancialNewsItem>>(response) ?? new List<FinancialNewsItem>();
            return newsList.Take(maxResults).ToList();
        }
    }
}
