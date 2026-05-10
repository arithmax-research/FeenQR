namespace FeenQR.Core.Models;

/// <summary>
/// Represents a structured stock pitch result for display.
/// </summary>
public class StockPitchResult
{
    public string Symbol { get; set; } = string.Empty;
    public string PositionType { get; set; } = string.Empty;
    public string TargetPrice { get; set; } = "N/A";
    public string ExpectedReturn { get; set; } = "N/A";
    public string Conviction { get; set; } = "Medium";
    public string TimeHorizon { get; set; } = "6-12 months";
    public string GeneratedAt { get; set; } = string.Empty;
    public double GenerationTimeMs { get; set; }
    public List<string> DataSources { get; set; } = new();
    public string RawPitchText { get; set; } = string.Empty;
    public string StockRecommendation { get; set; } = string.Empty;
    public string CompanyOverview { get; set; } = string.Empty;
    public string InvestmentThesis { get; set; } = string.Empty;
    public string Catalysts { get; set; } = string.Empty;
    public string ValuationAndReturns { get; set; } = string.Empty;
    public string RisksAndMitigation { get; set; } = string.Empty;
    public string Conclusion { get; set; } = string.Empty;
}
