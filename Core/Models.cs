namespace QuantResearchAgent.Core;

/// <summary>
/// Represents a podcast episode with analysis results
/// </summary>
public class PodcastEpisode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Transcript { get; set; }
    public DateTime PublishedDate { get; set; }
    public string PodcastUrl { get; set; } = string.Empty;
    public List<string> TechnicalInsights { get; set; } = new();
    public List<string> TradingSignals { get; set; } = new();
    public double SentimentScore { get; set; }
    public DateTime AnalyzedAt { get; set; }
}

/// <summary>
/// Represents a trading signal with metadata
/// </summary>
public class TradingSignal
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Symbol { get; set; } = string.Empty;
    public SignalType Type { get; set; }
    public double Strength { get; set; } // 0.0 to 1.0
    public double Price { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string Source { get; set; } = string.Empty; // podcast, technical_analysis, etc.
    public string Reasoning { get; set; } = string.Empty;
    public double? StopLoss { get; set; }
    public double? TakeProfit { get; set; }
    public TimeSpan? Duration { get; set; }
    public SignalStatus Status { get; set; } = SignalStatus.Active;
}

public enum SignalType
{
    Buy,
    Sell,
    Hold,
    StrongBuy,
    StrongSell
}

public enum SignalStatus
{
    Active,
    Executed,
    Expired,
    Cancelled
}

/// <summary>
/// Represents market data for a symbol
/// </summary>
public class MarketData
{
    public string Symbol { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // e.g. "local", "yahoo", "binance", etc.
    public double Price { get; set; }
    public double Volume { get; set; }
    public double Change24h { get; set; }
    public double ChangePercent24h { get; set; }
    public double High24h { get; set; }
    public double Low24h { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, double> TechnicalIndicators { get; set; } = new();
}

/// <summary>
/// Represents a portfolio position
/// </summary>
public class Position
{
    public string Symbol { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double AveragePrice { get; set; }
    public double CurrentPrice { get; set; }
    public double UnrealizedPnL => (CurrentPrice - AveragePrice) * Quantity;
    public double UnrealizedPnLPercent => ((CurrentPrice - AveragePrice) / AveragePrice) * 100;
    public DateTime OpenedAt { get; set; }
    public string? StopLoss { get; set; }
    public string? TakeProfit { get; set; }
}

/// <summary>
/// Represents portfolio performance metrics
/// </summary>
public class PortfolioMetrics
{
    public double TotalValue { get; set; }
    public double TotalPnL { get; set; }
    public double TotalPnLPercent { get; set; }
    public double DailyReturn { get; set; }
    public double Volatility { get; set; }
    public double SharpeRatio { get; set; }
    public double MaxDrawdown { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public double WinRate => WinningTrades + LosingTrades > 0 ? (double)WinningTrades / (WinningTrades + LosingTrades) : 0;
    public DateTime CalculatedAt { get; set; }
}

/// <summary>
/// Represents an agent job/task
/// </summary>
public class AgentJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty; // podcast_analysis, signal_generation, etc.
    public Dictionary<string, object> Parameters { get; set; } = new();
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public int Priority { get; set; } = 1; // 1 = highest, 10 = lowest
}

public enum JobStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Configuration for risk management
/// </summary>
public class RiskManagementConfig
{
    public double MaxDrawdown { get; set; } = 0.15;
    public double VolatilityTarget { get; set; } = 0.12;
    public int MaxPositions { get; set; } = 10;
    public double PositionSizePercent { get; set; } = 0.1;
    public double StopLossPercent { get; set; } = 0.05;
    public double TakeProfitPercent { get; set; } = 0.10;
}
