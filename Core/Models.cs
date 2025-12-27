using MathNet.Numerics.LinearAlgebra;

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
    public double Close { get; set; } // Closing price
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

/// <summary>
/// Stationarity test result (ADF, KPSS, Phillips-Perron)
/// </summary>
public class StationarityTestResult
{
    public string TestType { get; set; } = string.Empty; // "ADF", "KPSS", "Phillips-Perron"
    public double TestStatistic { get; set; }
    public Dictionary<double, double> CriticalValues { get; set; } = new();
    public bool IsStationary { get; set; }
    public int LagOrder { get; set; }
    public double SignificanceLevel { get; set; } = 0.05;
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Autocorrelation analysis result
/// </summary>
public class AutocorrelationResult
{
    public List<double> Autocorrelations { get; set; } = new();
    public List<double> PartialAutocorrelations { get; set; } = new();
    public int MaxLags { get; set; }
    public double LjungBoxStatistic { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Seasonal decomposition result
/// </summary>
public class SeasonalDecompositionResult
{
    public double[] OriginalData { get; set; } = Array.Empty<double>();
    public double[] Trend { get; set; } = Array.Empty<double>();
    public double[] Seasonal { get; set; } = Array.Empty<double>();
    public double[] Residual { get; set; } = Array.Empty<double>();
    public int SeasonalPeriod { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Engle-Granger cointegration test result
/// </summary>
public class EngleGrangerResult
{
    public List<string> Symbols { get; set; } = new();
    public double TestStatistic { get; set; }
    public Dictionary<double, double> CriticalValues { get; set; } = new();
    public bool IsCointegrated { get; set; }
    public double[] CointegrationVector { get; set; } = Array.Empty<double>();
    public double ResidualVariance { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Johansen cointegration test result
/// </summary>
public class JohansenResult
{
    public List<string> Symbols { get; set; } = new();
    public int Rank { get; set; } // Number of cointegrating relationships
    public double[] Eigenvalues { get; set; } = Array.Empty<double>();
    public double[] TraceStatistics { get; set; } = Array.Empty<double>();
    public double[] MaxEigenvalueStatistics { get; set; } = Array.Empty<double>();
    public Dictionary<double, double[]> CriticalValues { get; set; } = new();
    public double[][] CointegrationVectors { get; set; } = Array.Empty<double[]>();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Granger causality test result
/// </summary>
public class GrangerCausalityResult
{
    public string CauseSymbol { get; set; } = string.Empty;
    public string EffectSymbol { get; set; } = string.Empty;
    public int LagOrder { get; set; }
    public double FStatistic { get; set; }
    public double PValue { get; set; }
    public bool GrangerCauses { get; set; }
    public double SignificanceLevel { get; set; } = 0.05;
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lead-lag relationship analysis result
/// </summary>
public class LeadLagResult
{
    public string Symbol1 { get; set; } = string.Empty;
    public string Symbol2 { get; set; } = string.Empty;
    public int OptimalLag { get; set; }
    public double CrossCorrelation { get; set; }
    public bool Symbol1Leads { get; set; }
    public int LagPeriods { get; set; }
    public double[] CrossCorrelations { get; set; } = Array.Empty<double>();
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Advanced Alpaca order response
/// </summary>
public class AlpacaOrderResponse
{
    public string Id { get; set; } = string.Empty;
    public string ClientOrderId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? FilledAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public DateTime? CanceledAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? FailedReason { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Qty { get; set; } = string.Empty;
    public string? FilledQty { get; set; }
    public string Side { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string TimeInForce { get; set; } = string.Empty;
    public decimal? LimitPrice { get; set; }
    public decimal? StopPrice { get; set; }
    public decimal? TrailPrice { get; set; }
    public decimal? TrailPercent { get; set; }
    public decimal? Hwm { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool? ExtendedHours { get; set; }
    public List<AlpacaOrderLeg> Legs { get; set; } = new();
}

/// <summary>
/// Alpaca order leg for complex orders
/// </summary>
public class AlpacaOrderLeg
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Qty { get; set; } = string.Empty;
    public decimal? Price { get; set; }
}

/// <summary>
/// Portfolio analytics data
/// </summary>
public class PortfolioAnalytics
{
    public decimal TotalValue { get; set; }
    public decimal Cash { get; set; }
    public decimal BuyingPower { get; set; }
    public decimal DayChange { get; set; }
    public decimal DayChangePercent { get; set; }
    public List<PositionAnalytics> Positions { get; set; } = new();
    public int TotalPositions { get; set; }
    public decimal TotalMarketValue { get; set; }
    public decimal TotalUnrealizedPL { get; set; }
}

/// <summary>
/// Position analytics data
/// </summary>
public class PositionAnalytics
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedPL { get; set; }
    public decimal UnrealizedPLPercent { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal LastDayPrice { get; set; }
    public decimal ChangeToday { get; set; }
}

/// <summary>
/// Risk metrics for portfolio
/// </summary>
public class RiskMetrics
{
    public decimal PortfolioValue { get; set; }
    public int Positions { get; set; }
    public List<PositionConcentration> PositionConcentrations { get; set; } = new();
    public decimal DiversificationRatio { get; set; }
    public decimal ValueAtRisk95 { get; set; }
}

/// <summary>
/// Position concentration data
/// </summary>
public class PositionConcentration
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Value { get; set; }
    public decimal UnrealizedPL { get; set; }
}

/// <summary>
/// Tax lot information
/// </summary>
public class TaxLot
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CostBasis { get; set; }
    public DateTime PurchaseDate { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal UnrealizedGainLoss { get; set; }
}

/// <summary>
/// Performance metrics for portfolio
/// </summary>
public class PerformanceMetrics
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal StartValue { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal TotalReturnAmount { get; set; }
    public decimal AnnualizedReturn { get; set; }
    public decimal Volatility { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal MaxDrawdown { get; set; }
}

/// <summary>
/// Research strategy extracted from academic papers
/// </summary>
public class ResearchStrategy
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourcePaper { get; set; } = string.Empty;
    public string Implementation { get; set; } = string.Empty;
    public DateTime ExtractionDate { get; set; }
}

/// <summary>
/// Study replication results
/// </summary>
public class StudyReplication
{
    public ResearchStrategy OriginalStrategy { get; set; } = new();
    public DateTime ReplicationPeriodStart { get; set; }
    public DateTime ReplicationPeriodEnd { get; set; }
    public string GeneratedCode { get; set; } = string.Empty;
    public Dictionary<string, object> Results { get; set; } = new();
    public DateTime ReplicationDate { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// Citation network analysis
/// </summary>
public class CitationNetwork
{
    public string Topic { get; set; } = string.Empty;
    public List<ResearchPaper> Papers { get; set; } = new();
    public Dictionary<string, List<string>> Citations { get; set; } = new();
    public List<string> CentralPapers { get; set; } = new();
    public DateTime CreationDate { get; set; }
}

/// <summary>
/// Research paper information
/// </summary>
public class ResearchPaper
{
    public string Title { get; set; } = string.Empty;
    public List<string> Authors { get; set; } = new();
    public int PublicationYear { get; set; }
    public string Journal { get; set; } = string.Empty;
    public List<string> Citations { get; set; } = new();
    public string Abstract { get; set; } = string.Empty;
}

/// <summary>
/// Literature review synthesis
/// </summary>
public class LiteratureReview
{
    public string Topic { get; set; } = string.Empty;
    public List<PaperAnalysis> PaperAnalyses { get; set; } = new();
    public string Synthesis { get; set; } = string.Empty;
    public DateTime ReviewDate { get; set; }
    public int TotalPapersAnalyzed { get; set; }
}

/// <summary>
/// Individual paper analysis
/// </summary>
public class PaperAnalysis
{
    public string PaperTitle { get; set; } = string.Empty;
    public string KeyFindings { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; }
}

/// <summary>
/// Quantitative model extracted from papers
/// </summary>
public class QuantitativeModel
{
    public string Name { get; set; } = string.Empty;
    public string SourcePaper { get; set; } = string.Empty;
    public List<string> Equations { get; set; } = new();
    public Dictionary<string, double> Parameters { get; set; } = new();
    public string Implementation { get; set; } = string.Empty;
    public DateTime ExtractionDate { get; set; }
}

/// <summary>
/// AutoML pipeline results
/// </summary>
public class AutoMLResult
{
    public string TargetType { get; set; } = string.Empty;
    public DateTime TrainingPeriodStart { get; set; }
    public DateTime TrainingPeriodEnd { get; set; }
    public List<string> Symbols { get; set; } = new();
    public int FeatureCount { get; set; }
    public int ModelsTested { get; set; }
    public ModelResult BestModel { get; set; } = new();
    public List<ModelResult> AllModelResults { get; set; } = new();
    public DateTime ExecutionTime { get; set; }
}

/// <summary>
/// Training data for ML models
/// </summary>
public class TrainingData
{
    public List<List<double>> Features { get; set; } = new();
    public List<double> Targets { get; set; } = new();
    public List<string> FeatureNames { get; set; } = new();
    public string TargetName { get; set; } = string.Empty;
}

/// <summary>
/// Individual model result
/// </summary>
public class ModelResult
{
    public string ModelType { get; set; } = string.Empty;
    public ModelPerformance Performance { get; set; } = new();
    public TimeSpan TrainingTime { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Model performance metrics
/// </summary>
public class ModelPerformance
{
    public double Score { get; set; }
    public double MSE { get; set; }
    public double MAE { get; set; }
    public double R2 { get; set; }
}

/// <summary>
/// Feature selection results
/// </summary>
public class FeatureSelectionResult
{
    public int OriginalFeatureCount { get; set; }
    public int FinalFeatureCount { get; set; }
    public List<int> SelectedFeatures { get; set; } = new();
    public Dictionary<int, double> FeatureImportance { get; set; } = new();
    public string SelectionMethod { get; set; } = string.Empty;
}

/// <summary>
/// Ensemble prediction results
/// </summary>
public class EnsemblePrediction
{
    public EnsembleMethod Method { get; set; }
    public int ModelCount { get; set; }
    public List<double> Weights { get; set; } = new();
    public Vector<double> Predictions { get; set; } = null!;
    public double Confidence { get; set; }
    public DateTime GenerationDate { get; set; }
}

/// <summary>
/// Cross-validation results
/// </summary>
public class CrossValidationResult
{
    public int FoldCount { get; set; }
    public List<ValidationFoldResult> FoldResults { get; set; } = new();
    public double AverageScore { get; set; }
    public double ScoreStdDev { get; set; }
    public DateTime ExecutionDate { get; set; }
}

/// <summary>
/// Individual fold result
/// </summary>
public class ValidationFoldResult
{
    public int FoldIndex { get; set; }
    public double Score { get; set; }
    public int TrainingSize { get; set; }
    public int TestSize { get; set; }
}

/// <summary>
/// Hyperparameter optimization results
/// </summary>
public class HyperparameterOptimizationResult
{
    public string ModelType { get; set; } = string.Empty;
    public Dictionary<string, List<double>> ParameterGrid { get; set; } = new();
    public int CombinationsTested { get; set; }
    public Dictionary<string, double> BestParameters { get; set; } = new();
    public double BestScore { get; set; }
    public List<ParameterSetResult> AllResults { get; set; } = new();
    public DateTime OptimizationDate { get; set; }
}

/// <summary>
/// Parameter set evaluation result
/// </summary>
public class ParameterSetResult
{
    public Dictionary<string, double> Parameters { get; set; } = new();
    public double Score { get; set; }
}

/// <summary>
/// Ensemble method enumeration
/// </summary>
public enum EnsembleMethod
{
    SimpleAverage,
    WeightedAverage,
    Stacking,
    Blending
}

/// <summary>
/// SHAP analysis results
/// </summary>
public class SHAPAnalysis
{
    public double BaseValue { get; set; }
    public List<List<double>> SHAPValues { get; set; } = new();
    public List<string> FeatureNames { get; set; } = new();
    public int InstanceCount { get; set; }
    public Dictionary<string, double> FeatureImportance { get; set; } = new();
    public DateTime AnalysisDate { get; set; }
}

/// <summary>
/// Partial dependence analysis
/// </summary>
public class PartialDependenceAnalysis
{
    public string FeatureName { get; set; } = string.Empty;
    public List<double> FeatureGrid { get; set; } = new();
    public List<double> PartialDependenceValues { get; set; } = new();
    public int GridPoints { get; set; }
    public DateTime GenerationDate { get; set; }
}

/// <summary>
/// Feature interaction analysis
/// </summary>
public class FeatureInteractionAnalysis
{
    public List<FeatureInteraction> Interactions { get; set; } = new();
    public int TotalInteractionsAnalyzed { get; set; }
    public int TopInteractions { get; set; }
    public DateTime AnalysisDate { get; set; }
}

/// <summary>
/// Individual feature interaction
/// </summary>
public class FeatureInteraction
{
    public string Feature1 { get; set; } = string.Empty;
    public string Feature2 { get; set; } = string.Empty;
    public double InteractionStrength { get; set; }
}

/// <summary>
/// Prediction explanation for individual instance
/// </summary>
public class PredictionExplanation
{
    public double Prediction { get; set; }
    public double BaseValue { get; set; }
    public Dictionary<string, double> FeatureContributions { get; set; } = new();
    public Dictionary<string, double> TopPositiveFeatures { get; set; } = new();
    public Dictionary<string, double> TopNegativeFeatures { get; set; } = new();
    public DateTime ExplanationDate { get; set; }
}

/// <summary>
/// Permutation feature importance
/// </summary>
public class PermutationImportance
{
    public double BaseScore { get; set; }
    public Dictionary<string, double> FeatureImportance { get; set; } = new();
    public int PermutationsUsed { get; set; }
    public DateTime CalculationDate { get; set; }
}

/// <summary>
/// Model fairness analysis
/// </summary>
public class ModelFairnessAnalysis
{
    public List<string> ProtectedGroups { get; set; } = new();
    public Dictionary<string, FairnessMetrics> GroupMetrics { get; set; } = new();
    public double OverallFairness { get; set; }
    public DateTime AnalysisDate { get; set; }
}

/// <summary>
/// Fairness metrics for a group
/// </summary>
public class FairnessMetrics
{
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
}

/// <summary>
/// Market state for RL
/// </summary>
public class MarketState
{
    public int Index { get; set; }
    public double Price { get; set; }
    public List<double> Features { get; set; } = new();
    public double Volume { get; set; }
    public double Volatility { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Q-learning agent
/// </summary>
public class QAgent : RLAgent
{
    public QAgent() {}
    public QAgent(QLearningConfig config)
    {
        Config = config;
    }
    public Dictionary<string, double[]> QTable { get; set; } = new();
    public QLearningConfig Config { get; set; } = new();
    public bool TrainingComplete { get; set; }

    public int SelectAction(MarketState state, int episode)
    {
        var epsilon = Config.EpsilonStart * Math.Pow(Config.EpsilonDecay, episode);
        var random = new Random();

        if (random.NextDouble() < epsilon)
        {
            return random.Next(Config.ActionCount);
        }

        return GetOptimalAction(state);
    }

    public void UpdateQValue(MarketState state, int action, double reward, MarketState nextState)
    {
        var stateKey = GetStateKey(state);
        var nextStateKey = GetStateKey(nextState);

        if (!QTable.ContainsKey(stateKey))
        {
            QTable[stateKey] = new double[Config.ActionCount];
        }

        var currentQ = QTable[stateKey][action];
        var maxNextQ = QTable.ContainsKey(nextStateKey) ?
            QTable[nextStateKey].Max() : 0.0;

        QTable[stateKey][action] = currentQ + Config.LearningRate *
            (reward + Config.DiscountFactor * maxNextQ - currentQ);
    }

    public int GetOptimalAction(MarketState state)
    {
        var stateKey = GetStateKey(state);

        if (!QTable.ContainsKey(stateKey))
        {
            return new Random().Next(Config.ActionCount);
        }

        return Array.IndexOf(QTable[stateKey], QTable[stateKey].Max());
    }

    public double GetActionConfidence(MarketState state, int action)
    {
        var stateKey = GetStateKey(state);

        if (!QTable.ContainsKey(stateKey))
        {
            return 0.0;
        }

        var qValues = QTable[stateKey];
        var maxQ = qValues.Max();
        var actionQ = qValues[action];

        return actionQ / maxQ; // Simplified confidence
    }

    private string GetStateKey(MarketState state)
    {
        // Discretize state for Q-table
        var priceBin = (int)(state.Price / 10); // Simplified discretization
        var volumeBin = (int)(state.Volume / 1000);
        return $"{priceBin}_{volumeBin}";
    }
}

/// <summary>
/// Q-learning configuration
/// </summary>
public class QLearningConfig
{
    public int Episodes { get; set; } = 1000;
    public int MaxStepsPerEpisode { get; set; } = 100;
    public int ActionCount { get; set; } = 3; // Buy, Sell, Hold
    public double LearningRate { get; set; } = 0.1;
    public double DiscountFactor { get; set; } = 0.95;
    public double EpsilonStart { get; set; } = 1.0;
    public double EpsilonDecay { get; set; } = 0.995;
}

/// <summary>
/// Policy gradient agent
/// </summary>
public class PolicyGradientAgent
{
    public PolicyGradientAgent() {}
    public PolicyGradientAgent(PolicyGradientConfig config)
    {
        Config = config;
    }
    public PolicyGradientConfig Config { get; set; } = new();
    public bool TrainingComplete { get; set; }

    public int SelectAction(MarketState state)
    {
        // Simplified policy selection
        var random = new Random();
        return random.Next(Config.ActionCount);
    }

    public void UpdatePolicy(List<(MarketState, int, double)> trajectory)
    {
        // Simplified policy update
        // In practice, would update neural network parameters
    }
}

/// <summary>
/// Policy gradient configuration
/// </summary>
public class PolicyGradientConfig
{
    public int Episodes { get; set; } = 1000;
    public int MaxStepsPerEpisode { get; set; } = 100;
    public int ActionCount { get; set; } = 3;
    public double LearningRate { get; set; } = 0.01;
}

/// <summary>
/// Actor-critic agent
/// </summary>
public class ActorCriticAgent
{
    public ActorCriticAgent() {}
    public ActorCriticAgent(ActorCriticConfig config)
    {
        Config = config;
    }
    public ActorCriticConfig Config { get; set; } = new();
    public bool TrainingComplete { get; set; }

    public int SelectAction(MarketState state)
    {
        var random = new Random();
        return random.Next(Config.ActionCount);
    }

    public void UpdateActorCritic(List<(MarketState, int, double, MarketState)> trajectory)
    {
        // Simplified actor-critic update
    }
}

/// <summary>
/// Actor-critic configuration
/// </summary>
public class ActorCriticConfig
{
    public int Episodes { get; set; } = 1000;
    public int MaxStepsPerEpisode { get; set; } = 100;
    public int ActionCount { get; set; } = 3;
    public double ActorLearningRate { get; set; } = 0.01;
    public double CriticLearningRate { get; set; } = 0.01;
}

/// <summary>
/// Adaptive trading strategy
/// </summary>
public class AdaptiveStrategy
{
    public Dictionary<string, double> BaseParameters { get; set; } = new();
    public Dictionary<string, double> AdaptedParameters { get; set; } = new();
    public string AdaptationReason { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public DateTime AdaptationDate { get; set; }
}

/// <summary>
/// Parameter set for optimization
/// </summary>
public class ParameterSet
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, double> Parameters { get; set; } = new();
}

/// <summary>
/// Bandit optimization results
/// </summary>
public class BanditOptimization
{
    public ParameterSet OptimalParameters { get; set; } = new();
    public double BestReward { get; set; }
    public int TotalTrials { get; set; }
    public double Regret { get; set; }
    public DateTime OptimizationDate { get; set; }
}

/// <summary>
/// UCB1 bandit implementation
/// </summary>
public class UCB1Bandit
{
    private readonly int _arms;
    private readonly int[] _counts;
    private readonly double[] _values;
    private int _totalCount;

    public UCB1Bandit(int arms)
    {
        _arms = arms;
        _counts = new int[arms];
        _values = new double[arms];
    }

    public int SelectArm()
    {
        for (int i = 0; i < _arms; i++)
        {
            if (_counts[i] == 0)
                return i;
        }

        var ucbValues = new double[_arms];
        for (int i = 0; i < _arms; i++)
        {
            ucbValues[i] = _values[i] + Math.Sqrt(2 * Math.Log(_totalCount) / _counts[i]);
        }

        return Array.IndexOf(ucbValues, ucbValues.Max());
    }

    public void Update(int arm, double reward)
    {
        _counts[arm]++;
        _totalCount++;
        _values[arm] = ((_counts[arm] - 1) * _values[arm] + reward) / _counts[arm];
    }

    public double GetBestReward() => _values.Max();
    public double GetRegret() => _totalCount * _values.Max() - _values.Sum(v => v * Array.IndexOf(_values, v) + 1);
    public int GetBestArm() => Array.IndexOf(_values, _values.Max());
}

/// <summary>
/// Contextual bandit result
/// </summary>
public class ContextualBanditResult
{
    public List<ContextualBanditStep> Steps { get; set; } = new();
    public double TotalReward { get; set; }
    public double AverageRegret { get; set; }
    public Strategy BestStrategy { get; set; } = new();
    public DateTime CompletionDate { get; set; }
}

/// <summary>
/// Individual contextual bandit step
/// </summary>
public class ContextualBanditStep
{
    public MarketState Context { get; set; } = new();
    public Strategy SelectedStrategy { get; set; } = new();
    public double Reward { get; set; }
    public double Regret { get; set; }
}

/// <summary>
/// Trading strategy
/// </summary>
public class Strategy
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Dictionary<string, double> Parameters { get; set; } = new();
}

/// <summary>
/// LinUCB bandit for contextual bandits
/// </summary>
public class LinUCB
{
    private readonly int _arms;
    private readonly int _dimensions;
    private readonly List<Matrix<double>> _A;
    private readonly List<Vector<double>> _b;
    private readonly double _alpha;

    public LinUCB(int arms, int dimensions, double alpha = 1.0)
    {
        _arms = arms;
        _dimensions = dimensions;
        _alpha = alpha;
        _A = new List<Matrix<double>>();
        _b = new List<Vector<double>>();

        for (int i = 0; i < arms; i++)
        {
            _A.Add(Matrix<double>.Build.DenseIdentity(_dimensions));
            _b.Add(Vector<double>.Build.Dense(_dimensions));
        }
    }

    public int SelectArm(Vector<double> context)
    {
        var ucbValues = new double[_arms];

        for (int i = 0; i < _arms; i++)
        {
            var theta = _A[i].Inverse() * _b[i];
            var p = theta.DotProduct(context);
            var confidence = Math.Sqrt(context.DotProduct(_A[i].Inverse() * context));

            ucbValues[i] = p + _alpha * confidence;
        }

        return Array.IndexOf(ucbValues, ucbValues.Max());
    }

    public void Update(int arm, Vector<double> context, double reward)
    {
        _A[arm] += context.OuterProduct(context);
        _b[arm] += reward * context;
    }

    public double GetRegret() => 0.0; // Simplified
    public int GetBestArm() => 0; // Simplified
}

/// <summary>
/// Bandit configuration
/// </summary>
public class BanditConfig
{
    public int TotalTrials { get; set; } = 1000;
}

/// <summary>
/// Contextual bandit configuration
/// </summary>
public class ContextualBanditConfig
{
    public int TotalTrials { get; set; } = 1000;
}

/// <summary>
/// RL agent interface
/// </summary>
public interface RLAgent
{
    int GetOptimalAction(MarketState state);
}

/// <summary>
/// RL performance evaluation
/// </summary>
public class RLPerformanceEvaluation
{
    public double AverageReward { get; set; }
    public double RewardStdDev { get; set; }
    public double AverageEpisodeLength { get; set; }
    public double MaxReward { get; set; }
    public double MinReward { get; set; }
    public int EvaluationEpisodes { get; set; }
    public DateTime EvaluationDate { get; set; }
}
