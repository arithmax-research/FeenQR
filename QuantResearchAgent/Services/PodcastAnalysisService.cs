using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using SpotifyAPI.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuantResearchAgent.Services;

public class PodcastAnalysisService
{
    private readonly ILogger<PodcastAnalysisService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Kernel _kernel;
    private readonly SpotifyApi _spotifyApi;

    public PodcastAnalysisService(
        ILogger<PodcastAnalysisService> logger,
        IConfiguration configuration,
        Kernel kernel)
    {
        _logger = logger;
        _configuration = configuration;
        _kernel = kernel;

        // Initialize Spotify client
        var spotifyConfig = SpotifyClientConfig.CreateDefault()
            .WithAuthenticator(new ClientCredentialsAuthenticator(
                _configuration["Spotify:ClientId"]!,
                _configuration["Spotify:ClientSecret"]!));

        _spotifyApi = new SpotifyApi(spotifyConfig);
    }

    public async Task<PodcastEpisode> AnalyzePodcastAsync(string podcastUrl)
    {
        _logger.LogInformation("Starting podcast analysis for URL: {PodcastUrl}", podcastUrl);

        try
        {
            // Extract episode ID from URL
            var episodeId = ExtractEpisodeId(podcastUrl);
            if (string.IsNullOrEmpty(episodeId))
            {
                throw new ArgumentException("Invalid podcast URL format");
            }

            // Fetch episode metadata from Spotify
            var episode = await FetchEpisodeDataAsync(episodeId);
            
            // For now, we'll use the description as transcript (in real implementation, you'd use speech-to-text)
            episode.Transcript = episode.Description;
            
            // Analyze the content for technical insights
            await AnalyzeTechnicalContentAsync(episode);
            
            // Extract trading signals from insights
            await ExtractTradingSignalsAsync(episode);
            
            episode.AnalyzedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Completed podcast analysis for episode: {EpisodeName}", episode.Name);
            return episode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze podcast: {PodcastUrl}", podcastUrl);
            throw;
        }
    }

    private string ExtractEpisodeId(string podcastUrl)
    {
        var match = Regex.Match(podcastUrl, @"episode/([a-zA-Z0-9]+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private async Task<PodcastEpisode> FetchEpisodeDataAsync(string episodeId)
    {
        try
        {
            var episode = await _spotifyApi.Episodes.Get(episodeId);
            
            return new PodcastEpisode
            {
                Id = episode.Id,
                Name = episode.Name,
                Description = episode.Description,
                PublishedDate = episode.ReleaseDate,
                PodcastUrl = episode.ExternalUrls.FirstOrDefault().Value ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch episode data from Spotify for episode ID: {EpisodeId}", episodeId);
            throw;
        }
    }

    private async Task AnalyzeTechnicalContentAsync(PodcastEpisode episode)
    {
        var prompt = $@"
You are a quantitative finance expert analyzing podcast content for trading insights.

Podcast Episode: {episode.Name}
Content: {episode.Description}

Please analyze this content and extract:
1. Technical trading concepts mentioned
2. Market analysis or predictions
3. Investment strategies discussed
4. Risk management principles
5. Quantitative methods or indicators mentioned

Focus on actionable insights that could inform trading decisions.
Provide specific, concise bullet points for each category.
";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            var analysis = result.ToString();
            episode.TechnicalInsights = ParseTechnicalInsights(analysis);
            
            // Calculate sentiment score
            episode.SentimentScore = await CalculateSentimentAsync(episode.Description);
            
            _logger.LogInformation("Extracted {InsightCount} technical insights from episode: {EpisodeName}", 
                episode.TechnicalInsights.Count, episode.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze technical content for episode: {EpisodeId}", episode.Id);
            throw;
        }
    }

    private async Task ExtractTradingSignalsAsync(PodcastEpisode episode)
    {
        var insightsText = string.Join("\n", episode.TechnicalInsights);
        
        var prompt = $@"
Based on the following technical insights from a financial podcast, generate specific trading signals:

Technical Insights:
{insightsText}

Episode Sentiment Score: {episode.SentimentScore:F2}

Please generate trading signals in the following format:
- Symbol: [STOCK/CRYPTO SYMBOL]
- Action: [BUY/SELL/HOLD]
- Strength: [0.1-1.0]
- Reasoning: [Brief explanation]
- Time Horizon: [SHORT/MEDIUM/LONG]

Only suggest signals where you have high confidence based on the content.
Focus on the most liquid and well-known assets.
Be conservative and prioritize risk management.
";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            var signals = ParseTradingSignals(result.ToString());
            episode.TradingSignals = signals;
            
            _logger.LogInformation("Extracted {SignalCount} trading signals from episode: {EpisodeName}", 
                signals.Count, episode.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract trading signals for episode: {EpisodeId}", episode.Id);
            throw;
        }
    }

    private async Task<double> CalculateSentimentAsync(string text)
    {
        var prompt = $@"
Analyze the sentiment of the following financial content and provide a sentiment score:

Content: {text}

Please provide a sentiment score between -1.0 (very bearish) and +1.0 (very bullish) for this content.
Consider:
- Market outlook mentions
- Risk vs opportunity language
- Confidence indicators
- Economic sentiment

Respond with only the numerical score (e.g., 0.3, -0.5, 0.8).
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
        
        // Simple parsing - split by bullet points or newlines
        var lines = analysis.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var cleaned = line.Trim().TrimStart('-', '*', '•', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', ')');
            if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 10)
            {
                insights.Add(cleaned.Trim());
            }
        }
        
        return insights;
    }

    private List<string> ParseTradingSignals(string signalsText)
    {
        var signals = new List<string>();
        
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
        
        return signals;
    }
}
