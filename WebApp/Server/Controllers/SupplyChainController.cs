using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupplyChainController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "supply-chain-analysis", "supply-chain-risks", "supply-chain-geography", "supply-chain-diversification", "supply-chain-resilience", "supply-chain-comprehensive"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "20. Supply Chain Analysis", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("20. Supply Chain Analysis", feature, payload, Features);
}