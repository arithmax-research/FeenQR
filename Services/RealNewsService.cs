using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace QuantResearchAgent.Services;

/// <summary>
/// Real News Service - Integrates Python news fetcher with C# application
/// </summary>
public class RealNewsService
{
    private readonly ILogger<RealNewsService> _logger;

    public RealNewsService(ILogger<RealNewsService> logger)
    {
        _logger = logger;
    }

    public async Task<RealNewsResult> GetRealNewsSentimentAsync(string ticker)
    {
        try
        {
            _logger.LogInformation($"Fetching real news sentiment for {ticker}");

            var pythonScript = Path.Combine("sentiment_pipeline", "robust_news_fetcher.py");
            if (!File.Exists(pythonScript))
            {
                _logger.LogWarning("Python news fetcher script not found");
                return new RealNewsResult { Success = false, Error = "Python script not found" };
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = $"{pythonScript} --asset {ticker}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory()
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                var result = ParsePythonOutput(output, ticker);
                if (result.Success)
                {
                    _logger.LogInformation($"Successfully got real news sentiment: {result.SentimentScore:F2} (confidence: {result.Confidence:F2})");
                }
                return result;
            }
            else
            {
                _logger.LogWarning($"Python news fetcher failed: {error}");
                return new RealNewsResult { Success = false, Error = error };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get real news sentiment");
            return new RealNewsResult { Success = false, Error = ex.Message };
        }
    }

    private RealNewsResult ParsePythonOutput(string output, string ticker)
    {
        try
        {
            var result = new RealNewsResult { Success = false };

            // Extract sentiment score
            var scoreMatch = Regex.Match(output, @"Sentiment Score:\s*(-?\d+\.?\d*)", RegexOptions.IgnoreCase);
            if (scoreMatch.Success && double.TryParse(scoreMatch.Groups[1].Value, out var score))
            {
                result.SentimentScore = Math.Max(-1.0, Math.Min(1.0, score));
            }

            // Extract confidence
            var confidenceMatch = Regex.Match(output, @"Confidence:\s*(\d+\.?\d*)%?", RegexOptions.IgnoreCase);
            if (confidenceMatch.Success && double.TryParse(confidenceMatch.Groups[1].Value, out var confidence))
            {
                result.Confidence = confidence > 1.0 ? confidence / 100.0 : confidence;
                result.Confidence = Math.Max(0.0, Math.Min(1.0, result.Confidence));
            }

            // Extract article count
            var articlesMatch = Regex.Match(output, @"Analyzed\s*(\d+)\s*REAL news articles", RegexOptions.IgnoreCase);
            if (articlesMatch.Success && int.TryParse(articlesMatch.Groups[1].Value, out var articleCount))
            {
                result.ArticleCount = articleCount;
            }

            // Extract news articles
            result.NewsArticles = ExtractNewsArticles(output);

            // Extract overall label
            var labelMatch = Regex.Match(output, @"Overall Label:\s*🟢\s*(\w+)|🔴\s*(\w+)|🟡\s*(\w+)", RegexOptions.IgnoreCase);
            if (labelMatch.Success)
            {
                result.SentimentLabel = labelMatch.Groups[1].Value + labelMatch.Groups[2].Value + labelMatch.Groups[3].Value;
            }
            else
            {
                result.SentimentLabel = GetSentimentLabel(result.SentimentScore);
            }

            // Success if we have either confidence or articles
            result.Success = result.Confidence > 0 || result.ArticleCount > 0;

            if (result.Success)
            {
                result.Analysis = $"Real news analysis: {result.ArticleCount} articles analyzed for {ticker}. " +
                                $"Sentiment: {result.SentimentLabel} ({result.SentimentScore:F2})";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Python output");
            return new RealNewsResult { Success = false, Error = ex.Message };
        }
    }

    private List<NewsArticleInfo> ExtractNewsArticles(string output)
    {
        var articles = new List<NewsArticleInfo>();

        try
        {
            // Extract individual articles from the output
            var articlePattern = @"\*\*(\d+)\.\s*(🟢|🔴|🟡)\s*([^*]+)\*\*\s*📰 Title:\s*([^\n]+)\s*🏢 Publisher:\s*([^\n]+)\s*📊 Sentiment Score:\s*(-?\d+\.?\d*)\s*🔗 URL:\s*([^\n]+)";
            var matches = Regex.Matches(output, articlePattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 8)
                {
                    var article = new NewsArticleInfo
                    {
                        Title = match.Groups[4].Value.Trim(),
                        Publisher = match.Groups[5].Value.Trim(),
                        Url = match.Groups[7].Value.Trim(),
                        SentimentScore = double.TryParse(match.Groups[6].Value, out var score) ? score : 0.0,
                        SentimentEmoji = match.Groups[2].Value.Trim()
                    };

                    articles.Add(article);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract news articles from output");
        }

        return articles;
    }

    private string GetSentimentLabel(double score)
    {
        return score switch
        {
            >= 0.6 => "Very Bullish",
            >= 0.2 => "Bullish",
            >= -0.2 => "Neutral",
            >= -0.6 => "Bearish",
            _ => "Very Bearish"
        };
    }
}

public class RealNewsResult
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public double SentimentScore { get; set; }
    public double Confidence { get; set; }
    public int ArticleCount { get; set; }
    public string SentimentLabel { get; set; } = "Neutral";
    public string Analysis { get; set; } = string.Empty;
    public List<NewsArticleInfo> NewsArticles { get; set; } = new();
}

public class NewsArticleInfo
{
    public string Title { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public double SentimentScore { get; set; }
    public string SentimentEmoji { get; set; } = string.Empty;
}