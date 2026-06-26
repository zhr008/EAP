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
    protected readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(10);

    protected readonly ConcurrentDictionary<string, int> _subscribedTags = new();
    protected Task? _pollingTask;
    protected CancellationTokenSource? _pollingCts;
    protected readonly ConcurrentDictionary<string, object> _tagValues = new();

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

    protected void UpdateHeartbeatStatus(bool isNormal)
    {
        bool oldStatus = _heartbeatStatus;

        if (isNormal)
        {
            _lastHeartbeatTime = DateTime.Now;
            _heartbeatStatus = true;
        }
        else
        {
            if (DateTime.Now - _lastHeartbeatTime > _heartbeatTimeout)
            {
                _heartbeatStatus = false;
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
            
            if (!_heartbeatStatus && IsConnected)
            {
                OnConnectionStatusChanged(false, "Heartbeat timeout");
            }
        }
    }

    protected void OnConnectionStatusChanged(bool isConnected, string status, string? errorMessage = null)
    {
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
        UpdateHeartbeatStatus(true);
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

                        try
                        {
                            var value = await ReadNodeAsync(nodeId, _pollingCts.Token).ConfigureAwait(false);
                            if (value.Quality == DataQuality.Good && value.Value != null)
                            {
                                _tagValues[nodeId] = value.Value;
                                OnDataValueChanged(nodeId, value);
                                anyReadSuccess = true;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }

                    UpdateHeartbeatStatus(anyReadSuccess);
                    await Task.Delay(1000, _pollingCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    UpdateHeartbeatStatus(false);
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