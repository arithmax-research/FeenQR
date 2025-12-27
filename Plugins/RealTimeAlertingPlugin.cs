using System.ComponentModel;
using Microsoft.SemanticKernel;
using QuantResearchAgent.Services;

namespace QuantResearchAgent.Plugins
{
    public class RealTimeAlertingPlugin
    {
        private readonly RealTimeAlertingService _alertingService;

        public RealTimeAlertingPlugin(RealTimeAlertingService alertingService)
        {
            _alertingService = alertingService;
        }

        [KernelFunction, Description("Create a price-based alert for a stock")]
        public async Task<string> CreatePriceAlert(
            [Description("Stock symbol")] string symbol,
            [Description("Alert condition: above, below, crosses_above, crosses_below")] string condition,
            [Description("Price threshold")] decimal threshold,
            [Description("Optional custom message")] string message = null)
        {
            try
            {
                var alertId = await _alertingService.CreatePriceAlertAsync(symbol, condition, threshold, message);
                return $"Price alert created successfully: {alertId} for {symbol} {condition} {threshold:C}";
            }
            catch (Exception ex)
            {
                return $"Error creating price alert: {ex.Message}";
            }
        }

        [KernelFunction, Description("Create a technical indicator alert")]
        public async Task<string> CreateTechnicalAlert(
            [Description("Stock symbol")] string symbol,
            [Description("Technical indicator (rsi, macd, bollinger, etc.)")] string indicator,
            [Description("Alert condition: above, below")] string condition,
            [Description("Threshold value")] decimal threshold)
        {
            try
            {
                var alertId = await _alertingService.CreateTechnicalAlertAsync(symbol, indicator, condition, threshold);
                return $"Technical alert created successfully: {alertId} for {symbol} {indicator} {condition} {threshold}";
            }
            catch (Exception ex)
            {
                return $"Error creating technical alert: {ex.Message}";
            }
        }

        [KernelFunction, Description("Create a portfolio-level alert")]
        public async Task<string> CreatePortfolioAlert(
            [Description("Alert type: pnl_above, pnl_below, equity_below, drawdown_above")] string alertType,
            [Description("Threshold value")] decimal threshold,
            [Description("Optional custom message")] string message = null)
        {
            try
            {
                var alertId = await _alertingService.CreatePortfolioAlertAsync(alertType, threshold, message);
                return $"Portfolio alert created successfully: {alertId} for {alertType} {threshold:C}";
            }
            catch (Exception ex)
            {
                return $"Error creating portfolio alert: {ex.Message}";
            }
        }

        [KernelFunction, Description("Check all active alerts and return any triggered notifications")]
        public async Task<string> CheckAlerts()
        {
            try
            {
                var notifications = await _alertingService.CheckAlertsAsync();

                if (!notifications.Any())
                {
                    return "No alerts triggered";
                }

                var result = $"Alert Notifications ({notifications.Count}):\n";
                foreach (var notification in notifications)
                {
                    result += $"- {notification.TriggeredAt}: {notification.Message} " +
                             $"(Value: {notification.TriggerValue:C})\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error checking alerts: {ex.Message}";
            }
        }

        [KernelFunction, Description("Get list of all active alerts")]
        public async Task<string> GetActiveAlerts()
        {
            try
            {
                var alerts = await _alertingService.GetActiveAlertsAsync();

                if (!alerts.Any())
                {
                    return "No active alerts";
                }

                var result = $"Active Alerts ({alerts.Count}):\n";
                foreach (var alert in alerts)
                {
                    result += $"- {alert.AlertId}: {alert.AlertType} - {alert.Message} " +
                             $"(Triggers: {alert.TriggerCount})\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error getting active alerts: {ex.Message}";
            }
        }

        [KernelFunction, Description("Get recent alert notifications")]
        public async Task<string> GetRecentNotifications(
            [Description("Hours to look back")] int hours = 24)
        {
            try
            {
                var notifications = await _alertingService.GetRecentNotificationsAsync(hours);

                if (!notifications.Any())
                {
                    return $"No alert notifications in the last {hours} hours";
                }

                var result = $"Recent Notifications (last {hours} hours):\n";
                foreach (var notification in notifications)
                {
                    result += $"- {notification.TriggeredAt}: {notification.Message}\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"Error getting recent notifications: {ex.Message}";
            }
        }

        [KernelFunction, Description("Delete an alert by ID")]
        public async Task<string> DeleteAlert(
            [Description("Alert ID to delete")] string alertId)
        {
            try
            {
                var success = await _alertingService.DeleteAlertAsync(alertId);

                if (success)
                {
                    return $"Alert {alertId} deleted successfully";
                }
                else
                {
                    return "Failed to delete alert";
                }
            }
            catch (Exception ex)
            {
                return $"Error deleting alert: {ex.Message}";
            }
        }
    }
}