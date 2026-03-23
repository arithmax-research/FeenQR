using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FedGlobalEconomicsController : CategoryFeatureControllerBase
{
    private static readonly string[] Features =
    {
        "fed-fomc-announcements", "fed-interest-rates", "fed-economic-projections", "fed-speeches", "global-economic-indicators", "global-supply-chain", "global-trade-data", "global-currency-data", "global-commodity-prices"
    };

    [HttpGet("features")]
    public IActionResult GetFeatures() => Ok(new { category = "31. Federal Reserve & Global Economics", features = Features });

    [HttpPost("run/{feature}")]
    public IActionResult Run(string feature, [FromBody] JsonElement payload)
        => RunScaffoldedFeature("31. Federal Reserve & Global Economics", feature, payload, Features);
}