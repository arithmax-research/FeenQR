using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketMicrostructureController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "order-book-analysis", "market-depth", "liquidity-analysis", "spread-analysis", "orderbook-reconstruction", "hft-analysis", "manipulation-detection"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "21. Market Microstructure", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("21. Market Microstructure", feature, payload, Features);
}