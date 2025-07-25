using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace QuantResearchAgent.Services;

/// <summary>
/// Service for analyzing YouTube videos from Quantopian channel for trading insights
/// </summary>
public class YouTubeAnalysisService
{
    private readonly ILogger<YouTubeAnalysisService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Kernel _kernel;
    private readonly HttpClient _httpClient;
    private readonly string _youTubeApiKey;

    // Quantopian channel ID and common finance YouTube channels
    private const string QUANTOPIAN_CHANNEL_ID = "UC606MUq45P3zFLa4VGKbxfw";
    private const string YOUTUBE_API_BASE = "https://www.googleapis.com/youtube/v3";

    public YouTubeAnalysisService(
        ILogger<YouTubeAnalysisService> logger,
        IConfiguration configuration,
        Kernel kernel,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _kernel = kernel;
        _httpClient = httpClient;
        _youTubeApiKey = _configuration["YouTube:ApiKey"] ?? string.Empty;
    }

    /// <summary>
    /// Analyze a YouTube video for trading insights and signals
    /// </summary>
    public async Task<PodcastEpisode> AnalyzeVideoAsync(string videoUrl)
    {
        _logger.LogInformation("Starting YouTube video analysis for URL: {VideoUrl}", videoUrl);

        try
        {
            // Extract video ID from URL
            var videoId = ExtractVideoId(videoUrl);
            if (string.IsNullOrEmpty(videoId))
            {
                throw new ArgumentException("Invalid YouTube URL format");
            }

            // Fetch video metadata from YouTube API
            var episode = await FetchVideoDataAsync(videoId);
            
            // Use description and title as content for analysis
            episode.Transcript = $"{episode.Name}\n\n{episode.Description}";
            
            // Analyze the content for technical insights
            await AnalyzeTechnicalContentAsync(episode);
            
            // Extract trading signals from insights
            await ExtractTradingSignalsAsync(episode);
            
            episode.AnalyzedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Completed video analysis for: {VideoTitle}", episode.Name);
            return episode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze YouTube video: {VideoUrl}", videoUrl);
            throw;
        }
    }

    /// <summary>
    /// Get latest videos from Quantopian channel
    /// </summary>
    public async Task<List<string>> GetLatestQuantopianVideosAsync(int maxResults = 10)
    {
        try
        {
            var url = $"{YOUTUBE_API_BASE}/search?part=snippet&channelId={QUANTOPIAN_CHANNEL_ID}&type=video&order=date&maxResults={maxResults}&key={_youTubeApiKey}";
            
            var response = await _httpClient.GetStringAsync(url);
            var searchResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(response);
            
            var videoUrls = searchResult?.Items?.Select(item => $"https://www.youtube.com/watch?v={item.Id.VideoId}").ToList() ?? new List<string>();
            
            _logger.LogInformation("Retrieved {Count} latest videos from Quantopian channel", videoUrls.Count);
            return videoUrls;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch latest Quantopian videos");
            return new List<string>();
        }
    }

    /// <summary>
    /// Search for finance-related videos on YouTube
    /// </summary>
    public async Task<List<string>> SearchFinanceVideosAsync(string query, int maxResults = 5)
    {
        try
        {
            var encodedQuery = Uri.EscapeDataString($"{query} trading finance quantitative");
            var url = $"{YOUTUBE_API_BASE}/search?part=snippet&q={encodedQuery}&type=video&order=relevance&maxResults={maxResults}&key={_youTubeApiKey}";
            
            var response = await _httpClient.GetStringAsync(url);
            var searchResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(response);
            
            var videoUrls = searchResult?.Items?.Select(item => $"https://www.youtube.com/watch?v={item.Id.VideoId}").ToList() ?? new List<string>();
            
            _logger.LogInformation("Found {Count} finance videos for query: {Query}", videoUrls.Count, query);
            return videoUrls;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search finance videos for query: {Query}", query);
            return new List<string>();
        }
    }

    private string ExtractVideoId(string videoUrl)
    {
        if (string.IsNullOrWhiteSpace(videoUrl))
            return string.Empty;

        try
        {
            // Handle various YouTube URL formats including the one with si parameter
            var patterns = new[]
            {
                @"(?:youtube\.com/watch\?v=|youtu\.be/|youtube\.com/embed/|youtube\.com/v/)([a-zA-Z0-9_-]{11})",
                @"youtube\.com/watch\?.*v=([a-zA-Z0-9_-]{11})",
                @"youtu\.be/([a-zA-Z0-9_-]{11})",
                @"youtube\.com/embed/([a-zA-Z0-9_-]{11})",
                @"youtube\.com/v/([a-zA-Z0-9_-]{11})"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(videoUrl, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups[1].Value.Length == 11)
                {
                    var videoId = match.Groups[1].Value;
                    _logger.LogDebug("Extracted video ID: {VideoId} from URL: {VideoUrl}", videoId, videoUrl);
                    return videoId;
                }
            }

            // Try to extract just the 11-character video ID if it's at the end
            var lastPart = videoUrl.Split('/', '?', '&').LastOrDefault();
            if (!string.IsNullOrEmpty(lastPart) && lastPart.Length >= 11)
            {
                var possibleId = lastPart.Substring(0, Math.Min(11, lastPart.Length));
                if (Regex.IsMatch(possibleId, @"^[a-zA-Z0-9_-]{11}$"))
                {
                    _logger.LogDebug("Extracted video ID from last part: {VideoId}", possibleId);
                    return possibleId;
                }
            }

            _logger.LogWarning("Could not extract video ID from URL: {VideoUrl}", videoUrl);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting video ID from URL: {VideoUrl}", videoUrl);
            return string.Empty;
        }
    }

    private async Task<PodcastEpisode> FetchVideoDataAsync(string videoId)
    {
        try
        {
            var url = $"{YOUTUBE_API_BASE}/videos?part=snippet&id={videoId}&key={_youTubeApiKey}";
            var response = await _httpClient.GetStringAsync(url);
            var videoResult = JsonSerializer.Deserialize<YouTubeVideoResponse>(response);
            
            var video = videoResult?.Items?.FirstOrDefault();
            if (video == null)
            {
                throw new InvalidOperationException($"Video not found: {videoId}");
            }

            return new PodcastEpisode
            {
                Id = video.Id,
                Name = video.Snippet.Title,
                Description = video.Snippet.Description,
                PublishedDate = DateTime.Parse(video.Snippet.PublishedAt),
                PodcastUrl = $"https://www.youtube.com/watch?v={videoId}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch video data from YouTube for video ID: {VideoId}", videoId);
            throw;
        }
    }

    private async Task AnalyzeTechnicalContentAsync(PodcastEpisode episode)
    {
        var prompt = $@"
You are a quantitative finance expert analyzing YouTube video content for trading insights.

Video Title: {episode.Name}
Video Description: {episode.Description}
Channel: Quantopian/Finance Channel

Please analyze this content and extract:
1. Technical trading concepts mentioned (indicators, strategies, patterns)
2. Market analysis or predictions (specific markets, sectors, trends)
3. Investment strategies discussed (portfolio management, risk strategies)
4. Risk management principles (position sizing, stop losses, hedging)
5. Quantitative methods or indicators mentioned (algorithms, backtesting, statistics)
6. Educational concepts (finance theory, market mechanics)

Focus on actionable insights that could inform trading decisions.
Provide specific, concise bullet points for each category that has relevant content.
If a category has no relevant information, skip it.
Prioritize concrete, implementable insights over general concepts.
";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            var analysis = result.ToString();
            episode.TechnicalInsights = ParseTechnicalInsights(analysis);
            
            // Calculate sentiment score
            episode.SentimentScore = await CalculateSentimentAsync($"{episode.Name}\n{episode.Description}");
            
            _logger.LogInformation("Extracted {InsightCount} technical insights from video: {VideoTitle}", 
                episode.TechnicalInsights.Count, episode.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze technical content for video: {VideoId}", episode.Id);
            throw;
        }
    }

    private async Task ExtractTradingSignalsAsync(PodcastEpisode episode)
    {
        var insightsText = string.Join("\n", episode.TechnicalInsights);
        
        var prompt = $@"
Based on the following technical insights from a financial YouTube video, generate specific trading signals:

Video Title: {episode.Name}
Technical Insights:
{insightsText}

Video Sentiment Score: {episode.SentimentScore:F2}

Please generate trading signals in the following format:
- Symbol: [STOCK/ETF/CRYPTO SYMBOL] (use real, liquid symbols like SPY, QQQ, AAPL, BTC, etc.)
- Action: [BUY/SELL/HOLD]
- Strength: [0.1-1.0] (confidence level)
- Reasoning: [Brief explanation based on the video content]
- Time Horizon: [SHORT/MEDIUM/LONG] (days/weeks/months)

Only suggest signals where you have high confidence based on the video content.
Focus on the most liquid and well-known assets.
Be conservative and prioritize risk management.
Maximum 3 signals per video.
If no strong signals can be derived, return 'No clear trading signals identified.'
";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            var signals = ParseTradingSignals(result.ToString());
            episode.TradingSignals = signals;
            
            _logger.LogInformation("Extracted {SignalCount} trading signals from video: {VideoTitle}", 
                signals.Count, episode.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract trading signals for video: {VideoId}", episode.Id);
            throw;
        }
    }

    private async Task<double> CalculateSentimentAsync(string text)
    {
        var prompt = $@"
Analyze the sentiment of the following financial YouTube video content and provide a sentiment score:

Content: {text}

Please provide a sentiment score between -1.0 (very bearish/pessimistic) and +1.0 (very bullish/optimistic) for this content.
Consider:
- Market outlook mentions (bullish/bearish language)
- Risk vs opportunity language
- Confidence indicators in predictions
- Overall economic sentiment
- Educational tone (neutral) vs strong directional bias

Respond with only the numerical score (e.g., 0.3, -0.5, 0.8).
Educational content without strong directional bias should be close to 0.0.
";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            if (double.TryParse(result.ToString().Trim(), out var sentiment))
            {
                return Math.Max(-1.0, Math.Min(1.0, sentiment)); // Clamp between -1 and 1
            }
            
            return 0.0; // Neutral if parsing fails
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate sentiment, defaulting to neutral");
            return 0.0;
        }
    }

    private List<string> ParseTechnicalInsights(string analysis)
    {
        var insights = new List<string>();
        
        // Split by bullet points or numbered lists
        var lines = analysis.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var cleaned = line.Trim().TrimStart('-', '*', '•', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', ')');
            if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 15)
            {
                insights.Add(cleaned.Trim());
            }
        }
        
        return insights.Take(10).ToList(); // Limit to top 10 insights
    }

    private List<string> ParseTradingSignals(string signalsText)
    {
        var signals = new List<string>();
        
        if (signalsText.Contains("No clear trading signals"))
        {
            return signals;
        }
        
        // Parse the structured trading signals
        var sections = signalsText.Split(new[] { "Symbol:", "- Symbol:" }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var section in sections.Skip(1)) // Skip first empty section
        {
            var lines = section.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length >= 4) // Need at least Symbol, Action, Strength, Reasoning
            {
                var signalText = "Symbol:" + section.Trim();
                signals.Add(signalText);
            }
        }
        
        return signals.Take(3).ToList(); // Limit to 3 signals
    }
}

// YouTube API response models
public class YouTubeSearchResponse
{
    public List<YouTubeSearchItem>? Items { get; set; }
}

public class YouTubeSearchItem
{
    public YouTubeVideoId Id { get; set; } = new();
    public YouTubeSnippet Snippet { get; set; } = new();
}

public class YouTubeVideoId
{
    public string VideoId { get; set; } = string.Empty;
}

public class YouTubeVideoResponse
{
    public List<YouTubeVideo>? Items { get; set; }
}

public class YouTubeVideo
{
    public string Id { get; set; } = string.Empty;
    public YouTubeSnippet Snippet { get; set; } = new();
}

public class YouTubeSnippet
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PublishedAt { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
}
