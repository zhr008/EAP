using System;
using System.Windows.Forms;
using EAP.Client;
using EAP.Core;
using log4net;
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
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();
            
            // 初始化设备日志系统（创建设备日志目录）
            EAP.Core.DeviceLogger.Initialize(configuration);
            
            var logger = log4net.LogManager.GetLogger(typeof(Program));
            logger.Info("Application starting...");

            var deviceManager = new DeviceManager();
            
            Application.Run(new MainForm(deviceManager));
        }
        catch (Exception ex)
        {
            log4net.LogManager.GetLogger(typeof(Program)).Error($"Application startup failed: {ex.Message}", ex);
            MessageBox.Show($"应用程序启动失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
