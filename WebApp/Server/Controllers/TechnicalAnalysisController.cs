using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TechnicalAnalysisController : ControllerBase
    {
        private readonly ILogger<TechnicalAnalysisController> _logger;
        private readonly TechnicalAnalysisService _technicalAnalysisService;

        public TechnicalAnalysisController(
            ILogger<TechnicalAnalysisController> logger,
            TechnicalAnalysisService technicalAnalysisService)
        {
            _logger = logger;
            _technicalAnalysisService = technicalAnalysisService;
        }

        /// <summary>
        /// Perform comprehensive technical analysis on a stock symbol
        /// </summary>
        [HttpPost("analyze")]
        public async Task<IActionResult> AnalyzeSymbol([FromBody] TechnicalAnalysisRequest request)
        {
            try
            {
                _logger.LogInformation("Technical analysis request for {Symbol} with {Days} days lookback", 
                    request.Symbol, request.LookbackDays);

                var analysis = await _technicalAnalysisService.PerformFullAnalysisAsync(
                    request.Symbol, 
                    request.LookbackDays);

                return Ok(new TechnicalAnalysisResponse
                {
                    Symbol = analysis.Symbol,
                    CurrentPrice = analysis.CurrentPrice,
                    OverallSignal = analysis.OverallSignal.ToString(),
                    SignalStrength = analysis.SignalStrength,
                    Reasoning = analysis.Reasoning,
                    Indicators = analysis.Indicators,
                    Timestamp = analysis.Timestamp,
                    Type = "comprehensive-analysis"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in technical analysis for {Symbol}", request.Symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get detailed indicators breakdown by category
        /// </summary>
        [HttpPost("indicators")]
        public async Task<IActionResult> GetDetailedIndicators([FromBody] IndicatorRequest request)
        {
            try
            {
                _logger.LogInformation("Indicator request for {Symbol}, category: {Category}", 
                    request.Symbol, request.Category);

                var analysis = await _technicalAnalysisService.PerformFullAnalysisAsync(request.Symbol);
                var indicators = analysis.Indicators;

                var categorizedIndicators = request.Category.ToLower() switch
                {
                    "trend" => FilterTrendIndicators(indicators),
                    "momentum" => FilterMomentumIndicators(indicators),
                    "volume" => FilterVolumeIndicators(indicators),
                    "volatility" => FilterVolatilityIndicators(indicators),
                    _ => indicators
                };

                return Ok(new IndicatorResponse
                {
                    Symbol = analysis.Symbol,
                    Category = request.Category,
                    Indicators = categorizedIndicators,
                    Timestamp = analysis.Timestamp,
                    Type = "indicators"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting indicators for {Symbol}", request.Symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Compare technical analysis between multiple symbols
        /// </summary>
        [HttpPost("compare")]
        public async Task<IActionResult> CompareSymbols([FromBody] CompareRequest request)
        {
            try
            {
                _logger.LogInformation("Compare request for symbols: {Symbols}", 
                    string.Join(", ", request.Symbols));

                var comparisons = new List<TechnicalAnalysisComparison>();

                foreach (var symbol in request.Symbols)
                {
                    var analysis = await _technicalAnalysisService.PerformFullAnalysisAsync(symbol, request.LookbackDays);
                    comparisons.Add(new TechnicalAnalysisComparison
                    {
                        Symbol = symbol,
                        CurrentPrice = analysis.CurrentPrice,
                        OverallSignal = analysis.OverallSignal.ToString(),
                        SignalStrength = analysis.SignalStrength,
                        RSI = GetIndicatorValue(analysis.Indicators, "RSI"),
                        MACD = GetIndicatorValue(analysis.Indicators, "MACD"),
                        SMA50 = GetIndicatorValue(analysis.Indicators, "SMA_50"),
                        SMA200 = GetIndicatorValue(analysis.Indicators, "SMA_200")
                    });
                }

                return Ok(new CompareResponse
                {
                    Comparisons = comparisons,
                    Timestamp = DateTime.UtcNow,
                    Type = "comparison"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing symbols");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get pattern recognition analysis
        /// </summary>
        [HttpPost("patterns")]
        public async Task<IActionResult> GetPatterns([FromBody] PatternRequest request)
        {
            try
            {
                _logger.LogInformation("Pattern recognition request for {Symbol}", request.Symbol);

                var analysis = await _technicalAnalysisService.PerformFullAnalysisAsync(request.Symbol);
                
                var patterns = analysis.Indicators.TryGetValue("Patterns", out var patternsObj) && 
                              patternsObj is List<string> patternsList 
                    ? patternsList 
                    : new List<string>();

                return Ok(new PatternResponse
                {
                    Symbol = analysis.Symbol,
                    Patterns = patterns,
                    Timestamp = analysis.Timestamp,
                    Type = "patterns"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patterns for {Symbol}", request.Symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get long-term technical analysis (7 years)
        /// </summary>
        [HttpPost("analyze-long")]
        public async Task<IActionResult> AnalyzeSymbolLongTerm([FromBody] TechnicalAnalysisRequest request)
        {
            try
            {
                _logger.LogInformation("Long-term technical analysis request for {Symbol}", request.Symbol);

                // Long-term analysis with ~7 years of data (1800 days)
                var analysis = await _technicalAnalysisService.PerformFullAnalysisAsync(request.Symbol, 1800);

                return Ok(new TechnicalAnalysisResponse
                {
                    Symbol = analysis.Symbol,
                    CurrentPrice = analysis.CurrentPrice,
                    OverallSignal = analysis.OverallSignal.ToString(),
                    SignalStrength = analysis.SignalStrength,
                    Reasoning = analysis.Reasoning,
                    Indicators = analysis.Indicators,
                    Timestamp = analysis.Timestamp,
                    Type = "long-term-analysis"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in long-term technical analysis for {Symbol}", request.Symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Helper methods
        private Dictionary<string, object> FilterTrendIndicators(Dictionary<string, object> indicators)
        {
            var trendKeys = new[] { "SMA_20", "SMA_50", "SMA_200", "EMA_12", "EMA_26", "MACD", "MACD_Signal", 
                "MACD_Histogram", "PSAR", "ADX", "ADX_PlusDI", "ADX_MinusDI", "Aroon_Up", "Aroon_Down", 
                "Aroon_Oscillator", "Ichimoku_TenkanSen", "Ichimoku_KijunSen", "Ichimoku_SenkouSpanA", 
                "Ichimoku_SenkouSpanB" };
            
            return indicators.Where(kvp => trendKeys.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<string, object> FilterMomentumIndicators(Dictionary<string, object> indicators)
        {
            var momentumKeys = new[] { "RSI", "Stoch_K", "Stoch_D", "Williams_R", "ROC_12", "CCI", "MFI", 
                "Ultimate_Oscillator", "TRIX", "StochRSI_K", "StochRSI_D", "ConnorsRSI", "52Week_High", "52Week_Low" };
            
            return indicators.Where(kvp => momentumKeys.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<string, object> FilterVolumeIndicators(Dictionary<string, object> indicators)
        {
            var volumeKeys = new[] { "OBV", "VWAP", "Chaikin_Money_Flow", "Accumulation_Distribution", 
                "Volume_SMA", "Force_Index", "Ease_Of_Movement" };
            
            return indicators.Where(kvp => volumeKeys.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<string, object> FilterVolatilityIndicators(Dictionary<string, object> indicators)
        {
            var volatilityKeys = new[] { "ATR", "BB_Upper", "BB_Middle", "BB_Lower", "BB_PercentB", "BB_Width", 
                "Keltner_Upper", "Keltner_Middle", "Keltner_Lower", "Donchian_Upper", "Donchian_Lower", 
                "Standard_Deviation", "Historical_Volatility" };
            
            return indicators.Where(kvp => volatilityKeys.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private double GetIndicatorValue(Dictionary<string, object> indicators, string key)
        {
            if (indicators.TryGetValue(key, out var value))
            {
                return value is double d ? d : 
                       value is decimal dec ? (double)dec : 0.0;
            }
            return 0.0;
        }
    }

    // Request/Response models
    public class TechnicalAnalysisRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public int LookbackDays { get; set; } = 100;
    }

    public class TechnicalAnalysisResponse
    {
        public string Symbol { get; set; } = string.Empty;
        public double CurrentPrice { get; set; }
        public string OverallSignal { get; set; } = string.Empty;
        public double SignalStrength { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public Dictionary<string, object> Indicators { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    public class IndicatorRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public string Category { get; set; } = "all"; // trend, momentum, volume, volatility, all
    }

    public class IndicatorResponse
    {
        public string Symbol { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public Dictionary<string, object> Indicators { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    public class CompareRequest
    {
        public List<string> Symbols { get; set; } = new();
        public int LookbackDays { get; set; } = 100;
    }

    public class CompareResponse
    {
        public List<TechnicalAnalysisComparison> Comparisons { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
    }

    public class TechnicalAnalysisComparison
    {
        public string Symbol { get; set; } = string.Empty;
        public double CurrentPrice { get; set; }
        public string OverallSignal { get; set; } = string.Empty;
        public double SignalStrength { get; set; }
        public double RSI { get; set; }
        public double MACD { get; set; }
        public double SMA50 { get; set; }
        public double SMA200 { get; set; }
    }

    public class PatternRequest
    {
        public string Symbol { get; set; } = string.Empty;
    }

    public class PatternResponse
    {
        public string Symbol { get; set; } = string.Empty;
        public List<string> Patterns { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}
