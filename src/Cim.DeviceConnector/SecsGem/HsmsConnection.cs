using System.Net.Sockets;
using System.Net;
using Cim.Core.Models;

namespace Cim.DeviceConnector.SecsGem;

/// <summary>
/// HSMS (SECS/GEM over TCP/IP) 通信实现
/// 支持 SECS-I/II 消息格式和 GEM 标准功能
/// </summary>
public class HsmsConnection
{
    private readonly string _equipmentId;
    private readonly string _host;
    private readonly int _port;
    private readonly int _deviceId;
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    
    // Callbacks for UI integration
    private readonly Action? _onConnected;
    private readonly Action? _onDisconnected;
    private readonly Action<string>? _onMessageSent;
    private readonly Action<string>? _onMessageReceived;
    private readonly Action<Exception>? _onError;
    
    public event EventHandler<SecsGemMessageEventArgs>? MessageReceived;
    public event EventHandler<ConnectionStatus>? ConnectionChanged;

    public ConnectionStatus Status { get; private set; } = ConnectionStatus.Disconnected;
    public DateTime LastHeartbeat { get; private set; }

    /// <summary>
    /// 构造函数 - 用于服务间调用
    /// </summary>
    public HsmsConnection(string equipmentId, string host, int port, int deviceId)
    {
        _equipmentId = equipmentId;
        _host = host;
        _port = port;
        _deviceId = deviceId;
    }

    /// <summary>
    /// 构造函数 - 用于 UI 集成，带回调函数
    /// </summary>
    public HsmsConnection(
        string host, 
        int port, 
        string deviceId,
        Action? onConnected = null,
        Action? onDisconnected = null,
        Action<string>? onMessageSent = null,
        Action<string>? onMessageReceived = null,
        Action<Exception>? onError = null)
    {
        _host = host;
        _port = port;
        _deviceId = int.TryParse(deviceId.Replace("DEVICE-", ""), out var d) ? d : 1;
        _equipmentId = deviceId;
        _onConnected = onConnected;
        _onDisconnected = onDisconnected;
        _onMessageSent = onMessageSent;
        _onMessageReceived = onMessageReceived;
        _onError = onError;
    }

    /// <summary>
    /// 连接到设备
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (Status == ConnectionStatus.Connected || Status == ConnectionStatus.Connecting)
            return;

        Status = ConnectionStatus.Connecting;
        ConnectionChanged?.Invoke(this, Status);

        try
        {
            _tcpClient = new TcpClient();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var connectTask = _tcpClient.ConnectAsync(_host, _port);
            if (await Task.WhenAny(connectTask, Task.Delay(10000, _cts.Token)) != connectTask)
            {
                throw new TimeoutException($"Connection to {_host}:{_port} timed out");
            }

            await connectTask; // Propagate any exceptions
            _networkStream = _tcpClient.GetStream();
            
            Status = ConnectionStatus.Connected;
            LastHeartbeat = DateTime.UtcNow;
            ConnectionChanged?.Invoke(this, Status);
            
            // Invoke callback if provided
            _onConnected?.Invoke();

            // Start receiving messages
            _receiveTask = ReceiveLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            Status = ConnectionStatus.Disconnected;
            ConnectionChanged?.Invoke(this, Status);
            _onError?.Invoke(ex);
            throw new InvalidOperationException($"Failed to connect: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 断开连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        
        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch (OperationCanceledException) { }
        }

        _networkStream?.Close();
        _tcpClient?.Close();
        _tcpClient?.Dispose();
        
        _networkStream = null;
        _tcpClient = null;
        Status = ConnectionStatus.Disconnected;
        ConnectionChanged?.Invoke(this, Status);
        
        // Invoke callback if provided
        _onDisconnected?.Invoke();
    }

    /// <summary>
    /// 接收消息循环
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[65536];
        
        while (!cancellationToken.IsCancellationRequested && _networkStream != null)
        {
            try
            {
                var lengthBytes = new byte[4];
                var readLength = await _networkStream.ReadAsync(lengthBytes, 0, 4, cancellationToken);
                
                if (readLength != 4)
                    break;

                int messageLength = BitConverter.ToInt32(lengthBytes.Reverse().ToArray(), 0);
                
                if (messageLength <= 0 || messageLength > buffer.Length)
                    continue;

                var messageData = new byte[messageLength];
                var readDataLength = await _networkStream.ReadAsync(messageData, 0, messageLength, cancellationToken);
                
                if (readDataLength != messageLength)
                    continue;

                // Parse SECS message
                var message = ParseSecsMessage(messageData);
                if (message != null)
                {
                    LastHeartbeat = DateTime.UtcNow;
                    
                    // Format message for logging
                    var messageStr = $"S{message.Stream}F{message.Function} ({(message.IsPrimary ? "Primary" : "Secondary")})";
                    _onMessageReceived?.Invoke(messageStr);
                    
                    MessageReceived?.Invoke(this, new SecsGemMessageEventArgs(message));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
                _onError?.Invoke(ex);
            }
        }
    }

    /// <summary>
    /// 发送 SECS 消息
    /// </summary>
    public async Task SendAsync(SecsGemMessage message)
    {
        if (_networkStream == null || Status != ConnectionStatus.Connected)
            throw new InvalidOperationException("Not connected");

        var messageData = BuildSecsMessage(message);
        var lengthBytes = BitConverter.GetBytes(messageData.Length).Reverse().ToArray();
        
        await _networkStream.WriteAsync(lengthBytes);
        await _networkStream.WriteAsync(messageData);
        await _networkStream.FlushAsync();
        
        // Log sent message
        var messageStr = $"S{message.Stream}F{message.Function} ({(message.IsPrimary ? "Primary" : "Secondary")})";
        _onMessageSent?.Invoke(messageStr);
    }

    /// <summary>
    /// 构建 SECS 消息
    /// </summary>
    private byte[] BuildSecsMessage(SecsGemMessage message)
    {
        using var ms = new MemoryStream();
        
        // Header: Device ID (2 bytes), Stream (1), Function (1), Type (1), System Bytes (2)
        ms.WriteByte((byte)((_deviceId >> 8) & 0xFF));
        ms.WriteByte((byte)(_deviceId & 0xFF));
        ms.WriteByte(message.Stream);
        ms.WriteByte(message.Function);
        ms.WriteByte(message.IsPrimary ? (byte)0 : (byte)0x80); // Primary/Secondary bit
        ms.Write(message.SystemBytes, 0, 2);
        
        // Data (TLV format - simplified)
        if (message.Data.Length > 0)
        {
            ms.Write(message.Data, 0, message.Data.Length);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// 解析 SECS 消息
    /// </summary>
    private SecsGemMessage? ParseSecsMessage(byte[] data)
    {
        if (data.Length < 10) // Minimum header size
            return null;

        try
        {
            int offset = 0;
            var deviceId = (data[offset++] << 8) | data[offset++];
            var stream = data[offset++];
            var function = data[offset++];
            var type = data[offset++];
            var systemBytes = data.Skip(offset).Take(2).ToArray();
            offset += 2;
            
            var msgData = data.Skip(offset).ToArray();

            return new SecsGemMessage
            {
                Stream = stream,
                Function = function,
                IsPrimary = (type & 0x80) == 0,
                SystemBytes = systemBytes,
                Data = msgData
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 发送 S1F1 (AREQ - Access Request)
    /// </summary>
    public async Task SendAccessRequestAsync()
    {
        var message = new SecsGemMessage
        {
            Stream = 1,
            Function = 1,
            IsPrimary = true,
            SystemBytes = GenerateSystemBytes()
        };
        await SendAsync(message);
    }

    /// <summary>
    /// 发送 S2F41 (PP_REQ - Process Program Request)
    /// </summary>
    public async Task SendProcessProgramRequestAsync()
    {
        var message = new SecsGemMessage
        {
            Stream = 2,
            Function = 41,
            IsPrimary = true,
            SystemBytes = GenerateSystemBytes()
        };
        await SendAsync(message);
    }

    /// <summary>
    /// 发送 S2F43 (PP_SELECT - Process Program Select)
    /// </summary>
    public async Task SendProcessProgramSelectAsync(string recipeId)
    {
        var message = new SecsGemMessage
        {
            Stream = 2,
            Function = 43,
            IsPrimary = true,
            SystemBytes = GenerateSystemBytes(),
            Data = EncodeRecipeId(recipeId)
        };
        await SendAsync(message);
    }

    /// <summary>
    /// 发送 S2F47 (PP_CREATE - Download Recipe)
    /// </summary>
    public async Task SendProcessProgramCreateAsync(string recipeId, byte[] recipeBody)
    {
        var message = new SecsGemMessage
        {
            Stream = 2,
            Function = 47,
            IsPrimary = true,
            SystemBytes = GenerateSystemBytes(),
            Data = EncodeRecipeDownload(recipeId, recipeBody)
        };
        await SendAsync(message);
    }

    /// <summary>
    /// 发送 S2F13 (VREQ - Variable Request)
    /// </summary>
    public async Task SendVariableRequestAsync(List<string> variableIds)
    {
        var message = new SecsGemMessage
        {
            Stream = 2,
            Function = 13,
            IsPrimary = true,
            SystemBytes = GenerateSystemBytes(),
            Data = EncodeVariableIds(variableIds)
        };
        await SendAsync(message);
    }

    /// <summary>
    /// 发送 S5F1 (ALMD - Alarm Display)
    /// </summary>
    public async Task SendAlarmDisplayAsync(int alarmId, bool isSet)
    {
        var message = new SecsGemMessage
        {
            Stream = 5,
            Function = 1,
            IsPrimary = true,
            SystemBytes = GenerateSystemBytes(),
            Data = EncodeAlarm(alarmId, isSet)
        };
        await SendAsync(message);
    }

    /// <summary>
    /// 生成系统字节
    /// </summary>
    private byte[] GenerateSystemBytes()
    {
        var random = new Random();
        return new byte[] { (byte)random.Next(256), (byte)random.Next(256) };
    }

    /// <summary>
    /// 编码配方 ID
    /// </summary>
    private byte[] EncodeRecipeId(string recipeId)
    {
        return System.Text.Encoding.ASCII.GetBytes(recipeId);
    }

    /// <summary>
    /// 编码配方下载数据
    /// </summary>
    private byte[] EncodeRecipeDownload(string recipeId, byte[] recipeBody)
    {
        using var ms = new MemoryStream();
        var idBytes = EncodeRecipeId(recipeId);
        
        // Write length and ID
        ms.WriteByte((byte)idBytes.Length);
        ms.Write(idBytes, 0, idBytes.Length);
        
        // Write body
        ms.Write(recipeBody, 0, recipeBody.Length);
        
        return ms.ToArray();
    }

    /// <summary>
    /// 编码变量 ID 列表
    /// </summary>
    private byte[] EncodeVariableIds(List<string> variableIds)
    {
        using var ms = new MemoryStream();
        foreach (var vid in variableIds)
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes(vid);
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes, 0, bytes.Length);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// 编码报警信息
    /// </summary>
    private byte[] EncodeAlarm(int alarmId, bool isSet)
    {
        return new byte[]
        {
            (byte)((alarmId >> 8) & 0xFF),
            (byte)(alarmId & 0xFF),
            (byte)(isSet ? 1 : 0)
        };
    }

    /// <summary>
    /// 发送 S1F13 (Establish Communication Request) - 获取设备状态
    /// </summary>
    public async Task<string> SendStatusRequestAsync()
    {
        var message = new SecsGemMessage
        {
            Stream = 1,
            Function = 13,
            IsPrimary = true,
            SystemBytes = GenerateSystemBytes()
        };
        await SendAsync(message);
        return "S1F13 sent";
    }

    /// <summary>
    /// 收集数据 - S2F13 (Variable Request)
    /// </summary>
    public async Task<string> CollectDataAsync(int[] variableIds)
    {
        var variableIdStrings = variableIds.Select(id => id.ToString()).ToList();
        var message = new SecsGemMessage
        {
            Stream = 2,
            Function = 13,
            IsPrimary = true,
            SystemBytes = GenerateSystemBytes(),
            Data = EncodeVariableIds(variableIdStrings)
        };
        await SendAsync(message);
        return $"S2F13 sent for variables: {string.Join(", ", variableIds)}";
    }

    /// <summary>
    /// 上传配方 - S7F1 (Process Program Request)
    /// </summary>
    public async Task<byte[]> UploadRecipeAsync(string recipeName)
    {
        var message = new SecsGemMessage
        {
            Stream = 7,
            Function = 1,
            IsPrimary = true,
            SystemBytes = GenerateSystemBytes(),
            Data = EncodeRecipeId(recipeName)
        };
        await SendAsync(message);
        // Return placeholder data - in real scenario would wait for response
        return System.Text.Encoding.UTF8.GetBytes($"RECIPE:{recipeName}:CONTENT");
    }

    /// <summary>
    /// 下载配方 - S7F5 (Process Program Send)
    /// </summary>
    public async Task<string> DownloadRecipeAsync(string recipeName, byte[] recipeData)
    {
        var message = new SecsGemMessage
        {
            Stream = 7,
            Function = 5,
            IsPrimary = true,
            SystemBytes = GenerateSystemBytes(),
            Data = EncodeRecipeDownload(recipeName, recipeData)
        };
        await SendAsync(message);
        return $"S7F5 sent - Recipe '{recipeName}' downloaded ({recipeData.Length} bytes)";
    }
}

/// <summary>
/// SECS/GEM 消息事件参数
/// </summary>
public class SecsGemMessageEventArgs : EventArgs
{
    public SecsGemMessage Message { get; }

    public SecsGemMessageEventArgs(SecsGemMessage message)
    {
        Message = message;
    }
}

/// <summary>
/// 连接状态枚举
/// </summary>
public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Authenticating,
    Authenticated
}
