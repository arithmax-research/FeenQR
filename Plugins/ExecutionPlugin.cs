using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;
using System.ComponentModel;
using System.Text.Json;

namespace QuantResearchAgent.Plugins;

public class ExecutionPlugin
{
    private readonly ExecutionService _executionService;

    public ExecutionPlugin(ExecutionService executionService)
    {
        _executionService = executionService;
    }

    [KernelFunction, Description("Generate VWAP execution schedule")]
    public async Task<string> GenerateVWAPSchedule(
        [Description("Stock symbol")] string symbol,
        [Description("Total shares to trade")] double totalShares,
        [Description("Start time (ISO format)")] string startTime,
        [Description("End time (ISO format)")] string endTime,
        [Description("Expected volumes as comma-separated values (optional)")] string? expectedVolumes = null)
    {
        try
        {
            DateTime start = DateTime.Parse(startTime);
            DateTime end = DateTime.Parse(endTime);
            List<double>? volumes = null;

            if (!string.IsNullOrEmpty(expectedVolumes))
            {
                volumes = expectedVolumes.Split(',').Select(double.Parse).ToList();
            }

            var result = await _executionService.GenerateVWAPSchedule(symbol, totalShares, start, end, volumes);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error generating VWAP schedule: {ex.Message}";
        }
    }

    [KernelFunction, Description("Generate TWAP execution schedule")]
    public async Task<string> GenerateTWAPSchedule(
        [Description("Total shares to trade")] double totalShares,
        [Description("Start time (ISO format)")] string startTime,
        [Description("End time (ISO format)")] string endTime,
        [Description("Number of intervals (default 10)")] int intervals = 10)
    {
        try
        {
            DateTime start = DateTime.Parse(startTime);
            DateTime end = DateTime.Parse(endTime);

            var result = _executionService.GenerateTWAPSchedule(totalShares, start, end, intervals);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error generating TWAP schedule: {ex.Message}";
        }
    }

    [KernelFunction, Description("Create iceberg order structure")]
    public async Task<string> CreateIcebergOrder(
        [Description("Total quantity to trade")] double totalQuantity,
        [Description("Display quantity per slice")] double displayQuantity,
        [Description("Number of slices")] int numberOfSlices,
        [Description("Interval between slices in seconds (default 30)")] double sliceIntervalSeconds = 30)
    {
        try
        {
            var result = _executionService.CreateIcebergOrder(
                totalQuantity, displayQuantity, numberOfSlices, sliceIntervalSeconds);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error creating iceberg order: {ex.Message}";
        }
    }

    [KernelFunction, Description("Make smart routing decision")]
    public async Task<string> MakeSmartRoutingDecision(
        [Description("Stock symbol")] string symbol,
        [Description("Order quantity")] double quantity,
        [Description("Order type (market, limit, etc.)")] string orderType = "market")
    {
        try
        {
            var result = await _executionService.MakeSmartRoutingDecision(symbol, quantity, orderType);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error making smart routing decision: {ex.Message}";
        }
    }

    [KernelFunction, Description("Optimize execution parameters based on market conditions")]
    public async Task<string> OptimizeExecutionParameters(
        [Description("Stock symbol")] string symbol,
        [Description("Total shares to trade")] double totalShares,
        [Description("Urgency factor (0-1, where 1 is most urgent)")] double urgencyFactor = 0.5)
    {
        try
        {
            var result = _executionService.OptimizeExecutionParameters(symbol, totalShares, urgencyFactor);

            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error optimizing execution parameters: {ex.Message}";
        }
    }
}