using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StrategyDevelopmentController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "scenario-analysis", "build-strategy", "optimize-strategy", "generate-trading-template", "trading-template", "live-strategy", "deploy-strategy", "event-trading", "event-driven"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "25. Strategy Development", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("25. Strategy Development", feature, payload, Features);
}