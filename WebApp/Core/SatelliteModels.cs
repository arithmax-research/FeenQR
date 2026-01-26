namespace FeenQR.WebApp.Core;

public class GlobalPortAnalysis
{
    public DateTime AnalysisDate { get; set; }
    public string Period { get; set; } = "";
    public int PortsAnalyzed { get; set; }
    public long TotalContainersDetected { get; set; }
    public long PreviousContainerCount { get; set; }
    public double VolumeChangePercent { get; set; }
    public string EconomicSignal { get; set; } = "";
    public bool BottleneckWarning { get; set; }
    public List<PortContainerData> PortData { get; set; } = new();
    public string Summary { get; set; } = "";
}

public class PortContainerData
{
    public string PortName { get; set; } = "";
    public long CurrentContainerCount { get; set; }
    public long PreviousContainerCount { get; set; }
    public double ChangePercent { get; set; }
    public int ImageCount { get; set; } // Number of satellite images analyzed
    public double ConfidenceScore { get; set; } // Deep learning model confidence
}

public class RegionalPortAnalysis
{
    public string Region { get; set; } = "";
    public DateTime AnalysisDate { get; set; }
    public string Period { get; set; } = "";
    public long ContainerVolume { get; set; }
    public long PreviousVolume { get; set; }
    public double VolumeChange { get; set; }
    public double MarketCorrelation { get; set; }
    public double PredictiveAccuracy { get; set; }
    public bool IsSupplyChainCrisis { get; set; }
    public string AlphaSignal { get; set; } = "";
}

public class ContainerTradingSignal
{
    public string Market { get; set; } = "";
    public string Direction { get; set; } = ""; // LONG, SHORT, NEUTRAL, REDUCE EXPOSURE
    public double Confidence { get; set; }
    public double ExpectedReturn { get; set; }
    public DateTime GeneratedAt { get; set; }
    public double ContainerVolumeChange { get; set; }
    public double MarketCorrelation { get; set; }
    public string Reasoning { get; set; } = "";
}

public class BacktestResults
{
    public string Period { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal InitialCapital { get; set; }
    public decimal FinalCapital { get; set; }
    public double AnnualReturn { get; set; }
    public double SharpeRatio { get; set; }
    public double MaxDrawdown { get; set; }
    public int TotalTrades { get; set; }
    public double WinRate { get; set; }
    public int CountriesTested { get; set; }
    public double? CovidPerformance { get; set; } // Special COVID period performance
    public bool NoLookaheadBias { get; set; }
    public string TrainingPeriod { get; set; } = "";
    public string ValidationMethod { get; set; } = "";
}
