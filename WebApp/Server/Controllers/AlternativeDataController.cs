using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlternativeDataController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "web-scrape-earnings", "web-monitor-communications", "web-analyze-sentiment", "web-analyze-social", "web-monitor-darkweb", "patent-search", "patent-innovation", "patent-citations", "patent-value"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "30. Alternative Data", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("30. Alternative Data", feature, payload, Features);
}