namespace FeenQR.Core.Models;

/// <summary>
/// DTO for Chart.js technical chart rendering.
/// Matches the structure expected by feenqr.renderTechCharts()
/// </summary>
public class TechnicalChartDto
{
    public List<string> Dates { get; set; } = new();
    public List<decimal> Close { get; set; } = new();
    public List<decimal?> Sma20 { get; set; } = new();
    public List<decimal?> Sma50 { get; set; } = new();
    public List<decimal?> Sma200 { get; set; } = new();
    public List<double?> Rsi { get; set; } = new();
    public List<double?> Macd { get; set; } = new();
    public List<double?> MacdSignal { get; set; } = new();
}
