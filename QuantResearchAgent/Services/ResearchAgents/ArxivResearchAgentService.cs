using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using RestSharp;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace QuantResearchAgent.Services.ResearchAgents;

/// <summary>
/// ArXiv Research Agent - Reads and analyzes academic papers for trading strategies
/// </summary>
public class ArxivResearchAgentService
{
    private readonly ILogger<ArxivResearchAgentService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Kernel _kernel;
    private readonly RestClient _arxivClient;
    private readonly List<ArxivPaper> _analyzedPapers = new();

    public ArxivResearchAgentService(
        ILogger<ArxivResearchAgentService> logger,
        IConfiguration configuration,
        Kernel kernel)
    {
        _logger = logger;
        _configuration = configuration;
        _kernel = kernel;
        _arxivClient = new RestClient("http://export.arxiv.org");
    }

    public async Task<List<ArxivPaper>> SearchPapersAsync(string query, int maxResults = 20)
    {
        _logger.LogInformation("Searching ArXiv for papers with query: {Query}", query);

        try
        {
            var request = new RestRequest("/api/query");
            request.AddParameter("search_query", $"all:{query} AND (cat:q-fin* OR cat:econ* OR cat:stat* OR cat:cs.LG* OR cat:math.ST*)");
            request.AddParameter("start", "0");
            request.AddParameter("max_results", maxResults.ToString());
            request.AddParameter("sortBy", "submittedDate");
            request.AddParameter("sortOrder", "descending");

            var response = await _arxivClient.ExecuteAsync(request);
            if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
            {
                _logger.LogWarning("Failed to search ArXiv: {Error}", response.ErrorMessage);
                return new List<ArxivPaper>();
            }

            return ParseArxivResponse(response.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching ArXiv papers");
            return new List<ArxivPaper>();
        }
    }

    public async Task<ArxivPaper> AnalyzePaperAsync(string arxivId)
    {
        _logger.LogInformation("Analyzing ArXiv paper: {ArxivId}", arxivId);

        try
        {
            // First, get paper metadata
            var papers = await SearchPapersAsync($"id:{arxivId}", 1);
            var paper = papers.FirstOrDefault();
            
            if (paper == null)
            {
                throw new ArgumentException($"Paper not found: {arxivId}");
            }

            // Download and analyze the paper content
            await DownloadPaperContentAsync(paper);
            
            // Perform AI analysis
            await AnalyzePaperContentAsync(paper);
            
            // Extract trading strategies
            await ExtractTradingStrategiesAsync(paper);
            
            // Generate implementation roadmap
            await GenerateImplementationRoadmapAsync(paper);

            paper.AnalyzedAt = DateTime.UtcNow;
            _analyzedPapers.Add(paper);

            _logger.LogInformation("Completed analysis of paper: {Title}", paper.Title);
            return paper;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze ArXiv paper: {ArxivId}", arxivId);
            throw;
        }
    }

    public async Task<List<TradingStrategy>> ExtractImplementableStrategiesAsync(string query = "algorithmic trading")
    {
        _logger.LogInformation("Extracting implementable strategies from recent papers");

        try
        {
            var papers = await SearchPapersAsync(query, 10);
            var strategies = new List<TradingStrategy>();

            foreach (var paper in papers.Take(5)) // Analyze top 5 papers
            {
                var analyzedPaper = await AnalyzePaperAsync(paper.ArxivId);
                strategies.AddRange(analyzedPaper.TradingStrategies);
            }

            // Rank strategies by implementation feasibility
            var rankedStrategies = strategies
                .OrderByDescending(s => s.FeasibilityScore)
                .ThenByDescending(s => s.ExpectedReturn)
                .ToList();

            _logger.LogInformation("Extracted {StrategyCount} implementable strategies", rankedStrategies.Count);
            return rankedStrategies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract implementable strategies");
            throw;
        }
    }

    private List<ArxivPaper> ParseArxivResponse(string xmlContent)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var ns = XNamespace.Get("http://www.w3.org/2005/Atom");
            var arxivNs = XNamespace.Get("http://arxiv.org/schemas/atom");

            var papers = new List<ArxivPaper>();

            foreach (var entry in doc.Descendants(ns + "entry"))
            {
                var paper = new ArxivPaper
                {
                    ArxivId = ExtractArxivId(entry.Element(ns + "id")?.Value ?? ""),
                    Title = entry.Element(ns + "title")?.Value?.Trim() ?? "",
                    Summary = entry.Element(ns + "summary")?.Value?.Trim() ?? "",
                    Authors = entry.Elements(ns + "author")
                                   .Select(a => a.Element(ns + "name")?.Value?.Trim() ?? "")
                                   .Where(name => !string.IsNullOrEmpty(name))
                                   .ToList(),
                    PublishedDate = DateTime.TryParse(entry.Element(ns + "published")?.Value, out var pubDate) ? pubDate : DateTime.MinValue,
                    UpdatedDate = DateTime.TryParse(entry.Element(ns + "updated")?.Value, out var updDate) ? updDate : DateTime.MinValue,
                    Categories = entry.Elements(ns + "category")
                                      .Select(c => c.Attribute("term")?.Value ?? "")
                                      .Where(cat => !string.IsNullOrEmpty(cat))
                                      .ToList(),
                    PdfUrl = entry.Elements(ns + "link")
                                  .FirstOrDefault(l => l.Attribute("type")?.Value == "application/pdf")
                                  ?.Attribute("href")?.Value ?? ""
                };

                if (!string.IsNullOrEmpty(paper.Title) && IsRelevantPaper(paper))
                {
                    papers.Add(paper);
                }
            }

            return papers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse ArXiv response");
            return new List<ArxivPaper>();
        }
    }

    private string ExtractArxivId(string idUrl)
    {
        var match = Regex.Match(idUrl, @"arxiv\.org/abs/(.+)$");
        return match.Success ? match.Groups[1].Value : "";
    }

    private bool IsRelevantPaper(ArxivPaper paper)
    {
        var relevantKeywords = new[]
        {
            "trading", "portfolio", "algorithmic", "quantitative", "finance", "market",
            "volatility", "arbitrage", "machine learning", "deep learning", "reinforcement learning",
            "time series", "prediction", "forecasting", "risk management", "optimization",
            "neural network", "lstm", "transformer", "sentiment analysis", "nlp"
        };

        var text = $"{paper.Title} {paper.Summary}".ToLower();
        return relevantKeywords.Any(keyword => text.Contains(keyword));
    }

    private async Task DownloadPaperContentAsync(ArxivPaper paper)
    {
        try
        {
            // For now, we'll work with the abstract/summary
            // In a full implementation, you'd download and parse the PDF
            paper.FullText = paper.Summary;
            
            _logger.LogDebug("Paper content prepared for analysis: {ArxivId}", paper.ArxivId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download paper content for {ArxivId}", paper.ArxivId);
            paper.FullText = paper.Summary; // Fallback to summary
        }
    }

    private async Task AnalyzePaperContentAsync(ArxivPaper paper)
    {
        var prompt = $@"
You are a quantitative finance expert analyzing an academic paper for trading strategy implementation.

Paper Details:
Title: {paper.Title}
Authors: {string.Join(", ", paper.Authors)}
Categories: {string.Join(", ", paper.Categories)}
Abstract: {paper.Summary}

Please analyze this paper and provide:

1. TRADING RELEVANCE (Score 1-10):
   - How relevant is this paper to algorithmic trading?
   - Rate the practical applicability

2. KEY CONCEPTS:
   - List the main quantitative concepts
   - Mathematical models mentioned
   - Algorithms described

3. IMPLEMENTATION FEASIBILITY:
   - Rate complexity (1-10, where 1=very easy, 10=extremely complex)
   - Required data sources
   - Computational requirements
   - Time horizon (HFT, intraday, daily, etc.)

4. POTENTIAL STRATEGIES:
   - Concrete trading strategies that could be derived
   - Market conditions where they would work
   - Expected risk/return profile

5. INNOVATION SCORE (1-10):
   - How novel are the approaches?
   - Competitive advantage potential

Provide specific, actionable insights focused on implementation.
";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            paper.AnalysisResult = result.ToString();
            
            // Parse relevance score
            var relevanceMatch = Regex.Match(paper.AnalysisResult, @"TRADING RELEVANCE.*?(\d+)", RegexOptions.IgnoreCase);
            if (relevanceMatch.Success && int.TryParse(relevanceMatch.Groups[1].Value, out var relevance))
            {
                paper.RelevanceScore = relevance;
            }

            _logger.LogInformation("Completed AI analysis for paper: {Title}", paper.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze paper content for {ArxivId}", paper.ArxivId);
        }
    }

    private async Task ExtractTradingStrategiesAsync(ArxivPaper paper)
    {
        var prompt = $@"
Based on the following academic paper analysis, extract specific trading strategies that can be implemented:

Paper: {paper.Title}
Analysis: {paper.AnalysisResult}
Abstract: {paper.Summary}

For each implementable strategy, provide:

STRATEGY: [Name]
DESCRIPTION: [How it works]
SIGNAL_GENERATION: [How to generate buy/sell signals]
TIMEFRAME: [Holding period - seconds/minutes/hours/days]
MARKET_TYPE: [Stocks/Crypto/Forex/Commodities]
COMPLEXITY: [1-10 scale]
EXPECTED_RETURN: [Expected annual return %]
RISK_LEVEL: [Low/Medium/High]
REQUIRED_DATA: [What data is needed]
IMPLEMENTATION_STEPS: [Step-by-step implementation guide]

Focus on strategies that:
1. Can be implemented with available market data
2. Have clear mathematical foundations
3. Are computationally feasible
4. Show promising backtesting potential

Provide maximum 3 best strategies.
";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            paper.TradingStrategies = ParseTradingStrategies(result.ToString());
            
            _logger.LogInformation("Extracted {StrategyCount} trading strategies from paper: {Title}", 
                paper.TradingStrategies.Count, paper.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract trading strategies for {ArxivId}", paper.ArxivId);
        }
    }

    private async Task GenerateImplementationRoadmapAsync(ArxivPaper paper)
    {
        var strategiesText = string.Join("\n\n", paper.TradingStrategies.Select(s => 
            $"Strategy: {s.Name}\nComplexity: {s.Complexity}\nExpected Return: {s.ExpectedReturn}%"));

        var prompt = $@"
Create a detailed implementation roadmap for the trading strategies from this research paper:

Paper: {paper.Title}
Strategies:
{strategiesText}

Provide a roadmap with:

1. PRIORITY ORDER: Which strategy to implement first and why
2. DEVELOPMENT PHASES: Break down into phases (Research, Prototype, Backtest, Live)
3. TECHNICAL REQUIREMENTS: Programming languages, libraries, infrastructure
4. DATA REQUIREMENTS: Specific datasets, APIs, preprocessing needs
5. TIMELINE: Estimated development time for each phase
6. RISK MITIGATION: Potential issues and how to address them
7. SUCCESS METRICS: How to measure if implementation is successful
8. INTEGRATION: How to integrate with existing trading systems

Focus on practical, actionable steps that a quantitative developer can follow.
";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            paper.ImplementationRoadmap = result.ToString();
            
            _logger.LogInformation("Generated implementation roadmap for paper: {Title}", paper.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate implementation roadmap for {ArxivId}", paper.ArxivId);
        }
    }

    private List<TradingStrategy> ParseTradingStrategies(string strategiesText)
    {
        var strategies = new List<TradingStrategy>();
        
        try
        {
            var strategyBlocks = strategiesText.Split(new[] { "STRATEGY:" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var block in strategyBlocks.Skip(1)) // Skip first empty block
            {
                var strategy = new TradingStrategy();
                var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        strategy.Name = trimmedLine.Trim('[', ']');
                    }
                    else if (trimmedLine.StartsWith("DESCRIPTION:"))
                    {
                        strategy.Description = ExtractValue(trimmedLine, "DESCRIPTION:");
                    }
                    else if (trimmedLine.StartsWith("TIMEFRAME:"))
                    {
                        strategy.Timeframe = ExtractValue(trimmedLine, "TIMEFRAME:");
                    }
                    else if (trimmedLine.StartsWith("COMPLEXITY:"))
                    {
                        var complexityText = ExtractValue(trimmedLine, "COMPLEXITY:");
                        if (int.TryParse(complexityText.Split(' ')[0], out var complexity))
                        {
                            strategy.Complexity = complexity;
                        }
                    }
                    else if (trimmedLine.StartsWith("EXPECTED_RETURN:"))
                    {
                        var returnText = ExtractValue(trimmedLine, "EXPECTED_RETURN:");
                        var returnMatch = Regex.Match(returnText, @"(\d+(?:\.\d+)?)");
                        if (returnMatch.Success && double.TryParse(returnMatch.Groups[1].Value, out var expectedReturn))
                        {
                            strategy.ExpectedReturn = expectedReturn;
                        }
                    }
                    else if (trimmedLine.StartsWith("RISK_LEVEL:"))
                    {
                        strategy.RiskLevel = ExtractValue(trimmedLine, "RISK_LEVEL:");
                    }
                }
                
                // Calculate feasibility score based on complexity and expected return
                strategy.FeasibilityScore = CalculateFeasibilityScore(strategy);
                
                if (!string.IsNullOrEmpty(strategy.Name))
                {
                    strategies.Add(strategy);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse trading strategies");
        }
        
        return strategies;
    }

    private string ExtractValue(string line, string prefix)
    {
        var startIndex = line.IndexOf(prefix) + prefix.Length;
        if (startIndex < prefix.Length) return string.Empty;
        
        return line.Substring(startIndex).Trim();
    }

    private double CalculateFeasibilityScore(TradingStrategy strategy)
    {
        // Score based on complexity (lower is better) and expected return (higher is better)
        var complexityScore = strategy.Complexity > 0 ? (11 - strategy.Complexity) / 10.0 : 0.5;
        var returnScore = Math.Min(strategy.ExpectedReturn / 50.0, 1.0); // Cap at 50% return
        
        return (complexityScore * 0.6) + (returnScore * 0.4);
    }

    public List<ArxivPaper> GetAnalyzedPapers() => _analyzedPapers.ToList();

    public async Task<string> GenerateResearchSummaryAsync(string topic = "algorithmic trading")
    {
        try
        {
            var papers = await SearchPapersAsync(topic, 5);
            var recentPapers = papers.Take(3).ToList();
            
            var summaryPrompt = $@"
Generate a research summary of recent developments in {topic} based on these ArXiv papers:

{string.Join("\n\n", recentPapers.Select(p => 
    $"Title: {p.Title}\nAuthors: {string.Join(", ", p.Authors)}\nSummary: {p.Summary}"))}

Provide:
1. KEY TRENDS: What are the emerging trends?
2. BREAKTHROUGH TECHNOLOGIES: Any significant innovations?
3. PRACTICAL APPLICATIONS: How can these be applied to trading?
4. FUTURE DIRECTIONS: Where is the field heading?
5. IMPLEMENTATION OPPORTUNITIES: Specific opportunities for quantitative firms

Focus on actionable insights for algorithmic trading.
";

            var function = _kernel.CreateFunctionFromPrompt(summaryPrompt);
            var result = await _kernel.InvokeAsync(function);
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate research summary");
            return $"Error generating research summary: {ex.Message}";
        }
    }
}

// Model classes for ArXiv Research Agent
public class ArxivPaper
{
    public string ArxivId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> Authors { get; set; } = new();
    public DateTime PublishedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public List<string> Categories { get; set; } = new();
    public string PdfUrl { get; set; } = string.Empty;
    public string FullText { get; set; } = string.Empty;
    public string AnalysisResult { get; set; } = string.Empty;
    public int RelevanceScore { get; set; } = 0;
    public List<TradingStrategy> TradingStrategies { get; set; } = new();
    public string ImplementationRoadmap { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
}

public class TradingStrategy
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public int Complexity { get; set; } = 5; // 1-10 scale
    public double ExpectedReturn { get; set; } = 0; // Annual return %
    public string RiskLevel { get; set; } = "Medium";
    public double FeasibilityScore { get; set; } = 0; // 0-1 scale
    public string RequiredData { get; set; } = string.Empty;
    public string ImplementationSteps { get; set; } = string.Empty;
}
