using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EAP.Adapters.Factory;
using EAP.Core.Configuration;
using EAP.Core.Protocol;
using log4net;

namespace EAP.Services;

public interface IDeviceManager
{
    Task<bool> ConnectAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ConnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
    Task DisconnectAllAsync(CancellationToken cancellationToken = default);
    Task DisconnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default);
    Task<DataValue> ReadTagAsync(string deviceId, string nodeId, CancellationToken cancellationToken = default);
    Task<bool> WriteTagAsync(string deviceId, string nodeId, object value, CancellationToken cancellationToken = default);
    Task SubscribeTagAsync(string deviceId, string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default);
    Task UnsubscribeTagAsync(string deviceId, string nodeId, CancellationToken cancellationToken = default);
    Task PublishDataAsync(string deviceId, string nodeId, DataValue value);
    Task<bool> ReceiveAndWriteAsync(string deviceId, string nodeId, object value);
    IEnumerable<string> GetConnectedDevices();
    IEnumerable<DeviceConfig> GetDevices();
    IEnumerable<TagConfig> GetTags(string? deviceId = null);
    void ReloadConfiguration(string configDirectory);
    
    event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    event EventHandler<DataValueChangedEventArgs>? DataValueChanged;
    event EventHandler<HeartbeatStatusChangedEventArgs>? HeartbeatStatusChanged;
    event EventHandler<PublishDataEventArgs>? DataPublished;
}

public class PublishDataEventArgs : EventArgs
{
    public string DeviceId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public DataValue Value { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class DeviceManager : IDeviceManager
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(DeviceManager));
    
    private readonly EAPConfiguration _config;
    private readonly ConcurrentDictionary<string, IProtocolClient> _clients = new();
    private readonly SemaphoreSlim _clientLock = new(Environment.ProcessorCount, Environment.ProcessorCount);
    private readonly IDeviceProcessManager _processManager;
    private readonly bool _enableMultiProcess;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _reconnectCts = new();
    private readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(5); // 重连延迟

    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    public event EventHandler<DataValueChangedEventArgs>? DataValueChanged;
    public event EventHandler<HeartbeatStatusChangedEventArgs>? HeartbeatStatusChanged;
    public event EventHandler<PublishDataEventArgs>? DataPublished;

    public DeviceManager(EAPConfiguration config, string deviceAgentPath, bool enableMultiProcess = true)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _enableMultiProcess = enableMultiProcess;
        
        if (_enableMultiProcess)
        {
            _processManager = new DeviceProcessManager(deviceAgentPath);
            _processManager.ProcessStatusChanged += ProcessManager_ProcessStatusChanged;
        }
    }

    private void ProcessManager_ProcessStatusChanged(object? sender, ProcessStatusChangedEventArgs e)
    {
        Logger.Info($"Device process status changed: {e.DeviceId} - Running: {e.IsRunning}, ExitCode: {e.ExitCode}");
        
        if (!e.IsRunning && e.ExitCode != 0)
        {
            Logger.Warn($"Device process crashed: {e.DeviceId}, ExitCode: {e.ExitCode}, Error: {e.ErrorMessage}");
        }
    }

    public async Task<bool> ConnectAllAsync(CancellationToken cancellationToken = default)
    {
        Logger.Info("Connecting all devices...");
        
        var tasks = new List<Task<bool>>();
        var enabledDevices = _config.Devices.Where(d => d.Enabled).ToList();
        
        foreach (var device in enabledDevices)
        {
            tasks.Add(ConnectDeviceAsync(device.Id, cancellationToken));
        }
        
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var success = results.All(r => r);
        
        Logger.Info($"Connect all devices completed. Success: {success}");
        return success;
    }

    public async Task<bool> ConnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (_clients.ContainsKey(deviceId))
        {
            Logger.Warn($"Device already connected: {deviceId}");
            return true;
        }

        var device = _config.Devices.FirstOrDefault(d => d.Id == deviceId);
        if (device == null)
        {
            Logger.Error($"Device not found: {deviceId}");
            return false;
        }

        return await ConnectDeviceInternalAsync(deviceId, device, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ConnectDeviceInternalAsync(string deviceId, DeviceConfig deviceConfig, CancellationToken cancellationToken)
        {
            await _clientLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_clients.ContainsKey(deviceId))
                {
                    Logger.Info($"Device {deviceId} is already connected");
                    return true;
                }

                IProtocolClient client;

                if (_enableMultiProcess)
                {
                    Logger.Info($"Starting device process for {deviceId}");
                    await _processManager.StartDeviceProcessAsync(deviceConfig).ConfigureAwait(false);
                    
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                    
                    client = new DeviceAgentClient(deviceConfig);
                }
                else
                {
                    Logger.Info($"Creating protocol client for {deviceId}, ProtocolType: {deviceConfig.ProtocolType}");
                    client = ProtocolClientFactory.CreateClient(deviceConfig);
                    Logger.Info($"Client created: {client.GetType().Name}");
                }

                client.ConnectionStatusChanged += Client_ConnectionStatusChanged;
                client.DataValueChanged += Client_DataValueChanged;
                client.HeartbeatStatusChanged += Client_HeartbeatStatusChanged;

                Logger.Info($"Connecting to device {deviceId}...");
                var success = await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
                
                if (success)
                {
                    Logger.Info($"Device {deviceId} connected successfully");
                    _clients.TryAdd(deviceId, client);
                    
                    if (deviceConfig.Tags != null && deviceConfig.Tags.Any())
                    {
                        Logger.Info($"Subscribing to {deviceConfig.Tags.Count} tags for device {deviceId}");
                        var subscribeTasks = deviceConfig.Tags.Select(tag => 
                            client.SubscribeNodeAsync(tag.NodeId, tag.ReadOnly ? 1000 : 500, cancellationToken));
                        await Task.WhenAll(subscribeTasks).ConfigureAwait(false);
                        Logger.Info($"All tags subscribed for device {deviceId}");
                    }
                }
                else
                {
                    Logger.Error($"Device {deviceId} connection failed - ConnectAsync returned false");
                    client.ConnectionStatusChanged -= Client_ConnectionStatusChanged;
                    client.DataValueChanged -= Client_DataValueChanged;
                    client.HeartbeatStatusChanged -= Client_HeartbeatStatusChanged;
                    
                    if (_enableMultiProcess)
                    {
                        await _processManager.StopDeviceProcessAsync(deviceId).ConfigureAwait(false);
                    }
                }

                return success;
            }
        finally
        {
            _clientLock.Release();
        }
    }

    private void Client_ConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
    {
        ConnectionStatusChanged?.Invoke(this, e);
        
        if (!e.IsConnected && _enableMultiProcess)
        {
            Task.Run(async () =>
            {
                await _processManager.StopDeviceProcessAsync(e.ConnectionId).ConfigureAwait(false);
            });
        }
        
        // 当设备断开连接时，从客户端字典中移除
        if (!e.IsConnected)
        {
            if (_clients.TryRemove(e.ConnectionId, out var client))
            {
                client.ConnectionStatusChanged -= Client_ConnectionStatusChanged;
                client.DataValueChanged -= Client_DataValueChanged;
                client.HeartbeatStatusChanged -= Client_HeartbeatStatusChanged;
                client.Dispose();
                Logger.Info($"Client removed from cache after disconnection: {e.ConnectionId}");
            }
            
            // 启动自动重连
            StartReconnect(e.ConnectionId);
        }
    }
    
    private void StartReconnect(string deviceId)
    {
        // 如果已经有重连任务在运行，先取消它
        if (_reconnectCts.TryRemove(deviceId, out var existingCts))
        {
            existingCts.Cancel();
            existingCts.Dispose();
        }
        
        var cts = new CancellationTokenSource();
        _reconnectCts.TryAdd(deviceId, cts);
        
        Task.Run(async () =>
        {
            try
            {
                int attempt = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    attempt++;
                    Logger.Info($"Attempting to reconnect device {deviceId}, attempt {attempt}");
                    
                    try
                    {
                        var success = await ConnectDeviceAsync(deviceId, cts.Token).ConfigureAwait(false);
                        if (success)
                        {
                            Logger.Info($"Successfully reconnected device {deviceId} after {attempt} attempts");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Reconnect attempt {attempt} for device {deviceId} failed: {ex.Message}");
                    }
                    
                    // 等待后重试
                    await Task.Delay(_reconnectDelay, cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"Reconnect cancelled for device {deviceId}");
            }
            finally
            {
                _reconnectCts.TryRemove(deviceId, out _);
                cts.Dispose();
            }
        });
    }

    private void Client_HeartbeatStatusChanged(object? sender, HeartbeatStatusChangedEventArgs e)
    {
        HeartbeatStatusChanged?.Invoke(this, e);
    }

    private void Client_DataValueChanged(object? sender, DataValueChangedEventArgs e)
    {
        DataValueChanged?.Invoke(this, e);
        OnDataPublished(e.ConnectionId, e.NodeId, e.Value);
    }

    private void OnDataPublished(string deviceId, string nodeId, DataValue value)
    {
        DataPublished?.Invoke(this, new PublishDataEventArgs
        {
            DeviceId = deviceId,
            NodeId = nodeId,
            Value = value,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        Logger.Info("Disconnecting all devices...");

        if (_enableMultiProcess)
        {
            await _processManager.StopAllProcessesAsync().ConfigureAwait(false);
        }

        foreach (var client in _clients.Values)
        {
            try
            {
                await client.DisconnectAsync(cancellationToken).ConfigureAwait(false);
                client.ConnectionStatusChanged -= Client_ConnectionStatusChanged;
                client.DataValueChanged -= Client_DataValueChanged;
                client.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error disconnecting client: {ex.Message}", ex);
            }
        }

        _clients.Clear();
        Logger.Info("All devices disconnected");
    }

    public async Task DisconnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        // 取消可能正在进行的重连任务
        if (_reconnectCts.TryRemove(deviceId, out var reconnectCts))
        {
            reconnectCts.Cancel();
            reconnectCts.Dispose();
            Logger.Info($"Reconnect cancelled for device {deviceId}");
        }
        
        if (_clients.TryRemove(deviceId, out var client))
        {
            try
            {
                await client.DisconnectAsync(cancellationToken).ConfigureAwait(false);
                client.ConnectionStatusChanged -= Client_ConnectionStatusChanged;
                client.DataValueChanged -= Client_DataValueChanged;
                client.HeartbeatStatusChanged -= Client_HeartbeatStatusChanged;
                client.Dispose();
                
                if (_enableMultiProcess)
                {
                    await _processManager.StopDeviceProcessAsync(deviceId).ConfigureAwait(false);
                }
                
                Logger.Info($"Device disconnected: {deviceId}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error disconnecting device {deviceId}: {ex.Message}", ex);
            }
        }
        else
        {
            Logger.Warn($"Device not found or not connected: {deviceId}");
        }
    }

    public async Task<DataValue> ReadTagAsync(string deviceId, string nodeId, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(deviceId, out var client))
        {
            Logger.Error($"Device not connected: {deviceId}");
            return EAP.Core.Protocol.DataValue.NotConnected();
        }

        return await client.ReadNodeAsync(nodeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> WriteTagAsync(string deviceId, string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(deviceId, out var client))
        {
            Logger.Error($"Device not connected: {deviceId}");
            return false;
        }

        return await client.WriteNodeAsync(nodeId, value, cancellationToken).ConfigureAwait(false);
    }

    public async Task SubscribeTagAsync(string deviceId, string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(deviceId, out var client))
        {
            Logger.Error($"Device not connected: {deviceId}");
            return;
        }

        await client.SubscribeNodeAsync(nodeId, updateRate, cancellationToken).ConfigureAwait(false);
    }

    public async Task UnsubscribeTagAsync(string deviceId, string nodeId, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(deviceId, out var client))
        {
            Logger.Error($"Device not connected: {deviceId}");
            return;
        }

        await client.UnsubscribeNodeAsync(nodeId, cancellationToken).ConfigureAwait(false);
    }

    public Task PublishDataAsync(string deviceId, string nodeId, DataValue value)
    {
        Logger.Info($"Publishing data for {deviceId}:{nodeId}");
        OnDataPublished(deviceId, nodeId, value);
        return Task.CompletedTask;
    }

    public async Task<bool> ReceiveAndWriteAsync(string deviceId, string nodeId, object value)
    {
        Logger.Info($"Received data to write for {deviceId}:{nodeId} = {value}");
        return await WriteTagAsync(deviceId, nodeId, value).ConfigureAwait(false);
    }

    public IEnumerable<string> GetConnectedDevices()
    {
        return _clients.Keys.ToList();
    }

    public IEnumerable<DeviceConfig> GetDevices()
    {
        return _config.Devices.ToList();
    }

    public IEnumerable<TagConfig> GetTags(string? deviceId = null)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return _config.Devices.SelectMany(d => d.Tags).ToList();
        }

        var device = _config.Devices.FirstOrDefault(d => d.Id == deviceId);
        return device?.Tags ?? [];
    }

    public void ReloadConfiguration(string configDirectory)
    {
        Logger.Info($"正在从目录重新加载配置：{configDirectory}");
        
        try
        {
            DisconnectAllAsync().Wait();
            
            _clients.Clear();
            
            var newConfig = ConfigurationLoader.LoadConfiguration(configDirectory);
            
            System.Reflection.FieldInfo? field = typeof(DeviceManager).GetField("_config", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(this, newConfig);
            }
            
            Logger.Info($"配置重新加载成功，找到 {newConfig.Devices.Count} 台设备");
        }
        catch (Exception ex)
        {
            Logger.Error($"配置重新加载失败：{ex.Message}", ex);
            throw;
        }
    }
}
