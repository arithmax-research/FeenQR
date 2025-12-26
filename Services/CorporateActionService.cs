using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services;

/// <summary>
/// Service for processing corporate actions (splits, dividends, mergers, etc.)
/// </summary>
public class CorporateActionService
{
    private readonly ILogger<CorporateActionService> _logger;

    public CorporateActionService(ILogger<CorporateActionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Types of corporate actions
    /// </summary>
    public enum CorporateActionType
    {
        StockSplit,
        StockDividend,
        CashDividend,
        RightsOffering,
        Merger,
        Acquisition,
        SpinOff,
        ShareRepurchase,
        ShareIssuance
    }

    /// <summary>
    /// Corporate action record
    /// </summary>
    public class CorporateAction
    {
        public string Symbol { get; set; } = string.Empty;
        public CorporateActionType ActionType { get; set; }
        public DateTime ExDate { get; set; }
        public DateTime? RecordDate { get; set; }
        public DateTime? PaymentDate { get; set; }
        public decimal Ratio { get; set; } // For splits (e.g., 2.0 for 2:1 split)
        public decimal Value { get; set; } // Cash amount for dividends
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    /// <summary>
    /// Price adjustment result
    /// </summary>
    public class PriceAdjustmentResult
    {
        public Dictionary<DateTime, decimal> AdjustedPrices { get; set; } = new();
        public List<CorporateAction> AppliedActions { get; set; } = new();
        public decimal TotalAdjustmentFactor { get; set; }
        public string AdjustmentSummary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Process corporate actions for historical price data
    /// </summary>
    public async Task<PriceAdjustmentResult> ProcessCorporateActionsAsync(
        string symbol,
        Dictionary<DateTime, decimal> prices,
        List<CorporateAction> actions)
    {
        var result = new PriceAdjustmentResult();

        try
        {
            // Sort actions by ex-date
            var sortedActions = actions
                .Where(a => a.Symbol == symbol)
                .OrderBy(a => a.ExDate)
                .ToList();

            result.AppliedActions = sortedActions;

            // Start with original prices
            var adjustedPrices = new Dictionary<DateTime, decimal>(prices);
            decimal cumulativeFactor = 1.0m;

            foreach (var action in sortedActions)
            {
                adjustedPrices = await ApplyCorporateActionAsync(adjustedPrices, action);
                cumulativeFactor *= GetAdjustmentFactor(action);
                result.AdjustmentSummary += $"{action.ActionType} on {action.ExDate:yyyy-MM-dd}: {GetActionDescription(action)}\n";
            }

            result.AdjustedPrices = adjustedPrices;
            result.TotalAdjustmentFactor = cumulativeFactor;

            _logger.LogInformation($"Processed {sortedActions.Count} corporate actions for {symbol}. Total adjustment factor: {cumulativeFactor:F4}");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing corporate actions for {symbol}");
            // Return original prices if processing fails
            result.AdjustedPrices = new Dictionary<DateTime, decimal>(prices);
            result.AdjustmentSummary = "Error processing corporate actions - using original prices";
        }

        return result;
    }

    /// <summary>
    /// Apply a single corporate action to price data
    /// </summary>
    private async Task<Dictionary<DateTime, decimal>> ApplyCorporateActionAsync(
        Dictionary<DateTime, decimal> prices,
        CorporateAction action)
    {
        return await Task.Run(() =>
        {
            var adjustedPrices = new Dictionary<DateTime, decimal>();

            switch (action.ActionType)
            {
                case CorporateActionType.StockSplit:
                    return ApplyStockSplit(prices, action);

                case CorporateActionType.StockDividend:
                    return ApplyStockDividend(prices, action);

                case CorporateActionType.CashDividend:
                    // Cash dividends don't affect historical prices
                    return new Dictionary<DateTime, decimal>(prices);

                case CorporateActionType.RightsOffering:
                    return ApplyRightsOffering(prices, action);

                case CorporateActionType.Merger:
                case CorporateActionType.Acquisition:
                    return ApplyMergerAcquisition(prices, action);

                case CorporateActionType.SpinOff:
                    return ApplySpinOff(prices, action);

                default:
                    _logger.LogWarning($"Unsupported corporate action type: {action.ActionType}");
                    return new Dictionary<DateTime, decimal>(prices);
            }
        });
    }

    /// <summary>
    /// Apply stock split adjustment
    /// </summary>
    private Dictionary<DateTime, decimal> ApplyStockSplit(
        Dictionary<DateTime, decimal> prices,
        CorporateAction action)
    {
        var adjustedPrices = new Dictionary<DateTime, decimal>();

        foreach (var kvp in prices)
        {
            var date = kvp.Key;
            var price = kvp.Value;

            // Adjust prices before ex-date
            if (date < action.ExDate)
            {
                adjustedPrices[date] = price / action.Ratio;
            }
            else
            {
                adjustedPrices[date] = price;
            }
        }

        return adjustedPrices;
    }

    /// <summary>
    /// Apply stock dividend adjustment
    /// </summary>
    private Dictionary<DateTime, decimal> ApplyStockDividend(
        Dictionary<DateTime, decimal> prices,
        CorporateAction action)
    {
        var adjustedPrices = new Dictionary<DateTime, decimal>();
        var dividendRatio = action.Ratio;

        foreach (var kvp in prices)
        {
            var date = kvp.Key;
            var price = kvp.Value;

            // Adjust prices before ex-date
            if (date < action.ExDate)
            {
                adjustedPrices[date] = price / (1 + dividendRatio);
            }
            else
            {
                adjustedPrices[date] = price;
            }
        }

        return adjustedPrices;
    }

    /// <summary>
    /// Apply rights offering adjustment
    /// </summary>
    private Dictionary<DateTime, decimal> ApplyRightsOffering(
        Dictionary<DateTime, decimal> prices,
        CorporateAction action)
    {
        var adjustedPrices = new Dictionary<DateTime, decimal>();
        var dilutionRatio = action.Ratio; // Dilution factor

        foreach (var kvp in prices)
        {
            var date = kvp.Key;
            var price = kvp.Value;

            // Adjust prices before ex-date
            if (date < action.ExDate)
            {
                adjustedPrices[date] = price * (1 - dilutionRatio);
            }
            else
            {
                adjustedPrices[date] = price;
            }
        }

        return adjustedPrices;
    }

    /// <summary>
    /// Apply merger/acquisition adjustment
    /// </summary>
    private Dictionary<DateTime, decimal> ApplyMergerAcquisition(
        Dictionary<DateTime, decimal> prices,
        CorporateAction action)
    {
        var adjustedPrices = new Dictionary<DateTime, decimal>();
        var exchangeRatio = action.Ratio; // Exchange ratio

        foreach (var kvp in prices)
        {
            var date = kvp.Key;
            var price = kvp.Value;

            // Adjust prices before ex-date
            if (date < action.ExDate)
            {
                adjustedPrices[date] = price * exchangeRatio;
            }
            else
            {
                adjustedPrices[date] = price;
            }
        }

        return adjustedPrices;
    }

    /// <summary>
    /// Apply spin-off adjustment
    /// </summary>
    private Dictionary<DateTime, decimal> ApplySpinOff(
        Dictionary<DateTime, decimal> prices,
        CorporateAction action)
    {
        var adjustedPrices = new Dictionary<DateTime, decimal>();
        var distributionRatio = action.Ratio; // Distribution ratio

        foreach (var kvp in prices)
        {
            var date = kvp.Key;
            var price = kvp.Value;

            // Adjust prices before ex-date
            if (date < action.ExDate)
            {
                adjustedPrices[date] = price / (1 + distributionRatio);
            }
            else
            {
                adjustedPrices[date] = price;
            }
        }

        return adjustedPrices;
    }

    /// <summary>
    /// Get adjustment factor for a corporate action
    /// </summary>
    private decimal GetAdjustmentFactor(CorporateAction action)
    {
        switch (action.ActionType)
        {
            case CorporateActionType.StockSplit:
                return 1 / action.Ratio;

            case CorporateActionType.StockDividend:
                return 1 / (1 + action.Ratio);

            case CorporateActionType.CashDividend:
                return 1.0m; // No price adjustment

            case CorporateActionType.RightsOffering:
                return 1 - action.Ratio;

            case CorporateActionType.Merger:
            case CorporateActionType.Acquisition:
                return action.Ratio;

            case CorporateActionType.SpinOff:
                return 1 / (1 + action.Ratio);

            default:
                return 1.0m;
        }
    }

    /// <summary>
    /// Get human-readable description of corporate action
    /// </summary>
    private string GetActionDescription(CorporateAction action)
    {
        switch (action.ActionType)
        {
            case CorporateActionType.StockSplit:
                return $"{action.Ratio}:1 stock split";

            case CorporateActionType.StockDividend:
                return $"{action.Ratio * 100:F1}% stock dividend";

            case CorporateActionType.CashDividend:
                return $"${action.Value:F2} cash dividend";

            case CorporateActionType.RightsOffering:
                return $"Rights offering (dilution: {action.Ratio:P2})";

            case CorporateActionType.Merger:
                return $"Merger (exchange ratio: {action.Ratio:F4})";

            case CorporateActionType.Acquisition:
                return $"Acquisition (exchange ratio: {action.Ratio:F4})";

            case CorporateActionType.SpinOff:
                return $"Spin-off (distribution ratio: {action.Ratio:F4})";

            default:
                return action.Description;
        }
    }

    /// <summary>
    /// Detect potential corporate actions from price data
    /// </summary>
    public async Task<List<CorporateAction>> DetectCorporateActionsAsync(
        string symbol,
        Dictionary<DateTime, decimal> prices)
    {
        var detectedActions = new List<CorporateAction>();

        await Task.Run(() =>
        {
            var sortedPrices = prices.OrderBy(p => p.Key).ToList();

            // Detect potential stock splits (large price drops)
            for (int i = 1; i < sortedPrices.Count; i++)
            {
                var currentPrice = sortedPrices[i].Value;
                var previousPrice = sortedPrices[i - 1].Value;

                if (previousPrice > 0 && currentPrice > 0)
                {
                    var ratio = previousPrice / currentPrice;

                    // Check for common split ratios (2:1, 3:1, 4:1, etc.)
                    var commonRatios = new[] { 2.0m, 3.0m, 4.0m, 5.0m, 10.0m };
                    var closestRatio = commonRatios.OrderBy(r => Math.Abs(r - ratio)).First();

                    if (Math.Abs(ratio - closestRatio) / closestRatio < 0.05m) // Within 5%
                    {
                        detectedActions.Add(new CorporateAction
                        {
                            Symbol = symbol,
                            ActionType = CorporateActionType.StockSplit,
                            ExDate = sortedPrices[i].Key,
                            Ratio = closestRatio,
                            Description = $"Potential {closestRatio}:1 stock split detected"
                        });
                    }
                }
            }

            // Additional detection logic can be added for other action types
        });

        return detectedActions;
    }

    /// <summary>
    /// Maintain historical corporate action database
    /// </summary>
    public async Task UpdateCorporateActionDatabaseAsync(List<CorporateAction> newActions)
    {
        // In a real implementation, this would save to a database
        // For now, just log the actions
        foreach (var action in newActions)
        {
            _logger.LogInformation($"Corporate Action: {action.Symbol} - {action.ActionType} on {action.ExDate:yyyy-MM-dd}");
        }

        await Task.CompletedTask;
    }
}