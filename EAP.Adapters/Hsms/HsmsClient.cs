using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EAP.Core.Configuration;
using EAP.Core.Protocol;
using log4net;
using Secs4Net;

namespace EAP.Adapters.Hsms;

public class HsmsClient : IProtocolClient
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(HsmsClient));
    
    private readonly DeviceConfig _deviceConfig;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private object? _secsGem;
    private bool _isConnected;
    private bool _heartbeatStatus = false;
    private DateTime _lastHeartbeatTime = DateTime.MinValue;
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(10);
    private readonly ConcurrentDictionary<string, object> _tagValues = new();
    private readonly ConcurrentDictionary<string, int> _subscribedTags = new();
    private Task? _pollingTask;
    private CancellationTokenSource? _pollingCts;
    private Task? _messageListeningTask;
    private CancellationTokenSource? _messageListeningCts;

    public EAP.Core.Configuration.ProtocolType ProtocolType => EAP.Core.Configuration.ProtocolType.Hsms;
    public string ConnectionId => _deviceConfig.Id;
    public bool IsConnected => _isConnected;
    public bool HeartbeatStatus => _heartbeatStatus;

    public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;
    public event EventHandler<DataValueChangedEventArgs>? DataValueChanged;
    public event EventHandler<HeartbeatStatusChangedEventArgs>? HeartbeatStatusChanged;

    public HsmsClient(DeviceConfig config)
    {
        _deviceConfig = config ?? throw new ArgumentNullException(nameof(config));
        if (config.HsmsConfig == null)
        {
            throw new ArgumentException("HSMS configuration is required", nameof(config));
        }
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected)
            {
                Logger.Warn($"HSMS client already connected: {ConnectionId}");
                return true;
            }

            var config = _deviceConfig.HsmsConfig!;
            Logger.Info($"Connecting to HSMS device: {config.Host}:{config.Port}, Mode: {config.ConnectionMode}");

            try
            {
                _secsGem = CreateSecsGemConnection(config, cancellationToken);

                // 启动消息监听和轮询（连接状态将由消息监听成功后设置）
                StartPolling();
                StartMessageListening();

                // 等待连接状态确认
                var connectTask = WaitForConnectionAsync(cancellationToken);
                var timeoutTask = Task.Delay(10000, cancellationToken); // 10秒超时
                
                var completedTask = await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false);
                
                if (completedTask == timeoutTask)
                {
                    Logger.Error("HSMS connection timeout");
                    OnConnectionStatusChanged(false, "Connection timeout");
                    Cleanup();
                    return false;
                }
                
                return IsConnected;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to connect to HSMS device: {ex.Message}", ex);
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
    
    private async Task WaitForConnectionAsync(CancellationToken cancellationToken)
    {
        // 等待连接状态变为已连接，或超时
        while (!cancellationToken.IsCancellationRequested && !IsConnected)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    private void StartMessageListening()
    {
        _messageListeningCts = new CancellationTokenSource();
        _messageListeningTask = Task.Run(async () =>
        {
            if (_secsGem == null) return;
            
            try
            {
                var secsGemType = _secsGem.GetType();
                var getPrimaryMessageMethod = secsGemType.GetMethod("GetPrimaryMessageAsync", new[] { typeof(CancellationToken) });
                
                if (getPrimaryMessageMethod == null)
                {
                    Logger.Warn("GetPrimaryMessageAsync method not found on SecsGem");
                    // 设置连接状态为失败
                    if (!_isConnected)
                    {
                        OnConnectionStatusChanged(false, "GetPrimaryMessageAsync method not found");
                    }
                    return;
                }
                
                var asyncEnumerable = getPrimaryMessageMethod.Invoke(_secsGem, new object[] { _messageListeningCts.Token });
                if (asyncEnumerable == null) 
                {
                    Logger.Warn("GetPrimaryMessageAsync returned null");
                    // 设置连接状态为失败
                    if (!_isConnected)
                    {
                        OnConnectionStatusChanged(false, "GetPrimaryMessageAsync returned null");
                    }
                    return;
                }
                
                var getEnumeratorMethod = asyncEnumerable.GetType().GetMethod("GetAsyncEnumerator");
                if (getEnumeratorMethod == null) 
                {
                    Logger.Warn("GetAsyncEnumerator method not found");
                    // 设置连接状态为失败
                    if (!_isConnected)
                    {
                        OnConnectionStatusChanged(false, "GetAsyncEnumerator method not found");
                    }
                    return;
                }
                
                var enumerator = getEnumeratorMethod.Invoke(asyncEnumerable, null);
                if (enumerator == null) 
                {
                    Logger.Warn("GetAsyncEnumerator returned null");
                    // 设置连接状态为失败
                    if (!_isConnected)
                    {
                        OnConnectionStatusChanged(false, "GetAsyncEnumerator returned null");
                    }
                    return;
                }
                
                // 消息监听成功启动，设置连接状态为已连接
                if (!_isConnected)
                {
                    _isConnected = true;
                    Logger.Info($"HSMS client connected successfully: {ConnectionId}");
                    OnConnectionStatusChanged(true, "Connected");
                }
                
                var moveNextMethod = enumerator.GetType().GetMethod("MoveNextAsync");
                var currentProperty = enumerator.GetType().GetProperty("Current");
                
                while (!_messageListeningCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var moveNextTask = (Task<bool>?)moveNextMethod?.Invoke(enumerator, null);
                        if (moveNextTask == null) break;
                        
                        var hasNext = await moveNextTask.ConfigureAwait(false);
                        if (!hasNext) break;
                        
                        var messageWrapper = currentProperty?.GetValue(enumerator);
                        if (messageWrapper != null)
                        {
                            ProcessHsmsMessage(messageWrapper);
                            // 收到消息表示心跳正常
                            UpdateHeartbeatStatus(true);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error processing HSMS message: {ex.Message}", ex);
                        UpdateHeartbeatStatus(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Debug("HSMS message listening canceled");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HSMS message listening loop: {ex.Message}", ex);
            }
        });
    }

    private void ProcessHsmsMessage(dynamic messageWrapper)
    {
        try
        {
            var messageType = messageWrapper.GetType();
            var messageProp = messageType.GetProperty("Message");
            if (messageProp == null)
            {
                Logger.Debug("HSMS message wrapper has no Message property");
                return;
            }
            
            var message = messageProp.GetValue(messageWrapper);
            if (message == null) return;
            
            var messageValueType = message.GetType();
            var sProp = messageValueType.GetProperty("S");
            var fProp = messageValueType.GetProperty("F");
            var secsItemProp = messageValueType.GetProperty("SecsItem");
            
            if (sProp == null || fProp == null)
            {
                Logger.Debug("HSMS message has no S or F property");
                return;
            }
            
            int s = (int)sProp.GetValue(message);
            int f = (int)fProp.GetValue(message);
            
            Logger.Debug($"Received HSMS message: S{s}F{f}");
            
            var secsItem = secsItemProp?.GetValue(message);
            if (secsItem != null)
            {
                var nodeId = $"S{s}F{f}";
                var value = ConvertSecsItemToValue(secsItem);
                
                _tagValues[nodeId] = value;
                DataValueChanged?.Invoke(this, new DataValueChangedEventArgs
                {
                    ConnectionId = ConnectionId,
                    NodeId = nodeId,
                    Value = new DataValue
                    {
                        Value = value,
                        Quality = DataQuality.Good,
                        Timestamp = DateTime.UtcNow
                    }
                });
            }
            
            var replyMethod = messageWrapper.GetType().GetMethod("TryReplyAsync");
            if (replyMethod != null)
            {
                replyMethod.Invoke(messageWrapper, new object[] { null, CancellationToken.None });
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error processing HSMS message: {ex.Message}", ex);
        }
    }

    private object ConvertSecsItemToValue(dynamic item)
    {
        try
        {
            var itemType = item.GetType();
            var formatProp = itemType.GetProperty("Format");
            if (formatProp == null)
            {
                return item.ToString();
            }
            
            var format = formatProp.GetValue(item)?.ToString() ?? string.Empty;
            
            return format switch
            {
                "List" => GetItemItems(item),
                "Boolean" => GetItemMemory<bool>(item),
                "Int8" => GetItemMemory<sbyte>(item),
                "Int16" => GetItemMemory<short>(item),
                "Int32" => GetItemMemory<int>(item),
                "Int64" => GetItemMemory<long>(item),
                "UInt8" => GetItemMemory<byte>(item),
                "UInt16" => GetItemMemory<ushort>(item),
                "UInt32" => GetItemMemory<uint>(item),
                "UInt64" => GetItemMemory<ulong>(item),
                "Float4" => GetItemMemory<float>(item),
                "Float8" => GetItemMemory<double>(item),
                "ASCII" => GetItemString(item),
                "JIS8" => GetItemString(item),
                _ => item.ToString()
            };
        }
        catch
        {
            return item.ToString();
        }
    }

    private Array? GetItemItems(dynamic item)
    {
        try
        {
            var itemsProp = item.GetType().GetProperty("Items");
            return itemsProp?.GetValue(item) as Array;
        }
        catch
        {
            return null;
        }
    }

    private Array? GetItemMemory<T>(dynamic item)
    {
        try
        {
            var method = item.GetType().GetMethod("GetMemory", new Type[0]);
            if (method != null)
            {
                var memory = method.Invoke(item, null);
                var toArrayMethod = memory?.GetType().GetMethod("ToArray");
                return toArrayMethod?.Invoke(memory, null) as Array;
            }
        }
        catch
        {
            // Ignore
        }
        return null;
    }

    private string? GetItemString(dynamic item)
    {
        try
        {
            var method = item.GetType().GetMethod("GetString", new Type[0]);
            return method?.Invoke(item, null) as string;
        }
        catch
        {
            return null;
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
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug($"Error polling tag {nodeId}: {ex.Message}");
                        }
                        
                        await Task.Delay(updateRate, _pollingCts.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error in polling loop: {ex.Message}", ex);
                }
            }
        });
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

            _messageListeningCts?.Cancel();
            if (_messageListeningTask != null)
            {
                await _messageListeningTask.ConfigureAwait(false);
            }

            Cleanup();
            _isConnected = false;
            Logger.Info($"HSMS client disconnected: {ConnectionId}");
            OnConnectionStatusChanged(false, "Disconnected");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error disconnecting HSMS client: {ex.Message}", ex);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private object? CreateSecsGemConnection(HsmsConfig config, CancellationToken cancellationToken)
        {
            try
            {
                var secsGemType = Type.GetType("Secs4Net.SecsGem, Secs4Net");
                if (secsGemType == null)
                {
                    Logger.Error("SecsGem type not found in Secs4Net assembly");
                    throw new InvalidOperationException("SecsGem type not found");
                }

                var optionsType = Type.GetType("Secs4Net.SecsGemOptions, Secs4Net");
                if (optionsType == null)
                {
                    Logger.Error("SecsGemOptions type not found in Secs4Net assembly");
                    throw new InvalidOperationException("SecsGemOptions type not found");
                }

                // 创建 SecsGemOptions 实例
                var options = Activator.CreateInstance(optionsType);
                if (options == null)
                {
                    Logger.Error("Failed to create SecsGemOptions instance");
                    throw new InvalidOperationException("Failed to create SecsGemOptions instance");
                }

                SetPropertyValue(options, "IpAddress", config.Host);
                SetPropertyValue(options, "Port", (ushort)config.Port);
                SetPropertyValue(options, "IsActive", config.ConnectionMode == HsmsConnectionMode.Active);
                SetPropertyValue(options, "T3", TimeSpan.FromMilliseconds(config.T3Timeout));
                SetPropertyValue(options, "T5", TimeSpan.FromMilliseconds(config.T5Timeout));
                SetPropertyValue(options, "T6", TimeSpan.FromMilliseconds(config.T6Timeout));
                SetPropertyValue(options, "T7", TimeSpan.FromMilliseconds(config.T7Timeout));
                SetPropertyValue(options, "LinkTestInterval", TimeSpan.FromMilliseconds(config.LinkTestInterval));
                SetPropertyValue(options, "DeviceId", config.DeviceId);

                // 创建 IOptions<SecsGemOptions> 包装器
                var optionsWrapper = CreateOptionsWrapper(options, optionsType);
                if (optionsWrapper == null)
                {
                    Logger.Error("Failed to create IOptions<SecsGemOptions> wrapper");
                    throw new InvalidOperationException("Failed to create IOptions wrapper");
                }

                // 创建 ISecsGemLogger 适配器
                var logger = CreateSecsGemLogger();
                if (logger == null)
                {
                    Logger.Error("Failed to create ISecsGemLogger");
                    throw new InvalidOperationException("Failed to create ISecsGemLogger");
                }

                // 创建 ISecsConnection (HSMS 连接器)
                var connection = CreateHsmsConnection(config, optionsType, optionsWrapper, logger);
                if (connection == null)
                {
                    Logger.Error("Failed to create ISecsConnection");
                    throw new InvalidOperationException("Failed to create ISecsConnection");
                }

                // 使用构造函数创建 SecsGem 实例
                var constructor = secsGemType.GetConstructor(new[] 
                { 
                    typeof(Microsoft.Extensions.Options.IOptions<>).MakeGenericType(optionsType),
                    Type.GetType("Secs4Net.ISecsConnection, Secs4Net"),
                    Type.GetType("Secs4Net.ISecsGemLogger, Secs4Net")
                });

                if (constructor != null)
                {
                    Logger.Info("Creating SecsGem using constructor with IOptions, ISecsConnection, ISecsGemLogger");
                    return constructor.Invoke(new[] { optionsWrapper, connection, logger });
                }

                // 尝试查找任何接受这三个参数的构造函数（可能是具体类型而非接口）
                var allConstructors = secsGemType.GetConstructors();
                foreach (var ctor in allConstructors)
                {
                    var parameters = ctor.GetParameters();
                    if (parameters.Length == 3)
                    {
                        try
                        {
                            Logger.Info($"Trying constructor with 3 parameters: {parameters[0].ParameterType.Name}, {parameters[1].ParameterType.Name}, {parameters[2].ParameterType.Name}");
                            return ctor.Invoke(new[] { optionsWrapper, connection, logger });
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"Constructor invocation failed: {ex.Message}");
                        }
                    }
                }

                Logger.Error("No suitable constructor found for SecsGem");
                throw new InvalidOperationException("No suitable constructor found for SecsGem");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating SecsGem connection: {ex.Message}", ex);
                throw;
            }
        }

        private object? CreateOptionsWrapper(object options, Type optionsType)
        {
            try
            {
                // 使用 Microsoft.Extensions.Options.Options.Create<T> 方法
                var optionsTypeGeneric = typeof(Microsoft.Extensions.Options.Options);
                var createMethod = optionsTypeGeneric.GetMethod("Create", new[] { optionsType });
                if (createMethod != null)
                {
                    return createMethod.Invoke(null, new[] { options });
                }

                // 如果找不到静态方法，使用泛型包装类创建
                Logger.Warn("Options.Create not found, using generic wrapper class");
                
                // 创建泛型包装类实例
                var wrapperType = typeof(OptionsWrapper<>).MakeGenericType(optionsType);
                var wrapper = Activator.CreateInstance(wrapperType);
                
                // 设置 Value 属性
                var valueProperty = wrapperType.GetProperty("Value");
                if (valueProperty != null)
                {
                    valueProperty.SetValue(wrapper, options);
                }
                
                return wrapper;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating IOptions wrapper: {ex.Message}", ex);
                return null;
            }
        }
        
        // IOptions<T> 的简单实现
        private class OptionsWrapper<T> : Microsoft.Extensions.Options.IOptions<T> where T : class
        {
            public T Value { get; set; } = default!;
        }

        private object? CreateHsmsConnection(HsmsConfig config, Type optionsType, object optionsWrapper, object logger)
        {
            try
            {
                // 尝试查找 HSMS 连接器类型
                var connectionType = Type.GetType("Secs4Net.HsmsTcpConnection, Secs4Net");
                if (connectionType == null)
                {
                    connectionType = Type.GetType("Secs4Net.SecsGemConnection, Secs4Net");
                }
                if (connectionType == null)
                {
                    // 查找所有实现 ISecsConnection 的类型
                    var iSecsConnectionType = Type.GetType("Secs4Net.ISecsConnection, Secs4Net");
                    if (iSecsConnectionType != null)
                    {
                        connectionType = AppDomain.CurrentDomain.GetAssemblies()
                            .SelectMany(a => a.GetTypes())
                            .FirstOrDefault(t => iSecsConnectionType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                    }
                }

                if (connectionType != null)
                {
                    Logger.Info($"Found HSMS connection type: {connectionType.FullName}");
                    
                    // 尝试多种构造函数
                    var constructors = connectionType.GetConstructors();
                    Logger.Info($"Found {constructors.Length} constructors for {connectionType.FullName}");
                    
                    foreach (var ctor in constructors)
                    {
                        try
                        {
                            var parameters = ctor.GetParameters();
                            var paramTypes = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                            Logger.Info($"Trying connection constructor: ({paramTypes})");
                            
                            if (parameters.Length == 0)
                            {
                                return ctor.Invoke(null);
                            }
                            else if (parameters.Length == 1)
                            {
                                // 尝试 SecsGemOptions 或字符串参数
                                if (parameters[0].ParameterType == optionsType)
                                {
                                    var options = Activator.CreateInstance(optionsType);
                                    SetPropertyValue(options, "IpAddress", config.Host);
                                    SetPropertyValue(options, "Port", (ushort)config.Port);
                                    SetPropertyValue(options, "IsActive", config.ConnectionMode == HsmsConnectionMode.Active);
                                    return ctor.Invoke(new[] { options });
                                }
                                else if (parameters[0].ParameterType == typeof(string))
                                {
                                    // 可能是连接字符串
                                    return ctor.Invoke(new object[] { config.Host });
                                }
                                // 尝试 IOptions<SecsGemOptions>
                                var iOptionsType = typeof(Microsoft.Extensions.Options.IOptions<>).MakeGenericType(optionsType);
                                if (parameters[0].ParameterType == iOptionsType)
                                {
                                    return ctor.Invoke(new[] { optionsWrapper });
                                }
                            }
                            else if (parameters.Length == 2)
                            {
                                // 可能是 (string host, int port)
                                if (parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(int))
                                {
                                    return ctor.Invoke(new object[] { config.Host, config.Port });
                                }
                                // 可能是 (IPAddress, int)
                                else if (parameters[0].ParameterType == typeof(System.Net.IPAddress) && parameters[1].ParameterType == typeof(int))
                                {
                                    return ctor.Invoke(new object[] { System.Net.IPAddress.Parse(config.Host), config.Port });
                                }
                                // 可能是 (string, ushort)
                                else if (parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(ushort))
                                {
                                    return ctor.Invoke(new object[] { config.Host, (ushort)config.Port });
                                }
                                // 可能是 (IOptions<SecsGemOptions>, ISecsGemLogger)
                                var iOptionsType = typeof(Microsoft.Extensions.Options.IOptions<>).MakeGenericType(optionsType);
                                var iSecsGemLoggerType = Type.GetType("Secs4Net.ISecsGemLogger, Secs4Net");
                                if (parameters[0].ParameterType == iOptionsType && iSecsGemLoggerType != null && parameters[1].ParameterType == iSecsGemLoggerType)
                                {
                                    Logger.Info("Creating HsmsConnection with IOptions<SecsGemOptions> and ISecsGemLogger");
                                    try
                                    {
                                        var result = ctor.Invoke(new[] { optionsWrapper, logger });
                                        Logger.Info("HsmsConnection created successfully");
                                        return result;
                                    }
                                    catch (System.Reflection.TargetInvocationException tie)
                                    {
                                        Logger.Error($"HsmsConnection constructor threw exception: {tie.InnerException?.Message ?? tie.Message}", tie.InnerException ?? tie);
                                        throw;
                                    }
                                }
                            }
                            else if (parameters.Length == 3)
                            {
                                // 可能是 (string host, int port, bool isActive)
                                if (parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(int) && parameters[2].ParameterType == typeof(bool))
                                {
                                    return ctor.Invoke(new object[] { config.Host, config.Port, config.ConnectionMode == HsmsConnectionMode.Active });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"Connection constructor failed: {ex.Message}");
                        }
                    }
                }

                Logger.Error("HSMS connection type not found or could not be instantiated");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating HSMS connection: {ex.Message}", ex);
                return null;
            }
        }

        private object? CreateSecsGemLogger()
        {
            try
            {
                var loggerType = Type.GetType("Secs4Net.ISecsGemLogger, Secs4Net");
                if (loggerType == null)
                {
                    Logger.Warn("ISecsGemLogger type not found, trying alternative logger type");
                    loggerType = Type.GetType("Secs4Net.ILogger, Secs4Net");
                }

                if (loggerType == null)
                {
                    Logger.Error("No logger interface found in Secs4Net");
                    return null;
                }

                // 创建一个实现 ISecsGemLogger 的动态代理
                return CreateLoggerProxy(loggerType);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating SecsGem logger: {ex.Message}", ex);
                return null;
            }
        }

        private object? CreateLoggerProxy(Type loggerInterfaceType)
        {
            try
            {
                // 使用 DispatchProxy 创建动态代理 - 需要传递类型而非实例
                var proxyType = typeof(SecsGemLoggerProxy<>).MakeGenericType(loggerInterfaceType);
                var proxy = System.Reflection.DispatchProxy.Create(loggerInterfaceType, proxyType);
                
                // 设置内部 logger
                var loggerField = proxyType.GetField("_logger", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (loggerField != null)
                {
                    loggerField.SetValue(proxy, Logger);
                }
                
                return proxy;
            }
            catch (Exception ex)
            {
                Logger.Warn($"DispatchProxy failed: {ex.Message}, trying alternative approach");
                
                // 备用方案：创建一个简单的动态对象
                var expando = new System.Dynamic.ExpandoObject() as System.Collections.Generic.IDictionary<string, object>;
                
                // 添加所有接口方法的空实现
                foreach (var method in loggerInterfaceType.GetMethods())
                {
                    // 创建委托类型并创建空委托
                    var emptyDelegate = CreateEmptyDelegate(method);
                    if (emptyDelegate != null)
                    {
                        expando[method.Name] = emptyDelegate;
                    }
                }
                
                return expando;
            }
        }

        private object? CreateEmptyDelegate(System.Reflection.MethodInfo method)
        {
            try
            {
                var parameters = method.GetParameters();
                var returnType = method.ReturnType;

                // 根据参数数量和返回类型创建空委托
                if (returnType == typeof(void))
                {
                    switch (parameters.Length)
                    {
                        case 0: return new Action(() => { });
                        case 1: return Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(parameters[0].ParameterType), this, GetType().GetMethod("EmptyAction1", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                        case 2: return Delegate.CreateDelegate(typeof(Action<,>).MakeGenericType(parameters[0].ParameterType, parameters[1].ParameterType), this, GetType().GetMethod("EmptyAction2", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                        case 3: return Delegate.CreateDelegate(typeof(Action<,,>).MakeGenericType(parameters[0].ParameterType, parameters[1].ParameterType, parameters[2].ParameterType), this, GetType().GetMethod("EmptyAction3", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                    }
                }
                else
                {
                    switch (parameters.Length)
                    {
                        case 0: return Delegate.CreateDelegate(typeof(Func<>).MakeGenericType(returnType), this, GetType().GetMethod("EmptyFunc0", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.MakeGenericMethod(returnType));
                        case 1: return Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(parameters[0].ParameterType, returnType), this, GetType().GetMethod("EmptyFunc1", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.MakeGenericMethod(parameters[0].ParameterType, returnType));
                        case 2: return Delegate.CreateDelegate(typeof(Func<,,>).MakeGenericType(parameters[0].ParameterType, parameters[1].ParameterType, returnType), this, GetType().GetMethod("EmptyFunc2", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.MakeGenericMethod(parameters[0].ParameterType, parameters[1].ParameterType, returnType));
                    }
                }
            }
            catch { }
            
            return null;
        }

        private void EmptyAction1<T1>(T1 arg1) { }
        private void EmptyAction2<T1, T2>(T1 arg1, T2 arg2) { }
        private void EmptyAction3<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3) { }
        private T EmptyFunc0<T>() { return default!; }
        private T EmptyFunc1<T1, T>(T1 arg1) { return default!; }
        private T EmptyFunc2<T1, T2, T>(T1 arg1, T2 arg2) { return default!; }

        // 日志代理类 - 泛型版本
        private class SecsGemLoggerProxy<T> : System.Reflection.DispatchProxy
        {
            public log4net.ILog _logger;

            protected override object Invoke(System.Reflection.MethodInfo targetMethod, object[] args)
            {
                try
                {
                    if (_logger != null && targetMethod != null)
                    {
                        if (targetMethod.Name.StartsWith("Log"))
                        {
                            if (args != null && args.Length > 0 && args[0] is string message)
                            {
                                _logger.Info($"SecsGem: {message}");
                            }
                        }
                        else if (targetMethod.Name.StartsWith("Debug"))
                        {
                            if (args != null && args.Length > 0 && args[0] is string message)
                            {
                                _logger.Debug($"SecsGem: {message}");
                            }
                        }
                        else if (targetMethod.Name.StartsWith("Info"))
                        {
                            if (args != null && args.Length > 0 && args[0] is string message)
                            {
                                _logger.Info($"SecsGem: {message}");
                            }
                        }
                        else if (targetMethod.Name.StartsWith("Warn"))
                        {
                            if (args != null && args.Length > 0 && args[0] is string message)
                            {
                                _logger.Warn($"SecsGem: {message}");
                            }
                        }
                        else if (targetMethod.Name.StartsWith("Error"))
                        {
                            if (args != null && args.Length > 0 && args[0] is string message)
                            {
                                _logger.Error($"SecsGem: {message}");
                            }
                        }
                    }
                }
                catch { }

                return targetMethod != null && targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
            }
        }

    private void SetPropertyValue(object obj, string propertyName, object value)
    {
        var property = obj.GetType().GetProperty(propertyName);
        if (property != null)
        {
            try
            {
                var propertyType = property.PropertyType;
                if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    propertyType = Nullable.GetUnderlyingType(propertyType)!;
                }
                
                // 处理 TimeSpan 转 int 的情况
                if (propertyType == typeof(int) && value is TimeSpan timeSpanValue)
                {
                    property.SetValue(obj, (int)timeSpanValue.TotalMilliseconds);
                }
                // 处理 int 转 ushort 的情况
                else if (propertyType == typeof(ushort) && value is int intValue)
                {
                    property.SetValue(obj, (ushort)intValue);
                }
                else
                {
                    property.SetValue(obj, value);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting property {propertyName}: {ex.Message}");
                throw;
            }
        }
        else
        {
            Logger.Debug($"Property {propertyName} not found on {obj.GetType().Name}");
        }
    }

    private object? SendSecsMessage(byte stream, byte function, bool replyExpected, object? item, CancellationToken cancellationToken)
    {
        if (_secsGem == null) return null;
        
        try
        {
            var messageType = Type.GetType("Secs4Net.SecsMessage, Secs4Net");
            if (messageType == null)
            {
                Logger.Error("SecsMessage type not found");
                return null;
            }

            object[] constructorArgs;
            if (item != null)
            {
                constructorArgs = new object[] { stream, function, !replyExpected, item };
            }
            else
            {
                constructorArgs = new object[] { stream, function, !replyExpected };
            }

            var message = Activator.CreateInstance(messageType, constructorArgs);
            if (message == null)
            {
                Logger.Error("Failed to create SecsMessage instance");
                return null;
            }

            var sendMethod = _secsGem.GetType().GetMethod("SendAsync", new[] { messageType, typeof(CancellationToken) });
            if (sendMethod != null)
            {
                var task = (Task)sendMethod.Invoke(_secsGem, new object[] { message, cancellationToken });
                task.Wait(cancellationToken);
                var resultProperty = task.GetType().GetProperty("Result");
                return resultProperty?.GetValue(task);
            }

            Logger.Error("SendAsync method not found on SecsGem");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error sending SECS message: {ex.Message}", ex);
            return null;
        }
    }

    private object? GetSecsItemFromReply(object reply)
    {
        try
        {
            var secsItemProp = reply.GetType().GetProperty("SecsItem");
            return secsItemProp?.GetValue(reply);
        }
        catch
        {
            return null;
        }
    }

    private object? CreateSecsItemFromValue(object value)
    {
        try
        {
            var itemType = Type.GetType("Secs4Net.Item, Secs4Net");
            if (itemType == null)
            {
                Logger.Error("Item type not found in Secs4Net");
                return null;
            }

            if (value is string str)
            {
                var asciiMethod = itemType.GetMethod("ASCII", new[] { typeof(string) });
                return asciiMethod?.Invoke(null, new object[] { str });
            }
            else if (value is bool b)
            {
                var boolMethod = itemType.GetMethod("Boolean", new[] { typeof(bool) });
                return boolMethod?.Invoke(null, new object[] { b });
            }
            else if (value is bool[] bools)
            {
                var boolArrayMethod = itemType.GetMethod("Boolean", new[] { typeof(bool[]) });
                return boolArrayMethod?.Invoke(null, new object[] { bools });
            }
            else if (value is byte[] bytes)
            {
                var uint8Method = itemType.GetMethod("UInt8", new[] { typeof(byte[]) });
                return uint8Method?.Invoke(null, new object[] { bytes });
            }
            else if (value is int[] ints)
            {
                var int32Method = itemType.GetMethod("Int32", new[] { typeof(int[]) });
                return int32Method?.Invoke(null, new object[] { ints });
            }
            else if (value is float[] floats)
            {
                var float4Method = itemType.GetMethod("Float4", new[] { typeof(float[]) });
                return float4Method?.Invoke(null, new object[] { floats });
            }
            else if (value is double[] doubles)
            {
                var float8Method = itemType.GetMethod("Float8", new[] { typeof(double[]) });
                return float8Method?.Invoke(null, new object[] { doubles });
            }

            var asciiMethodFallback = itemType.GetMethod("ASCII", new[] { typeof(string) });
            return asciiMethodFallback?.Invoke(null, new object[] { value.ToString() ?? string.Empty });
        }
        catch (Exception ex)
        {
            Logger.Error($"Error creating SECS item: {ex.Message}", ex);
            return null;
        }
    }

    private void Cleanup()
    {
        if (_secsGem != null)
        {
            try
            {
                var disposeMethod = _secsGem.GetType().GetMethod("Dispose");
                if (disposeMethod != null)
                {
                    disposeMethod.Invoke(_secsGem, null);
                }
            }
            catch
            {
                // Ignore dispose errors
            }
            _secsGem = null;
        }
        _pollingTask = null;
        _pollingCts = null;
        _messageListeningTask = null;
        _messageListeningCts = null;
    }

    public async Task<DataValue> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _secsGem == null)
        {
            Logger.Warn($"HSMS client not connected, cannot read node: {nodeId}");
            return EAP.Core.Protocol.DataValue.NotConnected();
        }

        try
        {
            Logger.Debug($"Reading HSMS node: {nodeId}");
            
            var (stream, function) = ParseNodeId(nodeId);
            var reply = SendSecsMessage(stream, function, true, null, cancellationToken);
            
            if (reply != null)
            {
                var secsItem = GetSecsItemFromReply(reply);
                if (secsItem != null)
                {
                    var value = ConvertSecsItemToValue(secsItem);
                    return new DataValue
                    {
                        Value = value,
                        Quality = DataQuality.Good,
                        Timestamp = DateTime.UtcNow
                    };
                }
            }

            return EAP.Core.Protocol.DataValue.Bad("No data returned");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error reading HSMS node {nodeId}: {ex.Message}", ex);
            return EAP.Core.Protocol.DataValue.Bad(ex.Message);
        }
    }

    public async Task<bool> WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _secsGem == null)
        {
            Logger.Warn($"HSMS client not connected, cannot write node: {nodeId}");
            return false;
        }

        try
        {
            Logger.Debug($"Writing HSMS node {nodeId} with value: {value}");
            
            var (stream, function) = ParseNodeId(nodeId);
            var item = CreateSecsItemFromValue(value);
            SendSecsMessage(stream, function, false, item, cancellationToken);

            _tagValues[nodeId] = value;
            Logger.Info($"Successfully wrote to HSMS node {nodeId}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error writing HSMS node {nodeId}: {ex.Message}", ex);
            return false;
        }
    }

    private (byte Stream, byte Function) ParseNodeId(string nodeId)
    {
        try
        {
            var parts = nodeId.Split('F');
            if (parts.Length != 2)
                throw new FormatException("Invalid nodeId format. Expected SxFx");

            var streamPart = parts[0].Trim().ToUpper();
            var functionPart = parts[1].Trim();

            if (!streamPart.StartsWith("S"))
                throw new FormatException("NodeId must start with S");

            byte stream = Convert.ToByte(streamPart.Substring(1));
            byte function = Convert.ToByte(functionPart);

            return (stream, function);
        }
        catch (Exception ex)
        {
            throw new FormatException($"Invalid HSMS nodeId format: {nodeId}. Expected SxFx", ex);
        }
    }

    public Task SubscribeNodeAsync(string nodeId, int updateRate = 1000, CancellationToken cancellationToken = default)
    {
        Logger.Info($"HSMS subscription requested for node: {nodeId} (updateRate: {updateRate})");
        _subscribedTags[nodeId] = updateRate;
        return Task.CompletedTask;
    }

    public Task UnsubscribeNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        Logger.Info($"HSMS unsubscription requested for node: {nodeId}");
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
