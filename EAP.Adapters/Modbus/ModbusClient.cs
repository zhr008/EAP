using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EAP.Core;
using log4net;
using Microsoft.Extensions.Logging;
using NModbus;
using NModbus.Interfaces;
using NModbus.Transport.IP;
using NModbus.Transport.IP.ConnectionStrategies;

namespace EAP.Adapters.Modbus;

public class ModbusClient : ProtocolClientBase
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(ModbusClient));
    private static readonly ILoggerFactory NullLoggerFactory = new LoggerFactory();

    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private IModbusClient? _client;
    private IModbusClientTransport? _transport;
    private TcpClient? _tcpClient;
    private bool _isConnected;

    public override EAP.Core.ProtocolType ProtocolType => EAP.Core.ProtocolType.Modbus;
    public override bool IsConnected => _isConnected;

    public ModbusClient(DeviceConfig config) : base(config)
    {
        if (config.ModbusConfig == null)
        {
            throw new ArgumentException("Modbus configuration is required", nameof(config));
        }
    }

    public override async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected)
            {
                Logger.Warn($"Modbus client already connected: {ConnectionId}");
                return true;
            }

            var config = _deviceConfig.ModbusConfig!;
            Logger.Info($"Connecting to Modbus server: {config.Host}:{config.Port}, Slave ID: {config.SlaveId}");

            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(config.Host, config.Port, cancellationToken).ConfigureAwait(false);

                var stream = new TcpClientModbusStream(_tcpClient);
                var streamFactory = new SingletonStreamFactory(stream);
                var connectionStrategy = new SingletonStreamConnectionStrategy(streamFactory, NullLoggerFactory);
                _transport = new ModbusIPClientTransport(connectionStrategy, NullLoggerFactory);
                _client = new global::NModbus.ModbusClient(_transport, NullLoggerFactory, null!);

                _isConnected = true;
                Logger.Info($"Modbus client connected successfully: {ConnectionId}");
                OnConnectionStatusChanged(true, "Connected");
                UpdateHeartbeatStatus(true);

                StartPolling();
                return true;
            }
            catch (Exception ex)
            {
                if (_isConnected)
                {
                    _isConnected = false;
                    Logger.Error($"Failed to connect to Modbus server: {ex.Message}", ex);
                    OnConnectionStatusChanged(false, "Connection failed", ex.Message);
                }
                else
                {
                    Logger.Error($"Failed to connect to Modbus server (already disconnected): {ex.Message}", ex);
                }
                CleanupAsync().Wait();
                return false;
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public override async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            StopPolling();
            await WaitForPollingToStopAsync().ConfigureAwait(false);

            await CleanupAsync().ConfigureAwait(false);
            _isConnected = false;
            Logger.Info($"Modbus client disconnected: {ConnectionId}");
            OnConnectionStatusChanged(false, "Disconnected");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error disconnecting Modbus client: {ex.Message}", ex);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task CleanupAsync()
    {
        if (_client is IAsyncDisposable asyncClient)
            await asyncClient.DisposeAsync().ConfigureAwait(false);
        else
            (_client as IDisposable)?.Dispose();

        if (_transport is IAsyncDisposable asyncTransport)
            await asyncTransport.DisposeAsync().ConfigureAwait(false);
        else
            (_transport as IDisposable)?.Dispose();

        _tcpClient?.Dispose();
        _client = null;
        _transport = null;
        _tcpClient = null;
    }

    public override async Task<DataValue> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _client == null)
        {
            Logger.Warn($"Modbus client not connected, cannot read node: {nodeId}");
            return EAP.Core.DataValue.NotConnected();
        }

        try
        {
            Logger.Debug($"Reading Modbus node: {nodeId}");

            var config = _deviceConfig.ModbusConfig!;
            var parts = nodeId.Split(':');
            if (parts.Length >= 2)
            {
                var registerType = ParseRegisterType(parts[0]);
                if (!ushort.TryParse(parts[1], out ushort address))
                {
                    return EAP.Core.DataValue.Bad("Invalid address");
                }

                ushort count = parts.Length > 2 ? ushort.Parse(parts[2]) : (ushort)1;
                var slaveAddress = config.SlaveId;

                switch (registerType)
                {
                    case ModbusRegisterType.Coil:
                        var coils = await IModbusClientExtensions.ReadCoilsAsync(_client, slaveAddress, address, count, cancellationToken).ConfigureAwait(false);
                        return CreateDataValue(coils);
                    case ModbusRegisterType.DiscreteInput:
                        var inputs = await IModbusClientExtensions.ReadDiscreteInputsAsync(_client, slaveAddress, address, count, cancellationToken).ConfigureAwait(false);
                        return CreateDataValue(inputs);
                    case ModbusRegisterType.InputRegister:
                        var inputRegs = await IModbusClientExtensions.ReadInputRegistersAsync(_client, slaveAddress, address, count, cancellationToken).ConfigureAwait(false);
                        return CreateDataValue(inputRegs);
                    case ModbusRegisterType.HoldingRegister:
                        var holdingRegs = await IModbusClientExtensions.ReadHoldingRegistersAsync(_client, slaveAddress, address, count, cancellationToken).ConfigureAwait(false);
                        return CreateDataValue(holdingRegs);
                    default:
                        return EAP.Core.DataValue.Bad("Unknown register type");
                }
            }

            return EAP.Core.DataValue.Bad("Invalid node ID format");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error reading Modbus node {nodeId}: {ex.Message}", ex);
            
            // 检查是否是连接断开异常
            if (IsConnectionLostException(ex))
            {
                await HandleConnectionLostAsync().ConfigureAwait(false);
            }
            
            return EAP.Core.DataValue.Bad(ex.Message);
        }
    }

    private bool IsConnectionLostException(Exception ex)
    {
        // 检查是否是连接断开相关的异常
        return ex is IOException || 
               ex is SocketException ||
               (ex.InnerException != null && IsConnectionLostException(ex.InnerException));
    }

    private async Task HandleConnectionLostAsync()
    {
        if (_isConnected)
        {
            _isConnected = false;
            Logger.Error($"Modbus connection lost: {ConnectionId}");
            OnConnectionStatusChanged(false, "Connection lost", "Server disconnected");
            StopPolling();
            await WaitForPollingToStopAsync().ConfigureAwait(false);
            await CleanupAsync().ConfigureAwait(false);
        }
    }

    public override async Task<bool> WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _client == null)
        {
            Logger.Warn($"Modbus client not connected, cannot write node: {nodeId}");
            return false;
        }

        try
        {
            Logger.Debug($"Writing Modbus node {nodeId} with value: {value}");

            var config = _deviceConfig.ModbusConfig!;
            var parts = nodeId.Split(':');
            if (parts.Length >= 2)
            {
                var registerType = ParseRegisterType(parts[0]);
                if (!ushort.TryParse(parts[1], out ushort address))
                {
                    Logger.Error("Invalid address");
                    return false;
                }

                var slaveAddress = config.SlaveId;

                switch (registerType)
                {
                    case ModbusRegisterType.Coil:
                        bool coilValue = bool.Parse(value.ToString()!);
                        await IModbusClientExtensions.WriteSingleCoilAsync(_client, slaveAddress, address, coilValue, cancellationToken).ConfigureAwait(false);
                        break;
                    case ModbusRegisterType.HoldingRegister:
                        ushort regValue = ushort.Parse(value.ToString()!);
                        await IModbusClientExtensions.WriteSingleRegisterAsync(_client, slaveAddress, address, regValue, cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        Logger.Error("Cannot write to this register type");
                        return false;
                }

                _tagValues[nodeId] = value;
                Logger.Info($"Successfully wrote to Modbus node {nodeId}");
                return true;
            }

            Logger.Error($"Invalid node ID format: {nodeId}");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error writing Modbus node {nodeId}: {ex.Message}", ex);
            return false;
        }
    }

    private static ModbusRegisterType ParseRegisterType(string typeStr)
    {
        return typeStr.ToLower() switch
        {
            "coil" => ModbusRegisterType.Coil,
            "di" => ModbusRegisterType.DiscreteInput,
            "ir" => ModbusRegisterType.InputRegister,
            "hr" => ModbusRegisterType.HoldingRegister,
            "fc01" => ModbusRegisterType.Coil,
            "fc02" => ModbusRegisterType.DiscreteInput,
            "fc03" => ModbusRegisterType.HoldingRegister,
            "fc04" => ModbusRegisterType.InputRegister,
            _ => ModbusRegisterType.HoldingRegister
        };
    }

    private static DataValue CreateDataValue(bool[] values)
    {
        return new EAP.Core.DataValue
        {
            Value = values.Length == 1 ? values[0] : values,
            Quality = DataQuality.Good,
            Timestamp = DateTime.UtcNow
        };
    }

    private static DataValue CreateDataValue(ushort[] values)
    {
        return new EAP.Core.DataValue
        {
            Value = values.Length == 1 ? values[0] : values,
            Quality = DataQuality.Good,
            Timestamp = DateTime.UtcNow
        };
    }

    public override Task SubscribeNodeAsync(string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default)
    {
        Logger.Info($"Modbus subscription requested for node: {nodeId} (updateRate: {updateRate})");
        return base.SubscribeNodeAsync(nodeId, updateRate, cancellationToken);
    }

    public override Task UnsubscribeNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        Logger.Info($"Modbus unsubscription requested for node: {nodeId}");
        return base.UnsubscribeNodeAsync(nodeId, cancellationToken);
    }

    public override void Dispose()
    {
        base.Dispose();
        _connectLock.Dispose();
        CleanupAsync().Wait();
    }

    private enum ModbusRegisterType
    {
        Coil,
        DiscreteInput,
        InputRegister,
        HoldingRegister
    }

    private class TcpClientModbusStream : IModbusStream
    {
        private readonly TcpClient _tcpClient;

        public TcpClientModbusStream(TcpClient tcpClient)
        {
            _tcpClient = tcpClient;
        }

        public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _tcpClient.GetStream().ReadAsync(buffer, offset, count, cancellationToken);
        }

        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _tcpClient.GetStream().WriteAsync(buffer, offset, count, cancellationToken);
        }

        public void Dispose()
        {
            _tcpClient.GetStream().Dispose();
        }

        public ValueTask DisposeAsync()
        {
            _tcpClient.GetStream().Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private class SingletonStreamFactory : IStreamFactory
    {
        private readonly IModbusStream _stream;
        private bool _used;

        public SingletonStreamFactory(IModbusStream stream)
        {
            _stream = stream;
        }

        public Task<IModbusStream> CreateStreamAsync(CancellationToken cancellationToken)
        {
            if (_used)
                throw new InvalidOperationException("Stream already created");
            _used = true;
            return Task.FromResult(_stream);
        }

        public Task<IModbusStream> CreateAndConnectAsync(CancellationToken cancellationToken)
        {
            return CreateStreamAsync(cancellationToken);
        }
    }
}
