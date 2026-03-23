using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitoringComplianceController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "alerts", "check-alerts", "compliance", "compliance-check", "data-validation", "corporate-action", "corp-action", "timezone"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "26. Monitoring & Compliance", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("26. Monitoring & Compliance", feature, payload, Features);
}