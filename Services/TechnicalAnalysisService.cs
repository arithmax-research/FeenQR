using Microsoft.Extensions.Logging;
using Skender.Stock.Indicators;
using QuantResearchAgent.Core;
using System.Collections.Concurrent;
using Alpaca.Markets;
using MathNet.Numerics.Statistics;

namespace QuantResearchAgent.Services;

public class TechnicalAnalysisService
{
    private readonly ILogger<TechnicalAnalysisService> _logger;
    private readonly AlpacaService _alpacaService;
    private readonly ConcurrentDictionary<string, TechnicalAnalysisResult> _analysisCache = new();

    public TechnicalAnalysisService(ILogger<TechnicalAnalysisService> logger, AlpacaService alpacaService)
    {
        _logger = logger;
        _alpacaService = alpacaService;
    }

    public async Task<TechnicalAnalysisResult> PerformFullAnalysisAsync(string symbol, int lookbackDays = 100)
    {
        try
        {
            _logger.LogInformation("Starting full technical analysis for {Symbol} with {Days} days lookback", symbol, lookbackDays);
            
            var cacheKey = $"{symbol}_{lookbackDays}";
            if (_analysisCache.TryGetValue(cacheKey, out var cached) && 
                DateTime.UtcNow - cached.Timestamp < TimeSpan.FromMinutes(5))
            {
                _logger.LogInformation("Returning cached analysis for {Symbol}", symbol);
                return cached;
            }

            _logger.LogInformation("Fetching historical data for {Symbol}...", symbol);
            var bars = await _alpacaService.GetHistoricalBarsAsync(symbol, lookbackDays);
            
            _logger.LogInformation("Retrieved {Count} bars for {Symbol}", bars.Count, symbol);
            
            if (!bars.Any())
            {
                _logger.LogError("No historical data available for {Symbol}", symbol);
                throw new InvalidOperationException($"No historical data available for {symbol}");
            }

            _logger.LogInformation("Converting bars to quotes for {Symbol}...", symbol);
            var quotes = bars.Select(bar => new Quote
            {
                Timestamp = bar.TimeUtc,
                Open = bar.Open,
                High = bar.High,
                Low = bar.Low,
                Close = bar.Close,
                Volume = bar.Volume
            }).OrderBy(q => q.Timestamp).ToList();

            _logger.LogInformation("Successfully converted {Count} quotes for {Symbol}, calculating indicators...", quotes.Count, symbol);

            var result = new TechnicalAnalysisResult
            {
                Symbol = symbol,
                Timestamp = DateTime.UtcNow,
                CurrentPrice = (double)quotes.Last().Close,
                Indicators = new Dictionary<string, object>()
            };

            // Trend Indicators
            await CalculateTrendIndicators(quotes, result);
            
            // Momentum Indicators
            await CalculateMomentumIndicators(quotes, result);
            
            // Volume Indicators
            await CalculateVolumeIndicators(quotes, result);
            
            // Volatility Indicators
            await CalculateVolatilityIndicators(quotes, result);
            
            // Support and Resistance
            await CalculateSupportResistance(quotes, result);
            
            // Pattern Recognition
            await PerformPatternRecognition(quotes, result);
            
            // Overall Analysis
            result.OverallSignal = DetermineOverallSignal(result);
            result.SignalStrength = CalculateSignalStrength(result);
            result.Reasoning = GenerateReasoning(result);

            _analysisCache[cacheKey] = result;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing technical analysis for {Symbol}", symbol);
            throw;
        }
    }

    private Task CalculateTrendIndicators(List<Quote> quotes, TechnicalAnalysisResult result)
    {
        // Simple Moving Averages
        var sma20 = quotes.ToSma(20).LastOrDefault()?.Sma;
        var sma50 = quotes.ToSma(50).LastOrDefault()?.Sma;
        var sma200 = quotes.ToSma(200).LastOrDefault()?.Sma;
        
        result.Indicators["SMA_20"] = sma20 ?? 0;
        result.Indicators["SMA_50"] = sma50 ?? 0;
        result.Indicators["SMA_200"] = sma200 ?? 0;

        // Exponential Moving Averages
        var ema12 = quotes.ToEma(12).LastOrDefault()?.Ema;
        var ema26 = quotes.ToEma(26).LastOrDefault()?.Ema;
        
        result.Indicators["EMA_12"] = ema12 ?? 0;
        result.Indicators["EMA_26"] = ema26 ?? 0;

        // MACD
        var macd = quotes.ToMacd().LastOrDefault();
        result.Indicators["MACD"] = macd?.Macd ?? 0;
        result.Indicators["MACD_Signal"] = macd?.Signal ?? 0;
        result.Indicators["MACD_Histogram"] = macd?.Histogram ?? 0;

        // Parabolic SAR
        var psar = quotes.ToParabolicSar().LastOrDefault()?.Sar;
        result.Indicators["PSAR"] = psar ?? 0;

        // Average Directional Index (ADX)
        var adx = quotes.ToAdx().LastOrDefault();
        result.Indicators["ADX"] = adx?.Adx ?? 0;
        result.Indicators["ADX_PlusDI"] = adx?.Pdi ?? 0;
        result.Indicators["ADX_MinusDI"] = adx?.Mdi ?? 0;

        // Aroon
        var aroon = quotes.ToAroon().LastOrDefault();
        result.Indicators["Aroon_Up"] = aroon?.AroonUp ?? 0;
        result.Indicators["Aroon_Down"] = aroon?.AroonDown ?? 0;
        result.Indicators["Aroon_Oscillator"] = (aroon?.AroonUp ?? 0) - (aroon?.AroonDown ?? 0);

        // Ichimoku
        var ichimoku = quotes.ToIchimoku().LastOrDefault();
        result.Indicators["Ichimoku_TenkanSen"] = ichimoku?.TenkanSen ?? 0;
        result.Indicators["Ichimoku_KijunSen"] = ichimoku?.KijunSen ?? 0;
        result.Indicators["Ichimoku_SenkouSpanA"] = ichimoku?.SenkouSpanA ?? 0;
        result.Indicators["Ichimoku_SenkouSpanB"] = ichimoku?.SenkouSpanB ?? 0;

        return Task.CompletedTask;
    }

    private Task CalculateMomentumIndicators(List<Quote> quotes, TechnicalAnalysisResult result)
    {
        // RSI
        var rsi = quotes.ToRsi().LastOrDefault()?.Rsi;
        result.Indicators["RSI"] = rsi ?? 0;

        // Stochastic Oscillator
        var stoch = quotes.ToStoch().LastOrDefault();
        result.Indicators["Stoch_K"] = stoch?.K ?? 0;
        result.Indicators["Stoch_D"] = stoch?.D ?? 0;

        // Williams %R
        var willR = quotes.ToWilliamsR().LastOrDefault()?.WilliamsR;
        result.Indicators["Williams_R"] = willR ?? 0;

        // Rate of Change (ROC)
        var roc = quotes.ToRoc(12).LastOrDefault()?.Roc;
        result.Indicators["ROC_12"] = roc ?? 0;

        // Commodity Channel Index (CCI)
        var cci = quotes.ToCci().LastOrDefault()?.Cci;
        result.Indicators["CCI"] = cci ?? 0;

        // Money Flow Index (MFI)
        var mfi = quotes.ToMfi().LastOrDefault()?.Mfi;
        result.Indicators["MFI"] = mfi ?? 0;

        // Ultimate Oscillator
        var uo = quotes.ToUltimate().LastOrDefault()?.Ultimate;
        result.Indicators["Ultimate_Oscillator"] = uo ?? 0;

        // TRIX
        var trix = quotes.ToTrix(14).LastOrDefault()?.Trix;
        result.Indicators["TRIX"] = trix ?? 0;

        return Task.CompletedTask;
    }

    private Task CalculateVolumeIndicators(List<Quote> quotes, TechnicalAnalysisResult result)
    {
        // On-Balance Volume (OBV)
        var obv = quotes.ToObv().LastOrDefault()?.Obv;
        result.Indicators["OBV"] = obv ?? 0;

        // Volume Weighted Average Price (VWAP)
        var vwap = quotes.ToVwap().LastOrDefault()?.Vwap;
        result.Indicators["VWAP"] = vwap ?? 0;

        // Accumulation/Distribution Line
        var adl = quotes.ToAdl().LastOrDefault()?.Adl;
        result.Indicators["ADL"] = adl ?? 0;

        // Chaikin Money Flow
        var cmf = quotes.ToCmf().LastOrDefault()?.Cmf;
        result.Indicators["CMF"] = cmf ?? 0;

        // Force Index
        var fi = quotes.ToForceIndex().LastOrDefault()?.ForceIndex;
        result.Indicators["Force_Index"] = fi ?? 0;

        // Price Volume Trend (simplified calculation)
        var currentPrice = (double)quotes.Last().Close;
        var previousPrice = quotes.Count > 1 ? (double)quotes[^2].Close : currentPrice;
        var currentVolume = (double)quotes.Last().Volume;
        var pvt = currentVolume * ((currentPrice - previousPrice) / previousPrice);
        result.Indicators["PVT"] = pvt;

        return Task.CompletedTask;
    }

    private Task CalculateVolatilityIndicators(List<Quote> quotes, TechnicalAnalysisResult result)
    {
        // Bollinger Bands
        var bb = quotes.ToBollingerBands().LastOrDefault();
        result.Indicators["BB_Upper"] = bb?.UpperBand ?? 0;
        result.Indicators["BB_Middle"] = bb?.Sma ?? 0;
        result.Indicators["BB_Lower"] = bb?.LowerBand ?? 0;
        result.Indicators["BB_Width"] = (bb?.UpperBand - bb?.LowerBand) ?? 0;
        result.Indicators["BB_PercentB"] = bb?.PercentB ?? 0;

        // Average True Range (ATR)
        var atr = quotes.ToAtr().LastOrDefault()?.Atr;
        result.Indicators["ATR"] = atr ?? 0;

        // Keltner Channels
        var kc = quotes.ToKeltner().LastOrDefault();
        result.Indicators["KC_Upper"] = kc?.UpperBand ?? 0;
        result.Indicators["KC_Middle"] = kc?.Centerline ?? 0;
        result.Indicators["KC_Lower"] = kc?.LowerBand ?? 0;

        // Donchian Channels
        var dc = quotes.ToDonchian().LastOrDefault();
        result.Indicators["DC_Upper"] = dc?.UpperBand ?? 0;
        result.Indicators["DC_Middle"] = dc?.Centerline ?? 0;
        result.Indicators["DC_Lower"] = dc?.LowerBand ?? 0;

        // Standard Deviation
        var stdDev = quotes.ToStdDev(20).LastOrDefault()?.StdDev;
        result.Indicators["StdDev_20"] = stdDev ?? 0;

        return Task.CompletedTask;
    }

    private Task CalculateSupportResistance(List<Quote> quotes, TechnicalAnalysisResult result)
    {
        var highs = quotes.TakeLast(50).Select(q => (double)q.High).ToList();
        var lows = quotes.TakeLast(50).Select(q => (double)q.Low).ToList();

        // Pivot Points
        var lastQuote = quotes.Last();
        var pivotPoint = ((double)lastQuote.High + (double)lastQuote.Low + (double)lastQuote.Close) / 3;
        
        result.Indicators["Pivot_Point"] = pivotPoint;
        result.Indicators["Resistance_1"] = 2 * pivotPoint - (double)lastQuote.Low;
        result.Indicators["Support_1"] = 2 * pivotPoint - (double)lastQuote.High;
        result.Indicators["Resistance_2"] = pivotPoint + ((double)lastQuote.High - (double)lastQuote.Low);
        result.Indicators["Support_2"] = pivotPoint - ((double)lastQuote.High - (double)lastQuote.Low);

        // Fibonacci Retracement Levels
        var recentHigh = highs.Max();
        var recentLow = lows.Min();
        var diff = recentHigh - recentLow;

        result.Indicators["Fib_236"] = recentHigh - (diff * 0.236);
        result.Indicators["Fib_382"] = recentHigh - (diff * 0.382);
        result.Indicators["Fib_500"] = recentHigh - (diff * 0.500);
        result.Indicators["Fib_618"] = recentHigh - (diff * 0.618);
        result.Indicators["Fib_786"] = recentHigh - (diff * 0.786);

        return Task.CompletedTask;
    }

    private Task PerformPatternRecognition(List<Quote> quotes, TechnicalAnalysisResult result)
    {
        var patterns = new List<string>();

        // Golden Cross / Death Cross
        var sma50 = (double)(result.Indicators["SMA_50"]);
        var sma200 = (double)(result.Indicators["SMA_200"]);
        
        if (sma50 > sma200)
            patterns.Add("Golden Cross");
        else if (sma50 < sma200)
            patterns.Add("Death Cross");

        // MACD Bullish/Bearish Divergence
        var macd = (double)(result.Indicators["MACD"]);
        var macdSignal = (double)(result.Indicators["MACD_Signal"]);
        
        if (macd > macdSignal)
            patterns.Add("MACD Bullish");
        else if (macd < macdSignal)
            patterns.Add("MACD Bearish");

        // RSI Overbought/Oversold
        var rsi = (double)(result.Indicators["RSI"]);
        if (rsi > 70)
            patterns.Add("RSI Overbought");
        else if (rsi < 30)
            patterns.Add("RSI Oversold");

        // Bollinger Band Squeeze
        var bbWidth = (double)(result.Indicators["BB_Width"]);
        var atr = (double)(result.Indicators["ATR"]);
        if (bbWidth < atr * 0.5)
            patterns.Add("Bollinger Squeeze");

        result.Indicators["Patterns"] = patterns;

        return Task.CompletedTask;
    }

    private SignalType DetermineOverallSignal(TechnicalAnalysisResult result)
    {
        var bullishSignals = 0;
        var bearishSignals = 0;

        // Trend Analysis
        var currentPrice = result.CurrentPrice;
        var sma20 = (double)result.Indicators["SMA_20"];
        var sma50 = (double)result.Indicators["SMA_50"];

        if (currentPrice > sma20 && sma20 > sma50) bullishSignals += 2;
        else if (currentPrice < sma20 && sma20 < sma50) bearishSignals += 2;

        // MACD
        var macd = (double)result.Indicators["MACD"];
        var macdSignal = (double)result.Indicators["MACD_Signal"];
        if (macd > macdSignal) bullishSignals++;
        else bearishSignals++;

        // RSI
        var rsi = (double)result.Indicators["RSI"];
        if (rsi > 30 && rsi < 70) bullishSignals++;
        else if (rsi > 70) bearishSignals++;
        else if (rsi < 30) bullishSignals++;

        // Determine signal
        var netSignal = bullishSignals - bearishSignals;
        
        if (netSignal >= 3) return SignalType.StrongBuy;
        else if (netSignal >= 1) return SignalType.Buy;
        else if (netSignal <= -3) return SignalType.StrongSell;
        else if (netSignal <= -1) return SignalType.Sell;
        else return SignalType.Hold;
    }

    private double CalculateSignalStrength(TechnicalAnalysisResult result)
    {
        var indicators = result.Indicators;
        var strengthFactors = new List<double>();

        // RSI strength
        var rsi = (double)indicators["RSI"];
        strengthFactors.Add(Math.Abs(rsi - 50) / 50.0);

        // MACD strength
        var macd = (double)indicators["MACD"];
        var macdSignal = (double)indicators["MACD_Signal"];
        strengthFactors.Add(Math.Min(Math.Abs(macd - macdSignal) / Math.Abs(macdSignal), 1.0));

        // Volume strength (simplified)
        var mfi = (double)indicators["MFI"];
        strengthFactors.Add(Math.Abs(mfi - 50) / 50.0);

        return strengthFactors.Average();
    }

    private string GenerateReasoning(TechnicalAnalysisResult result)
    {
        var reasoning = new List<string>();
        var indicators = result.Indicators;

        var rsi = (double)indicators["RSI"];
        var macd = (double)indicators["MACD"];
        var macdSignal = (double)indicators["MACD_Signal"];
        var currentPrice = result.CurrentPrice;
        var sma20 = (double)indicators["SMA_20"];

        reasoning.Add($"The RSI is {(rsi > 70 ? "overbought" : rsi < 30 ? "oversold" : "neutral")} at around {rsi:F1}, indicating {(rsi > 70 ? "potential selling pressure" : rsi < 30 ? "potential buying opportunity" : "no extreme conditions")}.");

        var macdTrend = macd > macdSignal ? "bullish" : "bearish";
        var macdDivergence = Math.Abs(macd - macdSignal);
        reasoning.Add($"The MACD is {macdTrend} {(macdDivergence > 0.1 ? "with strong momentum" : "but has minimal divergence from the signal line")}, suggesting {(macdTrend == "bullish" && macdDivergence > 0.1 ? "strong bullish momentum" : macdTrend == "bearish" && macdDivergence > 0.1 ? "strong bearish momentum" : "a lack of strong momentum")}.");

        var pricePosition = currentPrice > sma20 ? "above" : "below";
        reasoning.Add($"The current price is {pricePosition} the 20-day SMA, suggesting {(pricePosition == "above" ? "short-term bullish sentiment" : "short-term bearish sentiment")}.");

        var atr = (double)indicators["ATR"];
        reasoning.Add($"Given the market volatility reflected by the ATR of {atr:F2}, it's {(result.OverallSignal == SignalType.Hold ? "prudent to hold" : "appropriate to take action")}.");

        return string.Join(" ", reasoning);
    }
}

public class TechnicalAnalysisResult
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double CurrentPrice { get; set; }
    public Dictionary<string, object> Indicators { get; set; } = new();
    public SignalType OverallSignal { get; set; }
    public double SignalStrength { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}
