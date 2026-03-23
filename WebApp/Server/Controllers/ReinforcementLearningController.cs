using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReinforcementLearningController : ControllerBase
{
    private readonly ILogger<ReinforcementLearningController> _logger;
    private readonly ReinforcementLearningWebService _rlService;

    public ReinforcementLearningController(
        ILogger<ReinforcementLearningController> logger,
        ReinforcementLearningWebService rlService)
    {
        _logger = logger;
        _rlService = rlService;
    }

    [HttpPost("train-q-learning")]
    public async Task<IActionResult> TrainQLearning([FromBody] RlTrainRequest request)
    {
        var result = await _rlService.TrainQLearningAsync(request.Environment, request.Episodes, request.LearningRate, request.DiscountFactor);
        return Ok(result);
    }

    [HttpPost("train-policy-gradient")]
    public async Task<IActionResult> TrainPolicyGradient([FromBody] RlTrainRequest request)
    {
        var result = await _rlService.TrainPolicyGradientAsync(request.Environment, request.Episodes, request.LearningRate);
        return Ok(result);
    }

    [HttpPost("train-actor-critic")]
    public async Task<IActionResult> TrainActorCritic([FromBody] ActorCriticRequest request)
    {
        var result = await _rlService.TrainActorCriticAsync(request.Environment, request.Episodes, request.ActorLearningRate, request.CriticLearningRate);
        return Ok(result);
    }

    [HttpPost("adapt-strategy")]
    public async Task<IActionResult> AdaptStrategy([FromBody] AdaptStrategyRequest request)
    {
        var result = await _rlService.AdaptStrategyAsync(request.StrategyName, request.MarketRegime);
        return Ok(result);
    }

    [HttpPost("bandit-optimization")]
    public async Task<IActionResult> BanditOptimization([FromBody] BanditRequest request)
    {
        var policies = request.CandidatePolicies?.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList()
            ?? new List<string> { "mean_reversion", "trend_following", "vol_breakout" };
        var result = await _rlService.OptimizeBanditAsync(policies);
        return Ok(result);
    }

    [HttpPost("contextual-bandit")]
    public async Task<IActionResult> ContextualBandit([FromBody] ContextualBanditRequest request)
    {
        var contexts = request.Contexts?.Where(c => !string.IsNullOrWhiteSpace(c)).ToList()
            ?? new List<string> { "risk_on", "risk_off", "high_vol", "low_vol" };
        var actions = request.Actions?.Where(a => !string.IsNullOrWhiteSpace(a)).ToList()
            ?? new List<string> { "long", "short", "flat" };
        var result = await _rlService.RunContextualBanditAsync(contexts, actions);
        return Ok(result);
    }

    [HttpPost("evaluate-rl-agent")]
    public async Task<IActionResult> EvaluateAgent([FromBody] EvaluateRequest request)
    {
        var result = await _rlService.EvaluateAgentAsync(request.Algorithm, request.Episodes);
        return Ok(result);
    }

    [HttpPost("rl-strategy-report")]
    public async Task<IActionResult> StrategyReport([FromBody] StrategyReportRequest request)
    {
        var result = await _rlService.GenerateStrategyReportAsync(request.StrategyName);
        return Ok(result);
    }
}

public class RlTrainRequest
{
    public string Environment { get; set; } = "equity-market-sim";
    public int Episodes { get; set; } = 300;
    public decimal LearningRate { get; set; } = 0.01m;
    public decimal DiscountFactor { get; set; } = 0.95m;
}

public class ActorCriticRequest : RlTrainRequest
{
    public decimal ActorLearningRate { get; set; } = 0.01m;
    public decimal CriticLearningRate { get; set; } = 0.02m;
}

public class AdaptStrategyRequest
{
    public string StrategyName { get; set; } = "regime-adaptive-momentum";
    public string MarketRegime { get; set; } = "high-volatility";
}

public class BanditRequest
{
    public List<string>? CandidatePolicies { get; set; }
}

public class ContextualBanditRequest
{
    public List<string>? Contexts { get; set; }
    public List<string>? Actions { get; set; }
}

public class EvaluateRequest
{
    public string Algorithm { get; set; } = "Actor-Critic";
    public int Episodes { get; set; } = 200;
}

public class StrategyReportRequest
{
    public string StrategyName { get; set; } = "regime-adaptive-momentum";
}
