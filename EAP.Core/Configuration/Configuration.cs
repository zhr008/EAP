using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

using log4net;
using Microsoft.Extensions.Configuration;

namespace EAP.Core;

public class EAPConfiguration
{
    public List<DeviceConfig> Devices { get; set; } = [];
    public int GlobalUpdateRate { get; set; } = 1000;
}

public static class ConfigurationLoader
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(ConfigurationLoader));
    private static EAPConfiguration? _cachedConfig;
    private static readonly object _lockObj = new();

    public static EAPConfiguration GetConfiguration()
    {
        lock (_lockObj)
        {
            if (_cachedConfig == null)
            {
                _cachedConfig = LoadConfiguration();
            }
            return _cachedConfig;
        }
    }

    public static void Refresh()
    {
        lock (_lockObj)
        {
            _cachedConfig = null;
        }
    }

    private static EAPConfiguration LoadConfiguration()
    {
        var configuration = new EAPConfiguration();

        try
        {
            var configDirectory = GetConfigDirectory();
            
            if (!Directory.Exists(configDirectory))
            {
                Logger.Warn($"Config directory not found: {configDirectory}");
                return configuration;
            }

            var deviceFolders = Directory.GetDirectories(configDirectory);

            foreach (var folder in deviceFolders)
            {
                var configFiles = Directory.GetFiles(folder, "*.config");
                
                if (configFiles.Length == 0)
                {
                    Logger.Debug($"No config file found in folder: {folder}");
                    continue;
                }
                
                if (configFiles.Length > 1)
                {
                    Logger.Error($"Error: Multiple config files found in folder {folder}: {configFiles.Length} files. Only one config file is allowed.");
                    continue;
                }

                var configFile = configFiles[0];

                if (File.Exists(configFile))
                {
                    try
                    {
                        var Dev = LoadDeviceConfig(configFile);
                        if (Dev != null)
                        {
                            Dev.FolderName = Path.GetFileName(folder);
                            Dev.FilePath = configFile;
                            configuration.Devices.Add(Dev);
                            Logger.Info($"Loaded device config: {Dev.DeviceId} ({Dev.DeviceName}) from {configFile}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error loading config file {configFile}: {ex.Message}", ex);
                    }
                }
            }

            Logger.Info($"Loaded {configuration.Devices.Count} device configurations");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error loading configurations: {ex.Message}", ex);
        }

        return configuration;
    }

    private static DeviceConfig? LoadDeviceConfig(string configFile)
    {
        using var reader = new StreamReader(configFile);
        var serializer = new XmlSerializer(typeof(DeviceConfig));
        return serializer.Deserialize(reader) as DeviceConfig;
    }

    private static string GetConfigDirectory()
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: true);
        
        var configuration = builder.Build();
        var configDirFromSettings = configuration["AppSettings:ConfigDirectory"];
        
        if (!string.IsNullOrEmpty(configDirFromSettings))
        {
            var configPath = Path.IsPathRooted(configDirFromSettings) 
                ? configDirFromSettings 
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configDirFromSettings);
            
            var fullPath = Path.GetFullPath(configPath);
            
            if (!Directory.Exists(fullPath))
            {
                Logger.Info($"Creating config directory: {fullPath}");
                Directory.CreateDirectory(fullPath);
            }
            
            return fullPath;
        }
        
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Config")
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        var defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
        Directory.CreateDirectory(defaultPath);
        return defaultPath;
    }

    public static void SaveDeviceConfig(DeviceConfig config, string configDirectory)
    {
        try
        {
            var deviceFolder = Path.Combine(configDirectory, config.DeviceId);
            Directory.CreateDirectory(deviceFolder);

            var configFile = Path.Combine(deviceFolder, "device.config");
            using var writer = new StreamWriter(configFile);
            var serializer = new XmlSerializer(typeof(DeviceConfig));
            serializer.Serialize(writer, config);

            Logger.Info($"Saved device config: {configFile}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error saving device config: {ex.Message}", ex);
            throw;
        }
    }
}