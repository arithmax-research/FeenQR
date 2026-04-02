using System.Globalization;
using System.Net;
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
        _baseUrl = config.BaseUrl.TrimEnd('/');
        _defaultPageSize = config.DefaultPageSize;
        _language = config.Language;
    }

    /// <summary>
    /// Fetch news for a symbol from NewsAPI.
    /// </summary>
    public Task<List<NewsItem>> GetNewsAsync(string symbol, int limit = 20)
    {
        return GetEverythingAsync(symbol, limit, sortBy: "publishedAt");
    }

    /// <summary>
    /// Fetch articles from the Everything endpoint with explicit filters.
    /// </summary>
    public async Task<List<NewsItem>> GetEverythingAsync(
        string? query = null,
        int limit = 20,
        DateTime? from = null,
        DateTime? to = null,
        string sortBy = "publishedAt",
        string? language = null,
        string? sources = null,
        string? domains = null,
        string? excludeDomains = null,
        string? searchIn = null)
    {
        if (string.IsNullOrWhiteSpace(query)
            && !from.HasValue
            && !to.HasValue
            && string.IsNullOrWhiteSpace(sources)
            && string.IsNullOrWhiteSpace(domains)
            && string.IsNullOrWhiteSpace(excludeDomains)
            && string.IsNullOrWhiteSpace(searchIn))
        {
            _logger.LogWarning("NewsAPI Everything request skipped because no filters were provided");
            return new List<NewsItem>();
        }

        var url = BuildEverythingUrl(query, limit, from, to, sortBy, language, sources, domains, excludeDomains, searchIn);
        var json = await SendRequestAsync(url, $"everything:{query}");
        return string.IsNullOrWhiteSpace(json) ? new List<NewsItem>() : ParseArticles(json);
    }

    /// <summary>
    /// Fetch articles from the Top Headlines endpoint.
    /// </summary>
    public async Task<List<NewsItem>> GetTopHeadlinesAsync(
        int limit = 20,
        string? country = null,
        string? category = null,
        string? sources = null,
        string? query = null,
        string? language = null)
    {
        if (string.IsNullOrWhiteSpace(country) && string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(sources) && string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("NewsAPI Top Headlines request skipped because no filters were provided");
            return new List<NewsItem>();
        }

        var url = BuildTopHeadlinesUrl(limit, country, category, sources, query, language);
        var json = await SendRequestAsync(url, "top-headlines");
        return string.IsNullOrWhiteSpace(json) ? new List<NewsItem>() : ParseArticles(json);
    }

    /// <summary>
    /// Fetch the available sources for the top-headlines endpoint.
    /// </summary>
    public async Task<List<NewsApiSourceInfo>> GetSourcesAsync(
        string? category = null,
        string? language = null,
        string? country = null)
    {
        var url = BuildSourcesUrl(category, language, country);
        var json = await SendRequestAsync(url, "sources");
        return string.IsNullOrWhiteSpace(json) ? new List<NewsApiSourceInfo>() : ParseSources(json);
    }

    private async Task<string?> SendRequestAsync(string url, string context)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("NewsAPI request skipped for {Context} because the API key is missing", context);
            return null;
        }

        var startTime = DateTime.UtcNow;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Api-Key", _apiKey);
            request.Headers.Add("User-Agent", "QuantResearchAgent/1.0 (Financial Research Application)");

            _logger.LogInformation("NewsAPI request started for {Context}", context);

            using var response = await _httpClient.SendAsync(request);
            var elapsed = DateTime.UtcNow - startTime;

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("NewsAPI rate limit exceeded for {Context}. Response time: {ResponseTime}ms", context, elapsed.TotalMilliseconds);
                return null;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogError("NewsAPI authentication failed for {Context}", context);
                return null;
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("NewsAPI bad request for {Context}. Error: {Error}. Response time: {ResponseTime}ms", context, errorContent, elapsed.TotalMilliseconds);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NewsAPI request failed for {Context}. Status: {Status}. Response time: {ResponseTime}ms", context, response.StatusCode, elapsed.TotalMilliseconds);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("NewsAPI request completed for {Context} in {ResponseTime}ms", context, elapsed.TotalMilliseconds);
            return json;
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "NewsAPI request failed for {Context}. Response time: {ResponseTime}ms", context, elapsed.TotalMilliseconds);
            return null;
        }
    }

    private string BuildEverythingUrl(
        string? query,
        int limit,
        DateTime? from,
        DateTime? to,
        string sortBy,
        string? language,
        string? sources,
        string? domains,
        string? excludeDomains,
        string? searchIn)
    {
        var pageSize = Math.Clamp(limit, 1, Math.Max(1, _defaultPageSize));
        var parameters = new List<string>
        {
            $"language={Escape(language ?? _language)}",
            $"sortBy={Escape(sortBy)}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            parameters.Add($"q={Escape(query)}");
        }

        if (from.HasValue)
        {
            parameters.Add($"from={Escape(FormatDate(from.Value))}");
        }

        if (to.HasValue)
        {
            parameters.Add($"to={Escape(FormatDate(to.Value))}");
        }

        if (!string.IsNullOrWhiteSpace(sources))
        {
            parameters.Add($"sources={Escape(sources)}");
        }

        if (!string.IsNullOrWhiteSpace(domains))
        {
            parameters.Add($"domains={Escape(domains)}");
        }

        if (!string.IsNullOrWhiteSpace(excludeDomains))
        {
            parameters.Add($"excludeDomains={Escape(excludeDomains)}");
        }

        if (!string.IsNullOrWhiteSpace(searchIn))
        {
            parameters.Add($"searchIn={Escape(searchIn)}");
        }

        return $"{_baseUrl}/everything?{string.Join("&", parameters)}";
    }

    private string BuildTopHeadlinesUrl(
        int limit,
        string? country,
        string? category,
        string? sources,
        string? query,
        string? language)
    {
        var pageSize = Math.Clamp(limit, 1, Math.Max(1, _defaultPageSize));
        var parameters = new List<string>
        {
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrWhiteSpace(country))
        {
            parameters.Add($"country={Escape(country)}");
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            parameters.Add($"category={Escape(category)}");
        }

        if (!string.IsNullOrWhiteSpace(sources))
        {
            parameters.Add($"sources={Escape(sources)}");
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            parameters.Add($"q={Escape(query)}");
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            parameters.Add($"language={Escape(language)}");
        }

        return $"{_baseUrl}/top-headlines?{string.Join("&", parameters)}";
    }

    private string BuildSourcesUrl(string? category, string? language, string? country)
    {
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(category))
        {
            parameters.Add($"category={Escape(category)}");
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            parameters.Add($"language={Escape(language)}");
        }

        if (!string.IsNullOrWhiteSpace(country))
        {
            parameters.Add($"country={Escape(country)}");
        }

        var queryString = parameters.Count == 0 ? string.Empty : $"?{string.Join("&", parameters)}";
        return $"{_baseUrl}/top-headlines/sources{queryString}";
    }

    private List<NewsItem> ParseArticles(string json)
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
                    var publisher = article.TryGetProperty("source", out var source) && source.TryGetProperty("name", out var sourceName)
                        ? sourceName.GetString() ?? string.Empty
                        : string.Empty;

                    var newsItem = new NewsItem
                    {
                        Title = article.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
                        Summary = article.TryGetProperty("description", out var description) ? description.GetString() ?? string.Empty : string.Empty,
                        Link = article.TryGetProperty("url", out var url) ? url.GetString() ?? string.Empty : string.Empty,
                        ImageUrl = article.TryGetProperty("urlToImage", out var imageUrl) ? imageUrl.GetString() : null,
                        PublishedDate = article.TryGetProperty("publishedAt", out var publishedAt) && DateTime.TryParse(publishedAt.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
                            ? date
                            : DateTime.UtcNow,
                        Publisher = publisher,
                        Source = "NewsAPI"
                    };

                    if (article.TryGetProperty("author", out var author) && !string.IsNullOrWhiteSpace(author.GetString()))
                    {
                        var authorName = author.GetString() ?? string.Empty;
                        newsItem.Publisher = string.IsNullOrEmpty(newsItem.Publisher)
                            ? authorName
                            : $"{newsItem.Publisher} - {authorName}";
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

    private List<NewsApiSourceInfo> ParseSources(string json)
    {
        var sources = new List<NewsApiSourceInfo>();

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("sources", out var sourceArray))
            {
                _logger.LogWarning("NewsAPI sources response missing 'sources' property");
                return sources;
            }

            foreach (var source in sourceArray.EnumerateArray())
            {
                sources.Add(new NewsApiSourceInfo
                {
                    Id = GetString(source, "id"),
                    Name = GetString(source, "name"),
                    Description = GetString(source, "description"),
                    Url = GetString(source, "url"),
                    Category = GetString(source, "category"),
                    Language = GetString(source, "language"),
                    Country = GetString(source, "country")
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse NewsAPI sources JSON");
        }

        return sources;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private static string FormatDate(DateTime value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
