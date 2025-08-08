using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Web;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Finviz news service for financial news scraping
    /// </summary>
    public class FinvizNewsService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FinvizNewsService> _logger;
        private const string BaseUrl = "https://finviz.com";

        public FinvizNewsService(HttpClient httpClient, ILogger<FinvizNewsService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            // Set user agent to avoid blocking
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }

        /// <summary>
        /// Get news for a specific symbol from Finviz
        /// </summary>
        public async Task<List<FinvizNewsItem>> GetNewsAsync(string symbol, int limit = 10)
        {
            try
            {
                var url = $"https://finviz.com/quote.ashx?t={symbol.ToUpper()}";
                _logger.LogInformation($"Fetching Finviz news for {symbol}");
                
                var response = await _httpClient.GetStringAsync(url);
                var newsItems = ParseNewsFromQuotePage(response, limit);
                
                _logger.LogInformation($"Found {newsItems.Count} Finviz news items for {symbol}");
                return newsItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Finviz news for {Symbol}", symbol);
                return new List<FinvizNewsItem>();
            }
        }

        /// <summary>
        /// Get general market news from Finviz
        /// </summary>
        public async Task<List<FinvizNewsItem>> GetMarketNewsAsync(int limit = 15)
        {
            try
            {
                var url = "https://finviz.com/news.ashx";
                _logger.LogInformation("Fetching Finviz market news");
                
                var response = await _httpClient.GetStringAsync(url);
                var newsItems = ParseNewsFromNewsPage(response, limit);
                
                _logger.LogInformation($"Found {newsItems.Count} Finviz market news items");
                return newsItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Finviz market news");
                return new List<FinvizNewsItem>();
            }
        }

        /// <summary>
        /// Search for news by keyword using Finviz
        /// </summary>
        public async Task<List<FinvizNewsItem>> SearchNewsAsync(string keyword, int limit = 10)
        {
            try
            {
                // For keyword search, get market news and filter
                var allNews = await GetMarketNewsAsync(50);
                
                var filteredNews = allNews
                    .Where(n => n.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                               n.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    .Take(limit)
                    .ToList();
                
                _logger.LogInformation($"Found {filteredNews.Count} Finviz news items for keyword: {keyword}");
                return filteredNews;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Finviz news for keyword {Keyword}", keyword);
                return new List<FinvizNewsItem>();
            }
        }

        private List<FinvizNewsItem> ParseNewsFromQuotePage(string html, int limit)
        {
            var newsItems = new List<FinvizNewsItem>();
            
            try
            {
                // More flexible pattern for news items in quote page
                // Look for news tables with different possible structures
                var patterns = new[]
                {
                    // Pattern 1: Standard news table format
                    @"<td[^>]*>(\d{2}-\d{2})<br>(\d{2}:\d{2})</td>[^<]*<td[^>]*><a[^>]*href=[""']([^""']*)[""'][^>]*>([^<]+)</a>",
                    // Pattern 2: Alternative news format
                    @"<tr[^>]*>\s*<td[^>]*>\s*(\d{2}-\d{2})\s*<br>\s*(\d{2}:\d{2})\s*</td>\s*<td[^>]*>\s*<a[^>]*href=[""']([^""']*)[""'][^>]*>([^<]*)</a>",
                    // Pattern 3: Simple date + link format
                    @"(\d{2}-\d{2})[^<]*<a[^>]*href=[""']([^""']*)[""'][^>]*>([^<]+)</a>"
                };

                foreach (var pattern in patterns)
                {
                    var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    
                    foreach (Match match in matches.Take(limit))
                    {
                        if (match.Groups.Count >= 4)
                        {
                            var dateStr = match.Groups[1].Value;
                            var timeStr = match.Groups.Count > 4 ? match.Groups[2].Value : "12:00";
                            var link = match.Groups[match.Groups.Count - 2].Value;
                            var title = match.Groups[match.Groups.Count - 1].Value.Trim();
                            
                            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(link))
                            {
                                // Ensure absolute URL
                                if (link.StartsWith("/"))
                                    link = BaseUrl + link;
                                else if (!link.StartsWith("http"))
                                    link = BaseUrl + "/" + link;
                                
                                var publishedDate = ParseFinvizDateTime(dateStr, timeStr);
                                
                                newsItems.Add(new FinvizNewsItem
                                {
                                    Title = HttpUtility.HtmlDecode(title),
                                    Link = link,
                                    PublishedDate = publishedDate,
                                    Summary = "",
                                    Publisher = "Finviz"
                                });
                            }
                        }
                    }
                    
                    if (newsItems.Count >= limit) break;
                }
                
                // If we still don't have news, try a more generic approach
                if (!newsItems.Any())
                {
                    var genericPattern = @"<a[^>]*href=[""']([^""']*)[""'][^>]*>([^<]{10,})</a>";
                    var matches = Regex.Matches(html, genericPattern, RegexOptions.IgnoreCase);
                    
                    foreach (Match match in matches.Take(limit))
                    {
                        var link = match.Groups[1].Value;
                        var title = match.Groups[2].Value.Trim();
                        
                        // Filter for news-like content
                        if (title.Length > 10 && title.Length < 200 && 
                            (link.Contains("news") || link.Contains("article") || 
                             title.Any(char.IsLower) && title.Any(char.IsUpper)))
                        {
                            if (link.StartsWith("/"))
                                link = BaseUrl + link;
                            else if (!link.StartsWith("http"))
                                continue;
                            
                            newsItems.Add(new FinvizNewsItem
                            {
                                Title = HttpUtility.HtmlDecode(title),
                                Link = link,
                                PublishedDate = DateTime.Now,
                                Summary = "",
                                Publisher = "Finviz"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing news from quote page");
            }
            
            return newsItems.Take(limit).ToList();
        }

        private List<FinvizNewsItem> ParseNewsFromNewsPage(string html, int limit)
        {
            var newsItems = new List<FinvizNewsItem>();
            
            try
            {
                // Multiple patterns to try for the news page
                var patterns = new[]
                {
                    @"<tr[^>]*>\s*<td[^>]*>\s*(\d{2}-\d{2})\s*<br>\s*(\d{2}:\d{2})\s*</td>\s*<td[^>]*>\s*<a[^>]*href=[""']([^""']*)[""'][^>]*>([^<]*)</a>",
                    @"(\d{2}-\d{2})[^<]*<br>[^<]*(\d{2}:\d{2})[^<]*<a[^>]*href=[""']([^""']*)[""'][^>]*>([^<]+)</a>",
                    @"<td[^>]*>(\d{2}-\d{2})<br>(\d{2}:\d{2})</td>[^<]*<td[^>]*><a[^>]*href=[""']([^""']*)[""'][^>]*>([^<]+)</a>"
                };

                foreach (var pattern in patterns)
                {
                    var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    
                    foreach (Match match in matches.Take(limit))
                    {
                        var dateStr = match.Groups[1].Value;
                        var timeStr = match.Groups[2].Value;
                        var link = match.Groups[3].Value;
                        var title = match.Groups[4].Value.Trim();
                        
                        if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(link))
                        {
                            if (link.StartsWith("/"))
                                link = BaseUrl + link;
                            else if (!link.StartsWith("http"))
                                link = BaseUrl + "/" + link;
                            
                            var publishedDate = ParseFinvizDateTime(dateStr, timeStr);
                            
                            newsItems.Add(new FinvizNewsItem
                            {
                                Title = HttpUtility.HtmlDecode(title),
                                Link = link,
                                PublishedDate = publishedDate,
                                Summary = "",
                                Publisher = "Finviz"
                            });
                        }
                    }
                    
                    if (newsItems.Count >= limit) break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing news from news page");
            }
            
            return newsItems.Take(limit).ToList();
        }

        private DateTime ParseFinvizDateTime(string dateStr, string timeStr)
        {
            try
            {
                var currentYear = DateTime.Now.Year;
                var fullDateStr = $"{currentYear}-{dateStr} {timeStr}";
                
                if (DateTime.TryParseExact(fullDateStr, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out var dateTime))
                {
                    return dateTime;
                }
                
                // Fallback: try without time
                if (DateTime.TryParseExact($"{currentYear}-{dateStr}", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
                {
                    return date;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse Finviz datetime: {DateStr} {TimeStr}", dateStr, timeStr);
            }
            
            return DateTime.Now;
        }
    }

    public class FinvizNewsItem
    {
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public DateTime PublishedDate { get; set; }
    }
}
