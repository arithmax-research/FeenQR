using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// Plugin for corporate action processing and price adjustment functions
/// </summary>
public class CorporateActionPlugin
{
    private readonly CorporateActionService _corporateActionService;

    public CorporateActionPlugin(CorporateActionService corporateActionService)
    {
        _corporateActionService = corporateActionService;
    }

    /// <summary>
    /// Process corporate actions for historical price data
    /// </summary>
    [KernelFunction, Description("Adjust historical prices for corporate actions (splits, dividends, mergers)")]
    public async Task<string> ProcessCorporateActionsAsync(
        [Description("Stock symbol")] string symbol,
        [Description("Historical prices as JSON object with dates as keys and prices as values")] string pricesJson,
        [Description("Corporate actions as JSON array")] string actionsJson)
    {
        try
        {
            var pricesDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(pricesJson);
            if (pricesDict == null)
            {
                return "Error: Invalid prices format. Expected JSON object with date strings as keys and decimal prices as values.";
            }

            // Convert string keys to DateTime
            var prices = new Dictionary<DateTime, decimal>();
            foreach (var kvp in pricesDict)
            {
                if (DateTime.TryParse(kvp.Key, out var date))
                {
                    prices[date] = kvp.Value;
                }
            }

            var actions = System.Text.Json.JsonSerializer.Deserialize<List<CorporateActionService.CorporateAction>>(actionsJson);
            if (actions == null)
            {
                actions = new List<CorporateActionService.CorporateAction>();
            }

            var result = await _corporateActionService.ProcessCorporateActionsAsync(symbol, prices, actions);

            var response = new System.Text.StringBuilder();
            response.AppendLine($"Corporate Action Processing Results for {symbol}");
            response.AppendLine("==========================================");
            response.AppendLine($"Total Adjustment Factor: {result.TotalAdjustmentFactor:F6}");
            response.AppendLine();

            if (result.AppliedActions.Any())
            {
                response.AppendLine("Applied Actions:");
                response.AppendLine("---------------");
                foreach (var action in result.AppliedActions)
                {
                    response.AppendLine($"{action.ExDate:yyyy-MM-dd}: {GetActionDescription(action)}");
                }
                response.AppendLine();
            }

            response.AppendLine("Adjustment Summary:");
            response.AppendLine("------------------");
            response.AppendLine(result.AdjustmentSummary);

            // Show sample of adjusted prices
            response.AppendLine("Sample Adjusted Prices:");
            response.AppendLine("----------------------");
            var samplePrices = result.AdjustedPrices.OrderByDescending(p => p.Key).Take(5);
            foreach (var price in samplePrices)
            {
                var originalPrice = prices.ContainsKey(price.Key) ? prices[price.Key] : 0;
                var adjustment = originalPrice != 0 ? (price.Value / originalPrice - 1) * 100 : 0;
                response.AppendLine($"{price.Key:yyyy-MM-dd}: {price.Value:F4} (adjusted from {originalPrice:F4}, {adjustment:+0.00;-0.00;0.00}%)");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            return $"Error processing corporate actions: {ex.Message}";
        }
    }

    /// <summary>
    /// Detect potential corporate actions from price data
    /// </summary>
    [KernelFunction, Description("Detect potential corporate actions from price movements")]
    public async Task<string> DetectCorporateActionsAsync(
        [Description("Stock symbol")] string symbol,
        [Description("Historical prices as JSON object")] string pricesJson)
    {
        try
        {
            var pricesDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(pricesJson);
            if (pricesDict == null)
            {
                return "Error: Invalid prices format.";
            }

            var prices = new Dictionary<DateTime, decimal>();
            foreach (var kvp in pricesDict)
            {
                if (DateTime.TryParse(kvp.Key, out var date))
                {
                    prices[date] = kvp.Value;
                }
            }

            var detectedActions = await _corporateActionService.DetectCorporateActionsAsync(symbol, prices);

            var response = new System.Text.StringBuilder();
            response.AppendLine($"Corporate Action Detection Results for {symbol}");
            response.AppendLine("===========================================");

            if (detectedActions.Any())
            {
                response.AppendLine($"{detectedActions.Count} potential corporate actions detected:");
                response.AppendLine();

                foreach (var action in detectedActions)
                {
                    response.AppendLine($"Date: {action.ExDate:yyyy-MM-dd}");
                    response.AppendLine($"Type: {action.ActionType}");
                    response.AppendLine($"Ratio: {action.Ratio:F4}");
                    response.AppendLine($"Description: {action.Description}");
                    response.AppendLine();
                }

                response.AppendLine("Note: These are algorithmic detections and should be verified with official corporate action data.");
            }
            else
            {
                response.AppendLine("No potential corporate actions detected in the price data.");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            return $"Error detecting corporate actions: {ex.Message}";
        }
    }

    /// <summary>
    /// Calculate price adjustment for a specific corporate action
    /// </summary>
    [KernelFunction, Description("Calculate price adjustment factor for a corporate action")]
    public async Task<string> CalculatePriceAdjustmentAsync(
        [Description("Corporate action type (StockSplit, StockDividend, CashDividend, Merger, etc.)")] string actionType,
        [Description("Action ratio or value")] double ratio,
        [Description("Original price before adjustment")] double originalPrice)
    {
        try
        {
            var action = new CorporateActionService.CorporateAction
            {
                ActionType = Enum.Parse<CorporateActionService.CorporateActionType>(actionType),
                Ratio = (decimal)ratio,
                Value = (decimal)ratio // For cash dividends
            };

            // Calculate adjustment factor
            var adjustmentFactor = GetAdjustmentFactor(action);
            var adjustedPrice = originalPrice / (double)adjustmentFactor;
            var changePercent = ((adjustedPrice / originalPrice) - 1) * 100;

            var response = new System.Text.StringBuilder();
            response.AppendLine("Price Adjustment Calculation");
            response.AppendLine("===========================");
            response.AppendLine($"Action Type: {action.ActionType}");
            response.AppendLine($"Ratio/Value: {ratio:F4}");
            response.AppendLine($"Original Price: ${originalPrice:F4}");
            response.AppendLine($"Adjustment Factor: {adjustmentFactor:F6}");
            response.AppendLine($"Adjusted Price: ${adjustedPrice:F4}");
            response.AppendLine($"Change: {changePercent:+0.00;-0.00;0.00}%");

            return response.ToString();
        }
        catch (Exception ex)
        {
            return $"Error calculating price adjustment: {ex.Message}";
        }
    }

    /// <summary>
    /// Get corporate action adjustment factor
    /// </summary>
    private decimal GetAdjustmentFactor(CorporateActionService.CorporateAction action)
    {
        switch (action.ActionType)
        {
            case CorporateActionService.CorporateActionType.StockSplit:
                return 1 / action.Ratio;

            case CorporateActionService.CorporateActionType.StockDividend:
                return 1 / (1 + action.Ratio);

            case CorporateActionService.CorporateActionType.CashDividend:
                return 1.0m; // No price adjustment

            case CorporateActionService.CorporateActionType.RightsOffering:
                return 1 - action.Ratio;

            case CorporateActionService.CorporateActionType.Merger:
            case CorporateActionService.CorporateActionType.Acquisition:
                return action.Ratio;

            case CorporateActionService.CorporateActionType.SpinOff:
                return 1 / (1 + action.Ratio);

            default:
                return 1.0m;
        }
    }

    /// <summary>
    /// Get human-readable description of corporate action
    /// </summary>
    private string GetActionDescription(CorporateActionService.CorporateAction action)
    {
        switch (action.ActionType)
        {
            case CorporateActionService.CorporateActionType.StockSplit:
                return $"{action.Ratio}:1 stock split";

            case CorporateActionService.CorporateActionType.StockDividend:
                return $"{action.Ratio * 100:F1}% stock dividend";

            case CorporateActionService.CorporateActionType.CashDividend:
                return $"${action.Value:F2} cash dividend";

            case CorporateActionService.CorporateActionType.RightsOffering:
                return $"Rights offering (dilution: {action.Ratio:P2})";

            case CorporateActionService.CorporateActionType.Merger:
                return $"Merger (exchange ratio: {action.Ratio:F4})";

            case CorporateActionService.CorporateActionType.Acquisition:
                return $"Acquisition (exchange ratio: {action.Ratio:F4})";

            case CorporateActionService.CorporateActionType.SpinOff:
                return $"Spin-off (distribution ratio: {action.Ratio:F4})";

            default:
                return action.Description;
        }
    }

    /// <summary>
    /// Analyze impact of corporate actions on returns
    /// </summary>
    [KernelFunction, Description("Analyze how corporate actions affect historical returns")]
    public async Task<string> AnalyzeCorporateActionImpactAsync(
        [Description("Stock symbol")] string symbol,
        [Description("Historical prices as JSON object")] string pricesJson,
        [Description("Corporate actions as JSON array")] string actionsJson)
    {
        try
        {
            var pricesDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(pricesJson);
            if (pricesDict == null)
            {
                return "Error: Invalid prices format.";
            }

            var prices = new Dictionary<DateTime, decimal>();
            foreach (var kvp in pricesDict)
            {
                if (DateTime.TryParse(kvp.Key, out var date))
                {
                    prices[date] = kvp.Value;
                }
            }

            var actions = System.Text.Json.JsonSerializer.Deserialize<List<CorporateActionService.CorporateAction>>(actionsJson);
            if (actions == null)
            {
                actions = new List<CorporateActionService.CorporateAction>();
            }

            var result = await _corporateActionService.ProcessCorporateActionsAsync(symbol, prices, actions);

            var response = new System.Text.StringBuilder();
            response.AppendLine($"Corporate Action Impact Analysis for {symbol}");
            response.AppendLine("==========================================");

            if (!result.AppliedActions.Any())
            {
                response.AppendLine("No corporate actions to analyze.");
                return response.ToString();
            }

            // Calculate returns around corporate action dates
            response.AppendLine("Impact on Returns Around Action Dates:");
            response.AppendLine("-------------------------------------");

            foreach (var action in result.AppliedActions)
            {
                var actionDate = action.ExDate;
                var pricesAroundAction = prices.Where(p => Math.Abs((p.Key - actionDate).TotalDays) <= 30)
                                              .OrderBy(p => p.Key)
                                              .ToList();

                if (pricesAroundAction.Count >= 2)
                {
                    var preActionPrice = pricesAroundAction.First().Value;
                    var postActionPrice = pricesAroundAction.Last().Value;
                    var rawReturn = (postActionPrice - preActionPrice) / preActionPrice * 100;

                    response.AppendLine($"{action.ActionType} on {actionDate:yyyy-MM-dd}:");
                    response.AppendLine($"  Raw return (±30 days): {rawReturn:+0.00;-0.00;0.00}%");
                    response.AppendLine($"  Adjustment factor: {GetAdjustmentFactor(action):F6}");
                }
            }

            response.AppendLine();
            response.AppendLine("Note: Corporate actions can significantly affect raw returns.");
            response.AppendLine("Always use adjusted prices for accurate performance analysis.");

            return response.ToString();
        }
        catch (Exception ex)
        {
            return $"Error analyzing corporate action impact: {ex.Message}";
        }
    }

    /// <summary>
    /// Create a corporate action record
    /// </summary>
    [KernelFunction, Description("Create a corporate action record for processing")]
    public async Task<string> CreateCorporateActionAsync(
        [Description("Stock symbol")] string symbol,
        [Description("Action type")] string actionType,
        [Description("Ex-date (ISO format)")] string exDate,
        [Description("Action ratio or value")] double ratio = 1.0,
        [Description("Optional description")] string description = "")
    {
        try
        {
            var action = new CorporateActionService.CorporateAction
            {
                Symbol = symbol,
                ActionType = Enum.Parse<CorporateActionService.CorporateActionType>(actionType),
                ExDate = DateTime.Parse(exDate),
                Ratio = (decimal)ratio,
                Value = (decimal)ratio,
                Description = string.IsNullOrEmpty(description) ? $"{actionType} for {symbol}" : description
            };

            // In a real implementation, this would be saved to a database
            await _corporateActionService.UpdateCorporateActionDatabaseAsync(new List<CorporateActionService.CorporateAction> { action });

            var response = new System.Text.StringBuilder();
            response.AppendLine("Corporate Action Created");
            response.AppendLine("========================");
            response.AppendLine($"Symbol: {action.Symbol}");
            response.AppendLine($"Type: {action.ActionType}");
            response.AppendLine($"Ex-Date: {action.ExDate:yyyy-MM-dd}");
            response.AppendLine($"Ratio/Value: {action.Ratio:F4}");
            response.AppendLine($"Description: {action.Description}");

            return response.ToString();
        }
        catch (Exception ex)
        {
            return $"Error creating corporate action: {ex.Message}";
        }
    }
}