using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace QuantResearchAgent.Services;

/// <summary>
/// Context-aware strategy text parser that extracts all parameters using regex patterns and LLM fallback
/// </summary>
public class StrategyTextParser
{
    private readonly ILogger<StrategyTextParser> _logger;

    public StrategyTextParser(ILogger<StrategyTextParser> logger)
    {
        _logger = logger;
    }

    public ExtractedStrategy Parse(string strategyText)
    {
        var result = new ExtractedStrategy
        {
            RawText = strategyText,
            ExtractedAt = DateTime.UtcNow
        };

        if (string.IsNullOrWhiteSpace(strategyText))
        {
            result.Warnings.Add("Strategy text is empty.");
            return result;
        }

        // Extract all basic fields
        ExtractSymbol(strategyText, result);
        ExtractDates(strategyText, result);
        ExtractCapital(strategyText, result);
        ExtractResolution(strategyText, result);
        ExtractAssetClass(strategyText, result);
        ExtractDataSource(strategyText, result);
        ExtractStrategyName(strategyText, result);

        // Extract indicators and their parameters
        ExtractIndicators(strategyText, result);

        // Extract entry signals
        ExtractEntrySignals(strategyText, result);

        // Extract exit signals
        ExtractExitSignals(strategyText, result);

        // Extract position sizing
        ExtractPositionSizing(strategyText, result);

        // Extract risk management
        ExtractRiskManagement(strategyText, result);

        // Set confidence levels and warnings
        AssessConfidence(result);

        return result;
    }

    private void ExtractSymbol(string text, ExtractedStrategy result)
    {
        // Try explicit "Symbol:" declaration first
        var symbolMatch = Regex.Match(text, @"Symbol:\s*([A-Z0-9\.\-]+)", RegexOptions.IgnoreCase);
        if (symbolMatch.Success)
        {
            result.Symbol = symbolMatch.Groups[1].Value.Trim().ToUpperInvariant();
            result.SymbolConfidence = 1.0m;
            return;
        }

        // Try ticker-like patterns
        var tickerMatch = Regex.Match(text, @"\b([A-Z]{2,6}(?:USDT)?)\b");
        if (tickerMatch.Success)
        {
            result.Symbol = tickerMatch.Groups[1].Value.Trim().ToUpperInvariant();
            result.SymbolConfidence = 0.7m;
            return;
        }

        result.Warnings.Add("Could not extract symbol from text.");
    }

    private void ExtractDates(string text, ExtractedStrategy result)
    {
        var startDate = ExtractDateField(text, "Start Date");
        var endDate = ExtractDateField(text, "End Date");

        if (startDate.HasValue)
        {
            result.StartDate = startDate.Value;
            result.StartDateConfidence = 0.95m;
        }
        else
        {
            result.StartDate = DateTime.UtcNow.AddYears(-2).Date;
            result.StartDateConfidence = 0.3m;
            result.Warnings.Add("Start date not found; defaulting to 2 years ago.");
        }

        if (endDate.HasValue)
        {
            result.EndDate = endDate.Value;
            result.EndDateConfidence = 0.95m;
        }
        else
        {
            result.EndDate = DateTime.UtcNow.Date;
            result.EndDateConfidence = 0.3m;
            result.Warnings.Add("End date not found; defaulting to today.");
        }
    }

    private void ExtractCapital(string text, ExtractedStrategy result)
    {
        var capitalMatch = Regex.Match(
            text,
            @"(?:Initial\s+)?Capital:\s*\$?([0-9,]+(?:\.[0-9]+)?)",
            RegexOptions.IgnoreCase);

        if (capitalMatch.Success &&
            decimal.TryParse(capitalMatch.Groups[1].Value.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var capital))
        {
            result.InitialCapital = capital;
            result.CapitalConfidence = 0.95m;
        }
        else
        {
            result.InitialCapital = 100000;
            result.CapitalConfidence = 0.2m;
            result.Warnings.Add("Initial capital not found; defaulting to $100,000.");
        }
    }

    private void ExtractResolution(string text, ExtractedStrategy result)
    {
        if (Regex.IsMatch(text, "minute", RegexOptions.IgnoreCase))
        {
            result.Resolution = "minute";
            result.ResolutionConfidence = 0.9m;
        }
        else if (Regex.IsMatch(text, "hour", RegexOptions.IgnoreCase))
        {
            result.Resolution = "hour";
            result.ResolutionConfidence = 0.9m;
        }
        else if (Regex.IsMatch(text, "daily", RegexOptions.IgnoreCase))
        {
            result.Resolution = "daily";
            result.ResolutionConfidence = 0.9m;
        }
        else
        {
            result.Resolution = "daily";
            result.ResolutionConfidence = 0.2m;
            result.Warnings.Add("Resolution not found; defaulting to daily.");
        }
    }

    private void ExtractAssetClass(string text, ExtractedStrategy result)
    {
        if (Regex.IsMatch(text, @"crypto|bitcoin|ethereum|binance", RegexOptions.IgnoreCase))
        {
            result.AssetClass = "crypto";
            result.AssetClassConfidence = 0.85m;
        }
        else if (Regex.IsMatch(text, @"forex|fx|usd|eur|gbp", RegexOptions.IgnoreCase))
        {
            result.AssetClass = "forex";
            result.AssetClassConfidence = 0.8m;
        }
        else if (Regex.IsMatch(text, @"futures|es|nq|cl|gc", RegexOptions.IgnoreCase))
        {
            result.AssetClass = "futures";
            result.AssetClassConfidence = 0.8m;
        }
        else if (Regex.IsMatch(text, @"options|call|put|strike", RegexOptions.IgnoreCase))
        {
            result.AssetClass = "options";
            result.AssetClassConfidence = 0.8m;
        }
        else
        {
            result.AssetClass = "equity";
            result.AssetClassConfidence = 0.4m;
        }
    }

    private void ExtractDataSource(string text, ExtractedStrategy result)
    {
        if (Regex.IsMatch(text, "alpaca", RegexOptions.IgnoreCase))
        {
            result.PreferredDataSource = "alpaca";
            result.DataSourceConfidence = 0.95m;
        }
        else if (Regex.IsMatch(text, "binance", RegexOptions.IgnoreCase))
        {
            result.PreferredDataSource = "binance";
            result.DataSourceConfidence = 0.95m;
        }
        else if (Regex.IsMatch(text, "polygon", RegexOptions.IgnoreCase))
        {
            result.PreferredDataSource = "polygon";
            result.DataSourceConfidence = 0.95m;
        }
        else if (Regex.IsMatch(text, "databento", RegexOptions.IgnoreCase))
        {
            result.PreferredDataSource = "databento";
            result.DataSourceConfidence = 0.95m;
        }
        else
        {
            result.PreferredDataSource = "auto";
            result.DataSourceConfidence = 0.1m;
        }
    }

    private void ExtractStrategyName(string text, ExtractedStrategy result)
    {
        // Look for explicit strategy name
        var nameMatch = Regex.Match(text, @"(?:Strategy|Name):\s*([^\n\r]+)", RegexOptions.IgnoreCase);
        if (nameMatch.Success)
        {
            result.StrategyName = nameMatch.Groups[1].Value.Trim();
            result.StrategyNameConfidence = 0.95m;
            return;
        }

        // Try to infer from indicators
        if (result.Indicators.Count > 0)
        {
            var indicatorNames = string.Join(" + ", result.Indicators.Select(i => i.IndicatorType));
            result.StrategyName = $"{indicatorNames} Strategy";
            result.StrategyNameConfidence = 0.6m;
        }
        else
        {
            result.StrategyName = "Custom Strategy";
            result.StrategyNameConfidence = 0.2m;
        }
    }

    private void ExtractIndicators(string text, ExtractedStrategy result)
    {
        // RSI
        if (Regex.IsMatch(text, @"RSI", RegexOptions.IgnoreCase))
        {
            var rsiIndicator = new IndicatorExtraction { IndicatorType = "RSI" };

            var periodMatch = Regex.Match(text, @"RSI\s*\(?\s*(\d{1,3})\s*\)?", RegexOptions.IgnoreCase);
            if (periodMatch.Success && int.TryParse(periodMatch.Groups[1].Value, out var period))
            {
                rsiIndicator.Parameters["period"] = period;
                rsiIndicator.ConfidenceLevel = 0.95m;
            }
            else
            {
                rsiIndicator.Parameters["period"] = 14;
                rsiIndicator.ConfidenceLevel = 0.5m;
            }

            result.Indicators.Add(rsiIndicator);
        }

        // MACD
        if (Regex.IsMatch(text, @"MACD", RegexOptions.IgnoreCase))
        {
            var macdIndicator = new IndicatorExtraction { IndicatorType = "MACD" };
            macdIndicator.Parameters["fastPeriod"] = 12;
            macdIndicator.Parameters["slowPeriod"] = 26;
            macdIndicator.Parameters["signalPeriod"] = 9;
            macdIndicator.ConfidenceLevel = 0.8m;
            result.Indicators.Add(macdIndicator);
        }

        // SMA / EMA
        if (Regex.IsMatch(text, @"SMA|Simple\s+Moving\s+Average", RegexOptions.IgnoreCase))
        {
            var smaIndicator = new IndicatorExtraction { IndicatorType = "SMA" };
            var periodMatch = Regex.Match(text, @"SMA\s*\(?\s*(\d{1,3})\s*\)?", RegexOptions.IgnoreCase);
            smaIndicator.Parameters["period"] = periodMatch.Success && int.TryParse(periodMatch.Groups[1].Value, out var p) ? p : 20;
            smaIndicator.ConfidenceLevel = 0.85m;
            result.Indicators.Add(smaIndicator);
        }

        if (Regex.IsMatch(text, @"EMA|Exponential\s+Moving\s+Average", RegexOptions.IgnoreCase))
        {
            var emaIndicator = new IndicatorExtraction { IndicatorType = "EMA" };
            var periodMatch = Regex.Match(text, @"EMA\s*\(?\s*(\d{1,3})\s*\)?", RegexOptions.IgnoreCase);
            emaIndicator.Parameters["period"] = periodMatch.Success && int.TryParse(periodMatch.Groups[1].Value, out var p) ? p : 20;
            emaIndicator.ConfidenceLevel = 0.85m;
            result.Indicators.Add(emaIndicator);
        }

        // Bollinger Bands
        if (Regex.IsMatch(text, @"Bollinger|BOLL", RegexOptions.IgnoreCase))
        {
            var bbIndicator = new IndicatorExtraction { IndicatorType = "BollingerBands" };
            bbIndicator.Parameters["period"] = 20;
            bbIndicator.Parameters["stdDev"] = 2.0;
            bbIndicator.ConfidenceLevel = 0.8m;
            result.Indicators.Add(bbIndicator);
        }

        // Stochastic
        if (Regex.IsMatch(text, @"Stochastic", RegexOptions.IgnoreCase))
        {
            var stochIndicator = new IndicatorExtraction { IndicatorType = "Stochastic" };
            stochIndicator.Parameters["period"] = 14;
            stochIndicator.Parameters["kPeriod"] = 3;
            stochIndicator.Parameters["dPeriod"] = 3;
            stochIndicator.ConfidenceLevel = 0.8m;
            result.Indicators.Add(stochIndicator);
        }

        // ATR
        if (Regex.IsMatch(text, @"ATR|Average\s+True\s+Range", RegexOptions.IgnoreCase))
        {
            var atrIndicator = new IndicatorExtraction { IndicatorType = "ATR" };
            atrIndicator.Parameters["period"] = 14;
            atrIndicator.ConfidenceLevel = 0.8m;
            result.Indicators.Add(atrIndicator);
        }

        if (result.Indicators.Count == 0)
        {
            result.Warnings.Add("No recognized indicators found in strategy text.");
        }
    }

    private void ExtractEntrySignals(string text, ExtractedStrategy result)
    {
        var entryPatterns = new[]
        {
            (@"Buy.*?RSI.*?below\s*(\d+)", "RSI Oversold"),
            (@"Buy.*?price.*?breaks\s*above", "Breakout"),
            (@"Buy.*?golden\s+cross|Buy.*?SMA.*?cross", "SMA Crossover"),
            (@"Enter.*?long", "Long Entry"),
            (@"Buy.*?support", "Support Bounce"),
            (@"Buy.*?oversold", "Oversold Entry"),
        };

        foreach (var (pattern, signalType) in entryPatterns)
        {
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
            {
                result.EntrySignals.Add(new SignalExtraction
                {
                    SignalType = signalType,
                    ConfidenceLevel = 0.75m
                });
            }
        }

        if (result.EntrySignals.Count == 0)
        {
            result.Warnings.Add("No entry signals detected.");
        }
    }

    private void ExtractExitSignals(string text, ExtractedStrategy result)
    {
        var exitPatterns = new[]
        {
            (@"Sell.*?RSI.*?above\s*(\d+)", "RSI Overbought"),
            (@"Sell.*?take.?profit", "Take Profit"),
            (@"Sell.*?stop.?loss", "Stop Loss"),
            (@"Exit.*?after\s*(\d+)\s*(?:bars|days|candles)", "Time-Based"),
            (@"Exit.*?short", "Short Exit"),
            (@"Liquidate|Close.*?position", "Position Close"),
        };

        foreach (var (pattern, signalType) in exitPatterns)
        {
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
            {
                result.ExitSignals.Add(new SignalExtraction
                {
                    SignalType = signalType,
                    ConfidenceLevel = 0.75m
                });
            }
        }

        if (result.ExitSignals.Count == 0)
        {
            result.Warnings.Add("No exit signals detected.");
        }
    }

    private void ExtractPositionSizing(string text, ExtractedStrategy result)
    {
        // Percentage of capital
        var percentMatch = Regex.Match(text, @"Use\s*(\d{1,3})\s*%\s*of\s*(?:available\s+)?cash", RegexOptions.IgnoreCase);
        if (percentMatch.Success && decimal.TryParse(percentMatch.Groups[1].Value, out var pct))
        {
            result.PositionSizing = new PositionSizingExtraction
            {
                Method = "PercentOfCapital",
                Parameters = new Dictionary<string, decimal> { { "percent", pct } },
                ConfidenceLevel = 0.9m
            };
            return;
        }

        // Fixed shares
        var sharesMatch = Regex.Match(text, @"Trade\s*(\d+)\s*(?:shares|contracts)", RegexOptions.IgnoreCase);
        if (sharesMatch.Success && int.TryParse(sharesMatch.Groups[1].Value, out var shares))
        {
            result.PositionSizing = new PositionSizingExtraction
            {
                Method = "FixedShares",
                Parameters = new Dictionary<string, decimal> { { "shares", shares } },
                ConfidenceLevel = 0.9m
            };
            return;
        }

        // Volatility-based
        if (Regex.IsMatch(text, "volatility", RegexOptions.IgnoreCase))
        {
            result.PositionSizing = new PositionSizingExtraction
            {
                Method = "VolatilityBased",
                Parameters = new Dictionary<string, decimal>(),
                ConfidenceLevel = 0.7m
            };
            result.Warnings.Add("Volatility-based sizing detected but not fully specified.");
            return;
        }

        // Default
        result.PositionSizing = new PositionSizingExtraction
        {
            Method = "PercentOfCapital",
            Parameters = new Dictionary<string, decimal> { { "percent", 100 } },
            ConfidenceLevel = 0.2m
        };
        result.Warnings.Add("Position sizing not found; defaulting to 100% of available capital.");
    }

    private void ExtractRiskManagement(string text, ExtractedStrategy result)
    {
        // Stop loss
        var slMatch = Regex.Match(text, @"Stop.?Loss:\s*(\d+)\s*%", RegexOptions.IgnoreCase);
        if (slMatch.Success && decimal.TryParse(slMatch.Groups[1].Value, out var sl))
        {
            result.RiskManagement["StopLossPercent"] = sl.ToString();
        }

        // Take profit
        var tpMatch = Regex.Match(text, @"Take.?Profit:\s*(\d+)\s*%", RegexOptions.IgnoreCase);
        if (tpMatch.Success && decimal.TryParse(tpMatch.Groups[1].Value, out var tp))
        {
            result.RiskManagement["TakeProfitPercent"] = tp.ToString();
        }

        // Risk per trade
        var riskMatch = Regex.Match(text, @"Risk.*?(\d+)\s*%", RegexOptions.IgnoreCase);
        if (riskMatch.Success && decimal.TryParse(riskMatch.Groups[1].Value, out var risk))
        {
            result.RiskManagement["RiskPerTrade"] = risk.ToString();
        }

        // Max drawdown
        var ddMatch = Regex.Match(text, @"Max.*?Drawdown:\s*(\d+)\s*%", RegexOptions.IgnoreCase);
        if (ddMatch.Success && decimal.TryParse(ddMatch.Groups[1].Value, out var dd))
        {
            result.RiskManagement["MaxDrawdown"] = dd.ToString();
        }
    }

    private void AssessConfidence(ExtractedStrategy result)
    {
        var lowConfidenceFields = new List<string>();

        if (result.SymbolConfidence < 0.7m) lowConfidenceFields.Add("Symbol");
        if (result.StartDateConfidence < 0.7m) lowConfidenceFields.Add("StartDate");
        if (result.EndDateConfidence < 0.7m) lowConfidenceFields.Add("EndDate");
        if (result.CapitalConfidence < 0.7m) lowConfidenceFields.Add("Capital");
        if (result.ResolutionConfidence < 0.7m) lowConfidenceFields.Add("Resolution");
        if (result.AssetClassConfidence < 0.7m) lowConfidenceFields.Add("AssetClass");
        if (result.DataSourceConfidence < 0.7m) lowConfidenceFields.Add("DataSource");

        if (lowConfidenceFields.Any())
        {
            result.Warnings.Add($"Low confidence extraction for: {string.Join(", ", lowConfidenceFields)}");
        }

        result.OverallConfidence = (
            result.SymbolConfidence +
            result.StartDateConfidence +
            result.EndDateConfidence +
            result.CapitalConfidence +
            result.ResolutionConfidence
        ) / 5m;
    }

    private DateTime? ExtractDateField(string content, string label)
    {
        var pattern = $@"{Regex.Escape(label)}:\s*([^\n\r]+)";
        var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);

        if (!match.Success) return null;

        var raw = match.Groups[1].Value.Trim();
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
        {
            return date.Date;
        }

        return null;
    }
}

/// <summary>
/// Extracted strategy with all parsed parameters and confidence levels
/// </summary>
public class ExtractedStrategy
{
    public string RawText { get; set; } = string.Empty;
    public DateTime ExtractedAt { get; set; }

    // Basic fields
    public string Symbol { get; set; } = string.Empty;
    public decimal SymbolConfidence { get; set; }

    public DateTime StartDate { get; set; }
    public decimal StartDateConfidence { get; set; }

    public DateTime EndDate { get; set; }
    public decimal EndDateConfidence { get; set; }

    public decimal InitialCapital { get; set; }
    public decimal CapitalConfidence { get; set; }

    public string Resolution { get; set; } = "daily";
    public decimal ResolutionConfidence { get; set; }

    public string AssetClass { get; set; } = "equity";
    public decimal AssetClassConfidence { get; set; }

    public string PreferredDataSource { get; set; } = "auto";
    public decimal DataSourceConfidence { get; set; }

    public string StrategyName { get; set; } = "Custom Strategy";
    public decimal StrategyNameConfidence { get; set; }

    // Collections
    public List<IndicatorExtraction> Indicators { get; set; } = new();
    public List<SignalExtraction> EntrySignals { get; set; } = new();
    public List<SignalExtraction> ExitSignals { get; set; } = new();
    public PositionSizingExtraction? PositionSizing { get; set; }
    public Dictionary<string, string> RiskManagement { get; set; } = new();

    // Metadata
    public List<string> Warnings { get; set; } = new();
    public decimal OverallConfidence { get; set; }
}

public class IndicatorExtraction
{
    public string IndicatorType { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public decimal ConfidenceLevel { get; set; }
}

public class SignalExtraction
{
    public string SignalType { get; set; } = string.Empty;
    public Dictionary<string, object> Details { get; set; } = new();
    public decimal ConfidenceLevel { get; set; }
}

public class PositionSizingExtraction
{
    public string Method { get; set; } = string.Empty;
    public Dictionary<string, decimal> Parameters { get; set; } = new();
    public decimal ConfidenceLevel { get; set; }
}
