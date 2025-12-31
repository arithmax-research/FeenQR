using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketDataController : ControllerBase
{
    private readonly ILogger<MarketDataController> _logger;
    private readonly AlpacaService _alpacaService;
    private readonly AlphaVantageService _alphaVantageService;

    public MarketDataController(
        ILogger<MarketDataController> logger,
        AlpacaService alpacaService,
        AlphaVantageService alphaVantageService)
    {
        _logger = logger;
        _alpacaService = alpacaService;
        _alphaVantageService = alphaVantageService;
    }

    [HttpGet("quotes")]
    public async Task<IActionResult> GetQuotes()
    {
        try
        {
            var symbols = new[] { "AAPL", "NVDA", "TSLA", "MSFT", "GOOGL" };
            var quotes = new List<object>();

            foreach (var symbol in symbols)
            {
                try
                {
                    var marketData = await _alpacaService.GetMarketDataAsync(symbol);
                    if (marketData != null)
                    {
                        quotes.Add(new
                        {
                            Symbol = marketData.Symbol,
                            Price = marketData.Price,
                            Change = marketData.ChangePercent24h,
                            Volume = (long)marketData.Volume,
                            MarketCap = "N/A" // Could be fetched from AlphaVantage if needed
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching data for {Symbol}", symbol);
                }
            }

            if (quotes.Count == 0)
            {
                _logger.LogWarning("No market data available, returning mock data");
                return Ok(GetMockQuotes());
            }

            return Ok(quotes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetQuotes");
            return Ok(GetMockQuotes());
        }
    }

    [HttpGet("quote/{symbol}")]
    public async Task<IActionResult> GetQuote(string symbol)
    {
        try
        {
            var marketData = await _alpacaService.GetMarketDataAsync(symbol);
            if (marketData != null)
            {
                return Ok(new
                {
                    Symbol = marketData.Symbol,
                    Price = marketData.Price,
                    Change = marketData.ChangePercent24h,
                    Volume = (long)marketData.Volume,
                    High = marketData.High24h,
                    Low = marketData.Low24h,
                    Timestamp = marketData.Timestamp
                });
            }

            return NotFound(new { Error = $"No data available for {symbol}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching quote for {Symbol}", symbol);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    private static object[] GetMockQuotes()
    {
        return new[]
        {
            new { Symbol = "AAPL", Price = 195.32, Change = 2.45, Volume = 52450000L, MarketCap = "$3.02T" },
            new { Symbol = "NVDA", Price = 523.18, Change = 5.67, Volume = 45820000L, MarketCap = "$1.29T" },
            new { Symbol = "TSLA", Price = 245.80, Change = -1.23, Volume = 115230000L, MarketCap = "$779B" },
            new { Symbol = "MSFT", Price = 412.50, Change = 1.89, Volume = 28450000L, MarketCap = "$3.07T" },
            new { Symbol = "GOOGL", Price = 142.75, Change = 3.21, Volume = 25670000L, MarketCap = "$1.78T" }
        };
    }
}
