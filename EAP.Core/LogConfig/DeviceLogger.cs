using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace EAP.Core;

/// <summary>
/// 设备日志管理器
/// 独立于log4net，专门用于按设备分目录存储设备交互日志
/// 目录结构：{DeviceLogDirectory}/{deviceId}/{level}/yyyyMMdd.log
/// </summary>
public static class DeviceLogger
{
    private static string _deviceLogDirectory = string.Empty;
    private static readonly ConcurrentDictionary<string, object> _fileLocks = new();
    private static readonly char[] _invalidChars = Path.GetInvalidFileNameChars();

    public static void Initialize(IConfiguration configuration)
    {
        _deviceLogDirectory = configuration["AppSettings:DeviceLogDirectory"] ??
                             Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        var configDirectory = configuration["AppSettings:ConfigDirectory"] ??
                             Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");

        if (Directory.Exists(configDirectory))
        {
            CreateDeviceLogDirectoriesFromConfig(configDirectory);
        }
    }

    public static void EnsureDeviceLogDirectory(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;

        var deviceLogPath = Path.Combine(_deviceLogDirectory, SanitizeDeviceId(deviceId));
        EnsureLogDirectories(deviceLogPath);
    }

    private static void EnsureLogDirectories(string baseDir)
    {
        Directory.CreateDirectory(Path.Combine(baseDir, "info"));
        Directory.CreateDirectory(Path.Combine(baseDir, "warn"));
        Directory.CreateDirectory(Path.Combine(baseDir, "error"));
        Directory.CreateDirectory(Path.Combine(baseDir, "debug"));
    }

    private static void CreateDeviceLogDirectoriesFromConfig(string configDirectory)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(configDirectory))
            {
                var deviceId = Path.GetFileName(dir);
                EnsureDeviceLogDirectory(deviceId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"创建设备日志目录失败: {ex.Message}");
        }
    }

    public static void Debug(string deviceId, string message)
    {
        WriteLog(deviceId, "DEBUG", message, null);
    }

    public static void Info(string deviceId, string message)
    {
        WriteLog(deviceId, "INFO", message, null);
    }

    public static void Warn(string deviceId, string message)
    {
        WriteLog(deviceId, "WARN", message, null);
    }

    public static void Warn(string deviceId, string message, Exception ex)
    {
        WriteLog(deviceId, "WARN", message, ex);
    }

    public static void Error(string deviceId, string message)
    {
        WriteLog(deviceId, "ERROR", message, null);
    }

    public static void Error(string deviceId, string message, Exception? ex)
    {
        WriteLog(deviceId, "ERROR", message, ex);
    }

    private static void WriteLog(string deviceId, string level, string message, Exception? ex)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return;

            var safeDeviceId = SanitizeDeviceId(deviceId);
            var baseDir = Path.Combine(_deviceLogDirectory, safeDeviceId, level.ToLowerInvariant());

            if (!Directory.Exists(baseDir))
                Directory.CreateDirectory(baseDir);

            var fileName = $"{DateTime.Now:yyyyMMdd}.log";
            var filePath = Path.Combine(baseDir, fileName);

            var sb = new StringBuilder();
            sb.AppendFormat("[{0:HH:mm:ss.fff}] [{1}] {2}", DateTime.Now, level, message);

            if (ex != null)
            {
                sb.AppendLine();
                sb.AppendFormat("  异常类型: {0}", ex.GetType().Name);
                sb.AppendLine();
                sb.AppendFormat("  异常消息: {0}", ex.Message);
                sb.AppendLine();
                if (ex.InnerException != null)
                {
                    sb.AppendFormat("  内部异常: {0} - {1}", ex.InnerException.GetType().Name, ex.InnerException.Message);
                    sb.AppendLine();
                }
                sb.AppendFormat("  堆栈跟踪: {0}", ex.StackTrace);
            }

            var logMessage = sb.ToString();
            var fileLock = _fileLocks.GetOrAdd(filePath, _ => new object());

            lock (fileLock)
            {
                File.AppendAllText(filePath, logMessage + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception writeEx)
        {
            System.Diagnostics.Debug.WriteLine($"写入设备日志失败 [{deviceId}]: {writeEx.Message}");
        }
    }

    private static string SanitizeDeviceId(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return "Unknown";

        var result = new char[deviceId.Length];
        for (int i = 0; i < deviceId.Length; i++)
        {
            result[i] = Array.IndexOf(_invalidChars, deviceId[i]) >= 0 ? '_' : deviceId[i];
        }
        return new string(result);
    }
}
