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
    private readonly string _openAiApiKey;
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
        _openAiApiKey = _configuration["OpenAI:ApiKey"] ?? string.Empty;
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
            
            // Use Python script for transcript (includes yt-dlp fallback)
            var transcript = await FetchTranscriptWithPythonAsync(videoId);
            if (!string.IsNullOrEmpty(transcript) && transcript.Length > 100)
            {
                episode.Transcript = transcript;
                _logger.LogInformation("Successfully fetched transcript for video: {VideoTitle} ({Length} chars)", 
                    episode.Name, transcript.Length);
            }
            else
            {
                _logger.LogWarning("No transcript available for video: {VideoTitle}", episode.Name);
                episode.Transcript = "No transcript available for this video.";
            }
            
            // Detect if video is finance/trading related
            var isFinanceRelated = await DetectFinanceContentAsync(episode);
            _logger.LogInformation("Video finance relevance for '{VideoTitle}': {IsFinance}", episode.Name, isFinanceRelated);
            
            // Analyze the content based on type
            try
            {
                if (isFinanceRelated)
                {
                    await AnalyzeTechnicalContentAsync(episode);
                    await ExtractTradingSignalsAsync(episode);
                }
                else
                {
                    await AnalyzeGeneralContentAsync(episode);
                    episode.TradingSignals = new List<string>(); // No trading signals for non-finance content
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Content analysis failed for video {VideoTitle}, using fallback", episode.Name);
                
                // Provide basic fallback analysis
                episode.TechnicalInsights = new List<string>
                {
                    $"Video: {episode.Name}",
                    $"Description: {episode.Description}",
                    "Note: Detailed analysis unavailable. The video may not contain trading/finance content."
                };
                episode.TradingSignals = new List<string>();
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
    public async Task<List<WebSearchResult>> SearchFinanceVideosAsync(string query, int maxResults = 5)
    {
        try
        {
            // First try YouTube API search
            var encodedQuery = Uri.EscapeDataString($"{query} trading finance quantitative");
            var url = $"{YOUTUBE_API_BASE}/search?part=snippet&q={encodedQuery}&type=video&order=relevance&maxResults={maxResults}&key={_youTubeApiKey}";
            
            var response = await _httpClient.GetStringAsync(url);
            var searchResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(response);
            
            var videoResults = searchResult?.Items?.Select(item => new WebSearchResult
            {
                Title = item.Snippet.Title ?? "No Title",
                Url = $"https://www.youtube.com/watch?v={item.Id.VideoId}",
                Snippet = item.Snippet.Description ?? "No Description"
            }).ToList() ?? new List<WebSearchResult>();
            
            if (videoResults.Any())
            {
                _logger.LogInformation("Found {Count} finance videos for query: {Query} via YouTube API", videoResults.Count, query);
                return videoResults;
            }
            
            _logger.LogWarning("No videos found via YouTube API for {Query}, trying Google Search fallback", query);
            
            // Fallback to Google Search for YouTube videos
            var googleQuery = $"site:youtube.com {query} trading finance investment analysis";
            var webResults = await _webSearchPlugin.SearchAsync(googleQuery, maxResults);
            
            var youtubeResults = webResults
                .Where(r => r.Url.Contains("youtube.com/watch"))
                .Take(maxResults)
                .ToList();
            
            if (youtubeResults.Any())
            {
                _logger.LogInformation("Found {Count} finance videos for query: {Query} via Google Search", youtubeResults.Count, query);
                return youtubeResults;
            }
            
            _logger.LogWarning("No finance videos found for query: {Query} in both YouTube API and Google Search", query);
            return new List<WebSearchResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search finance videos for query: {Query}", query);
            
            // Try Google Search as final fallback
            try
            {
                var googleQuery = $"site:youtube.com {query} trading finance";
                var webResults = await _webSearchPlugin.SearchAsync(googleQuery, maxResults);
                var youtubeResults = webResults
                    .Where(r => r.Url.Contains("youtube.com/watch"))
                    .Take(maxResults)
                    .ToList();
                
                _logger.LogInformation("Fallback Google Search found {Count} videos for {Query}", youtubeResults.Count, query);
                return youtubeResults;
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Google Search fallback also failed for query: {Query}", query);
                return new List<WebSearchResult>();
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
                PublishedDate = video.Snippet.PublishedAt,
                PodcastUrl = $"https://www.youtube.com/watch?v={videoId}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch video data from YouTube for video ID: {VideoId}", videoId);
            throw;
        }
    }

    /// <summary>
    /// Fetch transcript using Python youtube-transcript-api (most reliable method)
    /// </summary>
    private async Task<string> FetchTranscriptWithPythonAsync(string videoId)
    {
        // Use yt-dlp directly since youtube-transcript-api gets IP-blocked
        return await FetchTranscriptWithYtDlpAsync(videoId);
    }

    private async Task<string> FetchTranscriptWithYtDlpAsync(string videoId)
    {
        try
        {
            // Try multiple possible script locations
            var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Scripts", "get_youtube_transcript_ytdlp.py");
            if (!File.Exists(scriptPath))
            {
                // Docker container location
                scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "get_youtube_transcript_ytdlp.py");
            }
            if (!File.Exists(scriptPath))
            {
                // Local development fallback
                scriptPath = "/home/misango/codechest/FeenQR/Scripts/get_youtube_transcript_ytdlp.py";
            }

            if (!File.Exists(scriptPath))
            {
                _logger.LogWarning("yt-dlp transcript script not found at any expected location. BaseDirectory: {BaseDir}", 
                    AppDomain.CurrentDomain.BaseDirectory);
                return string.Empty;
            }

            _logger.LogDebug("Using transcript script at: {ScriptPath}", scriptPath);

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python3",
                    Arguments = $"\"{scriptPath}\" {videoId}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("yt-dlp stderr: {Error}", error);
            }

            if (process.ExitCode == 0)
            {
                var result = JsonSerializer.Deserialize<PythonTranscriptResult>(output);
                if (result?.Success == true && !string.IsNullOrEmpty(result.Transcript))
                {
                    _logger.LogInformation("Successfully fetched transcript using yt-dlp ({Length} chars)", result.Length);
                    return result.Transcript;
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch transcript using yt-dlp for video {VideoId}", videoId);
            return string.Empty;
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

            // Use Python script for transcript (includes yt-dlp fallback)
            var transcript = await FetchTranscriptWithPythonAsync(videoId);
            
            if (string.IsNullOrEmpty(transcript))
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
    /// Get video metadata (title, description, etc.)
    /// </summary>
    public async Task<PodcastEpisode> GetVideoMetadataAsync(string videoId)
    {
        try
        {
            var url = $"{YOUTUBE_API_BASE}/videos?part=snippet,contentDetails&id={videoId}&key={_youTubeApiKey}";
            var response = await _httpClient.GetStringAsync(url);
            var videoResponse = JsonSerializer.Deserialize<YouTubeVideoResponse>(response);

            if (videoResponse?.Items?.Any() == true)
            {
                var video = videoResponse.Items[0];
                return new PodcastEpisode
                {
                    Name = video.Snippet?.Title ?? "Unknown",
                    Description = video.Snippet?.Description ?? "",
                    PodcastUrl = $"https://www.youtube.com/watch?v={videoId}",
                    PublishedDate = video.Snippet?.PublishedAt ?? DateTime.MinValue
                };
            }

            return new PodcastEpisode { Name = "Unknown", PodcastUrl = $"https://www.youtube.com/watch?v={videoId}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get video metadata for: {VideoId}", videoId);
            return new PodcastEpisode { Name = "Unknown", PodcastUrl = $"https://www.youtube.com/watch?v={videoId}" };
        }
    }

    /// <summary>
    /// Parse YouTube duration format (PT1H2M3S) to TimeSpan
    /// </summary>
    private TimeSpan ParseDuration(string duration)
    {
        try
        {
            if (string.IsNullOrEmpty(duration)) return TimeSpan.Zero;
            
            var match = Regex.Match(duration, @"PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?");
            if (!match.Success) return TimeSpan.Zero;

            var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            var minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            var seconds = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

            return new TimeSpan(hours, minutes, seconds);
        }
        catch
        {
            return TimeSpan.Zero;
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

    /// <summary>
    /// Detect if video content is finance/trading related
    /// </summary>
    private async Task<bool> DetectFinanceContentAsync(PodcastEpisode episode)
    {
        try
        {
            var contentSample = $"{episode.Name} {episode.Description} ";
            if (!string.IsNullOrEmpty(episode.Transcript) && episode.Transcript.Length > 1000)
            {
                contentSample += episode.Transcript.Substring(0, 1000);
            }
            
            var prompt = @$"
Analyze if this YouTube video is related to finance, trading, investing, economics, or business.

Title: {episode.Name}
Description: {episode.Description}
Content Sample: {contentSample.Substring(0, Math.Min(contentSample.Length, 1500))}

Respond with ONLY 'YES' if the video discusses:
- Stock trading, investing, markets
- Finance, economics, business
- Crypto, forex, commodities
- Portfolio management, risk
- Financial news, analysis
- Quantitative finance, algorithms
- Personal finance, wealth

Respond with ONLY 'NO' if the video is about:
- Entertainment, gaming, sports
- Technology tutorials (non-finance)
- Lifestyle, cooking, travel
- General education (non-business)
- Other non-financial topics

Answer (YES or NO):";

            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            var response = result.ToString().Trim().ToUpper();
            
            return response.Contains("YES");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect finance content, assuming YES");
            return true; // Default to finance analysis if detection fails
        }
    }

    /// <summary>
    /// Analyze general (non-finance) video content
    /// </summary>
    private async Task AnalyzeGeneralContentAsync(PodcastEpisode episode)
    {
        var transcriptPreview = !string.IsNullOrEmpty(episode.Transcript) && episode.Transcript.Length > 5000 
            ? episode.Transcript.Substring(0, 5000) + "...[truncated]" 
            : episode.Transcript ?? "No transcript available";

        var prompt = @$"
You are an expert content analyst. Provide a comprehensive summary of this YouTube video.

Video Title: {episode.Name}
Description: {episode.Description}

TRANSCRIPT:
{transcriptPreview}

Provide a clear, structured analysis with:

1. MAIN TOPIC & PURPOSE
What is this video about? What is the creator trying to achieve?

2. KEY POINTS & INSIGHTS
List the 5-7 most important points, facts, or takeaways from the video.
Include direct quotes when relevant.

3. TARGET AUDIENCE
Who is this video for? What background knowledge is assumed?

4. PRACTICAL VALUE
What can viewers learn or apply from this content?
Any actionable advice or recommendations?

5. CONTENT QUALITY
Production quality, clarity of explanation, credibility of information.

6. BUSINESS/PROFESSIONAL ANGLE (if applicable)
If there are ANY business, career, or professional development aspects, 
mention them here. Otherwise, state 'Not applicable.'

Format as plain text with clear section headers. Be concise but informative.";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            episode.TechnicalInsights = new List<string>
            {
                "=== GENERAL CONTENT ANALYSIS ===",
                result.ToString(),
                $"\nNote: This video does not contain trading/finance content. For financial analysis, try videos about stocks, trading, investing, or economics."
            };
            
            // Calculate sentiment for general content
            episode.SentimentScore = await CalculateSentimentAsync($"{episode.Name}\n{episode.Description}");
            
            _logger.LogInformation("Completed general content analysis for: {VideoTitle}", episode.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze general content for video: {VideoId}", episode.Id);
            throw;
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
            var transcriptPreview = !string.IsNullOrEmpty(episode.Transcript) && episode.Transcript.Length > 8000 
                ? episode.Transcript.Substring(0, 8000) + "...[transcript truncated]" 
                : episode.Transcript ?? "No transcript available";

            var prompt = $@"
You are a world-class quantitative finance expert and research analyst. Analyze the following YouTube video content for trading insights, real-world applications, and global context for {region}.

Video Title: {episode.Name}
Video Description: {episode.Description}
Channel: Quantopian/Finance Channel
Region: {region}

VIDEO TRANSCRIPT:
{transcriptPreview}

{context}

Please provide CLEAN TEXT OUTPUT (no markdown, no asterisks, no hashtags) with:
1. Technical trading concepts mentioned (indicators, strategies, patterns) - CITE SPECIFIC QUOTES from the transcript
2. Market analysis or predictions (specific markets, sectors, trends) - INCLUDE DIRECT QUOTES
3. Investment strategies discussed (portfolio management, risk strategies) - CITE SPEAKER'S EXACT WORDS
4. Risk management principles (position sizing, stop losses, hedging)
5. Quantitative methods or indicators mentioned (algorithms, backtesting, statistics)
6. Educational concepts (finance theory, market mechanics)
7. Key quotes and sentiments: Extract 3-5 important quotes from the transcript that reveal market sentiment, predictions, or policy changes
8. Real-world applications: Summarize how these concepts and strategies are being applied in {region}. Include recent news, case studies, or notable funds if possible.
9. Possible securities: List stocks, ETFs, futures, or other instruments (from {region}) that could be used to implement these strategies. Be creative and global.

Format as simple numbered points with NO SPECIAL FORMATTING. Focus on actionable, concrete insights that could inform trading decisions. ALWAYS CITE DIRECT QUOTES when available to support analysis. Prioritize practical, implementable ideas and global context over general theory.
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
        var transcriptContext = !string.IsNullOrEmpty(episode.Transcript) && episode.Transcript.Length > 3000
            ? "\n\nKEY TRANSCRIPT EXCERPTS:\n" + episode.Transcript.Substring(0, 3000) + "..."
            : "";
        
        var prompt = $@"
You are an expert quantitative analyst. Based on the following technical insights from a financial YouTube video, generate SPECIFIC, ACTIONABLE trading signals and implementation strategies.

Video Title: {episode.Name}
Technical Insights:
{insightsText}
{transcriptContext}

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

    /// <summary>
    /// Transcribe YouTube video audio using OpenAI Whisper API
    /// </summary>
    private async Task<string> TranscribeVideoWithWhisperAsync(string videoId)
    {
        try
        {
            if (string.IsNullOrEmpty(_openAiApiKey))
            {
                _logger.LogWarning("OpenAI API key not configured, cannot transcribe audio");
                return string.Empty;
            }

            // Download audio from YouTube video
            var audioFilePath = await DownloadVideoAudioAsync(videoId);
            if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath))
            {
                _logger.LogWarning("Failed to download audio for video {VideoId}", videoId);
                return string.Empty;
            }

            try
            {
                // Call OpenAI Whisper API for transcription
                var transcript = await TranscribeAudioWithWhisperAsync(audioFilePath);
                return transcript;
            }
            finally
            {
                // Cleanup: delete the temporary audio file
                try
                {
                    if (File.Exists(audioFilePath))
                    {
                        File.Delete(audioFilePath);
                        _logger.LogInformation("Deleted temporary audio file: {FilePath}", audioFilePath);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to delete temporary audio file: {FilePath}", audioFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transcribe video audio for video {VideoId}", videoId);
            return string.Empty;
        }
    }

    /// <summary>
    /// Download audio from YouTube video using yt-dlp
    /// </summary>
    private async Task<string> DownloadVideoAudioAsync(string videoId)
    {
        try
        {
            var videoUrl = $"https://www.youtube.com/watch?v={videoId}";
            var outputDir = Path.Combine(Path.GetTempPath(), "youtube_audio");
            Directory.CreateDirectory(outputDir);
            
            var outputPath = Path.Combine(outputDir, $"{videoId}.mp3");

            // Check if yt-dlp is available
            var ytDlpPath = await FindYtDlpExecutableAsync();
            if (string.IsNullOrEmpty(ytDlpPath))
            {
                _logger.LogWarning("yt-dlp not found, cannot download audio. Install with: pip install yt-dlp");
                return string.Empty;
            }

            // Use yt-dlp to download audio
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ytDlpPath,
                    Arguments = $"-x --audio-format mp3 --audio-quality 5 -o \\\"{outputPath}\\\" \\\"{videoUrl}\\\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _logger.LogInformation("Downloading audio for video {VideoId}...", videoId);
            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                _logger.LogError("yt-dlp failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, error);
                return string.Empty;
            }

            if (File.Exists(outputPath))
            {
                _logger.LogInformation("Successfully downloaded audio to {OutputPath} ({Size} bytes)", 
                    outputPath, new FileInfo(outputPath).Length);
                return outputPath;
            }

            _logger.LogWarning("Audio file not found after download: {OutputPath}", outputPath);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download video audio for {VideoId}", videoId);
            return string.Empty;
        }
    }

    /// <summary>
    /// Find yt-dlp executable in PATH or common locations
    /// </summary>
    private async Task<string> FindYtDlpExecutableAsync()
    {
        var possibleNames = new[] { "yt-dlp", "yt-dlp.exe" };
        
        foreach (var name in possibleNames)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "which",
                        Arguments = name,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    return output.Trim();
                }
            }
            catch { /* Continue to next attempt */ }
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Transcribe audio file using OpenAI Whisper API
    /// </summary>
    private async Task<string> TranscribeAudioWithWhisperAsync(string audioFilePath)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            
            // Read audio file
            var fileBytes = await File.ReadAllBytesAsync(audioFilePath);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
            
            form.Add(fileContent, "file", Path.GetFileName(audioFilePath));
            form.Add(new StringContent("whisper-1"), "model");
            form.Add(new StringContent("text"), "response_format");

            // Call OpenAI Whisper API
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions")
            {
                Content = form
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _openAiApiKey);

            _logger.LogInformation("Sending audio to Whisper API for transcription ({Size} bytes)...", fileBytes.Length);
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Whisper API failed with status {StatusCode}: {Error}", 
                    response.StatusCode, errorContent);
                return string.Empty;
            }

            var transcript = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Successfully transcribed audio ({Length} characters)", transcript.Length);
            
            return transcript;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transcribe audio with Whisper API");
            return string.Empty;
        }
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

    [JsonPropertyName("contentDetails")]
    public YouTubeContentDetails? ContentDetails { get; set; }
}

public class YouTubeContentDetails
{
    [JsonPropertyName("duration")]
    public string Duration { get; set; } = string.Empty;
}

public class YouTubeSnippet
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("publishedAt")]
    public DateTime PublishedAt { get; set; }
    
    [JsonPropertyName("channelTitle")]
    public string ChannelTitle { get; set; } = string.Empty;
}

// Python transcript API response model
public class PythonTranscriptResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("video_id")]
    public string VideoId { get; set; } = string.Empty;
    
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;
    
    [JsonPropertyName("language_code")]
    public string LanguageCode { get; set; } = string.Empty;
    
    [JsonPropertyName("is_generated")]
    public bool IsGenerated { get; set; }
    
    [JsonPropertyName("transcript")]
    public string Transcript { get; set; } = string.Empty;
    
    [JsonPropertyName("length")]
    public int Length { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
