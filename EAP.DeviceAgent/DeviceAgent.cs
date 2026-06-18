using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EAP.Adapters.Factory;
using EAP.Core.Configuration;
using EAP.Core.Protocol;
using log4net;

namespace EAP.DeviceAgent;

public class DeviceAgent
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(DeviceAgent));
    
    private NamedPipeServerStream? _pipeServer;
    private IProtocolClient? _protocolClient;
    private DeviceConfig? _deviceConfig;
    private CancellationTokenSource? _cts;

    public async Task RunAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Logger.Error("Usage: EAP.DeviceAgent.exe <deviceId> <pipeName> [deviceName]");
            return;
        }

        var deviceId = args[0];
        var pipeName = args[1];
        var deviceName = args.Length > 2 ? args[2] : deviceId;
        
        SetProcessTitle(deviceName);
        
        Logger.Info($"Device Agent starting for device: {deviceId}, pipe: {pipeName}, name: {deviceName}");

        try
        {
            _deviceConfig = LoadDeviceConfig(deviceId);
            if (_deviceConfig == null)
            {
                Logger.Error($"Device config not found: {deviceId}");
                return;
            }

            _cts = new CancellationTokenSource();
            
            _protocolClient = ProtocolClientFactory.CreateClient(_deviceConfig);
            _protocolClient.ConnectionStatusChanged += ProtocolClient_ConnectionStatusChanged;
            _protocolClient.DataValueChanged += ProtocolClient_DataValueChanged;

            await _protocolClient.ConnectAsync(_cts.Token).ConfigureAwait(false);
            Logger.Info($"Device {deviceId} connected");

            await StartPipeServer(pipeName, _cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error($"Device agent error: {ex.Message}", ex);
            Environment.Exit(1);
        }
    }

    private DeviceConfig? LoadDeviceConfig(string deviceId)
    {
        var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        if (!Directory.Exists(configDir))
        {
            configDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Config"));
        }

        if (!Directory.Exists(configDir))
        {
            Logger.Error($"Config directory not found: {configDir}");
            return null;
        }

        try
        {
            var config = ConfigurationLoader.LoadConfiguration(configDir);
            return config.Devices.FirstOrDefault(d => d.Id == deviceId);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load device config: {ex.Message}", ex);
            return null;
        }
    }

    private async Task StartPipeServer(string pipeName, CancellationToken cancellationToken)
    {
        _pipeServer = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous);

        Logger.Info($"Pipe server started: {pipeName}");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _pipeServer.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                Logger.Info("Client connected");

                await ProcessClientMessages(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error($"Pipe error: {ex.Message}", ex);
            }
            finally
            {
                if (_pipeServer.IsConnected)
                {
                    _pipeServer.Disconnect();
                }
            }
        }
    }

    private async Task ProcessClientMessages(CancellationToken cancellationToken)
    {
        if (_pipeServer == null) return;

        var buffer = new byte[4096];
        
        while (!cancellationToken.IsCancellationRequested && _pipeServer.IsConnected)
        {
            try
            {
                var bytesRead = await _pipeServer.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0) break;

                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Logger.Debug($"Received message: {message}");

                await HandleMessage(message).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing message: {ex.Message}", ex);
                break;
            }
        }
    }

    private async Task HandleMessage(string message)
    {
        if (_pipeServer == null || _protocolClient == null) return;

        var parts = message.Split('|');
        if (parts.Length < 2)
        {
            await SendResponse("ERROR|Invalid message format").ConfigureAwait(false);
            return;
        }

        var command = parts[0];
        
        try
        {
            switch (command)
            {
                case "READ":
                    {
                        var nodeId = parts[1];
                        var value = await _protocolClient.ReadNodeAsync(nodeId).ConfigureAwait(false);
                        await SendResponse($"READ_OK|{nodeId}|{value.Quality}|{value.Value}").ConfigureAwait(false);
                        break;
                    }
                case "WRITE":
                    {
                        var nodeId = parts[1];
                        var value = parts[2];
                        var success = await _protocolClient.WriteNodeAsync(nodeId, value).ConfigureAwait(false);
                        await SendResponse(success ? "WRITE_OK" : "WRITE_ERROR").ConfigureAwait(false);
                        break;
                    }
                case "SUBSCRIBE":
                    {
                        var nodeId = parts[1];
                        var updateRate = parts.Length > 2 ? int.Parse(parts[2]) : 1000;
                        await _protocolClient.SubscribeNodeAsync(nodeId, updateRate).ConfigureAwait(false);
                        await SendResponse("SUBSCRIBE_OK").ConfigureAwait(false);
                        break;
                    }
                case "UNSUBSCRIBE":
                    {
                        var nodeId = parts[1];
                        await _protocolClient.UnsubscribeNodeAsync(nodeId).ConfigureAwait(false);
                        await SendResponse("UNSUBSCRIBE_OK").ConfigureAwait(false);
                        break;
                    }
                case "STATUS":
                    {
                        var status = _protocolClient.IsConnected ? "CONNECTED" : "DISCONNECTED";
                        await SendResponse($"STATUS_OK|{status}").ConfigureAwait(false);
                        break;
                    }
                case "DISCONNECT":
                    {
                        await _protocolClient.DisconnectAsync().ConfigureAwait(false);
                        await SendResponse("DISCONNECT_OK").ConfigureAwait(false);
                        _cts?.Cancel();
                        break;
                    }
                default:
                    await SendResponse($"ERROR|Unknown command: {command}").ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error handling command {command}: {ex.Message}", ex);
            await SendResponse($"ERROR|{ex.Message}").ConfigureAwait(false);
        }
    }

    private async Task SendResponse(string response)
    {
        if (_pipeServer == null || !_pipeServer.IsConnected) return;

        var bytes = Encoding.UTF8.GetBytes(response);
        await _pipeServer.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        await _pipeServer.FlushAsync().ConfigureAwait(false);
    }

    private void ProtocolClient_ConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
    {
        Logger.Info($"Device {e.ConnectionId} connection status changed: {e.IsConnected}");
        _ = SendResponse($"STATUS_CHANGED|{e.ConnectionId}|{e.IsConnected}|{e.Status}");
    }

    private void ProtocolClient_DataValueChanged(object? sender, DataValueChangedEventArgs e)
    {
        _ = SendResponse($"DATA_CHANGED|{e.ConnectionId}|{e.NodeId}|{e.Value.Quality}|{e.Value.Value}");
    }

    public void Stop()
    {
        _cts?.Cancel();
        // 使用非阻塞方式断开连接，避免死锁
        if (_protocolClient != null)
        {
            _ = _protocolClient.DisconnectAsync().ConfigureAwait(false);
        }
        _pipeServer?.Close();
    }

    private void SetProcessTitle(string deviceName)
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var title = $"EAP_{deviceName}";
            
            // 设置控制台标题（如果有控制台窗口）
            Console.Title = title;
            
            // 设置主窗口标题
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                SetWindowText(process.MainWindowHandle, title);
            }
            
            // 设置进程描述（在任务管理器的"描述"列显示）
            SetProcessDescription(process.Id, title);
            
            // 设置环境变量，方便其他工具识别
            Environment.SetEnvironmentVariable("EAP_DEVICE_NAME", title, EnvironmentVariableTarget.Process);
            
            Logger.Info($"Process title set to: {title}, PID: {process.Id}");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to set process title: {ex.Message}");
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr hProcess, int processInformationClass, IntPtr processInformation, uint processInformationLength);

    private void SetProcessDescription(int processId, string description)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            var hProcess = process.Handle;
            
            // 使用 Windows API 设置进程描述
            // 这在任务管理器的"描述"列中显示
            var info = new ProcessInformation { Description = description };
            var infoSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(info);
            var infoPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal((int)infoSize);
            
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(info, infoPtr, false);
                SetProcessInformation(hProcess, 2, infoPtr, infoSize);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(infoPtr);
            }
        }
        catch { }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct ProcessInformation
    {
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
        public string Description;
    }
}
