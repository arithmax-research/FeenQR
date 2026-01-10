using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;
using System.Text;
using Microsoft.SemanticKernel;

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
        private readonly LinkedInScrapingService _linkedInScrapingService;

        public ResearchController(
            ILogger<ResearchController> logger,
            ConversationalResearchService conversationalResearchService,
            AcademicResearchService academicResearchService,
            YouTubeAnalysisService youtubeAnalysisService,
            ReportGenerationService reportGenerationService,
            LinkedInScrapingService linkedInScrapingService)
        {
            _logger = logger;
            _conversationalResearchService = conversationalResearchService;
            _academicResearchService = academicResearchService;
            _youtubeAnalysisService = youtubeAnalysisService;
            _reportGenerationService = reportGenerationService;
            _linkedInScrapingService = linkedInScrapingService;
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

        // LinkedIn Scraper endpoint
        [HttpPost("linkedin-scrape")]
        public async Task<IActionResult> ScrapeLinkedInPosts([FromBody] LinkedInScrapeRequest request)
        {
            try
            {
                var posts = await _linkedInScrapingService.ScrapePostsAsync(request.Url);
                return Ok(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping LinkedIn posts from {Url}", request.Url);
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
        [HttpPost("quick-insights")]
        public async Task<IActionResult> GetQuickInsights([FromBody] QuickInsightsRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Url))
                {
                    return Ok(new ResearchResponse
                    {
                        Result = "Paper URL is required",
                        Timestamp = DateTime.UtcNow,
                        Type = "quick-insights"
                    });
                }

                _logger.LogInformation("Getting quick insights for paper: {Url}", request.Url);

                // Get quick insights from paper (lighter analysis)
                var insights = await _academicResearchService.GetQuickInsightsAsync(
                    request.Url,
                    request.Title ?? "Research Paper"
                );

                var result = $"QUICK INSIGHTS: {request.Title ?? "Research Paper"}\n";
                result += "═══════════════════════════════════════════════════════════════\n\n";
                result += $"Source: {request.Url}\n\n";
                result += insights;
                result += $"\n\nGenerated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n";

                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "quick-insights"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quick insights for {Url}", request.Url);
                return Ok(new ResearchResponse
                {
                    Result = $"Error getting insights: {ex.Message}\n\nThis could be due to:\n- PDF download failed\n- Paper is behind paywall\n- Network timeout\n- PDF is scanned/image-based",
                    Timestamp = DateTime.UtcNow,
                    Type = "quick-insights"
                });
            }
        }

        [HttpPost("deep-analyze-paper")]
        public async Task<IActionResult> DeepAnalyzePaper([FromBody] DeepAnalyzeRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Url))
                {
                    return Ok(new ResearchResponse
                    {
                        Result = "Paper URL is required",
                        Timestamp = DateTime.UtcNow,
                        Type = "deep-analysis"
                    });
                }

                _logger.LogInformation("Deep analyzing paper: {Url}", request.Url);

                // Extract strategy from paper URL (downloads PDF and analyzes)
                var strategy = await _academicResearchService.ExtractStrategyFromPaperAsync(
                    request.Url,
                    request.StrategyName ?? "Extracted Strategy"
                );

                var result = $"DEEP ANALYSIS: {request.Title ?? "Research Paper"}\n";
                result += "═══════════════════════════════════════════════════════════════\n\n";
                result += $"Source: {request.Url}\n\n";
                result += $"Strategy Name: {strategy.Name}\n\n";
                result += $"Description: {strategy.Description}\n\n";
                result += "IMPLEMENTATION:\n";
                result += "───────────────────────────────────────────────────────────────\n";
                result += $"{strategy.Implementation}\n\n";
                result += $"Analyzed: {strategy.ExtractionDate:yyyy-MM-dd HH:mm:ss} UTC\n";

                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "deep-analysis"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in deep paper analysis for {Url}", request.Url);
                return Ok(new ResearchResponse
                {
                    Result = $"Error analyzing paper: {ex.Message}\n\nThis could be due to:\n- PDF download failed\n- Paper is behind paywall\n- Network timeout\n- PDF is scanned/image-based",
                    Timestamp = DateTime.UtcNow,
                    Type = "deep-analysis"
                });
            }
        }

        [HttpPost("analyze-pdf-quick")]
        public async Task<IActionResult> AnalyzePdfQuick(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Ok(new ResearchResponse
                    {
                        Result = "No file uploaded",
                        Timestamp = DateTime.UtcNow,
                        Type = "pdf-quick-insights"
                    });
                }

                if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(new ResearchResponse
                    {
                        Result = "Only PDF files are supported",
                        Timestamp = DateTime.UtcNow,
                        Type = "pdf-quick-insights"
                    });
                }

                // Read PDF content
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var pdfBytes = memoryStream.ToArray();

                // Extract text from PDF using PdfPig
                string pdfText;
                using (var document = UglyToad.PdfPig.PdfDocument.Open(pdfBytes))
                {
                    var textBuilder = new StringBuilder();
                    // Only read first 10 pages for quick insights
                    var pagesToRead = Math.Min(10, document.NumberOfPages);
                    for (int i = 1; i <= pagesToRead; i++)
                    {
                        var page = document.GetPage(i);
                        textBuilder.AppendLine($"\n--- Page {page.Number} ---");
                        textBuilder.AppendLine(page.Text);
                    }
                    pdfText = textBuilder.ToString();
                }

                if (string.IsNullOrWhiteSpace(pdfText))
                {
                    return Ok(new ResearchResponse
                    {
                        Result = "Could not extract text from PDF. The file might be scanned/image-based.",
                        Timestamp = DateTime.UtcNow,
                        Type = "pdf-quick-insights"
                    });
                }

                // Limit to 6000 chars for quick analysis
                var limitedText = pdfText.Substring(0, Math.Min(6000, pdfText.Length));

                // Get quick insights using AI
                var prompt = $@"
Analyze this research paper and provide concise insights:

Paper: {file.FileName}

Content Preview:
{limitedText}

Provide:
1. MAIN FINDINGS (2-3 key takeaways)
2. METHODOLOGY (brief overview of approach)
3. KEY RESULTS (important metrics, outcomes)
4. PRACTICAL APPLICATIONS (how this can be used)
5. LIMITATIONS (if mentioned)

Keep it concise and actionable. Focus on what's useful for quantitative research.";

                var kernel = HttpContext.RequestServices.GetRequiredService<Kernel>();
                var insights = await kernel.InvokePromptAsync(prompt);

                var result = $"QUICK INSIGHTS: {file.FileName}\n";
                result += "═══════════════════════════════════════════════════════════════\n\n";
                result += insights.ToString();
                result += $"\n\nGenerated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n";

                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "pdf-quick-insights"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quick insights from PDF");
                return Ok(new ResearchResponse
                {
                    Result = $"Error analyzing PDF: {ex.Message}",
                    Timestamp = DateTime.UtcNow,
                    Type = "pdf-quick-insights"
                });
            }
        }

        [HttpPost("analyze-pdf")]
        public async Task<IActionResult> AnalyzePdf(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Ok(new ResearchResponse
                    {
                        Result = "No file uploaded",
                        Timestamp = DateTime.UtcNow,
                        Type = "pdf-analysis"
                    });
                }

                if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(new ResearchResponse
                    {
                        Result = "Only PDF files are supported",
                        Timestamp = DateTime.UtcNow,
                        Type = "pdf-analysis"
                    });
                }

                // Read PDF content
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                var pdfBytes = memoryStream.ToArray();

                // Extract text from PDF using PdfPig
                string pdfText;
                using (var document = UglyToad.PdfPig.PdfDocument.Open(pdfBytes))
                {
                    var textBuilder = new StringBuilder();
                    foreach (var page in document.GetPages())
                    {
                        textBuilder.AppendLine($"\n--- Page {page.Number} ---");
                        textBuilder.AppendLine(page.Text);
                    }
                    pdfText = textBuilder.ToString();
                }

                if (string.IsNullOrWhiteSpace(pdfText))
                {
                    return Ok(new ResearchResponse
                    {
                        Result = "Could not extract text from PDF. The file might be scanned/image-based.",
                        Timestamp = DateTime.UtcNow,
                        Type = "pdf-analysis"
                    });
                }

                // Analyze with AI using AcademicResearchService
                var strategy = await _academicResearchService.ExtractStrategyFromPaperAsync(
                    file.FileName, // Use filename as identifier
                    file.FileName.Replace(".pdf", ""),
                    pdfText // Pass extracted text directly
                );

                var result = $"PAPER ANALYSIS: {file.FileName}\n";
                result += "═══════════════════════════════════════════════════════════════\n\n";
                result += $"Strategy: {strategy.Name}\n\n";
                result += $"Description: {strategy.Description}\n\n";
                result += $"Implementation:\n{strategy.Implementation}\n\n";
                result += $"Extracted: {strategy.ExtractionDate}\n";

                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "pdf-analysis"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing PDF");
                return Ok(new ResearchResponse
                {
                    Result = $"Error analyzing PDF: {ex.Message}",
                    Timestamp = DateTime.UtcNow,
                    Type = "pdf-analysis"
                });
            }
        }
        [HttpPost("research-synthesis")]
        public async Task<IActionResult> ResearchSynthesis([FromBody] SynthesisRequest request)
        {
            try
            {
                _logger.LogInformation("Research synthesis requested for topic: {Topic}", request.Topic);
                
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
                else
                {
                    result += "No papers found. This may be due to:\n";
                    result += "- API quota limits\n";
                    result += "- Network connectivity issues\n";
                    result += "- Topic too specific or no matching papers\n\n";
                    result += "Try a broader topic or check API configurations.\n";
                }
                
                _logger.LogInformation("Research synthesis completed for topic: {Topic} with {Count} papers", request.Topic, network.Papers.Count);
                
                return Ok(new ResearchResponse
                {
                    Result = result,
                    Timestamp = DateTime.UtcNow,
                    Type = "research-synthesis"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synthesizing research for topic: {Topic}", request.Topic);
                return Ok(new ResearchResponse
                {
                    Result = $"Error synthesizing research for topic '{request.Topic}':\n\n{ex.Message}\n\nPlease check:\n- API keys and quotas\n- Network connectivity\n- Server logs for detailed error information",
                    Timestamp = DateTime.UtcNow,
                    Type = "research-synthesis-error"
                });
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

    public class QuickInsightsRequest
    {
        public string Url { get; set; } = string.Empty;
        public string? Title { get; set; }
    }

    public class DeepAnalyzeRequest
    {
        public string Url { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? StrategyName { get; set; }
    }

    public class ReportRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public string ReportType { get; set; } = "comprehensive";
    }

    public class LinkedInScrapeRequest
    {
        public string Url { get; set; } = string.Empty;
    }

    public class ResearchResponse
    {
        public string Result { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}
