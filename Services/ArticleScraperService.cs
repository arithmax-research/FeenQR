using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Service for scraping full article content from URLs using AngleSharp HTML parser
    /// </summary>
    public class ArticleScraperService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ArticleScraperService> _logger;
        private readonly SemaphoreSlim _rateLimiter;
        private readonly int _maxConcurrency;
        private readonly TimeSpan _timeout;
        private readonly string _userAgent;

        public ArticleScraperService(
            HttpClient httpClient,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<ArticleScraperService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Load configuration
            var config = new ArticleScraperConfig();
            configuration.GetSection("ArticleScraper").Bind(config);

            _maxConcurrency = config.MaxConcurrency;
            _timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
            _userAgent = config.UserAgent;
            _rateLimiter = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

            // Set User-Agent header
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", _userAgent);
            }

            // Note: Timeout should be configured via AddHttpClient in Program.cs
            // Setting it here causes "already started" exception
        }

        /// <summary>
        /// Scrape article content from a single URL
        /// </summary>
        /// <param name="url">Article URL to scrape</param>
        /// <returns>ArticleContent with extracted text or error information</returns>
        public async Task<ArticleContent> ScrapeArticleAsync(string url)
        {
            var startTime = DateTime.UtcNow;
            var result = new ArticleContent
            {
                Url = url,
                Success = false
            };

            try
            {
                await _rateLimiter.WaitAsync();

                try
                {
                    using var cts = new CancellationTokenSource(_timeout);
                    var response = await _httpClient.GetAsync(url, cts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        result.ErrorMessage = $"HTTP {response.StatusCode}";
                        _logger.LogWarning($"Failed to fetch article from {url}: {result.ErrorMessage}");
                        return result;
                    }

                    var html = await response.Content.ReadAsStringAsync(cts.Token);
                    var articleText = ExtractArticleText(html);
                    var cleanedText = CleanText(articleText);

                    result.Content = cleanedText;
                    result.ContentLength = cleanedText.Length;
                    result.Success = true;

                    _logger.LogInformation($"Successfully scraped article from {url}: {result.ContentLength} characters");
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }
            catch (TaskCanceledException)
            {
                result.ErrorMessage = "Request timeout";
                _logger.LogWarning($"Timeout scraping article from {url}");
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, $"Error scraping article from {url}");
            }
            finally
            {
                result.ScrapingDuration = DateTime.UtcNow - startTime;
            }

            return result;
        }

        /// <summary>
        /// Scrape article content from multiple URLs in parallel with concurrency control
        /// </summary>
        /// <param name="urls">List of article URLs to scrape</param>
        /// <returns>List of ArticleContent results</returns>
        public async Task<List<ArticleContent>> ScrapeArticlesAsync(List<string> urls)
        {
            if (urls == null || !urls.Any())
            {
                _logger.LogWarning("ScrapeArticlesAsync called with empty or null URL list");
                return new List<ArticleContent>();
            }

            _logger.LogInformation($"Starting batch scraping of {urls.Count} articles with max concurrency {_maxConcurrency}");
            var startTime = DateTime.UtcNow;

            var results = new List<ArticleContent>();
            var tasks = urls.Select(async url =>
            {
                var articleStartTime = DateTime.UtcNow;
                ArticleContent result;

                try
                {
                    result = await ScrapeArticleAsync(url);

                    // Log each scraping attempt
                    if (result.Success)
                    {
                        _logger.LogInformation(
                            "Article scraped successfully - URL: {Url}, Duration: {Duration}ms, ContentLength: {ContentLength}",
                            url,
                            result.ScrapingDuration.TotalMilliseconds,
                            result.ContentLength);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Article scraping failed - URL: {Url}, Duration: {Duration}ms, Error: {Error}",
                            url,
                            result.ScrapingDuration.TotalMilliseconds,
                            result.ErrorMessage ?? "Unknown error");
                    }
                }
                catch (Exception ex)
                {
                    // Error handling with fallback
                    var duration = DateTime.UtcNow - articleStartTime;
                    _logger.LogError(ex,
                        "Exception during article scraping - URL: {Url}, Duration: {Duration}ms",
                        url,
                        duration.TotalMilliseconds);

                    result = new ArticleContent
                    {
                        Url = url,
                        Success = false,
                        ErrorMessage = ex.Message,
                        ScrapingDuration = duration
                    };
                }

                return result;
            });

            results = (await Task.WhenAll(tasks)).ToList();

            var totalDuration = DateTime.UtcNow - startTime;
            var successCount = results.Count(r => r.Success);
            var failureCount = results.Count - successCount;

            _logger.LogInformation(
                "Batch scraping completed - Total: {Total}, Success: {Success}, Failed: {Failed}, Duration: {Duration}ms",
                results.Count,
                successCount,
                failureCount,
                totalDuration.TotalMilliseconds);

            return results;
        }


        /// <summary>
        /// Extract article text from HTML using AngleSharp parser
        /// Identifies article content using article/main tags and content classes
        /// </summary>
        /// <param name="html">Raw HTML content</param>
        /// <returns>Extracted article text</returns>
        private string ExtractArticleText(string html)
        {
            try
            {
                var context = BrowsingContext.New(Configuration.Default);
                var parser = context.GetService<IHtmlParser>();
                var document = parser?.ParseDocument(html);

                if (document == null)
                {
                    return string.Empty;
                }

                // Try to find article content using common patterns
                var articleCandidates = new List<(AngleSharp.Dom.IElement element, int score)>();

                // Strategy 1: Look for <article> tag
                var articleElements = document.QuerySelectorAll("article");
                foreach (var element in articleElements)
                {
                    var textLength = element.TextContent.Length;
                    articleCandidates.Add((element, textLength));
                }

                // Strategy 2: Look for <main> tag
                var mainElements = document.QuerySelectorAll("main");
                foreach (var element in mainElements)
                {
                    var textLength = element.TextContent.Length;
                    articleCandidates.Add((element, textLength));
                }

                // Strategy 3: Look for common content class names
                var contentSelectors = new[]
                {
                    ".article-content", ".article-body", ".post-content", ".entry-content",
                    ".content", ".story-body", ".article-text", "[class*='article']",
                    "[class*='content']", "[class*='story']"
                };

                foreach (var selector in contentSelectors)
                {
                    var elements = document.QuerySelectorAll(selector);
                    foreach (var element in elements)
                    {
                        var textLength = element.TextContent.Length;
                        articleCandidates.Add((element, textLength));
                    }
                }

                // Select the longest candidate as the primary article
                if (articleCandidates.Any())
                {
                    var bestCandidate = articleCandidates.OrderByDescending(c => c.score).First();
                    return ExtractTextFromElement(bestCandidate.element);
                }

                // Fallback: Extract from body, excluding navigation and ads
                var body = document.Body;
                if (body != null)
                {
                    return ExtractTextFromElement(body);
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing HTML with AngleSharp");
                return string.Empty;
            }
        }

        /// <summary>
        /// Extract text from an HTML element, including paragraphs, headings, and lists
        /// Excludes header, footer, nav, aside, and advertisement elements
        /// </summary>
        /// <param name="element">HTML element to extract text from</param>
        /// <returns>Extracted text with preserved paragraph structure</returns>
        private string ExtractTextFromElement(AngleSharp.Dom.IElement element)
        {
            var sb = new StringBuilder();
            var excludedTags = new HashSet<string> { "HEADER", "FOOTER", "NAV", "ASIDE", "SCRIPT", "STYLE", "NOSCRIPT" };
            var excludedClasses = new[] { "ad", "advertisement", "sidebar", "menu", "navigation" };

            void ProcessNode(AngleSharp.Dom.INode node)
            {
                if (node is AngleSharp.Dom.IElement elem)
                {
                    // Skip excluded tags
                    if (excludedTags.Contains(elem.TagName))
                    {
                        return;
                    }

                    // Skip elements with excluded class names
                    var className = elem.GetAttribute("class") ?? string.Empty;
                    if (excludedClasses.Any(ec => className.Contains(ec, StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }

                    // Extract text from paragraph, heading, and list elements
                    if (elem.TagName == "P" || elem.TagName == "H1" || elem.TagName == "H2" ||
                        elem.TagName == "H3" || elem.TagName == "H4" || elem.TagName == "H5" ||
                        elem.TagName == "H6" || elem.TagName == "LI")
                    {
                        var text = elem.TextContent.Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            sb.AppendLine(text);
                            sb.AppendLine(); // Preserve paragraph structure
                        }
                        return; // Don't process children separately
                    }
                }

                // Process child nodes
                foreach (var child in node.ChildNodes)
                {
                    ProcessNode(child);
                }
            }

            ProcessNode(element);
            return sb.ToString();
        }

        /// <summary>
        /// Clean text by removing HTML tags, scripts, styles, and decoding entities
        /// </summary>
        /// <param name="text">Text to clean</param>
        /// <returns>Cleaned text</returns>
        private string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            // Decode HTML entities
            text = System.Net.WebUtility.HtmlDecode(text);

            // Remove excessive whitespace while preserving paragraph structure
            var lines = text.Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join("\n\n", lines);
        }
    }

    /// <summary>
    /// Result of article scraping operation
    /// </summary>
    public class ArticleContent
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int ContentLength { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan ScrapingDuration { get; set; }
    }
}
