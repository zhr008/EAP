using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;


namespace EAP.Core;

public interface IProtocolClient : IDisposable
{
    ProtocolType ProtocolType { get; }
    string ConnectionId { get; }
    bool IsConnected { get; }
    bool HeartbeatStatus { get; }
    
    event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    event EventHandler<DataValueChangedEventArgs>? DataValueChanged;
    event EventHandler<HeartbeatStatusChangedEventArgs>? HeartbeatStatusChanged;

    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<DataValue> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<bool> WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default);
    Task SubscribeNodeAsync(string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default);
    Task UnsubscribeNodeAsync(string nodeId, CancellationToken cancellationToken = default);
}

public abstract class ProtocolClientBase : IProtocolClient
{
    protected readonly DeviceConfig _deviceConfig;
    protected bool _heartbeatStatus = false;
    protected DateTime _lastHeartbeatTime = DateTime.MinValue;
    protected int _consecutiveHeartbeatFailures = 0;
    protected readonly TimeSpan _heartbeatTimeout;
    protected readonly int _heartbeatFailuresBeforeDisconnect;

    protected bool _lastReportedConnected = false;
    protected readonly object _statusLock = new();

    protected readonly ConcurrentDictionary<string, int> _subscribedTags = new();
    protected Task? _pollingTask;
    protected CancellationTokenSource? _pollingCts;
    protected readonly ConcurrentDictionary<string, object> _tagValues = new();

    protected Task? _heartbeatTask;
    protected CancellationTokenSource? _heartbeatCts;
    protected string? _heartbeatTagNodeId;

    public abstract ProtocolType ProtocolType { get; }
    public string ConnectionId => _deviceConfig.DeviceId;
    public abstract bool IsConnected { get; }
    public bool HeartbeatStatus => _heartbeatStatus;

    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    public event EventHandler<DataValueChangedEventArgs>? DataValueChanged;
    public event EventHandler<HeartbeatStatusChangedEventArgs>? HeartbeatStatusChanged;

    protected ProtocolClientBase(DeviceConfig deviceConfig)
    {
        _deviceConfig = deviceConfig ?? throw new ArgumentNullException(nameof(deviceConfig));
        _heartbeatTimeout = TimeSpan.FromMilliseconds(deviceConfig.HeartbeatTimeout);
        _heartbeatFailuresBeforeDisconnect = deviceConfig.HeartbeatFailuresBeforeDisconnect;

        var heartbeatTag = deviceConfig.Tags.FirstOrDefault(t => t.IsHeartbeatTag);
        if (heartbeatTag != null)
        {
            _heartbeatTagNodeId = heartbeatTag.NodeId;
        }
    }

    public abstract Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    public abstract Task DisconnectAsync(CancellationToken cancellationToken = default);
    public abstract Task<DataValue> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default);
    public abstract Task<bool> WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default);

    public virtual Task SubscribeNodeAsync(string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default)
    {
        _subscribedTags[nodeId] = updateRate;
        return Task.CompletedTask;
    }

    public virtual Task UnsubscribeNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        _subscribedTags.TryRemove(nodeId, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 更新心跳状态
    /// 心跳正常时调用Pulse，异常时不立即断开，连续N次失败才断开
    /// </summary>
    protected void UpdateHeartbeatStatus(bool isNormal)
    {
        bool oldStatus = _heartbeatStatus;

        if (isNormal)
        {
            _lastHeartbeatTime = DateTime.Now;
            _consecutiveHeartbeatFailures = 0;
            _heartbeatStatus = true;
        }
        else
        {
            _consecutiveHeartbeatFailures++;

            if (DateTime.Now - _lastHeartbeatTime > _heartbeatTimeout)
            {
                _heartbeatStatus = false;
            }

            if (_heartbeatStatus == false && 
                _consecutiveHeartbeatFailures >= _heartbeatFailuresBeforeDisconnect && 
                IsConnected)
            {
                OnConnectionStatusChanged(false, $"Heartbeat timeout after {_consecutiveHeartbeatFailures} consecutive failures");
            }
        }

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

    /// <summary>
    /// 启动心跳检测
    /// 如果配置了独立心跳标签，则定时读取该标签
    /// 否则依赖业务数据更新心跳
    /// </summary>
    protected void StartHeartbeat()
    {
        if (_heartbeatTagNodeId == null)
        {
            return;
        }

        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = Task.Run(async () =>
        {
            while (!_heartbeatCts.Token.IsCancellationRequested && IsConnected)
            {
                try
                {
                    var value = await ReadNodeAsync(_heartbeatTagNodeId!, _heartbeatCts.Token).ConfigureAwait(false);
                    if (value.Quality == DataQuality.Good)
                    {
                        UpdateHeartbeatStatus(true);
                    }
                    else
                    {
                        UpdateHeartbeatStatus(false);
                        DeviceLogger.Warn(ConnectionId, $"心跳标签质量不佳: {value.Quality}, NodeId: {_heartbeatTagNodeId}");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (TimeoutException ex)
                {
                    UpdateHeartbeatStatus(false);
                    DeviceLogger.Warn(ConnectionId, $"心跳读取超时: {ex.Message}");
                }
                catch (IOException ex)
                {
                    UpdateHeartbeatStatus(false);
                    DeviceLogger.Warn(ConnectionId, $"心跳读取IO异常: {ex.Message}");
                }
                catch (Exception ex)
                {
                    UpdateHeartbeatStatus(false);
                    DeviceLogger.Error(ConnectionId, $"心跳读取异常: {ex.Message}", ex);
                }

                try
                {
                    await Task.Delay(_deviceConfig.HeartbeatInterval, _heartbeatCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });
    }

    /// <summary>
    /// 停止心跳检测
    /// </summary>
    protected void StopHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _consecutiveHeartbeatFailures = 0;
    }

    /// <summary>
    /// 等待心跳任务停止
    /// </summary>
    protected async Task WaitForHeartbeatToStopAsync()
    {
        if (_heartbeatTask != null)
        {
            try
            {
                await _heartbeatTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    protected void OnConnectionStatusChanged(bool isConnected, string status, string? errorMessage = null)
    {
        lock (_statusLock)
        {
            if (_lastReportedConnected == isConnected)
                return;

            _lastReportedConnected = isConnected;
        }

        ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
        {
            ConnectionId = ConnectionId,
            IsConnected = isConnected,
            Status = status,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        });
    }

    protected void OnDataValueChanged(string nodeId, DataValue value)
    {
        DataValueChanged?.Invoke(this, new DataValueChangedEventArgs
        {
            ConnectionId = ConnectionId,
            NodeId = nodeId,
            Value = value
        });

        if (_heartbeatTagNodeId == null)
        {
            UpdateHeartbeatStatus(true);
        }
    }

    protected void StartPolling()
    {
        _pollingCts = new CancellationTokenSource();
        _pollingTask = Task.Run(async () =>
        {
            while (!_pollingCts.Token.IsCancellationRequested && IsConnected)
            {
                try
                {
                    bool anyReadSuccess = false;

                    foreach (var tag in _subscribedTags)
                    {
                        if (_pollingCts.Token.IsCancellationRequested) break;

                        var nodeId = tag.Key;

                        if (nodeId == _heartbeatTagNodeId)
                        {
                            continue;
                        }

                        try
                        {
                            var value = await ReadNodeAsync(nodeId, _pollingCts.Token).ConfigureAwait(false);
                            if (value.Quality == DataQuality.Good && value.Value != null)
                            {
                                _tagValues[nodeId] = value.Value;
                                OnDataValueChanged(nodeId, value);
                                anyReadSuccess = true;
                            }
                            else if (value.Quality != DataQuality.Good)
                            {
                                DeviceLogger.Debug(ConnectionId, $"标签读取质量不佳: {nodeId}, Quality: {value.Quality}");
                            }
                        }
                        catch (TimeoutException ex)
                        {
                            DeviceLogger.Debug(ConnectionId, $"标签读取超时: {nodeId}, {ex.Message}");
                        }
                        catch (IOException ex)
                        {
                            DeviceLogger.Debug(ConnectionId, $"标签读取IO异常: {nodeId}, {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            DeviceLogger.Warn(ConnectionId, $"标签读取异常: {nodeId}, {ex.Message}", ex);
                        }
                    }

                    if (_heartbeatTagNodeId == null)
                    {
                        UpdateHeartbeatStatus(anyReadSuccess);
                    }

                    await Task.Delay(1000, _pollingCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    DeviceLogger.Error(ConnectionId, $"轮询循环异常: {ex.Message}", ex);
                    if (_heartbeatTagNodeId == null)
                    {
                        UpdateHeartbeatStatus(false);
                    }
                }
            }
        });
    }

    protected void StopPolling()
    {
        _pollingCts?.Cancel();
    }

    protected async Task WaitForPollingToStopAsync()
    {
        if (_pollingTask != null)
        {
            await _pollingTask.ConfigureAwait(false);
        }
    }

    public virtual void Dispose()
    {
        StopPolling();
        StopHeartbeat();
        _ = DisconnectAsync(CancellationToken.None);
    }
}

public enum DataQuality
{
    Good,
    Bad,
    Uncertain,
    NotConnected
}

public class DataValue
{
    public object? Value { get; set; }
    public DataQuality Quality { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ErrorMessage { get; set; }

    public static DataValue NotConnected()
    {
        return new DataValue
        {
            Quality = DataQuality.NotConnected,
            Timestamp = DateTime.UtcNow,
            ErrorMessage = "Not connected"
        };
    }

    public static DataValue Bad(string errorMessage)
    {
        return new DataValue
        {
            Quality = DataQuality.Bad,
            Timestamp = DateTime.UtcNow,
            ErrorMessage = errorMessage
        };
    }

    public static DataValue Good(object value)
    {
        return new DataValue
        {
            Value = value,
            Quality = DataQuality.Good,
            Timestamp = DateTime.UtcNow
        };
    }
}

public class ConnectionStatusChangedEventArgs : EventArgs
{
    public string ConnectionId { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
}

public class DataValueChangedEventArgs : EventArgs
{
    public string ConnectionId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public DataValue Value { get; set; } = new();
}

public class HeartbeatStatusChangedEventArgs : EventArgs
{
    public string ConnectionId { get; set; } = string.Empty;
    public bool IsNormal { get; set; }
    public DateTime Timestamp { get; set; }
}