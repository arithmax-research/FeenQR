using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Server.Controllers;

public abstract class CategoryFeatureControllerBase : ControllerBase
{
    protected IActionResult RunScaffoldedFeature(
        string category,
        string feature,
        JsonElement payload,
        IReadOnlyList<string> supportedFeatures)
    {
        if (!supportedFeatures.Contains(feature, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                error = $"Unsupported feature '{feature}' for {category}",
                supportedFeatures
            });
        }

        return Ok(new
        {
            category,
            feature,
            integrationStatus = "scaffolded-not-integrated",
            message = "Endpoint and UI are created. Wire a real backend provider/service for production analytics.",
            timestamp = DateTime.UtcNow,
            payloadEcho = payload
        });
    }
}