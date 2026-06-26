using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EAP.Core;
using log4net;
using OpcDaNetCore.Contracts;
using OpcDaNetCore.Factory;
using OpcDaNetCore.ValueObjects;

namespace EAP.Adapters.OpcDa;

public class OpcDaClient : ProtocolClientBase
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(OpcDaClient));

    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private IOpcDaService? _server;
    private bool _isConnected;

    public override ProtocolType ProtocolType => ProtocolType.OpcDa;
    public override bool IsConnected => _isConnected;

    public OpcDaClient(DeviceConfig config) : base(config)
    {
        if (config.OpcDaConfig == null)
        {
            throw new ArgumentException("OPC DA configuration is required", nameof(config));
        }
    }

    public override async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
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
                var factory = new OpcDaFactory();

                if (!string.IsNullOrEmpty(config.RemoteHost))
                {
                    factory = factory.WithIp(config.RemoteHost);
                }

                _server = await factory.WithServerName(config.ServerProgId)
                    .BuildAsync(cancellationToken)
                    .ConfigureAwait(false);

                _server.DataChanged += OnDataChanged;

                bool connected = await _server.ConnectAsync(cancellationToken).ConfigureAwait(false);

                if (connected)
                {
                    _isConnected = true;
                    Logger.Info($"OPC DA client connected successfully: {ConnectionId}");
                    OnConnectionStatusChanged(true, "Connected");
                    UpdateHeartbeatStatus(true);

                    StartPolling();
                    StartHeartbeat();
                    return true;
                }
                else
                {
                    Logger.Error("OPC DA server connection returned false");
                    OnConnectionStatusChanged(false, "Connection failed", "Server returned false");
                    Cleanup();
                    return false;
                }
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

    private void OnDataChanged(object? sender, IEnumerable<ItemDataValue> e)
    {
        UpdateHeartbeatStatus(true);

        foreach (var item in e)
        {
            try
            {
                var nodeId = item.ItemName;
                _tagValues[nodeId] = item.Value;
                OnDataValueChanged(nodeId, new DataValue
                {
                    Value = item.Value,
                    Quality = DataQuality.Good,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error processing data changed for item: {ex.Message}");
            }
        }
    }

    public override async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            StopPolling();
            StopHeartbeat();
            await WaitForPollingToStopAsync().ConfigureAwait(false);
            await WaitForHeartbeatToStopAsync().ConfigureAwait(false);

            if (_server != null)
            {
                _server.DataChanged -= OnDataChanged;
                _server.Disconnect();
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
    }

    public override async Task<DataValue> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _server == null)
        {
            Logger.Warn($"OPC DA client not connected, cannot read node: {nodeId}");
            return EAP.Core.DataValue.NotConnected();
        }

        try
        {
            Logger.Debug($"Reading OPC DA node: {nodeId}");

            var groupName = GetGroupName(nodeId);

            var readResults = _server.Read(groupName, nodeId);

            if (readResults != null)
            {
                foreach (var result in readResults)
                {
                    if (result.ItemName.Equals(nodeId, StringComparison.OrdinalIgnoreCase))
                    {
                        return new EAP.Core.DataValue
                        {
                            Value = result.Value,
                            Quality = DataQuality.Good,
                            Timestamp = DateTime.UtcNow
                        };
                    }
                }
            }

            return EAP.Core.DataValue.Bad("No data returned");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error reading OPC DA node {nodeId}: {ex.Message}", ex);
            return EAP.Core.DataValue.Bad(ex.Message);
        }
    }

    public override async Task<bool> WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default)
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

            _server.AddItems(groupName, nodeId);

            var itemValue = new ItemDataValue(nodeId, value);
            _server.Write(groupName, itemValue);

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

    private string GetGroupName(string nodeId)
    {
        return $"EAP_{ConnectionId}_Group";
    }

    public override Task SubscribeNodeAsync(string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default)
    {
        Logger.Info($"OPC DA subscription requested for node: {nodeId} (updateRate: {updateRate})");

        if (_server != null)
        {
            var groupName = GetGroupName(nodeId);
            try
            {
                _server.AddItems(groupName, nodeId);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error adding item to group: {ex.Message}");
            }
        }

        return base.SubscribeNodeAsync(nodeId, updateRate, cancellationToken);
    }

    public override Task UnsubscribeNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        Logger.Info($"OPC DA unsubscription requested for node: {nodeId}");

        if (_server != null)
        {
            var groupName = GetGroupName(nodeId);
            try
            {
                _server.RemoveItems(groupName, nodeId);
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error removing item from group: {ex.Message}");
            }
        }

        return base.UnsubscribeNodeAsync(nodeId, cancellationToken);
    }

    public override void Dispose()
    {
        base.Dispose();
        _connectLock.Dispose();
    }
}