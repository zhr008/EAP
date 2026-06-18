using System;
using System.Threading;
using System.Threading.Tasks;
using EAP.Core.Configuration;

namespace EAP.Core.Protocol;

public interface IProtocolClient : IDisposable
{
    ProtocolType ProtocolType { get; }
    string ConnectionId { get; }
    bool IsConnected { get; }
    bool HeartbeatStatus { get; } // 心跳状态
    
    event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    event EventHandler<DataValueChangedEventArgs>? DataValueChanged;
    event EventHandler<HeartbeatStatusChangedEventArgs>? HeartbeatStatusChanged; // 心跳状态变化事件

    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<DataValue> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<bool> WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default);
    Task SubscribeNodeAsync(string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default);
    Task UnsubscribeNodeAsync(string nodeId, CancellationToken cancellationToken = default);
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