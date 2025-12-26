using Microsoft.Extensions.Logging;
using QuantResearchAgent.Core;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace QuantResearchAgent.Services;

/// <summary>
/// FIX (Financial Information eXchange) Protocol Service
/// Implements FIX protocol for institutional trading connectivity
/// </summary>
public class FIXService
{
    private readonly ILogger<FIXService> _logger;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly string _host;
    private readonly int _port;
    private readonly string _senderCompID;
    private readonly string _targetCompID;
    private int _msgSeqNum = 1;
    private bool _isConnected = false;

    public FIXService(
        ILogger<FIXService> logger,
        string host = "localhost",
        int port = 9876,
        string senderCompID = "QUANT_AGENT",
        string targetCompID = "BROKER")
    {
        _logger = logger;
        _host = host;
        _port = port;
        _senderCompID = senderCompID;
        _targetCompID = targetCompID;
    }

    /// <summary>
    /// Connect to FIX server
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port);
            _stream = _client.GetStream();
            _isConnected = true;

            // Send Logon message
            await SendLogonMessageAsync();

            _logger.LogInformation("Connected to FIX server at {Host}:{Port}", _host, _port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to FIX server");
            return false;
        }
    }

    /// <summary>
    /// Disconnect from FIX server
    /// </summary>
    public async Task DisconnectAsync()
    {
        try
        {
            if (_stream != null)
            {
                await SendLogoutMessageAsync();
                _stream.Close();
            }
            _client?.Close();
            _isConnected = false;
            _logger.LogInformation("Disconnected from FIX server");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
        }
    }

    /// <summary>
    /// Send New Order Single message
    /// </summary>
    public async Task<string> SendNewOrderAsync(
        string symbol,
        string side, // "1"=Buy, "2"=Sell
        string orderType, // "1"=Market, "2"=Limit
        decimal quantity,
        decimal? price = null,
        string timeInForce = "0") // "0"=Day
    {
        try
        {
            var message = BuildNewOrderMessage(symbol, side, orderType, quantity, price, timeInForce);
            await SendMessageAsync(message);

            var orderId = GenerateOrderID();
            _logger.LogInformation("Sent new order: {OrderId} for {Symbol}", orderId, symbol);
            return orderId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send new order");
            return string.Empty;
        }
    }

    /// <summary>
    /// Send Order Cancel Request
    /// </summary>
    public async Task<bool> CancelOrderAsync(string orderId, string symbol)
    {
        try
        {
            var message = BuildCancelOrderMessage(orderId, symbol);
            await SendMessageAsync(message);

            _logger.LogInformation("Sent cancel request for order: {OrderId}", orderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order");
            return false;
        }
    }

    /// <summary>
    /// Request market data snapshot
    /// </summary>
    public async Task<bool> RequestMarketDataAsync(string symbol)
    {
        try
        {
            var message = BuildMarketDataRequestMessage(symbol);
            await SendMessageAsync(message);

            _logger.LogInformation("Requested market data for: {Symbol}", symbol);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request market data");
            return false;
        }
    }

    /// <summary>
    /// Send heartbeat message
    /// </summary>
    public async Task SendHeartbeatAsync()
    {
        try
        {
            var message = BuildHeartbeatMessage();
            await SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send heartbeat");
        }
    }

    private async Task SendLogonMessageAsync()
    {
        var message = new StringBuilder();
        message.Append("8=FIX.4.4|"); // BeginString
        message.Append("9=000|"); // BodyLength (placeholder)
        message.Append("35=A|"); // MsgType (Logon)
        message.Append($"34={_msgSeqNum++}|"); // MsgSeqNum
        message.Append($"49={_senderCompID}|"); // SenderCompID
        message.Append($"56={_targetCompID}|"); // TargetCompID
        message.Append("52=" + DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss") + "|"); // SendingTime
        message.Append("98=0|"); // EncryptMethod (None)
        message.Append("108=30|"); // HeartBtInt
        message.Append("10=000|"); // CheckSum (placeholder)

        await SendMessageAsync(message.ToString());
    }

    private async Task SendLogoutMessageAsync()
    {
        var message = BuildBasicMessage("5"); // Logout
        await SendMessageAsync(message);
    }

    private string BuildNewOrderMessage(string symbol, string side, string orderType,
        decimal quantity, decimal? price, string timeInForce)
    {
        var message = new StringBuilder();
        message.Append("8=FIX.4.4|9=000|35=D|"); // New Order Single
        message.Append($"34={_msgSeqNum++}|");
        message.Append($"49={_senderCompID}|");
        message.Append($"56={_targetCompID}|");
        message.Append("52=" + DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss") + "|");
        message.Append($"11={GenerateOrderID()}|"); // ClOrdID
        message.Append($"55={symbol}|"); // Symbol
        message.Append($"54={side}|"); // Side
        message.Append("60=" + DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss") + "|"); // TransactTime
        message.Append($"40={orderType}|"); // OrdType
        message.Append($"38={quantity}|"); // OrderQty

        if (price.HasValue)
            message.Append($"44={price.Value}|"); // Price

        message.Append($"59={timeInForce}|"); // TimeInForce
        message.Append("10=000|");

        return message.ToString();
    }

    private string BuildCancelOrderMessage(string orderId, string symbol)
    {
        var message = new StringBuilder();
        message.Append("8=FIX.4.4|9=000|35=F|"); // Order Cancel Request
        message.Append($"34={_msgSeqNum++}|");
        message.Append($"49={_senderCompID}|");
        message.Append($"56={_targetCompID}|");
        message.Append("52=" + DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss") + "|");
        message.Append($"41={orderId}|"); // OrigClOrdID
        message.Append($"11={GenerateOrderID()}|"); // ClOrdID
        message.Append($"55={symbol}|"); // Symbol
        message.Append("54=1|"); // Side (placeholder)
        message.Append("60=" + DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss") + "|");
        message.Append("10=000|");

        return message.ToString();
    }

    private string BuildMarketDataRequestMessage(string symbol)
    {
        var message = new StringBuilder();
        message.Append("8=FIX.4.4|9=000|35=V|"); // Market Data Request
        message.Append($"34={_msgSeqNum++}|");
        message.Append($"49={_senderCompID}|");
        message.Append($"56={_targetCompID}|");
        message.Append("52=" + DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss") + "|");
        message.Append("262=" + Guid.NewGuid().ToString().Substring(0, 8) + "|"); // MDReqID
        message.Append("263=0|"); // SubscriptionRequestType (Snapshot)
        message.Append("264=1|"); // MarketDepth
        message.Append("265=0|"); // MDUpdateType
        message.Append("146=1|"); // NoRelatedSym
        message.Append($"55={symbol}|"); // Symbol
        message.Append("10=000|");

        return message.ToString();
    }

    private string BuildHeartbeatMessage()
    {
        return BuildBasicMessage("0"); // Heartbeat
    }

    private string BuildBasicMessage(string msgType)
    {
        var message = new StringBuilder();
        message.Append("8=FIX.4.4|9=000|");
        message.Append($"35={msgType}|");
        message.Append($"34={_msgSeqNum++}|");
        message.Append($"49={_senderCompID}|");
        message.Append($"56={_targetCompID}|");
        message.Append("52=" + DateTime.UtcNow.ToString("yyyyMMdd-HH:mm:ss") + "|");
        message.Append("10=000|");

        return message.ToString();
    }

    private async Task SendMessageAsync(string message)
    {
        if (_stream == null || !_isConnected)
            throw new InvalidOperationException("Not connected to FIX server");

        // Calculate and update body length
        var parts = message.Split('|');
        var bodyStart = message.IndexOf("35=") - 1; // Start after 9=
        var bodyLength = message.Length - bodyStart - 7; // Subtract |10=000|
        parts[1] = $"9={bodyLength:D3}";

        // Calculate checksum
        var checksum = CalculateChecksum(string.Join("|", parts));
        parts[parts.Length - 1] = $"10={checksum:D3}";

        var finalMessage = string.Join("|", parts) + "\u0001"; // SOH delimiter

        var data = Encoding.ASCII.GetBytes(finalMessage);
        await _stream.WriteAsync(data, 0, data.Length);
    }

    private int CalculateChecksum(string message)
    {
        int sum = 0;
        foreach (char c in message)
        {
            if (c != '|') sum += (int)c;
        }
        return sum % 256;
    }

    private string GenerateOrderID()
    {
        return $"ORD_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}";
    }

    /// <summary>
    /// Get connection status
    /// </summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// Get current message sequence number
    /// </summary>
    public int MessageSequenceNumber => _msgSeqNum;
}