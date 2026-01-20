using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsController : ControllerBase
{
    private readonly AlphaVantageService _alphaVantageService;
    private readonly PolygonService? _polygonService;
    private readonly ILogger<NewsController> _logger;

    public NewsController(
        AlphaVantageService alphaVantageService,
        ILogger<NewsController> logger,
        IServiceProvider serviceProvider)
    {
        _alphaVantageService = alphaVantageService;
        _logger = logger;
        
        // Try to get PolygonService if available
        _polygonService = serviceProvider.GetService<PolygonService>();
    }

    [HttpGet]
    public async Task<IActionResult> GetNews([FromQuery] string? symbol = null, [FromQuery] int limit = 20)
    {
        try
        {
            var newsList = new List<NewsArticle>();

            // Skip Alpha Vantage for now (needs GetNewsAsync implementation)
            _logger.LogInformation("Alpha Vantage news integration pending");

            // Get news from Polygon if available
            if (_polygonService != null && !string.IsNullOrEmpty(symbol))
            {
                try
                {
                    var polygonNews = await _polygonService.GetNewsAsync(symbol, limit);
                    if (polygonNews != null && polygonNews.Any())
                    {
                        foreach (var item in polygonNews)
                        {
                            newsList.Add(new NewsArticle
                            {
                                Title = item.Title ?? "No title",
                                Summary = item.Description ?? "",
                                Source = item.Publisher?.Name ?? "Polygon",
                                Url = item.ArticleUrl ?? "#",
                                PublishedAt = item.PublishedUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                                Sentiment = "neutral",
                                Symbols = item.Tickers ?? new List<string>()
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch news from Polygon");
                }
            }

            // Sort by published date (newest first)
            newsList = newsList
                .OrderByDescending(n => n.PublishedAt)
                .Take(limit)
                .ToList();

            if (newsList.Count == 0)
            {
                _logger.LogError("No news data available from news service");
                return StatusCode(503, new { error = "News services unavailable. Ensure news API keys are configured." });
            }

            _logger.LogInformation($"Fetched {newsList.Count} news articles");
            return Ok(newsList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news");
            return StatusCode(500, new { error = $"Failed to fetch news: {ex.Message}" });
        }
    }
}

public class NewsArticle
{
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Source { get; set; } = "";
    public string Url { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public string Sentiment { get; set; } = "neutral";
    public List<string> Symbols { get; set; } = new();
}
