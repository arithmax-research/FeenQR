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

/// <summary>
/// Represents a comprehensive trading strategy template
/// </summary>
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

/// <summary>
/// Research data collected for strategy generation
/// </summary>
public class ResearchData
{
    public MarketData? MarketData { get; set; }
    public CompanyInfo? CompanyInfo { get; set; }
    public TechnicalAnalysis? TechnicalAnalysis { get; set; }
    public SentimentAnalysis? SentimentAnalysis { get; set; }
    public string WebResearch { get; set; } = string.Empty;
    public decimal VolatilityData { get; set; }
    public KeyLevels? KeyLevels { get; set; }
    public Dictionary<string, object> StructuredWebData { get; set; } = new();
}

/// <summary>
/// Key price levels for technical analysis
/// </summary>
public class KeyLevels
{
    public decimal Support { get; set; }
    public decimal Resistance { get; set; }
}

/// <summary>
/// Company fundamental information
/// </summary>
public class CompanyInfo
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public long MarketCap { get; set; }
    public decimal PERatio { get; set; }
    public decimal DividendYield { get; set; }
    public decimal Beta { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Technical analysis results
/// </summary>
public class TechnicalAnalysis
{
    public string Symbol { get; set; } = string.Empty;
    public Dictionary<string, decimal> Indicators { get; set; } = new();
    public List<decimal> SupportLevels { get; set; } = new();
    public List<decimal> ResistanceLevels { get; set; } = new();
    public string Trend { get; set; } = string.Empty;
    public decimal Momentum { get; set; }
}

/// <summary>
/// Sentiment analysis results
/// </summary>
public class SentimentAnalysis
{
    public string Symbol { get; set; } = string.Empty;
    public decimal OverallSentiment { get; set; } // -1 to 1
    public decimal NewsSentiment { get; set; }
    public decimal SocialSentiment { get; set; }
    public int NewsArticleCount { get; set; }
    public DateTime AnalyzedAt { get; set; }
}

/// <summary>
/// Statistical test result
/// </summary>
public class StatisticalTest
{
    public string TestName { get; set; } = string.Empty;
    public string TestType { get; set; } = string.Empty; // t-test, anova, chi-square, etc.
    public double TestStatistic { get; set; }
    public double PValue { get; set; }
    public double SignificanceLevel { get; set; } = 0.05;
    public bool IsSignificant { get; set; }
    public string NullHypothesis { get; set; } = string.Empty;
    public string AlternativeHypothesis { get; set; } = string.Empty;
    public Dictionary<string, double> Parameters { get; set; } = new();
    public string Interpretation { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Power analysis result
/// </summary>
public class PowerAnalysis
{
    public double EffectSize { get; set; }
    public int SampleSize { get; set; }
    public double Power { get; set; }
    public double SignificanceLevel { get; set; } = 0.05;
    public string TestType { get; set; } = string.Empty;
    public Dictionary<string, double> Parameters { get; set; } = new();
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Time series analysis result
/// </summary>
public class TimeSeriesAnalysis
{
    public string Symbol { get; set; } = string.Empty;
    public bool IsStationary { get; set; }
    public double ADFStatistic { get; set; }
    public double ADFPValue { get; set; }
    public double KPSSStatistic { get; set; }
    public double KPSSPValue { get; set; }
    public List<double> Autocorrelation { get; set; } = new();
    public List<double> PartialAutocorrelation { get; set; } = new();
    public string Trend { get; set; } = string.Empty;
    public string Seasonality { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Cointegration test result
/// </summary>
public class CointegrationTest
{
    public string TestType { get; set; } = string.Empty; // Engle-Granger, Johansen
    public List<string> Symbols { get; set; } = new();
    public double TestStatistic { get; set; }
    public double CriticalValue { get; set; }
    public double PValue { get; set; }
    public bool IsCointegrated { get; set; }
    public List<double> CointegrationVector { get; set; } = new();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Statistical test result with data source and AI interpretation
/// </summary>
public class StatisticalTestResult
{
    public StatisticalTest Test { get; set; } = new();
    public string DataSource { get; set; } = string.Empty;
    public int DataPoints { get; set; }
    public string TimeRange { get; set; } = string.Empty;
    public List<double> Data { get; set; } = new();
    public string AIInterpretation { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
