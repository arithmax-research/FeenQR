using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdvancedDataSourcesController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "extract-web-data", "scrape-social-media", "analyze-satellite-imagery", "geo-satellite", "consumer-pulse"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "28. Advanced Data Sources", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("28. Advanced Data Sources", feature, payload, Features);
}