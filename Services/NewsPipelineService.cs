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
        Task<BatchSentimentResult> AnalyzeSentimentAsync(string symbol, IEnumerable<ArticleForAnalysis> articles);
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

        public async Task<BatchSentimentResult> AnalyzeSentimentAsync(string symbol, IEnumerable<ArticleForAnalysis> articles)
        {
            var articleList = articles?.Where(article => article != null).ToList() ?? new List<ArticleForAnalysis>();
            if (articleList.Count == 0)
            {
                return new BatchSentimentResult
                {
                    Symbol = symbol,
                    ArticleResults = new List<ArticleSentimentResult>(),
                    OverallSentiment = "Neutral",
                    OverallScore = 0.0,
                    Confidence = 0.0,
                    AnalyzedAt = DateTime.UtcNow
                };
            }

            var payload = new
            {
                articles = articleList.Select(article => new
                {
                    id = article.Id,
                    title = article.Title,
                    content = article.Content,
                    source = article.Source,
                    published_at = article.PublishedDate,
                    url = article.Url
                }).ToList()
            };

            var json = await RunPythonPipelineAsync(new[] { "--sentiment", "--pretty" }, JsonSerializer.Serialize(payload));
            return ParseSentimentResult(json, symbol, articleList.Count);
        }

        private async Task<string> RunPythonPipelineAsync(IEnumerable<string> arguments, string? stdinPayload = null)
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
                RedirectStandardInput = true,
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

            if (!string.IsNullOrWhiteSpace(stdinPayload))
            {
                await process.StandardInput.WriteAsync(stdinPayload);
                await process.StandardInput.FlushAsync();
            }

            process.StandardInput.Close();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Python pipeline failed with exit code {process.ExitCode}: {stderr}");
            }

            return stdout;
        }

        private static BatchSentimentResult ParseSentimentResult(string json, string symbol, int expectedCount)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var result = new BatchSentimentResult
            {
                Symbol = symbol,
                AnalyzedAt = DateTime.UtcNow
            };

            if (root.TryGetProperty("aggregate", out var aggregate))
            {
                if (aggregate.TryGetProperty("sentiment", out var sentimentProp))
                {
                    result.OverallSentiment = sentimentProp.GetString() ?? "Neutral";
                }

                if (aggregate.TryGetProperty("avg_polarity", out var polarityProp) && polarityProp.ValueKind == JsonValueKind.Number)
                {
                    polarityProp.TryGetDouble(out var overallScore);
                    result.OverallScore = overallScore;
                }

                if (aggregate.TryGetProperty("bullish_pct", out var bullishProp) && bullishProp.ValueKind == JsonValueKind.Number)
                {
                    bullishProp.TryGetDouble(out var bullish);
                    result.BullishPercentage = bullish / 100.0;
                }

                if (aggregate.TryGetProperty("bearish_pct", out var bearishProp) && bearishProp.ValueKind == JsonValueKind.Number)
                {
                    bearishProp.TryGetDouble(out var bearish);
                    result.BearishPercentage = bearish / 100.0;
                }

                if (aggregate.TryGetProperty("neutral_pct", out var neutralProp) && neutralProp.ValueKind == JsonValueKind.Number)
                {
                    neutralProp.TryGetDouble(out var neutral);
                    result.NeutralPercentage = neutral / 100.0;
                }

                if (aggregate.TryGetProperty("article_count", out var articleCountProp) && articleCountProp.ValueKind == JsonValueKind.Number)
                {
                    articleCountProp.TryGetInt32(out var articleCount);
                    expectedCount = articleCount;
                }
            }

            if (root.TryGetProperty("articles", out var articlesElement) && articlesElement.ValueKind == JsonValueKind.Array)
            {
                var articleResults = new List<ArticleSentimentResult>();

                foreach (var item in articlesElement.EnumerateArray())
                {
                    var sentiment = item.TryGetProperty("sentiment", out var sentimentProp)
                        ? sentimentProp.GetString() ?? "NEUTRAL"
                        : "NEUTRAL";

                    var score = 0.0;
                    if (item.TryGetProperty("polarity", out var polarityProp) && polarityProp.ValueKind == JsonValueKind.Number)
                    {
                        polarityProp.TryGetDouble(out score);
                    }

                    var confidence = 0.0;
                    if (item.TryGetProperty("confidence", out var confidenceProp) && confidenceProp.ValueKind == JsonValueKind.Number)
                    {
                        confidenceProp.TryGetDouble(out confidence);
                    }

                    var keyTopics = item.TryGetProperty("keyTopics", out var topicsProp) && topicsProp.ValueKind == JsonValueKind.Array
                        ? topicsProp.EnumerateArray().Select(topic => topic.GetString() ?? string.Empty).Where(topic => !string.IsNullOrWhiteSpace(topic)).ToList()
                        : new List<string>();

                    articleResults.Add(new ArticleSentimentResult
                    {
                        Sentiment = NormalizeSentimentLabel(sentiment),
                        Score = score,
                        Confidence = confidence,
                        KeyTopics = keyTopics,
                        Impact = item.TryGetProperty("impact", out var impactProp)
                            ? impactProp.GetString() ?? "Medium"
                            : "Medium"
                    });
                }

                result.ArticleResults = articleResults;
            }

            while (result.ArticleResults.Count < expectedCount)
            {
                result.ArticleResults.Add(new ArticleSentimentResult
                {
                    Sentiment = "Neutral",
                    Score = 0.0,
                    Confidence = 0.0,
                    KeyTopics = new List<string>(),
                    Impact = "Low"
                });
            }

            if (result.ArticleResults.Any())
            {
                result.Confidence = result.ArticleResults.Average(item => item.Confidence > 0 ? item.Confidence : Math.Abs(item.Score));
            }

            return result;
        }

        private static string NormalizeSentimentLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return "Neutral";
            }

            return label.ToUpperInvariant() switch
            {
                "POSITIVE" => "Positive",
                "NEGATIVE" => "Negative",
                "NEUTRAL" => "Neutral",
                "BULLISH" => "Positive",
                "BEARISH" => "Negative",
                _ when label.Length == 1 => char.ToUpperInvariant(label[0]).ToString(),
                _ => char.ToUpperInvariant(label[0]) + label[1..].ToLowerInvariant()
            };
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
