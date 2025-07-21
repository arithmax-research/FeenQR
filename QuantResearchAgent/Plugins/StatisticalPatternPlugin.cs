using Microsoft.SemanticKernel;
using QuantResearchAgent.Services.ResearchAgents;
using System.ComponentModel;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Statistical Pattern Plugin - Exposes pattern detection capabilities to Semantic Kernel
/// </summary>
public class StatisticalPatternPlugin
{
    private readonly StatisticalPatternAgentService _patternService;

    public StatisticalPatternPlugin(StatisticalPatternAgentService patternService)
    {
        _patternService = patternService;
    }

    [KernelFunction]
    [Description("Detect statistical patterns in market data for a specific symbol")]
    public async Task<string> DetectPatternsAsync(
        [Description("Stock/crypto symbol to analyze (e.g., 'BTCUSD', 'AAPL')")] string symbol,
        [Description("Analysis window in hours (e.g., 24, 168 for 1 week, 720 for 1 month)")] int hours = 168)
    {
        try
        {
            var window = TimeSpan.FromHours(hours);
            var report = await _patternService.DetectStatisticalPatternsAsync(symbol, window);
            
            var result = $"## Statistical Pattern Analysis\n\n";
            result += $"**Symbol:** {symbol.ToUpper()}\n";
            result += $"**Analysis Window:** {FormatTimeSpan(window)}\n";
            result += $"**Analysis Date:** {report.AnalysisDate:yyyy-MM-dd HH:mm} UTC\n\n";
            
            // Summary
            result += $"### Pattern Detection Summary\n";
            result += $"**Total Patterns Found:** {report.DetectedPatterns.Count}\n";
            result += $"**High Confidence Patterns:** {report.DetectedPatterns.Count(p => p.Confidence > 0.7)}\n";
            result += $"**Exploitable Patterns:** {report.DetectedPatterns.Count(p => p.Exploitability > 0.6)}\n\n";
            
            // Top patterns
            var topPatterns = report.DetectedPatterns.Take(5);
            if (topPatterns.Any())
            {
                result += "### Top Detected Patterns\n";
                foreach (var pattern in topPatterns)
                {
                    var emoji = GetPatternEmoji(pattern.Type);
                    result += $"{emoji} **{pattern.Name}**\n";
                    result += $"   Type: {pattern.Type}\n";
                    result += $"   Confidence: {pattern.Confidence:P0} | Exploitability: {pattern.Exploitability:P0} | Strength: {pattern.Strength:F2}\n";
                    result += $"   Description: {pattern.Description}\n\n";
                }
            }
            
            // Risk metrics
            result += "### Risk Analysis\n";
            result += $"**Volatility:** {report.RiskMetrics.Volatility:P2}\n";
            result += $"**Max Drawdown:** {report.RiskMetrics.MaxDrawdown:P2}\n";
            result += $"**Sharpe Ratio:** {report.RiskMetrics.SharpeRatio:F2}\n";
            result += $"**95% VaR:** {report.RiskMetrics.VaR95:P2}\n";
            result += $"**Pattern Risk Score:** {report.RiskMetrics.PatternRisk:F2}\n\n";
            
            // Trading strategies
            if (report.TradingStrategies.Any())
            {
                result += "### Suggested Trading Strategies\n";
                foreach (var strategy in report.TradingStrategies.Take(3))
                {
                    result += $"• {strategy}\n";
                }
                result += "\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error detecting patterns for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Detect specific type of patterns (mean reversion, momentum, etc.)")]
    public async Task<string> DetectSpecificPatternAsync(
        [Description("Symbol to analyze")] string symbol,
        [Description("Pattern type: MeanReversion, Momentum, Seasonality, Volatility, Correlation, Anomaly, Fractal, Arbitrage, Regime, Microstructure")] string patternType,
        [Description("Analysis window in hours")] int hours = 168)
    {
        try
        {
            // Parse pattern type
            if (!Enum.TryParse<PatternType>(patternType, true, out var parsedPatternType))
            {
                return $"Invalid pattern type: {patternType}. Valid types: {string.Join(", ", Enum.GetNames<PatternType>())}";
            }

            var window = TimeSpan.FromHours(hours);
            var report = await _patternService.DetectStatisticalPatternsAsync(symbol, window, new[] { parsedPatternType });
            
            var result = $"## {patternType} Pattern Analysis\n\n";
            result += $"**Symbol:** {symbol.ToUpper()}\n";
            result += $"**Pattern Type:** {patternType}\n";
            result += $"**Window:** {FormatTimeSpan(window)}\n\n";
            
            var targetPatterns = report.DetectedPatterns.Where(p => p.Type == parsedPatternType).ToList();
            
            if (!targetPatterns.Any())
            {
                result += $"❌ No {patternType} patterns detected in the specified timeframe.\n";
                return result;
            }

            foreach (var pattern in targetPatterns)
            {
                result += $"### {pattern.Name}\n";
                result += $"**Confidence:** {pattern.Confidence:P0}\n";
                result += $"**Exploitability:** {pattern.Exploitability:P0}\n";
                result += $"**Strength:** {pattern.Strength:F2}\n\n";
                
                result += $"**Description:** {pattern.Description}\n\n";
                
                if (pattern.Parameters.Any())
                {
                    result += "**Key Parameters:**\n";
                    foreach (var param in pattern.Parameters.Take(5))
                    {
                        result += $"• {param.Key}: {FormatParameterValue(param.Value)}\n";
                    }
                    result += "\n";
                }
                
                if (!string.IsNullOrEmpty(pattern.Analysis))
                {
                    result += $"**Detailed Analysis:**\n{pattern.Analysis}\n\n";
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error detecting {patternType} patterns: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Generate implementation roadmap for detected patterns")]
    public async Task<string> GenerateImplementationRoadmapAsync(
        [Description("Symbol that was analyzed")] string symbol,
        [Description("Analysis window in hours")] int hours = 168)
    {
        try
        {
            var window = TimeSpan.FromHours(hours);
            var report = await _patternService.DetectStatisticalPatternsAsync(symbol, window);
            
            var result = $"## Implementation Roadmap for {symbol.ToUpper()}\n\n";
            result += $"**Analysis Date:** {report.AnalysisDate:yyyy-MM-dd HH:mm} UTC\n";
            result += $"**Patterns Analyzed:** {report.DetectedPatterns.Count}\n\n";
            
            if (!string.IsNullOrEmpty(report.ImplementationRoadmap))
            {
                result += report.ImplementationRoadmap;
            }
            else
            {
                result += "No implementation roadmap generated. This may occur when no significant patterns are detected.";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error generating implementation roadmap: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Compare pattern detection across multiple symbols")]
    public async Task<string> ComparePatternAcrossSymbolsAsync(
        [Description("Comma-separated list of symbols to compare")] string symbols,
        [Description("Analysis window in hours")] int hours = 168)
    {
        try
        {
            var symbolList = symbols.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(s => s.Trim().ToUpper())
                                   .ToList();

            var result = $"## Pattern Comparison Analysis\n\n";
            result += $"**Symbols:** {string.Join(", ", symbolList)}\n";
            result += $"**Window:** {FormatTimeSpan(TimeSpan.FromHours(hours))}\n";
            result += $"**Analysis Date:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n";

            var symbolResults = new List<(string Symbol, int PatternCount, double AvgConfidence, double AvgExploitability)>();

            foreach (var symbol in symbolList)
            {
                try
                {
                    var window = TimeSpan.FromHours(hours);
                    var report = await _patternService.DetectStatisticalPatternsAsync(symbol, window);
                    
                    var avgConfidence = report.DetectedPatterns.Any() ? report.DetectedPatterns.Average(p => p.Confidence) : 0;
                    var avgExploitability = report.DetectedPatterns.Any() ? report.DetectedPatterns.Average(p => p.Exploitability) : 0;
                    
                    symbolResults.Add((symbol, report.DetectedPatterns.Count, avgConfidence, avgExploitability));
                }
                catch (Exception ex)
                {
                    result += $"⚠️ Error analyzing {symbol}: {ex.Message}\n";
                }
            }

            if (symbolResults.Any())
            {
                // Sort by pattern count and exploitability
                var rankedSymbols = symbolResults.OrderByDescending(s => s.PatternCount)
                                                 .ThenByDescending(s => s.AvgExploitability)
                                                 .ToList();

                result += "### Pattern Detection Rankings\n";
                for (int i = 0; i < rankedSymbols.Count; i++)
                {
                    var symbolData = rankedSymbols[i];
                    var emoji = symbolData.PatternCount > 3 ? "🟢" : symbolData.PatternCount > 1 ? "🟡" : "🔴";
                    
                    result += $"{i + 1}. {emoji} **{symbolData.Symbol}**\n";
                    result += $"   Patterns Found: {symbolData.PatternCount}\n";
                    result += $"   Avg Confidence: {symbolData.AvgConfidence:P0}\n";
                    result += $"   Avg Exploitability: {symbolData.AvgExploitability:P0}\n\n";
                }

                result += "### Key Insights\n";
                var bestSymbol = rankedSymbols.First();
                var worstSymbol = rankedSymbols.Last();
                
                result += $"• **Most Pattern-Rich:** {bestSymbol.Symbol} ({bestSymbol.PatternCount} patterns)\n";
                result += $"• **Least Pattern-Rich:** {worstSymbol.Symbol} ({worstSymbol.PatternCount} patterns)\n";
                result += $"• **Average Patterns per Symbol:** {symbolResults.Average(s => s.PatternCount):F1}\n";
                result += $"• **Best Exploitability:** {symbolResults.OrderByDescending(s => s.AvgExploitability).First().Symbol} ({symbolResults.Max(s => s.AvgExploitability):P0})\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error comparing patterns across symbols: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Get pattern detection trends and history")]
    public async Task<string> GetPatternTrendsAsync(
        [Description("Symbol to analyze trends for")] string symbol,
        [Description("Number of days to look back")] int days = 30)
    {
        try
        {
            var trendAnalysis = await _patternService.GeneratePatternTrendAnalysisAsync(symbol, days);
            
            var result = $"## Pattern Detection Trends\n\n";
            result += $"**Symbol:** {symbol.ToUpper()}\n";
            result += $"**Time Period:** Last {days} days\n\n";
            result += $"### Trend Analysis\n{trendAnalysis}\n\n";

            return result;
        }
        catch (Exception ex)
        {
            return $"Error getting pattern trends: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Detect anomalies and outliers in market data")]
    public async Task<string> DetectAnomaliesAsync(
        [Description("Symbol to analyze for anomalies")] string symbol,
        [Description("Analysis window in hours")] int hours = 168)
    {
        try
        {
            var window = TimeSpan.FromHours(hours);
            var report = await _patternService.DetectStatisticalPatternsAsync(symbol, window, new[] { PatternType.Anomaly });
            
            var result = $"## Anomaly Detection Report\n\n";
            result += $"**Symbol:** {symbol.ToUpper()}\n";
            result += $"**Window:** {FormatTimeSpan(window)}\n\n";
            
            var anomalyPatterns = report.DetectedPatterns.Where(p => p.Type == PatternType.Anomaly).ToList();
            
            if (!anomalyPatterns.Any())
            {
                result += "✅ No significant anomalies detected in the specified timeframe.\n";
                return result;
            }

            foreach (var pattern in anomalyPatterns)
            {
                result += $"### {pattern.Name}\n";
                result += $"**Anomaly Strength:** {pattern.Strength:F3}\n";
                result += $"**Detection Confidence:** {pattern.Confidence:P0}\n\n";
                
                if (pattern.Parameters.ContainsKey("OutlierCount"))
                {
                    result += $"**Outliers Detected:** {pattern.Parameters["OutlierCount"]}\n";
                }
                
                if (pattern.Parameters.ContainsKey("AnomalyScore"))
                {
                    var score = (double)pattern.Parameters["AnomalyScore"];
                    result += $"**Anomaly Score:** {score:P2} of observations\n";
                }
                
                result += $"\n**Analysis:** {pattern.Analysis}\n\n";
                
                // Risk assessment for anomalies
                result += "### Risk Assessment\n";
                if (pattern.Strength > 0.1)
                {
                    result += "⚠️ **High anomaly activity** - Consider increased volatility and unpredictable price movements\n";
                }
                else if (pattern.Strength > 0.05)
                {
                    result += "🟡 **Moderate anomaly activity** - Monitor for unusual market conditions\n";
                }
                else
                {
                    result += "🟢 **Low anomaly activity** - Normal market behavior observed\n";
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error detecting anomalies: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Analyze volatility patterns and clustering")]
    public async Task<string> AnalyzeVolatilityPatternsAsync(
        [Description("Symbol to analyze volatility patterns")] string symbol,
        [Description("Analysis window in hours")] int hours = 168)
    {
        try
        {
            var window = TimeSpan.FromHours(hours);
            var report = await _patternService.DetectStatisticalPatternsAsync(symbol, window, new[] { PatternType.Volatility });
            
            var result = $"## Volatility Pattern Analysis\n\n";
            result += $"**Symbol:** {symbol.ToUpper()}\n";
            result += $"**Window:** {FormatTimeSpan(window)}\n\n";
            
            var volPatterns = report.DetectedPatterns.Where(p => p.Type == PatternType.Volatility).ToList();
            
            if (!volPatterns.Any())
            {
                result += "No significant volatility patterns detected.\n";
                return result;
            }

            foreach (var pattern in volPatterns)
            {
                result += $"### {pattern.Name}\n";
                result += $"**Pattern Strength:** {pattern.Strength:F3}\n";
                result += $"**Confidence:** {pattern.Confidence:P0}\n\n";
                
                // Extract volatility metrics
                if (pattern.Parameters.ContainsKey("Volatility"))
                {
                    var vol = (double)pattern.Parameters["Volatility"];
                    result += $"**Current Volatility:** {vol:P2}\n";
                }
                
                if (pattern.Parameters.ContainsKey("GARCHAlpha") && pattern.Parameters.ContainsKey("GARCHBeta"))
                {
                    var alpha = (double)pattern.Parameters["GARCHAlpha"];
                    var beta = (double)pattern.Parameters["GARCHBeta"];
                    var persistence = alpha + beta;
                    
                    result += $"**GARCH Parameters:** α={alpha:F3}, β={beta:F3}\n";
                    result += $"**Volatility Persistence:** {persistence:F3} ";
                    result += persistence > 0.95 ? "(High persistence)" : persistence > 0.8 ? "(Moderate persistence)" : "(Low persistence)";
                    result += "\n";
                }
                
                if (pattern.Parameters.ContainsKey("VolClustering"))
                {
                    var clustering = (double)pattern.Parameters["VolClustering"];
                    result += $"**Volatility Clustering:** {clustering:F3}\n";
                }
                
                result += $"\n**Detailed Analysis:**\n{pattern.Analysis}\n\n";
                
                // Trading implications
                result += "### Trading Implications\n";
                if (pattern.Exploitability > 0.6)
                {
                    result += "🟢 **High exploitability** - Strong volatility trading opportunities\n";
                    result += "• Consider volatility breakout strategies\n";
                    result += "• Monitor for volatility regime changes\n";
                }
                else if (pattern.Exploitability > 0.3)
                {
                    result += "🟡 **Moderate exploitability** - Some volatility trading potential\n";
                    result += "• Use caution with volatility-based strategies\n";
                }
                else
                {
                    result += "🔴 **Low exploitability** - Limited volatility trading opportunities\n";
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error analyzing volatility patterns: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Get comprehensive pattern analysis summary")]
    public async Task<string> GetPatternAnalysisSummaryAsync(
        [Description("Symbol to analyze")] string symbol,
        [Description("Analysis window in hours")] int hours = 168)
    {
        try
        {
            var window = TimeSpan.FromHours(hours);
            var report = await _patternService.DetectStatisticalPatternsAsync(symbol, window);
            
            var result = $"## Comprehensive Pattern Analysis Summary\n\n";
            result += $"**Symbol:** {symbol.ToUpper()}\n";
            result += $"**Analysis Period:** {FormatTimeSpan(window)}\n";
            result += $"**Report Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n";
            
            // Executive summary
            result += "### Executive Summary\n";
            result += $"• **Total Patterns Detected:** {report.DetectedPatterns.Count}\n";
            result += $"• **High-Confidence Patterns:** {report.DetectedPatterns.Count(p => p.Confidence > 0.7)}\n";
            result += $"• **Tradeable Patterns:** {report.DetectedPatterns.Count(p => p.Exploitability > 0.6)}\n";
            result += $"• **Overall Risk Level:** {CalculateRiskLevel(report.RiskMetrics)}\n\n";
            
            // Pattern type breakdown
            result += "### Pattern Type Breakdown\n";
            var patternGroups = report.DetectedPatterns.GroupBy(p => p.Type);
            foreach (var group in patternGroups.OrderByDescending(g => g.Average(p => p.Exploitability)))
            {
                var avgExploitability = group.Average(p => p.Exploitability);
                var emoji = avgExploitability > 0.6 ? "🟢" : avgExploitability > 0.3 ? "🟡" : "🔴";
                result += $"{emoji} **{group.Key}:** {group.Count()} pattern(s), Avg Exploitability: {avgExploitability:P0}\n";
            }
            result += "\n";
            
            // Top opportunities
            var topPatterns = report.DetectedPatterns.OrderByDescending(p => p.Exploitability * p.Confidence).Take(3);
            if (topPatterns.Any())
            {
                result += "### Top Trading Opportunities\n";
                foreach (var pattern in topPatterns)
                {
                    result += $"🎯 **{pattern.Name}**\n";
                    result += $"   Score: {(pattern.Exploitability * pattern.Confidence):F2} | Type: {pattern.Type}\n";
                    result += $"   {pattern.Description}\n\n";
                }
            }
            
            // Risk warnings
            result += "### Risk Considerations\n";
            result += GenerateRiskWarnings(report);
            
            return result;
        }
        catch (Exception ex)
        {
            return $"Error generating pattern analysis summary: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Get pattern detection history")]
    public string GetPatternHistoryAsync()
    {
        try
        {
            var history = _patternService.GetPatternHistory();
            
            if (!history.Any())
            {
                return "No pattern detection history available.";
            }

            var result = $"## Pattern Detection History\n\n";
            result += $"Total analyses performed: {history.Count}\n\n";

            // Group by symbol
            var symbolGroups = history.GroupBy(h => h.Symbol)
                                     .OrderByDescending(g => g.Count())
                                     .Take(10);

            foreach (var group in symbolGroups)
            {
                result += $"### {group.Key}\n";
                result += $"**Analyses:** {group.Count()}\n";
                result += $"**Avg Patterns Found:** {group.Average(h => h.PatternsFound):F1}\n";
                result += $"**Avg High Confidence:** {group.Average(h => h.HighConfidencePatterns):F1}\n";
                
                var latest = group.OrderByDescending(h => h.Timestamp).First();
                result += $"**Latest Analysis:** {latest.Timestamp:MMM dd, yyyy HH:mm}\n\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"Error retrieving pattern history: {ex.Message}";
        }
    }

    // Helper methods
    private string GetPatternEmoji(PatternType type)
    {
        return type switch
        {
            PatternType.MeanReversion => "🔄",
            PatternType.Momentum => "📈",
            PatternType.Seasonality => "📅",
            PatternType.Volatility => "📊",
            PatternType.Correlation => "🔗",
            PatternType.Anomaly => "⚠️",
            PatternType.Fractal => "🌀",
            PatternType.Arbitrage => "💰",
            PatternType.Regime => "🔄",
            PatternType.Microstructure => "🔬",
            _ => "📉"
        };
    }

    private string FormatTimeSpan(TimeSpan span)
    {
        if (span.TotalDays >= 1)
            return $"{span.TotalDays:F0} day(s)";
        else if (span.TotalHours >= 1)
            return $"{span.TotalHours:F0} hour(s)";
        else
            return $"{span.TotalMinutes:F0} minute(s)";
    }

    private string FormatParameterValue(object value)
    {
        return value switch
        {
            double d => d.ToString("F4"),
            float f => f.ToString("F4"),
            int i => i.ToString(),
            string s => s,
            _ => value?.ToString() ?? "N/A"
        };
    }

    private string CalculateRiskLevel(RiskMetrics metrics)
    {
        var riskScore = 0;
        
        if (metrics.Volatility > 0.05) riskScore++; // High volatility
        if (metrics.MaxDrawdown > 0.2) riskScore++; // High drawdown
        if (metrics.SharpeRatio < 0.5) riskScore++; // Poor risk-adjusted returns
        if (metrics.VaR95 < -0.05) riskScore++; // High VaR
        if (metrics.PatternRisk > 0.5) riskScore++; // High pattern uncertainty
        
        return riskScore switch
        {
            >= 4 => "🔴 Very High",
            3 => "🟠 High", 
            2 => "🟡 Moderate",
            1 => "🟢 Low",
            _ => "✅ Very Low"
        };
    }

    private string GenerateRiskWarnings(PatternAnalysisReport report)
    {
        var warnings = new List<string>();

        // High volatility warning
        if (report.RiskMetrics.Volatility > 0.05)
        {
            warnings.Add("⚠️ High volatility detected - Use appropriate position sizing");
        }

        // Pattern confidence warning
        var lowConfidencePatterns = report.DetectedPatterns.Count(p => p.Confidence < 0.5);
        if (lowConfidencePatterns > report.DetectedPatterns.Count / 2)
        {
            warnings.Add("⚠️ Many patterns have low confidence - Validate with additional analysis");
        }

        // Risk-adjusted returns warning
        if (report.RiskMetrics.SharpeRatio < 0)
        {
            warnings.Add("⚠️ Negative risk-adjusted returns - Consider defensive strategies");
        }

        // Max drawdown warning
        if (report.RiskMetrics.MaxDrawdown > 0.2)
        {
            warnings.Add("⚠️ High historical drawdown - Implement strict risk management");
        }

        // No patterns warning
        if (!report.DetectedPatterns.Any())
        {
            warnings.Add("ℹ️ No significant patterns detected - Market may be in random walk phase");
        }

        return warnings.Any() ? string.Join("\n", warnings) : "✅ No significant risk warnings identified";
    }
}
