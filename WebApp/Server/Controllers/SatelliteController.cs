using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;

namespace FeenQR.WebApp.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SatelliteController : ControllerBase
{
    private readonly PortContainerAnalysisService _satelliteService;
    private readonly ILogger<SatelliteController> _logger;

    public SatelliteController(
        PortContainerAnalysisService satelliteService,
        ILogger<SatelliteController> logger)
    {
        _satelliteService = satelliteService;
        _logger = logger;
    }

    [HttpGet("global")]
    public async Task<IActionResult> AnalyzeGlobalPorts(
        [FromQuery] string period = "90d",
        [FromQuery] string selection = "all")
    {
        try
        {
            var result = await _satelliteService.AnalyzeGlobalPortsAsync(period, selection);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing global ports");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("regional")]
    public async Task<IActionResult> AnalyzeRegionalPorts(
        [FromQuery] string region = "us",
        [FromQuery] string period = "90d")
    {
        try
        {
            var result = await _satelliteService.AnalyzeRegionalPortsAsync(region, period);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing regional ports");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("signals")]
    public async Task<IActionResult> GenerateTradingSignals(
        [FromQuery] string market = "SPY")
    {
        try
        {
            var result = await _satelliteService.GenerateTradingSignalsAsync(market);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating trading signals");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("backtest")]
    public async Task<IActionResult> RunBacktest(
        [FromQuery] string period = "2018-2022",
        [FromQuery] decimal capital = 100000)
    {
        try
        {
            var result = await _satelliteService.RunBacktestAsync(period, capital);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running backtest");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
