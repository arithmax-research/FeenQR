using Alpaca.Markets;
using QuantResearchAgent.Core;
using FeenQR.Core.Models;
using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;
using Skender.Stock.Indicators;
using System.Drawing;
using System.Runtime.Versioning;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PitchController : ControllerBase
    {
        private readonly TechnicalAnalysisService _technicalAnalysisService;
        private readonly AlpacaService _alpacaService;
        private readonly MarketDataService _marketDataService;
        private readonly YahooFinanceService _yahooFinanceService;
        private readonly ILogger<PitchController> _logger;

        public PitchController(
            TechnicalAnalysisService technicalAnalysisService,
            AlpacaService alpacaService,
            MarketDataService marketDataService,
            YahooFinanceService yahooFinanceService,
            ILogger<PitchController> logger)
        {
            _technicalAnalysisService = technicalAnalysisService;
            _alpacaService = alpacaService;
            _marketDataService = marketDataService;
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
                    var fallbackData = await _marketDataService.GetHistoricalDataAsync(cleanSymbol, days);
                    if (fallbackData?.Any() == true)
                    {
                        return Ok(BuildTechnicalChartDtoFromMarketData(fallbackData));
                    }

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
                // OHLCV
                dto.Open = quotes.Select(q => q.Open).ToList();
                dto.High = quotes.Select(q => q.High).ToList();
                dto.Low = quotes.Select(q => q.Low).ToList();
                dto.Volume = quotes.Select(q => q.Volume).ToList();

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

        /// <summary>
        /// Returns a PNG image rendered with System.Drawing for the given symbol showing price and SMAs.
        /// On non-Windows platforms this endpoint returns 501; consider adding a cross-platform renderer like SkiaSharp.
        /// </summary>
        [SupportedOSPlatform("windows")]
        [HttpGet("chartimage/{symbol}")]
        public async Task<IActionResult> GetChartImage(string symbol, [FromQuery] int days = 365)
        {
            try
            {
                var cleanSymbol = symbol.Trim().ToUpper();

                if (!OperatingSystem.IsWindows())
                {
                    return StatusCode(501, new { error = "Chart rendering using System.Drawing is supported on Windows only. Use SkiaSharp or install libgdiplus for Linux/macOS." });
                }
                var bars = await FetchBarsAsync(cleanSymbol, days);
                if (bars == null || bars.Count == 0)
                {
                    var fallbackData = await _marketDataService.GetHistoricalDataAsync(cleanSymbol, days);
                    if (fallbackData?.Any() == true)
                    {
                        bars = fallbackData
                            .Select(d => new AlpacaBar
                            {
                                Symbol = cleanSymbol,
                                TimeUtc = d.Timestamp,
                                Open = (decimal)d.Close,
                                High = (decimal)d.Close,
                                Low = (decimal)d.Close,
                                Close = (decimal)d.Close,
                                Volume = (decimal)d.Volume,
                                Vwap = (decimal)d.Close,
                                TradeCount = 0
                            })
                            .Cast<IBar>()
                            .ToList();
                    }
                    else
                    {
                        return NotFound(new { error = "No data" });
                    }
                }

                var quotes = bars.Select(b => new Skender.Stock.Indicators.Quote
                {
                    Timestamp = b.TimeUtc,
                    Open = b.Open,
                    High = b.High,
                    Low = b.Low,
                    Close = b.Close,
                    Volume = b.Volume
                }).OrderBy(q => q.Timestamp).ToList();

                var sma20 = quotes.ToSma(20).ToList();
                var sma50 = quotes.ToSma(50).ToList();
                var sma200 = quotes.ToSma(200).ToList();

                // Simple fallback renderer using System.Drawing to avoid ScottPlot API mismatches.
                var closes = quotes.Select(q => (double)q.Close).ToArray();
                int width = 2000;
                int height = 1000;
                using var bmp = new Bitmap(width, height);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(12, 18, 26));
                    // margins
                    int left = 80, right = 40, top = 60, bottom = 80;
                    int plotW = width - left - right;
                    int plotH = height - top - bottom;

                    // draw axes
                    using var axisPen = new Pen(Color.FromArgb(80, 130, 180));
                    g.DrawLine(axisPen, left, top + plotH, left + plotW, top + plotH); // x axis
                    g.DrawLine(axisPen, left, top, left, top + plotH); // y axis

                    if (closes.Length > 1)
                    {
                        double min = closes.Min();
                        double max = closes.Max();
                        double range = Math.Max(1e-6, max - min);

                        PointF[] points = new PointF[closes.Length];
                        for (int i = 0; i < closes.Length; i++)
                        {
                            float x = left + (float)(i * (double)plotW / (closes.Length - 1));
                            float y = top + plotH - (float)((closes[i] - min) / range * plotH);
                            points[i] = new PointF(x, y);
                        }

                        using var pricePen = new Pen(Color.CornflowerBlue, 2);
                        g.DrawLines(pricePen, points);

                        // draw simple SMAs if available
                        void DrawSma(IEnumerable<decimal?> smaValues, Color color, float thickness)
                        {
                            var vals = smaValues.Select(v => v.HasValue ? (double?)v.Value : null).ToArray();
                            if (vals.Any(v => v.HasValue))
                            {
                                PointF[] spts = new PointF[vals.Length];
                                for (int i = 0; i < vals.Length; i++)
                                {
                                    double? v = vals[i];
                                    float x = left + (float)(i * (double)plotW / (vals.Length - 1));
                                    float y = top + plotH - (float)(((v ?? min) - min) / range * plotH);
                                    spts[i] = new PointF(x, y);
                                }
                                using var pen = new Pen(color, thickness);
                                g.DrawLines(pen, spts);
                            }
                        }

                        DrawSma(sma20.Select(s => (decimal?)s?.Sma), Color.Orange, 1.5f);
                        DrawSma(sma50.Select(s => (decimal?)s?.Sma), Color.MediumSeaGreen, 1.5f);
                        DrawSma(sma200.Select(s => (decimal?)s?.Sma), Color.Goldenrod, 1.5f);
                    }

                    // title
                    using var titleFont = new Font("Segoe UI", 18, FontStyle.Bold);
                    using var titleBrush = new SolidBrush(Color.White);
                    g.DrawString($"{symbol.ToUpper()} - Close & SMAs (last {days} days)", titleFont, titleBrush, new PointF(left, 12));
                }

                using var ms = new System.IO.MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Seek(0, System.IO.SeekOrigin.Begin);
                return File(ms.ToArray(), "image/png");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rendering chart image for {Symbol}", symbol);
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

        private TechnicalChartDto BuildTechnicalChartDtoFromMarketData(List<MarketData> data)
        {
            var ordered = data.OrderBy(d => d.Timestamp).ToList();
            var quotes = ordered.Select(d => new Skender.Stock.Indicators.Quote
            {
                Timestamp = d.Timestamp,
                Open = (decimal)d.Close,
                High = (decimal)d.Close,
                Low = (decimal)d.Close,
                Close = (decimal)d.Close,
                Volume = (decimal)d.Volume
            }).ToList();

            var dto = new TechnicalChartDto();
            dto.Dates = quotes.Select(q => q.Timestamp.ToString("yyyy-MM-dd")).ToList();
            dto.Close = quotes.Select(q => q.Close).ToList();
            dto.Open = quotes.Select(q => q.Open).ToList();
            dto.High = quotes.Select(q => q.High).ToList();
            dto.Low = quotes.Select(q => q.Low).ToList();
            dto.Volume = quotes.Select(q => q.Volume).ToList();

            var sma20List = quotes.ToSma(20).ToList();
            var sma50List = quotes.ToSma(50).ToList();
            var sma200List = quotes.ToSma(200).ToList();
            for (int i = 0; i < quotes.Count; i++)
            {
                dto.Sma20.Add(i < sma20List.Count ? (decimal?)sma20List[i]?.Sma : null);
                dto.Sma50.Add(i < sma50List.Count ? (decimal?)sma50List[i]?.Sma : null);
                dto.Sma200.Add(i < sma200List.Count ? (decimal?)sma200List[i]?.Sma : null);
            }

            var rsiList = quotes.ToRsi(14).ToList();
            for (int i = 0; i < quotes.Count; i++)
            {
                dto.Rsi.Add(i < rsiList.Count ? rsiList[i]?.Rsi : null);
            }

            var macdList = quotes.ToMacd(12, 26, 9).ToList();
            for (int i = 0; i < quotes.Count; i++)
            {
                dto.Macd.Add(i < macdList.Count ? macdList[i]?.Macd : null);
                dto.MacdSignal.Add(i < macdList.Count ? macdList[i]?.Signal : null);
            }

            return dto;
        }

    }
}

