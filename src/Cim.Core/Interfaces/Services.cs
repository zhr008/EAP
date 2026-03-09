using Cim.Core.Models;

namespace Cim.Core.Interfaces;

/// <summary>
/// 设备状态管理服务接口
/// </summary>
public interface IEquipmentStateService
{
    Task<EquipmentInfo> GetEquipmentAsync(string equipmentId);
    Task<IEnumerable<EquipmentInfo>> GetAllEquipmentsAsync();
    Task UpdateEquipmentStateAsync(string equipmentId, EquipmentState state, string? reason = null);
    Task SetVariableAsync(string equipmentId, string variableId, object value);
    Task<object?> GetVariableAsync(string equipmentId, string variableId);
    Task<Dictionary<string, object>> GetAllVariablesAsync(string equipmentId);
}

/// <summary>
/// 配方管理服务接口
/// </summary>
public interface IRecipeService
{
    Task<RecipeInfo> GetRecipeAsync(string recipeId);
    Task<IEnumerable<RecipeInfo>> GetAllRecipesAsync(string? equipmentType = null);
    Task<RecipeInfo> CreateRecipeAsync(RecipeInfo recipe);
    Task UpdateRecipeAsync(RecipeInfo recipe);
    Task DeleteRecipeAsync(string recipeId);
    Task DownloadRecipeToDeviceAsync(string equipmentId, string recipeId);
    Task UploadRecipeFromDeviceAsync(string equipmentId, string recipeId);
    Task SelectRecipeAsync(string equipmentId, string recipeId);
}

/// <summary>
/// 报警管理服务接口
/// </summary>
public interface IAlarmService
{
    Task<AlarmInfo> GetAlarmAsync(string alarmId);
    Task<IEnumerable<AlarmInfo>> GetActiveAlarmsAsync(string? equipmentId = null);
    Task<IEnumerable<AlarmInfo>> GetAllAlarmsAsync(string? equipmentId = null, DateTime? from = null, DateTime? to = null);
    Task ClearAlarmAsync(string alarmId, string? comment = null);
    Task AcknowledgeAlarmAsync(string alarmId, string operatorId);
}

/// <summary>
/// 事件报告服务接口
/// </summary>
public interface IEventReportService
{
    Task RegisterEventAsync(string equipmentId, int eventId, string eventType, Dictionary<string, object> dataItems);
    Task<IEnumerable<EventReport>> GetEventsAsync(string? equipmentId = null, DateTime? from = null, DateTime? to = null, int? limit = null);
}

/// <summary>
/// 数据采集服务接口
/// </summary>
public interface IDataCollectionService
{
    Task CollectDataAsync(string equipmentId, IEnumerable<DataCollectionItem> items);
    Task<IEnumerable<DataCollectionItem>> GetDataAsync(string equipmentId, string variableId, DateTime from, DateTime to);
    Task ConfigureCollectionAsync(string equipmentId, string variableId, int intervalSeconds);
}

/// <summary>
/// SECS/GEM 通信接口
/// </summary>
public interface ISecsGemCommunication
{
    Task ConnectAsync(string equipmentId, string host, int port, int deviceId);
    Task DisconnectAsync(string equipmentId);
    Task<bool> IsConnectedAsync(string equipmentId);
    Task SendS1F1Async(string equipmentId); // AREQ - Access Request
    Task SendS2F41Async(string equipmentId); // PP_REQ - Process Program Request
    Task SendS2F43Async(string equipmentId, string recipeId); // PP_SELECT - Process Program Select
    Task SendS2F47Async(string equipmentId, string recipeId); // PP_CREATE - Process Program Create (Download)
    Task SendS2F49Async(string equipmentId, string recipeId); // PP_DELETE - Process Program Delete
    Task SendS2F13Async(string equipmentId, List<string> variableIds); // VREQ - Variable Request
    Task SendS5F1Async(string equipmentId, int alarmId, bool set); // ALMD - Alarm Display
    Task SendS6F1Async(string equipmentId, int eventId); // ER_REQ - Event Report Request
}

/// <summary>
/// 消息队列发布接口
/// </summary>
public interface IMessagePublisher
{
    Task PublishAsync<T>(string topic, T message);
    Task PublishEquipmentStateChangeAsync(string equipmentId, Models.EquipmentState previousState, Models.EquipmentState newState, string? reason = null);
    Task PublishAlarmEventAsync(string alarmId, string equipmentId, AlarmInfo alarmInfo, bool isRaised);
    Task PublishEventReportAsync(string equipmentId, EventReport report);
    Task PublishDataCollectionAsync(string equipmentId, IEnumerable<DataCollectionItem> items);
}

/// <summary>
/// Agent 协调器接口 (Master-Agent 模式)
/// </summary>
public interface IAgentCoordinator
{
    Task RegisterAgentAsync(string agentId, string host, int port);
    Task UnregisterAgentAsync(string agentId);
    Task<IEnumerable<string>> GetManagedAgentsAsync();
    Task DispatchToAgentAsync(string agentId, string command, object payload);
    Task BroadcastToAgentsAsync(string command, object payload);
}
