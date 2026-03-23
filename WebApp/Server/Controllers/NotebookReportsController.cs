using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotebookReportsController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "create-notebook", "execute-notebook", "generate-report", "generate-research-report"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "32. Notebook & Report Generation", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("32. Notebook & Report Generation", feature, payload, Features);
}