using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OtherSpecializedController : CategoryFeatureControllerBase
{
    private static readonly string[] Features = { "other-specialized" };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "34. Other Specialized Features", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("34. Other Specialized Features", feature, payload, Features);
}