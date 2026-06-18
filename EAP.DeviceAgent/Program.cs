using System;
using System.IO;
using System.Threading.Tasks;
using log4net;
using log4net.Config;

namespace EAP.DeviceAgent;

class Program
{
    private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));

    static async Task Main(string[] args)
    {
        ConfigureLogging();
        
        try
        {
            Logger.Info("EAP Device Agent starting...");
            
            var agent = new DeviceAgent();
            await agent.RunAsync(args).ConfigureAwait(false);
            
            Logger.Info("EAP Device Agent exiting...");
        }
        catch (Exception ex)
        {
            // 如果日志尚未初始化，直接输出到控制台
            if (Logger != null)
            {
                Logger.Error($"Device agent fatal error: {ex.Message}", ex);
            }
            else
            {
                Console.WriteLine($"Device agent fatal error: {ex.Message}");
            }
            Environment.Exit(1);
        }
    }

    private static void ConfigureLogging()
    {
        // 尝试从多个位置加载 log4net 配置
        var possibleConfigPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "log4net.config"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "log4net.config")
        };

        foreach (var configPath in possibleConfigPaths)
        {
            var fullPath = Path.GetFullPath(configPath);
            if (File.Exists(fullPath))
            {
                var fileInfo = new FileInfo(fullPath);
                XmlConfigurator.ConfigureAndWatch(fileInfo);
                Console.WriteLine($"Using log4net config from: {fullPath}");
                return;
            }
        }

        // 如果找不到配置文件，使用默认配置
        BasicConfigurator.Configure();
        Console.WriteLine("log4net config not found, using basic configuration");
    }
}
