using FeenQR.Core.Models;
using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockPitchController : ControllerBase
    {
        private readonly StockPitchService _stockPitchService;
        private readonly ILogger<StockPitchController> _logger;

        public StockPitchController(
            StockPitchService stockPitchService,
            ILogger<StockPitchController> logger)
        {
            _stockPitchService = stockPitchService;
            _logger = logger;
        }

        /// <summary>
        /// Generate a complete institutional-quality stock pitch for a given symbol.
        /// Uses DeepSeek/LLM combined with real research data from all available
        /// data sources (Fundamental Analysis, Valuation, News/Sentiment, SEC Filings,
        /// Earnings, Technical Analysis, etc.)
        /// </summary>
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateStockPitch([FromBody] StockPitchRequest request)
        {
            try
            {
                _logger.LogInformation("Generating stock pitch for {Symbol}, position: {PositionType}", 
                    request.Symbol, request.PositionType);

                if (string.IsNullOrWhiteSpace(request.Symbol))
                {
                    return BadRequest(new { error = "Symbol is required" });
                }

                var result = await _stockPitchService.GenerateStockPitchAsync(
                    request.Symbol.Trim().ToUpper(), 
                    request.PositionType ?? "auto");

                if (!result.Success)
                {
                    return StatusCode(500, new { 
                        error = "Failed to generate stock pitch", 
                        details = string.Join("; ", result.Errors),
                        symbol = result.Symbol
                    });
                }

                return Ok(new
                {
                    success = true,
                    symbol = result.Symbol,
                    positionType = result.PositionType,
                    targetPrice = result.TargetPriceFormatted,
                    expectedReturn = result.ExpectedReturnFormatted,
                    conviction = result.Conviction,
                    timeHorizon = result.TimeHorizon,
                    generatedAt = result.GeneratedAt,
                    generationTimeMs = result.GenerationTimeMs,
                    dataSourcesUsed = result.DataSourcesUsed,
                    fundamentals = BuildFundamentals(result),
                    citations = BuildCitations(result),
                    pitch = new
                    {
                        stockRecommendation = result.StockRecommendation,
                        companyOverview = result.CompanyOverview,
                        investmentThesis = result.InvestmentThesis,
                        catalysts = result.Catalysts,
                        valuationAndReturns = result.ValuationAndReturns,
                        risksAndMitigation = result.RisksAndMitigation,
                        conclusion = result.Conclusion
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating stock pitch for {Symbol}", request.Symbol);
                return StatusCode(500, new { error = $"Internal error: {ex.Message}" });
            }
        }

        private static Dictionary<string, string> BuildFundamentals(global::QuantResearchAgent.Services.StockPitchResult result)
        {
            var fundamentals = new Dictionary<string, string>();

            if (result.CompanyOverviewData != null)
            {
                var c = result.CompanyOverviewData;
                fundamentals["Company"] = c.CompanyName;
                fundamentals["Sector"] = c.Sector;
                fundamentals["Industry"] = c.Industry;
                fundamentals["Market Cap"] = c.MarketCap > 0 ? $"${c.MarketCap:N0}" : "N/A";
                fundamentals["P/E Ratio"] = c.PERatio > 0 ? c.PERatio.ToString("F2") : "N/A";
                fundamentals["Dividend Yield"] = c.DividendYield > 0 ? c.DividendYield.ToString("P2") : "N/A";
                fundamentals["ROE"] = c.ReturnOnEquityTTM > 0 ? c.ReturnOnEquityTTM.ToString("P2") : "N/A";
                fundamentals["ROA"] = c.ReturnOnAssetsTTM > 0 ? c.ReturnOnAssetsTTM.ToString("P2") : "N/A";
                fundamentals["52W High"] = c.FiftyTwoWeekHigh > 0 ? $"${c.FiftyTwoWeekHigh:F2}" : "N/A";
                fundamentals["52W Low"] = c.FiftyTwoWeekLow > 0 ? $"${c.FiftyTwoWeekLow:F2}" : "N/A";
            }

            if (result.ValuationData != null)
            {
                var v = result.ValuationData;
                fundamentals["Target Price"] = v.AnalystTargetPrice > 0 ? $"${v.AnalystTargetPrice:F2}" : "N/A";
                fundamentals["Forward P/E"] = v.ForwardPE > 0 ? v.ForwardPE.ToString("F2") : "N/A";
                fundamentals["PEG Ratio"] = v.PEGRatio > 0 ? v.PEGRatio.ToString("F2") : "N/A";
                fundamentals["EV/EBITDA"] = v.EVToEBITDA > 0 ? v.EVToEBITDA.ToString("F2") : "N/A";
                fundamentals["EV/Revenue"] = v.EVToRevenue > 0 ? v.EVToRevenue.ToString("F2") : "N/A";
            }

            if (result.AnalystData != null)
            {
                fundamentals["Consensus Rating"] = result.AnalystData.ConsensusRating ?? "N/A";
                fundamentals["Analyst Count"] = result.AnalystData.NumberOfAnalysts > 0 ? result.AnalystData.NumberOfAnalysts.ToString() : "N/A";
            }

            if (result.MarketData?.Any() == true)
            {
                var latest = result.MarketData.OrderByDescending(x => x.Timestamp).First();
                fundamentals["Last Price"] = latest.Close > 0 ? $"${latest.Close:F2}" : "N/A";
                fundamentals["Last Volume"] = latest.Volume > 0 ? latest.Volume.ToString("N0") : "N/A";
                fundamentals["Source"] = latest.Source;
            }

            return fundamentals;
        }

        private static List<global::FeenQR.Core.Models.Citation> BuildCitations(global::QuantResearchAgent.Services.StockPitchResult result)
        {
            var citations = new List<global::FeenQR.Core.Models.Citation>();
            var symbol = result.Symbol?.ToUpperInvariant() ?? string.Empty;

            void Add(string title, string url, string source, string description)
            {
                citations.Add(new global::FeenQR.Core.Models.Citation
                {
                    Title = title,
                    Url = url,
                    Source = source,
                    Description = description,
                    PublishedAt = DateTime.UtcNow
                });
            }

            if (result.DataSourcesUsed.Any(x => x.Contains("MarketData", StringComparison.OrdinalIgnoreCase)))
                Add($"{symbol} Market Data", $"/api/pitch/technicalcharts/{symbol}", "MarketData", "Historical technical and market data used in the pitch.");

            if (result.CompanyOverviewData != null || result.ValuationData != null)
                Add($"{symbol} Yahoo Finance", $"https://finance.yahoo.com/quote/{symbol}", "Yahoo Finance", "Price, valuation, and company fundamentals reference.");

            if (result.SecFilingData != null)
                Add($"{symbol} SEC Filings", $"https://www.sec.gov/edgar/search/#/q={symbol}", "SEC", "Recent SEC filing context used in the pitch.");

            if (result.SentimentData?.NewsItems?.Any() == true)
                Add($"{symbol} News Sentiment", "https://news.google.com/search?q=" + Uri.EscapeDataString(symbol + " stock"), "News", "Recent news and sentiment context used in the pitch.");

            if (!citations.Any())
                Add($"{symbol} Research", $"https://finance.yahoo.com/quote/{symbol}", "Research", "Primary market research reference.");

            return citations;
        }

        /// <summary>
        /// Quick endpoint that generates a summary-level stock pitch (faster, less detail).
        /// </summary>
        [HttpPost("quick")]
        public async Task<IActionResult> QuickStockPitch([FromBody] StockPitchRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Symbol))
                {
                    return BadRequest(new { error = "Symbol is required" });
                }

                var result = await _stockPitchService.GenerateStockPitchAsync(
                    request.Symbol.Trim().ToUpper(), 
                    request.PositionType ?? "auto");

                if (!result.Success)
                {
                    return StatusCode(500, new { error = "Failed to generate stock pitch" });
                }

                // Return a condensed summary
                return Ok(new
                {
                    success = true,
                    symbol = result.Symbol,
                    summary = result.Conclusion,
                    recommendation = result.StockRecommendation?.Split('\n').FirstOrDefault() ?? result.PositionType,
                    targetPrice = result.TargetPriceFormatted,
                    expectedReturn = result.ExpectedReturnFormatted,
                    conviction = result.Conviction,
                    generatedAt = result.GeneratedAt,
                    dataSourcesUsed = result.DataSourcesUsed
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in quick pitch for {Symbol}", request.Symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Health check endpoint.
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { 
                status = "healthy", 
                service = "StockPitchService",
                timestamp = DateTime.UtcNow 
            });
        }
    }

    public class StockPitchRequest
    {
        /// <summary>Stock ticker symbol (e.g., AAPL, MSFT, TSLA)</summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// Position type: "long", "short", or "auto" (let AI decide based on data).
        /// Default is "auto".
        /// </summary>
        public string? PositionType { get; set; } = "auto";
    }
}
