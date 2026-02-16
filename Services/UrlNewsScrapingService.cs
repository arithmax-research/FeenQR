using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Service for scraping news articles from specific URLs (CNBC, Bloomberg, Reuters)
    /// </summary>
    public class UrlNewsScrapingService
    {
        private readonly ILogger<UrlNewsScrapingService> _logger;
        private readonly DeepSeekService _deepSeekService;
        private readonly string _pythonScriptPath;

        public UrlNewsScrapingService(
            ILogger<UrlNewsScrapingService> logger,
            DeepSeekService deepSeekService)
        {
            _logger = logger;
            _deepSeekService = deepSeekService;
            
            // Use project root Scripts directory
            var projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../.."));
            _pythonScriptPath = Path.Combine(projectRoot, "Scripts", "url_news_scraper.py");
        }

        /// <summary>
        /// Scrape a news article from a given URL and provide DeepSeek analytics
        /// </summary>
        public async Task<NewsScraperResult> ScrapeAndAnalyzeUrlAsync(string url)
        {
            try
            {
                _logger.LogInformation($"Scraping news from URL: {url}");

                // Validate URL
                if (!IsValidNewsUrl(url))
                {
                    return new NewsScraperResult
                    {
                        Success = false,
                        Error = "Invalid or unsupported URL. Please provide a valid CNBC, Bloomberg, or Reuters news URL."
                    };
                }

                // Scrape the article
                var article = await ScrapeUrlAsync(url);
                
                if (article == null)
                {
                    return new NewsScraperResult
                    {
                        Success = false,
                        Error = "Failed to scrape article content. The URL may be invalid or the website structure may have changed."
                    };
                }

                // Generate DeepSeek analytics
                var analytics = await GenerateAnalyticsAsync(article);

                return new NewsScraperResult
                {
                    Success = true,
                    Article = article,
                    Analytics = analytics
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to scrape and analyze URL: {url}");
                return new NewsScraperResult
                {
                    Success = false,
                    Error = $"Error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Scrape multiple URLs in batch
        /// </summary>
        public async Task<List<NewsScraperResult>> ScrapeAndAnalyzeMultipleUrlsAsync(List<string> urls)
        {
            var results = new List<NewsScraperResult>();

            foreach (var url in urls)
            {
                var result = await ScrapeAndAnalyzeUrlAsync(url);
                results.Add(result);
                
                // Add a small delay to avoid overwhelming the servers
                await Task.Delay(1000);
            }

            return results;
        }

        private bool IsValidNewsUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                var uri = new Uri(url);
                var host = uri.Host.ToLower();
                
                return host.Contains("cnbc.com") || 
                       host.Contains("bloomberg.com") || 
                       host.Contains("reuters.com") ||
                       host.Contains("yahoo.com") ||
                       host.Contains("marketwatch.com") ||
                       host.Contains("wsj.com") ||
                       host.Contains("ft.com");
            }
            catch
            {
                return false;
            }
        }

        private async Task<ScrapedNewsArticle?> ScrapeUrlAsync(string url)
        {
            try
            {
                // Ensure the Python script exists
                if (!File.Exists(_pythonScriptPath))
                {
                    _logger.LogWarning($"Python script not found at {_pythonScriptPath}");
                    return null;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = $"\"{_pythonScriptPath}\" \"{url}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                if (!string.IsNullOrWhiteSpace(error))
                {
                    _logger.LogInformation($"Python scraper output: {error}");
                }

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"Python script failed with exit code {process.ExitCode}");
                    return null;
                }

                if (string.IsNullOrWhiteSpace(output))
                {
                    _logger.LogWarning("Python script returned empty output");
                    return null;
                }

                // Parse JSON output
                var article = JsonSerializer.Deserialize<ScrapedNewsArticle>(output);
                return article;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Python scraper");
                return null;
            }
        }

        private async Task<NewsAnalytics> GenerateAnalyticsAsync(ScrapedNewsArticle article)
        {
            try
            {
                // Create a comprehensive prompt for DeepSeek
                var prompt = $@"Analyze the following news article and provide detailed analytics:

Title: {article.Title}
Source: {article.Source}
Published: {article.PublishedDate}
Author: {article.Author}

Content:
{article.Content}

Please provide a comprehensive analysis including:
1. **Summary**: A concise 2-3 sentence summary of the main points
2. **Key Topics**: Identify 3-5 main topics or themes 
3. **Sentiment Analysis**: Overall sentiment (Positive/Negative/Neutral) with confidence score
4. **Market Impact**: Potential impact on financial markets (if applicable)
5. **Key Entities**: Important companies, people, or organizations mentioned
6. **Actionable Insights**: 2-3 key takeaways or actionable insights
7. **Related Sectors**: Financial sectors or industries most impacted

Format your response as a structured analysis.";

                var analyticsText = await _deepSeekService.GetChatCompletionAsync(prompt);

                return new NewsAnalytics
                {
                    Summary = ExtractSection(analyticsText, "Summary") ?? "Analysis completed successfully",
                    KeyTopics = ExtractListSection(analyticsText, "Key Topics"),
                    Sentiment = ExtractSection(analyticsText, "Sentiment Analysis") ?? "Neutral",
                    MarketImpact = ExtractSection(analyticsText, "Market Impact") ?? "Not specified",
                    KeyEntities = ExtractListSection(analyticsText, "Key Entities"),
                    ActionableInsights = ExtractListSection(analyticsText, "Actionable Insights"),
                    RelatedSectors = ExtractListSection(analyticsText, "Related Sectors"),
                    FullAnalysis = analyticsText,
                    AnalyzedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate analytics");
                return new NewsAnalytics
                {
                    Summary = "Failed to generate analytics",
                    FullAnalysis = $"Error: {ex.Message}",
                    AnalyzedAt = DateTime.UtcNow
                };
            }
        }

        private string? ExtractSection(string text, string sectionName)
        {
            try
            {
                var pattern = $@"\*\*{sectionName}\*\*:?\s*(.+?)(?=\n\*\*|\n\n|$)";
                var match = System.Text.RegularExpressions.Regex.Match(text, pattern, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | 
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }

                // Try alternative format
                var lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains(sectionName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 < lines.Length)
                        {
                            return lines[i + 1].Trim();
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private List<string> ExtractListSection(string text, string sectionName)
        {
            try
            {
                var items = new List<string>();
                var lines = text.Split('\n');
                bool inSection = false;

                foreach (var line in lines)
                {
                    if (line.Contains(sectionName, StringComparison.OrdinalIgnoreCase))
                    {
                        inSection = true;
                        continue;
                    }

                    if (inSection)
                    {
                        if (line.StartsWith("**") || string.IsNullOrWhiteSpace(line))
                        {
                            if (items.Count > 0)
                                break;
                        }
                        else if (line.Trim().StartsWith("-") || line.Trim().StartsWith("•") || 
                                 line.Trim().StartsWith("*") || char.IsDigit(line.Trim().FirstOrDefault()))
                        {
                            var cleaned = line.Trim()
                                .TrimStart('-', '•', '*', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', ')')
                                .Trim();
                            if (!string.IsNullOrWhiteSpace(cleaned))
                            {
                                items.Add(cleaned);
                            }
                        }
                    }
                }

                return items.Count > 0 ? items : new List<string> { "Not specified" };
            }
            catch
            {
                return new List<string> { "Not specified" };
            }
        }
    }

    // Data models
    public class ScrapedNewsArticle
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string PublishedDate { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class NewsAnalytics
    {
        public string Summary { get; set; } = string.Empty;
        public List<string> KeyTopics { get; set; } = new();
        public string Sentiment { get; set; } = string.Empty;
        public string MarketImpact { get; set; } = string.Empty;
        public List<string> KeyEntities { get; set; } = new();
        public List<string> ActionableInsights { get; set; } = new();
        public List<string> RelatedSectors { get; set; } = new();
        public string FullAnalysis { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; }
    }

    public class NewsScraperResult
    {
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
        public ScrapedNewsArticle? Article { get; set; }
        public NewsAnalytics? Analytics { get; set; }
    }
}
