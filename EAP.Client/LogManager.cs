using System;
using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Configuration;

namespace EAP.Client;

public static class Log
{
    private static ILog? _mainLogger;
    private static ILog? _deviceLogger;
    private static string _deviceLogDirectory = string.Empty;
    private static string _mainLogDirectory = string.Empty;

    public static void Initialize(IConfiguration configuration)
    {
        // 初始化log4net
        var logConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config");
        if (File.Exists(logConfigPath))
        {
            var logRepository = log4net.LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo(logConfigPath));
        }
        
        _mainLogger = log4net.LogManager.GetLogger("Main");
        _deviceLogger = log4net.LogManager.GetLogger("Device");

        // 设置主程序日志目录
        _mainLogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        EnsureLogDirectories(_mainLogDirectory);

        // 设置设备日志目录
        _deviceLogDirectory = configuration["AppSettings:DeviceLogDirectory"] ?? 
                             Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Devices");
        
        // 根据ConfigDirectory路径下的文件夹名创建设备日志目录
        var configDirectory = configuration["AppSettings:ConfigDirectory"] ?? 
                             Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        
        if (Directory.Exists(configDirectory))
        {
            CreateDeviceLogDirectoriesFromConfig(configDirectory);
        }
    }

    private static void EnsureLogDirectories(string baseDir)
    {
        Directory.CreateDirectory(Path.Combine(baseDir, "info"));
        Directory.CreateDirectory(Path.Combine(baseDir, "warn"));
        Directory.CreateDirectory(Path.Combine(baseDir, "error"));
    }

    private static void CreateDeviceLogDirectoriesFromConfig(string configDirectory)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(configDirectory))
            {
                var deviceId = Path.GetFileName(dir);
                EnsureLogDirectories(Path.Combine(_deviceLogDirectory, deviceId));
            }
        }
        catch (Exception ex)
        {
            _mainLogger?.Warn($"Failed to create device log directories from config: {ex.Message}");
        }
    }

    // 主程序日志
    public static void Info(string message)
    {
        _mainLogger?.Info(message);
        WriteToFile(_mainLogDirectory, "info", message);
    }

    public static void Warn(string message)
    {
        _mainLogger?.Warn(message);
        WriteToFile(_mainLogDirectory, "warn", message);
    }

    public static void Error(string message, Exception? ex = null)
    {
        _mainLogger?.Error(message, ex);
        WriteToFile(_mainLogDirectory, "error", message, ex);
    }

    // 设备日志
    public static void Info(string deviceId, string message)
    {
        var formattedMessage = $"[{deviceId}] {message}";
        _deviceLogger?.Info(formattedMessage);
        WriteToFile(Path.Combine(_deviceLogDirectory, deviceId), "info", formattedMessage);
    }

    public static void Warn(string deviceId, string message)
    {
        var formattedMessage = $"[{deviceId}] {message}";
        _deviceLogger?.Warn(formattedMessage);
        WriteToFile(Path.Combine(_deviceLogDirectory, deviceId), "warn", formattedMessage);
    }

    public static void Error(string deviceId, string message, Exception? ex = null)
    {
        var formattedMessage = $"[{deviceId}] {message}";
        _deviceLogger?.Error(formattedMessage, ex);
        WriteToFile(Path.Combine(_deviceLogDirectory, deviceId), "error", formattedMessage, ex);
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

            File.AppendAllText(filePath, logMessage + Environment.NewLine);
        }
        catch (Exception writeEx)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write log: {writeEx.Message}");
        }
    }
}