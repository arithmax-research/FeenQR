namespace FeenQR.Core.Models;

public class Quote
{
    public required string Symbol { get; set; }
    public double Price { get; set; }
    public double Change { get; set; }
    public double ChangePercent { get; set; }
    public long Volume { get; set; }
    public DateTime Timestamp { get; set; }
}