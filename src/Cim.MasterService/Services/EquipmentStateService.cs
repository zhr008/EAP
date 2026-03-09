using Cim.Core.Models;
using Cim.Core.Interfaces;

namespace Cim.MasterService.Services;

/// <summary>
/// 设备状态管理服务实现
/// </summary>
public class EquipmentStateService : IEquipmentStateService
{
    private readonly ILogger<EquipmentStateService> _logger;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ConcurrentDictionary<string, EquipmentInfo> _equipments = new();

    public EquipmentStateService(
        ILogger<EquipmentStateService> logger,
        IMessagePublisher messagePublisher)
    {
        _logger = logger;
        _messagePublisher = messagePublisher;
    }

    public Task<EquipmentInfo> GetEquipmentAsync(string equipmentId)
    {
        if (_equipments.TryGetValue(equipmentId, out var equipment))
            return Task.FromResult(equipment);
        
        throw new KeyNotFoundException($"Equipment {equipmentId} not found");
    }

    public Task<IEnumerable<EquipmentInfo>> GetAllEquipmentsAsync()
    {
        return Task.FromResult<IEnumerable<EquipmentInfo>>(_equipments.Values.ToList());
    }

    public async Task UpdateEquipmentStateAsync(string equipmentId, EquipmentState state, string? reason = null)
    {
        var equipment = _equipments.GetOrAdd(equipmentId, _ => new EquipmentInfo 
        { 
            EquipmentId = equipmentId,
            State = state 
        });

        var previousState = equipment.State;
        equipment.State = state;
        equipment.LastHeartbeat = DateTime.UtcNow;

        if (previousState != state)
        {
            await _messagePublisher.PublishEquipmentStateChangeAsync(
                equipmentId, previousState, state, reason);
            
            _logger.LogInformation("Equipment {EquipmentId} state changed from {PreviousState} to {NewState}",
                equipmentId, previousState, state);
        }
    }

    public Task SetVariableAsync(string equipmentId, string variableId, object value)
    {
        var equipment = _equipments.GetOrAdd(equipmentId, _ => new EquipmentInfo 
        { 
            EquipmentId = equipmentId 
        });

        equipment.Variables[variableId] = value;
        _logger.LogDebug("Variable {VariableId} set on {EquipmentId} to {Value}",
            variableId, equipmentId, value);

        return Task.CompletedTask;
    }

    public Task<object?> GetVariableAsync(string equipmentId, string variableId)
    {
        if (_equipments.TryGetValue(equipmentId, out var equipment) &&
            equipment.Variables.TryGetValue(variableId, out var value))
        {
            return Task.FromResult(value);
        }
        return Task.FromResult<object?>(null);
    }

    public Task<Dictionary<string, object>> GetAllVariablesAsync(string equipmentId)
    {
        if (_equipments.TryGetValue(equipmentId, out var equipment))
            return Task.FromResult(equipment.Variables);
        
        return Task.FromResult(new Dictionary<string, object>());
    }

    /// <summary>
    /// 注册设备
    /// </summary>
    public void RegisterEquipment(EquipmentInfo equipment)
    {
        _equipments[equipment.EquipmentId] = equipment;
        _logger.LogInformation("Equipment {EquipmentId} registered", equipment.EquipmentId);
    }
}
