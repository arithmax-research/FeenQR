using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class AdvancedAlpacaPlugin
    {
        private readonly AdvancedAlpacaService _advancedAlpacaService;

        public AdvancedAlpacaPlugin(AdvancedAlpacaService advancedAlpacaService)
        {
            _advancedAlpacaService = advancedAlpacaService;
        }

        [KernelFunction("place_bracket_order")]
        [Description("Places a bracket order with stop loss and take profit levels")]
        public async Task<string> PlaceBracketOrderAsync(
            [Description("Stock symbol (e.g., AAPL)")] string symbol,
            [Description("Number of shares")] int quantity,
            [Description("Limit price for entry")] decimal limitPrice,
            [Description("Stop loss price")] decimal stopLossPrice,
            [Description("Take profit price")] decimal takeProfitPrice,
            [Description("Order side: buy or sell")] string side = "buy")
        {
            try
            {
                var order = await _advancedAlpacaService.PlaceBracketOrderAsync(
                    symbol, quantity, limitPrice, stopLossPrice, takeProfitPrice, side);

                return $"Bracket order placed successfully:\n" +
                       $"Order ID: {order.Id}\n" +
                       $"Symbol: {order.Symbol}\n" +
                       $"Quantity: {order.Qty}\n" +
                       $"Side: {order.Side}\n" +
                       $"Status: {order.Status}\n" +
                       $"Limit Price: ${order.LimitPrice}\n" +
                       $"Stop Loss: ${order.StopPrice}";
            }
            catch (System.Exception ex)
            {
                return $"Error placing bracket order: {ex.Message}";
            }
        }

        [KernelFunction("place_oco_order")]
        [Description("Places a one-cancels-other order with stop loss and take profit")]
        public async Task<string> PlaceOcoOrderAsync(
            [Description("Stock symbol (e.g., AAPL)")] string symbol,
            [Description("Number of shares")] int quantity,
            [Description("Stop loss price")] decimal stopLossPrice,
            [Description("Take profit price")] decimal takeProfitPrice,
            [Description("Order side: buy or sell")] string side = "buy")
        {
            try
            {
                var order = await _advancedAlpacaService.PlaceOcoOrderAsync(
                    symbol, quantity, stopLossPrice, takeProfitPrice, side);

                return $"OCO order placed successfully:\n" +
                       $"Order ID: {order.Id}\n" +
                       $"Symbol: {order.Symbol}\n" +
                       $"Quantity: {order.Qty}\n" +
                       $"Side: {order.Side}\n" +
                       $"Status: {order.Status}\n" +
                       $"Stop Loss: ${order.StopPrice}";
            }
            catch (System.Exception ex)
            {
                return $"Error placing OCO order: {ex.Message}";
            }
        }

        [KernelFunction("place_trailing_stop_order")]
        [Description("Places a trailing stop order that follows price movements")]
        public async Task<string> PlaceTrailingStopOrderAsync(
            [Description("Stock symbol (e.g., AAPL)")] string symbol,
            [Description("Number of shares")] int quantity,
            [Description("Trailing percentage (e.g., 5.0 for 5%)")] decimal trailPercent,
            [Description("Order side: buy or sell")] string side = "buy")
        {
            try
            {
                var order = await _advancedAlpacaService.PlaceTrailingStopOrderAsync(
                    symbol, quantity, trailPercent, side);

                return $"Trailing stop order placed successfully:\n" +
                       $"Order ID: {order.Id}\n" +
                       $"Symbol: {order.Symbol}\n" +
                       $"Quantity: {order.Qty}\n" +
                       $"Side: {order.Side}\n" +
                       $"Status: {order.Status}\n" +
                       $"Trail Percent: {trailPercent}%";
            }
            catch (System.Exception ex)
            {
                return $"Error placing trailing stop order: {ex.Message}";
            }
        }

        [KernelFunction("get_portfolio_analytics")]
        [Description("Gets comprehensive portfolio analytics including positions and performance")]
        public async Task<string> GetPortfolioAnalyticsAsync()
        {
            try
            {
                var analytics = await _advancedAlpacaService.GetPortfolioAnalyticsAsync();

                var result = $"Portfolio Analytics:\n" +
                           $"Total Value: ${analytics.TotalValue:N2}\n" +
                           $"Cash: ${analytics.Cash:N2}\n" +
                           $"Buying Power: ${analytics.BuyingPower:N2}\n" +
                           $"Day Change: ${analytics.DayChange:N2} ({analytics.DayChangePercent:P2})\n" +
                           $"Total Positions: {analytics.TotalPositions}\n" +
                           $"Total Market Value: ${analytics.TotalMarketValue:N2}\n" +
                           $"Total Unrealized P&L: ${analytics.TotalUnrealizedPL:N2}\n\n" +
                           "Positions:\n";

                foreach (var position in analytics.Positions)
                {
                    result += $"{position.Symbol}: {position.Quantity} shares @ ${position.CurrentPrice:N2} = ${position.MarketValue:N2}\n" +
                             $"  P&L: ${position.UnrealizedPL:N2} ({position.UnrealizedPLPercent:P2})\n" +
                             $"  Change Today: ${position.ChangeToday:N2}\n\n";
                }

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error getting portfolio analytics: {ex.Message}";
            }
        }

        [KernelFunction("calculate_risk_metrics")]
        [Description("Calculates portfolio risk metrics including VaR and diversification")]
        public async Task<string> CalculateRiskMetricsAsync()
        {
            try
            {
                var riskMetrics = await _advancedAlpacaService.CalculateRiskMetricsAsync();

                var result = $"Risk Metrics:\n" +
                           $"Portfolio Value: ${riskMetrics.PortfolioValue:N2}\n" +
                           $"Number of Positions: {riskMetrics.Positions}\n" +
                           $"Value at Risk (95%): ${riskMetrics.ValueAtRisk95:N2}\n" +
                           $"Diversification Ratio: {riskMetrics.DiversificationRatio:P2}\n\n" +
                           "Top Position Concentrations:\n";

                foreach (var concentration in riskMetrics.PositionConcentrations.Take(10))
                {
                    result += $"{concentration.Symbol}: {concentration.Weight:P2} (${concentration.Value:N2})\n" +
                             $"  Unrealized P&L: ${concentration.UnrealizedPL:N2}\n";
                }

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error calculating risk metrics: {ex.Message}";
            }
        }

        [KernelFunction("get_performance_metrics")]
        [Description("Gets portfolio performance metrics over a specified period")]
        public async Task<string> GetPerformanceMetricsAsync(
            [Description("Start date (YYYY-MM-DD)")] string startDate,
            [Description("End date (YYYY-MM-DD)")] string endDate)
        {
            try
            {
                var start = System.DateTime.Parse(startDate);
                var end = System.DateTime.Parse(endDate);

                var metrics = await _advancedAlpacaService.GetPerformanceMetricsAsync(start, end);

                return $"Performance Metrics ({start:yyyy-MM-dd} to {end:yyyy-MM-dd}):\n" +
                       $"Starting Value: ${metrics.StartValue:N2}\n" +
                       $"Current Value: ${metrics.CurrentValue:N2}\n" +
                       $"Total Return: ${metrics.TotalReturnAmount:N2} ({metrics.TotalReturn:P2})\n" +
                       $"Annualized Return: {metrics.AnnualizedReturn:P2}\n" +
                       $"Volatility: {metrics.Volatility:P2}\n" +
                       $"Sharpe Ratio: {metrics.SharpeRatio:N2}\n" +
                       $"Max Drawdown: {metrics.MaxDrawdown:P2}";
            }
            catch (System.Exception ex)
            {
                return $"Error getting performance metrics: {ex.Message}";
            }
        }

        [KernelFunction("get_tax_lots")]
        [Description("Gets tax lot information for a specific symbol (requires Alpaca tax reporting API)")]
        public async Task<string> GetTaxLotsAsync(
            [Description("Stock symbol (e.g., AAPL)")] string symbol)
        {
            try
            {
                var taxLots = await _advancedAlpacaService.GetTaxLotsAsync(symbol);

                if (!taxLots.Any())
                {
                    return $"No tax lot information available for {symbol}. This feature requires Alpaca's tax reporting API access.";
                }

                var result = $"Tax Lots for {symbol}:\n";
                foreach (var lot in taxLots)
                {
                    result += $"Quantity: {lot.Quantity}, Cost Basis: ${lot.CostBasis:N2}, Acquired: {lot.AcquisitionDate:yyyy-MM-dd}\n";
                }

                return result;
            }
            catch (System.Exception ex)
            {
                return $"Error getting tax lots: {ex.Message}";
            }
        }

        [KernelFunction("analyze_portfolio_strategy")]
        [Description("Analyzes current portfolio against a given investment strategy")]
        public async Task<string> AnalyzePortfolioStrategyAsync(
            [Description("Investment strategy to analyze against (e.g., growth, value, dividend)")] string strategy)
        {
            try
            {
                var analytics = await _advancedAlpacaService.GetPortfolioAnalyticsAsync();
                var riskMetrics = await _advancedAlpacaService.CalculateRiskMetricsAsync();

                var analysis = $"Portfolio Analysis for {strategy} Strategy:\n\n" +
                             "Current Portfolio:\n" +
                             $"- Total Value: ${analytics.TotalValue:N2}\n" +
                             $"- Number of Positions: {analytics.TotalPositions}\n" +
                             $"- Cash Allocation: {analytics.Cash / analytics.TotalValue:P2}\n" +
                             $"- Diversification Ratio: {riskMetrics.DiversificationRatio:P2}\n\n";

                // Strategy-specific analysis
                switch (strategy.ToLower())
                {
                    case "growth":
                        analysis += "Growth Strategy Analysis:\n" +
                                  "- Focus on high-growth technology and innovation stocks\n" +
                                  "- Higher volatility expected\n" +
                                  "- Consider concentration in top performers\n" +
                                  $"- Current VaR (95%): ${riskMetrics.ValueAtRisk95:N2}\n";
                        break;

                    case "value":
                        analysis += "Value Strategy Analysis:\n" +
                                  "- Focus on undervalued companies with strong fundamentals\n" +
                                  "- Lower volatility preferred\n" +
                                  "- Consider increasing diversification\n" +
                                  $"- Current diversification: {riskMetrics.DiversificationRatio:P2}\n";
                        break;

                    case "dividend":
                        analysis += "Dividend Strategy Analysis:\n" +
                                  "- Focus on stable companies with consistent dividends\n" +
                                  "- Lower risk tolerance\n" +
                                  "- Consider yield optimization\n" +
                                  $"- Current cash position: ${analytics.Cash:N2}\n";
                        break;

                    default:
                        analysis += $"General Strategy Analysis for {strategy}:\n" +
                                  "- Monitor risk metrics regularly\n" +
                                  "- Maintain appropriate diversification\n" +
                                  "- Align with investment objectives\n";
                        break;
                }

                return analysis;
            }
            catch (System.Exception ex)
            {
                return $"Error analyzing portfolio strategy: {ex.Message}";
            }
        }
    }
}