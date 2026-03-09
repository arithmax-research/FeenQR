using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services;

/// <summary>
/// Client for fetching news from NewsAPI.org
/// </summary>
public class NewsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NewsApiClient> _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly int _defaultPageSize;
    private readonly string _language;

    public NewsApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<NewsApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var config = configuration.GetSection("NewsAPI").Get<NewsApiConfig>() ?? new NewsApiConfig();
        _apiKey = config.ApiKey;
        _baseUrl = config.BaseUrl;
        _defaultPageSize = config.DefaultPageSize;
        _language = config.Language;
    }

    /// <summary>
    /// Fetch news for a symbol from NewsAPI
    /// </summary>
    /// <param name="symbol">Stock symbol to search for</param>
    /// <param name="limit">Maximum number of articles to return (default: 20)</param>
    /// <returns>List of news items</returns>
    public async Task<List<NewsItem>> GetNewsAsync(string symbol, int limit = 20)
    {
        var startTime = DateTime.UtcNow;
        
        // Validate symbol parameter
        if (string.IsNullOrWhiteSpace(symbol))
        {
            _logger.LogWarning("NewsAPI: Symbol parameter is null or empty, skipping request");
            return new List<NewsItem>();
        }
        
        try
        {
            var url = BuildQueryUrl(symbol, limit);
            
            // Add API key to request header
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Api-Key", _apiKey);

            _logger.LogInformation("NewsAPI request: symbol={Symbol}, limit={Limit}", symbol, limit);

            var response = await _httpClient.SendAsync(request);
            var responseTime = DateTime.UtcNow - startTime;

            // Handle rate limit error (HTTP 429)
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("NewsAPI rate limit exceeded for symbol {Symbol}. Response time: {ResponseTime}ms", 
                    symbol, responseTime.TotalMilliseconds);
                return new List<NewsItem>();
            }

            // Handle authentication error (HTTP 401)
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError("NewsAPI authentication failed for symbol {Symbol}. Invalid API key.", symbol);
                return new List<NewsItem>();
            }

            // Handle bad request (HTTP 400) - invalid query
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("NewsAPI bad request for symbol {Symbol}. Error: {Error}. Response time: {ResponseTime}ms", 
                    symbol, errorContent, responseTime.TotalMilliseconds);
                return new List<NewsItem>();
            }

            // Handle other errors gracefully
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NewsAPI request failed for symbol {Symbol}. Status: {Status}. Response time: {ResponseTime}ms", 
                    symbol, response.StatusCode, responseTime.TotalMilliseconds);
                return new List<NewsItem>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var newsItems = ParseResponse(json);

            _logger.LogInformation("NewsAPI response: symbol={Symbol}, resultCount={Count}, responseTime={ResponseTime}ms",
                symbol, newsItems.Count, responseTime.TotalMilliseconds);

            return newsItems;
        }
        catch (UnauthorizedAccessException)
        {
            throw; // Re-throw authentication errors
        }
        catch (Exception ex)
        {
            var responseTime = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "NewsAPI request failed for symbol {Symbol}. Response time: {ResponseTime}ms", 
                symbol, responseTime.TotalMilliseconds);
            return new List<NewsItem>();
        }
    }

    /// <summary>
    /// Build query URL for NewsAPI /everything endpoint
    /// </summary>
    private string BuildQueryUrl(string symbol, int limit)
    {
        var pageSize = Math.Min(limit, _defaultPageSize);
        return $"{_baseUrl}/everything?q={Uri.EscapeDataString(symbol)}&language={_language}&sortBy=publishedAt&pageSize={pageSize}";
    }

    /// <summary>
    /// Parse NewsAPI JSON response to NewsItem model
    /// </summary>
    private List<NewsItem> ParseResponse(string json)
    {
        var newsItems = new List<NewsItem>();

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("articles", out var articles))
            {
                _logger.LogWarning("NewsAPI response missing 'articles' property");
                return newsItems;
            }

            foreach (var article in articles.EnumerateArray())
            {
                try
                {
                    var newsItem = new NewsItem
                    {
                        Title = article.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
                        Summary = article.TryGetProperty("description", out var desc) ? desc.GetString() ?? string.Empty : string.Empty,
                        Link = article.TryGetProperty("url", out var url) ? url.GetString() ?? string.Empty : string.Empty,
                        PublishedDate = article.TryGetProperty("publishedAt", out var pubDate) && DateTime.TryParse(pubDate.GetString(), out var date) 
                            ? date 
                            : DateTime.UtcNow,
                        Publisher = article.TryGetProperty("source", out var source) && source.TryGetProperty("name", out var sourceName)
                            ? sourceName.GetString() ?? string.Empty
                            : string.Empty,
                        Source = "NewsAPI"
                    };

                    // Add author if available
                    if (article.TryGetProperty("author", out var author) && !string.IsNullOrEmpty(author.GetString()))
                    {
                        var authorName = author.GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(authorName))
                        {
                            newsItem.Publisher = string.IsNullOrEmpty(newsItem.Publisher) 
                                ? authorName 
                                : $"{newsItem.Publisher} - {authorName}";
                        }
                    }

                    newsItems.Add(newsItem);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse NewsAPI article");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse NewsAPI response JSON");
        }

        return newsItems;
    }
}
