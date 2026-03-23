using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FactorModelsController : ControllerBase
{
    private readonly ILogger<FactorModelsController> _logger;
    private readonly FactorModelService _factorModelService;
    private readonly FactorResearchService _factorResearchService;
    private readonly DynamicFactorService _dynamicFactorService;

    public FactorModelsController(
        ILogger<FactorModelsController> logger,
        FactorModelService factorModelService,
        FactorResearchService factorResearchService,
        DynamicFactorService dynamicFactorService)
    {
        _logger = logger;
        _factorModelService = factorModelService;
        _factorResearchService = factorResearchService;
        _dynamicFactorService = dynamicFactorService;
    }

    [HttpPost("fama-french-3factor")]
    public async Task<IActionResult> AnalyzeFamaFrench3([FromBody] FactorModelRequest request)
    {
        try
        {
            var start = request.StartDate ?? DateTime.UtcNow.AddYears(-3);
            var end = request.EndDate ?? DateTime.UtcNow;
            var result = await _factorModelService.AnalyzeFamaFrench3FactorAsync(request.AssetSymbol, start, end);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Fama-French 3-factor for {Asset}", request.AssetSymbol);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("carhart-4factor")]
    public async Task<IActionResult> AnalyzeCarhart4([FromBody] FactorModelRequest request)
    {
        try
        {
            var start = request.StartDate ?? DateTime.UtcNow.AddYears(-3);
            var end = request.EndDate ?? DateTime.UtcNow;
            var result = await _factorModelService.AnalyzeCarhart4FactorAsync(request.AssetSymbol, start, end);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Carhart 4-factor for {Asset}", request.AssetSymbol);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("custom-factor-model")]
    public async Task<IActionResult> CreateCustomFactorModel([FromBody] CustomFactorModelRequest request)
    {
        try
        {
            var start = request.StartDate ?? DateTime.UtcNow.AddYears(-3);
            var end = request.EndDate ?? DateTime.UtcNow;
            var factors = request.Factors?.Where(f => !string.IsNullOrWhiteSpace(f)).Distinct().ToList() ?? new List<string>();
            if (factors.Count == 0)
            {
                factors = new List<string> { "Market", "SMB", "HML", "Momentum" };
            }

            var result = await _factorModelService.CreateCustomFactorModelAsync(
                request.ModelName ?? "Custom Factor Model",
                request.Description ?? "User-defined model",
                factors,
                request.AssetSymbol,
                start,
                end);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating custom factor model for {Asset}", request.AssetSymbol);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("factor-attribution")]
    public async Task<IActionResult> GetFactorAttribution([FromBody] FactorModelRequest request)
    {
        try
        {
            var start = request.StartDate ?? DateTime.UtcNow.AddYears(-3);
            var end = request.EndDate ?? DateTime.UtcNow;
            var model = await _factorModelService.AnalyzeCarhart4FactorAsync(request.AssetSymbol, start, end);

            var attribution = new FactorAttribution
            {
                Asset = request.AssetSymbol,
                PeriodStart = start,
                PeriodEnd = end,
                FactorContributions = new Dictionary<string, double>
                {
                    ["Market"] = model.MarketBeta * model.FactorReturns.GetValueOrDefault("Market", 0),
                    ["SMB"] = model.SizeBeta * model.FactorReturns.GetValueOrDefault("SMB", 0),
                    ["HML"] = model.ValueBeta * model.FactorReturns.GetValueOrDefault("HML", 0),
                    ["MOM"] = model.MomentumBeta * model.FactorReturns.GetValueOrDefault("MOM", 0)
                }
            };

            attribution.TotalAttribution = attribution.FactorContributions.Values.Sum();
            attribution.ResidualReturn = model.Alpha;

            return Ok(attribution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running factor attribution for {Asset}", request.AssetSymbol);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("compare-factor-models")]
    public async Task<IActionResult> CompareFactorModels([FromBody] FactorModelRequest request)
    {
        try
        {
            var start = request.StartDate ?? DateTime.UtcNow.AddYears(-3);
            var end = request.EndDate ?? DateTime.UtcNow;

            var ff3Task = _factorModelService.AnalyzeFamaFrench3FactorAsync(request.AssetSymbol, start, end);
            var carhartTask = _factorModelService.AnalyzeCarhart4FactorAsync(request.AssetSymbol, start, end);
            var ff5Task = _factorModelService.AnalyzeFamaFrench5FactorAsync(request.AssetSymbol, start, end);

            await Task.WhenAll(ff3Task, carhartTask, ff5Task);

            var ff3 = await ff3Task;
            var carhart = await carhartTask;
            var ff5 = await ff5Task;

            var scores = new Dictionary<string, double>
            {
                ["Fama-French 3"] = ff3.R2,
                ["Carhart 4"] = carhart.R2,
                ["Fama-French 5"] = ff5.R2
            };

            var best = scores.OrderByDescending(s => s.Value).First();

            return Ok(new
            {
                asset = request.AssetSymbol,
                period = new { start, end },
                bestModel = best.Key,
                scores,
                models = new
                {
                    famaFrench3 = ff3,
                    carhart4 = carhart,
                    famaFrench5 = ff5
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing factor models for {Asset}", request.AssetSymbol);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("factor-research")]
    public async Task<IActionResult> RunFactorResearch([FromBody] FactorResearchRequest request)
    {
        try
        {
            var symbols = request.Symbols?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList()
                ?? new List<string> { "AAPL", "MSFT", "NVDA", "AMZN", "GOOGL" };
            var start = request.StartDate ?? DateTime.UtcNow.AddYears(-2);
            var end = request.EndDate ?? DateTime.UtcNow;

            var factor = await _factorResearchService.CreateTechnicalFactorAsync(
                request.FactorName ?? "Momentum_21D",
                request.Description ?? "21-day momentum factor",
                symbols,
                start,
                end,
                window =>
                {
                    if (window.Count < 2) return 0;
                    var first = window.First().Price;
                    var last = window.Last().Price;
                    return first > 0 ? (last / first) - 1.0 : 0;
                },
                request.LookbackPeriod ?? 21);

            return Ok(factor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running factor research");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("factor-portfolio")]
    public async Task<IActionResult> BuildFactorPortfolio([FromBody] FactorResearchRequest request)
    {
        try
        {
            var symbols = request.Symbols?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList()
                ?? new List<string> { "AAPL", "MSFT", "NVDA", "AMZN", "GOOGL" };
            var start = request.StartDate ?? DateTime.UtcNow.AddYears(-2);
            var end = request.EndDate ?? DateTime.UtcNow;

            var factor = await _factorResearchService.CreateTechnicalFactorAsync(
                request.FactorName ?? "Momentum_21D",
                request.Description ?? "21-day momentum factor",
                symbols,
                start,
                end,
                window =>
                {
                    if (window.Count < 2) return 0;
                    var first = window.First().Price;
                    var last = window.Last().Price;
                    return first > 0 ? (last / first) - 1.0 : 0;
                },
                request.LookbackPeriod ?? 21);

            var efficacy = await _factorResearchService.TestFactorEfficacyAsync(
                factor,
                start,
                end,
                request.PortfolioCount ?? 5);

            return Ok(new
            {
                factor = factor.Name,
                portfolioCount = efficacy.PortfolioCount,
                portfolios = efficacy.Portfolios,
                topSharpe = efficacy.SharpeRatio
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building factor portfolio");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("factor-efficacy")]
    public async Task<IActionResult> TestFactorEfficacy([FromBody] FactorResearchRequest request)
    {
        try
        {
            var symbols = request.Symbols?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList()
                ?? new List<string> { "AAPL", "MSFT", "NVDA", "AMZN", "GOOGL" };
            var start = request.StartDate ?? DateTime.UtcNow.AddYears(-2);
            var end = request.EndDate ?? DateTime.UtcNow;

            var factor = await _factorResearchService.CreateTechnicalFactorAsync(
                request.FactorName ?? "Momentum_21D",
                request.Description ?? "21-day momentum factor",
                symbols,
                start,
                end,
                window =>
                {
                    if (window.Count < 2) return 0;
                    var first = window.First().Price;
                    var last = window.Last().Price;
                    return first > 0 ? (last / first) - 1.0 : 0;
                },
                request.LookbackPeriod ?? 21);

            var efficacy = await _factorResearchService.TestFactorEfficacyAsync(
                factor,
                start,
                end,
                request.PortfolioCount ?? 5);

            return Ok(efficacy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing factor efficacy");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("dynamic-factors")]
    public async Task<IActionResult> AnalyzeDynamicFactors([FromBody] DynamicFactorsRequest request)
    {
        try
        {
            var symbols = request.Symbols?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList()
                ?? new List<string> { "SPY", "AAPL", "MSFT", "NVDA" };

            var model = await _dynamicFactorService.BuildDynamicFactorModelAsync(symbols, request.LookbackDays ?? 252);

            var covarianceRows = new List<List<double>>();
            for (int i = 0; i < model.FactorCovariance.RowCount; i++)
            {
                covarianceRows.Add(model.FactorCovariance.Row(i).ToList());
            }

            return Ok(new
            {
                symbols = model.Symbols,
                factors = model.Factors.Select(f => new
                {
                    f.Name,
                    type = f.Type.ToString(),
                    f.Volatility,
                    f.SharpeRatio,
                    sampleCount = f.Returns.Count
                }),
                factorCovariance = covarianceRows,
                model.AssetFactorLoadings,
                model.RegimeAdjustedFactors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing dynamic factors");
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class FactorModelRequest
{
    public string AssetSymbol { get; set; } = "AAPL";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class CustomFactorModelRequest : FactorModelRequest
{
    public string? ModelName { get; set; }
    public string? Description { get; set; }
    public List<string>? Factors { get; set; }
}

public class FactorResearchRequest
{
    public string? FactorName { get; set; }
    public string? Description { get; set; }
    public List<string>? Symbols { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? PortfolioCount { get; set; }
    public int? LookbackPeriod { get; set; }
}

public class DynamicFactorsRequest
{
    public List<string>? Symbols { get; set; }
    public int? LookbackDays { get; set; }
}
