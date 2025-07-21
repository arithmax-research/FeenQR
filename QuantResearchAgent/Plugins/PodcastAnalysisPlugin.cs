using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for podcast analysis functionality
/// </summary>
public class PodcastAnalysisPlugin
{
    private readonly PodcastAnalysisService _podcastService;

    public PodcastAnalysisPlugin(PodcastAnalysisService podcastService)
    {
        _podcastService = podcastService;
    }

    [KernelFunction, Description("Analyze a podcast episode for trading insights and signals")]
    public async Task<string> AnalyzePodcastAsync(
        [Description("The Spotify URL of the podcast episode to analyze")] string podcastUrl)
    {
        try
        {
            var episode = await _podcastService.AnalyzePodcastAsync(podcastUrl);
            
            var result = $"Podcast Analysis Complete:\n" +
                        $"Episode: {episode.Name}\n" +
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
            return $"Error analyzing podcast: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get the sentiment analysis of podcast content")]
    public async Task<string> GetPodcastSentimentAsync(
        [Description("The Spotify URL of the podcast episode")] string podcastUrl)
    {
        try
        {
            var episode = await _podcastService.AnalyzePodcastAsync(podcastUrl);
            
            var sentimentLabel = episode.SentimentScore switch
            {
                >= 0.5 => "Very Bullish",
                >= 0.2 => "Bullish",
                >= -0.2 => "Neutral",
                >= -0.5 => "Bearish",
                _ => "Very Bearish"
            };
            
            return $"Podcast Sentiment Analysis:\n" +
                   $"Episode: {episode.Name}\n" +
                   $"Sentiment Score: {episode.SentimentScore:F2}\n" +
                   $"Sentiment Label: {sentimentLabel}\n" +
                   $"Analysis Date: {episode.AnalyzedAt:yyyy-MM-dd HH:mm}";
        }
        catch (Exception ex)
        {
            return $"Error getting podcast sentiment: {ex.Message}";
        }
    }

    [KernelFunction, Description("Extract technical trading insights from podcast content")]
    public async Task<string> ExtractTradingInsightsAsync(
        [Description("The Spotify URL of the podcast episode")] string podcastUrl,
        [Description("Focus area for insights (e.g., 'risk management', 'technical analysis', 'market outlook')")] string? focus = null)
    {
        try
        {
            var episode = await _podcastService.AnalyzePodcastAsync(podcastUrl);
            
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
                    : "No technical insights extracted from this episode.";
            }
            
            return result;
        }
        catch (Exception ex)
        {
            return $"Error extracting trading insights: {ex.Message}";
        }
    }
}
