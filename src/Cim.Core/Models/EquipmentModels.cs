namespace Cim.Core.Models;

/// <summary>
/// 设备状态枚举 (SECS/GEM State Model)
/// </summary>
public enum EquipmentState
{
    Offline = 0,      // 离线
    Online = 1,       // 在线
    Running = 2,      // 运行中
    Idle = 3,         // 空闲
    Paused = 4,       // 暂停
    Stopped = 5,      // 停止
    Error = 6,        // 错误
    Maintenance = 7   // 维护中
}

/// <summary>
/// 设备连接状态
/// </summary>
public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Authenticating,
    Authenticated
}

/// <summary>
/// 设备信息
/// </summary>
public class EquipmentInfo
{
    public string EquipmentId { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty;
    public string HostAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public int DeviceId { get; set; }
    public EquipmentState State { get; set; }
    public ConnectionStatus ConnectionStatus { get; set; }
    public string? CurrentRecipe { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
    public List<AlarmInfo> ActiveAlarms { get; set; } = new();
}

/// <summary>
/// 报警信息
/// </summary>
public class AlarmInfo
{
    public string AlarmId { get; set; } = string.Empty;
    public string AlarmCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AlarmSeverity Severity { get; set; }
    public DateTime RaisedTime { get; set; }
    public DateTime? ClearedTime { get; set; }
    public bool IsCleared { get; set; }
    public string? EquipmentId { get; set; }
}

/// <summary>
/// 报警严重程度
/// </summary>
public enum AlarmSeverity
{
    Critical = 0,
    Major = 1,
    Minor = 2,
    Warning = 3,
    Information = 4
}

/// <summary>
/// 配方信息
/// </summary>
public class RecipeInfo
{
    public string RecipeId { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string EquipmentType { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime CreatedTime { get; set; }
    public DateTime ModifiedTime { get; set; }
    public string? Body { get; set; } // 配方内容
}

/// <summary>
/// 事件报告
/// </summary>
public class EventReport
{
    public string EventId { get; set; } = string.Empty;
    public string EquipmentId { get; set; } = string.Empty;
    public int EventId_SECS { get; set; } // SECS/GEM Event ID
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> DataItems { get; set; } = new();
    public string? Description { get; set; }
}

/// <summary>
/// 数据采集项
/// </summary>
public class DataCollectionItem
{
    public string VariableId { get; set; } = string.Empty;
    public string VariableName { get; set; } = string.Empty;
    public object Value { get; set; } = string.Empty;
    public DataType DataType { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string EquipmentId { get; set; } = string.Empty;
}

/// <summary>
/// 数据类型
/// </summary>
public enum DataType
{
    Boolean,
    Integer,
    Long,
    Float,
    Double,
    String,
    Array,
    Enum
}

/// <summary>
/// SECS/GEM 消息类型
/// </summary>
public class SecsGemMessage
{
    public byte Stream { get; set; }
    public byte Function { get; set; }
    public bool IsPrimary { get; set; }
    public byte[] SystemBytes { get; set; } = new byte[2];
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
