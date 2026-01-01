using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;

namespace Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketDataController : ControllerBase
{
    private readonly ILogger<MarketDataController> _logger;
    private readonly AlpacaService _alpacaService;
    private readonly AlphaVantageService _alphaVantageService;
    private readonly YahooFinanceService _yahooService;
    private readonly PolygonService _polygonService;
    private readonly DataBentoService _databentoService;

    public MarketDataController(
        ILogger<MarketDataController> logger,
        AlpacaService alpacaService,
        AlphaVantageService alphaVantageService,
        YahooFinanceService yahooService,
        PolygonService polygonService,
        DataBentoService databentoService)
    {
        _logger = logger;
        _alpacaService = alpacaService;
        _alphaVantageService = alphaVantageService;
        _yahooService = yahooService;
        _polygonService = polygonService;
        _databentoService = databentoService;
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

    [HttpGet("yahoo/{symbol}")]
    public async Task<IActionResult> GetYahooData(string symbol)
    {
        try
        {
            var data = await _yahooService.GetMarketDataAsync(symbol);
            if (data == null)
            {
                return NotFound(new { Error = $"No data found for symbol: {symbol}" });
            }
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Yahoo data for {Symbol}", symbol);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("polygon/quote/{symbol}")]
    public async Task<IActionResult> GetPolygonQuote(string symbol)
    {
        try
        {
            var quote = await _polygonService.GetQuoteAsync(symbol);
            if (quote == null)
            {
                return NotFound(new { Error = $"No quote found for symbol: {symbol}" });
            }
            return Ok(quote);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Polygon quote for {Symbol}", symbol);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("polygon/daily/{symbol}")]
    public async Task<IActionResult> GetPolygonDailyBar(string symbol, [FromQuery] DateTime? date = null)
    {
        try
        {
            var targetDate = date ?? DateTime.UtcNow.AddDays(-1);
            var bar = await _polygonService.GetDailyBarAsync(symbol, targetDate);
            if (bar == null)
            {
                return NotFound(new { Error = $"No daily bar found for symbol: {symbol}" });
            }
            return Ok(bar);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Polygon daily bar for {Symbol}", symbol);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("polygon/aggregates/{symbol}")]
    public async Task<IActionResult> GetPolygonAggregates(
        string symbol, 
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string timespan = "day")
    {
        try
        {
            var fromDate = from ?? DateTime.UtcNow.AddMonths(-3);
            var toDate = to ?? DateTime.UtcNow;
            
            var aggregates = await _polygonService.GetAggregatesAsync(symbol, 1, timespan, fromDate, toDate);
            if (aggregates == null || aggregates.Count == 0)
            {
                return NotFound(new { Error = $"No aggregates found for symbol: {symbol}" });
            }
            return Ok(aggregates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Polygon aggregates for {Symbol}", symbol);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("polygon/financials/{symbol}")]
    public async Task<IActionResult> GetPolygonFinancials(string symbol)
    {
        try
        {
            var financials = await _polygonService.GetFinancialsAsync(symbol);
            if (financials == null)
            {
                return NotFound(new { Error = $"No financials found for symbol: {symbol}" });
            }
            return Ok(financials);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Polygon financials for {Symbol}", symbol);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("databento/{symbol}")]
    public async Task<IActionResult> GetDatabentoData(string symbol)
    {
        try
        {
            // DataBentoService doesn't have GetMarketDataAsync - use GetOHLCVAsync instead
            var data = await _databentoService.GetOHLCVAsync(symbol, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
            if (data == null || data.Count == 0)
            {
                return NotFound(new { Error = $"No data found for symbol: {symbol}" });
            }
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Databento data for {Symbol}", symbol);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpGet("multi-quotes")]
    public async Task<IActionResult> GetMultipleQuotes([FromQuery] string symbols)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(symbols))
            {
                return BadRequest(new { Error = "Symbols parameter is required" });
            }

            var symbolList = symbols.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList();

            var quotes = new Dictionary<string, object>();
            
            foreach (var symbol in symbolList)
            {
                try
                {
                    // Try Yahoo first as it's most reliable for real-time data
                    var data = await _yahooService.GetMarketDataAsync(symbol);
                    if (data != null)
                    {
                        quotes[symbol] = data;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error fetching data for {Symbol}", symbol);
                    quotes[symbol] = new { error = "Data unavailable" };
                }
            }

            return Ok(quotes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching multiple quotes");
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
