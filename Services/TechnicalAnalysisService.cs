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
    private readonly LLMRouterService _llmService;
    private readonly ConcurrentDictionary<string, TechnicalAnalysisResult> _analysisCache = new();

    public TechnicalAnalysisService(ILogger<TechnicalAnalysisService> logger, AlpacaService alpacaService, LLMRouterService llmService)
    {
        _logger = logger;
        _alpacaService = alpacaService;
        _llmService = llmService;
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
            result.Reasoning = await GenerateReasoningAsync(result);

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

        // Stochastic RSI
        var stochRsi = quotes.ToStochRsi(14, 14, 3, 3).LastOrDefault();
        result.Indicators["StochRSI_K"] = stochRsi?.StochRsi ?? 0;
        result.Indicators["StochRSI_D"] = stochRsi?.StochRsi ?? 0;

        // Connors RSI
        var connorsRsi = quotes.ToConnorsRsi(3, 2, 100).LastOrDefault()?.ConnorsRsi;
        result.Indicators["ConnorsRSI"] = connorsRsi ?? 0;

        // 52 Week High/Low
        var last252Days = quotes.TakeLast(252).ToList();
        if (last252Days.Any())
        {
            result.Indicators["52Week_High"] = (double)last252Days.Max(q => q.High);
            result.Indicators["52Week_Low"] = (double)last252Days.Min(q => q.Low);
        }

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

        // SuperTrend
        var superTrend = quotes.ToSuperTrend(10, 3).LastOrDefault();
        result.Indicators["SuperTrend"] = superTrend?.SuperTrend ?? 0;
        result.Indicators["SuperTrend_Signal"] = superTrend?.UpperBand != null ? "BUY" : "SELL";

        // Choppiness Index
        var chop = quotes.ToChop(14).LastOrDefault()?.Chop;
        result.Indicators["Choppiness_Index"] = chop ?? 0;

        // Historical Volatility (simplified)
        var volatilityPeriod = Math.Min(30, quotes.Count);
        if (volatilityPeriod > 1)
        {
            var recentQuotes = quotes.TakeLast(volatilityPeriod).ToList();
            var returns = recentQuotes.Select((q, i) => 
                i > 0 ? Math.Log((double)q.Close / (double)recentQuotes[i - 1].Close) : 0
            ).Skip(1).ToList();
            
            if (returns.Any())
            {
                var volatility = returns.StandardDeviation() * Math.Sqrt(252);
                result.Indicators["Historical_Volatility"] = volatility;
            }
        }

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

    private async Task PerformPatternRecognition(List<Quote> quotes, TechnicalAnalysisResult result)
    {
        var patterns = new List<string>();

        // === TREND PATTERNS ===
        DetectTrendPatterns(quotes, result, patterns);
        
        // === CANDLESTICK PATTERNS ===
        DetectCandlestickPatterns(quotes, patterns);
        
        // === CHART PATTERNS ===
        DetectChartPatterns(quotes, result, patterns);
        
        // === DIVERGENCE PATTERNS ===
        DetectDivergences(quotes, result, patterns);
        
        // === VOLUME PATTERNS ===
        DetectVolumePatterns(quotes, patterns);
        
        // === MOMENTUM PATTERNS ===
        DetectMomentumPatterns(result, patterns);
        
        // === VOLATILITY PATTERNS ===
        DetectVolatilityPatterns(result, patterns);

        result.Indicators["Patterns"] = patterns;
        
        // Generate AI explanations for detected patterns
        var patternDetails = await GeneratePatternExplanations(result.Symbol, patterns, result);
        result.Indicators["PatternDetails"] = patternDetails;
    }

    private void DetectTrendPatterns(List<Quote> quotes, TechnicalAnalysisResult result, List<string> patterns)
    {
        var sma20 = (double)result.Indicators["SMA_20"];
        var sma50 = (double)result.Indicators["SMA_50"];
        var sma200 = (double)result.Indicators["SMA_200"];
        var currentPrice = result.CurrentPrice;
        
        // Golden Cross / Death Cross
        if (sma50 > sma200)
            patterns.Add("Golden Cross");
        else if (sma50 < sma200)
            patterns.Add("Death Cross");
        
        // Moving Average Alignment
        if (currentPrice > sma20 && sma20 > sma50 && sma50 > sma200)
            patterns.Add("Perfect Bull Alignment");
        else if (currentPrice < sma20 && sma20 < sma50 && sma50 < sma200)
            patterns.Add("Perfect Bear Alignment");
        
        // Price Position Relative to SMAs
        if (currentPrice > sma200)
            patterns.Add("Above 200 SMA");
        else
            patterns.Add("Below 200 SMA");
        
        // ADX Trend Strength
        var adx = (double)result.Indicators["ADX"];
        if (adx > 25)
            patterns.Add($"Strong Trend (ADX {adx:F1})");
        else if (adx < 20)
            patterns.Add($"Weak Trend (ADX {adx:F1})");
    }

    private void DetectCandlestickPatterns(List<Quote> quotes, List<string> patterns)
    {
        if (quotes.Count < 3) return;
        
        var current = quotes[^1];
        var prev1 = quotes[^2];
        var prev2 = quotes[^3];
        
        var currentBody = Math.Abs((double)(current.Close - current.Open));
        var currentRange = (double)(current.High - current.Low);
        var prev1Body = Math.Abs((double)(prev1.Close - prev1.Open));
        
        // Doji (indecision)
        if (currentBody < currentRange * 0.1)
            patterns.Add("Doji");
        
        // Hammer / Hanging Man
        var lowerWick = (double)(Math.Min(current.Open, current.Close) - current.Low);
        var upperWick = (double)(current.High - Math.Max(current.Open, current.Close));
        if (lowerWick > currentBody * 2 && upperWick < currentBody * 0.3)
        {
            if (current.Close > current.Open)
                patterns.Add("Hammer");
            else
                patterns.Add("Hanging Man");
        }
        
        // Shooting Star / Inverted Hammer
        if (upperWick > currentBody * 2 && lowerWick < currentBody * 0.3)
        {
            if (current.Close < current.Open)
                patterns.Add("Shooting Star");
            else
                patterns.Add("Inverted Hammer");
        }
        
        // Engulfing Patterns
        if (current.Close > current.Open && prev1.Close < prev1.Open &&
            current.Open < prev1.Close && current.Close > prev1.Open)
            patterns.Add("Bullish Engulfing");
        
        if (current.Close < current.Open && prev1.Close > prev1.Open &&
            current.Open > prev1.Close && current.Close < prev1.Open)
            patterns.Add("Bearish Engulfing");
        
        // Three White Soldiers / Three Black Crows
        if (current.Close > current.Open && prev1.Close > prev1.Open && prev2.Close > prev2.Open &&
            current.Close > prev1.Close && prev1.Close > prev2.Close)
            patterns.Add("Three White Soldiers");
        
        if (current.Close < current.Open && prev1.Close < prev1.Open && prev2.Close < prev2.Open &&
            current.Close < prev1.Close && prev1.Close < prev2.Close)
            patterns.Add("Three Black Crows");
        
        // Morning Star / Evening Star
        if (quotes.Count >= 3)
        {
            var prev2Body = Math.Abs((double)(prev2.Close - prev2.Open));
            if (prev2.Close < prev2.Open && prev1Body < prev2Body * 0.3 && 
                current.Close > current.Open && current.Close > (prev2.Open + prev2.Close) / 2)
                patterns.Add("Morning Star");
            
            if (prev2.Close > prev2.Open && prev1Body < prev2Body * 0.3 && 
                current.Close < current.Open && current.Close < (prev2.Open + prev2.Close) / 2)
                patterns.Add("Evening Star");
        }
    }

    private void DetectChartPatterns(List<Quote> quotes, TechnicalAnalysisResult result, List<string> patterns)
    {
        if (quotes.Count < 20) return;
        
        var recent = quotes.TakeLast(20).ToList();
        var highs = recent.Select(q => (double)q.High).ToArray();
        var lows = recent.Select(q => (double)q.Low).ToArray();
        var closes = recent.Select(q => (double)q.Close).ToArray();
        
        var currentPrice = result.CurrentPrice;
        var high20 = highs.Max();
        var low20 = lows.Min();
        var range = high20 - low20;
        
        // Breakout Patterns
        if (currentPrice >= high20 * 0.999)
            patterns.Add("Breakout Above 20-Day High");
        if (currentPrice <= low20 * 1.001)
            patterns.Add("Breakdown Below 20-Day Low");
        
        // Support/Resistance Test
        var support = result.Indicators.ContainsKey("Support_1") ? (double)result.Indicators["Support_1"] : low20;
        var resistance = result.Indicators.ContainsKey("Resistance_1") ? (double)result.Indicators["Resistance_1"] : high20;
        
        if (Math.Abs(currentPrice - resistance) / currentPrice < 0.01)
            patterns.Add("Testing Resistance");
        if (Math.Abs(currentPrice - support) / currentPrice < 0.01)
            patterns.Add("Testing Support");
        
        // Range Contraction
        var avgRange = highs.Zip(lows, (h, l) => h - l).Average();
        var currentRange = (double)(quotes[^1].High - quotes[^1].Low);
        if (currentRange < avgRange * 0.5)
            patterns.Add("Range Contraction");
        
        // Higher Highs / Lower Lows
        if (quotes.Count >= 5)
        {
            var last5Highs = quotes.TakeLast(5).Select(q => (double)q.High).ToList();
            var last5Lows = quotes.TakeLast(5).Select(q => (double)q.Low).ToList();
            
            bool higherHighs = true;
            bool higherLows = true;
            bool lowerHighs = true;
            bool lowerLows = true;
            
            for (int i = 1; i < 5; i++)
            {
                if (last5Highs[i] <= last5Highs[i-1]) higherHighs = false;
                if (last5Lows[i] <= last5Lows[i-1]) higherLows = false;
                if (last5Highs[i] >= last5Highs[i-1]) lowerHighs = false;
                if (last5Lows[i] >= last5Lows[i-1]) lowerLows = false;
            }
            
            if (higherHighs && higherLows)
                patterns.Add("Ascending Pattern");
            if (lowerHighs && lowerLows)
                patterns.Add("Descending Pattern");
            if (higherLows && lowerHighs)
                patterns.Add("Symmetrical Triangle");
        }
        
        // Gap Detection
        if (quotes.Count >= 2)
        {
            var prevHigh = (double)quotes[^2].High;
            var prevLow = (double)quotes[^2].Low;
            var currOpen = (double)quotes[^1].Open;
            
            if (currOpen > prevHigh)
                patterns.Add("Gap Up");
            if (currOpen < prevLow)
                patterns.Add("Gap Down");
        }
    }

    private void DetectDivergences(List<Quote> quotes, TechnicalAnalysisResult result, List<string> patterns)
    {
        if (quotes.Count < 14) return;
        
        var rsi = (double)result.Indicators["RSI"];
        var macd = (double)result.Indicators["MACD"];
        var macdSignal = (double)result.Indicators["MACD_Signal"];
        
        // MACD Signal Crosses
        if (macd > macdSignal)
            patterns.Add("MACD Bullish Cross");
        else if (macd < macdSignal)
            patterns.Add("MACD Bearish Cross");
        
        // MACD Histogram Momentum
        var macdHist = (double)result.Indicators["MACD_Histogram"];
        if (Math.Abs(macdHist) > 0.5)
        {
            if (macdHist > 0)
                patterns.Add("Strong Bullish MACD Momentum");
            else
                patterns.Add("Strong Bearish MACD Momentum");
        }
        
        // Stochastic Signals
        if (result.Indicators.ContainsKey("Stoch_%K"))
        {
            var stochK = (double)result.Indicators["Stoch_%K"];
            var stochD = (double)result.Indicators["Stoch_%D"];
            
            if (stochK > 80 && stochD > 80)
                patterns.Add("Stochastic Overbought");
            else if (stochK < 20 && stochD < 20)
                patterns.Add("Stochastic Oversold");
            
            if (stochK > stochD && stochK < 20)
                patterns.Add("Stochastic Bullish Cross in Oversold");
            if (stochK < stochD && stochK > 80)
                patterns.Add("Stochastic Bearish Cross in Overbought");
        }
    }

    private void DetectVolumePatterns(List<Quote> quotes, List<string> patterns)
    {
        if (quotes.Count < 20) return;
        
        var volumes = quotes.TakeLast(20).Select(q => (double)q.Volume).ToArray();
        var avgVolume = volumes.Average();
        var currentVolume = (double)quotes[^1].Volume;
        
        // Volume Spike
        if (currentVolume > avgVolume * 2)
            patterns.Add("High Volume Spike");
        else if (currentVolume > avgVolume * 1.5)
            patterns.Add("Above Average Volume");
        else if (currentVolume < avgVolume * 0.5)
            patterns.Add("Low Volume");
        
        // Volume Trend
        var recentVolume = volumes.TakeLast(5).Average();
        var olderVolume = volumes.Take(5).Average();
        if (recentVolume > olderVolume * 1.3)
            patterns.Add("Increasing Volume Trend");
        else if (recentVolume < olderVolume * 0.7)
            patterns.Add("Decreasing Volume Trend");
        
        // Volume + Price Confirmation
        var currentClose = (double)quotes[^1].Close;
        var prevClose = (double)quotes[^2].Close;
        if (currentClose > prevClose && currentVolume > avgVolume * 1.5)
            patterns.Add("Bullish Volume Confirmation");
        if (currentClose < prevClose && currentVolume > avgVolume * 1.5)
            patterns.Add("Bearish Volume Confirmation");
    }

    private void DetectMomentumPatterns(TechnicalAnalysisResult result, List<string> patterns)
    {
        var rsi = (double)result.Indicators["RSI"];
        
        // RSI Zones
        if (rsi > 70)
            patterns.Add($"RSI Overbought ({rsi:F1})");
        else if (rsi > 60)
            patterns.Add($"RSI Strong Zone ({rsi:F1})");
        else if (rsi < 30)
            patterns.Add($"RSI Oversold ({rsi:F1})");
        else if (rsi < 40)
            patterns.Add($"RSI Weak Zone ({rsi:F1})");
        else
            patterns.Add($"RSI Neutral ({rsi:F1})");
        
        // CCI Extremes
        if (result.Indicators.ContainsKey("CCI"))
        {
            var cci = (double)result.Indicators["CCI"];
            if (cci > 100)
                patterns.Add($"CCI Overbought ({cci:F1})");
            else if (cci < -100)
                patterns.Add($"CCI Oversold ({cci:F1})");
        }
        
        // Williams %R
        if (result.Indicators.ContainsKey("Williams_%R"))
        {
            var williamsR = (double)result.Indicators["Williams_%R"];
            if (williamsR > -20)
                patterns.Add("Williams %R Overbought");
            else if (williamsR < -80)
                patterns.Add("Williams %R Oversold");
        }
    }

    private void DetectVolatilityPatterns(TechnicalAnalysisResult result, List<string> patterns)
    {
        var bbWidth = (double)result.Indicators["BB_Width"];
        var atr = (double)result.Indicators["ATR"];
        var currentPrice = result.CurrentPrice;
        
        // Bollinger Band Squeeze
        if (bbWidth < atr * 0.5)
            patterns.Add("Bollinger Squeeze");
        else if (bbWidth > atr * 1.5)
            patterns.Add("Bollinger Expansion");
        
        // Bollinger Band Position
        var bbUpper = (double)result.Indicators["BB_Upper"];
        var bbLower = (double)result.Indicators["BB_Lower"];
        var bbMiddle = (double)result.Indicators["BB_Middle"];
        
        if (currentPrice > bbUpper)
            patterns.Add("Price Above Upper Bollinger Band");
        else if (currentPrice < bbLower)
            patterns.Add("Price Below Lower Bollinger Band");
        else if (currentPrice > bbMiddle)
            patterns.Add("Price in Upper BB Half");
        else
            patterns.Add("Price in Lower BB Half");
        
        // Bollinger %B
        var bbPercentB = result.Indicators.ContainsKey("Bollinger_%B") ? 
            (double)result.Indicators["Bollinger_%B"] : 0;
        if (bbPercentB > 1)
            patterns.Add("Bollinger %B Overextended High");
        else if (bbPercentB < 0)
            patterns.Add("Bollinger %B Overextended Low");
        
        // ATR Volatility Level
        var atrPercent = (atr / currentPrice) * 100;
        if (atrPercent > 5)
            patterns.Add($"Very High Volatility (ATR {atrPercent:F1}%)");
        else if (atrPercent > 3)
            patterns.Add($"High Volatility (ATR {atrPercent:F1}%)");
        else if (atrPercent < 1.5)
            patterns.Add($"Low Volatility (ATR {atrPercent:F1}%)");
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
        // Guard against division by zero / NaN when macdSignal is zero
        var macdStrength = Math.Abs(macdSignal) > 1e-10
            ? Math.Min(Math.Abs(macd - macdSignal) / Math.Abs(macdSignal), 1.0)
            : (macd != 0 ? 1.0 : 0.0);
        strengthFactors.Add(double.IsNaN(macdStrength) || double.IsInfinity(macdStrength) ? 0.0 : macdStrength);

        // Volume strength (simplified)
        var mfi = (double)indicators["MFI"];
        strengthFactors.Add(Math.Abs(mfi - 50) / 50.0);

        var avg = strengthFactors.Any() ? strengthFactors.Average() : 0.0;
        // Ensure we never return NaN/Infinity which would break JSON serialization
        return double.IsNaN(avg) || double.IsInfinity(avg) ? 0.0 : avg;
    }

    private async Task<string> GenerateReasoningAsync(TechnicalAnalysisResult result)
    {
        var indicators = result.Indicators;
        
        var prompt = $@"Analyze the following technical indicators for {result.Symbol} and provide a concise professional analysis (2-3 sentences):

Current Price: ${result.CurrentPrice:F2}
RSI: {indicators.GetValueOrDefault("RSI", 0):F2}
MACD: {indicators.GetValueOrDefault("MACD", 0):F4}
MACD Signal: {indicators.GetValueOrDefault("MACD_Signal", 0):F4}
SMA 20: {indicators.GetValueOrDefault("SMA_20", 0):F2}
SMA 50: {indicators.GetValueOrDefault("SMA_50", 0):F2}
ATR: {indicators.GetValueOrDefault("ATR", 0):F2}
Bollinger Upper: {indicators.GetValueOrDefault("Bollinger_Upper", 0):F2}
Bollinger Lower: {indicators.GetValueOrDefault("Bollinger_Lower", 0):F2}
Overall Signal: {result.OverallSignal}

Provide technical analysis focusing on momentum, trend, and volatility. Be specific about what the indicators suggest.";

        try
        {
            var analysis = await _llmService.GetChatCompletionAsync(prompt, "openai");
            return analysis?.Trim() ?? "Unable to generate analysis at this time.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating LLM analysis for {Symbol}", result.Symbol);
            return $"Technical analysis for {result.Symbol}: RSI at {indicators.GetValueOrDefault("RSI", 0):F1}, MACD trend is {(((double)indicators.GetValueOrDefault("MACD", 0) > (double)indicators.GetValueOrDefault("MACD_Signal", 0)) ? "bullish" : "bearish")}, overall signal is {result.OverallSignal}.";
        }
    }

    // Wrapper method for compatibility
    public async Task<Dictionary<string, object>> GetTechnicalIndicatorsAsync(string symbol)
    {
        var result = await PerformFullAnalysisAsync(symbol);
        return result.Indicators;
    }

    private async Task<List<PatternDetection>> GeneratePatternExplanations(string symbol, List<string> patterns, TechnicalAnalysisResult result)
    {
        var patternDetails = new List<PatternDetection>();
        
        if (!patterns.Any())
            return patternDetails;
        
        try
        {
            var indicators = result.Indicators;
            var prompt = $@"You are a technical analysis expert. Explain why each of the following patterns was detected for {symbol} at ${result.CurrentPrice:F2}.

For each pattern, provide a clear, concise explanation (2-3 sentences) that:
1. Explains what the pattern means
2. States why it was detected based on the technical indicators
3. Describes the potential trading implications

Technical Indicators:
- RSI: {(indicators.ContainsKey("RSI") ? $"{(double)indicators["RSI"]:F1}" : "N/A")}
- MACD: {(indicators.ContainsKey("MACD") ? $"{(double)indicators["MACD"]:F4}" : "N/A")}
- MACD Signal: {(indicators.ContainsKey("MACD_Signal") ? $"{(double)indicators["MACD_Signal"]:F4}" : "N/A")}
- SMA 20: {(indicators.ContainsKey("SMA_20") ? $"${(double)indicators["SMA_20"]:F2}" : "N/A")}
- SMA 50: {(indicators.ContainsKey("SMA_50") ? $"${(double)indicators["SMA_50"]:F2}" : "N/A")}
- SMA 200: {(indicators.ContainsKey("SMA_200") ? $"${(double)indicators["SMA_200"]:F2}" : "N/A")}
- ADX: {(indicators.ContainsKey("ADX") ? $"{(double)indicators["ADX"]:F1}" : "N/A")}
- BB Upper: {(indicators.ContainsKey("BB_Upper") ? $"${(double)indicators["BB_Upper"]:F2}" : "N/A")}
- BB Lower: {(indicators.ContainsKey("BB_Lower") ? $"${(double)indicators["BB_Lower"]:F2}" : "N/A")}
- ATR: {(indicators.ContainsKey("ATR") ? $"{(double)indicators["ATR"]:F2}" : "N/A")}

Detected Patterns:
{string.Join("\n", patterns.Select((p, i) => $"{i + 1}. {p}"))}

Provide your response in this exact format:

PATTERN: [Pattern Name]
EXPLANATION: [Your explanation]

(Repeat for each pattern)";

            var response = await _llmService.GetChatCompletionAsync(prompt, "openai");
            
            // Parse the response
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string currentPattern = string.Empty;
            string currentExplanation = string.Empty;
            
            foreach (var line in lines)
            {
                if (line.StartsWith("PATTERN:"))
                {
                    if (!string.IsNullOrEmpty(currentPattern) && !string.IsNullOrEmpty(currentExplanation))
                    {
                        patternDetails.Add(new PatternDetection
                        {
                            PatternName = currentPattern.Trim(),
                            Explanation = currentExplanation.Trim()
                        });
                    }
                    currentPattern = line.Replace("PATTERN:", "").Trim();
                    currentExplanation = string.Empty;
                }
                else if (line.StartsWith("EXPLANATION:"))
                {
                    currentExplanation = line.Replace("EXPLANATION:", "").Trim();
                }
                else if (!string.IsNullOrEmpty(currentPattern))
                {
                    // Continuation of explanation
                    currentExplanation += " " + line.Trim();
                }
            }
            
            // Add the last pattern
            if (!string.IsNullOrEmpty(currentPattern) && !string.IsNullOrEmpty(currentExplanation))
            {
                patternDetails.Add(new PatternDetection
                {
                    PatternName = currentPattern.Trim(),
                    Explanation = currentExplanation.Trim()
                });
            }
            
            // If parsing failed, create basic explanations
            if (!patternDetails.Any())
            {
                foreach (var pattern in patterns)
                {
                    patternDetails.Add(new PatternDetection
                    {
                        PatternName = pattern,
                        Explanation = "This pattern was detected based on the current technical indicators and price action."
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating pattern explanations for {Symbol}", symbol);
            // Return basic pattern info without explanations
            foreach (var pattern in patterns)
            {
                patternDetails.Add(new PatternDetection
                {
                    PatternName = pattern,
                    Explanation = "Pattern detected based on technical analysis criteria."
                });
            }
        }
        
        return patternDetails;
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

public class PatternDetection
{
    public string PatternName { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}
