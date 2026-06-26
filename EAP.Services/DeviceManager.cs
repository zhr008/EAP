using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EAP.Adapters.Factory;
using EAP.Core;
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
    IEnumerable<string> GetConnectedDevices();
    bool IsDeviceConnected(string deviceId);
    bool GetDeviceHeartbeatStatus(string deviceId);
    IEnumerable<DeviceConfig> GetDevices();
    IEnumerable<TagConfig> GetTags(string? deviceId = null);
    void ReloadConfiguration(string configDirectory);
    
    event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    event EventHandler<DataValueChangedEventArgs>? DataValueChanged;
    event EventHandler<HeartbeatStatusChangedEventArgs>? HeartbeatStatusChanged;
}

public class DeviceManager : IDeviceManager
{
    private static readonly ILog _logger = log4net.LogManager.GetLogger(typeof(DeviceManager));
    private EAPConfiguration _config;
    private readonly ConcurrentDictionary<string, IProtocolClient> _clients = new();
    private readonly SemaphoreSlim _clientLock = new(Environment.ProcessorCount, Environment.ProcessorCount);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _reconnectCts = new();
    private readonly ConcurrentDictionary<string, bool> _connectingDevices = new();
    private readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(5);

    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    public event EventHandler<DataValueChangedEventArgs>? DataValueChanged;
    public event EventHandler<HeartbeatStatusChangedEventArgs>? HeartbeatStatusChanged;

    public DeviceManager()
    {
        _config = ConfigurationLoader.GetConfiguration();
    }
    
    public DeviceManager(EAPConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<bool> ConnectAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.Info("Connecting all devices...");
        
        var tasks = new List<Task<bool>>();
        var enabledDevices = _config.Devices.Where(d => d.Enabled).ToList();
        
        foreach (var device in enabledDevices)
        {
            tasks.Add(ConnectDeviceAsync(device.DeviceId, cancellationToken));
        }
        
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var success = results.All(r => r);
        
        _logger.Info($"Connect all devices completed. Success: {success}");
        return success;
    }

    public async Task<bool> ConnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (_clients.ContainsKey(deviceId))
        {
            _logger.Warn($"Device already connected: {deviceId}");
            return true;
        }

        if (!_connectingDevices.TryAdd(deviceId, true))
        {
            _logger.Warn($"Device is already connecting: {deviceId}");
            return false;
        }

        try
        {
            var device = _config.Devices.FirstOrDefault(d => d.DeviceId == deviceId);
            if (device == null)
            {
                _logger.Error($"Device not found: {deviceId}");
                return false;
            }

            return await ConnectDeviceInternalAsync(deviceId, device, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectingDevices.TryRemove(deviceId, out _);
        }
    }

    private async Task<bool> ConnectDeviceInternalAsync(string deviceId, DeviceConfig deviceConfig, CancellationToken cancellationToken)
        {
            await _clientLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_clients.ContainsKey(deviceId))
                {
                    _logger.Info($"Device {deviceId} is already connected");
                    return true;
                }

                IProtocolClient client;

                _logger.Info($"Creating protocol client for {deviceId}, ProtocolType: {deviceConfig.ProtocolType}");
                client = ProtocolClientFactory.CreateClient(deviceConfig);
                _logger.Info($"Client created: {client.GetType().Name}");

                client.ConnectionStatusChanged += Client_ConnectionStatusChanged;
                client.DataValueChanged += Client_DataValueChanged;
                client.HeartbeatStatusChanged += Client_HeartbeatStatusChanged;

                _logger.Info($"Connecting to device {deviceId}...");
                var success = await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
                
                if (success)
                {
                    _logger.Info($"Device {deviceId} connected successfully");
                    _clients.TryAdd(deviceId, client);
                    
                    if (deviceConfig.Tags != null && deviceConfig.Tags.Any())
                    {
                        _logger.Info($"Subscribing to {deviceConfig.Tags.Count} tags for device {deviceId}");
                        var subscribeTasks = deviceConfig.Tags.Select(tag => 
                            client.SubscribeNodeAsync(tag.NodeId, tag.ReadOnly ? 1000 : 500, cancellationToken));
                        await Task.WhenAll(subscribeTasks).ConfigureAwait(false);
                        _logger.Info($"All tags subscribed for device {deviceId}");
                    }
                }
                else
                {
                    _logger.Error($"Device {deviceId} connection failed - ConnectAsync returned false");
                    client.ConnectionStatusChanged -= Client_ConnectionStatusChanged;
                    client.DataValueChanged -= Client_DataValueChanged;
                    client.HeartbeatStatusChanged -= Client_HeartbeatStatusChanged;
                    
                    StartReconnect(deviceId);
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
        
        if (!e.IsConnected)
        {
            if (_clients.TryRemove(e.ConnectionId, out var client))
            {
                client.ConnectionStatusChanged -= Client_ConnectionStatusChanged;
                client.DataValueChanged -= Client_DataValueChanged;
                client.HeartbeatStatusChanged -= Client_HeartbeatStatusChanged;
                client.Dispose();
                _logger.Info($"Client removed from cache after disconnection: {e.ConnectionId}");
            }
            
            StartReconnect(e.ConnectionId);
        }
    }
    
    private void StartReconnect(string deviceId)
    {
        if (!_reconnectCts.TryAdd(deviceId, new CancellationTokenSource()))
        {
            _logger.Warn($"Reconnect task already running for device {deviceId}");
            return;
        }
        
        Task.Run(async () =>
        {
            var cts = _reconnectCts[deviceId];
            int attempt = 0;
            
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    attempt++;
                    
                    if (attempt > 1)
                    {
                        await Task.Delay(_reconnectDelay, cts.Token).ConfigureAwait(false);
                    }
                    
                    _logger.Info($"Attempting to reconnect device {deviceId}, attempt {attempt}");
                    
                    try
                    {
                        var success = await ConnectDeviceAsync(deviceId, cts.Token).ConfigureAwait(false);
                        if (success)
                        {
                            _logger.Info($"Successfully reconnected device {deviceId} after {attempt} attempts");
                            
                            if (_clients.ContainsKey(deviceId))
                            {
                                _logger.Info($"Device {deviceId} is connected and in cache, exiting reconnect loop");
                                break;
                            }
                            else
                            {
                                _logger.Warn($"Device {deviceId} connected but not in cache, continuing reconnect");
                            }
                        }
                        else
                        {
                            _logger.Warn($"Reconnect attempt {attempt} for device {deviceId} failed - ConnectAsync returned false");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Reconnect attempt {attempt} for device {deviceId} failed: {ex.Message}");
                    }
                    
                    if (!cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(_reconnectDelay, cts.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"Reconnect cancelled for device {deviceId}");
            }
            finally
            {
                if (_reconnectCts.TryRemove(deviceId, out var tokenSource))
                {
                    tokenSource.Dispose();
                }
                _logger.Info($"Reconnect task ended for device {deviceId} after {attempt} attempts");
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
    }

    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.Info("Disconnecting all devices...");

        foreach (var deviceId in _clients.Keys.ToList())
        {
            if (_reconnectCts.TryRemove(deviceId, out var reconnectCts))
            {
                reconnectCts.Cancel();
                reconnectCts.Dispose();
            }
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
                _logger.Error($"Error disconnecting client: {ex.Message}", ex);
            }
        }

        _clients.Clear();
        _logger.Info("All devices disconnected");
    }

    public async Task DisconnectDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        if (_reconnectCts.TryRemove(deviceId, out var reconnectCts))
        {
            reconnectCts.Cancel();
            reconnectCts.Dispose();
            _logger.Info($"Reconnect cancelled for device {deviceId}");
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
                
                _logger.Info($"Device disconnected: {deviceId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error disconnecting device {deviceId}: {ex.Message}", ex);
            }
        }
        else
        {
            _logger.Warn($"Device not found or not connected: {deviceId}");
        }
    }

    public async Task<DataValue> ReadTagAsync(string deviceId, string nodeId, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(deviceId, out var client))
        {
            _logger.Error($"Device not connected: {deviceId}");
            return EAP.Core.DataValue.NotConnected();
        }

        return await client.ReadNodeAsync(nodeId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> WriteTagAsync(string deviceId, string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(deviceId, out var client))
        {
            _logger.Error($"Device not connected: {deviceId}");
            return false;
        }

        return await client.WriteNodeAsync(nodeId, value, cancellationToken).ConfigureAwait(false);
    }

    public async Task SubscribeTagAsync(string deviceId, string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(deviceId, out var client))
        {
            _logger.Error($"Device not connected: {deviceId}");
            return;
        }

        await client.SubscribeNodeAsync(nodeId, updateRate, cancellationToken).ConfigureAwait(false);
    }

    public async Task UnsubscribeTagAsync(string deviceId, string nodeId, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(deviceId, out var client))
        {
            _logger.Error($"Device not connected: {deviceId}");
            return;
        }

        await client.UnsubscribeNodeAsync(nodeId, cancellationToken).ConfigureAwait(false);
    }
    

    public IEnumerable<string> GetConnectedDevices()
    {
        return _clients.Keys.ToList();
    }

    public bool IsDeviceConnected(string deviceId)
    {
        return _clients.ContainsKey(deviceId);
    }

    public bool GetDeviceHeartbeatStatus(string deviceId)
    {
        if (_clients.TryGetValue(deviceId, out var client))
        {
            return client.HeartbeatStatus;
        }
        return false;
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

        var device = _config.Devices.FirstOrDefault(d => d.DeviceId == deviceId);
        return device?.Tags ?? [];
    }

    public void ReloadConfiguration(string configDirectory)
    {
        _logger.Info($"正在从目录重新加载配置：{configDirectory}");
        
        try
        {
            DisconnectAllAsync().Wait();
            
            _clients.Clear();
            
            ConfigurationLoader.Refresh();
            _config = ConfigurationLoader.GetConfiguration();
            
            _logger.Info($"配置重新加载成功，找到 {_config.Devices.Count} 台设备");
        }
        catch (Exception ex)
        {
            _logger.Error($"配置重新加载失败：{ex.Message}", ex);
            throw;
        }
    }
}