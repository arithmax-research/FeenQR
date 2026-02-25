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
        private readonly DeepSeekService _deepSeekService;

        public TechnicalAnalysisController(
            ILogger<TechnicalAnalysisController> logger,
            TechnicalAnalysisService technicalAnalysisService,
            DeepSeekService deepSeekService)
        {
            _logger = logger;
            _technicalAnalysisService = technicalAnalysisService;
            _deepSeekService = deepSeekService;
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
                _logger.LogInformation("Pattern recognition request for {Symbol} with {Days} days lookback", 
                    request.Symbol, request.LookbackDays);

                var analysis = await _technicalAnalysisService.PerformFullAnalysisAsync(
                    request.Symbol, 
                    request.LookbackDays);
                
                var patterns = analysis.Indicators.TryGetValue("Patterns", out var patternsObj) && 
                              patternsObj is List<string> patternsList 
                    ? patternsList 
                    : new List<string>();

                var patternDetails = analysis.Indicators.TryGetValue("PatternDetails", out var detailsObj) &&
                                    detailsObj is List<PatternDetection> detailsList
                    ? detailsList
                    : new List<PatternDetection>();

                return Ok(new PatternResponse
                {
                    Symbol = analysis.Symbol,
                    Patterns = patterns,
                    PatternDetails = patternDetails.Select(pd => new PatternDetail
                    {
                        PatternName = pd.PatternName,
                        Explanation = pd.Explanation
                    }).ToList(),
                    Timestamp = analysis.Timestamp,
                    Type = "patterns",
                    LookbackDays = request.LookbackDays
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patterns for {Symbol}", request.Symbol);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get AI analysis of detected patterns using DeepSeek
        /// </summary>
        [HttpPost("analyze-patterns-ai")]
        public async Task<IActionResult> AnalyzePatternsWithAI([FromBody] AIPatternAnalysisRequest request)
        {
            try
            {
                _logger.LogInformation("AI pattern analysis request for {Symbol}", request.Symbol);

                var patternsList = string.Join(", ", request.Patterns.Select(p => p.PatternName));
                var patternDetails = string.Join("\n", request.Patterns.Select(p => 
                    $"- {p.PatternName}: {p.Explanation}"));

                var prompt = $@"You are an expert technical analyst. Analyze the following chart patterns detected in {request.Symbol} over the past {request.LookbackDays} days:

{patternDetails}

Provide a comprehensive analysis covering:
1. What these patterns indicate about the current market sentiment
2. Potential price movements or trends these patterns suggest
3. Key support and resistance levels to watch
4. Trading recommendations (bullish/bearish outlook)
5. Risk factors to consider

Keep the analysis concise but actionable.";

                var analysis = await _deepSeekService.GetChatCompletionAsync(prompt);

                return Ok(new AIAnalysisResponse
                {
                    Analysis = analysis,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AI pattern analysis for {Symbol}", request.Symbol);
                return StatusCode(500, new { error = ex.Message, details = "Make sure DeepSeek API key is configured" });
            }
        }

        /// <summary>
        /// Ask a specific question about detected patterns using DeepSeek
        /// </summary>
        [HttpPost("ask-pattern-question")]
        public async Task<IActionResult> AskPatternQuestion([FromBody] AIPatternQuestionRequest request)
        {
            try
            {
                _logger.LogInformation("AI pattern question for {Symbol}: {Question}", 
                    request.Symbol, request.Question);

                var patternDetails = string.Join("\n", request.Patterns.Select(p => 
                    $"- {p.PatternName}: {p.Explanation}"));

                var prompt = $@"Context: You previously analyzed chart patterns for {request.Symbol}:

{patternDetails}

";

                if (!string.IsNullOrEmpty(request.PreviousAnalysis))
                {
                    prompt += $"Your previous analysis:\n{request.PreviousAnalysis}\n\n";
                }

                prompt += $"Question: {request.Question}\n\nProvide a clear, concise answer based on technical analysis principles.";

                var answer = await _deepSeekService.GetChatCompletionAsync(prompt);

                return Ok(new AIAnalysisResponse
                {
                    Analysis = answer,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AI pattern question for {Symbol}", request.Symbol);
                return StatusCode(500, new { error = ex.Message, details = "Make sure DeepSeek API key is configured" });
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
        public int LookbackDays { get; set; } = 365;
    }

    public class AIPatternAnalysisRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public List<PatternDetail> Patterns { get; set; } = new();
        public int LookbackDays { get; set; }
    }

    public class AIPatternQuestionRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public List<PatternDetail> Patterns { get; set; } = new();
        public string Question { get; set; } = string.Empty;
        public string PreviousAnalysis { get; set; } = string.Empty;
    }

    public class AIAnalysisResponse
    {
        public string Analysis { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class PatternResponse
    {
        public string Symbol { get; set; } = string.Empty;
        public List<string> Patterns { get; set; } = new();
        public List<PatternDetail> PatternDetails { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
        public int LookbackDays { get; set; }
    }

    public class PatternDetail
    {
        public string PatternName { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
    }
}
