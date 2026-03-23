using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OptionsDerivativesController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "option-monte-carlo", "options-flow", "unusual-options", "gamma-exposure", "options-orderbook", "volatility-surface", "vix-analysis", "volatility-strategy", "volatility-monitor", "vol-surface"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "23. Options & Derivatives", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("23. Options & Derivatives", feature, payload, Features);
}