using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EAP.Core.Configuration;
using EAP.Core.Protocol;
using log4net;

namespace EAP.Adapters.Modbus;

public class ModbusClient : IProtocolClient
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(ModbusClient));
    
    private readonly DeviceConfig _deviceConfig;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private object? _modbusClient;
    private TcpClient? _tcpClient;
    private SerialPort? _serialPort;
    private bool _isConnected;
    private bool _heartbeatStatus = false;
    private DateTime _lastHeartbeatTime = DateTime.MinValue;
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(10);
    private readonly ConcurrentDictionary<string, object> _tagValues = new();
    private readonly ConcurrentDictionary<string, int> _subscribedTags = new();
    private Task? _pollingTask;
    private CancellationTokenSource? _pollingCts;

    public EAP.Core.Configuration.ProtocolType ProtocolType => EAP.Core.Configuration.ProtocolType.Modbus;
    public string ConnectionId => _deviceConfig.Id;
    public bool IsConnected => _isConnected;
    public bool HeartbeatStatus => _heartbeatStatus;

    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    public event EventHandler<DataValueChangedEventArgs>? DataValueChanged;
    public event EventHandler<HeartbeatStatusChangedEventArgs>? HeartbeatStatusChanged;

    public ModbusClient(DeviceConfig config)
    {
        _deviceConfig = config ?? throw new ArgumentNullException(nameof(config));
        if (config.ModbusConfig == null)
        {
            throw new ArgumentException("Modbus configuration is required", nameof(config));
        }
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
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
            Logger.Info($"Connecting to Modbus device: {config.Mode} mode, Host: {config.Host}, Port: {config.Port}");

            try
            {
                if (config.Mode == ModbusMode.Tcp)
                {
                    await ConnectTcpAsync(config, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    ConnectSerial(config);
                }

                _isConnected = true;
                Logger.Info($"Modbus client connected successfully: {ConnectionId}");
                OnConnectionStatusChanged(true, "Connected");

                StartPolling();

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to connect to Modbus device: {ex.Message}", ex);
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

    private async Task ConnectTcpAsync(ModbusConfig config, CancellationToken cancellationToken)
    {
        var factoryType = Type.GetType("NModbus.ModbusFactory, NModbus");
        if (factoryType == null)
        {
            throw new InvalidOperationException("NModbus library not found");
        }

        var createMethod = factoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        if (createMethod == null)
        {
            throw new InvalidOperationException("Cannot find ModbusFactory.Create method");
        }

        var factory = createMethod.Invoke(null, null);
        if (factory == null)
        {
            throw new InvalidOperationException("Failed to create ModbusFactory instance");
        }

        var createTcpClientMethod = factory.GetType().GetMethod("CreateTcpClient", new[] { typeof(string), typeof(int) });
        if (createTcpClientMethod == null)
        {
            throw new InvalidOperationException("Cannot find CreateTcpClient method");
        }

        _modbusClient = createTcpClientMethod.Invoke(factory, new object[] { config.Host, config.Port });
        if (_modbusClient == null)
        {
            throw new InvalidOperationException("Failed to create Modbus TCP client");
        }

        var connectAsyncMethod = _modbusClient.GetType().GetMethod("ConnectAsync", new[] { typeof(CancellationToken) });
        if (connectAsyncMethod == null)
        {
            throw new InvalidOperationException("Cannot find ConnectAsync method");
        }

        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            await (Task)connectAsyncMethod.Invoke(_modbusClient, new object[] { cts.Token })!;
        }

        Logger.Info($"Modbus TCP connected to {config.Host}:{config.Port}");
    }

    private void ConnectSerial(ModbusConfig config)
    {
        _serialPort = new SerialPort(config.SerialPort, config.BaudRate)
        {
            DataBits = config.DataBits,
            Parity = GetParity(config.Parity),
            StopBits = GetStopBits(config.StopBits),
            ReadTimeout = config.ReadTimeout,
            WriteTimeout = config.WriteTimeout
        };
        _serialPort.Open();

        var factoryType = Type.GetType("NModbus.ModbusFactory, NModbus");
        if (factoryType == null)
        {
            throw new InvalidOperationException("NModbus library not found");
        }

        var createMethod = factoryType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        if (createMethod == null)
        {
            throw new InvalidOperationException("Cannot find ModbusFactory.Create method");
        }

        var factory = createMethod.Invoke(null, null);
        if (factory == null)
        {
            throw new InvalidOperationException("Failed to create ModbusFactory instance");
        }

        MethodInfo createClientMethod;
        if (config.Mode == ModbusMode.Rtu)
        {
            createClientMethod = factory.GetType().GetMethod("CreateRtuClient", new[] { typeof(System.IO.Stream) }) 
                ?? throw new InvalidOperationException("Cannot find CreateRtuClient method");
        }
        else
        {
            createClientMethod = factory.GetType().GetMethod("CreateAsciiClient", new[] { typeof(System.IO.Stream) }) 
                ?? throw new InvalidOperationException("Cannot find CreateAsciiClient method");
        }

        _modbusClient = createClientMethod.Invoke(factory, new object[] { _serialPort.BaseStream });
        if (_modbusClient == null)
        {
            throw new InvalidOperationException($"Failed to create Modbus {config.Mode} client");
        }

        var connectMethod = _modbusClient.GetType().GetMethod("Connect", Type.EmptyTypes);
        if (connectMethod == null)
        {
            throw new InvalidOperationException("Cannot find Connect method");
        }

        connectMethod.Invoke(_modbusClient, null);

        Logger.Info($"Modbus {config.Mode} connected to {config.SerialPort}");
    }

    private Parity GetParity(string parity)
    {
        return parity switch
        {
            "Odd" => Parity.Odd,
            "Even" => Parity.Even,
            "Mark" => Parity.Mark,
            "Space" => Parity.Space,
            _ => Parity.None
        };
    }

    private StopBits GetStopBits(int stopBits)
    {
        return stopBits switch
        {
            2 => StopBits.Two,
            1 => StopBits.One,
            _ => StopBits.One
        };
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
            if (DateTime.Now - _lastHeartbeatTime > _heartbeatTimeout)
            {
                _heartbeatStatus = false;
                Logger.Warn($"Heartbeat timeout for device {ConnectionId}, disconnecting...");
                _ = DisconnectAsync(CancellationToken.None);
            }
        }
        
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

    private void Cleanup()
    {
        if (_modbusClient != null)
        {
            var disconnectMethod = _modbusClient.GetType().GetMethod("Disconnect", Type.EmptyTypes);
            disconnectMethod?.Invoke(_modbusClient, null);

            if (_modbusClient is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _modbusClient = null;
        
        _tcpClient?.Close();
        _tcpClient?.Dispose();
        _tcpClient = null;
        
        _serialPort?.Close();
        _serialPort?.Dispose();
        _serialPort = null;
        
        _pollingTask = null;
        _pollingCts = null;
    }

    private object? CallModbusMethod(string methodName, params object[] parameters)
    {
        if (_modbusClient == null)
            return null;

        var method = _modbusClient.GetType().GetMethod(methodName, parameters.Select(p => p.GetType()).ToArray());
        if (method != null)
        {
            return method.Invoke(_modbusClient, parameters);
        }

        return null;
    }

    public async Task<DataValue> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _modbusClient == null)
        {
            Logger.Warn($"Modbus client not connected, cannot read node: {nodeId}");
            return EAP.Core.Protocol.DataValue.NotConnected();
        }

        try
        {
            Logger.Debug($"Reading Modbus node: {nodeId}");
            
            var (address, quantity, functionCode) = ParseNodeId(nodeId);
            var config = _deviceConfig.ModbusConfig!;

            return await Task.Run(() =>
            {
                object? result = null;
                
                switch (functionCode)
                {
                    case 1:
                        result = CallModbusMethod("ReadCoils", config.SlaveId, address, quantity);
                        break;
                    case 2:
                        result = CallModbusMethod("ReadDiscreteInputs", config.SlaveId, address, quantity);
                        break;
                    case 3:
                        result = CallModbusMethod("ReadHoldingRegisters", config.SlaveId, address, quantity);
                        break;
                    case 4:
                        result = CallModbusMethod("ReadInputRegisters", config.SlaveId, address, quantity);
                        break;
                }
                
                if (result != null)
                {
                    return CreateDataValue(result);
                }
                
                return EAP.Core.Protocol.DataValue.Bad($"Unsupported function code: {functionCode}");
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error reading Modbus node {nodeId}: {ex.Message}", ex);
            return EAP.Core.Protocol.DataValue.Bad(ex.Message);
        }
    }

    public async Task<bool> WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _modbusClient == null)
        {
            Logger.Warn($"Modbus client not connected, cannot write node: {nodeId}");
            return false;
        }

        try
        {
            Logger.Debug($"Writing Modbus node {nodeId} with value: {value}");
            
            var (address, _, functionCode) = ParseNodeId(nodeId);
            var config = _deviceConfig.ModbusConfig!;

            await Task.Run(() =>
            {
                switch (functionCode)
                {
                    case 5:
                        bool coilValue = Convert.ToBoolean(value);
                        CallModbusMethod("WriteSingleCoil", config.SlaveId, address, coilValue);
                        break;
                    case 6:
                        ushort regValue = Convert.ToUInt16(value);
                        CallModbusMethod("WriteSingleRegister", config.SlaveId, address, regValue);
                        break;
                    case 15:
                        if (value is bool[] coils)
                        {
                            CallModbusMethod("WriteMultipleCoils", config.SlaveId, address, coils);
                        }
                        else
                        {
                            throw new ArgumentException("Value must be bool[] for multiple coils");
                        }
                        break;
                    case 16:
                        if (value is ushort[] registers)
                        {
                            CallModbusMethod("WriteMultipleRegisters", config.SlaveId, address, registers);
                        }
                        else
                        {
                            throw new ArgumentException("Value must be ushort[] for multiple registers");
                        }
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported function code for write: {functionCode}");
                }
            }, cancellationToken).ConfigureAwait(false);

            _tagValues[nodeId] = value;
            Logger.Info($"Successfully wrote to Modbus node {nodeId}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error writing Modbus node {nodeId}: {ex.Message}", ex);
            return false;
        }
    }

    private (ushort Address, ushort Quantity, byte FunctionCode) ParseNodeId(string nodeId)
    {
        try
        {
            var parts = nodeId.Split(':');
            if (parts.Length != 2)
                throw new FormatException("Invalid nodeId format. Expected FCxx:address");

            var fcPart = parts[0].Trim().ToUpper();
            var addrPart = parts[1].Trim();

            if (!fcPart.StartsWith("FC"))
                throw new FormatException("NodeId must start with FC");

            if (!int.TryParse(fcPart.Substring(2), out int functionCode))
                throw new FormatException("Invalid function code");

            ushort quantity = 1;
            ushort address;

            var addrParts = addrPart.Split(',');
            if (addrParts.Length == 2)
            {
                address = Convert.ToUInt16(addrParts[0].Trim());
                quantity = Convert.ToUInt16(addrParts[1].Trim());
            }
            else
            {
                address = Convert.ToUInt16(addrPart);
            }

            return (address, quantity, (byte)functionCode);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error parsing nodeId {nodeId}: {ex.Message}");
            throw;
        }
    }

    private EAP.Core.Protocol.DataValue CreateDataValue(object result)
    {
        if (result is bool[] boolArray)
        {
            if (boolArray.Length == 1)
            {
                return EAP.Core.Protocol.DataValue.Good(boolArray[0]);
            }
            return EAP.Core.Protocol.DataValue.Good(boolArray);
        }
        
        if (result is ushort[] ushortArray)
        {
            if (ushortArray.Length == 1)
            {
                return EAP.Core.Protocol.DataValue.Good(ushortArray[0]);
            }
            return EAP.Core.Protocol.DataValue.Good(ushortArray);
        }
        
        return EAP.Core.Protocol.DataValue.Good(result);
    }

    public Task SubscribeNodeAsync(string nodeId, int updateRate, CancellationToken cancellationToken = default)
    {
        Logger.Debug($"Subscribing to Modbus node: {nodeId} with update rate: {updateRate}ms");
        _subscribedTags[nodeId] = updateRate;
        return Task.CompletedTask;
    }

    public Task UnsubscribeNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        Logger.Debug($"Unsubscribing from Modbus node: {nodeId}");
        _subscribedTags.TryRemove(nodeId, out _);
        return Task.CompletedTask;
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
    }
}