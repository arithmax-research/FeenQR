using Alpaca.Markets;
using FeenQR.Core.Models;
using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;
using Skender.Stock.Indicators;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PitchController : ControllerBase
    {
        private readonly TechnicalAnalysisService _technicalAnalysisService;
        private readonly AlpacaService _alpacaService;
        private readonly YahooFinanceService _yahooFinanceService;
        private readonly ILogger<PitchController> _logger;

        public PitchController(
            TechnicalAnalysisService technicalAnalysisService,
            AlpacaService alpacaService,
            YahooFinanceService yahooFinanceService,
            ILogger<PitchController> logger)
        {
            _technicalAnalysisService = technicalAnalysisService;
            _alpacaService = alpacaService;
            _yahooFinanceService = yahooFinanceService;
            _logger = logger;
        }

        /// <summary>
        /// Returns technical chart data (prices, SMAs, RSI, MACD) for Chart.js rendering.
        /// </summary>
        [HttpGet("technicalcharts/{symbol}")]
        public async Task<IActionResult> GetTechnicalCharts(string symbol, [FromQuery] int days = 365)
        {
            try
            {
                var cleanSymbol = symbol.Trim().ToUpper();
                _logger.LogInformation("Fetching technical chart data for {Symbol}, days={Days}", cleanSymbol, days);

                // Fetch bars from Alpaca, fallback to TechnicalAnalysisService data
                var bars = await FetchBarsAsync(cleanSymbol, days);
                if (bars == null || bars.Count == 0)
                {
                    return Ok(new TechnicalChartDto()); // empty
                }

                // Convert to quotes for indicator computation
                var quotes = bars.Select(b => new Skender.Stock.Indicators.Quote
                {
                    Timestamp = b.TimeUtc,
                    Open = b.Open,
                    High = b.High,
                    Low = b.Low,
                    Close = b.Close,
                    Volume = b.Volume
                }).OrderBy(q => q.Timestamp).ToList();

                var dto = new TechnicalChartDto();

                // Dates
                dto.Dates = quotes.Select(q => q.Timestamp.ToString("yyyy-MM-dd")).ToList();

                // Close prices
                dto.Close = quotes.Select(q => q.Close).ToList();

                // SMAs
                var sma20List = quotes.ToSma(20).ToList();
                var sma50List = quotes.ToSma(50).ToList();
                var sma200List = quotes.ToSma(200).ToList();

                for (int i = 0; i < quotes.Count; i++)
                {
                    dto.Sma20.Add(i < sma20List.Count ? (decimal?)sma20List[i]?.Sma : null);
                    dto.Sma50.Add(i < sma50List.Count ? (decimal?)sma50List[i]?.Sma : null);
                    dto.Sma200.Add(i < sma200List.Count ? (decimal?)sma200List[i]?.Sma : null);
                }

                // RSI
                var rsiList = quotes.ToRsi(14).ToList();
                for (int i = 0; i < quotes.Count; i++)
                {
                    dto.Rsi.Add(i < rsiList.Count ? rsiList[i]?.Rsi : null);
                }

                // MACD
                var macdList = quotes.ToMacd(12, 26, 9).ToList();
                for (int i = 0; i < quotes.Count; i++)
                {
                    dto.Macd.Add(i < macdList.Count ? macdList[i]?.Macd : null);
                    dto.MacdSignal.Add(i < macdList.Count ? macdList[i]?.Signal : null);
                }

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching technical charts for {Symbol}", symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task<List<IBar>> FetchBarsAsync(string symbol, int days)
        {
            // Try Alpaca first
            try
            {
                var bars = await _alpacaService.GetHistoricalBarsAsync(symbol, days);
                if (bars != null && bars.Count > 0)
                    return bars;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Alpaca failed to fetch bars for {Symbol}", symbol);
            }

            // Fallback: try to get data from the technical analysis service's internal data
            try
            {
                // Use TechnicalAnalysisService which also fetches via Alpaca
                var analysis = await _technicalAnalysisService.PerformFullAnalysisAsync(symbol, days);
                // The analysis object has limited bar data, try to construct from it
                if (analysis?.Indicators != null)
                {
                    _logger.LogInformation("Got technical analysis for {Symbol}, but cannot reconstruct full bars from it", symbol);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TechnicalAnalysisService fallback failed for {Symbol}", symbol);
            }

            return new List<IBar>();
        }
    }
}

