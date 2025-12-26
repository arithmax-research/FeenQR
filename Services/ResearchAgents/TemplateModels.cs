namespace QuantResearchAgent.Services.ResearchAgents
{
    // Data structures for trading strategy templates
    public class TradingStrategyTemplate
    {
        public string Symbol { get; set; }
        public string Parameters { get; set; }
        public string EntryConditions { get; set; }
        public string ExitConditions { get; set; }
        public string RiskManagement { get; set; }
        public string ExportFormat { get; set; }
    }

    /// <summary>
    /// Comprehensive trading template model
    /// </summary>
    public class TradingTemplate
    {
        public string Symbol { get; set; }
        public string StrategyType { get; set; }
        public DateTime GeneratedAt { get; set; }
        public StrategyParameters StrategyParams { get; set; }
        public EntryConditions EntryConditions { get; set; }
        public ExitFramework ExitFramework { get; set; }
        public RiskManagement RiskManagement { get; set; }
        public TechnicalIndicators TechnicalIndicators { get; set; }
        public DataRequirements DataRequirements { get; set; }
        public BacktestConfiguration BacktestConfig { get; set; }
        public List<string> Limitations { get; set; }
        public List<string> ImplementationNotes { get; set; }
    }

    public class StrategyParameters
    {
        public decimal SupportLevel { get; set; }
        public decimal ResistanceLevel { get; set; }
        public int RSIPeriod { get; set; }
        public int EMAPeriod { get; set; }
        public decimal PositionRiskPercent { get; set; }
        public decimal VolatilityThreshold { get; set; }
        public decimal StopLossPercent { get; set; }
        public decimal TakeProfitPercent { get; set; }
    }

    public class EntryConditions
    {
        public List<string> PrimaryConditions { get; set; }
        public List<string> SecondaryConditions { get; set; }
        public List<string> ConfirmationSignals { get; set; }
    }

    public class ExitFramework
    {
        public List<ProfitTarget> ProfitTargets { get; set; }
        public List<RiskExit> RiskExits { get; set; }
        public List<string> TimeBasedExits { get; set; }
    }

    public class ProfitTarget
    {
        public int Level { get; set; }
        public decimal Percentage { get; set; }
        public string Description { get; set; }
    }

    public class RiskExit
    {
        public string Type { get; set; }
        public decimal Threshold { get; set; }
        public string Description { get; set; }
    }

    public class RiskManagement
    {
        public decimal MaxPositionSize { get; set; }
        public decimal MaxPortfolioRisk { get; set; }
        public int MaxConcurrentPositions { get; set; }
        public List<string> RiskRules { get; set; }
    }

    public class TechnicalIndicators
    {
        public List<string> TrendIndicators { get; set; }
        public List<string> MomentumIndicators { get; set; }
        public List<string> VolatilityIndicators { get; set; }
        public List<string> VolumeIndicators { get; set; }
    }

    public class DataRequirements
    {
        public List<string> RequiredDataSources { get; set; }
        public List<string> FrequencyRequirements { get; set; }
        public List<string> HistoricalDataPeriod { get; set; }
    }

    public class BacktestConfiguration
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal InitialCapital { get; set; }
        public string BenchmarkSymbol { get; set; }
        public List<string> PerformanceMetrics { get; set; }
    }
}
