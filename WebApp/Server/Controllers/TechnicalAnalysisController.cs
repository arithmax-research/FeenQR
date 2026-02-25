using Microsoft.AspNetCore.Mvc;
using QuantResearchAgent.Services;
using QuantResearchAgent.Core;
using System.Text.Json;

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
        /// Generate comprehensive AI comparison report for multiple symbols
        /// </summary>
        [HttpPost("comprehensive-compare-ai")]
        public async Task<IActionResult> GenerateComprehensiveComparison([FromBody] ComprehensiveCompareRequest request)
        {
            try
            {
                _logger.LogInformation("Comprehensive AI comparison for symbols: {Symbols}", 
                    string.Join(", ", request.Symbols));

                // Build comprehensive comparison data
                var comparisonSummary = new System.Text.StringBuilder();
                comparisonSummary.AppendLine($"Comparison of {request.Comparisons.Count} symbols over {request.LookbackDays} days:\n");

                foreach (var comp in request.Comparisons)
                {
                    comparisonSummary.AppendLine($"**{comp.Symbol}**");
                    comparisonSummary.AppendLine($"- Current Price: ${comp.CurrentPrice:F2}");
                    comparisonSummary.AppendLine($"- Overall Signal: {comp.OverallSignal} (Strength: {comp.SignalStrength:F2})");
                    comparisonSummary.AppendLine($"- RSI: {comp.RSI:F1}");
                    comparisonSummary.AppendLine($"- MACD: {comp.MACD:F4}");
                    comparisonSummary.AppendLine($"- SMA 50: ${comp.SMA50:F2}");
                    comparisonSummary.AppendLine($"- SMA 200: ${comp.SMA200:F2}");
                    comparisonSummary.AppendLine();
                }

                var prompt = $@"You are an expert quantitative analyst. Provide a comprehensive comparative analysis of the following stocks based on their technical indicators:

{comparisonSummary}

Your analysis should include:

1. **Executive Summary**: Which stock(s) show the strongest/weakest technical position and why?

2. **Individual Stock Analysis**: 
   - Brief technical outlook for each stock
   - Key strengths and weaknesses
   - Notable technical patterns or divergences

3. **Comparative Analysis**:
   - Rank stocks by overall technical strength
   - Compare momentum indicators (RSI, MACD)
   - Compare trend strength (SMAs, price position)
   - Identify which stocks are in similar technical phases

4. **Correlation & Sector Insights**:
   - Are these stocks moving together or independently?
   - Which stock shows the most unique technical pattern?

5. **Trading Recommendations**:
   - Best long opportunity (if any)
   - Stocks to avoid or consider shorting
   - Risk-adjusted ranking for allocation
   - Optimal entry points and stop-loss levels

6. **Timeframe Considerations**:
   - Short-term (days-weeks) vs Long-term (months) outlook for each
   - How the {request.LookbackDays}-day analysis impacts interpretation

7. **Risk Assessment**:
   - Which stock has the highest volatility risk?
   - Correlation risks if holding multiple positions
   - Key technical levels that could change the outlook

Provide actionable, specific insights with clear reasoning. Format with clear headings and bullet points.";

                var analysis = await _deepSeekService.GetChatCompletionAsync(prompt);

                return Ok(new AIAnalysisResponse
                {
                    Analysis = analysis,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in comprehensive AI comparison");
                return StatusCode(500, new { error = ex.Message, details = "Make sure DeepSeek API key is configured" });
            }
        }

        /// <summary>
        /// Ask a specific question about the comparison
        /// </summary>
        [HttpPost("ask-comparison-question")]
        public async Task<IActionResult> AskComparisonQuestion([FromBody] ComparisonQuestionRequest request)
        {
            try
            {
                _logger.LogInformation("AI comparison question for symbols: {Symbols} - {Question}", 
                    string.Join(", ", request.Symbols), request.Question);

                var comparisonSummary = new System.Text.StringBuilder();
                foreach (var comp in request.Comparisons)
                {
                    comparisonSummary.AppendLine($"{comp.Symbol}: ${comp.CurrentPrice:F2}, Signal: {comp.OverallSignal}, RSI: {comp.RSI:F1}, MACD: {comp.MACD:F4}");
                }

                var prompt = $@"Context: You are analyzing a comparison of these stocks over {request.LookbackDays} days:

{comparisonSummary}

";

                if (!string.IsNullOrEmpty(request.PreviousReport))
                {
                    prompt += $"Your previous comprehensive analysis:\n{request.PreviousReport}\n\n";
                }

                prompt += $"Question: {request.Question}\n\nProvide a detailed, actionable answer based on technical analysis principles.";

                var answer = await _deepSeekService.GetChatCompletionAsync(prompt);

                return Ok(new AIAnalysisResponse
                {
                    Analysis = answer,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AI comparison question");
                return StatusCode(500, new { error = ex.Message, details = "Make sure DeepSeek API key is configured" });
            }
        }

        /// <summary>
        /// Generate comprehensive AI indicator analysis
        /// </summary>
        [HttpPost("comprehensive-indicator-ai")]
        public async Task<IActionResult> GenerateComprehensiveIndicatorAnalysis([FromBody] IndicatorAnalysisRequest request)
        {
            try
            {
                _logger.LogInformation("Comprehensive AI indicator analysis for {Symbol}, Category: {Category}", 
                    request.Symbol, request.Category);

                // Build indicator summary
                var indicatorSummary = new System.Text.StringBuilder();
                indicatorSummary.AppendLine($"Technical Indicators for {request.Symbol} ({request.Category.ToUpper()})");
                indicatorSummary.AppendLine($"Analysis Period: {request.LookbackDays} days\n");
                
                if (request.Indicators != null && request.Indicators.Count > 0)
                {
                    indicatorSummary.AppendLine("**Current Indicator Values:**");
                    foreach (var indicator in request.Indicators)
                    {
                        indicatorSummary.AppendLine($"- {indicator.Key}: {FormatIndicatorValue(indicator.Value)}");
                    }
                }

                var prompt = $@"You are an expert technical analyst specializing in {request.Category} indicators. Provide a comprehensive analysis of the following technical indicators for {request.Symbol}:

{indicatorSummary}

Your analysis should include:

1. **Indicator Interpretation**: What is each indicator telling us about the stock's current state?

2. **Signal Convergence/Divergence**: Are the indicators aligned or showing conflicting signals? What does this mean?

3. **Trading Implications**: 
   - Current market position (overbought/oversold, trending/ranging, etc.)
   - Potential entry/exit points
   - Support and resistance levels suggested by indicators

4. **Risk Assessment**: Key risks indicated by these technical metrics

5. **Time Horizon**: Are these indicators better suited for short-term, medium-term, or long-term trading decisions?

6. **Actionable Recommendations**: Specific trading strategies based on these indicators

Provide clear, actionable insights formatted with markdown headings and bullet points.";

                var analysis = await _deepSeekService.GetChatCompletionAsync(prompt);

                return Ok(new Dictionary<string, object>
                {
                    { "analysis", analysis },
                    { "timestamp", DateTime.UtcNow }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in comprehensive indicator AI analysis");
                return StatusCode(500, new { error = ex.Message, details = "Make sure DeepSeek API key is configured" });
            }
        }

        /// <summary>
        /// Answer specific questions about indicator analysis
        /// </summary>
        [HttpPost("ask-indicator-question")]
        public async Task<IActionResult> AskIndicatorQuestion([FromBody] IndicatorQuestionRequest request)
        {
            try
            {
                _logger.LogInformation("AI indicator question for {Symbol} - {Question}", 
                    request.Symbol, request.Question);

                var indicatorSummary = new System.Text.StringBuilder();
                indicatorSummary.AppendLine($"{request.Symbol} ({request.Category} indicators, {request.LookbackDays} day period):");
                
                if (request.Indicators != null && request.Indicators.Count > 0)
                {
                    foreach (var indicator in request.Indicators)
                    {
                        indicatorSummary.AppendLine($"- {indicator.Key}: {FormatIndicatorValue(indicator.Value)}");
                    }
                }

                var prompt = $@"Context: You are analyzing technical indicators for {request.Symbol}:

{indicatorSummary}

";

                if (!string.IsNullOrEmpty(request.CurrentAnalysis))
                {
                    prompt += $"Your previous analysis:\n{request.CurrentAnalysis}\n\n";
                }

                prompt += $"Question: {request.Question}\n\nProvide a detailed, technical answer based on these indicators.";

                var answer = await _deepSeekService.GetChatCompletionAsync(prompt);

                return Ok(new Dictionary<string, object>
                {
                    { "answer", answer },
                    { "timestamp", DateTime.UtcNow }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AI indicator question");
                return StatusCode(500, new { error = ex.Message, details = "Make sure DeepSeek API key is configured" });
            }
        }

        /// <summary>
        /// Helper method to format indicator values
        /// </summary>
        private string FormatIndicatorValue(object? value)
        {
            if (value == null) return "N/A";
            
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.Number => jsonElement.GetDouble().ToString("F2"),
                    JsonValueKind.String => jsonElement.GetString() ?? "N/A",
                    _ => value.ToString() ?? "N/A"
                };
            }
            
            if (value is double d) return d.ToString("F2");
            if (value is decimal dec) return dec.ToString("F2");
            if (value is float f) return f.ToString("F2");
            
            return value.ToString() ?? "N/A";
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

    public class ComprehensiveCompareRequest
    {
        public List<TechnicalAnalysisComparison> Comparisons { get; set; } = new();
        public List<string> Symbols { get; set; } = new();
        public int LookbackDays { get; set; }
    }

    public class ComparisonQuestionRequest
    {
        public List<TechnicalAnalysisComparison> Comparisons { get; set; } = new();
        public List<string> Symbols { get; set; } = new();
        public string Question { get; set; } = string.Empty;
        public string PreviousReport { get; set; } = string.Empty;
        public int LookbackDays { get; set; }
    }

    public class IndicatorAnalysisRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public string Category { get; set; } = "all";
        public int LookbackDays { get; set; }
        public Dictionary<string, object>? Indicators { get; set; }
    }

    public class IndicatorQuestionRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public string Category { get; set; } = "all";
        public int LookbackDays { get; set; }
        public Dictionary<string, object>? Indicators { get; set; }
        public string CurrentAnalysis { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
    }}
