using Cim.Core.Interfaces;
using Cim.Core.Models;
using Cim.DeviceConnector.SecsGem;

namespace Cim.AgentService.Services;

/// <summary>
/// Agent 服务 - 负责管理设备连接和 SECS/GEM 通信
/// Master-Agent 架构中的 Agent 角色
/// </summary>
public class DeviceAgentService : BackgroundService
{
    private readonly ILogger<DeviceAgentService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, HsmsConnection> _connections = new();
    private readonly string _agentId;
    private readonly string? _masterAddress;
    private readonly int _masterPort;

    public DeviceAgentService(
        ILogger<DeviceAgentService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _agentId = configuration["Agent:Id"] ?? $"Agent-{Environment.MachineName}";
        _masterAddress = configuration["Master:Address"];
        _masterPort = configuration.GetValue<int>("Master:Port", 5000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Device Agent Service starting... AgentId: {AgentId}", _agentId);

        // 连接到 Master (如果配置了)
        if (!string.IsNullOrEmpty(_masterAddress))
        {
            await RegisterWithMasterAsync(stoppingToken);
        }

        // 加载设备配置并建立连接
        await LoadAndConnectDevicesAsync(stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 心跳检测
                await HeartbeatAsync(stoppingToken);
                
                // 等待或处理其他任务
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Device Agent Service stopping...");
        }
        finally
        {
            // 断开所有连接
            await DisconnectAllAsync();
        }
    }

    /// <summary>
    /// 向 Master 注册
    /// </summary>
    private async Task RegisterWithMasterAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient();
            var payload = new
            {
                AgentId = _agentId,
                Host = Environment.MachineName,
                Port = 5001,
                Timestamp = DateTime.UtcNow
            };

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(
                $"http://{_masterAddress}:{_masterPort}/api/agents/register",
                content,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully registered with Master at {Address}:{Port}",
                    _masterAddress, _masterPort);
            }
            else
            {
                _logger.LogWarning("Failed to register with Master: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering with Master");
        }
    }

    /// <summary>
    /// 加载设备配置并建立连接
    /// </summary>
    private async Task LoadAndConnectDevicesAsync(CancellationToken cancellationToken)
    {
        var devices = GetConfiguredDevices();

        foreach (var device in devices)
        {
            try
            {
                await ConnectToDeviceAsync(device, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to device {EquipmentId}", device.EquipmentId);
            }
        }
    }

    /// <summary>
    /// 连接到单个设备
    /// </summary>
    private async Task ConnectToDeviceAsync(EquipmentInfo device, CancellationToken cancellationToken)
    {
        var connection = new HsmsConnection(
            device.EquipmentId,
            device.HostAddress,
            device.Port,
            device.DeviceId);

        connection.MessageReceived += OnMessageReceived;
        connection.ConnectionChanged += OnConnectionChanged;

        await connection.ConnectAsync(cancellationToken);
        
        _connections[device.EquipmentId] = connection;
        _logger.LogInformation("Connected to device {EquipmentId}", device.EquipmentId);

        await connection.SendAccessRequestAsync();
    }

    /// <summary>
    /// 处理接收到的 SECS/GEM 消息
    /// </summary>
    private void OnMessageReceived(object? sender, SecsGemMessageEventArgs e)
    {
        var connection = sender as HsmsConnection;
        if (connection == null) return;

        _logger.LogDebug("Received message from {EquipmentId}: Stream={Stream}, Function={Function}",
            connection._equipmentId, e.Message.Stream, e.Message.Function);

        ProcessSecsMessage(connection, e.Message);
    }

    /// <summary>
    /// 处理 SECS 消息
    /// </summary>
    private void ProcessSecsMessage(HsmsConnection connection, SecsGemMessage message)
    {
        switch ((message.Stream, message.Function))
        {
            case (1, 2):
                HandleAccessRequestAck(connection, message);
                break;
            case (2, 14):
                HandleVariableDataResponse(connection, message);
                break;
            case (2, 42):
                HandleProcessProgramList(connection, message);
                break;
            case (5, 2):
                HandleAlarmAck(connection, message);
                break;
            case (6, 2):
                HandleEventReport(connection, message);
                break;
        }
    }

    private void HandleAccessRequestAck(HsmsConnection connection, SecsGemMessage message)
    {
        _logger.LogInformation("Access granted for {EquipmentId}", connection._equipmentId);
    }

    private void HandleVariableDataResponse(HsmsConnection connection, SecsGemMessage message)
    {
        _logger.LogDebug("Received variable data from {EquipmentId}", connection._equipmentId);
    }

    private void HandleProcessProgramList(HsmsConnection connection, SecsGemMessage message)
    {
        _logger.LogDebug("Received recipe list from {EquipmentId}", connection._equipmentId);
    }

    private void HandleAlarmAck(HsmsConnection connection, SecsGemMessage message)
    {
        _logger.LogDebug("Alarm acknowledged for {EquipmentId}", connection._equipmentId);
    }

    private void HandleEventReport(HsmsConnection connection, SecsGemMessage message)
    {
        _logger.LogInformation("Received event report from {EquipmentId}", connection._equipmentId);
    }

    private void OnConnectionChanged(object? sender, ConnectionStatus status)
    {
        var connection = sender as HsmsConnection;
        if (connection == null) return;

        _logger.LogInformation("Connection status changed for {EquipmentId}: {Status}",
            connection._equipmentId, status);
    }

    private async Task HeartbeatAsync(CancellationToken cancellationToken)
    {
        foreach (var kvp in _connections.ToList())
        {
            var connection = kvp.Value;
            if (connection.Status != ConnectionStatus.Connected)
                continue;

            if (DateTime.UtcNow - connection.LastHeartbeat > TimeSpan.FromMinutes(5))
            {
                _logger.LogWarning("Heartbeat timeout for {EquipmentId}", kvp.Key);
            }
        }

        if (!string.IsNullOrEmpty(_masterAddress))
        {
            await SendHeartbeatToMasterAsync(cancellationToken);
        }
    }

    private async Task SendHeartbeatToMasterAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient();
            var payload = new
            {
                AgentId = _agentId,
                Timestamp = DateTime.UtcNow,
                ConnectedDevices = _connections.Count
            };

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            await client.PostAsync(
                $"http://{_masterAddress}:{_masterPort}/api/agents/heartbeat",
                content,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error sending heartbeat to Master");
        }
    }

    private async Task DisconnectAllAsync()
    {
        foreach (var kvp in _connections.ToList())
        {
            try
            {
                await kvp.Value.DisconnectAsync();
                _logger.LogInformation("Disconnected from {EquipmentId}", kvp.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from {EquipmentId}", kvp.Key);
            }
        }
        _connections.Clear();
    }

    private List<EquipmentInfo> GetConfiguredDevices()
    {
        return new List<EquipmentInfo>
        {
            new EquipmentInfo
            {
                EquipmentId = "EQP001",
                EquipmentType = "ETCHER",
                HostAddress = "192.168.1.100",
                Port = 5000,
                DeviceId = 1
            },
            new EquipmentInfo
            {
                EquipmentId = "EQP002",
                EquipmentType = "DEPOSITOR",
                HostAddress = "192.168.1.101",
                Port = 5000,
                DeviceId = 2
            }
        };
    }

    public async Task<bool> DownloadRecipeAsync(string equipmentId, string recipeId, byte[] recipeBody)
    {
        if (!_connections.TryGetValue(equipmentId, out var connection))
            return false;

        try
        {
            await connection.SendProcessProgramCreateAsync(recipeId, recipeBody);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading recipe to {EquipmentId}", equipmentId);
            return false;
        }
    }

    public async Task<bool> SelectRecipeAsync(string equipmentId, string recipeId)
    {
        if (!_connections.TryGetValue(equipmentId, out var connection))
            return false;

        try
        {
            await connection.SendProcessProgramSelectAsync(recipeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error selecting recipe on {EquipmentId}", equipmentId);
            return false;
        }
    }

    public async Task<bool> RequestVariablesAsync(string equipmentId, List<string> variableIds)
    {
        if (!_connections.TryGetValue(equipmentId, out var connection))
            return false;

        try
        {
            await connection.SendVariableRequestAsync(variableIds);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting variables from {EquipmentId}", equipmentId);
            return false;
        }
    }
}
