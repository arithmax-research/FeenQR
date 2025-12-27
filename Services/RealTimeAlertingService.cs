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
    public class RealTimeAlertingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RealTimeAlertingService> _logger;
        private readonly IConfiguration _configuration;
        private readonly AdvancedAlpacaService _alpacaService;
        private readonly MarketDataService _marketDataService;
        private readonly TechnicalAnalysisService _technicalAnalysisService;

        public RealTimeAlertingService(
            HttpClient httpClient,
            ILogger<RealTimeAlertingService> logger,
            IConfiguration configuration,
            AdvancedAlpacaService alpacaService,
            MarketDataService marketDataService,
            TechnicalAnalysisService technicalAnalysisService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _configuration = configuration;
            _alpacaService = alpacaService;
            _marketDataService = marketDataService;
            _technicalAnalysisService = technicalAnalysisService;
        }

        public class AlertRule
        {
            public string AlertId { get; set; }
            public string AlertType { get; set; } // "price", "technical", "portfolio", "news"
            public string Symbol { get; set; }
            public string Condition { get; set; } // "above", "below", "crosses_above", "crosses_below"
            public decimal Threshold { get; set; }
            public string Message { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedAt { get; set; }
            public int TriggerCount { get; set; }
        }

        public class AlertNotification
        {
            public string NotificationId { get; set; }
            public string AlertId { get; set; }
            public string Symbol { get; set; }
            public string Message { get; set; }
            public decimal TriggerValue { get; set; }
            public DateTime TriggeredAt { get; set; }
            public bool IsRead { get; set; }
        }

        public async Task<string> CreatePriceAlertAsync(string symbol, string condition, decimal threshold, string message = null)
        {
            try
            {
                _logger.LogInformation($"Creating price alert for {symbol}: {condition} {threshold}");

                var alert = new AlertRule
                {
                    AlertId = Guid.NewGuid().ToString(),
                    AlertType = "price",
                    Symbol = symbol,
                    Condition = condition,
                    Threshold = threshold,
                    Message = message ?? $"{symbol} price {condition} {threshold}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    TriggerCount = 0
                };

                // In a real implementation, save to database
                _logger.LogInformation($"Price alert created: {alert.AlertId}");
                return alert.AlertId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating price alert: {ex.Message}");
                throw;
            }
        }

        public async Task<string> CreateTechnicalAlertAsync(string symbol, string indicator, string condition, decimal threshold)
        {
            try
            {
                _logger.LogInformation($"Creating technical alert for {symbol}: {indicator} {condition} {threshold}");

                var alert = new AlertRule
                {
                    AlertId = Guid.NewGuid().ToString(),
                    AlertType = "technical",
                    Symbol = symbol,
                    Condition = condition,
                    Threshold = threshold,
                    Message = $"{symbol} {indicator} {condition} {threshold}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    TriggerCount = 0
                };

                _logger.LogInformation($"Technical alert created: {alert.AlertId}");
                return alert.AlertId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating technical alert: {ex.Message}");
                throw;
            }
        }

        public async Task<string> CreatePortfolioAlertAsync(string alertType, decimal threshold, string message = null)
        {
            try
            {
                _logger.LogInformation($"Creating portfolio alert: {alertType} at {threshold}");

                var alert = new AlertRule
                {
                    AlertId = Guid.NewGuid().ToString(),
                    AlertType = "portfolio",
                    Symbol = "PORTFOLIO",
                    Condition = alertType, // "pnl_above", "pnl_below", "drawdown_above", etc.
                    Threshold = threshold,
                    Message = message ?? $"Portfolio {alertType} {threshold}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    TriggerCount = 0
                };

                _logger.LogInformation($"Portfolio alert created: {alert.AlertId}");
                return alert.AlertId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating portfolio alert: {ex.Message}");
                throw;
            }
        }

        public async Task<List<AlertNotification>> CheckAlertsAsync()
        {
            try
            {
                _logger.LogInformation("Checking all active alerts...");

                var notifications = new List<AlertNotification>();

                // Get active alerts (in production, load from database)
                var activeAlerts = await GetActiveAlertsAsync();

                foreach (var alert in activeAlerts)
                {
                    var notification = await CheckAlertConditionAsync(alert);
                    if (notification != null)
                    {
                        notifications.Add(notification);
                        alert.TriggerCount++;
                    }
                }

                if (notifications.Any())
                {
                    _logger.LogInformation($"Generated {notifications.Count} alert notifications");
                }

                return notifications;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking alerts: {ex.Message}");
                return new List<AlertNotification>();
            }
        }

        public async Task<List<AlertRule>> GetActiveAlertsAsync()
        {
            // In production, load from database
            return new List<AlertRule>
            {
                new AlertRule
                {
                    AlertId = "price-aapl-150",
                    AlertType = "price",
                    Symbol = "AAPL",
                    Condition = "above",
                    Threshold = 150.0m,
                    Message = "AAPL crossed above $150",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    TriggerCount = 0
                },
                new AlertRule
                {
                    AlertId = "rsi-aapl-70",
                    AlertType = "technical",
                    Symbol = "AAPL",
                    Condition = "above",
                    Threshold = 70.0m,
                    Message = "AAPL RSI above 70 (overbought)",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    TriggerCount = 0
                }
            };
        }

        public async Task<List<AlertNotification>> GetRecentNotificationsAsync(int hours = 24)
        {
            // In production, load from database
            return new List<AlertNotification>
            {
                new AlertNotification
                {
                    NotificationId = Guid.NewGuid().ToString(),
                    AlertId = "price-aapl-150",
                    Symbol = "AAPL",
                    Message = "AAPL price crossed above $150",
                    TriggerValue = 152.30m,
                    TriggeredAt = DateTime.UtcNow.AddHours(-2),
                    IsRead = false
                }
            };
        }

        public async Task<bool> DeleteAlertAsync(string alertId)
        {
            try
            {
                _logger.LogInformation($"Deleting alert: {alertId}");

                // In production, mark as inactive in database
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting alert: {ex.Message}");
                return false;
            }
        }

        private async Task<AlertNotification> CheckAlertConditionAsync(AlertRule alert)
        {
            try
            {
                switch (alert.AlertType)
                {
                    case "price":
                        return await CheckPriceAlertAsync(alert);
                    case "technical":
                        return await CheckTechnicalAlertAsync(alert);
                    case "portfolio":
                        return await CheckPortfolioAlertAsync(alert);
                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error checking alert {alert.AlertId}: {ex.Message}");
                return null;
            }
        }

        private async Task<AlertNotification> CheckPriceAlertAsync(AlertRule alert)
        {
            var currentPrice = await _marketDataService.GetRealTimeQuoteAsync(alert.Symbol);

            if (currentPrice == null) return null;

            // Extract price from quote (simplified)
            var price = 150.0m; // Placeholder - would extract from currentPrice

            bool isTriggered = false;

            switch (alert.Condition)
            {
                case "above":
                    isTriggered = price > alert.Threshold;
                    break;
                case "below":
                    isTriggered = price < alert.Threshold;
                    break;
                case "crosses_above":
                    // Would need previous price to detect crossing
                    isTriggered = price > alert.Threshold;
                    break;
                case "crosses_below":
                    isTriggered = price < alert.Threshold;
                    break;
            }

            if (isTriggered)
            {
                return new AlertNotification
                {
                    NotificationId = Guid.NewGuid().ToString(),
                    AlertId = alert.AlertId,
                    Symbol = alert.Symbol,
                    Message = alert.Message,
                    TriggerValue = price,
                    TriggeredAt = DateTime.UtcNow,
                    IsRead = false
                };
            }

            return null;
        }

        private async Task<AlertNotification> CheckTechnicalAlertAsync(AlertRule alert)
        {
            // Get technical indicators
            var technicalData = await _technicalAnalysisService.GetTechnicalIndicatorsAsync(alert.Symbol, 100);

            if (technicalData == null) return null;

            // Check RSI condition as example
            if (alert.Condition.Contains("rsi") && technicalData.RSI.HasValue)
            {
                var rsi = technicalData.RSI.Value;
                bool isTriggered = false;

                if (alert.Condition.Contains("above"))
                    isTriggered = rsi > alert.Threshold;
                else if (alert.Condition.Contains("below"))
                    isTriggered = rsi < alert.Threshold;

                if (isTriggered)
                {
                    return new AlertNotification
                    {
                        NotificationId = Guid.NewGuid().ToString(),
                        AlertId = alert.AlertId,
                        Symbol = alert.Symbol,
                        Message = $"{alert.Symbol} RSI: {rsi:F2} {alert.Condition} {alert.Threshold}",
                        TriggerValue = rsi,
                        TriggeredAt = DateTime.UtcNow,
                        IsRead = false
                    };
                }
            }

            return null;
        }

        private async Task<AlertNotification> CheckPortfolioAlertAsync(AlertRule alert)
        {
            var account = await _alpacaService.GetAccountAsync();

            if (account == null) return null;

            decimal value = 0;
            bool isTriggered = false;

            switch (alert.Condition)
            {
                case "pnl_above":
                    value = account.Equity - account.LastEquity;
                    isTriggered = value > alert.Threshold;
                    break;
                case "pnl_below":
                    value = account.Equity - account.LastEquity;
                    isTriggered = value < alert.Threshold;
                    break;
                case "equity_below":
                    value = account.Equity;
                    isTriggered = value < alert.Threshold;
                    break;
            }

            if (isTriggered)
            {
                return new AlertNotification
                {
                    NotificationId = Guid.NewGuid().ToString(),
                    AlertId = alert.AlertId,
                    Symbol = "PORTFOLIO",
                    Message = $"{alert.Condition}: {value:C2} (Threshold: {alert.Threshold:C2})",
                    TriggerValue = value,
                    TriggeredAt = DateTime.UtcNow,
                    IsRead = false
                };
            }

            return null;
        }
    }
}
