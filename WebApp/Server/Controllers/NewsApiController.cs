using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsApiController : ControllerBase
{
    private readonly NewsApiClient _newsApiClient;
    private readonly ILogger<NewsApiController> _logger;

    public NewsApiController(NewsApiClient newsApiClient, ILogger<NewsApiController> logger)
    {
        _newsApiClient = newsApiClient;
        _logger = logger;
    }

    [HttpGet("market-pulse")]
    public async Task<ActionResult<NewsApiMarketPulseResponse>> GetMarketPulse(
        [FromQuery] string symbol = "AAPL",
        [FromQuery] string? searchQuery = null,
        [FromQuery] string country = "us",
        [FromQuery] string? countryCategory = null,
        [FromQuery] string? sourceId = null,
        [FromQuery] string? domains = null)
    {
        try
        {
            var normalizedSymbol = NormalizeSymbol(symbol);
            var normalizedSearchQuery = NormalizeSearchQuery(searchQuery);
            var normalizedCountry = NormalizeCountry(country);
            var normalizedCountryCategory = NormalizeCountryCategory(countryCategory);
            var normalizedDomains = NormalizeDomains(domains);

            var sources = await _newsApiClient.GetSourcesAsync(category: "business", language: "en", country: normalizedCountry);
            var selectedSource = SelectSource(sourceId, sources);

            var now = DateTime.UtcNow;
            var yesterday = now.Date.AddDays(-1);

            var yesterdayCoverageTask = _newsApiClient.GetEverythingAsync(
                normalizedSymbol,
                limit: 10,
                from: yesterday,
                to: yesterday,
                sortBy: "popularity",
                language: "en");

            var monthCoverageTask = _newsApiClient.GetEverythingAsync(
                normalizedSymbol,
                limit: 10,
                from: now.AddMonths(-1),
                to: now,
                sortBy: "publishedAt",
                language: "en");

            var countryHeadlinesTask = GetCountryHeadlinesAsync(sources, normalizedCountry, normalizedCountryCategory);

            var sourceHeadlinesTask = !string.IsNullOrWhiteSpace(selectedSource.Id)
                ? _newsApiClient.GetTopHeadlinesAsync(
                    limit: 10,
                    sources: selectedSource.Id,
                    language: "en")
                : Task.FromResult(new List<NewsItem>());

            var searchResultsTask = string.IsNullOrWhiteSpace(normalizedSearchQuery)
                ? Task.FromResult(new List<NewsItem>())
                : _newsApiClient.GetEverythingAsync(
                    query: normalizedSearchQuery,
                    limit: 10,
                    from: now.AddDays(-30),
                    to: now,
                    sortBy: "popularity",
                    language: "en");

            var domainCoverageTask = string.IsNullOrWhiteSpace(normalizedDomains)
                ? Task.FromResult(new List<NewsItem>())
                : _newsApiClient.GetEverythingAsync(
                    query: null,
                    limit: 10,
                    from: now.AddMonths(-6),
                    to: now,
                    sortBy: "publishedAt",
                    language: "en",
                    domains: normalizedDomains);

            await Task.WhenAll(yesterdayCoverageTask, monthCoverageTask, countryHeadlinesTask, sourceHeadlinesTask, domainCoverageTask, searchResultsTask);

            return Ok(new NewsApiMarketPulseResponse
            {
                Symbol = normalizedSymbol,
                SearchQuery = normalizedSearchQuery,
                Country = normalizedCountry,
                SelectedSourceId = string.IsNullOrWhiteSpace(selectedSource.Id) ? null : selectedSource.Id,
                SelectedSourceName = string.IsNullOrWhiteSpace(selectedSource.Name) ? null : selectedSource.Name,
                Domains = normalizedDomains,
                Timestamp = DateTime.UtcNow,
                Sources = sources,
                Tabs = new List<NewsApiMarketPulseTab>
                {
                    new()
                    {
                        Key = "search",
                        Title = string.IsNullOrWhiteSpace(normalizedSearchQuery)
                            ? "Search news"
                            : $"Search results for \"{normalizedSearchQuery}\"",
                        Subtitle = string.IsNullOrWhiteSpace(normalizedSearchQuery)
                            ? "Use the search bar to query NewsAPI's Everything endpoint by keyword."
                            : "Keyword search results from NewsAPI's Everything endpoint, sorted by popularity.",
                        EmptyMessage = string.IsNullOrWhiteSpace(normalizedSearchQuery)
                            ? "Enter a keyword in the search bar and press Search."
                            : $"No articles found for \"{normalizedSearchQuery}\".",
                        Articles = searchResultsTask.Result
                    },
                    new()
                    {
                        Key = "yesterday",
                        Title = $"Yesterday's {normalizedSymbol} coverage",
                        Subtitle = "Articles that mentioned the stock over the last trading day, sorted by popularity.",
                        EmptyMessage = $"No articles found for {normalizedSymbol} yesterday.",
                        Articles = yesterdayCoverageTask.Result
                    },
                    new()
                    {
                        Key = "month",
                        Title = $"{normalizedSymbol} over the last month",
                        Subtitle = "Recent coverage from the last 30 days, newest first.",
                        EmptyMessage = $"No recent articles found for {normalizedSymbol}.",
                        Articles = monthCoverageTask.Result
                    },
                    new()
                    {
                        Key = "country",
                        Title = string.IsNullOrWhiteSpace(normalizedCountryCategory)
                            ? $"Top headlines in {GetCountryLabel(normalizedCountry)}"
                            : $"Top {normalizedCountryCategory} headlines in {GetCountryLabel(normalizedCountry)}",
                        Subtitle = string.IsNullOrWhiteSpace(normalizedCountryCategory)
                            ? "National headlines from NewsAPI's curated sources."
                            : $"National {normalizedCountryCategory} headlines from NewsAPI's curated sources.",
                        EmptyMessage = string.IsNullOrWhiteSpace(normalizedCountryCategory)
                            ? $"No headlines returned for {GetCountryLabel(normalizedCountry)}."
                            : $"No {normalizedCountryCategory} headlines returned for {GetCountryLabel(normalizedCountry)}.",
                        Articles = countryHeadlinesTask.Result
                    },
                    new()
                    {
                        Key = "source",
                        Title = string.IsNullOrWhiteSpace(selectedSource.Name)
                            ? "Top headlines from selected source"
                            : $"Top headlines from {selectedSource.Name}",
                        Subtitle = "A single source selected from the NewsAPI sources directory.",
                        EmptyMessage = "Pick a source to load its headlines.",
                        Articles = sourceHeadlinesTask.Result
                    },
                    new()
                    {
                        Key = "domains",
                        Title = string.IsNullOrWhiteSpace(normalizedDomains)
                            ? "Coverage from selected domains"
                            : $"Coverage from {normalizedDomains}",
                        Subtitle = "Articles from the selected domains over the last six months. This tab does not use the stock symbol.",
                        EmptyMessage = string.IsNullOrWhiteSpace(normalizedDomains)
                            ? "Enter one or more domains to load this tab."
                            : $"No articles found for {normalizedDomains}.",
                        Articles = domainCoverageTask.Result
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build NewsAPI market pulse payload for {Symbol}", symbol);
            return StatusCode(500, new { error = "Failed to load NewsAPI market pulse", details = ex.Message });
        }
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return "AAPL";
        }

        var trimmed = symbol.Trim().ToUpperInvariant();
        if (trimmed.Contains(':'))
        {
            var parts = trimmed.Split(':', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                return parts[1];
            }
        }

        return trimmed;
    }

    private static string NormalizeCountry(string country)
    {
        return string.IsNullOrWhiteSpace(country) ? "us" : country.Trim().ToLowerInvariant();
    }

    private static string NormalizeSearchQuery(string? searchQuery)
    {
        return string.IsNullOrWhiteSpace(searchQuery) ? string.Empty : searchQuery.Trim();
    }

    private static string NormalizeCountryCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return string.Empty;
        }

        var normalized = category.Trim().ToLowerInvariant();
        return normalized switch
        {
            "business" or "entertainment" or "general" or "health" or "science" or "sports" or "technology" => normalized,
            _ => string.Empty
        };
    }

    private static string NormalizeDomains(string? domains)
    {
        if (string.IsNullOrWhiteSpace(domains))
        {
            return string.Empty;
        }

        var normalizedDomains = domains
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Select(domain => domain.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.Join(',', normalizedDomains);
    }

    private async Task<List<NewsItem>> GetCountryHeadlinesAsync(List<NewsApiSourceInfo> sources, string country, string? category)
    {
        var headlines = await _newsApiClient.GetTopHeadlinesAsync(
            limit: 10,
            country: country,
            category: string.IsNullOrWhiteSpace(category) ? null : category,
            language: "en");

        if (headlines.Count > 0)
        {
            return headlines;
        }

        var fallbackSourceIds = sources
            .Where(source => string.Equals(source.Country, country, StringComparison.OrdinalIgnoreCase))
            .Where(source => string.IsNullOrWhiteSpace(category) || string.Equals(source.Category, category, StringComparison.OrdinalIgnoreCase))
            .Select(source => source.Id)
            .Where(sourceId => !string.IsNullOrWhiteSpace(sourceId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

        if (fallbackSourceIds.Length > 0)
        {
            var sourceFallbackHeadlines = await _newsApiClient.GetTopHeadlinesAsync(
                limit: 10,
                sources: string.Join(',', fallbackSourceIds),
                language: "en");

            if (sourceFallbackHeadlines.Count > 0)
            {
                return sourceFallbackHeadlines;
            }
        }

        return await _newsApiClient.GetTopHeadlinesAsync(
            limit: 10,
            country: country,
            language: "en");
    }

    private static NewsApiSourceInfo SelectSource(string? sourceId, List<NewsApiSourceInfo> sources)
    {
        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            var selectedSource = sources.FirstOrDefault(source => string.Equals(source.Id, sourceId, StringComparison.OrdinalIgnoreCase));
            if (selectedSource != null)
            {
                return selectedSource;
            }
        }

        return sources.FirstOrDefault() ?? new NewsApiSourceInfo();
    }

    private static string GetCountryLabel(string countryCode)
    {
        return countryCode.ToLowerInvariant() switch
        {
            "us" => "United States",
            "gb" => "United Kingdom",
            "ca" => "Canada",
            "au" => "Australia",
            "de" => "Germany",
            "fr" => "France",
            "in" => "India",
            "jp" => "Japan",
            "br" => "Brazil",
            "cn" => "China",
            "hk" => "Hong Kong",
            "za" => "South Africa",
            _ => countryCode.ToUpperInvariant()
        };
    }
}