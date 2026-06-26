using System;
using System.Threading;
using System.Threading.Tasks;
using EAP.Core;
using Microsoft.Extensions.Options;
using Secs4Net;

namespace EAP.Adapters.Hsms;

public class HsmsClient : ProtocolClientBase
{
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private SecsGem? _secsGem;
    private HsmsConnection? _connection;
    private bool _isConnected;
    private CancellationTokenSource? _messageLoopCts;
    private CancellationTokenSource? _connectionCts;

    public override ProtocolType ProtocolType => ProtocolType.Hsms;
    public override bool IsConnected => _isConnected;

    public HsmsClient(DeviceConfig config) : base(config)
    {
        if (config.HsmsConfig == null)
        {
            throw new ArgumentException("HSMS configuration is required", nameof(config));
        }
    }

    public override async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected)
            {
                DeviceLogger.Warn(_deviceConfig.DeviceId, $"HSMS client already connected: {ConnectionId}");
                return true;
            }

            var config = _deviceConfig.HsmsConfig!;
            int deviceId = config.DeviceId > 0 ? config.DeviceId : 1;
            
            DeviceLogger.Info(_deviceConfig.DeviceId, $"Connecting to HSMS server: {config.Host}:{config.Port}, Device ID: {deviceId}, Mode: {config.Mode}, ConnectionMode: {config.ConnectionMode}");

            try
            {
                var options = Options.Create(new SecsGemOptions
                {
                    IpAddress = config.Host!,
                    Port = config.Port,
                    DeviceId = (ushort)deviceId,
                    IsActive = config.ConnectionMode == HsmsConnectionMode.Active,
                    T3 = config.T3Timeout,
                    T5 = config.T5Timeout,
                    T6 = config.T6Timeout,
                    T7 = config.T7Timeout,
                    T8 = config.T8Timeout,
                    LinkTestInterval = config.LinkTestInterval
                });

                var logger = new SecsGemLogger(_deviceConfig.DeviceId);
                _connection = new HsmsConnection(options, logger);
                _secsGem = new SecsGem(options, _connection, logger);

                _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _connectionCts.CancelAfter(TimeSpan.FromSeconds(config.ConnectionTimeout / 1000.0));

                try
                {
                    DeviceLogger.Info(_deviceConfig.DeviceId, $"Starting HSMS connection...");
                    _connection.Start(_connectionCts.Token);
                    DeviceLogger.Info(_deviceConfig.DeviceId, $"HSMS connection started, current state: {_connection.State}");

                    bool connected = await WaitForConnectionAsync(_connectionCts.Token).ConfigureAwait(false);
                    if (!connected)
                    {
                        DeviceLogger.Error(_deviceConfig.DeviceId, $"HSMS connection failed or timeout, final state: {_connection?.State}");
                        OnConnectionStatusChanged(false, "Connection failed");
                        Cleanup();
                        return false;
                    }

                    _isConnected = true;
                    DeviceLogger.Info(_deviceConfig.DeviceId, $"HSMS client connected successfully: {ConnectionId}, state: {_connection.State}");
                    OnConnectionStatusChanged(true, "Connected");
                    UpdateHeartbeatStatus(true);
                    StartMessageLoop();
                    StartHeartbeat();
                    return true;
                }
                catch (Exception ex)
                {
                    DeviceLogger.Error(_deviceConfig.DeviceId, $"Failed to connect to HSMS server: {ex.Message}", ex);
                    OnConnectionStatusChanged(false, "Connection failed", ex.Message);
                    Cleanup();
                    return false;
                }
            }
            catch (Exception ex)
            {
                DeviceLogger.Error(_deviceConfig.DeviceId, $"Failed to create HSMS connection: {ex.Message}", ex);
                OnConnectionStatusChanged(false, "Connection failed", ex.Message);
                Cleanup();
                return false;
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task<bool> WaitForConnectionAsync(CancellationToken cancellationToken)
    {
        int attempts = 0;
        int maxAttempts = 100;
        DeviceLogger.Info(_deviceConfig.DeviceId, $"Waiting for HSMS connection...");
        
        while (!cancellationToken.IsCancellationRequested && attempts < maxAttempts)
        {
            if (_connection != null && (_connection.State == ConnectionState.Connected || _connection.State == ConnectionState.Selected))
            {
                DeviceLogger.Info(_deviceConfig.DeviceId, $"HSMS connection state changed to {_connection.State}");
                return true;
            }
            
            if (_connection != null)
            {
                DeviceLogger.Info(_deviceConfig.DeviceId, $"HSMS connection state: {_connection.State}, attempt {attempts + 1}/{maxAttempts}");
            }
            
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            attempts++;
        }
        
        DeviceLogger.Warn(_deviceConfig.DeviceId, $"HSMS connection timeout after {maxAttempts * 50}ms, state: {_connection?.State}");
        return _connection != null && (_connection.State == ConnectionState.Connected || _connection.State == ConnectionState.Selected);
    }

    private void StartMessageLoop()
    {
        _messageLoopCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                DeviceLogger.Info(_deviceConfig.DeviceId, "HSMS message loop started");
                await foreach (var e in _secsGem!.GetPrimaryMessageAsync(_messageLoopCts.Token).ConfigureAwait(false))
                {
                    using var primaryMsg = e.PrimaryMessage;
                    ProcessPrimaryMessage(e);
                    UpdateHeartbeatStatus(true);
                }
            }
            catch (OperationCanceledException)
            {
                DeviceLogger.Info(_deviceConfig.DeviceId, "HSMS message loop cancelled");
            }
            catch (Exception ex)
            {
                DeviceLogger.Error(_deviceConfig.DeviceId, $"HSMS message loop error: {ex.Message}", ex);
            }
            finally
            {
                if (_isConnected && (_connection == null || _connection.State != ConnectionState.Connected && _connection.State != ConnectionState.Selected))
                {
                    _isConnected = false;
                    DeviceLogger.Error(_deviceConfig.DeviceId, $"HSMS connection lost, state: {_connection?.State}");
                    OnConnectionStatusChanged(false, "Connection lost");
                }
            }
        });
    }

    private void ProcessPrimaryMessage(PrimaryMessageWrapper e)
    {
        try
        {
            var msg = e.PrimaryMessage;
            DeviceLogger.Info(_deviceConfig.DeviceId, $"HSMS message received: S{msg.S}F{msg.F}");

            var nodeId = $"S{msg.S}F{msg.F}";
            var value = FormatSecsItem(msg.SecsItem);
            
            _tagValues[nodeId] = value;
            
            OnDataValueChanged(nodeId, new DataValue
            {
                Value = value,
                Quality = DataQuality.Good,
                Timestamp = DateTime.UtcNow
            });

            ReplyToPrimaryMessage(e);
        }
        catch (Exception ex)
        {
            DeviceLogger.Error(_deviceConfig.DeviceId, $"Error processing HSMS message: {ex.Message}", ex);
        }
    }

    private string FormatSecsItem(Item? item)
    {
        if (item == null)
            return "null";
        
        try
        {
            var str = item.ToString() ?? "null";
            // 移除SML格式前缀
            if (str.StartsWith("L(") || str.StartsWith("["))
            {
                return str;
            }
            return str;
        }
        catch
        {
            return item.GetType().Name;
        }
    }

    private async void ReplyToPrimaryMessage(PrimaryMessageWrapper e)
    {
        try
        {
            using var replyMsg = new SecsMessage(e.PrimaryMessage.S, (byte)(e.PrimaryMessage.F + 1));
            replyMsg.SecsItem = Item.L(Item.U1(0));
            await e.TryReplyAsync(replyMsg, CancellationToken.None);
            DeviceLogger.Info(_deviceConfig.DeviceId, $"HSMS reply sent: S{replyMsg.S}F{replyMsg.F}");
        }
        catch (Exception ex)
        {
            DeviceLogger.Error(_deviceConfig.DeviceId, $"Error sending HSMS reply: {ex.Message}", ex);
        }
    }

    public override async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            DeviceLogger.Warn(_deviceConfig.DeviceId, $"HSMS client already disposed: {ConnectionId}");
            return;
        }

        try
        {
            _isConnected = false;
            StopPolling();
            StopHeartbeat();
            await WaitForPollingToStopAsync().ConfigureAwait(false);
            await WaitForHeartbeatToStopAsync().ConfigureAwait(false);

            Cleanup();
            DeviceLogger.Info(_deviceConfig.DeviceId, $"HSMS client disconnected: {ConnectionId}");
            OnConnectionStatusChanged(false, "Disconnected");
        }
        catch (Exception ex)
        {
            DeviceLogger.Error(_deviceConfig.DeviceId, $"Error disconnecting HSMS client: {ex.Message}", ex);
        }
        finally
        {
            try
            {
                _connectLock.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private void Cleanup()
    {
        _messageLoopCts?.Cancel();
        _messageLoopCts?.Dispose();
        _messageLoopCts = null;

        _connectionCts?.Cancel();
        _connectionCts?.Dispose();
        _connectionCts = null;

        if (_secsGem != null)
        {
            _secsGem.Dispose();
        }
        _secsGem = null;

        if (_connection != null)
        {
            try
            {
                _connection.DisposeAsync().AsTask().Wait(3000);
            }
            catch (Exception)
            {
            }
        }
        _connection = null;
    }

    public override async Task<DataValue> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _secsGem == null)
        {
            DeviceLogger.Warn(_deviceConfig.DeviceId, $"HSMS client not connected, cannot read node: {nodeId}");
            return EAP.Core.DataValue.NotConnected();
        }

        DeviceLogger.Debug(_deviceConfig.DeviceId, $"Reading HSMS node: {nodeId}");

        try
        {
            var s = byte.Parse(nodeId.Split('F')[0].Replace("S", ""));
            var f = byte.Parse(nodeId.Split('F')[1]);
            
            using var msg = new SecsMessage(s, f);
            using var reply = await _secsGem.SendAsync(msg, cancellationToken).ConfigureAwait(false);
            
            var value = FormatSecsItem(reply.SecsItem);
            _tagValues[nodeId] = value;
            
            return new DataValue
            {
                Value = value,
                Quality = DataQuality.Good,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            DeviceLogger.Error(_deviceConfig.DeviceId, $"Failed to read HSMS node {nodeId}: {ex.Message}", ex);
            return new DataValue
            {
                Value = null,
                Quality = DataQuality.Bad,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public override async Task<bool> WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _secsGem == null)
        {
            DeviceLogger.Warn(_deviceConfig.DeviceId, $"HSMS client not connected, cannot write node: {nodeId}");
            return false;
        }

        DeviceLogger.Debug(_deviceConfig.DeviceId, $"Writing HSMS node {nodeId} with value: {value}");
        
        try
        {
            var s = byte.Parse(nodeId.Split('F')[0].Replace("S", ""));
            var f = byte.Parse(nodeId.Split('F')[1]);
            
            var msg = new SecsMessage(s, f)
            {
                SecsItem = Item.A(value.ToString() ?? string.Empty)
            };
            
            await _secsGem.SendAsync(msg, cancellationToken).ConfigureAwait(false);
            _tagValues[nodeId] = value;
            DeviceLogger.Info(_deviceConfig.DeviceId, $"Successfully wrote to HSMS node {nodeId}");
            return true;
        }
        catch (Exception ex)
        {
            DeviceLogger.Error(_deviceConfig.DeviceId, $"Failed to write to HSMS node {nodeId}: {ex.Message}", ex);
            return false;
        }
    }

    public override Task SubscribeNodeAsync(string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default)
    {
        DeviceLogger.Info(_deviceConfig.DeviceId, $"HSMS subscription requested for node: {nodeId} (updateRate: {updateRate})");
        return base.SubscribeNodeAsync(nodeId, updateRate, cancellationToken);
    }

    public override Task UnsubscribeNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        DeviceLogger.Info(_deviceConfig.DeviceId, $"HSMS unsubscription requested for node: {nodeId}");
        return base.UnsubscribeNodeAsync(nodeId, cancellationToken);
    }

    public override void Dispose()
    {
        base.Dispose();
        _connectLock.Dispose();
        Cleanup();
    }

    private class SecsGemLogger : ISecsGemLogger
    {
        private readonly string _deviceId;

        public SecsGemLogger(string deviceId)
        {
            _deviceId = deviceId;
        }

        public void Debug(string msg) => DeviceLogger.Debug(_deviceId, $"[SecsGem] {msg}");
        public void Error(string msg) => DeviceLogger.Error(_deviceId, $"[SecsGem] {msg}");
        public void Error(string msg, Exception? ex) => DeviceLogger.Error(_deviceId, $"[SecsGem] {msg}", ex);
        public void Error(string msg, SecsMessage? message, Exception? ex) => DeviceLogger.Error(_deviceId, $"[SecsGem] {msg}", ex);
        public void Info(string msg) => DeviceLogger.Info(_deviceId, $"[SecsGem] {msg}");
        public void MessageIn(SecsMessage msg, int id) => DeviceLogger.Info(_deviceId, $"[SecsGem] MessageIn: S{msg.S}F{msg.F}, id={id}");
        public void MessageOut(SecsMessage msg, int id) => DeviceLogger.Info(_deviceId, $"[SecsGem] MessageOut: S{msg.S}F{msg.F}, id={id}");
        public void Warning(string msg) => DeviceLogger.Warn(_deviceId, $"[SecsGem] {msg}");
    }
}
