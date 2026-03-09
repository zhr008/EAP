using Cim.Core.Models;

namespace Cim.Core.Events;

/// <summary>
/// 设备状态变更事件
/// </summary>
public class EquipmentStateChangedEvent
{
    public string EquipmentId { get; set; } = string.Empty;
    public EquipmentState PreviousState { get; set; }
    public EquipmentState NewState { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
}

/// <summary>
/// 报警事件
/// </summary>
public class AlarmEvent
{
    public string AlarmId { get; set; } = string.Empty;
    public string EquipmentId { get; set; } = string.Empty;
    public AlarmInfo AlarmInfo { get; set; } = new();
    public bool IsAlarmRaised { get; set; } // true=报警触发，false=报警清除
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 事件报告事件
/// </summary>
public class EventReportEvent
{
    public string EquipmentId { get; set; } = string.Empty;
    public EventReport Report { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 配方变更事件
/// </summary>
public class RecipeChangeEvent
{
    public string EquipmentId { get; set; } = string.Empty;
    public RecipeInfo Recipe { get; set; } = new();
    public RecipeChangeType ChangeType { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum RecipeChangeType
{
    Uploaded,
    Downloaded,
    Selected,
    Deleted,
    Modified
}

/// <summary>
/// 数据采集事件
/// </summary>
public class DataCollectionEvent
{
    public string EquipmentId { get; set; } = string.Empty;
    public List<DataCollectionItem> Items { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 设备连接事件
/// </summary>
public class DeviceConnectionEvent
{
    public string EquipmentId { get; set; } = string.Empty;
    public ConnectionStatus Status { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
