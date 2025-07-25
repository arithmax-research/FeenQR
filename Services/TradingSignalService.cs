using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using Skender.Stock.Indicators;
using System.Collections.Concurrent;

namespace QuantResearchAgent.Services;

public class TradingSignalService
{
    private readonly ILogger<TradingSignalService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Kernel _kernel;
    private readonly MarketDataService _marketDataService;
    private readonly ConcurrentDictionary<string, List<TradingSignal>> _signalHistory = new();
    private readonly List<string> _trackedSymbols = new() { "BTCUSDT", "ETHUSDT", "AAPL", "MSFT", "GOOGL", "TSLA" };

    public TradingSignalService(
        ILogger<TradingSignalService> logger,
        IConfiguration configuration,
        Kernel kernel,
        MarketDataService marketDataService)
    {
        _logger = logger;
        _configuration = configuration;
        _kernel = kernel;
        _marketDataService = marketDataService;
    }

    public async Task<List<TradingSignal>> GenerateSignalsAsync()
    {
        var signals = new List<TradingSignal>();
        
        foreach (var symbol in _trackedSymbols)
        {
            try
            {
                var signal = await GenerateSignalAsync(symbol);
                if (signal != null)
                {
                    signals.Add(signal);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate signal for symbol: {Symbol}", symbol);
            }
        }
        
        _logger.LogInformation("Generated {SignalCount} trading signals", signals.Count);
        return signals;
    }

    public async Task<TradingSignal?> GenerateSignalAsync(string symbol)
    {
        try
        {
            // Get current market data
            var marketData = await _marketDataService.GetMarketDataAsync(symbol);
            if (marketData == null)
            {
                _logger.LogWarning("No market data available for symbol: {Symbol}", symbol);
                return null;
            }

            // Get historical data for technical analysis
            var historicalData = await _marketDataService.GetHistoricalDataAsync(symbol, 100);
            if (historicalData == null || !historicalData.Any())
            {
                _logger.LogWarning("No historical data available for symbol: {Symbol}", symbol);
                return null;
            }

            // Calculate technical indicators
            var technicalIndicators = CalculateTechnicalIndicators(historicalData);
            
            // Generate signal using AI analysis
            var signal = await GenerateAISignalAsync(symbol, marketData, technicalIndicators);
            
            if (signal != null)
            {
                // Store in signal history
                _signalHistory.AddOrUpdate(symbol, 
                    new List<TradingSignal> { signal },
                    (key, existingSignals) =>
                    {
                        existingSignals.Add(signal);
                        return existingSignals.TakeLast(100).ToList(); // Keep last 100 signals
                    });
                
                _logger.LogInformation("Generated {SignalType} signal for {Symbol} with strength {Strength:F2}", 
                    signal.Type, symbol, signal.Strength);
            }
            
            return signal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate signal for symbol: {Symbol}", symbol);
            return null;
        }
    }

    public async Task<List<TradingSignal>> GenerateSignalsFromPodcastAsync(PodcastEpisode episode)
    {
        var signals = new List<TradingSignal>();
        
        try
        {
            foreach (var signalText in episode.TradingSignals)
            {
                var signal = await ParsePodcastSignalAsync(signalText, episode);
                if (signal != null)
                {
                    signals.Add(signal);
                }
            }
            
            _logger.LogInformation("Generated {SignalCount} signals from podcast episode: {EpisodeName}", 
                signals.Count, episode.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate signals from podcast episode: {EpisodeId}", episode.Id);
        }
        
        return signals;
    }

    private Dictionary<string, double> CalculateTechnicalIndicators(IEnumerable<MarketData> historicalData)
    {
        var indicators = new Dictionary<string, double>();
        
        try
        {
            // Convert to quotes for technical analysis
            var quotes = historicalData.Select(d => new Quote
            {
                Timestamp = d.Timestamp,
                Open = (decimal)d.Price, // Simplified - in real implementation you'd have OHLC data
                High = (decimal)d.High24h,
                Low = (decimal)d.Low24h,
                Close = (decimal)d.Price,
                Volume = (decimal)d.Volume
            }).OrderBy(q => q.Timestamp).ToList();

            if (quotes.Count < 20)
            {
                _logger.LogWarning("Insufficient data for technical indicators calculation");
                return indicators;
            }

            // Calculate RSI
            var rsiResults = quotes.ToRsi(14).LastOrDefault();
            if (rsiResults != null)
                indicators["RSI"] = (double)(rsiResults.Rsi ?? 50);

            // Calculate MACD
            var macdResults = quotes.ToMacd(12, 26, 9).LastOrDefault();
            if (macdResults != null)
            {
                indicators["MACD"] = (double)(macdResults.Macd ?? 0);
                indicators["MACD_Signal"] = (double)(macdResults.Signal ?? 0);
                indicators["MACD_Histogram"] = (double)(macdResults.Histogram ?? 0);
            }

            // Calculate SMA
            var sma20Results = quotes.ToSma(20).LastOrDefault();
            if (sma20Results != null)
                indicators["SMA_20"] = (double)(sma20Results.Sma ?? 0);

            var sma50Results = quotes.ToSma(50).LastOrDefault();
            if (sma50Results != null)
                indicators["SMA_50"] = (double)(sma50Results.Sma ?? 0);

            // Calculate Bollinger Bands
            var bbResults = quotes.ToBollingerBands(20, 2).LastOrDefault();
            if (bbResults != null)
            {
                indicators["BB_Upper"] = (double)(bbResults.UpperBand ?? 0);
                indicators["BB_Middle"] = (double)(bbResults.Sma ?? 0);
                indicators["BB_Lower"] = (double)(bbResults.LowerBand ?? 0);
            }

            // Calculate ATR for volatility
            var atrResults = quotes.ToAtr(14).LastOrDefault();
            if (atrResults != null)
                indicators["ATR"] = (double)(atrResults.Atr ?? 0);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate technical indicators");
        }
        
        return indicators;
    }

    private async Task<TradingSignal?> GenerateAISignalAsync(
        string symbol, 
        MarketData currentData, 
        Dictionary<string, double> indicators)
    {
        var indicatorsText = string.Join("\n", indicators.Select(kvp => $"{kvp.Key}: {kvp.Value:F4}"));
        
        var prompt = $@"
You are a quantitative trading expert analyzing market data to generate trading signals.

Symbol: {symbol}
Current Price: ${currentData.Price:F2}
24h Change: {currentData.ChangePercent24h:F2}%
Volume: {currentData.Volume:F0}
High 24h: ${currentData.High24h:F2}
Low 24h: ${currentData.Low24h:F2}

Technical Indicators:
{indicatorsText}

Based on this data, provide a trading signal in the following JSON format:
{{
  ""signal_type"": ""BUY"" | ""SELL"" | ""HOLD"" | ""STRONG_BUY"" | ""STRONG_SELL"",
  ""strength"": 0.0-1.0,
  ""reasoning"": ""Brief explanation of the signal"",
  ""stop_loss_percent"": 0.0-0.20,
  ""take_profit_percent"": 0.0-0.50,
  ""duration_hours"": 1-720
}}

Consider:
1. Technical indicator convergence/divergence
2. Price momentum and trend
3. Volume analysis
4. Risk/reward ratio
5. Market volatility

Only provide signals with confidence > 0.3. Be conservative with risk management.
";

        try
        {
            var function = _kernel.CreateFunctionFromPrompt(prompt);
            var result = await _kernel.InvokeAsync(function);
            
            var signal = ParseAISignalResponse(result.ToString(), symbol, currentData);
            return signal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate AI signal for symbol: {Symbol}", symbol);
            return null;
        }
    }

    private TradingSignal? ParseAISignalResponse(string response, string symbol, MarketData currentData)
    {
        try
        {
            // Simple parsing - in a real implementation, you'd use proper JSON parsing
            var lines = response.Split('\n');
            var signal = new TradingSignal
            {
                Symbol = symbol,
                Price = currentData.Price,
                GeneratedAt = DateTime.UtcNow,
                Source = "technical_analysis"
            };

            foreach (var line in lines)
            {
                if (line.Contains("signal_type") && line.Contains("BUY"))
                {
                    if (line.Contains("STRONG_BUY"))
                        signal.Type = SignalType.StrongBuy;
                    else
                        signal.Type = SignalType.Buy;
                }
                else if (line.Contains("signal_type") && line.Contains("SELL"))
                {
                    if (line.Contains("STRONG_SELL"))
                        signal.Type = SignalType.StrongSell;
                    else
                        signal.Type = SignalType.Sell;
                }
                else if (line.Contains("signal_type") && line.Contains("HOLD"))
                {
                    signal.Type = SignalType.Hold;
                }
                else if (line.Contains("strength"))
                {
                    var strengthMatch = System.Text.RegularExpressions.Regex.Match(line, @"(\d+\.?\d*)");
                    if (strengthMatch.Success && double.TryParse(strengthMatch.Value, out var strength))
                    {
                        signal.Strength = Math.Max(0.0, Math.Min(1.0, strength));
                    }
                }
                else if (line.Contains("reasoning"))
                {
                    var reasoningStart = line.IndexOf('"') + 1;
                    var reasoningEnd = line.LastIndexOf('"');
                    if (reasoningStart > 0 && reasoningEnd > reasoningStart)
                    {
                        signal.Reasoning = line.Substring(reasoningStart, reasoningEnd - reasoningStart);
                    }
                }
            }

            // Only return signals with reasonable strength
            if (signal.Strength < 0.3)
            {
                return null;
            }

            // Set stop loss and take profit
            signal.StopLoss = signal.Price * (signal.Type == SignalType.Buy || signal.Type == SignalType.StrongBuy ? 0.95 : 1.05);
            signal.TakeProfit = signal.Price * (signal.Type == SignalType.Buy || signal.Type == SignalType.StrongBuy ? 1.10 : 0.90);

            return signal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse AI signal response");
            return null;
        }
    }

    private async Task<TradingSignal?> ParsePodcastSignalAsync(string signalText, PodcastEpisode episode)
    {
        try
        {
            // Extract signal information from podcast-generated text
            var lines = signalText.Split('\n');
            var signal = new TradingSignal
            {
                GeneratedAt = DateTime.UtcNow,
                Source = $"podcast_{episode.Id}"
            };

            foreach (var line in lines)
            {
                if (line.Contains("Symbol:"))
                {
                    signal.Symbol = ExtractValue(line, "Symbol:");
                }
                else if (line.Contains("Action:"))
                {
                    var action = ExtractValue(line, "Action:");
                    signal.Type = action.ToUpper() switch
                    {
                        "BUY" => SignalType.Buy,
                        "SELL" => SignalType.Sell,
                        "HOLD" => SignalType.Hold,
                        _ => SignalType.Hold
                    };
                }
                else if (line.Contains("Strength:"))
                {
                    var strengthText = ExtractValue(line, "Strength:");
                    if (double.TryParse(strengthText, out var strength))
                    {
                        signal.Strength = Math.Max(0.0, Math.Min(1.0, strength));
                    }
                }
                else if (line.Contains("Reasoning:"))
                {
                    signal.Reasoning = ExtractValue(line, "Reasoning:");
                }
            }

            if (!string.IsNullOrEmpty(signal.Symbol) && signal.Strength > 0.3)
            {
                // Get current price for the symbol
                var marketData = await _marketDataService.GetMarketDataAsync(signal.Symbol);
                if (marketData != null)
                {
                    signal.Price = marketData.Price;
                }

                return signal;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse podcast signal");
            return null;
        }
    }

    private string ExtractValue(string line, string prefix)
    {
        var startIndex = line.IndexOf(prefix) + prefix.Length;
        if (startIndex < prefix.Length) return string.Empty;
        
        return line.Substring(startIndex).Trim();
    }

    public List<TradingSignal> GetSignalHistory(string symbol)
    {
        return _signalHistory.GetValueOrDefault(symbol, new List<TradingSignal>());
    }

    public List<TradingSignal> GetActiveSignals()
    {
        var activeSignals = new List<TradingSignal>();
        
        foreach (var symbolSignals in _signalHistory.Values)
        {
            activeSignals.AddRange(symbolSignals.Where(s => s.Status == SignalStatus.Active));
        }
        
        return activeSignals.OrderByDescending(s => s.GeneratedAt).ToList();
    }
}
