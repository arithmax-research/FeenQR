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
        private readonly AchestService _achest;
        private readonly Dictionary<string, PipelineSearchResult> _cache = new();

        public NewsPipelineService(
            HttpClient httpClient,
            ILogger<NewsPipelineService> logger,
            IOptions<NewsPipelineConfig> options,
            AchestService achest)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = options.Value;
            _achest = achest;
        }

        public async Task<PipelineSearchResult> SearchNewsAsync(string ticker, string sourceFilter = "All", int limit = 25)
        {
            var requestedLimit = Math.Clamp(limit, 1, 100);
            var providerTasks = new List<Task<List<PipelineArticle>>>();

            if (ShouldQueryNewsApi(sourceFilter))
            {
                providerTasks.Add(GetNewsApiArticlesAsync(ticker, sourceFilter, requestedLimit));
            }

            if (ShouldQueryAchest(sourceFilter))
            {
                providerTasks.Add(GetAchestArticlesAsync(ticker, sourceFilter, requestedLimit));
            }

            var providerResults = await Task.WhenAll(providerTasks);
            var articles = providerResults
                .SelectMany(items => items)
                .GroupBy(article => string.IsNullOrWhiteSpace(article.Url) ? article.Title : article.Url, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderByDescending(article => article.PublishedDate)
                .Take(requestedLimit)
                .ToList();

            var result = new PipelineSearchResult
            {
                Ticker = ticker,
                SourceFilter = sourceFilter,
                Articles = articles,
                TotalFound = articles.Count,
                SearchedAt = DateTime.UtcNow
            };
            _cache[ticker] = result;
            return result;
        }

        private async Task<List<PipelineArticle>> GetNewsApiArticlesAsync(string ticker, string sourceFilter, int limit)
        {
            var articles = new List<PipelineArticle>();
            if (string.IsNullOrWhiteSpace(_config.NewsApiKey))
            {
                _logger.LogDebug("NewsAPI search skipped because NewsApi:ApiKey is not configured");
                return articles;
            }

            var query = $"\"{ticker}\" stock";
            var url = $"https://newsapi.org/v2/everything?q={Uri.EscapeDataString(query)}&language=en&sortBy=publishedAt&pageSize={limit}&apiKey={Uri.EscapeDataString(_config.NewsApiKey)}";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("FeenQR/1.0 (+https://feen-qr.misango.me)");
                using var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("NewsAPI search failed with status {StatusCode}: {ResponseBody}", response.StatusCode, body);
                    return articles;
                }

                using var document = JsonDocument.Parse(body);
                if (!document.RootElement.TryGetProperty("articles", out var items) || items.ValueKind != JsonValueKind.Array)
                {
                    return articles;
                }

                foreach (var item in items.EnumerateArray())
                {
                    var provider = GetString(item, "source", "name");
                    if (!IsNewsApiFilter(sourceFilter) && !MatchesSourceFilter(provider, sourceFilter))
                    {
                        continue;
                    }

                    var articleUrl = GetString(item, "url");
                    var title = GetString(item, "title");
                    if (string.IsNullOrWhiteSpace(articleUrl) || string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    articles.Add(new PipelineArticle
                    {
                        Title = title,
                        Summary = GetString(item, "description"),
                        Url = articleUrl,
                        Source = "NewsAPI",
                        Provider = provider,
                        Author = GetString(item, "author"),
                        ImageUrl = GetString(item, "urlToImage"),
                        PublishedDate = ParseDateTime(GetString(item, "publishedAt"))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Direct NewsAPI search failed for {Ticker}", ticker);
            }

            return articles;
        }

        private async Task<List<PipelineArticle>> GetAchestArticlesAsync(string ticker, string sourceFilter, int limit)
        {
            var articles = new List<PipelineArticle>();
            var payload = await _achest.GetNewsAsync(ticker);
            if (payload == null || payload.Value.ValueKind != JsonValueKind.Array)
            {
                return articles;
            }

            foreach (var item in payload.Value.EnumerateArray().Take(limit))
            {
                var provider = GetString(item, "source");
                if (!IsAchestFilter(sourceFilter) && !MatchesSourceFilter(provider, sourceFilter))
                {
                    continue;
                }

                var articleUrl = GetString(item, "realUrl");
                if (string.IsNullOrWhiteSpace(articleUrl))
                {
                    articleUrl = GetString(item, "url");
                }

                var title = GetString(item, "headline");
                if (string.IsNullOrWhiteSpace(articleUrl) || string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                articles.Add(new PipelineArticle
                {
                    Id = GetString(item, "id"),
                    Title = title,
                    Summary = GetString(item, "summary"),
                    Url = articleUrl,
                    Source = "Achest/EulerPool",
                    Provider = provider,
                    ImageUrl = GetString(item, "image"),
                    PublishedDate = ParseUnixDateTime(GetString(item, "datetime"))
                });
            }

            return articles;
        }

        private static bool ShouldQueryNewsApi(string sourceFilter)
                => IsNewsApiFilter(sourceFilter)
                    || string.IsNullOrWhiteSpace(sourceFilter)
                    || sourceFilter.Equals("All", StringComparison.OrdinalIgnoreCase);

        private static bool ShouldQueryAchest(string sourceFilter)
                => IsAchestFilter(sourceFilter)
                    || string.IsNullOrWhiteSpace(sourceFilter)
                    || sourceFilter.Equals("All", StringComparison.OrdinalIgnoreCase)
                    || sourceFilter.Equals("Yahoo", StringComparison.OrdinalIgnoreCase);

          private static bool IsNewsApiFilter(string sourceFilter)
                => sourceFilter.Equals("NewsAPI", StringComparison.OrdinalIgnoreCase)
                    || sourceFilter.Equals("News API", StringComparison.OrdinalIgnoreCase);

          private static bool IsAchestFilter(string sourceFilter)
                => sourceFilter.Equals("Achest", StringComparison.OrdinalIgnoreCase)
                    || sourceFilter.Equals("EulerPool", StringComparison.OrdinalIgnoreCase)
                    || sourceFilter.Equals("Achest/EulerPool", StringComparison.OrdinalIgnoreCase);

        private static bool MatchesSourceFilter(string provider, string sourceFilter)
            => string.IsNullOrWhiteSpace(sourceFilter)
               || sourceFilter.Equals("All", StringComparison.OrdinalIgnoreCase)
               || provider.Contains(sourceFilter, StringComparison.OrdinalIgnoreCase);

        private static string GetString(JsonElement item, string property, string? nestedProperty = null)
        {
            if (!item.TryGetProperty(property, out var value))
            {
                return string.Empty;
            }

            if (nestedProperty != null && value.ValueKind == JsonValueKind.Object)
            {
                return value.TryGetProperty(nestedProperty, out var nested) ? nested.GetString() ?? string.Empty : string.Empty;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
        }

        private static DateTime ParseDateTime(string value)
            => DateTime.TryParse(value, out var parsed) ? parsed.ToUniversalTime() : DateTime.UtcNow;

        private static DateTime ParseUnixDateTime(string value)
            => long.TryParse(value, out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                : DateTime.UtcNow;

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
