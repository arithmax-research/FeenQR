using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using QuantResearchAgent.Plugins;
using System.Text;
using System.Text.Json;
using System.Net.Http;

namespace QuantResearchAgent.Services.ResearchAgents
{
    /// <summary>
    /// Agent that researches stocks/companies and generates comprehensive trading strategy templates
    /// </summary>
    public class TradingTemplateGeneratorAgent
    {
        private readonly Kernel _kernel;
        private readonly ILogger<TradingTemplateGeneratorAgent> _logger;
        private readonly IConfiguration _configuration;
        private readonly MarketDataService _marketDataService;
        private readonly CompanyValuationService _companyValuationService;
        private readonly TechnicalAnalysisService _technicalAnalysisService;
        private readonly NewsSentimentAnalysisService _newsSentimentService;
        private readonly IWebSearchPlugin _webSearchPlugin;
        private readonly HttpClient _httpClient;
        private readonly ILLMService _llmService;
        private readonly WebDataExtractionService _webDataExtractionService;
        private readonly DeepSeekService _deepSeekService;

        public TradingTemplateGeneratorAgent(
            Kernel kernel,
            ILogger<TradingTemplateGeneratorAgent> logger,
            IConfiguration configuration,
            MarketDataService marketDataService,
            CompanyValuationService companyValuationService,
            TechnicalAnalysisService technicalAnalysisService,
            NewsSentimentAnalysisService newsSentimentService,
            IWebSearchPlugin webSearchPlugin,
            HttpClient httpClient,
            ILLMService llmService,
            WebDataExtractionService webDataExtractionService,
            DeepSeekService deepSeekService)
        {
            _kernel = kernel;
            _logger = logger;
            _configuration = configuration;
            _marketDataService = marketDataService;
            _companyValuationService = companyValuationService;
            _technicalAnalysisService = technicalAnalysisService;
            _newsSentimentService = newsSentimentService;
            _webSearchPlugin = webSearchPlugin;
            _httpClient = httpClient;
            _llmService = llmService;
            _webDataExtractionService = webDataExtractionService;
            _deepSeekService = deepSeekService;
        }

        /// <summary>
        /// Generate a comprehensive trading template for a given stock symbol
        /// </summary>
        public async Task<TradingTemplate> GenerateTradingTemplateAsync(string symbol, string strategyType = "swing")
        {
            _logger.LogInformation($"Generating trading template for {symbol} with strategy type: {strategyType}");

            try
            {
                // Gather comprehensive research data
                var researchData = await GatherResearchDataAsync(symbol);

                // Generate strategy parameters
                var strategyParams = await GenerateStrategyParametersAsync(symbol, researchData);

                // Create entry conditions
                var entryConditions = await GenerateEntryConditionsAsync(symbol, strategyType, researchData);

                // Define exit framework
                var exitFramework = await GenerateExitFrameworkAsync(symbol, researchData);

                // Create risk management rules
                var riskManagement = await GenerateRiskManagementAsync(symbol, researchData);

                // Generate technical indicators
                var technicalIndicators = await GenerateTechnicalIndicatorsAsync(symbol, researchData);

                // Define data requirements
                var dataRequirements = GenerateDataRequirements(symbol);

                // Create backtest configuration
                var backtestConfig = GenerateBacktestConfiguration(symbol);

                // Identify limitations and notes
                var limitations = await GenerateLimitationsAsync(symbol, researchData);
                var implementationNotes = GenerateImplementationNotes(symbol);

                // Compile the complete template
                var template = new TradingTemplate
                {
                    Symbol = symbol,
                    StrategyType = strategyType,
                    GeneratedAt = DateTime.UtcNow,
                    StrategyParameters = strategyParams,
                    EntryConditions = entryConditions,
                    ExitFramework = exitFramework,
                    RiskManagement = riskManagement,
                    TechnicalIndicators = technicalIndicators,
                    DataRequirements = dataRequirements,
                    BacktestConfiguration = backtestConfig,
                    KnownLimitations = limitations,
                    ImplementationNotes = implementationNotes
                };

                // Save template to file
                await SaveTemplateToFileAsync(template);

                return template;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating trading template for {symbol}");
                throw;
            }
        }

        private async Task<ResearchData> GatherResearchDataAsync(string symbol)
        {
            var researchData = new ResearchData();

            try
            {
                // Get market data
                researchData.MarketData = await _marketDataService.GetMarketDataAsync(symbol);

                // Get company fundamentals using AnalyzeStockAsync
                researchData.CompanyInfoString = await _companyValuationService.AnalyzeStockAsync(symbol);

                // Get technical analysis
                researchData.TechnicalAnalysis = await _technicalAnalysisService.PerformFullAnalysisAsync(symbol);

                // Get news sentiment
                researchData.SentimentAnalysis = await _newsSentimentService.AnalyzeSymbolSentimentAsync(symbol);

                // Enhanced web research using structured data extraction
                var webResearchData = await PerformEnhancedWebResearchAsync(symbol);
                researchData.WebResearch = webResearchData.CombinedText;
                researchData.StructuredWebData = webResearchData.StructuredData;

                // Get historical volatility and key levels
                researchData.VolatilityData = await CalculateVolatilityMetricsAsync(symbol);
                researchData.KeyLevels = await IdentifyKeyLevelsAsync(symbol);

                // Detect chart patterns and market regimes
                researchData.DetectedPatterns = await DetectChartPatternsAsync(symbol);
                researchData.MarketRegime = await DetermineMarketRegimeAsync(symbol);

            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error gathering research data for {symbol}");
            }

            return researchData;
        }

        private async Task<WebResearchResult> PerformEnhancedWebResearchAsync(string symbol)
        {
            var result = new WebResearchResult();
            var combinedText = new StringBuilder();
            var structuredData = new Dictionary<string, object>();

            try
            {
                // Get initial search results for relevant URLs
                var searchResults = await _webSearchPlugin.SearchAsync($"{symbol} stock analysis trading strategy research financial reports");

                // Extract data from top financial websites
                var urlsToExtract = new List<string>();

                // Add specific financial websites
                urlsToExtract.AddRange(new[] {
                    $"https://finance.yahoo.com/quote/{symbol}",
                    $"https://www.marketwatch.com/invest/stock/{symbol}",
                    $"https://seekingalpha.com/symbol/{symbol}",
                    $"https://www.zacks.com/stock/quote/{symbol}"
                });

                // Add URLs from search results (top 3)
                urlsToExtract.AddRange(searchResults.Take(3).Select(r => r.Url));

                // Extract structured data from each URL
                foreach (var url in urlsToExtract)
                {
                    try
                    {
                        var extractionResult = await _webDataExtractionService.ExtractStructuredDataAsync(url, "financial");

                        // Add to combined text
                        combinedText.AppendLine($"Source: {extractionResult.Title}");
                        combinedText.AppendLine($"URL: {extractionResult.Url}");
                        combinedText.AppendLine($"Content: {extractionResult.Content}");
                        combinedText.AppendLine($"Financial Data: {JsonSerializer.Serialize(extractionResult.FinancialData)}");
                        combinedText.AppendLine("---");

                        // Merge structured data
                        foreach (var kvp in extractionResult.StructuredData)
                        {
                            structuredData[kvp.Key] = kvp.Value;
                        }

                        // Add financial data
                        foreach (var kvp in extractionResult.FinancialData)
                        {
                            structuredData[$"financial_{kvp.Key}"] = kvp.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to extract data from {url}");
                        // Continue with other URLs
                    }
                }

                result.CombinedText = combinedText.ToString();
                result.StructuredData = structuredData;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error performing enhanced web research for {symbol}");
                // Fallback to basic search
                var searchResults = await _webSearchPlugin.SearchAsync($"{symbol} stock analysis trading strategy research");
                result.CombinedText = string.Join("\n", searchResults.Select(r => $"{r.Title}: {r.Snippet}"));
                result.StructuredData = new Dictionary<string, object>();
            }

            return result;
        }

        private async Task<string> GenerateStrategyParametersAsync(string symbol, ResearchData researchData)
        {
            // Use DeepSeek for mathematical analysis and parameter optimization
            var deepSeekPrompt = $@"As a quantitative analyst, analyze the following data for {symbol} and generate mathematically rigorous trading strategy parameters.

Market Data: {JsonSerializer.Serialize(researchData.MarketData)}
Technical Analysis: {JsonSerializer.Serialize(researchData.TechnicalAnalysis)}
Volatility: {researchData.VolatilityData}
Key Levels: Support={researchData.KeyLevels?.Support ?? 0}, Resistance={researchData.KeyLevels?.Resistance ?? 0}

Please calculate and provide:
1. Optimal position sizing using Kelly Criterion or similar risk management formula
2. Stop loss levels based on volatility-adjusted ATR calculations
3. Take profit targets using risk-reward ratios (minimum 1:2)
4. Entry timing parameters based on technical indicators
5. Maximum drawdown limits based on historical volatility

Format your response as a JSON object with these calculated parameters.";

            try
            {
                var deepSeekResponse = await _deepSeekService.GetChatCompletionAsync(deepSeekPrompt);
                var mathematicalParams = JsonSerializer.Deserialize<Dictionary<string, object>>(deepSeekResponse);

                // Use the mathematical parameters to enhance the strategy generation
                var enhancedPrompt = $@"Generate C# strategy parameters for {symbol} using these mathematically calculated values:

Mathematical Analysis: {JsonSerializer.Serialize(mathematicalParams)}
Research Data: {JsonSerializer.Serialize(researchData)}

Generate precise C# field declarations with calculated values and mathematical justifications in comments.";

                var result = await _kernel.InvokeAsync("TradingTemplateGeneratorPlugin", "GenerateStrategyParameters", new() {
                    ["symbol"] = symbol,
                    ["researchData"] = JsonSerializer.Serialize(researchData),
                    ["mathematicalParams"] = JsonSerializer.Serialize(mathematicalParams)
                });

                return result.GetValue<string>() ?? GenerateDefaultParameters(symbol, researchData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"DeepSeek analysis failed for {symbol}, falling back to standard generation");
                // Fallback to original method
                var prompt = $@"Generate strategy parameters for {symbol} based on the following research data:

Market Data: {JsonSerializer.Serialize(researchData.MarketData)}
Company Info: {researchData.CompanyInfoString}
Technical Analysis: {JsonSerializer.Serialize(researchData.TechnicalAnalysis)}
Volatility: {researchData.VolatilityData}

Generate appropriate support/resistance levels, periods, risk percentages, and thresholds.
Format as C# private field declarations with comments.";

                var result = await _kernel.InvokeAsync("TradingTemplateGeneratorPlugin", "GenerateStrategyParameters", new() {
                    ["symbol"] = symbol,
                    ["researchData"] = JsonSerializer.Serialize(researchData)
                });

                return result.GetValue<string>() ?? GenerateDefaultParameters(symbol, researchData);
            }
        }

        private async Task<string> GenerateEntryConditionsAsync(string symbol, string strategyType, ResearchData researchData)
        {
            // Use DeepSeek to generate pattern-based entry conditions
            var patternBasedPrompt = $@"Generate sophisticated entry conditions for {symbol} based on detected patterns and market regime.

Market Regime: {researchData.MarketRegime}
Detected Patterns: {string.Join(", ", researchData.DetectedPatterns)}

Research Data:
- Current Price: {researchData.MarketData?.Price ?? 0}
- Support: {researchData.KeyLevels?.Support ?? 0}
- Resistance: {researchData.KeyLevels?.Resistance ?? 0}
- Volatility: {researchData.VolatilityData}

Create 3-5 specific entry conditions that incorporate:
1. Pattern recognition (e.g., breakout from triangle, cup & handle completion)
2. Multi-timeframe confirmation
3. Volume analysis
4. Momentum indicators
5. Risk management filters

Adapt the strategy to the current market regime:
- Strong Uptrend: Momentum-based entries with trend continuation patterns
- Sideways/Ranging: Mean reversion and range breakout strategies
- High Volatility: Volatility contraction patterns and breakouts
- Mean Reversion: Overbought/oversold conditions with reversal patterns

Format as detailed, actionable entry conditions with specific price levels and indicators.";

            try
            {
                var patternBasedConditions = await _deepSeekService.GetChatCompletionAsync(patternBasedPrompt);
                return patternBasedConditions;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"DeepSeek pattern analysis failed for {symbol}, falling back to standard generation");

                // Enhanced fallback with pattern awareness
                var enhancedPrompt = $@"Generate entry conditions for {strategyType} trading strategy for {symbol} incorporating pattern recognition.

Market Regime: {researchData.MarketRegime}
Detected Patterns: {string.Join(", ", researchData.DetectedPatterns)}

Research Data Summary:
- Current Price: {researchData.MarketData?.Price ?? 0}
- 52W High: {researchData.KeyLevels?.Resistance ?? 0}
- 52W Low: {researchData.KeyLevels?.Support ?? 0}
- Average Volume: {researchData.MarketData?.Volume ?? 0}
- Volatility: {researchData.VolatilityData}

Generate 2-3 specific entry conditions with clear technical criteria that incorporate detected patterns.
Format as numbered list with detailed conditions.";

                var result = await _kernel.InvokeAsync("TradingTemplateGeneratorPlugin", "GenerateEntryConditions", new() {
                    ["symbol"] = symbol,
                    ["strategyType"] = strategyType,
                    ["researchData"] = JsonSerializer.Serialize(researchData)
                });

                return result.GetValue<string>() ?? GenerateDefaultEntryConditions(strategyType, researchData);
            }
        }

        private async Task<string> GenerateExitFrameworkAsync(string symbol, ResearchData researchData)
        {
            // Use DeepSeek for pattern-based exit framework
            var patternBasedExitPrompt = $@"Design a comprehensive exit framework for {symbol} based on detected patterns and market regime.

Market Regime: {researchData.MarketRegime}
Detected Patterns: {string.Join(", ", researchData.DetectedPatterns)}

Key Levels:
- Support: {researchData.KeyLevels?.Support ?? 0}
- Resistance: {researchData.KeyLevels?.Resistance ?? 0}
- Current Price: {researchData.MarketData?.Price ?? 0}
- Volatility: {researchData.VolatilityData}

Create pattern-aware exit rules:

1. **Profit Targets** based on pattern completion:
   - Triangle breakouts: Target measured move to opposite side
   - Head & Shoulders: Target distance from head to neckline
   - Cup & Handle: Target equal to cup depth
   - Flag patterns: Target flagpole height

2. **Stop Loss Levels** using pattern failure points:
   - Pattern invalidation levels
   - Recent swing lows/highs
   - Volatility-adjusted stops

3. **Trailing Stops** for trend-following patterns:
   - Percentage-based trailing for strong trends
   - ATR-based trailing for volatile markets
   - Pattern-based trailing (e.g., below rising trendline)

4. **Time-based Exits** for pattern timeframes:
   - Maximum hold time based on pattern duration
   - Weekly/monthly cycle exits

5. **Scale-out Rules** for partial profit taking:
   - Fibonacci-based scale-out levels
   - Volume-based scale-out signals
   - Pattern extension targets

Adapt to market regime:
- Strong trends: Let profits run with trailing stops
- Sideways markets: Quick profit taking and tight stops
- High volatility: Wider stops and scale-out approach

Format as structured exit framework with specific levels and conditions.";

            try
            {
                var patternBasedExits = await _deepSeekService.GetChatCompletionAsync(patternBasedExitPrompt);
                return patternBasedExits;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"DeepSeek exit framework failed for {symbol}, falling back to standard generation");

                // Enhanced fallback with pattern awareness
                var enhancedPrompt = $@"Generate exit framework for {symbol} trading strategy incorporating pattern recognition.

Market Regime: {researchData.MarketRegime}
Detected Patterns: {string.Join(", ", researchData.DetectedPatterns)}

Key Levels:
- Support: {researchData.KeyLevels?.Support ?? 0}
- Resistance: {researchData.KeyLevels?.Resistance ?? 0}
- Volatility: {researchData.VolatilityData}

Include profit targets, trailing stops, and time-based exits adapted to detected patterns.
Format with clear sections for profit targets and stop management.";

                var result = await _kernel.InvokeAsync("TradingTemplateGeneratorPlugin", "GenerateExitFramework", new() {
                    ["symbol"] = symbol,
                    ["researchData"] = JsonSerializer.Serialize(researchData)
                });

                return result.GetValue<string>() ?? GenerateDefaultExitFramework(researchData);
            }
        }

        private async Task<string> GenerateRiskManagementAsync(string symbol, ResearchData researchData)
        {
            // Use DeepSeek for mathematical risk calculations
            var deepSeekPrompt = $@"As a quantitative risk manager, calculate optimal risk management parameters for {symbol}.

Market Data: {JsonSerializer.Serialize(researchData.MarketData)}
Technical Analysis: {JsonSerializer.Serialize(researchData.TechnicalAnalysis)}
Volatility: {researchData.VolatilityData}
Key Levels: Support={researchData.KeyLevels?.Support ?? 0}, Resistance={researchData.KeyLevels?.Resistance ?? 0}

Calculate using mathematical formulas:
1. Value at Risk (VaR) using historical simulation method
2. Expected Shortfall (CVaR) for tail risk assessment
3. Optimal position size using Modern Portfolio Theory
4. Dynamic stop loss using volatility-adjusted ATR bands
5. Kelly Criterion for position sizing: K = (bp - q) / b
   Where b = odds, p = probability of win, q = probability of loss
6. Maximum drawdown limits based on historical volatility
7. Risk-adjusted return metrics (Sharpe ratio, Sortino ratio)

Provide calculations and recommended parameters in JSON format.";

            try
            {
                var deepSeekResponse = await _deepSeekService.GetChatCompletionAsync(deepSeekPrompt);
                var riskCalculations = JsonSerializer.Deserialize<Dictionary<string, object>>(deepSeekResponse);

                // Use mathematical calculations to enhance risk management rules
                var enhancedPrompt = $@"Generate comprehensive risk management rules for {symbol} using these mathematical calculations:

Mathematical Risk Analysis: {JsonSerializer.Serialize(riskCalculations)}
Research Data: {JsonSerializer.Serialize(researchData)}

Create specific, mathematically justified risk rules including:
1. Position sizing based on Kelly Criterion and VaR calculations
2. Stop loss levels using volatility-adjusted formulas
3. Take profit targets with risk-reward optimization
4. Maximum drawdown limits based on historical analysis
5. Portfolio-level risk controls
6. Circuit breakers for adverse market conditions

Format as structured rules with calculated percentages and mathematical justifications.";

                var response = await _llmService.GetChatCompletionAsync(enhancedPrompt);
                return response;
            }
            catch (Exception deepSeekEx)
            {
                _logger.LogWarning(deepSeekEx, $"DeepSeek risk analysis failed for {symbol}, falling back to standard generation");
                // Fallback to original method
                var prompt = $@"Generate comprehensive risk management rules for {symbol} trading strategy.

Portfolio Context:
- Current volatility: {researchData.VolatilityData:P2}
- Company Info: {researchData.CompanyInfoString}
- Technical Analysis: {JsonSerializer.Serialize(researchData.TechnicalAnalysis)}

Include:
1. Position sizing rules (percentage of portfolio)
2. Stop loss levels (initial and trailing)
3. Take profit targets (multiple levels)
4. Maximum drawdown limits
5. Daily loss limits
6. Maximum open positions
7. Circuit breakers for adverse conditions
8. Hedging strategies if applicable

Format as structured rules with specific percentages and conditions.";

                try
                {
                    var response = await _llmService.GetChatCompletionAsync(prompt);
                    return response;
                }
                catch (Exception llmEx)
                {
                    _logger.LogWarning(llmEx, $"Error generating risk management via LLM for {symbol}, using defaults");
                    return GenerateDefaultRiskManagement(researchData);
                }
            }
        }

        private async Task<string> GenerateTechnicalIndicatorsAsync(string symbol, ResearchData researchData)
        {
            // Use DeepSeek for pattern-specific indicator generation
            var patternIndicatorPrompt = $@"Create sophisticated technical indicators specifically designed for {symbol} based on detected patterns and market regime.

Market Regime: {researchData.MarketRegime}
Detected Patterns: {string.Join(", ", researchData.DetectedPatterns)}

Technical Analysis Data: {JsonSerializer.Serialize(researchData.TechnicalAnalysis)}
Volatility: {researchData.VolatilityData}

Design 3-5 custom indicators that complement the detected patterns:

**Pattern-Specific Indicators:**
- For Triangle patterns: Triangle breakout probability indicator
- For Head & Shoulders: Neckline strength and volume confirmation
- For Channels: Channel width and slope indicators
- For Support/Resistance: Dynamic level strength indicators

**Regime-Adaptive Indicators:**
- Strong Trend: Trend strength and continuation indicators
- Sideways: Mean reversion and range indicators
- High Volatility: Volatility-adjusted momentum indicators
- Low Volatility: Breakout anticipation indicators

**Composite Indicators:**
- Pattern completion probability
- Multi-timeframe momentum divergence
- Volume-price confirmation indicators
- Risk-adjusted momentum oscillators

For each indicator, provide:
1. Mathematical formula with code-like syntax
2. Pattern-specific interpretation
3. Entry/exit signal generation
4. Risk management integration
5. Timeframe optimization

Format as detailed indicator specifications with QuantConnect/LEAN compatible code examples.";

            try
            {
                var patternIndicators = await _deepSeekService.GetChatCompletionAsync(patternIndicatorPrompt);
                return patternIndicators;
            }
            catch (Exception deepSeekIndicatorEx)
            {
                _logger.LogWarning(deepSeekIndicatorEx, $"DeepSeek indicator generation failed for {symbol}, falling back to standard generation");

                // Enhanced fallback with pattern awareness
                var enhancedPrompt = $@"Generate custom technical indicators for {symbol} trading strategy incorporating pattern recognition.

Market Regime: {researchData.MarketRegime}
Detected Patterns: {string.Join(", ", researchData.DetectedPatterns)}
Technical Analysis Data: {JsonSerializer.Serialize(researchData.TechnicalAnalysis)}

Create 2-3 custom composite indicators or unique indicator combinations that complement the detected patterns.
For each indicator, include:
1. Formula/calculation method
2. Interpretation guidelines
3. Buy/sell signals
4. Optimal timeframes
5. Risk considerations

Format as structured indicator definitions with code-like syntax where appropriate.";

                try
                {
                    var response = await _llmService.GetChatCompletionAsync(enhancedPrompt);
                    return response;
                }
                catch (Exception llmIndicatorEx)
                {
                    _logger.LogWarning(llmIndicatorEx, $"Error generating technical indicators via LLM for {symbol}, using defaults");
                    return GenerateDefaultTechnicalIndicators();
                }
            }
        }

        private string GenerateDataRequirements(string symbol)
        {
            return $@"// Data Requirements
1. Primary Data:
   - {symbol} Minute Bars (OHLCV)
   - Corporate Action Handling enabled

2. Secondary Data:
   - SPY 5-minute bars (correlation check)
   - VIX Daily Close
   - US10Y Yield (macro filter)
   - {symbol} Options Chain (for earnings strategies)";
        }

        private string GenerateBacktestConfiguration(string symbol)
        {
            return $@"// Backtest Configuration
- Benchmark: SPY
- Commission: Interactive Brokers Tiered
- Slippage: VolumeShareModel(0.1, 5)
- Warmup Period: 200 trading days
- Optimization Params:
  Genetic Settings:
  Population Size: 100
  Generations: 20
  Mutation Probability: 0.1";
        }

        private async Task<string> GenerateLimitationsAsync(string symbol, ResearchData researchData)
        {
            var limitations = new List<string>();

            if (researchData.CompanyInfoString.Contains("Technology") || researchData.CompanyInfoString.Contains("Semiconductor"))
                limitations.Add("Technology sector volatility may cause unexpected price swings");

            if (researchData.VolatilityData > 0.03m)
                limitations.Add("High volatility may lead to increased slippage and stop hunting");

            limitations.Add("Assumes continuous options liquidity");
            limitations.Add("Doesn't account for sudden geopolitical events");
            limitations.Add("Earnings date accuracy depends on data feed reliability");

            return string.Join("\n", limitations.Select(l => $"{limitations.IndexOf(l) + 1}. {l}"));
        }

        private string GenerateImplementationNotes(string symbol)
        {
            return $@"// Implementation Notes
- Requires custom Universe Selection for earnings dates
- Needs fundamental data integration for contract news
- Suggested walk-forward period: 90 days
- Critical to implement IV percentile calculations
- Consider implementing adaptive position sizing based on VIX levels";
        }

        private async Task SaveTemplateToFileAsync(TradingTemplate template)
        {
            var fileName = $"{template.Symbol}_Trading_Strategy_Template_{DateTime.UtcNow:yyyy-MM-dd}.txt";
            var filePath = Path.Combine("Extracted_Strategies", fileName);

            // Ensure directory exists
            Directory.CreateDirectory("Extracted_Strategies");

            var content = FormatTemplateAsText(template);
            await File.WriteAllTextAsync(filePath, content);

            _logger.LogInformation($"Trading template saved to: {filePath}");
        }

        private string FormatTemplateAsText(TradingTemplate template)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"{template.Symbol.ToUpper()} Algorithmic Trading Strategy (QuantConnect/LEAN C# Template)");
            sb.AppendLine(new string('-', 80));
            sb.AppendLine($"Last Updated: {template.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();

            sb.AppendLine("// Strategy Parameters");
            sb.AppendLine(template.StrategyParameters);
            sb.AppendLine();

            sb.AppendLine("// Entry Conditions");
            sb.AppendLine(template.EntryConditions);
            sb.AppendLine();

            sb.AppendLine("// Exit Framework");
            sb.AppendLine(template.ExitFramework);
            sb.AppendLine();

            sb.AppendLine("// Risk Management");
            sb.AppendLine(template.RiskManagement);
            sb.AppendLine();

            sb.AppendLine("// Technical Indicators");
            sb.AppendLine(template.TechnicalIndicators);
            sb.AppendLine();

            sb.AppendLine(template.DataRequirements);
            sb.AppendLine();

            sb.AppendLine(template.BacktestConfiguration);
            sb.AppendLine();

            sb.AppendLine("// Known Limitations");
            sb.AppendLine(template.KnownLimitations);
            sb.AppendLine();

            sb.AppendLine("// Implementation Notes");
            sb.AppendLine(template.ImplementationNotes);
            sb.AppendLine();

            sb.AppendLine("DISCLAIMER: This strategy contains hypothetical assumptions. Actual market conditions may vary significantly. Always test with out-of-sample data before live deployment. Options trading carries substantial risk of loss.");

            return sb.ToString();
        }

        // Helper methods for default generation
        private string GenerateDefaultParameters(string symbol, ResearchData researchData)
        {
            var support = researchData.KeyLevels?.Support ?? 100m;
            var resistance = researchData.KeyLevels?.Resistance ?? 200m;
            var volatility = researchData.VolatilityData;

            return $@"private decimal _supportLevel = {support:F2}m; // Key support level
private decimal _resistanceLevel = {resistance:F2}m; // Key resistance level
private int _rsiPeriod = 14;
private int _emaPeriod = 20;
private decimal _positionRiskPercent = 2.0m;
private decimal _volatilityThreshold = {volatility:F4}m; // Current ATR";
        }

        private string GenerateDefaultEntryConditions(string strategyType, ResearchData researchData)
        {
            return $@"1. Long Entry:
   - Close > _resistanceLevel
   - Volume > 1.5 * 20-day average volume
   - RSI(14) < 70
   - EMA20 trending upward

2. Mean Reversion Entry:
   - Price within 3% of _supportLevel
   - RSI(14) < 30
   - Volume confirmation required";
        }

        private string GenerateDefaultExitFramework(ResearchData researchData)
        {
            return $@"- Profit Targets:
  1. First Target: 5% gain (liquidate 50%)
  2. Second Target: 10% gain (liquidate remaining)

- Trailing Stop:
  Initial: 4% from entry
  Adjusted using ATR(14) for volatility";
        }

        private string GenerateDefaultRiskManagement(ResearchData researchData)
        {
            return $@"- Position Sizing:
  DollarAmount = (Portfolio.TotalPortfolioValue * _positionRiskPercent) / RiskAmount

- Circuit Breakers:
  1. Daily Drawdown > 2%: Reduce position sizes by 50%
  2. Weekly Drawdown > 6%: Full liquidation";
        }

        private string GenerateDefaultTechnicalIndicators()
        {
            return $@"- Custom Momentum Index:
  (0.4 * RSI(14)) + (0.3 * MACD Histogram) + (0.3 * Volume ROC(5))";
        }

        private async Task<List<string>> DetectChartPatternsAsync(string symbol)
        {
            var patterns = new List<string>();

            try
            {
                // Use DeepSeek for advanced pattern recognition
                var patternAnalysisPrompt = $@"Analyze the technical data for {symbol} and identify chart patterns and price action signals.

Based on typical technical analysis, identify which of these patterns are likely present:
- Head & Shoulders (bullish/bearish)
- Double Top/Bottom
- Triple Top/Bottom
- Ascending/Descending Triangle
- Symmetrical Triangle
- Wedge (rising/falling)
- Flag/Bullish Flag
- Pennant
- Cup & Handle
- Inverse Head & Shoulders
- Rectangle/Range
- Channel (ascending/descending)

Also identify:
- Support/Resistance levels
- Trend channels
- Fibonacci retracement levels
- Volume patterns
- Momentum divergences

Provide a list of detected patterns with confidence levels.";

                var patternAnalysis = await _deepSeekService.GetChatCompletionAsync(patternAnalysisPrompt);
                patterns.AddRange(ParsePatternsFromAnalysis(patternAnalysis));

                // Fallback patterns if DeepSeek fails
                if (patterns.Count == 0)
                {
                    patterns.AddRange(new[] {
                        "Support/Resistance Levels",
                        "Moving Average Crossovers",
                        "Volume Confirmation Patterns"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error detecting patterns for {symbol}, using defaults");
                patterns.AddRange(new[] {
                    "Support/Resistance Levels",
                    "Moving Average Crossovers",
                    "Volume Confirmation Patterns"
                });
            }

            return patterns;
        }

        private async Task<string> DetermineMarketRegimeAsync(string symbol)
        {
            try
            {
                // Use DeepSeek to determine market regime
                var regimeAnalysisPrompt = $@"Determine the current market regime for {symbol}.

Analyze whether the market is in:
- Strong Uptrend
- Weak Uptrend
- Sideways/Ranging
- Weak Downtrend
- Strong Downtrend
- High Volatility Breakout
- Low Volatility Consolidation
- Mean Reversion Mode

Consider:
- Trend strength (ADX)
- Volatility levels
- Volume patterns
- Recent price action
- Key level reactions

Return the most likely regime with reasoning.";

                var regimeAnalysis = await _deepSeekService.GetChatCompletionAsync(regimeAnalysisPrompt);
                return ParseRegimeFromAnalysis(regimeAnalysis);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error determining market regime for {symbol}, using default");
                return "sideways_ranging";
            }
        }

        private List<string> ParsePatternsFromAnalysis(string analysis)
        {
            var patterns = new List<string>();
            // Simple parsing - in production, use more sophisticated NLP
            var commonPatterns = new[] {
                "head and shoulders", "double top", "double bottom", "triangle", "wedge",
                "flag", "pennant", "cup and handle", "rectangle", "channel",
                "support resistance", "fibonacci", "divergence", "breakout"
            };

            foreach (var pattern in commonPatterns)
            {
                if (analysis.ToLower().Contains(pattern.ToLower()))
                {
                    patterns.Add(pattern);
                }
            }

            return patterns.Count > 0 ? patterns : new List<string> { "technical_levels", "momentum_signals" };
        }

        private string ParseRegimeFromAnalysis(string analysis)
        {
            var analysisLower = analysis.ToLower();
            if (analysisLower.Contains("strong uptrend")) return "strong_uptrend";
            if (analysisLower.Contains("weak uptrend")) return "weak_uptrend";
            if (analysisLower.Contains("strong downtrend")) return "strong_downtrend";
            if (analysisLower.Contains("weak downtrend")) return "weak_downtrend";
            if (analysisLower.Contains("high volatility")) return "high_volatility";
            if (analysisLower.Contains("low volatility")) return "low_volatility";
            if (analysisLower.Contains("mean reversion")) return "mean_reversion";
            return "sideways_ranging";
        }

        private async Task<decimal> CalculateVolatilityMetricsAsync(string symbol)
        {
            // Calculate ATR or similar volatility measure
            try
            {
                var technicalAnalysis = await _technicalAnalysisService.PerformFullAnalysisAsync(symbol, 30);
                if (technicalAnalysis?.Indicators.ContainsKey("ATR") == true)
                {
                    return Convert.ToDecimal(technicalAnalysis.Indicators["ATR"]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error calculating volatility for {symbol}");
            }
            return 0.02m; // Default 2% volatility
        }

        private async Task<KeyLevels> IdentifyKeyLevelsAsync(string symbol)
        {
            try
            {
                var technicalAnalysis = await _technicalAnalysisService.PerformFullAnalysisAsync(symbol, 252);
                if (technicalAnalysis?.Indicators.ContainsKey("SMA_20") == true && technicalAnalysis?.Indicators.ContainsKey("SMA_50") == true)
                {
                    var sma20 = Convert.ToDecimal(technicalAnalysis.Indicators["SMA_20"]);
                    var sma50 = Convert.ToDecimal(technicalAnalysis.Indicators["SMA_50"]);

                    return new KeyLevels
                    {
                        Resistance = Math.Max(sma20, sma50) * 1.1m, // 10% above higher MA
                        Support = Math.Min(sma20, sma50) * 0.9m    // 10% below lower MA
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error identifying key levels for {symbol}");
            }

            return new KeyLevels { Resistance = 100m, Support = 50m };
        }
    }

    public class TradingTemplate
    {
        public string Symbol { get; set; } = string.Empty;
        public string StrategyType { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public string StrategyParameters { get; set; } = string.Empty;
        public string EntryConditions { get; set; } = string.Empty;
        public string ExitFramework { get; set; } = string.Empty;
        public string RiskManagement { get; set; } = string.Empty;
        public string TechnicalIndicators { get; set; } = string.Empty;
        public string DataRequirements { get; set; } = string.Empty;
        public string BacktestConfiguration { get; set; } = string.Empty;
        public string KnownLimitations { get; set; } = string.Empty;
        public string ImplementationNotes { get; set; } = string.Empty;
    }

    public class ResearchData
    {
        public MarketData? MarketData { get; set; }
        public string CompanyInfoString { get; set; } = string.Empty;
        public TechnicalAnalysisResult? TechnicalAnalysis { get; set; }
        public SymbolSentimentAnalysis? SentimentAnalysis { get; set; }
        public string WebResearch { get; set; } = string.Empty;
        public decimal VolatilityData { get; set; }
        public KeyLevels? KeyLevels { get; set; }
        public Dictionary<string, object> StructuredWebData { get; set; } = new();
        public List<string> DetectedPatterns { get; set; } = new();
        public string MarketRegime { get; set; } = "unknown";
    }

    public class KeyLevels
    {
        public decimal Support { get; set; }
        public decimal Resistance { get; set; }
    }

    public class WebResearchResult
    {
        public string CombinedText { get; set; } = string.Empty;
        public Dictionary<string, object> StructuredData { get; set; } = new();
    }
}