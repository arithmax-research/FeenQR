using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdvancedPortfolioOptimizationController : ControllerBase
{
    private readonly ILogger<AdvancedPortfolioOptimizationController> _logger;
    private readonly AdvancedOptimizationService _advancedOptimizationService;

    public AdvancedPortfolioOptimizationController(
        ILogger<AdvancedPortfolioOptimizationController> logger,
        AdvancedOptimizationService advancedOptimizationService)
    {
        _logger = logger;
        _advancedOptimizationService = advancedOptimizationService;
    }

    [HttpPost("black-litterman")]
    public async Task<IActionResult> RunBlackLitterman([FromBody] BlackLittermanOptimizationRequest request)
    {
        try
        {
            var assets = request.Assets?.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList() ?? new List<string>();
            if (assets.Count < 2)
            {
                return BadRequest(new { error = "At least two assets are required." });
            }

            var views = new BlackLittermanViews
            {
                AbsoluteViews = request.AbsoluteViews ?? new Dictionary<string, double>(),
                ViewConfidences = request.ViewConfidences ?? new Dictionary<string, double>()
            };

            var result = await _advancedOptimizationService.RunBlackLittermanOptimizationAsync(
                assets,
                views,
                BuildConstraints(request),
                request.StartDate ?? DateTime.UtcNow.AddYears(-2),
                request.EndDate ?? DateTime.UtcNow);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Black-Litterman optimization");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("risk-parity")]
    public async Task<IActionResult> RunRiskParity([FromBody] AdvancedOptimizationRequest request)
    {
        try
        {
            var assets = request.Assets?.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList() ?? new List<string>();
            if (assets.Count < 2)
            {
                return BadRequest(new { error = "At least two assets are required." });
            }

            var result = await _advancedOptimizationService.OptimizeRiskParityAsync(
                assets,
                BuildConstraints(request),
                request.StartDate ?? DateTime.UtcNow.AddYears(-2),
                request.EndDate ?? DateTime.UtcNow);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running risk parity optimization");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("hierarchical-risk-parity")]
    public async Task<IActionResult> RunHierarchicalRiskParity([FromBody] AdvancedOptimizationRequest request)
    {
        try
        {
            var assets = request.Assets?.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList() ?? new List<string>();
            if (assets.Count < 2)
            {
                return BadRequest(new { error = "At least two assets are required." });
            }

            var result = await _advancedOptimizationService.OptimizeHierarchicalRiskParityAsync(
                assets,
                BuildConstraints(request),
                request.StartDate ?? DateTime.UtcNow.AddYears(-2),
                request.EndDate ?? DateTime.UtcNow);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running hierarchical risk parity optimization");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("minimum-variance")]
    public async Task<IActionResult> RunMinimumVariance([FromBody] AdvancedOptimizationRequest request)
    {
        try
        {
            var assets = request.Assets?.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList() ?? new List<string>();
            if (assets.Count < 2)
            {
                return BadRequest(new { error = "At least two assets are required." });
            }

            var result = await _advancedOptimizationService.OptimizeMinimumVarianceAsync(
                assets,
                BuildConstraints(request),
                request.StartDate ?? DateTime.UtcNow.AddYears(-2),
                request.EndDate ?? DateTime.UtcNow);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running minimum variance optimization");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("compare-optimization")]
    public async Task<IActionResult> CompareOptimizationMethods([FromBody] BlackLittermanOptimizationRequest request)
    {
        try
        {
            var assets = request.Assets?.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList() ?? new List<string>();
            if (assets.Count < 2)
            {
                return BadRequest(new { error = "At least two assets are required." });
            }

            var startDate = request.StartDate ?? DateTime.UtcNow.AddYears(-2);
            var endDate = request.EndDate ?? DateTime.UtcNow;
            var constraints = BuildConstraints(request);

            var views = new BlackLittermanViews
            {
                AbsoluteViews = request.AbsoluteViews ?? new Dictionary<string, double>(),
                ViewConfidences = request.ViewConfidences ?? new Dictionary<string, double>()
            };

            var blackLittermanTask = _advancedOptimizationService.RunBlackLittermanOptimizationAsync(assets, views, constraints, startDate, endDate);
            var riskParityTask = _advancedOptimizationService.OptimizeRiskParityAsync(assets, constraints, startDate, endDate);
            var hrpTask = _advancedOptimizationService.OptimizeHierarchicalRiskParityAsync(assets, constraints, startDate, endDate);
            var minVarianceTask = _advancedOptimizationService.OptimizeMinimumVarianceAsync(assets, constraints, startDate, endDate);

            await Task.WhenAll(blackLittermanTask, riskParityTask, hrpTask, minVarianceTask);

            var blackLitterman = await blackLittermanTask;
            var riskParity = await riskParityTask;
            var hrp = await hrpTask;
            var minVariance = await minVarianceTask;

            var methodScores = new Dictionary<string, double>
            {
                ["Black-Litterman"] = blackLitterman.SharpeRatio,
                ["Risk Parity"] = riskParity.TotalRisk > 0 ? riskParity.ExpectedReturn / riskParity.TotalRisk : 0,
                ["Hierarchical Risk Parity"] = hrp.TotalRisk > 0 ? hrp.ExpectedReturn / hrp.TotalRisk : 0,
                ["Minimum Variance"] = minVariance.PortfolioVolatility > 0 ? minVariance.ExpectedReturn / minVariance.PortfolioVolatility : 0
            };

            var recommendedMethod = methodScores.OrderByDescending(m => m.Value).First().Key;

            return Ok(new
            {
                assets,
                timestamp = DateTime.UtcNow,
                summary = new
                {
                    recommendedMethod,
                    bestScore = methodScores[recommendedMethod]
                },
                scores = methodScores,
                results = new
                {
                    blackLitterman,
                    riskParity,
                    hierarchicalRiskParity = hrp,
                    minimumVariance = minVariance
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing portfolio optimization methods");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static QuantResearchAgent.Core.OptimizationConstraints BuildConstraints(AdvancedOptimizationRequest request)
    {
        var constraints = new QuantResearchAgent.Core.OptimizationConstraints
        {
            AllowShortSelling = request.AllowShortSelling,
            MaxRisk = request.MaxRisk ?? 0.35,
            TurnoverLimit = request.TurnoverLimit ?? 0.50,
            MinReturn = request.MinReturn ?? 0
        };

        var assets = request.Assets?.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList() ?? new List<string>();
        foreach (var asset in assets)
        {
            constraints.MinWeights[asset] = request.MinWeight ?? 0.0;
            constraints.MaxWeights[asset] = request.MaxWeight ?? 0.35;
        }

        return constraints;
    }
}

public class AdvancedOptimizationRequest
{
    public List<string> Assets { get; set; } = new();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool AllowShortSelling { get; set; }
    public double? MinWeight { get; set; }
    public double? MaxWeight { get; set; }
    public double? MinReturn { get; set; }
    public double? MaxRisk { get; set; }
    public double? TurnoverLimit { get; set; }
}

public class BlackLittermanOptimizationRequest : AdvancedOptimizationRequest
{
    public Dictionary<string, double>? AbsoluteViews { get; set; }
    public Dictionary<string, double>? ViewConfidences { get; set; }
}
