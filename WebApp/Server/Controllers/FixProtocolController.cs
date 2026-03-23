using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FixProtocolController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "fix-connect", "fix-disconnect", "fix-order", "fix-cancel", "fix-market-data", "fix-heartbeat", "fix-status", "fix-info"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "29. FIX Protocol", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("29. FIX Protocol", feature, payload, Features);
}