using System;
using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Configuration;

namespace EAP.Core;

public static class DeviceLogger
{
    private static string _deviceLogDirectory = string.Empty;

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
        var deviceLogPath = Path.Combine(_deviceLogDirectory, deviceId);
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
            System.Diagnostics.Debug.WriteLine($"Failed to create device log directories from config: {ex.Message}");
        }
    }

    public static void Debug(string deviceId, string message)
    {
        WriteToFile(Path.Combine(_deviceLogDirectory, deviceId), "debug", message);
    }

    public static void Info(string deviceId, string message)
    {
        WriteToFile(Path.Combine(_deviceLogDirectory, deviceId), "info", message);
    }

    public static void Warn(string deviceId, string message)
    {
        WriteToFile(Path.Combine(_deviceLogDirectory, deviceId), "warn", message);
    }

    public static void Error(string deviceId, string message, Exception? ex = null)
    {
        WriteToFile(Path.Combine(_deviceLogDirectory, deviceId), "error", message, ex);
    }

    private static void WriteToFile(string baseDir, string level, string message, Exception? ex = null)
    {
        try
        {
            var logDir = Path.Combine(baseDir, level);
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            var fileName = $"{DateTime.Now:yyyyMMdd}.log";
            var filePath = Path.Combine(logDir, fileName);

            var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] [{level.ToUpper()}] {message}";
            if (ex != null)
            {
                logMessage += $"\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}";
            }

            File.AppendAllText(filePath, logMessage + Environment.NewLine, System.Text.Encoding.UTF8);
        }
        catch (Exception writeEx)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write log: {writeEx.Message}");
        }
    }
}
