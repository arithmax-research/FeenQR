using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExecutionAlgorithmsController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "almgren-chriss", "implementation-shortfall", "price-impact", "vwap-schedule", "twap-schedule", "execution-optimization", "optimal-execution"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "22. Execution Algorithms", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("22. Execution Algorithms", feature, payload, Features);
}