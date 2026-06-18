using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using EAP.Core.Configuration;
using EAP.Core.Protocol;
using log4net;

namespace EAP.Adapters.OpcDa;

public class OpcDaClient : IProtocolClient
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(OpcDaClient));
    
    private readonly DeviceConfig _deviceConfig;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private object? _server;
    private bool _isConnected;
    private bool _heartbeatStatus = false;
    private DateTime _lastHeartbeatTime = DateTime.MinValue;
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(10);
    private readonly ConcurrentDictionary<string, object> _tagValues = new();
    private readonly ConcurrentDictionary<string, int> _subscribedTags = new();
    private Task? _pollingTask;
    private CancellationTokenSource? _pollingCts;

    public EAP.Core.Configuration.ProtocolType ProtocolType => EAP.Core.Configuration.ProtocolType.OpcDa;
    public string ConnectionId => _deviceConfig.Id;
    public bool IsConnected => _isConnected;
    public bool HeartbeatStatus => _heartbeatStatus;

    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    public event EventHandler<DataValueChangedEventArgs>? DataValueChanged;
    public event EventHandler<HeartbeatStatusChangedEventArgs>? HeartbeatStatusChanged;

    public OpcDaClient(DeviceConfig config)
    {
        _deviceConfig = config ?? throw new ArgumentNullException(nameof(config));
        if (config.OpcDaConfig == null)
        {
            throw new ArgumentException("OPC DA configuration is required", nameof(config));
        }
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected)
            {
                Logger.Warn($"OPC DA client already connected: {ConnectionId}");
                return true;
            }

            var config = _deviceConfig.OpcDaConfig!;
            Logger.Info($"Connecting to OPC DA server: {config.ServerProgId}, Host: {config.RemoteHost ?? "localhost"}");

            try
            {
                _server = await CreateOpcDaServerAsync(config, cancellationToken).ConfigureAwait(false);

                _isConnected = true;
                Logger.Info($"OPC DA client connected successfully: {ConnectionId}");
                OnConnectionStatusChanged(true, "Connected");

                StartPolling();

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to connect to OPC DA server: {ex.Message}", ex);
                OnConnectionStatusChanged(false, "Connection failed", ex.Message);
                Cleanup();
                return false;
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task<object?> CreateOpcDaServerAsync(OpcDaConfig config, CancellationToken cancellationToken)
    {
        try
        {
            var opcDaNetCore = Type.GetType("OpcDaNetCore.OpcDaFactory, OpcDaNetCore");
            if (opcDaNetCore == null)
            {
                throw new TypeLoadException("Cannot load OpcDaFactory from OpcDaNetCore");
            }

            var factory = Activator.CreateInstance(opcDaNetCore);
            if (factory == null)
            {
                throw new InvalidOperationException("Failed to create OpcDaFactory instance");
            }

            var withIpMethod = opcDaNetCore.GetMethod("WithIp", new[] { typeof(string) });
            if (withIpMethod != null && !string.IsNullOrEmpty(config.RemoteHost))
            {
                factory = withIpMethod.Invoke(factory, new object[] { config.RemoteHost });
            }

            var withCredentialsMethod = opcDaNetCore.GetMethod("WithCredentials", new[] { typeof(string), typeof(string) });
            if (withCredentialsMethod != null && !config.UseAnonymousAuth && !string.IsNullOrEmpty(config.Username))
            {
                factory = withCredentialsMethod.Invoke(factory, new object[] { config.Username, config.Password });
            }

            var withServerNameMethod = opcDaNetCore.GetMethod("WithServerName", new[] { typeof(string) });
            if (withServerNameMethod == null)
            {
                throw new InvalidOperationException("Cannot find WithServerName method");
            }
            factory = withServerNameMethod.Invoke(factory, new object[] { config.ServerProgId });

            var buildAsyncMethod = opcDaNetCore.GetMethod("BuildAsync", new[] { typeof(CancellationToken) });
            if (buildAsyncMethod == null)
            {
                throw new InvalidOperationException("Cannot find BuildAsync method");
            }

            var task = (Task?)buildAsyncMethod.Invoke(factory, new object[] { cancellationToken });
            if (task != null)
            {
                await task.ConfigureAwait(false);
                var resultProperty = task.GetType().GetProperty("Result");
                return resultProperty?.GetValue(task);
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error creating OPC DA server: {ex.Message}", ex);
            throw;
        }
    }

    private void StartPolling()
    {
        _pollingCts = new CancellationTokenSource();
        _pollingTask = Task.Run(async () =>
        {
            while (!_pollingCts.Token.IsCancellationRequested && IsConnected)
            {
                try
                {
                    bool anyReadSuccess = false;
                    
                    foreach (var tag in _subscribedTags)
                    {
                        if (_pollingCts.Token.IsCancellationRequested) break;
                        
                        var nodeId = tag.Key;
                        var updateRate = tag.Value;
                        
                        try
                        {
                            var value = await ReadNodeAsync(nodeId, _pollingCts.Token).ConfigureAwait(false);
                            if (value.Quality == DataQuality.Good && value.Value != null)
                            {
                                _tagValues[nodeId] = value.Value;
                                DataValueChanged?.Invoke(this, new DataValueChangedEventArgs
                                {
                                    ConnectionId = ConnectionId,
                                    NodeId = nodeId,
                                    Value = value
                                });
                                anyReadSuccess = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug($"Error polling tag {nodeId}: {ex.Message}");
                        }
                        
                        await Task.Delay(updateRate, _pollingCts.Token).ConfigureAwait(false);
                    }
                    
                    // 更新心跳状态
                    UpdateHeartbeatStatus(anyReadSuccess);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error in polling loop: {ex.Message}", ex);
                    UpdateHeartbeatStatus(false);
                }
            }
        });
    }
    
    private void UpdateHeartbeatStatus(bool isNormal)
    {
        bool oldStatus = _heartbeatStatus;
        
        if (isNormal)
        {
            _lastHeartbeatTime = DateTime.Now;
            _heartbeatStatus = true;
        }
        else
        {
            // 检查是否超时
            if (DateTime.Now - _lastHeartbeatTime > _heartbeatTimeout)
            {
                _heartbeatStatus = false;
                // 心跳超时，断开连接
                Logger.Warn($"Heartbeat timeout for device {ConnectionId}, disconnecting...");
                _ = DisconnectAsync(CancellationToken.None);
            }
        }
        
        // 如果状态变化，触发事件
        if (oldStatus != _heartbeatStatus)
        {
            HeartbeatStatusChanged?.Invoke(this, new HeartbeatStatusChangedEventArgs
            {
                ConnectionId = ConnectionId,
                IsNormal = _heartbeatStatus,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _pollingCts?.Cancel();
            if (_pollingTask != null)
            {
                await _pollingTask.ConfigureAwait(false);
            }

            Cleanup();
            _isConnected = false;
            Logger.Info($"OPC DA client disconnected: {ConnectionId}");
            OnConnectionStatusChanged(false, "Disconnected");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error disconnecting OPC DA client: {ex.Message}", ex);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private void Cleanup()
    {
        if (_server is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _server = null;
        _pollingTask = null;
        _pollingCts = null;
    }

    public async Task<DataValue> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _server == null)
        {
            Logger.Warn($"OPC DA client not connected, cannot read node: {nodeId}");
            return EAP.Core.Protocol.DataValue.NotConnected();
        }

        try
            {
                Logger.Debug($"Reading OPC DA node: {nodeId}");
                
                var groupName = GetGroupName(nodeId);
                var readResult = InvokeRead(_server, groupName, nodeId);
                
                if (readResult != null)
                {
                    var result = GetFirstItemFromResult(readResult);
                    if (result != null)
                    {
                        return ExtractDataValueFromResult(result);
                    }
                }

                return EAP.Core.Protocol.DataValue.Bad("No data returned");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reading OPC DA node {nodeId}: {ex.Message}", ex);
                return EAP.Core.Protocol.DataValue.Bad(ex.Message);
            }
        }

        private object? InvokeRead(object server, string groupName, string nodeId)
        {
            var method = server.GetType().GetMethod("Read", new[] { typeof(string), typeof(string[]) });
            if (method != null)
            {
                return method.Invoke(server, new object[] { groupName, new[] { nodeId } });
            }
            return null;
        }

        private object? GetFirstItemFromResult(object readResult)
        {
            try
            {
                var resultType = readResult.GetType();
                if (resultType.IsArray)
                {
                    var array = (Array)readResult;
                    if (array.Length > 0)
                    {
                        return array.GetValue(0);
                    }
                }
                var listProp = resultType.GetProperty("Item");
                if (listProp != null)
                {
                    return listProp.GetValue(readResult, new object[] { 0 });
                }
            }
            catch
            {
                // Ignore reflection errors
            }
            return null;
        }

        private EAP.Core.Protocol.DataValue ExtractDataValueFromResult(object result)
        {
            try
            {
                var resultType = result.GetType();
                var valueProp = resultType.GetProperty("Value");
                var qualityProp = resultType.GetProperty("Quality");
                var timestampProp = resultType.GetProperty("Timestamp");
                var errorProp = resultType.GetProperty("ErrorMessage");

                object? value = valueProp?.GetValue(result);
                var quality = DataQuality.Good;
                
                if (qualityProp != null)
                {
                    var qualityValue = qualityProp.GetValue(result);
                    if (qualityValue != null)
                    {
                        var qualityString = qualityValue.ToString()?.ToLower();
                        if (qualityString?.Contains("bad") == true)
                        {
                            quality = DataQuality.Bad;
                        }
                        else if (qualityString?.Contains("uncertain") == true)
                        {
                            quality = DataQuality.Uncertain;
                        }
                    }
                }

                DateTime timestamp = timestampProp != null ? (DateTime)(timestampProp.GetValue(result) ?? DateTime.UtcNow) : DateTime.UtcNow;
                string? errorMessage = errorProp?.GetValue(result)?.ToString();

                return new EAP.Core.Protocol.DataValue
                {
                    Value = value,
                    Quality = quality,
                    Timestamp = timestamp,
                    ErrorMessage = quality == DataQuality.Bad ? errorMessage : null
                };
            }
            catch
            {
                return EAP.Core.Protocol.DataValue.Bad("Failed to extract data value");
            }
        }

    public async Task<bool> WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || _server == null)
            {
                Logger.Warn($"OPC DA client not connected, cannot write node: {nodeId}");
                return false;
            }

            try
            {
                Logger.Debug($"Writing OPC DA node {nodeId} with value: {value}");
                
                var groupName = GetGroupName(nodeId);
                InvokeAddItems(_server, groupName, nodeId);
                
                var itemValues = CreateItemValues(nodeId, value);
                InvokeWrite(_server, groupName, itemValues);

                _tagValues[nodeId] = value;
                Logger.Info($"Successfully wrote to OPC DA node {nodeId}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error writing OPC DA node {nodeId}: {ex.Message}", ex);
                return false;
            }
        }

        private void InvokeAddItems(object server, string groupName, string nodeId)
        {
            var method = server.GetType().GetMethod("AddItems", new[] { typeof(string), typeof(string[]) });
            if (method != null)
            {
                method.Invoke(server, new object[] { groupName, new[] { nodeId } });
            }
        }

        private object[] CreateItemValues(string nodeId, object value)
        {
            var serverType = _server?.GetType();
            if (serverType != null)
            {
                var itemValueType = serverType.Assembly.GetType("OpcDaNetCore.ItemDataValue");
                if (itemValueType != null)
                {
                    var constructor = itemValueType.GetConstructor(new[] { typeof(string), typeof(object) });
                    if (constructor != null)
                    {
                        var itemValue = constructor.Invoke(new object[] { nodeId, value });
                        return new[] { itemValue };
                    }
                }
            }
            return new[] { new { ItemId = nodeId, Value = value } };
        }

        private void InvokeWrite(object server, string groupName, object[] values)
        {
            var method = server.GetType().GetMethod("Write", new[] { typeof(string), values.GetType() });
            if (method != null)
            {
                method.Invoke(server, new object[] { groupName, values });
            }
            else
            {
                var genericMethod = server.GetType().GetMethod("Write");
                if (genericMethod != null)
                {
                    genericMethod.Invoke(server, new object[] { groupName, values });
                }
            }
        }

        private string GetGroupName(string nodeId)
        {
            return $"EAP_{ConnectionId}_Group";
        }

    public Task SubscribeNodeAsync(string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default)
        {
            Logger.Info($"OPC DA subscription requested for node: {nodeId} (updateRate: {updateRate})");
            _subscribedTags[nodeId] = updateRate;
            return Task.CompletedTask;
        }

        public Task UnsubscribeNodeAsync(string nodeId, CancellationToken cancellationToken = default)
        {
            Logger.Info($"OPC DA unsubscription requested for node: {nodeId}");
            _subscribedTags.TryRemove(nodeId, out _);
            
            if (_server != null)
            {
                var groupName = GetGroupName(nodeId);
                try
                {
                    InvokeRemoveItems(_server, groupName, nodeId);
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Error removing item from group: {ex.Message}");
                }
            }
            
            return Task.CompletedTask;
        }

        private void InvokeRemoveItems(object server, string groupName, string nodeId)
        {
            var method = server.GetType().GetMethod("RemoveItems", new[] { typeof(string), typeof(string[]) });
            if (method != null)
            {
                method.Invoke(server, new object[] { groupName, new[] { nodeId } });
            }
        }

        private void OnConnectionStatusChanged(bool isConnected, string status, string? errorMessage = null)
        {
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
            {
                ConnectionId = ConnectionId,
                IsConnected = isConnected,
                Status = status,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            });
        }

        public void Dispose()
        {
            _ = DisconnectAsync(CancellationToken.None);
            _connectLock.Dispose();
        }
    }
