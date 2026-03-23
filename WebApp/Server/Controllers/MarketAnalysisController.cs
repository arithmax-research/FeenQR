using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketAnalysisController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "detect-market-regime", "detect-anomalies", "anomaly-scan", "test-apis"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "33. Market Analysis", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("33. Market Analysis", feature, payload, Features);
}