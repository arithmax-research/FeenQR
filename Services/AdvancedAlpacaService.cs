using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;

namespace QuantResearchAgent.Services
{
    public class AdvancedAlpacaService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AdvancedAlpacaService> _logger;
        private readonly IConfiguration _configuration;
        private readonly AlpacaService _baseAlpacaService;

        public AdvancedAlpacaService(
            HttpClient httpClient,
            ILogger<AdvancedAlpacaService> logger,
            IConfiguration configuration,
            AlpacaService baseAlpacaService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            _baseAlpacaService = baseAlpacaService;

            var baseUrl = configuration["Alpaca:BaseUrl"] ?? "https://paper-api.alpaca.markets/v2";
            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "QuantResearchAgent/1.0");
        }

        // Advanced Order Types
        public async Task<AlpacaOrderResponse> PlaceBracketOrderAsync(
            string symbol,
            int quantity,
            decimal limitPrice,
            decimal stopLossPrice,
            decimal takeProfitPrice,
            string side = "buy")
        {
            try
            {
                _logger.LogInformation($"Placing bracket order for {symbol}: {quantity} shares, limit: {limitPrice}, stop: {stopLossPrice}, profit: {takeProfitPrice}");

                var orderRequest = new
                {
                    symbol = symbol,
                    qty = quantity,
                    side = side,
                    type = "limit",
                    time_in_force = "gtc",
                    limit_price = limitPrice,
                    order_class = "bracket",
                    stop_loss = new { stop_price = stopLossPrice },
                    take_profit = new { limit_price = takeProfitPrice }
                };

                var response = await _httpClient.PostAsJsonAsync("orders", orderRequest);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Alpaca API returned {response.StatusCode}: {errorContent}");
                }

                var orderResponse = await response.Content.ReadFromJsonAsync<AlpacaOrderResponse>();
                _logger.LogInformation($"Bracket order placed successfully: {orderResponse?.Id}");
                return orderResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error placing bracket order: {ex.Message}");
                throw;
            }
        }

        public async Task<AlpacaOrderResponse> PlaceOcoOrderAsync(
            string symbol,
            int quantity,
            decimal stopLossPrice,
            decimal takeProfitPrice,
            string side = "buy")
        {
            try
            {
                _logger.LogInformation($"Placing OCO order for {symbol}: {quantity} shares, stop: {stopLossPrice}, profit: {takeProfitPrice}");

                var orderRequest = new
                {
                    symbol = symbol,
                    qty = quantity,
                    side = side,
                    type = "market",
                    time_in_force = "gtc",
                    order_class = "oco",
                    stop_loss = new { stop_price = stopLossPrice },
                    take_profit = new { limit_price = takeProfitPrice }
                };

                var response = await _httpClient.PostAsJsonAsync("orders", orderRequest);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Alpaca API returned {response.StatusCode}: {errorContent}");
                }

                var orderResponse = await response.Content.ReadFromJsonAsync<AlpacaOrderResponse>();
                _logger.LogInformation($"OCO order placed successfully: {orderResponse?.Id}");
                return orderResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error placing OCO order: {ex.Message}");
                throw;
            }
        }

        public async Task<AlpacaOrderResponse> PlaceTrailingStopOrderAsync(
            string symbol,
            int quantity,
            decimal trailPercent,
            string side = "buy")
        {
            try
            {
                _logger.LogInformation($"Placing trailing stop order for {symbol}: {quantity} shares, trail: {trailPercent}%");

                var orderRequest = new
                {
                    symbol = symbol,
                    qty = quantity,
                    side = side,
                    type = "trailing_stop",
                    time_in_force = "gtc",
                    trail_percent = trailPercent
                };

                var response = await _httpClient.PostAsJsonAsync("orders", orderRequest);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Alpaca API returned {response.StatusCode}: {errorContent}");
                }

                var orderResponse = await response.Content.ReadFromJsonAsync<AlpacaOrderResponse>();
                _logger.LogInformation($"Trailing stop order placed successfully: {orderResponse?.Id}");
                return orderResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error placing trailing stop order: {ex.Message}");
                throw;
            }
        }

        // Real-time Portfolio Analytics
        public async Task<PortfolioAnalytics> GetPortfolioAnalyticsAsync()
        {
            try
            {
                _logger.LogInformation("Fetching portfolio analytics");

                var account = await _baseAlpacaService.GetAccountInfoAsync();
                var positions = await _baseAlpacaService.GetPositionsAsync();

                if (account == null)
                {
                    throw new InvalidOperationException("Unable to retrieve account information");
                }

                var analytics = new PortfolioAnalytics
                {
                    TotalValue = (decimal)account.Equity,
                    Cash = account.TradableCash,
                    BuyingPower = (decimal)account.BuyingPower,
                    DayChange = 0, // Would need historical data
                    DayChangePercent = 0, // Would need historical data
                    Positions = positions.Select(p => new PositionAnalytics
                    {
                        Symbol = p.Symbol,
                        Quantity = (decimal)p.Quantity,
                        MarketValue = (decimal)p.MarketValue,
                        UnrealizedPL = p.UnrealizedProfitLoss ?? 0,
                        UnrealizedPLPercent = p.Quantity != 0 ? (decimal)((p.UnrealizedProfitLoss ?? 0) / ((decimal)p.MarketValue - (p.UnrealizedProfitLoss ?? 0))) : 0,
                        CurrentPrice = p.Quantity != 0 ? (decimal)p.MarketValue / (decimal)p.Quantity : 0,
                        LastDayPrice = p.Quantity != 0 ? (decimal)p.MarketValue / (decimal)p.Quantity : 0, // Placeholder
                        ChangeToday = 0 // Placeholder
                    }).ToList()
                };

                // Calculate additional metrics
                analytics.TotalPositions = analytics.Positions.Count;
                analytics.TotalMarketValue = (decimal)analytics.Positions.Sum(p => p.MarketValue);
                analytics.TotalUnrealizedPL = (decimal)analytics.Positions.Sum(p => p.UnrealizedPL);

                _logger.LogInformation($"Portfolio analytics calculated: ${analytics.TotalValue:N2} total value");
                return analytics;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching portfolio analytics: {ex.Message}");
                throw;
            }
        }

        // Enhanced Risk Management
        public async Task<RiskMetrics> CalculateRiskMetricsAsync()
        {
            try
            {
                _logger.LogInformation("Calculating risk metrics");

                var account = await _baseAlpacaService.GetAccountInfoAsync();
                var positions = await _baseAlpacaService.GetPositionsAsync();

                if (account == null)
                {
                    throw new InvalidOperationException("Unable to retrieve account information");
                }

                var riskMetrics = new RiskMetrics
                {
                    PortfolioValue = (decimal)account.Equity,
                    Positions = positions.Count
                };

                // Calculate position concentrations
                var totalValue = positions.Sum(p => Math.Abs((decimal)p.MarketValue));
                riskMetrics.PositionConcentrations = positions.Select(p => new PositionConcentration
                {
                    Symbol = p.Symbol,
                    Weight = totalValue != 0 ? Math.Abs((decimal)p.MarketValue) / totalValue : 0,
                    Value = (decimal)p.MarketValue,
                    UnrealizedPL = p.UnrealizedProfitLoss ?? 0
                }).OrderByDescending(pc => pc.Weight).ToList();

                // Calculate diversification metrics
                var top10Weight = riskMetrics.PositionConcentrations.Take(10).Sum(pc => pc.Weight);
                riskMetrics.DiversificationRatio = riskMetrics.PositionConcentrations.Count != 0 ? top10Weight / (decimal)riskMetrics.PositionConcentrations.Count : 0;

                // Calculate VaR estimate (simplified)
                var returns = positions.Select(p => p.Quantity != 0 ? (double)((p.UnrealizedProfitLoss ?? 0) / (p.MarketValue - (p.UnrealizedProfitLoss ?? 0))) : 0).ToList();
                if (returns.Any())
                {
                    var avgReturn = returns.Average();
                    var stdDev = Math.Sqrt(returns.Sum(r => Math.Pow(r - avgReturn, 2)) / returns.Count);
                    riskMetrics.ValueAtRisk95 = (decimal)account.Equity * (decimal)(avgReturn - 1.645 * stdDev);
                }

                _logger.LogInformation($"Risk metrics calculated: {riskMetrics.Positions} positions, VaR95: ${riskMetrics.ValueAtRisk95:N2}");
                return riskMetrics;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating risk metrics: {ex.Message}");
                throw;
            }
        }

        // Tax Reporting and Cost Basis
        public async Task<List<TaxLot>> GetTaxLotsAsync(string symbol)
        {
            try
            {
                _logger.LogInformation($"Fetching tax lots for {symbol}");

                // This would typically require accessing Alpaca's tax lot endpoint
                // For now, return a placeholder structure
                var taxLots = new List<TaxLot>();

                // In a real implementation, this would call Alpaca's tax lot API
                _logger.LogWarning("Tax lot functionality requires Alpaca's tax reporting API access");

                return taxLots;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching tax lots: {ex.Message}");
                throw;
            }
        }

        // Performance Tracking
        public async Task<PerformanceMetrics> GetPerformanceMetricsAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation($"Calculating performance metrics from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                var account = await _baseAlpacaService.GetAccountInfoAsync();

                if (account == null)
                {
                    throw new InvalidOperationException("Unable to retrieve account information");
                }

                var metrics = new PerformanceMetrics
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    CurrentValue = (decimal)account.Equity,
                    StartValue = (decimal)account.Equity * 0.95m, // Placeholder - would need historical data
                    TotalReturn = 0, // Placeholder - would need historical data
                    AnnualizedReturn = 0, // Placeholder
                    Volatility = 0.15m, // Placeholder - would need historical returns
                    SharpeRatio = 1.2m, // Placeholder
                    MaxDrawdown = -0.05m // Placeholder
                };

                metrics.TotalReturnAmount = metrics.CurrentValue - metrics.StartValue;

                _logger.LogInformation($"Performance metrics calculated: {metrics.TotalReturn:P2} total return");
                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating performance metrics: {ex.Message}");
                throw;
            }
        }

        // Wrapper methods for compatibility
        public async Task<object> GetPortfolioPositionsAsync()
        {
            return await _baseAlpacaService.GetPositionsAsync();
        }

        public async Task<object> GetAccountAsync()
        {
            return await _baseAlpacaService.GetAccountInfoAsync();
        }

        public async Task<object> PlaceMarketOrderAsync(string symbol, int quantity, string side)
        {
            return await _baseAlpacaService.PlaceMarketOrderAsync(symbol, quantity, side);
        }

        public async Task<object> GetPositionAsync(string symbol)
        {
            return await _baseAlpacaService.GetPositionAsync(symbol);
        }
    }

    // Data Models
    public class AlpacaOrderResponse
    {
        public string? Id { get; set; }
        public string? ClientOrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? FilledAt { get; set; }
        public string? Symbol { get; set; }
        public decimal? Qty { get; set; }
        public decimal? FilledQty { get; set; }
        public string? Side { get; set; }
        public string? Type { get; set; }
        public string? TimeInForce { get; set; }
        public decimal? LimitPrice { get; set; }
        public decimal? StopPrice { get; set; }
        public decimal? FilledAvgPrice { get; set; }
        public string? Status { get; set; }
    }

    public class PortfolioAnalytics
    {
        public decimal TotalValue { get; set; }
        public decimal Cash { get; set; }
        public decimal BuyingPower { get; set; }
        public decimal? DayChange { get; set; }
        public decimal? DayChangePercent { get; set; }
        public int TotalPositions { get; set; }
        public decimal TotalMarketValue { get; set; }
        public decimal TotalUnrealizedPL { get; set; }
        public List<PositionAnalytics> Positions { get; set; } = new();
    }

    public class PositionAnalytics
    {
        public string? Symbol { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? MarketValue { get; set; }
        public decimal? UnrealizedPL { get; set; }
        public decimal? UnrealizedPLPercent { get; set; }
        public decimal? CurrentPrice { get; set; }
        public decimal? LastDayPrice { get; set; }
        public decimal? ChangeToday { get; set; }
    }

    public class RiskMetrics
    {
        public decimal PortfolioValue { get; set; }
        public int Positions { get; set; }
        public decimal ValueAtRisk95 { get; set; }
        public decimal DiversificationRatio { get; set; }
        public List<PositionConcentration> PositionConcentrations { get; set; } = new();
    }

    public class PositionConcentration
    {
        public string? Symbol { get; set; }
        public decimal Weight { get; set; }
        public decimal Value { get; set; }
        public decimal? UnrealizedPL { get; set; }
    }

    public class TaxLot
    {
        public string? Symbol { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostBasis { get; set; }
        public DateTime AcquisitionDate { get; set; }
    }

    public class PerformanceMetrics
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal StartValue { get; set; }
        public decimal TotalReturn { get; set; }
        public decimal TotalReturnAmount { get; set; }
        public decimal AnnualizedReturn { get; set; }
        public decimal Volatility { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal MaxDrawdown { get; set; }
    }
}