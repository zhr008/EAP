using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using EAP.Core.Configuration;
using EAP.Core.Protocol;
using log4net;
using Opc.Ua;
using Opc.Ua.Client;

namespace EAP.Adapters.OpcUa;

public class OpcUaClient : IProtocolClient
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(OpcUaClient));
    
    private readonly DeviceConfig _deviceConfig;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private Session? _session;
    private object? _reconnectHandler;
    private bool _isConnected;
    private bool _heartbeatStatus = false;
    private DateTime _lastHeartbeatTime = DateTime.MinValue;
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(10);
    private readonly ConcurrentDictionary<string, MonitoredItem> _monitoredItems = new();
    private readonly ConcurrentDictionary<string, EAP.Core.Protocol.DataValue> _tagValues = new();

    public EAP.Core.Configuration.ProtocolType ProtocolType => EAP.Core.Configuration.ProtocolType.OpcUa;
    public string ConnectionId => _deviceConfig.Id;
    public bool IsConnected => _session?.Connected ?? false;
    public bool HeartbeatStatus => _heartbeatStatus;

    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    public event EventHandler<DataValueChangedEventArgs>? DataValueChanged;
    public event EventHandler<HeartbeatStatusChangedEventArgs>? HeartbeatStatusChanged;

    public OpcUaClient(DeviceConfig config)
    {
        _deviceConfig = config ?? throw new ArgumentNullException(nameof(config));
        if (config.OpcUaConfig == null)
        {
            throw new ArgumentException("OPC UA configuration is required", nameof(config));
        }
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected)
            {
                Logger.Warn($"OPC UA client already connected: {ConnectionId}");
                return true;
            }

            var config = _deviceConfig.OpcUaConfig!;
            Logger.Info($"Connecting to OPC UA server: {config.EndpointUrl}");

            try
            {
                var applicationConfig = new ApplicationConfiguration
                {
                    ApplicationName = "EAP Client",
                    ApplicationType = ApplicationType.Client,
                    SecurityConfiguration = new SecurityConfiguration
                    {
                        ApplicationCertificate = new CertificateIdentifier(),
                        TrustedIssuerCertificates = new CertificateTrustList(),
                        TrustedPeerCertificates = new CertificateTrustList(),
                        RejectedCertificateStore = new CertificateTrustList(),
                        AutoAcceptUntrustedCertificates = config.SkipCertificateValidation
                    },
                    TransportConfigurations = new TransportConfigurationCollection(),
                    TransportQuotas = new TransportQuotas
                    {
                        OperationTimeout = config.SessionTimeout
                    },
                    ClientConfiguration = new ClientConfiguration
                    {
                        DefaultSessionTimeout = config.SessionTimeout
                    }
                };

                await applicationConfig.Validate(ApplicationType.Client).ConfigureAwait(false);

                _session = await CreateSessionAsync(applicationConfig, config).ConfigureAwait(false);

                Logger.Info($"OPC UA session created: {_session.SessionId}");

                if (config.EnableAutoReconnect)
                {
                    try
                    {
                        var reconnectHandlerType = Type.GetType("Opc.Ua.Client.SessionReconnectHandler, OPCFoundation.NetStandard.Opc.Ua");
                        if (reconnectHandlerType != null)
                        {
                            _reconnectHandler = Activator.CreateInstance(reconnectHandlerType);
                            var beginReconnectMethod = reconnectHandlerType.GetMethod("BeginReconnect", new[] { typeof(Session), typeof(int), typeof(EventHandler) });
                            beginReconnectMethod?.Invoke(_reconnectHandler, new object[] { _session, config.ReconnectInterval, null });
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Failed to create reconnect handler: {ex.Message}");
                    }
                }

                _session.KeepAlive += Session_KeepAlive;

                _isConnected = true;
                Logger.Info($"OPC UA client connected successfully: {ConnectionId}");
                OnConnectionStatusChanged(true, "Connected");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to connect to OPC UA server: {ex.Message}", ex);
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

    private UserIdentity CreateUserIdentity(OpcUaConfig config)
    {
        if (!config.UseAnonymousAuth && !string.IsNullOrEmpty(config.Username) && !string.IsNullOrEmpty(config.Password))
        {
            var userNameToken = new UserNameIdentityToken
            {
                UserName = config.Username,
                Password = System.Text.Encoding.UTF8.GetBytes(config.Password)
            };
            return new UserIdentity(userNameToken);
        }
        
        return new UserIdentity();
    }

    private async Task<Session> CreateSessionAsync(ApplicationConfiguration applicationConfig, OpcUaConfig config)
    {
        var endpointDescription = new EndpointDescription(config.EndpointUrl);
        var endpoint = new ConfiguredEndpoint(null, endpointDescription);
        
        var userIdentity = CreateUserIdentity(config);
        
        var sessionType = typeof(Session);
        var createMethods = sessionType.GetMethods().Where(m => m.Name == "Create" && m.IsStatic).ToList();
        
        foreach (var method in createMethods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 6 && 
                parameters[0].ParameterType == typeof(ApplicationConfiguration) &&
                parameters[2].ParameterType == typeof(ConfiguredEndpoint))
            {
                var task = (Task<Session>?)method.Invoke(null, new object[] 
                { 
                    applicationConfig, 
                    false, 
                    endpoint, 
                    "EAP Client", 
                    userIdentity, 
                    null 
                });
                
                if (task != null)
                {
                    return await task.ConfigureAwait(false);
                }
            }
        }
        
        throw new InvalidOperationException("Could not find suitable Session.Create method");
    }

    private void Session_KeepAlive(object? sender, KeepAliveEventArgs e)
    {
        if (e.Status != null && ServiceResult.IsNotGood(e.Status))
        {
            Logger.Warn($"OPC UA keep-alive error: {e.Status}");
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_reconnectHandler != null)
            {
                try
                {
                    var cancelMethod = _reconnectHandler.GetType().GetMethod("Cancel");
                    cancelMethod?.Invoke(_reconnectHandler, null);
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Error cancelling reconnect handler: {ex.Message}");
                }
            }
            
            if (_session != null)
            {
                _session.KeepAlive -= Session_KeepAlive;
                
                foreach (var item in _monitoredItems.Values)
                {
                    item.Notification -= MonitoredItem_Notification;
                }
                _monitoredItems.Clear();

                await _session.CloseAsync().ConfigureAwait(false);
                _session.Dispose();
                _session = null;
            }

            _isConnected = false;
            Logger.Info($"OPC UA client disconnected: {ConnectionId}");
            OnConnectionStatusChanged(false, "Disconnected");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error disconnecting OPC UA client: {ex.Message}", ex);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private void Cleanup()
    {
        if (_reconnectHandler != null)
        {
            var cancelMethod = _reconnectHandler.GetType().GetMethod("Cancel");
            cancelMethod?.Invoke(_reconnectHandler, null);
        }
        _session?.Dispose();
        _session = null;
        _monitoredItems.Clear();
        _tagValues.Clear();
    }

    public async Task<EAP.Core.Protocol.DataValue> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            Logger.Warn($"OPC UA client not connected, cannot read node: {nodeId}");
            return EAP.Core.Protocol.DataValue.NotConnected();
        }

        try
        {
            Logger.Debug($"Reading OPC UA node: {nodeId}");
            
            var node = new NodeId(nodeId);
            var result = await _session.ReadValueAsync(node, cancellationToken).ConfigureAwait(false);

            return ConvertToDataValue(result);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error reading OPC UA node {nodeId}: {ex.Message}", ex);
            return EAP.Core.Protocol.DataValue.Bad(ex.Message);
        }
    }

    public async Task<bool> WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            Logger.Warn($"OPC UA client not connected, cannot write node: {nodeId}");
            return false;
        }

        try
        {
            Logger.Debug($"Writing OPC UA node {nodeId} with value: {value}");
            
            var node = new NodeId(nodeId);
            var writeValue = new WriteValue
            {
                NodeId = node,
                AttributeId = Attributes.Value,
                Value = new Opc.Ua.DataValue(new Variant(value))
            };

            var results = await _session.WriteAsync(null, new WriteValueCollection { writeValue }, cancellationToken).ConfigureAwait(false);
            
            var resultArray = results.GetType().GetProperty("Results")?.GetValue(results) as Array;
            if (resultArray != null && resultArray.Length > 0)
            {
                var firstResult = resultArray.GetValue(0);
                if (firstResult != null)
                {
                    var statusCode = firstResult.GetType().GetProperty("StatusCode")?.GetValue(firstResult);
                    if (statusCode != null && ServiceResult.IsGood((uint)statusCode))
                    {
                        Logger.Info($"Successfully wrote to OPC UA node {nodeId}");
                        return true;
                    }
                }
            }
            
            Logger.Error($"Failed to write to OPC UA node {nodeId}");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error writing OPC UA node {nodeId}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task SubscribeNodeAsync(string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            Logger.Warn($"OPC UA client not connected, cannot subscribe node: {nodeId}");
            return;
        }

        try
        {
            Logger.Info($"OPC UA subscription requested for node: {nodeId} (updateRate: {updateRate})");

            if (_monitoredItems.TryGetValue(nodeId, out var existingItem))
            {
                existingItem.Notification -= MonitoredItem_Notification;
            }

            var subscription = new Subscription(_session.DefaultSubscription)
            {
                PublishingInterval = updateRate,
                KeepAliveCount = 3,
                LifetimeCount = 10
            };
            _session.AddSubscription(subscription);
            await subscription.CreateAsync(cancellationToken).ConfigureAwait(false);

            var monitoredItem = new MonitoredItem(subscription.DefaultItem)
            {
                StartNodeId = new NodeId(nodeId),
                AttributeId = Attributes.Value,
                MonitoringMode = MonitoringMode.Reporting,
                SamplingInterval = updateRate
            };
            monitoredItem.Notification += MonitoredItem_Notification;
            
            subscription.AddItem(monitoredItem);
            await subscription.ApplyChangesAsync(cancellationToken).ConfigureAwait(false);

            _monitoredItems[nodeId] = monitoredItem;
            Logger.Info($"OPC UA node subscribed: {nodeId}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error subscribing OPC UA node {nodeId}: {ex.Message}", ex);
        }
    }

    private void MonitoredItem_Notification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        try
        {
            var nodeId = monitoredItem.StartNodeId.ToString();
            
            Opc.Ua.DataValue opcValue = null;
            if (e.NotificationValue != null && e.NotificationValue is Opc.Ua.DataValue)
            {
                opcValue = (Opc.Ua.DataValue)e.NotificationValue;
            }
            else if (e.NotificationValue != null)
            {
                opcValue = new Opc.Ua.DataValue(new Variant(e.NotificationValue));
            }
            
            var value = opcValue != null ? ConvertToDataValue(opcValue) : EAP.Core.Protocol.DataValue.Bad("No value");
            
            _tagValues[nodeId] = value;
            
            DataValueChanged?.Invoke(this, new DataValueChangedEventArgs
            {
                ConnectionId = ConnectionId,
                NodeId = nodeId,
                Value = value
            });
            
            // 收到数据表示心跳正常
            UpdateHeartbeatStatus(true);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error processing monitored item notification: {ex.Message}", ex);
        }
    }

    public async Task UnsubscribeNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            Logger.Warn($"OPC UA client not connected, cannot unsubscribe node: {nodeId}");
            return;
        }

        try
        {
            Logger.Info($"OPC UA unsubscription requested for node: {nodeId}");

            if (_monitoredItems.TryRemove(nodeId, out var item))
            {
                item.Notification -= MonitoredItem_Notification;
                
                var subscription = item.Subscription;
                subscription.RemoveItem(item);
                await subscription.ApplyChangesAsync(cancellationToken).ConfigureAwait(false);
                
                Logger.Info($"OPC UA node unsubscribed: {nodeId}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error unsubscribing OPC UA node {nodeId}: {ex.Message}", ex);
        }
    }

    private EAP.Core.Protocol.DataValue ConvertToDataValue(Opc.Ua.DataValue opcValue)
    {
        var quality = DataQuality.Bad;
        if (ServiceResult.IsGood(opcValue.StatusCode))
        {
            quality = DataQuality.Good;
        }
        else if (ServiceResult.IsUncertain(opcValue.StatusCode))
        {
            quality = DataQuality.Uncertain;
        }

        return new EAP.Core.Protocol.DataValue
        {
            Value = opcValue.Value,
            Quality = quality,
            Timestamp = opcValue.ServerTimestamp.ToUniversalTime(),
            ErrorMessage = ServiceResult.IsNotGood(opcValue.StatusCode) ? opcValue.StatusCode.ToString() : null
        };
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

    public void Dispose()
    {
        _ = DisconnectAsync(CancellationToken.None);
        _connectLock.Dispose();
    }
}
