using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantResearchAgent.Models;

namespace QuantResearchAgent.Services
{
    /// <summary>
    /// Thin bridge that invokes the Python dual-news pipeline.
    /// </summary>
    public interface INewsPipelineService
    {
        Task<PipelineSearchResult> SearchNewsAsync(string ticker, string sourceFilter = "All", int limit = 25);
        Task<string> ScrapeArticleAsync(string url);
        Task<List<PipelineArticle>> GetStoredArticlesAsync(string ticker);
        Task ClearArticlesAsync(string ticker);
    }

    public class NewsPipelineService : INewsPipelineService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NewsPipelineService> _logger;
        private readonly NewsPipelineConfig _config;
        private readonly Dictionary<string, PipelineSearchResult> _cache = new();

        public NewsPipelineService(
            HttpClient httpClient,
            ILogger<NewsPipelineService> logger,
            IOptions<NewsPipelineConfig> options)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = options.Value;
        }

        public async Task<PipelineSearchResult> SearchNewsAsync(string ticker, string sourceFilter = "All", int limit = 25)
        {
            var json = await RunPythonPipelineAsync(new[]
            {
                ticker,
                "--source", sourceFilter,
                "--max-articles", limit.ToString(),
                "--word-limit", _config.WordLimitPerArticle.ToString(),
                "--pretty"
            });

            var result = ParseSearchResult(json, ticker, sourceFilter);
            _cache[ticker] = result;
            return result;
        }

        public async Task<string> ScrapeArticleAsync(string url)
        {
            var json = await RunPythonPipelineAsync(new[]
            {
                "--url", url,
                "--pretty"
            });

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("content", out var contentElement))
            {
                return contentElement.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        public Task<List<PipelineArticle>> GetStoredArticlesAsync(string ticker)
        {
            return Task.FromResult(_cache.TryGetValue(ticker, out var result) ? result.Articles : new List<PipelineArticle>());
        }

        public Task ClearArticlesAsync(string ticker)
        {
            _cache.Remove(ticker);
            return Task.CompletedTask;
        }

        private async Task<string> RunPythonPipelineAsync(IEnumerable<string> arguments)
        {
            var scriptPath = ResolveScriptPath();
            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"Unable to locate Python pipeline script at {scriptPath}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "python3",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };

            startInfo.ArgumentList.Add(scriptPath);
            foreach (var arg in arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start python3 process");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Python pipeline failed with exit code {process.ExitCode}: {stderr}");
            }

            return stdout;
        }

        private PipelineSearchResult ParseSearchResult(string json, string ticker, string sourceFilter)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var result = new PipelineSearchResult
            {
                Ticker = root.TryGetProperty("ticker", out var tickerProp) ? tickerProp.GetString() ?? ticker : ticker,
                SourceFilter = root.TryGetProperty("source", out var sourceProp) ? sourceProp.GetString() ?? sourceFilter : sourceFilter,
                SearchedAt = DateTime.UtcNow
            };

            if (root.TryGetProperty("articles", out var articlesElement) && articlesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in articlesElement.EnumerateArray())
                {
                    result.Articles.Add(MapArticle(item));
                }
            }

            result.TotalFound = result.Articles.Count;
            return result;
        }

        private static PipelineArticle MapArticle(JsonElement item)
        {
            var fullContent = item.TryGetProperty("content", out var contentProp) ? contentProp.GetString() ?? string.Empty : string.Empty;
            var isScraped = item.TryGetProperty("is_scraped", out var scrapedProp) && scrapedProp.ValueKind == JsonValueKind.True;

            var article = new PipelineArticle
            {
                Title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? string.Empty : string.Empty,
                Summary = string.IsNullOrWhiteSpace(fullContent) ? string.Empty : fullContent.Substring(0, Math.Min(fullContent.Length, 240)),
                FullContent = fullContent,
                Url = item.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? string.Empty : string.Empty,
                Source = item.TryGetProperty("source", out var sourceProp) ? sourceProp.GetString() ?? string.Empty : string.Empty,
                Provider = item.TryGetProperty("provider", out var providerProp) ? providerProp.GetString() ?? string.Empty : string.Empty,
                Author = string.Empty,
                ImageUrl = string.Empty,
                IsScraped = isScraped,
                AddedDate = DateTime.UtcNow
            };

            if (item.TryGetProperty("published_at", out var publishedProp) && DateTime.TryParse(publishedProp.GetString(), out var publishedDate))
            {
                article.PublishedDate = publishedDate;
            }

            return article;
        }

        private string ResolveScriptPath()
        {
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "news_pipeline.py")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Scripts", "news_pipeline.py")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Scripts", "news_pipeline.py"))
            };

            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }
    }
}
