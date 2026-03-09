using Cim.GrpcContracts;
using Cim.Core.Interfaces;
using Cim.Core.Models;
using Grpc.Core;

namespace Cim.MasterService.Services;

/// <summary>
/// gRPC 设备服务实现
/// </summary>
public class EquipmentGrpcService : EquipmentService.EquipmentServiceBase
{
    private readonly ILogger<EquipmentGrpcService> _logger;
    private readonly IEquipmentStateService _stateService;

    public EquipmentGrpcService(
        ILogger<EquipmentGrpcService> logger,
        IEquipmentStateService stateService)
    {
        _logger = logger;
        _stateService = stateService;
    }

    public override async Task<EquipmentResponse> GetEquipment(GetEquipmentRequest request, ServerCallContext context)
    {
        try
        {
            var equipment = await _stateService.GetEquipmentAsync(request.EquipmentId);
            return new EquipmentResponse
            {
                Success = true,
                Equipment = new Cim.GrpcContracts.EquipmentInfo
                {
                    EquipmentId = equipment.EquipmentId,
                    EquipmentType = equipment.EquipmentType,
                    HostAddress = equipment.HostAddress,
                    Port = equipment.Port,
                    DeviceId = equipment.DeviceId,
                    State = (int)equipment.State,
                    ConnectionStatus = (int)equipment.ConnectionStatus,
                    CurrentRecipe = equipment.CurrentRecipe ?? "",
                    LastHeartbeat = equipment.LastHeartbeat.ToString("O")
                }
            };
        }
        catch (KeyNotFoundException ex)
        {
            return new EquipmentResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting equipment {EquipmentId}", request.EquipmentId);
            return new EquipmentResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public override async Task<EquipmentListResponse> GetAllEquipments(GetAllEquipmentsRequest request, ServerCallContext context)
    {
        try
        {
            var equipments = await _stateService.GetAllEquipmentsAsync();
            var response = new EquipmentListResponse();
            
            foreach (var eq in equipments)
            {
                response.Equipments.Add(new Cim.GrpcContracts.EquipmentInfo
                {
                    EquipmentId = eq.EquipmentId,
                    EquipmentType = eq.EquipmentType,
                    HostAddress = eq.HostAddress,
                    Port = eq.Port,
                    DeviceId = eq.DeviceId,
                    State = (int)eq.State,
                    ConnectionStatus = (int)eq.ConnectionStatus,
                    CurrentRecipe = eq.CurrentRecipe ?? "",
                    LastHeartbeat = eq.LastHeartbeat.ToString("O")
                });
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all equipments");
            return new EquipmentListResponse();
        }
    }

    public override async Task<CommonResponse> UpdateEquipmentState(UpdateEquipmentStateRequest request, ServerCallContext context)
    {
        try
        {
            var state = (EquipmentState)request.State;
            await _stateService.UpdateEquipmentStateAsync(request.EquipmentId, state, request.Reason);
            
            return new CommonResponse
            {
                Success = true,
                Message = $"Equipment state updated to {state}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating equipment state");
            return new CommonResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public override async Task<VariableResponse> GetVariable(GetVariableRequest request, ServerCallContext context)
    {
        try
        {
            var value = await _stateService.GetVariableAsync(request.EquipmentId, request.VariableId);
            
            return new VariableResponse
            {
                Success = true,
                Value = value?.ToString() ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting variable");
            return new VariableResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public override async Task<CommonResponse> SetVariable(SetVariableRequest request, ServerCallContext context)
    {
        try
        {
            object value = request.Value;
            if ((DataType)request.DataType == DataType.Integer)
            {
                value = int.Parse(request.Value);
            }
            else if ((DataType)request.DataType == DataType.Double)
            {
                value = double.Parse(request.Value);
            }
            else if ((DataType)request.DataType == DataType.Boolean)
            {
                value = bool.Parse(request.Value);
            }

            await _stateService.SetVariableAsync(request.EquipmentId, request.VariableId, value);
            
            return new CommonResponse
            {
                Success = true,
                Message = "Variable set successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting variable");
            return new CommonResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
}
