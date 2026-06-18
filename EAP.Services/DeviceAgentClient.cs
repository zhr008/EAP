using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EAP.Core.Configuration;
using EAP.Core.Protocol;
using log4net;

namespace EAP.Services;

public class DeviceAgentClient : IProtocolClient
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(DeviceAgentClient));
    
    private readonly DeviceConfig _deviceConfig;
    private readonly string _pipeName;
    private NamedPipeClientStream? _pipeClient;
    private Task? _listenTask;
    private CancellationTokenSource? _cts;
    private bool _isConnected;
    private bool _heartbeatStatus = false;
    private DateTime _lastHeartbeatTime = DateTime.MinValue;
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(10);

    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    public event EventHandler<DataValueChangedEventArgs>? DataValueChanged;
    public event EventHandler<HeartbeatStatusChangedEventArgs>? HeartbeatStatusChanged;

    public EAP.Core.Configuration.ProtocolType ProtocolType => _deviceConfig.ProtocolType;
    public bool IsConnected => _isConnected;
    public bool HeartbeatStatus => _heartbeatStatus;
    public string ConnectionId => _deviceConfig.Id;

    public DeviceAgentClient(DeviceConfig deviceConfig)
    {
        _deviceConfig = deviceConfig;
        _pipeName = $"EAP_Device_{deviceConfig.Id}";
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_isConnected)
            {
                Logger.Warn($"Device {_deviceConfig.Id} already connected");
                return true;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            _pipeClient = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await _pipeClient.ConnectAsync(5000, cancellationToken).ConfigureAwait(false);
            Logger.Info($"Connected to device agent pipe: {_pipeName}");

            _isConnected = true;
            OnConnectionStatusChanged(true, "Connected");

            _listenTask = Task.Run(ListenForMessages, _cts.Token);

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to connect to device agent: {ex.Message}", ex);
            _isConnected = false;
            OnConnectionStatusChanged(false, ex.Message);
            return false;
        }
    }

    private async Task ListenForMessages()
    {
        if (_pipeClient == null || _cts == null) return;

        var buffer = new byte[4096];

        try
        {
            while (!_cts.Token.IsCancellationRequested && _pipeClient.IsConnected)
            {
                var bytesRead = await _pipeClient.ReadAsync(buffer, 0, buffer.Length, _cts.Token).ConfigureAwait(false);
                if (bytesRead == 0) break;

                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                await ProcessMessage(message).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("Listening canceled");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error listening for messages: {ex.Message}", ex);
            _isConnected = false;
            OnConnectionStatusChanged(false, ex.Message);
        }
    }

    private async Task ProcessMessage(string message)
    {
        var parts = message.Split('|');
        if (parts.Length < 2) return;

        switch (parts[0])
        {
            case "STATUS_CHANGED":
                {
                    if (parts.Length >= 4)
                    {
                        var deviceId = parts[1];
                        var isConnected = bool.Parse(parts[2]);
                        var statusMessage = parts[3];
                        
                        _isConnected = isConnected;
                        OnConnectionStatusChanged(isConnected, statusMessage);
                    }
                    break;
                }
            case "DATA_CHANGED":
                {
                    if (parts.Length >= 5)
                    {
                        var deviceId = parts[1];
                        var nodeId = parts[2];
                        var quality = (DataQuality)Enum.Parse(typeof(DataQuality), parts[3]);
                        var value = parts[4];

                        OnDataValueChanged(deviceId, nodeId, new DataValue
                        {
                            Value = value,
                            Quality = quality,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    break;
                }
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _cts?.Cancel();
            
            if (_pipeClient != null && _pipeClient.IsConnected)
            {
                await SendMessage("DISCONNECT").ConfigureAwait(false);
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            _pipeClient?.Close();
            _pipeClient?.Dispose();
            _pipeClient = null;

            _isConnected = false;
            OnConnectionStatusChanged(false, "Disconnected");
            
            Logger.Info($"Device {_deviceConfig.Id} disconnected");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error disconnecting: {ex.Message}", ex);
        }
    }

    public async Task<DataValue> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _pipeClient == null)
        {
            return DataValue.NotConnected();
        }

        try
        {
            var response = await SendMessageAndWait($"READ|{nodeId}", cancellationToken).ConfigureAwait(false);
            return ParseReadResponse(response);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error reading node {nodeId}: {ex.Message}", ex);
            return DataValue.Bad(ex.Message);
        }
    }

    public async Task<bool> WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _pipeClient == null)
        {
            return false;
        }

        try
        {
            var response = await SendMessageAndWait($"WRITE|{nodeId}|{value}", cancellationToken).ConfigureAwait(false);
            return response.StartsWith("WRITE_OK");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error writing node {nodeId}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task SubscribeNodeAsync(string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _pipeClient == null)
            return;

        try
        {
            await SendMessageAndWait($"SUBSCRIBE|{nodeId}|{updateRate}", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error subscribing to node {nodeId}: {ex.Message}", ex);
        }
    }

    public Task UnsubscribeNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!_isConnected || _pipeClient == null)
            return Task.CompletedTask;

        return SendMessage($"UNSUBSCRIBE|{nodeId}");
    }

    private async Task<string> SendMessageAndWait(string message, CancellationToken cancellationToken)
    {
        if (_pipeClient == null)
            throw new InvalidOperationException("Not connected");

        await SendMessage(message).ConfigureAwait(false);

        var buffer = new byte[4096];
        var bytesRead = await _pipeClient.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
    }

    private async Task SendMessage(string message)
    {
        if (_pipeClient == null || !_pipeClient.IsConnected)
            throw new InvalidOperationException("Not connected");

        var bytes = Encoding.UTF8.GetBytes(message);
        await _pipeClient.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        await _pipeClient.FlushAsync().ConfigureAwait(false);
    }

    private DataValue ParseReadResponse(string response)
    {
        var parts = response.Split('|');
        if (parts.Length < 3)
        {
            return DataValue.Bad("Invalid response");
        }

        if (parts[0] != "READ_OK")
        {
            return DataValue.Bad(response);
        }

        var quality = (DataQuality)Enum.Parse(typeof(DataQuality), parts[2]);
        var value = parts.Length > 3 ? parts[3] : string.Empty;

        return new DataValue
        {
            Value = value,
            Quality = quality,
            Timestamp = DateTime.UtcNow
        };
    }

    private void OnConnectionStatusChanged(bool isConnected, string message)
    {
        ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
        {
            ConnectionId = ConnectionId,
            IsConnected = isConnected,
            Status = message,
            Timestamp = DateTime.UtcNow
        });
    }

    private void OnDataValueChanged(string connectionId, string nodeId, DataValue value)
    {
        DataValueChanged?.Invoke(this, new DataValueChangedEventArgs
        {
            ConnectionId = connectionId,
            NodeId = nodeId,
            Value = value
        });
        
        // 收到数据表示心跳正常
        UpdateHeartbeatStatus(true);
    }
    
    private void UpdateHeartbeatStatus(bool isNormal)
    {
        bool oldStatus = _heartbeatStatus;
        
        if (isNormal)
        {
            _lastHeartbeatTime = DateTime.Now;
            _heartbeatStatus = true;
        }
        else
        {
            // 检查是否超时
            if (DateTime.Now - _lastHeartbeatTime > _heartbeatTimeout)
            {
                _heartbeatStatus = false;
                // 心跳超时，断开连接
                Logger.Warn($"Heartbeat timeout for device {ConnectionId}, disconnecting...");
                _ = DisconnectAsync(CancellationToken.None);
            }
        }
        
        // 如果状态变化，触发事件
        if (oldStatus != _heartbeatStatus)
        {
            HeartbeatStatusChanged?.Invoke(this, new HeartbeatStatusChangedEventArgs
            {
                ConnectionId = ConnectionId,
                IsNormal = _heartbeatStatus,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public void Dispose()
    {
        // 使用 ConfigureAwait(false) 避免死锁，非阻塞方式断开连接
        _ = DisconnectAsync().ConfigureAwait(false);
    }
}
