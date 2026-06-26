using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using EAP.Core;
using Microsoft.Extensions.Options;
using Secs4Net;

namespace EAP.Adapters.Hsms;

/// <summary>
/// HSMS/SECS-GEM 协议客户端
/// 
/// SECS/GEM 协议特殊性：
/// - SECS 消息是自描述的，通过 S（Stream）+ F（Function）编号标识消息类型
/// - 不需要预定义 Tag 点表，数据通过标准消息动态查询
/// - 支持 Host 和 Eqp 两种角色模式
/// - 支持 Active（主动连接）和 Passive（被动监听）两种连接模式
/// 
/// 心跳机制（按优先级）：
/// 1. LinkTest - HSMS 协议层面链路检测（Secs4Net 内部处理）
/// 2. S1F1 - Host 模式主动发送 Are You There?
/// 3. MessageActivity - Eqp 模式通过消息活动检测
/// 4. None - 不检测
/// </summary>
public class HsmsClient : ProtocolClientBase
{
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private SecsGem? _secsGem;
    private HsmsConnection? _connection;
    private bool _isConnected;
    private CancellationTokenSource? _messageLoopCts;
    private CancellationTokenSource? _connectionCts;

    public override ProtocolType ProtocolType => ProtocolType.Hsms;
    public override bool IsConnected => _isConnected;

    public HsmsClient(DeviceConfig config) : base(config)
    {
        if (config.HsmsConfig == null)
        {
            throw new ArgumentException("HSMS configuration is required", nameof(config));
        }
    }

    public override async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected)
            {
                DeviceLogger.Warn(_deviceConfig.DeviceId, $"HSMS client already connected: {ConnectionId}");
                return true;
            }

            var config = _deviceConfig.HsmsConfig!;
            int deviceId = config.DeviceId > 0 ? config.DeviceId : 1;

            DeviceLogger.Info(_deviceConfig.DeviceId,
                $"Connecting HSMS: {config.Host}:{config.Port}, " +
                $"Mode: {config.Mode}, ConnectionMode: {config.ConnectionMode}, " +
                $"HeartbeatType: {config.HeartbeatType}, DeviceId: {deviceId}");

            try
            {
                var options = Options.Create(new SecsGemOptions
                {
                    IpAddress = config.Host!,
                    Port = config.Port,
                    DeviceId = (ushort)deviceId,
                    IsActive = config.ConnectionMode == HsmsConnectionMode.Active,
                    T3 = config.T3Timeout,
                    T5 = config.T5Timeout,
                    T6 = config.T6Timeout,
                    T7 = config.T7Timeout,
                    T8 = config.T8Timeout,
                    LinkTestInterval = config.LinkTestInterval
                });

                var logger = new SecsGemLogger(_deviceConfig.DeviceId);
                _connection = new HsmsConnection(options, logger);
                _secsGem = new SecsGem(options, _connection, logger);

                _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _connectionCts.CancelAfter(TimeSpan.FromSeconds(config.ConnectionTimeout / 1000.0));

                try
                {
                    DeviceLogger.Info(_deviceConfig.DeviceId, $"Starting HSMS connection...");
                    _connection.Start(_connectionCts.Token);
                    DeviceLogger.Info(_deviceConfig.DeviceId, $"HSMS connection started, current state: {_connection.State}");

                    bool connected = await WaitForConnectionAsync(_connectionCts.Token).ConfigureAwait(false);
                    if (!connected)
                    {
                        DeviceLogger.Error(_deviceConfig.DeviceId,
                            $"HSMS connection failed or timeout, final state: {_connection?.State}");
                        OnConnectionStatusChanged(false, "Connection failed");
                        Cleanup();
                        return false;
                    }

                    _isConnected = true;
                    DeviceLogger.Info(_deviceConfig.DeviceId,
                        $"HSMS connected successfully: {ConnectionId}, state: {_connection.State}");

                    OnConnectionStatusChanged(true, "Connected");
                    UpdateHeartbeatStatus(true);

                    StartMessageLoop();
                    StartHeartbeat();

                    return true;
                }
                catch (Exception ex)
                {
                    DeviceLogger.Error(_deviceConfig.DeviceId,
                        $"Failed to connect to HSMS server: {ex.Message}", ex);
                    OnConnectionStatusChanged(false, "Connection failed", ex.Message);
                    Cleanup();
                    return false;
                }
            }
            catch (Exception ex)
            {
                DeviceLogger.Error(_deviceConfig.DeviceId,
                    $"Failed to create HSMS connection: {ex.Message}", ex);
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

    private async Task<bool> WaitForConnectionAsync(CancellationToken cancellationToken)
    {
        int attempts = 0;
        int maxAttempts = 100;
        DeviceLogger.Info(_deviceConfig.DeviceId, $"Waiting for HSMS connection...");

        while (!cancellationToken.IsCancellationRequested && attempts < maxAttempts)
        {
            if (_connection != null &&
                (_connection.State == ConnectionState.Connected ||
                 _connection.State == ConnectionState.Selected))
            {
                DeviceLogger.Info(_deviceConfig.DeviceId,
                    $"HSMS connection state changed to {_connection.State}");
                return true;
            }

            if (_connection != null)
            {
                DeviceLogger.Debug(_deviceConfig.DeviceId,
                    $"HSMS connection state: {_connection.State}, attempt {attempts + 1}/{maxAttempts}");
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            attempts++;
        }

        DeviceLogger.Warn(_deviceConfig.DeviceId,
            $"HSMS connection timeout after {maxAttempts * 50}ms, state: {_connection?.State}");
        return _connection != null &&
               (_connection.State == ConnectionState.Connected ||
                _connection.State == ConnectionState.Selected);
    }

    private void StartMessageLoop()
    {
        _messageLoopCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                DeviceLogger.Info(_deviceConfig.DeviceId, "HSMS message loop started");
                await foreach (var e in _secsGem!.GetPrimaryMessageAsync(_messageLoopCts.Token)
                    .ConfigureAwait(false))
                {
                    try
                    {
                        await ProcessPrimaryMessageAsync(e).ConfigureAwait(false);
                    }
                    finally
                    {
                        e.PrimaryMessage.Dispose();
                    }

                    var config = _deviceConfig.HsmsConfig!;
                    if (config.HeartbeatType == HsmsHeartbeatType.MessageActivity ||
                        config.Mode == HsmsMode.Eqp)
                    {
                        UpdateHeartbeatStatus(true);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                DeviceLogger.Info(_deviceConfig.DeviceId, "HSMS message loop cancelled");
            }
            catch (Exception ex)
            {
                DeviceLogger.Error(_deviceConfig.DeviceId,
                    $"HSMS message loop error: {ex.Message}", ex);
            }
            finally
            {
                if (_isConnected &&
                    (_connection == null ||
                     (_connection.State != ConnectionState.Connected &&
                      _connection.State != ConnectionState.Selected)))
                {
                    _isConnected = false;
                    DeviceLogger.Error(_deviceConfig.DeviceId,
                        $"HSMS connection lost, state: {_connection?.State}");
                    OnConnectionStatusChanged(false, "Connection lost");
                }
            }
        });
    }

    private async Task ProcessPrimaryMessageAsync(PrimaryMessageWrapper e)
    {
        try
        {
            var msg = e.PrimaryMessage;
            var nodeId = $"S{msg.S}F{msg.F}";

            var smlText = msg.SecsItem?.ToString() ?? "(empty)";
            var formattedSml = FormatSml(smlText);
            DeviceLogger.Info(_deviceConfig.DeviceId,
                $"← RECV S{msg.S}F{msg.F} [{(msg.ReplyExpected ? "W" : " ")}]{Environment.NewLine}{formattedSml}");

            var xml = SecsItemToXml(msg.SecsItem);
            var displayValue = $"S{msg.S}F{msg.F}:{Environment.NewLine}--- SML ---{Environment.NewLine}{formattedSml}";
            if (!string.IsNullOrEmpty(xml))
            {
                displayValue += $"{Environment.NewLine}--- XML ---{Environment.NewLine}{xml}";
            }
            _tagValues[nodeId] = displayValue;

            OnDataValueChanged(nodeId, new DataValue
            {
                Value = displayValue,
                Quality = DataQuality.Good,
                Timestamp = DateTime.UtcNow
            });

            await ReplyToPrimaryMessageAsync(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DeviceLogger.Error(_deviceConfig.DeviceId,
                $"Error processing HSMS message S{e.PrimaryMessage.S}F{e.PrimaryMessage.F}: {ex.Message}", ex);
        }
    }

    private async Task ReplyToPrimaryMessageAsync(PrimaryMessageWrapper e)
    {
        try
        {
            var config = _deviceConfig.HsmsConfig!;
            var replyF = (byte)(e.PrimaryMessage.F + 1);

            using var replyMsg = new SecsMessage(e.PrimaryMessage.S, replyF, replyExpected: false);

            if (e.PrimaryMessage.S == 1 && e.PrimaryMessage.F == 1)
            {
                replyMsg.SecsItem = Item.L(
                    Item.A(_deviceConfig.DeviceName),
                    Item.A(config.DeviceType)
                );
            }
            else
            {
                replyMsg.SecsItem = Item.L(Item.U1(0));
            }

            var replySml = replyMsg.SecsItem?.ToString() ?? "(empty)";
            var formattedReplySml = FormatSml(replySml);
            DeviceLogger.Info(_deviceConfig.DeviceId,
                $"→ REPLY S{replyMsg.S}F{replyMsg.F} [{(replyMsg.ReplyExpected ? "W" : " ")}]{Environment.NewLine}{formattedReplySml}");

            bool success = await e.TryReplyAsync(replyMsg, CancellationToken.None).ConfigureAwait(false);
            if (!success)
            {
                DeviceLogger.Warn(_deviceConfig.DeviceId,
                    $"Failed to reply S{e.PrimaryMessage.S}F{replyF}, TryReplyAsync returned false");
            }
        }
        catch (Exception ex)
        {
            DeviceLogger.Error(_deviceConfig.DeviceId,
                $"Error sending HSMS reply S{e.PrimaryMessage.S}F{e.PrimaryMessage.F + 1}: {ex.Message}", ex);
        }
    }

    private string FormatSml(string sml)
    {
        if (string.IsNullOrWhiteSpace(sml))
            return sml;

        return string.Join(Environment.NewLine,
            sml.Split('\n').Select(line => "  " + line.TrimEnd()));
    }

    protected override void StartHeartbeat()
    {
        var config = _deviceConfig.HsmsConfig!;

        if (config.HeartbeatType == HsmsHeartbeatType.None ||
            config.HeartbeatType == HsmsHeartbeatType.LinkTest)
        {
            return;
        }

        if (config.Mode == HsmsMode.Eqp && config.HeartbeatType == HsmsHeartbeatType.S1F1)
        {
            DeviceLogger.Info(_deviceConfig.DeviceId,
                "Eqp mode does not actively send S1F1 heartbeat, using MessageActivity instead");
            return;
        }

        if (config.HeartbeatType == HsmsHeartbeatType.S1F1 && config.Mode == HsmsMode.Host)
        {
            _heartbeatCts = new CancellationTokenSource();
            _heartbeatTask = Task.Run(async () =>
            {
                while (!_heartbeatCts.Token.IsCancellationRequested && IsConnected)
                {
                    try
                    {
                        using var msg = new SecsMessage(1, 1);
                        DeviceLogger.Debug(_deviceConfig.DeviceId,
                            $"→ SEND S{msg.S}F{msg.F} [{(msg.ReplyExpected ? "W" : " ")}] (heartbeat)");

                        using var reply = await _secsGem!.SendAsync(msg, _heartbeatCts.Token)
                            .ConfigureAwait(false);

                        DeviceLogger.Debug(_deviceConfig.DeviceId,
                            $"← RESP S{reply.S}F{reply.F} [{(reply.ReplyExpected ? "W" : " ")}] (heartbeat)");

                        if (reply.S == 1 && reply.F == 2)
                        {
                            UpdateHeartbeatStatus(true);
                        }
                        else
                        {
                            UpdateHeartbeatStatus(false);
                            DeviceLogger.Warn(_deviceConfig.DeviceId,
                                $"Unexpected S1F1 response: S{reply.S}F{reply.F}");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (TimeoutException ex)
                    {
                        UpdateHeartbeatStatus(false);
                        DeviceLogger.Warn(_deviceConfig.DeviceId,
                            $"S1F1 heartbeat timeout: {ex.Message}");
                    }
                    catch (IOException ex)
                    {
                        UpdateHeartbeatStatus(false);
                        DeviceLogger.Warn(_deviceConfig.DeviceId,
                            $"S1F1 heartbeat IO error: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        UpdateHeartbeatStatus(false);
                        DeviceLogger.Error(_deviceConfig.DeviceId,
                            $"S1F1 heartbeat error: {ex.Message}", ex);
                    }

                    try
                    {
                        await Task.Delay(_deviceConfig.HeartbeatInterval, _heartbeatCts.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, _heartbeatCts.Token);
        }
    }

    protected override void StopHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
        _consecutiveHeartbeatFailures = 0;
    }

    private string? SecsItemToXml(Item? item)
    {
        if (item == null)
            return null;

        try
        {
            var doc = new XDocument();
            var root = new XElement("SECS");
            doc.Add(root);
            AddItemToXml(root, item);
            return doc.ToString();
        }
        catch
        {
            return null;
        }
    }

    private void AddItemToXml(XElement parent, Item item)
    {
        if (item == null) return;

        var element = new XElement(item.Format.ToString());

        try
        {
            switch (item.Format)
            {
                case SecsFormat.List:
                    foreach (var child in item.Items)
                    {
                        AddItemToXml(element, child);
                    }
                    break;
                case SecsFormat.ASCII:
                case SecsFormat.JIS8:
                    element.Value = item.GetString() ?? string.Empty;
                    break;
                case SecsFormat.Boolean:
                    element.Value = item.FirstValue<bool>().ToString();
                    break;
                case SecsFormat.I1:
                    element.Value = string.Join(",", item.GetMemory<sbyte>().ToArray());
                    break;
                case SecsFormat.I2:
                    element.Value = string.Join(",", item.GetMemory<short>().ToArray());
                    break;
                case SecsFormat.I4:
                    element.Value = string.Join(",", item.GetMemory<int>().ToArray());
                    break;
                case SecsFormat.I8:
                    element.Value = string.Join(",", item.GetMemory<long>().ToArray());
                    break;
                case SecsFormat.U1:
                    element.Value = string.Join(",", item.GetMemory<byte>().ToArray());
                    break;
                case SecsFormat.U2:
                    element.Value = string.Join(",", item.GetMemory<ushort>().ToArray());
                    break;
                case SecsFormat.U4:
                    element.Value = string.Join(",", item.GetMemory<uint>().ToArray());
                    break;
                case SecsFormat.U8:
                    element.Value = string.Join(",", item.GetMemory<ulong>().ToArray());
                    break;
                case SecsFormat.F4:
                    element.Value = string.Join(",", item.GetMemory<float>().ToArray());
                    break;
                case SecsFormat.F8:
                    element.Value = string.Join(",", item.GetMemory<double>().ToArray());
                    break;
                case SecsFormat.Binary:
                    element.Value = BitConverter.ToString(item.GetMemory<byte>().ToArray());
                    break;
                default:
                    element.Value = item.ToString() ?? string.Empty;
                    break;
            }
        }
        catch
        {
            element.Value = item.ToString() ?? string.Empty;
        }

        parent.Add(element);
    }

    public override async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            DeviceLogger.Warn(_deviceConfig.DeviceId, $"HSMS client already disposed: {ConnectionId}");
            return;
        }

        try
        {
            _isConnected = false;
            StopPolling();
            StopHeartbeat();
            await WaitForPollingToStopAsync().ConfigureAwait(false);
            await WaitForHeartbeatToStopAsync().ConfigureAwait(false);

            Cleanup();
            DeviceLogger.Info(_deviceConfig.DeviceId, $"HSMS client disconnected: {ConnectionId}");
            OnConnectionStatusChanged(false, "Disconnected");
        }
        catch (Exception ex)
        {
            DeviceLogger.Error(_deviceConfig.DeviceId,
                $"Error disconnecting HSMS client: {ex.Message}", ex);
        }
        finally
        {
            try
            {
                _connectLock.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private void Cleanup()
    {
        _messageLoopCts?.Cancel();
        _messageLoopCts?.Dispose();
        _messageLoopCts = null;

        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = null;

        _connectionCts?.Cancel();
        _connectionCts?.Dispose();
        _connectionCts = null;

        if (_secsGem != null)
        {
            _secsGem.Dispose();
        }
        _secsGem = null;

        if (_connection != null)
        {
            try
            {
                _connection.DisposeAsync().AsTask().Wait(3000);
            }
            catch (Exception)
            {
            }
        }
        _connection = null;
    }

    public override async Task<DataValue> ReadNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _secsGem == null)
        {
            DeviceLogger.Warn(_deviceConfig.DeviceId, $"HSMS client not connected, cannot read: {nodeId}");
            return EAP.Core.DataValue.NotConnected();
        }

        try
        {
            var s = byte.Parse(nodeId.Split('F')[0].Replace("S", ""));
            var f = byte.Parse(nodeId.Split('F')[1]);

            using var msg = new SecsMessage(s, f);
            var sendSml = msg.SecsItem?.ToString() ?? "(empty)";
            var formattedSendSml = FormatSml(sendSml);
            DeviceLogger.Info(_deviceConfig.DeviceId,
                $"→ SEND S{msg.S}F{msg.F} [{(msg.ReplyExpected ? "W" : " ")}]{Environment.NewLine}{formattedSendSml}");

            using var reply = await _secsGem.SendAsync(msg, cancellationToken).ConfigureAwait(false);

            var replySml = reply.SecsItem?.ToString() ?? "(empty)";
            var formattedReplySml = FormatSml(replySml);
            DeviceLogger.Info(_deviceConfig.DeviceId,
                $"← RESP S{reply.S}F{reply.F} [{(reply.ReplyExpected ? "W" : " ")}]{Environment.NewLine}{formattedReplySml}");

            var xml = SecsItemToXml(reply.SecsItem);
            var displayValue = $"S{reply.S}F{reply.F}:{Environment.NewLine}--- SML ---{Environment.NewLine}{formattedReplySml}";
            if (!string.IsNullOrEmpty(xml))
            {
                displayValue += $"{Environment.NewLine}--- XML ---{Environment.NewLine}{xml}";
            }
            _tagValues[nodeId] = displayValue;

            return new DataValue
            {
                Value = displayValue,
                Quality = DataQuality.Good,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            DeviceLogger.Error(_deviceConfig.DeviceId,
                $"Failed to read {nodeId}: {ex.Message}", ex);
            return new DataValue
            {
                Value = null,
                Quality = DataQuality.Bad,
                Timestamp = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    public override async Task<bool> WriteNodeAsync(string nodeId, object value, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _secsGem == null)
        {
            DeviceLogger.Warn(_deviceConfig.DeviceId, $"HSMS client not connected, cannot write: {nodeId}");
            return false;
        }

        DeviceLogger.Debug(_deviceConfig.DeviceId, $"Writing {nodeId} with value: {value}");

        try
        {
            var s = byte.Parse(nodeId.Split('F')[0].Replace("S", ""));
            var f = byte.Parse(nodeId.Split('F')[1]);

            using var msg = new SecsMessage(s, f)
            {
                SecsItem = Item.A(value.ToString() ?? string.Empty)
            };

            var sendSml = msg.SecsItem?.ToString() ?? "(empty)";
            var formattedSendSml = FormatSml(sendSml);
            DeviceLogger.Info(_deviceConfig.DeviceId,
                $"→ SEND S{msg.S}F{msg.F} [{(msg.ReplyExpected ? "W" : " ")}]{Environment.NewLine}{formattedSendSml}");

            using var reply = await _secsGem.SendAsync(msg, cancellationToken).ConfigureAwait(false);

            var replySml = reply.SecsItem?.ToString() ?? "(empty)";
            var formattedReplySml = FormatSml(replySml);
            DeviceLogger.Info(_deviceConfig.DeviceId,
                $"← RESP S{reply.S}F{reply.F} [{(reply.ReplyExpected ? "W" : " ")}]{Environment.NewLine}{formattedReplySml}");

            _tagValues[nodeId] = value;
            DeviceLogger.Info(_deviceConfig.DeviceId, $"Successfully wrote {nodeId}");
            return true;
        }
        catch (Exception ex)
        {
            DeviceLogger.Error(_deviceConfig.DeviceId,
                $"Failed to write {nodeId}: {ex.Message}", ex);
            return false;
        }
    }

    public override Task SubscribeNodeAsync(string nodeId, int updateRate = 1000,
        CancellationToken cancellationToken = default)
    {
        DeviceLogger.Info(_deviceConfig.DeviceId,
            $"HSMS subscribe requested: {nodeId} (note: SECS messages are event-driven, no polling needed)");
        return base.SubscribeNodeAsync(nodeId, updateRate, cancellationToken);
    }

    public override Task UnsubscribeNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        DeviceLogger.Info(_deviceConfig.DeviceId, $"HSMS unsubscribe requested: {nodeId}");
        return base.UnsubscribeNodeAsync(nodeId, cancellationToken);
    }

    public override void Dispose()
    {
        base.Dispose();
        _connectLock.Dispose();
        Cleanup();
    }

    private class SecsGemLogger : ISecsGemLogger
    {
        private readonly string _deviceId;

        public SecsGemLogger(string deviceId)
        {
            _deviceId = deviceId;
        }

        public void Debug(string msg) => DeviceLogger.Debug(_deviceId, $"[SecsGem] {msg}");
        public void Error(string msg) => DeviceLogger.Error(_deviceId, $"[SecsGem] {msg}");
        public void Error(string msg, Exception? ex) => DeviceLogger.Error(_deviceId, $"[SecsGem] {msg}", ex);
        public void Error(string msg, SecsMessage? message, Exception? ex) =>
            DeviceLogger.Error(_deviceId, $"[SecsGem] {msg}", ex);
        public void Info(string msg) => DeviceLogger.Info(_deviceId, $"[SecsGem] {msg}");

        public void MessageIn(SecsMessage msg, int id) =>
            DeviceLogger.Debug(_deviceId, $"[SecsGem] ← IN  S{msg.S}F{msg.F}, id={id}");

        public void MessageOut(SecsMessage msg, int id) =>
            DeviceLogger.Debug(_deviceId, $"[SecsGem] → OUT S{msg.S}F{msg.F}, id={id}");

        public void Warning(string msg) => DeviceLogger.Warn(_deviceId, $"[SecsGem] {msg}");
    }
}
