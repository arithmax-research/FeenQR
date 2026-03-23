using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResearchToolsController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "academic-research", "replicate-study", "citation-network", "quantitative-model", "literature-review"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "27. Research Tools", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("27. Research Tools", feature, payload, Features);
}