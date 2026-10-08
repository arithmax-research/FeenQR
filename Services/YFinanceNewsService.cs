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
        private readonly AchestService? _achest;
        private const string BaseUrl = "https://query1.finance.yahoo.com/v1/finance/search";

        public YFinanceNewsService(HttpClient httpClient, ILogger<YFinanceNewsService> logger, AchestService? achest = null)
        {
            _httpClient = httpClient;
            _logger = logger;
            _achest = achest;
            
            // Set user agent to avoid blocking
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }

        /// <summary>
        /// Get news for a specific symbol using Yahoo Finance
        /// </summary>
        public async Task<List<YFinanceNewsItem>> GetNewsAsync(string symbol, int limit = 10)
        {
            // Preferred source: Arithmax Chest (unified news aggregation).
            if (_achest != null)
            {
                try
                {
                    var news = await _achest.GetNewsAsync(symbol);
                    var mapped = MapAchestNews(news, limit);
                    if (mapped.Count > 0)
                    {
                        return mapped;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "achest news fallback for {Symbol}", symbol);
                }
            }

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
            // Preferred source: Arithmax Chest market-wide news.
            if (_achest != null)
            {
                try
                {
                    var news = await _achest.GetMarketNewsAsync();
                    var mapped = MapAchestNews(news, limit);
                    if (mapped.Count > 0)
                    {
                        return mapped;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "achest market news fallback");
                }
            }

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

        /// <summary>Maps achest Eulerpool news records onto YFinance news items.</summary>
        private List<YFinanceNewsItem> MapAchestNews(JsonElement? data, int limit)
        {
            var items = new List<YFinanceNewsItem>();
            if (!data.HasValue || data.Value.ValueKind != JsonValueKind.Array)
            {
                return items;
            }

            foreach (var n in data.Value.EnumerateArray())
            {
                items.Add(new YFinanceNewsItem
                {
                    Id = n.TryGetProperty("id", out var id) ? id.ToString() : Guid.NewGuid().ToString(),
                    Title = n.TryGetProperty("headline", out var h) ? h.GetString() ?? string.Empty : string.Empty,
                    Summary = n.TryGetProperty("summary", out var s) ? s.GetString() ?? string.Empty : string.Empty,
                    Publisher = n.TryGetProperty("source", out var src) ? src.GetString() ?? string.Empty : string.Empty,
                    Link = n.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty,
                    ProviderPublishTime = ParseUnix(n)
                });

                if (items.Count >= limit) break;
            }

            return items;
        }

        private static long ParseUnix(JsonElement n)
        {
            if (!n.TryGetProperty("datetime", out var d)) return 0;
            return d.ValueKind switch
            {
                JsonValueKind.Number => (long)d.GetDouble(),
                JsonValueKind.String when long.TryParse(d.GetString(), out var v) => v,
                _ => 0
            };
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
