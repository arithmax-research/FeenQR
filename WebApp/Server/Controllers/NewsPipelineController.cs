using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Models;
using QuantResearchAgent.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuantResearchAgent.Controllers
{
    /// <summary>
    /// News Pipeline API - dual source (NewsAPI + yfinance) with newspaper3k scraping
    /// </summary>
    [ApiController]
    [Route("api/news")]
    public class NewsPipelineController : ControllerBase
    {
        private readonly INewsPipelineService _pipelineService;
        private readonly ILogger<NewsPipelineController> _logger;

        public NewsPipelineController(
            INewsPipelineService pipelineService,
            ILogger<NewsPipelineController> logger)
        {
            _pipelineService = pipelineService;
            _logger = logger;
        }

        /// <summary>
        /// Search for news articles from dual pipeline (NewsAPI + yfinance)
        /// Articles are scraped asynchronously and cached locally
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<PipelineSearchResult>> SearchNews(
            [FromQuery] string? ticker,
            [FromQuery] string? symbol = null,
            [FromQuery] string sourceFilter = "All",
            [FromQuery] int limit = 25)
        {
            var resolvedTicker = !string.IsNullOrWhiteSpace(ticker) ? ticker : symbol;

            if (string.IsNullOrWhiteSpace(resolvedTicker))
                return BadRequest("Ticker is required");

            try
            {
                _logger.LogInformation($"[API] News search: {resolvedTicker}, source: {sourceFilter}, limit: {limit}");
                var result = await _pipelineService.SearchNewsAsync(resolvedTicker, sourceFilter, limit);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching for {resolvedTicker}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get cached articles for a ticker (without re-fetching)
        /// </summary>
        [HttpGet("cached/{ticker}")]
        public async Task<ActionResult<List<PipelineArticle>>> GetCachedArticles(string ticker)
        {
            try
            {
                var articles = await _pipelineService.GetStoredArticlesAsync(ticker);
                return Ok(articles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving cached articles for {ticker}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Clear cached articles for a ticker
        /// </summary>
        [HttpDelete("cached/{ticker}")]
        public async Task<ActionResult> ClearCache(string ticker)
        {
            try
            {
                await _pipelineService.ClearArticlesAsync(ticker);
                return Ok(new { message = $"Cache cleared for {ticker}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error clearing cache for {ticker}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Scrape a single article URL and return full content
        /// (Used when user clicks on an article)
        /// </summary>
        [HttpPost("scrape")]
        public async Task<ActionResult<object>> ScrapeArticle([FromBody] ScrapeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
                return BadRequest("URL is required");

            try
            {
                var content = await _pipelineService.ScrapeArticleAsync(request.Url);
                return Ok(new { url = request.Url, content });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error scraping {request.Url}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Analyze sentiment of articles (used by Market Sentiment and Symbol Sentiment)
        /// </summary>
        [HttpPost("sentiment")]
        public async Task<ActionResult<object>> AnalyzeSentiment([FromBody] BatchSentimentRequest request)
        {
            var articleCount = request.Articles?.Count ?? 0;
            if (string.IsNullOrWhiteSpace(request.Symbol) || articleCount == 0)
                return BadRequest("Symbol and articles are required");

            try
            {
                _logger.LogInformation($"[API] Sentiment analysis: {request.Symbol}, articles: {articleCount}");
                // Sentiment analysis would be implemented in NewsSentimentAnalysisService
                // For now, return placeholder
                return Ok(new { message = "Sentiment analysis endpoint - implement with LLM service" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error analyzing sentiment for {request.Symbol}");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    public class ScrapeRequest
    {
        public string Url { get; set; } = string.Empty;
    }
}
