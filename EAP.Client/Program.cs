using System;
using System.IO;
using System.Windows.Forms;
using EAP.Core.Configuration;
using EAP.Services;
using Microsoft.Extensions.Configuration;

namespace EAP.Client;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            var configuration = LoadAppSettings();
            Log.Initialize(configuration);
            Log.Info("Application starting...");
            
            var configDirectory = GetConfigDirectory(configuration);
            Log.Info($"Using config directory: {configDirectory}");
            
            var deviceAgentPath = GetDeviceAgentPath(configuration);
            Log.Info($"Using device agent path: {deviceAgentPath}");
            
            var enableMultiProcess = configuration.GetValue<bool>("AppSettings:EnableMultiProcess", true);

            var eapConfig = ConfigurationLoader.LoadConfiguration(configDirectory);
            Log.Info($"Loaded {eapConfig.Devices.Count} device configurations");

            var deviceManager = new DeviceManager(eapConfig, deviceAgentPath, enableMultiProcess);
            Application.Run(new MainForm(deviceManager, configDirectory));
        }
        catch (Exception ex)
        {
            Log.Error($"Application startup failed: {ex.Message}", ex);
            MessageBox.Show($"应用程序启动失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static IConfiguration LoadAppSettings()
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), optional: true, reloadOnChange: true);
        
        return builder.Build();
    }

    private static string GetConfigDirectory(IConfiguration configuration)
    {
        var configDirFromSettings = configuration["AppSettings:ConfigDirectory"];
        
        if (!string.IsNullOrEmpty(configDirFromSettings))
        {
            var configPath = Path.IsPathRooted(configDirFromSettings) 
                ? configDirFromSettings 
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configDirFromSettings);
            
            var fullPath = Path.GetFullPath(configPath);
            
            if (!Directory.Exists(fullPath))
            {
                Log.Info("Main", $"Creating config directory: {fullPath}");
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

    private static string GetDeviceAgentPath(IConfiguration configuration)
    {
        var agentPathFromSettings = configuration["AppSettings:DeviceAgentPath"];
        
        if (!string.IsNullOrEmpty(agentPathFromSettings))
        {
            return Path.IsPathRooted(agentPathFromSettings) 
                ? agentPathFromSettings 
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, agentPathFromSettings);
        }

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EAP.DeviceAgent.exe");
    }
}