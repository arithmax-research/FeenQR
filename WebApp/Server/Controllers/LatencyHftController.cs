using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LatencyHftController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "latency-arbitrage", "colocation-analysis", "order-routing", "market-data-feeds", "arbitrage-profitability", "latency-scan"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "24. Latency & HFT", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("24. Latency & HFT", feature, payload, Features);
}