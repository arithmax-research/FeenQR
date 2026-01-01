using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResearchController : ControllerBase
    {
        private readonly ILogger<ResearchController> _logger;
        private readonly ConversationalResearchService _conversationalResearchService;
        private readonly AcademicResearchService _academicResearchService;
        private readonly YouTubeAnalysisService _youtubeAnalysisService;
        private readonly ReportGenerationService _reportGenerationService;

        public ResearchController(
            ILogger<ResearchController> logger,
            ConversationalResearchService conversationalResearchService,
            AcademicResearchService academicResearchService,
            YouTubeAnalysisService youtubeAnalysisService,
            ReportGenerationService reportGenerationService)
        {
            _logger = logger;
            _conversationalResearchService = conversationalResearchService;
            _academicResearchService = academicResearchService;
            _youtubeAnalysisService = youtubeAnalysisService;
            _reportGenerationService = reportGenerationService;
        }

        // AI Assistant endpoints
        [HttpPost("quick-research")]
        public async Task<IActionResult> QuickResearch([FromBody] ResearchRequest request)
        {
            try
            {
                var result = await _conversationalResearchService.ExecuteResearchQueryAsync(request.Query);
                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "quick-research"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in quick research");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("research-llm")]
        public async Task<IActionResult> ResearchLLM([FromBody] ResearchRequest request)
        {
            try
            {
                var result = await _conversationalResearchService.ExecuteResearchQueryAsync(request.Query);
                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "research-llm"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in research LLM");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("research-query")]
        public async Task<IActionResult> ResearchQuery([FromBody] ResearchRequest request)
        {
            try
            {
                var result = await _conversationalResearchService.ExecuteResearchQueryAsync(request.Query);
                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "research-query"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in research query");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Video Analysis endpoints
        [HttpPost("analyze-video")]
        public async Task<IActionResult> AnalyzeVideo([FromBody] VideoAnalysisRequest request)
        {
            try
            {
                var podcastResult = await _youtubeAnalysisService.AnalyzeVideoAsync(request.VideoUrl);
                
                // Extract URLs from insights and signals
                var allText = string.Join(" ", podcastResult.TechnicalInsights.Concat(podcastResult.TradingSignals));
                var urlPattern = @"https?://[^\s]+";
                var urls = System.Text.RegularExpressions.Regex.Matches(allText, urlPattern)
                    .Select(m => m.Value)
                    .Distinct()
                    .ToList();

                var result = $"Video: {podcastResult.Name}\n" +
                           $"Description: {podcastResult.Description}\n" +
                           $"Published: {podcastResult.PublishedDate:yyyy-MM-dd}\n" +
                           $"Sentiment: {podcastResult.SentimentScore:F2}\n\n" +
                           $"Technical Insights:\n{string.Join("\n", podcastResult.TechnicalInsights)}\n\n" +
                           $"Trading Signals:\n{string.Join("\n", podcastResult.TradingSignals)}";

                // Append links at the end if found
                if (urls.Any())
                {
                    result += $"\n\n===== LINKS FOUND IN ANALYSIS =====\n";
                    result += $"The following URLs were discovered during analysis:\n\n";
                    result += string.Join("\n", urls.Select((url, i) => $"{i + 1}. {url}"));
                    result += $"\n\nTip: You can parse these URLs for deeper analysis using our URL analyzer feature.";
                }
                
                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "video-analysis"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing video");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("quantopian-videos")]
        public async Task<IActionResult> GetQuantopianVideos()
        {
            try
            {
                var videos = await _youtubeAnalysisService.GetLatestQuantopianVideosAsync(20);
                var result = $"Found {videos.Count} Quantopian videos:\n\n{string.Join("\n", videos)}";
                
                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "quantopian-videos"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Quantopian videos");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("search-finance-videos")]
        public async Task<IActionResult> SearchFinanceVideos([FromBody] VideoSearchRequest request)
        {
            try
            {
                var videos = await _youtubeAnalysisService.SearchFinanceVideosAsync(request.Query, request.MaxResults);
                
                var result = $"Found {videos.Count} finance videos:\n\n";
                result += "═══════════════════════════════════════════════════════════════\n\n";
                
                for (int i = 0; i < videos.Count; i++)
                {
                    var video = videos[i];
                    result += $"VIDEO {i + 1}:\n";
                    result += $"Title: {video.Title}\n";
                    result += $"Link:  {video.Url}\n";
                    result += $"Description: {video.Snippet}\n";
                    result += "\n───────────────────────────────────────────────────────────────\n\n";
                }
                
                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "finance-video-search"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching finance videos");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Academic Papers endpoints
        [HttpPost("research-papers")]
        public async Task<IActionResult> SearchResearchPapers([FromBody] PaperSearchRequest request)
        {
            try
            {
                // Use literature review generation as a proxy for paper search
                var literatureReview = await _academicResearchService.GenerateLiteratureReviewAsync(request.Query, request.MaxResults);
                
                var result = $"ACADEMIC PAPERS SEARCH: {literatureReview.Topic.ToUpper()}\n";
                result += "═══════════════════════════════════════════════════════════════\n\n";
                result += $"Papers analyzed: {literatureReview.PaperAnalyses.Count}\n\n";
                
                if (literatureReview.PaperAnalyses.Any())
                {
                    result += "KEY FINDINGS:\n\n";
                    for (int i = 0; i < Math.Min(10, literatureReview.PaperAnalyses.Count); i++)
                    {
                        var paper = literatureReview.PaperAnalyses[i];
                        result += $"{i + 1}. {paper.PaperTitle}\n";
                        if (!string.IsNullOrWhiteSpace(paper.KeyFindings))
                        {
                            result += $"   Key Findings: {paper.KeyFindings}\n";
                        }
                        result += "\n───────────────────────────────────────────────────────────────\n\n";
                    }
                }
                
                if (!string.IsNullOrWhiteSpace(literatureReview.Synthesis))
                {
                    result += $"\n\nSYNTHESIS:\n{literatureReview.Synthesis}";
                }
                
                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "paper-search"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching research papers");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("analyze-paper")]
        public async Task<IActionResult> AnalyzePaper([FromBody] PaperAnalysisRequest request)
        {
            try
            {
                var strategy = await _academicResearchService.ExtractStrategyFromPaperAsync(request.PaperUrl, "Strategy Analysis");
                var result = $"Paper Strategy Analysis\n\n" +
                           $"Strategy: {strategy.Name}\n" +
                           $"Description: {strategy.Description}\n\n" +
                           $"Implementation:\n{strategy.Implementation}";
                
                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "paper-analysis"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing paper");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("research-synthesis")]
        public async Task<IActionResult> ResearchSynthesis([FromBody] SynthesisRequest request)
        {
            try
            {
                var network = await _academicResearchService.BuildCitationNetworkAsync(request.Topic, request.MaxPapers);
                
                var result = $"CITATION NETWORK ANALYSIS: {network.Topic.ToUpper()}\n";
                result += "═══════════════════════════════════════════════════════════════\n\n";
                result += $"Papers analyzed: {network.Papers.Count}\n\n";
                
                if (network.Papers.Any())
                {
                    result += "CENTRAL PAPERS:\n\n";
                    for (int i = 0; i < Math.Min(10, network.Papers.Count); i++)
                    {
                        var paper = network.Papers[i];
                        result += $"{i + 1}. {paper.Title}\n";
                        if (paper.Authors.Any())
                        {
                            result += $"   Authors: {string.Join(", ", paper.Authors)}\n";
                        }
                        if (paper.PublicationYear > 0)
                        {
                            result += $"   Year: {paper.PublicationYear}\n";
                        }
                        if (!string.IsNullOrWhiteSpace(paper.Journal))
                        {
                            result += $"   Journal: {paper.Journal}\n";
                        }
                        result += "\n───────────────────────────────────────────────────────────────\n\n";
                    }
                }
                
                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "research-synthesis"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synthesizing research");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Research Reports endpoints
        [HttpPost("research-report")]
        public async Task<IActionResult> GenerateResearchReport([FromBody] ReportRequest request)
        {
            try
            {
                var reportType = request.ReportType.ToLower() == "technical" 
                    ? ReportType.TechnicalAnalysis 
                    : request.ReportType.ToLower() == "earnings"
                    ? ReportType.EarningsAnalysis
                    : ReportType.StockAnalysis;
                    
                var report = await _reportGenerationService.GenerateComprehensiveReportAsync(request.Symbol, reportType);
                
                return Ok(new ResearchResponse
                {
                    Result = report.MarkdownContent,
                    Timestamp = DateTime.UtcNow,
                    Type = "research-report"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating research report");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("report-library")]
        public IActionResult GetReportLibrary()
        {
            try
            {
                // Get list of generated reports from disk
                var reportsPath = Path.Combine(Directory.GetCurrentDirectory(), "Reports");
                if (!Directory.Exists(reportsPath))
                {
                    return Ok(new { reports = new List<object>() });
                }

                var reports = Directory.GetFiles(reportsPath)
                    .Select(f => new
                    {
                        Name = Path.GetFileName(f),
                        Path = f,
                        Created = System.IO.File.GetCreationTime(f),
                        Size = new FileInfo(f).Length
                    })
                    .OrderByDescending(r => r.Created)
                    .ToList();

                return Ok(new { reports });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting report library");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    // Request/Response models
    public class ResearchRequest
    {
        public string Query { get; set; } = string.Empty;
    }

    public class VideoAnalysisRequest
    {
        public string VideoUrl { get; set; } = string.Empty;
    }

    public class VideoSearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public int MaxResults { get; set; } = 10;
    }

    public class PaperSearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public int MaxResults { get; set; } = 20;
    }

    public class PaperAnalysisRequest
    {
        public string PaperUrl { get; set; } = string.Empty;
    }

    public class SynthesisRequest
    {
        public string Topic { get; set; } = string.Empty;
        public List<string> PaperUrls { get; set; } = new();
        public int MaxPapers { get; set; } = 10;
    }

    public class ReportRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public string ReportType { get; set; } = "comprehensive";
    }

    public class ResearchResponse
    {
        public string Result { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}
