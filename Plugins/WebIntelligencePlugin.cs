using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for web intelligence operations
/// </summary>
public class WebIntelligencePlugin
{
    private readonly WebIntelligenceService _webIntelligenceService;

    public WebIntelligencePlugin(WebIntelligenceService webIntelligenceService)
    {
        _webIntelligenceService = webIntelligenceService;
    }

    [KernelFunction, Description("Scrape earnings presentations from company websites")]
    public async Task<string> ScrapeEarningsPresentations(
        [Description("The stock symbol to scrape earnings for (e.g., AAPL, TSLA)")] string symbol)
    {
        try
        {
            var presentations = await _webIntelligenceService.ScrapeEarningsPresentationsAsync(symbol, DateTime.Now.AddMonths(-6));

            if (presentations.Any())
            {
                return $"Found {presentations.Count} earnings presentations for {symbol}:\n" +
                       string.Join("\n", presentations.Select(p =>
                           $"- {p.Title} ({p.Date:yyyy-MM-dd}): {p.Url}"));
            }
            else
            {
                return $"No earnings presentations found for {symbol}";
            }
        }
        catch (Exception ex)
        {
            return $"Error scraping earnings presentations: {ex.Message}";
        }
    }

    [KernelFunction, Description("Monitor corporate communications and press releases")]
    public async Task<string> MonitorCorporateCommunications(
        [Description("The stock symbol to monitor communications for")] string symbol)
    {
        try
        {
            var communications = await _webIntelligenceService.MonitorCorporateCommunicationsAsync(symbol, DateTime.Now.AddDays(-30));

            if (communications.Any())
            {
                return $"Recent corporate communications for {symbol}:\n" +
                       string.Join("\n", communications.Select(c =>
                           $"- {c.Type}: {c.Title} ({c.Date:yyyy-MM-dd})"));
            }
            else
            {
                return $"No recent corporate communications found for {symbol}";
            }
        }
        catch (Exception ex)
        {
            return $"Error monitoring corporate communications: {ex.Message}";
        }
    }

    [KernelFunction, Description("Analyze web sentiment for a company")]
    public async Task<string> AnalyzeWebSentiment(
        [Description("The stock symbol to analyze sentiment for")] string symbol)
    {
        try
        {
            var sentimentAnalysis = await _webIntelligenceService.AnalyzeWebSentimentAsync(symbol);

            if (sentimentAnalysis.Any())
            {
                var analysis = sentimentAnalysis.First();
                return $"Web sentiment analysis for {symbol}:\n" +
                       $"Overall Sentiment: {analysis.OverallSentiment:F2}\n" +
                       $"Positive Mentions: {analysis.PositiveMentions}\n" +
                       $"Negative Mentions: {analysis.NegativeMentions}\n" +
                       $"Sources: {string.Join(", ", analysis.Sources)}";
            }
            else
            {
                return $"No sentiment analysis available for {symbol}";
            }
        }
        catch (Exception ex)
        {
            return $"Error analyzing web sentiment: {ex.Message}";
        }
    }

    [KernelFunction, Description("Analyze social media influencers mentioning the company")]
    public async Task<string> AnalyzeSocialMedia(
        [Description("The stock symbol to analyze social media for")] string symbol)
    {
        try
        {
            var mentions = await _webIntelligenceService.AnalyzeSocialMediaInfluencersAsync(symbol);

            if (mentions.Any())
            {
                return $"Social media influencer analysis for {symbol}:\n" +
                       string.Join("\n", mentions.Select(m =>
                           $"{m.InfluencerName} ({m.Platform}): {m.Followers} followers, " +
                           $"Sentiment: {m.Sentiment:F2}, Volume: {m.VolumeScore:F2}, " +
                           $"Engagement: {m.EngagementRate:F2}"));
            }
            else
            {
                return $"No social media influencer data available for {symbol}";
            }
        }
        catch (Exception ex)
        {
            return $"Error analyzing social media influencers: {ex.Message}";
        }
    }

    [KernelFunction, Description("Monitor dark web for company-related security concerns")]
    public async Task<string> MonitorDarkWeb(
        [Description("The stock symbol to monitor dark web for")] string symbol)
    {
        try
        {
            var mentions = await _webIntelligenceService.MonitorDarkWebAsync(symbol);

            if (mentions.Any())
            {
                return $"Dark web monitoring for {symbol}:\n" +
                       string.Join("\n", mentions.Select(m =>
                           $"{m.MentionType} ({m.Severity}): {m.Content}"));
            }
            else
            {
                return $"No dark web mentions found for {symbol}";
            }
        }
        catch (Exception ex)
        {
            return $"Error monitoring dark web: {ex.Message}";
        }
    }
}