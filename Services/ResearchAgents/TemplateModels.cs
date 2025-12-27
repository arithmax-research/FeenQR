namespace QuantResearchAgent.Services.ResearchAgents
{
    // Data structures for trading strategy templates
    public class TradingStrategyTemplate
    {
        public required string Symbol { get; set; }
        public required string Parameters { get; set; }
        public required string EntryConditions { get; set; }
        public required string ExitConditions { get; set; }
        public required string RiskManagement { get; set; }
        public required string ExportFormat { get; set; }
    }

    /// <summary>
    /// Comprehensive trading template model
    /// </summary>
    public class TradingTemplate
    {
        public required string Symbol { get; set; }
        public required string StrategyType { get; set; }
        public DateTime GeneratedAt { get; set; }
        public required StrategyParameters StrategyParams { get; set; }
        public required EntryConditions EntryConditions { get; set; }
        public required ExitFramework ExitFramework { get; set; }
        public required RiskManagement RiskManagement { get; set; }
        public required TechnicalIndicators TechnicalIndicators { get; set; }
        public required DataRequirements DataRequirements { get; set; }
        public required BacktestConfiguration BacktestConfig { get; set; }
        public required List<string> Limitations { get; set; }
        public required List<string> ImplementationNotes { get; set; }
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
        public required List<string> PrimaryConditions { get; set; }
        public required List<string> SecondaryConditions { get; set; }
        public required List<string> ConfirmationSignals { get; set; }
    }

    public class ExitFramework
    {
        public required List<ProfitTarget> ProfitTargets { get; set; }
        public required List<RiskExit> RiskExits { get; set; }
        public required List<string> TimeBasedExits { get; set; }
    }

    public class ProfitTarget
    {
        public int Level { get; set; }
        public decimal Percentage { get; set; }
        public required string Description { get; set; }
    }

    public class RiskExit
    {
        public required string Type { get; set; }
        public decimal Threshold { get; set; }
        public required string Description { get; set; }
    }

    public class RiskManagement
    {
        public decimal MaxPositionSize { get; set; }
        public decimal MaxPortfolioRisk { get; set; }
        public int MaxConcurrentPositions { get; set; }
        public required List<string> RiskRules { get; set; }
    }

    public class TechnicalIndicators
    {
        public required List<string> TrendIndicators { get; set; }
        public required List<string> MomentumIndicators { get; set; }
        public required List<string> VolatilityIndicators { get; set; }
        public required List<string> VolumeIndicators { get; set; }
    }

    public class DataRequirements
    {
        public required List<string> RequiredDataSources { get; set; }
        public required List<string> FrequencyRequirements { get; set; }
        public required List<string> HistoricalDataPeriod { get; set; }
    }

    public class BacktestConfiguration
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal InitialCapital { get; set; }
        public required string BenchmarkSymbol { get; set; }
        public required List<string> PerformanceMetrics { get; set; }
    }
}
