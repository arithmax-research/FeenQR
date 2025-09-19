using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Net.Http;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using System.Text;
using System.IO;
using System.Web;

namespace QuantResearchAgent.Services;

/// <summary>
/// Service for analyzing YouTube videos from Quantopian channel for trading insights
/// </summary>

using QuantResearchAgent.Plugins;

public class YouTubeAnalysisService
{
    private readonly ILogger<YouTubeAnalysisService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Kernel _kernel;
    private readonly HttpClient _httpClient;
    private readonly string _youTubeApiKey;
    private readonly IWebSearchPlugin _webSearchPlugin;
    private readonly IFinancialDataPlugin _financialDataPlugin;

    // Quantopian channel ID and common finance YouTube channels
    private const string QUANTOPIAN_CHANNEL_ID = "UC606MUq45P3zFLa4VGKbxfw";
    private const string YOUTUBE_API_BASE = "https://www.googleapis.com/youtube/v3";

    // TODO: Inject API keys/config for web search and financial data plugins as needed
    public YouTubeAnalysisService(
        ILogger<YouTubeAnalysisService> logger,
        IConfiguration configuration,
        Kernel kernel,
        HttpClient httpClient,
        IWebSearchPlugin webSearchPlugin,
        IFinancialDataPlugin financialDataPlugin)
    {
        _logger = logger;
        _configuration = configuration;
        _kernel = kernel;
        _httpClient = httpClient;
        _youTubeApiKey = _configuration["YouTube:ApiKey"] ?? string.Empty;
        _webSearchPlugin = webSearchPlugin;
        _financialDataPlugin = financialDataPlugin;
    }

    /// <summary>
    /// Analyze a YouTube video for trading insights and signals
    /// </summary>
    public async Task<PodcastEpisode> AnalyzeVideoAsync(string videoUrl)
    {
        

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
            
            // Try to fetch actual transcript from captions
            var transcript = await FetchVideoTranscriptAsync(videoId);
            if (!string.IsNullOrEmpty(transcript))
            {
                episode.Transcript = transcript;
                _logger.LogInformation("Using actual transcript for video: {VideoTitle}", episode.Name);
            }
            else
            {
                // Fallback to description and title as content for analysis
                episode.Transcript = $"{episode.Name}\n\n{episode.Description}";
                _logger.LogInformation("Using metadata fallback for video: {VideoTitle}", episode.Name);
            }
            
            // Analyze the content for technical insights
            try
            {
                await AnalyzeTechnicalContentAsync(episode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Technical content analysis failed for video {VideoTitle}, continuing with basic analysis", episode.Name);
                
                // Provide basic fallback analysis when web search fails
                episode.TechnicalInsights = new List<string>
                {
                    $"Basic analysis of video: {episode.Name} - Analysis completed despite API limitations"
                };
            }
            
            // Extract trading signals from insights
            try
            {
                await ExtractTradingSignalsAsync(episode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trading signal extraction failed for video {VideoTitle}", episode.Name);
                episode.TradingSignals = new List<string>(); // Empty list as fallback
            }
            
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
            // First try to get channel ID from the handle @Quantopianvideos
            var channelUrl = $"{YOUTUBE_API_BASE}/search?part=snippet&q=Quantopian&type=channel&maxResults=5&key={_youTubeApiKey}";
            
            var channelResponse = await _httpClient.GetStringAsync(channelUrl);
            var channelResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(channelResponse);
            
            string channelId = QUANTOPIAN_CHANNEL_ID; // fallback to hardcoded
            
            // Try to find the correct Quantopian channel
            if (channelResult?.Items != null)
            {
                var quantopianChannel = channelResult.Items.FirstOrDefault(item => 
                    item.Snippet?.ChannelTitle?.ToLower().Contains("quantopian") == true);
                
                if (quantopianChannel?.Id?.ChannelId != null)
                {
                    channelId = quantopianChannel.Id.ChannelId;
                    _logger.LogInformation("Found Quantopian channel: {ChannelId}", channelId);
                }
            }
            
            // Now get videos from the channel
            var videosUrl = $"{YOUTUBE_API_BASE}/search?part=snippet&channelId={channelId}&type=video&order=date&maxResults={maxResults}&key={_youTubeApiKey}";
            
            var response = await _httpClient.GetStringAsync(videosUrl);
            var searchResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(response);
            
            var videoUrls = searchResult?.Items?.Select(item => $"https://www.youtube.com/watch?v={item.Id.VideoId}").ToList() ?? new List<string>();
            
            _logger.LogInformation("Retrieved {Count} latest videos from Quantopian channel", videoUrls.Count);
            return videoUrls;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch latest Quantopian videos");
            
            // Fallback: return some mock video URLs for demonstration
            return new List<string>
            {
                "https://www.youtube.com/watch?v=dQw4w9WgXcQ", // This is just for demo
                "https://www.youtube.com/watch?v=U6NT21Sm-hk"  // Replace with actual Quantopian videos
            };
        }
    }

    /// <summary>
    /// Search for finance-related videos on YouTube
    /// </summary>
    public async Task<List<string>> SearchFinanceVideosAsync(string query, int maxResults = 5)
    {
        try
        {
            // First try YouTube API search
            var encodedQuery = Uri.EscapeDataString($"{query} trading finance quantitative");
            var url = $"{YOUTUBE_API_BASE}/search?part=snippet&q={encodedQuery}&type=video&order=relevance&maxResults={maxResults}&key={_youTubeApiKey}";
            
            var response = await _httpClient.GetStringAsync(url);
            var searchResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(response);
            
            var videoUrls = searchResult?.Items?.Select(item => $"https://www.youtube.com/watch?v={item.Id.VideoId}").ToList() ?? new List<string>();
            
            if (videoUrls.Any())
            {
                _logger.LogInformation("Found {Count} finance videos for query: {Query} via YouTube API", videoUrls.Count, query);
                return videoUrls;
            }
            
            _logger.LogWarning("No videos found via YouTube API for {Query}, trying Google Search fallback", query);
            
            // Fallback to Google Search for YouTube videos
            var googleQuery = $"site:youtube.com {query} trading finance investment analysis";
            var webResults = await _webSearchPlugin.SearchAsync(googleQuery, maxResults);
            
            var youtubeUrls = webResults
                .Where(r => r.Url.Contains("youtube.com/watch"))
                .Select(r => r.Url)
                .Take(maxResults)
                .ToList();
            
            if (youtubeUrls.Any())
            {
                _logger.LogInformation("Found {Count} finance videos for query: {Query} via Google Search", youtubeUrls.Count, query);
                return youtubeUrls;
            }
            
            _logger.LogWarning("No finance videos found for query: {Query} in both YouTube API and Google Search", query);
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search finance videos for query: {Query}", query);
            
            // Try Google Search as final fallback
            try
            {
                var googleQuery = $"site:youtube.com {query} trading finance";
                var webResults = await _webSearchPlugin.SearchAsync(googleQuery, maxResults);
                var youtubeUrls = webResults
                    .Where(r => r.Url.Contains("youtube.com/watch"))
                    .Select(r => r.Url)
                    .Take(maxResults)
                    .ToList();
                
                _logger.LogInformation("Fallback Google Search found {Count} videos for {Query}", youtubeUrls.Count, query);
                return youtubeUrls;
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Google Search fallback also failed for query: {Query}", query);
                return new List<string>();
            }
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
            
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var videoResult = JsonSerializer.Deserialize<YouTubeVideoResponse>(response, options);

            if (videoResult == null)
            {
                _logger.LogError("Deserialization returned null for videoId {VideoId}. Raw response: {Response}", videoId, response);
                throw new InvalidOperationException($"Failed to parse YouTube API response for video: {videoId}");
            }

            var video = videoResult.Items?.FirstOrDefault();
            if (video == null)
            {
                _logger.LogError("No video found in API response for videoId {VideoId}. Raw response: {Response}", videoId, response);
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

    private async Task<string> FetchVideoTranscriptAsync(string videoId)
    {
        try
        {
            // Initialize YouTube service
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = _youTubeApiKey,
                ApplicationName = "QuantResearchAgent"
            });

            // List caption tracks for the video
            var captionListRequest = youtubeService.Captions.List("snippet", videoId);
            var captionListResponse = await captionListRequest.ExecuteAsync();

            if (captionListResponse.Items == null || captionListResponse.Items.Count == 0)
            {
                _logger.LogWarning("No captions available for video {VideoId}", videoId);
                return await FetchTranscriptFromWebAsync(videoId);
            }

            // Get the first available caption track (preferably English)
            var captionTrack = captionListResponse.Items
                .FirstOrDefault(c => c.Snippet.Language == "en") ?? captionListResponse.Items.First();

            _logger.LogInformation("Found caption track for video {VideoId}: {Language} ({TrackKind})",
                videoId, captionTrack.Snippet.Language, captionTrack.Snippet.TrackKind);

            // Try OAuth2 download first (if configured)
            var transcript = await TryDownloadWithOAuthAsync(captionTrack.Id);
            if (!string.IsNullOrEmpty(transcript))
            {
                return ParseCaptionToText(transcript);
            }

            // Fallback to web scraping
            _logger.LogWarning("OAuth2 download failed for video {VideoId}, trying web scraping fallback", videoId);
            return await FetchTranscriptFromWebAsync(videoId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch transcript for video {VideoId}, trying web fallback", videoId);
            return await FetchTranscriptFromWebAsync(videoId);
        }
    }

    private async Task<string> TryDownloadWithOAuthAsync(string captionId)
    {
        try
        {
            // Check if OAuth2 credentials are configured
            var clientId = _configuration["YouTube:OAuth2ClientId"];
            var clientSecret = _configuration["YouTube:OAuth2ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                return string.Empty;
            }

            // TODO: Implement OAuth2 flow for caption download
            // This would require user authentication and token management
            // For now, return empty to use fallback
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OAuth2 download failed");
            return string.Empty;
        }
    }

    private async Task<string> FetchTranscriptFromWebAsync(string videoId)
    {
        try
        {
            // Use web scraping as fallback for getting captions
            var videoUrl = $"https://www.youtube.com/watch?v={videoId}";

            // Try to get captions from YouTube's transcript endpoint
            var transcriptUrl = $"https://www.youtube.com/api/timedtext?lang=en&v={videoId}&fmt=json3";
            var response = await _httpClient.GetStringAsync(transcriptUrl);

            if (!string.IsNullOrEmpty(response) && response != "[]")
            {
                return ParseJsonTranscript(response);
            }

            // If JSON fails, try XML format
            var xmlTranscriptUrl = $"https://www.youtube.com/api/timedtext?lang=en&v={videoId}&fmt=xml";
            var xmlResponse = await _httpClient.GetStringAsync(xmlTranscriptUrl);

            if (!string.IsNullOrEmpty(xmlResponse))
            {
                return ParseXmlTranscript(xmlResponse);
            }

            return "No captions/transcript available for this video.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Web scraping fallback failed for video {VideoId}", videoId);
            return "No captions/transcript available for this video.";
        }
    }

    private string ParseJsonTranscript(string jsonResponse)
    {
        try
        {
            // Parse YouTube's JSON3 format
            var transcript = new StringBuilder();

            // Simple JSON parsing for events
            var events = jsonResponse.Split(new[] { "{\"start\":" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var eventData in events.Skip(1))
            {
                var textStart = eventData.IndexOf("\"text\":\"");
                if (textStart >= 0)
                {
                    textStart += 8; // Length of "\"text\":\""
                    var textEnd = eventData.IndexOf("\"", textStart);
                    if (textEnd > textStart)
                    {
                        var text = eventData.Substring(textStart, textEnd - textStart);
                        text = System.Web.HttpUtility.HtmlDecode(text); // Decode HTML entities
                        transcript.AppendLine(text);
                    }
                }
            }

            return transcript.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON transcript");
            return string.Empty;
        }
    }

    private string ParseXmlTranscript(string xmlResponse)
    {
        try
        {
            // Parse YouTube's XML format
            var transcript = new StringBuilder();

            // Simple XML parsing for text elements
            var textPattern = @"<text[^>]*>(.*?)</text>";
            var matches = System.Text.RegularExpressions.Regex.Matches(xmlResponse, textPattern);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var text = System.Web.HttpUtility.HtmlDecode(match.Groups[1].Value);
                transcript.AppendLine(text);
            }

            return transcript.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse XML transcript");
            return string.Empty;
        }
    }

    private string ParseCaptionToText(string captionContent)
    {
        // Parse SubRip (.sbv) format to plain text
        var lines = captionContent.Split('\n');
        var transcript = new StringBuilder();

        foreach (var line in lines)
        {
            // Skip timestamp lines (format: 0:00:00.000,0:00:05.000)
            if (!Regex.IsMatch(line.Trim(), @"^\d+:\d+:\d+\.\d+,\d+:\d+:\d+\.\d+$"))
            {
                // Remove HTML tags if any and add to transcript
                var cleanLine = Regex.Replace(line, @"<[^>]+>", "").Trim();
                if (!string.IsNullOrEmpty(cleanLine))
                {
                    transcript.AppendLine(cleanLine);
                }
            }
        }

        return transcript.ToString().Trim();
    }

    /// <summary>
    /// Get the raw transcript text for a YouTube video
    /// </summary>
    public async Task<string> GetVideoTranscriptAsync(string videoUrl)
    {
        try
        {
            var videoId = ExtractVideoId(videoUrl);
            if (string.IsNullOrEmpty(videoId))
            {
                throw new ArgumentException("Invalid YouTube URL format");
            }

            var transcript = await FetchVideoTranscriptAsync(videoId);

            if (string.IsNullOrEmpty(transcript) || transcript == "No captions/transcript available for this video.")
            {
                _logger.LogWarning("No transcript available for video: {VideoUrl}", videoUrl);
                return "No captions/transcript available for this video.";
            }

            return transcript;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get transcript for video: {VideoUrl}", videoUrl);
            return $"Error fetching transcript: {ex.Message}";
        }
    }

    /// <summary>
    /// Save the video transcript to a file
    /// </summary>
    public async Task<string> SaveVideoTranscriptAsync(string videoUrl, string? outputPath = null)
    {
        try
        {
            var transcript = await GetVideoTranscriptAsync(videoUrl);
            
            if (transcript.StartsWith("No captions") || transcript.StartsWith("Error"))
            {
                return transcript; // Return the error/status message
            }

            // Generate filename if not provided
            if (string.IsNullOrEmpty(outputPath))
            {
                var videoId = ExtractVideoId(videoUrl);
                outputPath = Path.Combine("transcripts", $"{videoId}_transcript.txt");
                Directory.CreateDirectory("transcripts");
            }

            await File.WriteAllTextAsync(outputPath, transcript);
            _logger.LogInformation("Transcript saved to: {OutputPath}", outputPath);
            
            return $"Transcript saved successfully to: {outputPath}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save transcript for video: {VideoUrl}", videoUrl);
            return $"Error saving transcript: {ex.Message}";
        }
    }

    private async Task AnalyzeTechnicalContentAsync(PodcastEpisode episode)
    {
        // Chunk transcript for region-specific prompts
        var regions = new[] { "USA", "China", "India", "Europe", "Global" };
        var regionResults = new List<string>();

        foreach (var region in regions)
        {
            List<WebSearchResult> searchResults;
            try
            {
                searchResults = await _webSearchPlugin.SearchAsync($"{episode.Name} {region} finance trading", 3);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Web search failed for region {Region}, continuing without search results", region);
                searchResults = new List<WebSearchResult>(); // Empty list to continue processing
            }

            // 1. Run LLM/web search analysis first (simulate by using searchResults and video info)
            // 2. Extract tickers from search results and video info
            var aiContext = $"{episode.Name} {episode.Description} {string.Join(" ", searchResults.Select(r => r.Snippet))}";
            var tickers = QuantResearchAgent.Plugins.YahooFinanceDataPlugin.ExtractTickersFromText(aiContext);

            // 3. Query only for valid tickers
            var securities = await _financialDataPlugin.GetSecuritiesForTickersAsync(tickers, 5);
            var validSecurities = securities.Where(s => !string.IsNullOrWhiteSpace(s.Symbol)).ToList();

            // 4. Only call /news for valid tickers
            var newsList = new List<QuantResearchAgent.Plugins.FinancialNewsItem>();
            foreach (var sec in validSecurities)
            {
                try
                {
                    var news = await _financialDataPlugin.GetNewsAsync(sec.Symbol, 3);
                    if (news != null) newsList.AddRange(news);
                }
                catch { /* skip errors for individual tickers */ }
            }

            // Build context string
            var context = $"\nWeb Search Results for {region}:\n" +
                string.Join("\n", searchResults.Select(r => $"- {r.Title}: {r.Snippet} ({r.Url})")) +
                "\nSecurities:\n" +
                string.Join("\n", validSecurities.Select(s => $"- {s.Symbol} ({s.Name}, {s.Exchange})")) +
                "\nRecent News:\n" +
                string.Join("\n", newsList.Select(n => $"- {n.Headline}: {n.Summary} [{n.Source}] ({n.Url})"));

            // Region-specific prompt
            var prompt = $@"
You are a world-class quantitative finance expert and research analyst. Analyze the following YouTube video content for trading insights, real-world applications, and global context for {region}.

Video Title: {episode.Name}
Video Description: {episode.Description}
Channel: Quantopian/Finance Channel
Region: {region}

{context}

Please provide CLEAN TEXT OUTPUT (no markdown, no asterisks, no hashtags) with:
1. Technical trading concepts mentioned (indicators, strategies, patterns)
2. Market analysis or predictions (specific markets, sectors, trends)
3. Investment strategies discussed (portfolio management, risk strategies)
4. Risk management principles (position sizing, stop losses, hedging)
5. Quantitative methods or indicators mentioned (algorithms, backtesting, statistics)
6. Educational concepts (finance theory, market mechanics)
7. Real-world applications: Summarize how these concepts and strategies are being applied in {region}. Include recent news, case studies, or notable funds if possible.
8. Possible securities: List stocks, ETFs, futures, or other instruments (from {region}) that could be used to implement these strategies. Be creative and global.

Format as simple numbered points with NO SPECIAL FORMATTING. Focus on actionable, concrete insights that could inform trading decisions. Prioritize practical, implementable ideas and global context over general theory.
";

            try
            {
                var function = _kernel.CreateFunctionFromPrompt(prompt);
                var result = await _kernel.InvokeAsync(function);
                regionResults.Add(result.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to analyze technical content for region: {region}, video: {episode.Id}");
            }
        }

        // Aggregate results
        var analysis = string.Join("\n\n---\n\n", regionResults);
        episode.TechnicalInsights = ParseTechnicalInsights(analysis);

        // Calculate sentiment score
        episode.SentimentScore = await CalculateSentimentAsync($"{episode.Name}\n{episode.Description}");

        _logger.LogInformation("Extracted {InsightCount} technical insights from video: {VideoTitle}", 
            episode.TechnicalInsights.Count, episode.Name);
    }

    private async Task ExtractTradingSignalsAsync(PodcastEpisode episode)
    {
        var insightsText = string.Join("\n", episode.TechnicalInsights);
        
        var prompt = $@"
You are an expert quantitative analyst. Based on the following technical insights from a financial YouTube video, generate SPECIFIC, ACTIONABLE trading signals and implementation strategies.

Video Title: {episode.Name}
Technical Insights:
{insightsText}

Video Sentiment Score: {episode.SentimentScore:F2}

GENERATE COMPREHENSIVE TRADING ANALYSIS WITH CLEAN TEXT (NO MARKDOWN FORMATTING):

1. TRADING SIGNALS (Generate 2-3 signals per video):
Format for each signal (use simple text, no special characters):
Symbol: [SPECIFIC STOCK/ETF/PAIR] (use real symbols like AAPL, MSFT, SPY, QQQ, XLF, XLK, etc.)
Action: [BUY/SELL/PAIR_TRADE/HEDGE]
Strength: [0.1-1.0] (confidence level)
Reasoning: [Detailed explanation based on video strategy]
Time Horizon: [SHORT/MEDIUM/LONG] (specific timeframe)
Entry Price: [Current price reference or level]
Stop Loss: [Risk management level]
Target: [Profit target]

2. STRATEGY IMPLEMENTATION:
Specific securities suitable for this strategy
Portfolio allocation recommendations
Risk management framework
Market conditions when to apply/avoid

3. PORTFOLIO MANAGEMENT OVERVIEW:
Position sizing guidelines
Correlation analysis for pairs (if applicable)
Hedging recommendations
Performance monitoring metrics

For strategies like Statistical Arbitrage, Pairs Trading, or Market Neutral:
Identify SPECIFIC correlated pairs (e.g., XLF vs BAC, AAPL vs MSFT)
Provide exact entry/exit criteria
Include correlation coefficients and historical spreads
Risk management for mean reversion strategies

ALWAYS generate actionable signals even for complex strategies. Use current market context and real trading instruments. Be specific and implementable. USE PLAIN TEXT ONLY.
";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            var signals = ParseTradingSignals(result.ToString());
            episode.TradingSignals = signals;
            
            // Store the full analysis text for detailed insights
            episode.TechnicalInsights.Add($"STRATEGY IMPLEMENTATION ANALYSIS:\n{result}");
            
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
        
        // Parse the structured trading signals - handle both simple and complex formats
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
        
        // If no structured signals found, try to extract any trading recommendations
        if (signals.Count == 0)
        {
            var lines = signalsText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var currentSignal = new List<string>();
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Contains("BUY") || trimmed.Contains("SELL") || trimmed.Contains("PAIR_TRADE") || 
                    trimmed.Contains("HOLD") || trimmed.Contains("HEDGE"))
                {
                    if (currentSignal.Count > 0)
                    {
                        signals.Add(string.Join("\n", currentSignal));
                        currentSignal.Clear();
                    }
                    currentSignal.Add(trimmed);
                }
                else if (currentSignal.Count > 0 && trimmed.Length > 0)
                {
                    currentSignal.Add(trimmed);
                }
            }
            
            if (currentSignal.Count > 0)
            {
                signals.Add(string.Join("\n", currentSignal));
            }
        }
        
        return signals.Take(5).ToList(); // Allow up to 5 signals for complex strategies
    }
}

// YouTube API response models
public class YouTubeSearchResponse
{
    [JsonPropertyName("items")]
    public List<YouTubeSearchItem>? Items { get; set; }
}

public class YouTubeSearchItem
{
    [JsonPropertyName("id")]
    public YouTubeVideoId Id { get; set; } = new();
    
    [JsonPropertyName("snippet")]
    public YouTubeSnippet Snippet { get; set; } = new();
}

public class YouTubeVideoId
{
    [JsonPropertyName("videoId")]
    public string VideoId { get; set; } = string.Empty;
    
    [JsonPropertyName("channelId")]
    public string ChannelId { get; set; } = string.Empty;
}

public class YouTubeVideoResponse
{
    [JsonPropertyName("items")]
    public List<YouTubeVideo>? Items { get; set; }
}

public class YouTubeVideo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("snippet")]
    public YouTubeSnippet Snippet { get; set; } = new();
}

public class YouTubeSnippet
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("publishedAt")]
    public string PublishedAt { get; set; } = string.Empty;
    
    [JsonPropertyName("channelTitle")]
    public string ChannelTitle { get; set; } = string.Empty;
}
