using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using QuantResearchAgent.Services.ResearchAgents;
using System.Text.Json;

namespace QuantResearchAgent.Controllers;

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly Kernel _kernel;
    private readonly AgentOrchestrator _orchestrator;
    private readonly ILogger<ApiController> _logger;
    private readonly ComprehensiveStockAnalysisAgent _comprehensiveAgent;
    private readonly AcademicResearchPaperAgent _researchAgent;
    private readonly YahooFinanceService _yahooFinanceService;
    private readonly AlpacaService _alpacaService;
    private readonly PolygonService _polygonService;
    private readonly NewsSentimentAnalysisService _newsSentimentService;

    public ApiController(
        Kernel kernel,
        AgentOrchestrator orchestrator,
        ILogger<ApiController> logger,
        ComprehensiveStockAnalysisAgent comprehensiveAgent,
        AcademicResearchPaperAgent researchAgent,
        YahooFinanceService yahooFinanceService,
        AlpacaService alpacaService,
        PolygonService polygonService,
        NewsSentimentAnalysisService newsSentimentService)
    {
        _kernel = kernel;
        _orchestrator = orchestrator;
        _logger = logger;
        _comprehensiveAgent = comprehensiveAgent;
        _researchAgent = researchAgent;
        _yahooFinanceService = yahooFinanceService;
        _alpacaService = alpacaService;
        _polygonService = polygonService;
        _newsSentimentService = newsSentimentService;
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    [HttpGet("market-data/{symbol}")]
    public async Task<IActionResult> GetMarketData(string symbol)
    {
        try
        {
            var yahooData = await _yahooFinanceService.GetMarketDataAsync(symbol);
            return Ok(new { symbol, data = yahooData, source = "yahoo" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching market data for {Symbol}", symbol);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("technical-analysis/{symbol}")]
    public async Task<IActionResult> GetTechnicalAnalysis(string symbol)
    {
        try
        {
            var function = _kernel.Plugins["TechnicalAnalysisPlugin"]["AnalyzeSymbol"];
            var result = await _kernel.InvokeAsync(function, new() { ["symbol"] = symbol });
            return Ok(new { symbol, analysis = result.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing technical analysis for {Symbol}", symbol);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("sentiment-analysis/{symbol}")]
    public async Task<IActionResult> GetSentimentAnalysis(string symbol)
    {
        try
        {
            var analysis = await _newsSentimentService.AnalyzeSymbolSentimentAsync(symbol);
            return Ok(new { symbol, sentiment = analysis });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing sentiment analysis for {Symbol}", symbol);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("comprehensive-analysis/{symbol}")]
    public async Task<IActionResult> GetComprehensiveAnalysis(string symbol, [FromQuery] string assetType = "stock")
    {
        try
        {
            var result = await _comprehensiveAgent.AnalyzeAndRecommendAsync(symbol, assetType);
            return Ok(new { symbol, assetType, analysis = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing comprehensive analysis for {Symbol}", symbol);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("portfolio")]
    public async Task<IActionResult> GetPortfolio()
    {
        try
        {
            var function = _kernel.Plugins["RiskManagementPlugin"]["GetPortfolioSummary"];
            var result = await _kernel.InvokeAsync(function);
            return Ok(new { portfolio = result.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching portfolio data");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("risk-assessment")]
    public async Task<IActionResult> GetRiskAssessment()
    {
        try
        {
            var function = _kernel.Plugins["RiskManagementPlugin"]["AssessPortfolioRisk"];
            var result = await _kernel.InvokeAsync(function);
            return Ok(new { riskAssessment = result.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing risk assessment");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("news/{symbol}")]
    public async Task<IActionResult> GetNews(string symbol)
    {
        try
        {
            // Get news from multiple sources
            var polygonNews = await _polygonService.GetNewsAsync(symbol, 5);
            
            return Ok(new { 
                symbol, 
                news = new {
                    polygon = polygonNews
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching news for {Symbol}", symbol);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("market-sentiment")]
    public async Task<IActionResult> GetMarketSentiment()
    {
        try
        {
            var analysis = await _newsSentimentService.AnalyzeMarketSentimentAsync();
            return Ok(new { marketSentiment = analysis });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing market sentiment analysis");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("research-papers")]
    public async Task<IActionResult> SearchResearchPapers([FromBody] ResearchRequest request)
    {
        try
        {
            var result = await _researchAgent.SearchAcademicPapersAsync(request.Topic, request.MaxPapers ?? 5);
            return Ok(new { topic = request.Topic, papers = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching research papers for topic {Topic}", request.Topic);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("alpaca/account")]
    public async Task<IActionResult> GetAlpacaAccount()
    {
        try
        {
            var function = _kernel.Plugins["AlpacaPlugin"]["GetAccountInfo"];
            var result = await _kernel.InvokeAsync(function);
            return Ok(new { account = result.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Alpaca account info");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("alpaca/positions")]
    public async Task<IActionResult> GetAlpacaPositions()
    {
        try
        {
            var function = _kernel.Plugins["AlpacaPlugin"]["GetPositions"];
            var result = await _kernel.InvokeAsync(function);
            return Ok(new { positions = result.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Alpaca positions");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class ResearchRequest
{
    public string Topic { get; set; } = "";
    public int? MaxPapers { get; set; }
}
