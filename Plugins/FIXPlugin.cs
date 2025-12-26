using Microsoft.SemanticKernel;
using QuantResearchAgent.Core;
using QuantResearchAgent.Services;
using System.ComponentModel;
using System.Threading.Tasks;

namespace QuantResearchAgent.Plugins;

/// <summary>
/// FIX (Financial Information eXchange) Protocol Plugin
/// Provides institutional trading connectivity through FIX protocol
/// </summary>
public class FIXPlugin
{
    private readonly FIXService _fixService;
    private readonly ILogger<FIXPlugin> _logger;

    public FIXPlugin(FIXService fixService, ILogger<FIXPlugin> logger)
    {
        _fixService = fixService;
        _logger = logger;
    }

    /// <summary>
    /// Connect to FIX server
    /// </summary>
    [KernelFunction, Description("Connect to a FIX protocol server for institutional trading")]
    public async Task<string> ConnectToFIXServerAsync(
        [Description("FIX server hostname")] string host = "localhost",
        [Description("FIX server port")] int port = 9876,
        [Description("Sender company ID")] string senderCompID = "QUANT_AGENT",
        [Description("Target company ID")] string targetCompID = "BROKER")
    {
        try
        {
            var success = await _fixService.ConnectAsync();
            if (success)
            {
                _logger.LogInformation("Successfully connected to FIX server at {Host}:{Port}", host, port);
                return $"Connected to FIX server at {host}:{port} with SenderCompID: {senderCompID}, TargetCompID: {targetCompID}";
            }
            else
            {
                _logger.LogWarning("Failed to connect to FIX server at {Host}:{Port}", host, port);
                return $"Failed to connect to FIX server at {host}:{port}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to FIX server");
            return $"Error connecting to FIX server: {ex.Message}";
        }
    }

    /// <summary>
    /// Disconnect from FIX server
    /// </summary>
    [KernelFunction, Description("Disconnect from the FIX protocol server")]
    public async Task<string> DisconnectFromFIXServerAsync()
    {
        try
        {
            await _fixService.DisconnectAsync();
            _logger.LogInformation("Disconnected from FIX server");
            return "Successfully disconnected from FIX server";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from FIX server");
            return $"Error disconnecting from FIX server: {ex.Message}";
        }
    }

    /// <summary>
    /// Send a new order via FIX protocol
    /// </summary>
    [KernelFunction, Description("Send a new order through FIX protocol")]
    public async Task<string> SendFIXOrderAsync(
        [Description("Stock symbol")] string symbol,
        [Description("Order side: 1=Buy, 2=Sell")] string side,
        [Description("Order type: 1=Market, 2=Limit")] string orderType,
        [Description("Order quantity")] decimal quantity,
        [Description("Limit price (optional for limit orders)")] decimal? price = null,
        [Description("Time in force: 0=Day, 1=GTC, 2=IOC, 3=FOK")] string timeInForce = "0")
    {
        try
        {
            var orderId = await _fixService.SendNewOrderAsync(symbol, side, orderType, quantity, price, timeInForce);
            if (!string.IsNullOrEmpty(orderId))
            {
                _logger.LogInformation("FIX order sent successfully: {OrderId}", orderId);
                return $"Order sent successfully. Order ID: {orderId}, Symbol: {symbol}, Side: {side}, Quantity: {quantity}";
            }
            else
            {
                _logger.LogWarning("Failed to send FIX order for {Symbol}", symbol);
                return $"Failed to send order for {symbol}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending FIX order");
            return $"Error sending FIX order: {ex.Message}";
        }
    }

    /// <summary>
    /// Cancel an existing order via FIX protocol
    /// </summary>
    [KernelFunction, Description("Cancel an existing order through FIX protocol")]
    public async Task<string> CancelFIXOrderAsync(
        [Description("Order ID to cancel")] string orderId,
        [Description("Stock symbol")] string symbol)
    {
        try
        {
            var success = await _fixService.CancelOrderAsync(orderId, symbol);
            if (success)
            {
                _logger.LogInformation("FIX order cancel request sent for {OrderId}", orderId);
                return $"Cancel request sent for Order ID: {orderId}, Symbol: {symbol}";
            }
            else
            {
                _logger.LogWarning("Failed to send FIX cancel request for {OrderId}", orderId);
                return $"Failed to cancel order {orderId}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling FIX order");
            return $"Error canceling FIX order: {ex.Message}";
        }
    }

    /// <summary>
    /// Request market data via FIX protocol
    /// </summary>
    [KernelFunction, Description("Request market data snapshot through FIX protocol")]
    public async Task<string> RequestFIXMarketDataAsync(
        [Description("Stock symbol")] string symbol)
    {
        try
        {
            var success = await _fixService.RequestMarketDataAsync(symbol);
            if (success)
            {
                _logger.LogInformation("FIX market data request sent for {Symbol}", symbol);
                return $"Market data request sent for {symbol}";
            }
            else
            {
                _logger.LogWarning("Failed to request FIX market data for {Symbol}", symbol);
                return $"Failed to request market data for {symbol}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting FIX market data");
            return $"Error requesting FIX market data: {ex.Message}";
        }
    }

    /// <summary>
    /// Send heartbeat to maintain FIX connection
    /// </summary>
    [KernelFunction, Description("Send heartbeat message to maintain FIX connection")]
    public async Task<string> SendFIXHeartbeatAsync()
    {
        try
        {
            await _fixService.SendHeartbeatAsync();
            _logger.LogInformation("FIX heartbeat sent");
            return "Heartbeat sent successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending FIX heartbeat");
            return $"Error sending heartbeat: {ex.Message}";
        }
    }

    /// <summary>
    /// Get FIX connection status
    /// </summary>
    [KernelFunction, Description("Check the current FIX connection status")]
    public string GetFIXConnectionStatus()
    {
        var isConnected = _fixService.IsConnected;
        var seqNum = _fixService.MessageSequenceNumber;

        _logger.LogInformation("FIX connection status checked: Connected={Connected}, SeqNum={SeqNum}",
            isConnected, seqNum);

        return $"FIX Connection Status: {(isConnected ? "Connected" : "Disconnected")}, Message Sequence: {seqNum}";
    }

    /// <summary>
    /// Get FIX protocol information
    /// </summary>
    [KernelFunction, Description("Get information about FIX protocol capabilities")]
    public string GetFIXProtocolInfo()
    {
        return @"
FIX (Financial Information eXchange) Protocol Information:

Supported Message Types:
- Logon (35=A): Session establishment
- Logout (35=5): Session termination
- Heartbeat (35=0): Connection maintenance
- New Order Single (35=D): Place new orders
- Order Cancel Request (35=F): Cancel existing orders
- Market Data Request (35=V): Request market data snapshots

Order Types:
- 1 = Market Order
- 2 = Limit Order

Order Sides:
- 1 = Buy
- 2 = Sell

Time in Force:
- 0 = Day
- 1 = Good Till Cancel
- 2 = Immediate or Cancel
- 3 = Fill or Kill

This plugin enables institutional-grade trading connectivity through the FIX protocol,
commonly used by professional trading firms and brokerages for high-speed, reliable order execution.
";
    }
}