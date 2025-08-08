using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Yahoo Finance news service for live financial news
    /// </summary>
    public class YFinanceNewsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<YFinanceNewsService> _logger;
        private const string BaseUrl = "https://query1.finance.yahoo.com/v1/finance/search";

        public YFinanceNewsService(HttpClient httpClient, ILogger<YFinanceNewsService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            // Set user agent to avoid blocking
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }

        /// <summary>
        /// Get news for a specific symbol using Yahoo Finance
        /// </summary>
        public async Task<List<YFinanceNewsItem>> GetNewsAsync(string symbol, int limit = 10)
        {
            try
            {
                var encodedSymbol = HttpUtility.UrlEncode(symbol);
                var url = $"{BaseUrl}?q={encodedSymbol}&quotesCount=0&newsCount={limit}";
                
                _logger.LogInformation($"Fetching Yahoo Finance news for {symbol}");
                
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<YFinanceSearchResponse>(response);
                
                return result?.News ?? new List<YFinanceNewsItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Yahoo Finance news for {Symbol}", symbol);
                return new List<YFinanceNewsItem>();
            }
        }

        /// <summary>
        /// Get general market news (top stories)
        /// </summary>
        public async Task<List<YFinanceNewsItem>> GetMarketNewsAsync(int limit = 10)
        {
            try
            {
                // Get news for major market indices
                var marketSymbols = new[] { "SPY", "QQQ", "DIA", "^GSPC", "^IXIC", "^DJI" };
                var allNews = new List<YFinanceNewsItem>();
                
                foreach (var symbol in marketSymbols.Take(2)) // Limit to avoid rate limiting
                {
                    var news = await GetNewsAsync(symbol, limit / 2);
                    allNews.AddRange(news);
                    
                    // Small delay to be respectful
                    await Task.Delay(500);
                }
                
                // Remove duplicates and return top stories
                var uniqueNews = allNews
                    .GroupBy(n => n.Title)
                    .Select(g => g.First())
                    .OrderByDescending(n => n.ProviderPublishTime)
                    .Take(limit)
                    .ToList();
                
                return uniqueNews;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting market news");
                return new List<YFinanceNewsItem>();
            }
        }

        /// <summary>
        /// Search for news by keyword
        /// </summary>
        public async Task<List<YFinanceNewsItem>> SearchNewsAsync(string keyword, int limit = 10)
        {
            try
            {
                var encodedKeyword = HttpUtility.UrlEncode(keyword);
                var url = $"{BaseUrl}?q={encodedKeyword}&quotesCount=0&newsCount={limit}";
                
                _logger.LogInformation($"Searching Yahoo Finance news for keyword: {keyword}");
                
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<YFinanceSearchResponse>(response);
                
                return result?.News ?? new List<YFinanceNewsItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Yahoo Finance news for keyword {Keyword}", keyword);
                return new List<YFinanceNewsItem>();
            }
        }
    }

    // Yahoo Finance data models
    public class YFinanceSearchResponse
    {
        [JsonPropertyName("news")]
        public List<YFinanceNewsItem>? News { get; set; }
    }

    public class YFinanceNewsItem
    {
        [JsonPropertyName("uuid")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;
        
        [JsonPropertyName("publisher")]
        public string Publisher { get; set; } = string.Empty;
        
        [JsonPropertyName("link")]
        public string Link { get; set; } = string.Empty;
        
        [JsonPropertyName("providerPublishTime")]
        public long ProviderPublishTime { get; set; }
        
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonPropertyName("thumbnail")]
        public YFinanceThumbnail? Thumbnail { get; set; }
        
        [JsonPropertyName("relatedTickers")]
        public List<string>? RelatedTickers { get; set; }
        
        public DateTime PublishedDate => DateTimeOffset.FromUnixTimeSeconds(ProviderPublishTime).DateTime;
    }

    public class YFinanceThumbnail
    {
        [JsonPropertyName("resolutions")]
        public List<YFinanceResolution>? Resolutions { get; set; }
    }

    public class YFinanceResolution
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
        
        [JsonPropertyName("width")]
        public int Width { get; set; }
        
        [JsonPropertyName("height")]
        public int Height { get; set; }
    }
}
