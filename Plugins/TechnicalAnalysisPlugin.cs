using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;
using System.Text.Json;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Semantic Kernel plugin for comprehensive technical analysis
/// </summary>
public class TechnicalAnalysisPlugin
{
    private readonly TechnicalAnalysisService _technicalAnalysisService;

    public TechnicalAnalysisPlugin(TechnicalAnalysisService technicalAnalysisService)
    {
        _technicalAnalysisService = technicalAnalysisService;
    }

    [KernelFunction, Description("Perform comprehensive technical analysis on a stock symbol with hundreds of indicators")]
    public async Task<string> AnalyzeSymbolAsync(
        [Description("The stock symbol to analyze (e.g., AAPL, TSLA, SPY)")] string symbol,
        [Description("Number of days of historical data to analyze (default: 100)")] int lookbackDays = 100)
    {
        try
        {
            var analysis = await _technicalAnalysisService.PerformFullAnalysisAsync(symbol, lookbackDays);
            
            var result = $"📊 Technical Analysis for {symbol}\n" +
                        $"Current Price: ${analysis.CurrentPrice:F2}\n" +
                        $"Signal: {GetSignalEmoji(analysis.OverallSignal)} {analysis.OverallSignal.ToString().ToUpper()}\n" +
                        $"Strength: {analysis.SignalStrength:F2}\n" +
                        $"Timestamp: {analysis.Timestamp:yyyy-MM-dd HH:mm:ss}\n\n" +
                        
                        "🔍 Key Technical Indicators:\n" +
                        $"• RSI: {GetIndicatorValue(analysis, "RSI"):F1} {GetRSICondition(GetIndicatorValue(analysis, "RSI"))}\n" +
                        $"• MACD: {GetIndicatorValue(analysis, "MACD"):F4} (Signal: {GetIndicatorValue(analysis, "MACD_Signal"):F4})\n" +
                        $"• SMA 20: ${GetIndicatorValue(analysis, "SMA_20"):F2}\n" +
                        $"• SMA 50: ${GetIndicatorValue(analysis, "SMA_50"):F2}\n" +
                        $"• SMA 200: ${GetIndicatorValue(analysis, "SMA_200"):F2}\n" +
                        $"• ATR: {GetIndicatorValue(analysis, "ATR"):F2}\n" +
                        $"• Volume (OBV): {GetIndicatorValue(analysis, "OBV"):F0}\n\n" +
                        
                        "📈 Bollinger Bands:\n" +
                        $"• Upper: ${GetIndicatorValue(analysis, "BB_Upper"):F2}\n" +
                        $"• Middle: ${GetIndicatorValue(analysis, "BB_Middle"):F2}\n" +
                        $"• Lower: ${GetIndicatorValue(analysis, "BB_Lower"):F2}\n" +
                        $"• %B: {GetIndicatorValue(analysis, "BB_PercentB"):F3}\n\n" +
                        
                        "🎯 Support & Resistance:\n" +
                        $"• Pivot Point: ${GetIndicatorValue(analysis, "Pivot_Point"):F2}\n" +
                        $"• Support 1: ${GetIndicatorValue(analysis, "Support_1"):F2}\n" +
                        $"• Resistance 1: ${GetIndicatorValue(analysis, "Resistance_1"):F2}\n\n" +
                        
                        $"💡 Analysis: {analysis.Reasoning}";

            if (analysis.Indicators.TryGetValue("Patterns", out var patternsObj) && 
                patternsObj is List<string> patterns && patterns.Any())
            {
                result += $"\n\n🔍 Detected Patterns:\n{string.Join("\n", patterns.Select(p => $"• {p}"))}";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error analyzing {symbol}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get detailed indicator breakdown for a symbol")]
    public async Task<string> GetDetailedIndicatorsAsync(
        [Description("The stock symbol to analyze")] string symbol,
        [Description("Category of indicators: trend, momentum, volume, volatility, or all")] string category = "all")
    {
        try
        {
            var analysis = await _technicalAnalysisService.PerformFullAnalysisAsync(symbol);
            var indicators = analysis.Indicators;
            
            var result = $"📊 Detailed {category.ToUpper()} Indicators for {symbol}\n";
            result += new string('=', 50) + "\n\n";

            switch (category.ToLower())
            {
                case "trend":
                    result += FormatTrendIndicators(indicators);
                    break;
                case "momentum":
                    result += FormatMomentumIndicators(indicators);
                    break;
                case "volume":
                    result += FormatVolumeIndicators(indicators);
                    break;
                case "volatility":
                    result += FormatVolatilityIndicators(indicators);
                    break;
                default:
                    result += FormatTrendIndicators(indicators) + "\n\n";
                    result += FormatMomentumIndicators(indicators) + "\n\n";
                    result += FormatVolumeIndicators(indicators) + "\n\n";
                    result += FormatVolatilityIndicators(indicators);
                    break;
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error getting indicators for {symbol}: {ex.Message}";
        }
    }

    [KernelFunction, Description("Compare technical analysis between multiple symbols")]
    public async Task<string> CompareSymbolsAsync(
        [Description("Comma-separated list of symbols to compare (e.g., AAPL,MSFT,GOOGL)")] string symbols)
    {
        try
        {
            var symbolList = symbols.Split(',').Select(s => s.Trim().ToUpper()).ToList();
            var analyses = new Dictionary<string, TechnicalAnalysisResult>();

            foreach (var symbol in symbolList)
            {
                analyses[symbol] = await _technicalAnalysisService.PerformFullAnalysisAsync(symbol);
            }

            var result = $"📊 Technical Analysis Comparison\n";
            result += new string('=', 50) + "\n\n";

            // Summary table
            result += "Symbol".PadRight(8) + " | " + "Signal".PadRight(12) + " | " + "RSI".PadRight(6) + " | " + "Price".PadRight(10) + " | Strength\n";
            result += new string('-', 60) + "\n";

            foreach (var (symbol, analysis) in analyses)
            {
                var signal = $"{GetSignalEmoji(analysis.OverallSignal)} {analysis.OverallSignal}";
                var rsi = GetIndicatorValue(analysis, "RSI");
                
                result += symbol.PadRight(8) + " | " + 
                         signal.PadRight(12) + " | " + 
                         $"{rsi:F1}".PadRight(6) + " | " + 
                         $"${analysis.CurrentPrice:F2}".PadRight(10) + " | " + 
                         $"{analysis.SignalStrength:F2}\n";
            }

            result += "\n📈 Key Observations:\n";
            
            // Find strongest signals
            var strongestBull = analyses.Where(a => a.Value.OverallSignal == Core.SignalType.StrongBuy || a.Value.OverallSignal == Core.SignalType.Buy)
                                      .OrderByDescending(a => a.Value.SignalStrength)
                                      .FirstOrDefault();
                                      
            var strongestBear = analyses.Where(a => a.Value.OverallSignal == Core.SignalType.StrongSell || a.Value.OverallSignal == Core.SignalType.Sell)
                                      .OrderByDescending(a => a.Value.SignalStrength)
                                      .FirstOrDefault();

            if (strongestBull.Key != null)
                result += $"• Strongest bullish signal: {strongestBull.Key} (Strength: {strongestBull.Value.SignalStrength:F2})\n";
                
            if (strongestBear.Key != null)
                result += $"• Strongest bearish signal: {strongestBear.Key} (Strength: {strongestBear.Value.SignalStrength:F2})\n";

            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error comparing symbols: {ex.Message}";
        }
    }

    [KernelFunction, Description("Get pattern recognition analysis for a symbol")]
    public async Task<string> GetPatternAnalysisAsync(
        [Description("The stock symbol to analyze")] string symbol)
    {
        try
        {
            var analysis = await _technicalAnalysisService.PerformFullAnalysisAsync(symbol);
            
            var result = $"🔍 Pattern Recognition Analysis for {symbol}\n";
            result += new string('=', 50) + "\n\n";

            if (analysis.Indicators.TryGetValue("Patterns", out var patternsObj) && 
                patternsObj is List<string> patterns)
            {
                if (patterns.Any())
                {
                    result += "Detected Patterns:\n";
                    foreach (var pattern in patterns)
                    {
                        result += $"✓ {pattern}\n";
                    }
                }
                else
                {
                    result += "No significant patterns detected at this time.\n";
                }
            }

            // Add candlestick pattern analysis (basic)
            result += "\n📊 Key Technical Levels:\n";
            result += $"• Current Price: ${analysis.CurrentPrice:F2}\n";
            result += $"• Pivot Point: ${GetIndicatorValue(analysis, "Pivot_Point"):F2}\n";
            result += $"• Immediate Support: ${GetIndicatorValue(analysis, "Support_1"):F2}\n";
            result += $"• Immediate Resistance: ${GetIndicatorValue(analysis, "Resistance_1"):F2}\n";

            // Fibonacci levels
            result += "\n🌀 Fibonacci Retracement Levels:\n";
            result += $"• 23.6%: ${GetIndicatorValue(analysis, "Fib_236"):F2}\n";
            result += $"• 38.2%: ${GetIndicatorValue(analysis, "Fib_382"):F2}\n";
            result += $"• 50.0%: ${GetIndicatorValue(analysis, "Fib_500"):F2}\n";
            result += $"• 61.8%: ${GetIndicatorValue(analysis, "Fib_618"):F2}\n";

            return result;
        }
        catch (Exception ex)
        {
            return $"❌ Error analyzing patterns for {symbol}: {ex.Message}";
        }
    }

    private static double GetIndicatorValue(TechnicalAnalysisResult analysis, string key)
    {
        return analysis.Indicators.TryGetValue(key, out var value) ? Convert.ToDouble(value) : 0.0;
    }

    private static string GetSignalEmoji(Core.SignalType signal)
    {
        return signal switch
        {
            Core.SignalType.StrongBuy => "🟢",
            Core.SignalType.Buy => "🔵",
            Core.SignalType.Hold => "⚪",
            Core.SignalType.Sell => "🟠",
            Core.SignalType.StrongSell => "🔴",
            _ => "⚪"
        };
    }

    private static string GetRSICondition(double rsi)
    {
        return rsi switch
        {
            > 70 => "(Overbought)",
            < 30 => "(Oversold)", 
            _ => "(Neutral)"
        };
    }

    private static string FormatTrendIndicators(Dictionary<string, object> indicators)
    {
        return "📈 TREND INDICATORS:\n" +
               $"• SMA 20: ${GetValue(indicators, "SMA_20"):F2}\n" +
               $"• SMA 50: ${GetValue(indicators, "SMA_50"):F2}\n" +
               $"• SMA 200: ${GetValue(indicators, "SMA_200"):F2}\n" +
               $"• EMA 12: ${GetValue(indicators, "EMA_12"):F2}\n" +
               $"• EMA 26: ${GetValue(indicators, "EMA_26"):F2}\n" +
               $"• MACD: {GetValue(indicators, "MACD"):F4}\n" +
               $"• MACD Signal: {GetValue(indicators, "MACD_Signal"):F4}\n" +
               $"• MACD Histogram: {GetValue(indicators, "MACD_Histogram"):F4}\n" +
               $"• Parabolic SAR: ${GetValue(indicators, "PSAR"):F2}\n" +
               $"• ADX: {GetValue(indicators, "ADX"):F1}\n" +
               $"• Aroon Up: {GetValue(indicators, "Aroon_Up"):F1}\n" +
               $"• Aroon Down: {GetValue(indicators, "Aroon_Down"):F1}";
    }

    private static string FormatMomentumIndicators(Dictionary<string, object> indicators)
    {
        return "⚡ MOMENTUM INDICATORS:\n" +
               $"• RSI: {GetValue(indicators, "RSI"):F1}\n" +
               $"• Stochastic %K: {GetValue(indicators, "Stoch_K"):F1}\n" +
               $"• Stochastic %D: {GetValue(indicators, "Stoch_D"):F1}\n" +
               $"• Williams %R: {GetValue(indicators, "Williams_R"):F1}\n" +
               $"• ROC (12): {GetValue(indicators, "ROC_12"):F2}%\n" +
               $"• CCI: {GetValue(indicators, "CCI"):F1}\n" +
               $"• MFI: {GetValue(indicators, "MFI"):F1}\n" +
               $"• Ultimate Oscillator: {GetValue(indicators, "Ultimate_Oscillator"):F1}\n" +
               $"• TRIX: {GetValue(indicators, "TRIX"):F4}";
    }

    private static string FormatVolumeIndicators(Dictionary<string, object> indicators)
    {
        return "📊 VOLUME INDICATORS:\n" +
               $"• OBV: {GetValue(indicators, "OBV"):F0}\n" +
               $"• VWAP: ${GetValue(indicators, "VWAP"):F2}\n" +
               $"• A/D Line: {GetValue(indicators, "ADL"):F0}\n" +
               $"• Chaikin Money Flow: {GetValue(indicators, "CMF"):F3}\n" +
               $"• Force Index: {GetValue(indicators, "Force_Index"):F0}\n" +
               $"• PVT: {GetValue(indicators, "PVT"):F0}";
    }

    private static string FormatVolatilityIndicators(Dictionary<string, object> indicators)
    {
        return "📊 VOLATILITY INDICATORS:\n" +
               $"• Bollinger Upper: ${GetValue(indicators, "BB_Upper"):F2}\n" +
               $"• Bollinger Middle: ${GetValue(indicators, "BB_Middle"):F2}\n" +
               $"• Bollinger Lower: ${GetValue(indicators, "BB_Lower"):F2}\n" +
               $"• Bollinger Width: {GetValue(indicators, "BB_Width"):F2}\n" +
               $"• Bollinger %B: {GetValue(indicators, "BB_PercentB"):F3}\n" +
               $"• ATR: {GetValue(indicators, "ATR"):F2}\n" +
               $"• Keltner Upper: ${GetValue(indicators, "KC_Upper"):F2}\n" +
               $"• Keltner Lower: ${GetValue(indicators, "KC_Lower"):F2}\n" +
               $"• Standard Deviation: {GetValue(indicators, "StdDev_20"):F2}";
    }

    private static double GetValue(Dictionary<string, object> indicators, string key)
    {
        return indicators.TryGetValue(key, out var value) ? Convert.ToDouble(value) : 0.0;
    }
}
