using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using EAP.Core;
using log4net;
using Opc.Ua;
using Opc.Ua.Client;

namespace EAP.Adapters.OpcUa;

public class OpcUaClient : ProtocolClientBase
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(OpcUaClient));
    
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private Session? _session;
    private object? _reconnectHandler;
    private readonly ConcurrentDictionary<string, MonitoredItem> _monitoredItems = new();
    private new readonly ConcurrentDictionary<string, EAP.Core.DataValue> _tagValues = new();

    public override ProtocolType ProtocolType => ProtocolType.OpcUa;
    public override bool IsConnected => _session?.Connected ?? false;

    public OpcUaClient(DeviceConfig config) : base(config)
    {
        if (config.OpcUaConfig == null)
        {
            throw new ArgumentException("OPC UA configuration is required", nameof(config));
        }
    }

    public override async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
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

                await applicationConfig.ValidateAsync(ApplicationType.Client).ConfigureAwait(false);

                _session = await CreateSessionAsync(applicationConfig, config).ConfigureAwait(false);

                Logger.Info($"OPC UA session created: {_session.SessionId}");

                if (config.EnableAutoReconnect)
                {
                    try
                    {
                        var reconnectHandlerType = Type.GetType("Opc.Ua.Client.SessionReconnectHandler, OPCFoundation.NetStandard.Opc.Ua");
                        if (reconnectHandlerType != null)
                        {
                            _reconnectHandler = Activator.CreateInstance(reconnectHandlerType!)!;
                            var beginReconnectMethod = reconnectHandlerType.GetMethod("BeginReconnect", new[] { typeof(Session), typeof(int), typeof(EventHandler) });
                            beginReconnectMethod?.Invoke(_reconnectHandler, new object[] { _session, config.ReconnectInterval, null! });
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Failed to create reconnect handler: {ex.Message}");
                    }
                }

                _session.KeepAlive += Session_KeepAlive;

                Logger.Info($"OPC UA client connected successfully: {ConnectionId}");
                OnConnectionStatusChanged(true, "Connected");

                return true;
            }
            catch (Exception ex)
                {
                    Logger.Error($"Failed to connect to OPC UA server: {ex.Message}", ex);
                    OnConnectionStatusChanged(false, "Connection failed", ex.Message);
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
                    null! 
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

    public override async Task DisconnectAsync(CancellationToken cancellationToken = default)
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

    public override async Task<EAP.Core.DataValue> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _session == null)
        {
            Logger.Warn($"OPC UA client not connected, cannot read node: {nodeId}");
            return EAP.Core.DataValue.NotConnected();
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
            return EAP.Core.DataValue.Bad(ex.Message);
        }
    }

    public override async Task<bool> WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default)
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

    public override async Task SubscribeNodeAsync(string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default)
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
            
            Opc.Ua.DataValue? opcValue = null;
            if (e.NotificationValue != null && e.NotificationValue is Opc.Ua.DataValue)
            {
                opcValue = (Opc.Ua.DataValue)e.NotificationValue;
            }
            else if (e.NotificationValue != null)
            {
                opcValue = new Opc.Ua.DataValue(new Variant(e.NotificationValue));
            }
            
            var value = opcValue != null ? ConvertToDataValue(opcValue) : EAP.Core.DataValue.Bad("No value");
            
            _tagValues[nodeId] = value;
            OnDataValueChanged(nodeId, value);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error processing monitored item notification: {ex.Message}", ex);
        }
    }

    public override async Task UnsubscribeNodeAsync(string nodeId, CancellationToken cancellationToken = default)
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
                subscription?.RemoveItem(item);
                if (subscription != null)
                {
                    await subscription.ApplyChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                
                Logger.Info($"OPC UA node unsubscribed: {nodeId}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error unsubscribing OPC UA node {nodeId}: {ex.Message}", ex);
        }
    }

    private EAP.Core.DataValue ConvertToDataValue(Opc.Ua.DataValue opcValue)
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

        return new EAP.Core.DataValue
        {
            Value = opcValue.Value,
            Quality = quality,
            Timestamp = opcValue.ServerTimestamp.ToUniversalTime(),
            ErrorMessage = ServiceResult.IsNotGood(opcValue.StatusCode) ? opcValue.StatusCode.ToString() : null
        };
    }

    public override void Dispose()
    {
        base.Dispose();
        _connectLock.Dispose();
    }
}