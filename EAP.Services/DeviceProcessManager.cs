using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EAP.Core.Configuration;
using log4net;

namespace EAP.Services;

public interface IDeviceProcessManager
{
    Task StartDeviceProcessAsync(DeviceConfig deviceConfig);
    Task StopDeviceProcessAsync(string deviceId);
    Task StopAllProcessesAsync();
    bool IsProcessRunning(string deviceId);
    event EventHandler<ProcessStatusChangedEventArgs>? ProcessStatusChanged;
}

public class ProcessStatusChangedEventArgs : EventArgs
{
    public string DeviceId { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public int ExitCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DeviceProcessManager : IDeviceProcessManager
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(DeviceProcessManager));
    
    private readonly ConcurrentDictionary<string, Process> _processes = new();
    private readonly SemaphoreSlim _processLock = new(1, 1);
    private string _agentExePath = string.Empty;

    public event EventHandler<ProcessStatusChangedEventArgs>? ProcessStatusChanged;

    public DeviceProcessManager(string agentExePath)
    {
        _agentExePath = ResolveAgentPath(agentExePath);
    }

    private string ResolveAgentPath(string agentExePath)
    {
        // 如果是相对路径，转换为绝对路径
        if (!Path.IsPathRooted(agentExePath))
        {
            agentExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, agentExePath);
        }
        
        agentExePath = Path.GetFullPath(agentExePath);
        
        if (File.Exists(agentExePath))
        {
            Logger.Info($"Device agent path resolved to: {agentExePath}");
            return agentExePath;
        }

        // 尝试其他可能的路径
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EAP.DeviceAgent.exe"),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "EAP.DeviceAgent", "bin", "Debug", "net10.0", "EAP.DeviceAgent.exe")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "EAP.DeviceAgent", "bin", "Debug", "net10.0", "EAP.DeviceAgent.exe"))
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                Logger.Info($"Device agent found at fallback path: {path}");
                return path;
            }
        }

        Logger.Warn($"Device agent executable not found at: {agentExePath}");
        return agentExePath;
    }

    public async Task StartDeviceProcessAsync(DeviceConfig deviceConfig)
    {
        await _processLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_processes.ContainsKey(deviceConfig.Id))
            {
                Logger.Warn($"Device process already running: {deviceConfig.Id}");
                return;
            }

            if (!File.Exists(_agentExePath))
            {
                Logger.Error($"Device agent executable not found: {_agentExePath}");
                OnProcessStatusChanged(deviceConfig.Id, false, -1, "Agent executable not found");
                return;
            }

            var pipeName = $"EAP_Device_{deviceConfig.Id}";
            var processName = $"EAP_{deviceConfig.Name}";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _agentExePath,
                Arguments = $"\"{deviceConfig.Id}\" \"{pipeName}\" \"{deviceConfig.Name}\"",
                WorkingDirectory = Path.GetDirectoryName(_agentExePath) ?? AppDomain.CurrentDomain.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new Process
            {
                StartInfo = processStartInfo,
                EnableRaisingEvents = true
            };
            
            process.StartInfo.EnvironmentVariables["EAP_DEVICE_NAME"] = processName;

            process.Exited += (sender, e) => Process_Exited(sender as Process, deviceConfig.Id);
            process.OutputDataReceived += (sender, e) => Process_OutputReceived(deviceConfig.Id, e.Data);
            process.ErrorDataReceived += (sender, e) => Process_ErrorReceived(deviceConfig.Id, e.Data);

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                _processes.TryAdd(deviceConfig.Id, process);
                
                Logger.Info($"Device process started: {deviceConfig.Id}, PID: {process.Id}");
                
                // 等待进程启动并检查是否正常运行
                var isHealthy = await WaitForProcessHealthy(process, deviceConfig.Id).ConfigureAwait(false);
                
                if (isHealthy)
                {
                    OnProcessStatusChanged(deviceConfig.Id, true, 0, null);
                }
                else
                {
                    Logger.Error($"Device process failed to start properly: {deviceConfig.Id}");
                    OnProcessStatusChanged(deviceConfig.Id, false, process.HasExited ? process.ExitCode : -2, "Process failed to initialize");
                    
                    // 清理失败的进程
                    if (!process.HasExited)
                    {
                        try { process.Kill(); } catch { }
                    }
                    _processes.TryRemove(deviceConfig.Id, out _);
                    process.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to start device process {deviceConfig.Id}: {ex.Message}", ex);
                OnProcessStatusChanged(deviceConfig.Id, false, -1, ex.Message);
            }
        }
        finally
        {
            _processLock.Release();
        }
    }

    private async Task<bool> WaitForProcessHealthy(Process process, string deviceId)
    {
        const int maxWaitMs = 5000;
        const int checkIntervalMs = 200;
        var elapsedMs = 0;

        while (elapsedMs < maxWaitMs)
        {
            if (process.HasExited)
            {
                Logger.Error($"Device process exited prematurely: {deviceId}, ExitCode: {process.ExitCode}");
                return false;
            }

            // 检查进程是否有主窗口（表示已初始化）
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                Logger.Debug($"Device process has main window: {deviceId}");
                return true;
            }

            await Task.Delay(checkIntervalMs).ConfigureAwait(false);
            elapsedMs += checkIntervalMs;
        }

        // 如果进程还在运行但没有主窗口，认为是正常的（可能是后台进程）
        if (!process.HasExited)
        {
            Logger.Warn($"Device process started but no main window detected within {maxWaitMs}ms: {deviceId}");
            return true;
        }

        return false;
    }

    public async Task StopDeviceProcessAsync(string deviceId)
    {
        await _processLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_processes.TryRemove(deviceId, out var process))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    
                    Logger.Info($"Device process stopped: {deviceId}, ExitCode: {process.ExitCode}");
                    OnProcessStatusChanged(deviceId, false, process.ExitCode, null);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error stopping device process {deviceId}: {ex.Message}", ex);
                }
                finally
                {
                    process.Dispose();
                }
            }
            else
            {
                Logger.Warn($"Device process not found: {deviceId}");
            }
        }
        finally
        {
            _processLock.Release();
        }
    }

    public async Task StopAllProcessesAsync()
    {
        var deviceIds = _processes.Keys.ToList();
        
        foreach (var deviceId in deviceIds)
        {
            await StopDeviceProcessAsync(deviceId).ConfigureAwait(false);
        }
    }

    public bool IsProcessRunning(string deviceId)
    {
        if (_processes.TryGetValue(deviceId, out var process))
        {
            return !process.HasExited;
        }
        return false;
    }

    private void Process_Exited(Process? process, string deviceId)
    {
        if (process != null)
        {
            Logger.Warn($"Device process exited: {deviceId}, ExitCode: {process.ExitCode}");
            
            _processes.TryRemove(deviceId, out _);
            
            OnProcessStatusChanged(deviceId, false, process.ExitCode, null);

            if (process.ExitCode != 0)
            {
                Logger.Info($"Attempting to restart device process: {deviceId}");
                Task.Run(async () =>
                {
                    await Task.Delay(3000).ConfigureAwait(false);
                    RestartProcess(deviceId);
                });
            }
        }
    }

    private async void RestartProcess(string deviceId)
    {
        try
        {
            var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
            if (!Directory.Exists(configDir))
            {
                configDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Config"));
            }

            if (Directory.Exists(configDir))
            {
                var config = ConfigurationLoader.LoadConfiguration(configDir);
                var deviceConfig = config.Devices.FirstOrDefault(d => d.Id == deviceId);
                
                if (deviceConfig != null && deviceConfig.Enabled)
                {
                    await StartDeviceProcessAsync(deviceConfig).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to restart device process {deviceId}: {ex.Message}", ex);
        }
    }

    private void Process_OutputReceived(string deviceId, string? data)
    {
        if (!string.IsNullOrEmpty(data))
        {
            Logger.Debug($"[{deviceId}] Output: {data}");
        }
    }

    private void Process_ErrorReceived(string deviceId, string? data)
    {
        if (!string.IsNullOrEmpty(data))
        {
            Logger.Error($"[{deviceId}] Error: {data}");
        }
    }

    private void OnProcessStatusChanged(string deviceId, bool isRunning, int exitCode, string? errorMessage)
    {
        ProcessStatusChanged?.Invoke(this, new ProcessStatusChangedEventArgs
        {
            DeviceId = deviceId,
            IsRunning = isRunning,
            ExitCode = exitCode,
            ErrorMessage = errorMessage
        });
    }
}
