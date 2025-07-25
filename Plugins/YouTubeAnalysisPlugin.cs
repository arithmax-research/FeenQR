using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for YouTube video analysis functionality (replacing Spotify podcasts)
/// </summary>
public class YouTubeAnalysisPlugin
{
    private readonly YouTubeAnalysisService _youtubeService;

    public YouTubeAnalysisPlugin(YouTubeAnalysisService youtubeService)
    {
        _youtubeService = youtubeService;
    }

    [KernelFunction, Description("Analyze a YouTube video (especially from Quantopian channel) for trading insights and signals")]
    public async Task<string> AnalyzeVideoAsync(
        [Description("The YouTube URL of the video to analyze")] string videoUrl)
    {
        try
        {
            var episode = await _youtubeService.AnalyzeVideoAsync(videoUrl);
            
            var result = $"YouTube Video Analysis Complete:\n" +
                        $"Video: {episode.Name}\n" +
                        $"Published: {episode.PublishedDate:yyyy-MM-dd}\n" +
                        $"Sentiment Score: {episode.SentimentScore:F2}\n" +
                        $"Technical Insights: {episode.TechnicalInsights.Count}\n" +
                        $"Trading Signals: {episode.TradingSignals.Count}\n\n" +
                        $"Key Insights:\n{string.Join("\n- ", episode.TechnicalInsights.Take(5))}\n\n" +
                        $"Generated Signals:\n{string.Join("\n", episode.TradingSignals.Take(3))}";
            
            return result;
        }
        catch (Exception ex)
        {
            return $"Error analyzing YouTube video: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get the sentiment analysis of YouTube video content")]
    public async Task<string> GetVideoSentimentAsync(
        [Description("The YouTube URL of the video")] string videoUrl)
    {
        try
        {
            var episode = await _youtubeService.AnalyzeVideoAsync(videoUrl);
            
            var sentimentLabel = episode.SentimentScore switch
            {
                >= 0.5 => "Very Bullish",
                >= 0.2 => "Bullish",
                >= -0.2 => "Neutral",
                >= -0.5 => "Bearish",
                _ => "Very Bearish"
            };
            
            return $"Video Sentiment Analysis:\n" +
                   $"Video: {episode.Name}\n" +
                   $"Sentiment Score: {episode.SentimentScore:F2}\n" +
                   $"Sentiment Label: {sentimentLabel}\n" +
                   $"Analysis Date: {episode.AnalyzedAt:yyyy-MM-dd HH:mm}";
        }
        catch (Exception ex)
        {
            return $"Error getting video sentiment: {ex.Message}";
        }
    }

    [KernelFunction, Description("Extract technical trading insights from YouTube video content")]
    public async Task<string> ExtractTradingInsightsAsync(
        [Description("The YouTube URL of the video")] string videoUrl,
        [Description("Focus area for insights (e.g., 'risk management', 'technical analysis', 'market outlook')")] string? focus = null)
    {
        try
        {
            var episode = await _youtubeService.AnalyzeVideoAsync(videoUrl);
            
            var insights = episode.TechnicalInsights;
            
            // Filter insights if focus is specified
            if (!string.IsNullOrEmpty(focus))
            {
                insights = insights.Where(insight => 
                    insight.ToLower().Contains(focus.ToLower()) ||
                    insight.ToLower().Contains(focus.ToLower().Replace(" ", ""))
                ).ToList();
            }
            
            var result = $"Technical Trading Insights from {episode.Name}:\n\n";
            
            if (insights.Any())
            {
                result += string.Join("\n\n", insights.Select((insight, index) => 
                    $"{index + 1}. {insight}"));
            }
            else
            {
                result += focus != null 
                    ? $"No specific insights found for focus area: {focus}"
                    : "No technical insights extracted from this video.";
            }
            
            return result;
        }
        catch (Exception ex)
        {
            return $"Error extracting trading insights: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get latest videos from Quantopian YouTube channel")]
    public async Task<string> GetLatestQuantopianVideosAsync(
        [Description("Maximum number of videos to retrieve (default: 5)")] int maxResults = 5)
    {
        try
        {
            var videoUrls = await _youtubeService.GetLatestQuantopianVideosAsync(maxResults);
            
            if (!videoUrls.Any())
            {
                return "No recent videos found from Quantopian channel.";
            }
            
            var result = $"Latest {videoUrls.Count} videos from Quantopian channel:\n\n";
            result += string.Join("\n", videoUrls.Select((url, index) => $"{index + 1}. {url}"));
            
            return result;
        }
        catch (Exception ex)
        {
            return $"Error retrieving Quantopian videos: {ex.Message}";
        }
    }

    [KernelFunction, Description("Search for finance-related videos on YouTube")]
    public async Task<string> SearchFinanceVideosAsync(
        [Description("Search query for finance videos")] string query,
        [Description("Maximum number of videos to retrieve (default: 5)")] int maxResults = 5)
    {
        try
        {
            var videoUrls = await _youtubeService.SearchFinanceVideosAsync(query, maxResults);
            
            if (!videoUrls.Any())
            {
                return $"No finance videos found for query: {query}";
            }
            
            var result = $"Found {videoUrls.Count} finance videos for '{query}':\n\n";
            result += string.Join("\n", videoUrls.Select((url, index) => $"{index + 1}. {url}"));
            
            return result;
        }
        catch (Exception ex)
        {
            return $"Error searching finance videos: {ex.Message}";
        }
    }
}
