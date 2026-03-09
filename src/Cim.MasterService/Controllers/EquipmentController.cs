using Cim.Core.Interfaces;
using Cim.Core.Models;

namespace Cim.MasterService.Controllers;

/// <summary>
/// 设备管理 REST API 控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EquipmentController : ControllerBase
{
    private readonly ILogger<EquipmentController> _logger;
    private readonly IEquipmentStateService _stateService;

    public EquipmentController(
        ILogger<EquipmentController> logger,
        IEquipmentStateService stateService)
    {
        _logger = logger;
        _stateService = stateService;
    }

    /// <summary>
    /// 获取所有设备
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EquipmentInfo>>> GetAll()
    {
        var equipments = await _stateService.GetAllEquipmentsAsync();
        return Ok(equipments);
    }

    /// <summary>
    /// 获取单个设备
    /// </summary>
    [HttpGet("{equipmentId}")]
    public async Task<ActionResult<EquipmentInfo>> Get(string equipmentId)
    {
        try
        {
            var equipment = await _stateService.GetEquipmentAsync(equipmentId);
            return Ok(equipment);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Equipment {equipmentId} not found");
        }
    }

    /// <summary>
    /// 更新设备状态
    /// </summary>
    [HttpPut("{equipmentId}/state")]
    public async Task<ActionResult> UpdateState(string equipmentId, [FromBody] UpdateStateRequest request)
    {
        try
        {
            await _stateService.UpdateEquipmentStateAsync(equipmentId, request.State, request.Reason);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating equipment state");
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 获取设备变量
    /// </summary>
    [HttpGet("{equipmentId}/variables")]
    public async Task<ActionResult<Dictionary<string, object>>> GetVariables(string equipmentId)
    {
        var variables = await _stateService.GetAllVariablesAsync(equipmentId);
        return Ok(variables);
    }

    /// <summary>
    /// 设置设备变量
    /// </summary>
    [HttpPut("{equipmentId}/variables/{variableId}")]
    public async Task<ActionResult> SetVariable(string equipmentId, string variableId, [FromBody] SetVariableRequest request)
    {
        try
        {
            await _stateService.SetVariableAsync(equipmentId, variableId, request.Value!);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting variable");
            return BadRequest(ex.Message);
        }
    }
}

public class UpdateStateRequest
{
    public EquipmentState State { get; set; }
    public string? Reason { get; set; }
}

public class SetVariableRequest
{
    public object? Value { get; set; }
}

/// <summary>
/// Agent 管理 REST API 控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(ILogger<AgentsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Agent 注册
    /// </summary>
    [HttpPost("register")]
    public ActionResult Register([FromBody] AgentRegisterRequest request)
    {
        _logger.LogInformation("Agent registered: {AgentId} at {Host}:{Port}", 
            request.AgentId, request.Host, request.Port);
        return Ok(new { Success = true, Message = "Agent registered successfully" });
    }

    /// <summary>
    /// Agent 心跳
    /// </summary>
    [HttpPost("heartbeat")]
    public ActionResult Heartbeat([FromBody] AgentHeartbeatRequest request)
    {
        _logger.LogDebug("Heartbeat from {AgentId}, connected devices: {Count}",
            request.AgentId, request.ConnectedDevices);
        return Ok(new { Success = true });
    }
}

public class AgentRegisterRequest
{
    public string AgentId { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public DateTime Timestamp { get; set; }
}

public class AgentHeartbeatRequest
{
    public string AgentId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int ConnectedDevices { get; set; }
}
