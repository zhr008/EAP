using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Cim.DeviceConnector.SecsGem;
using Cim.Core.Models;
using log4net;
using System.Reflection;
using System.IO;

namespace Cim.AgentUI;

public partial class MainWindow : Window
{
    private static readonly ILog _log = LogManager.GetLogger(typeof(MainWindow));
    private HsmsConnection? _connection;
    private int _messageCount;
    private string _logFilePath;

    public MainWindow()
    {
        InitializeComponent();
        InitializeLog4Net();
        _log.Info("CIM Agent UI Started");
        UpdateStatusMessage("Ready - Click Connect to start");
    }

    private void InitializeLog4Net()
    {
        var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
        
        // 配置 log4net
        var logConfig = @"
<log4net>
    <appender name=""FileAppender"" type=""log4net.Appender.FileAppender"">
        <file value=""logs\agent-log.txt"" />
        <appendToFile value=""true"" />
        <rollingStyle value=""Composite"" />
        <datePattern value=""yyyyMMdd"" />
        <maxSizeRollBackups value=""10"" />
        <maximumFileSize value=""10MB"" />
        <staticLogFileName value=""false"" />
        <layout type=""log4net.Layout.PatternLayout"">
            <conversionPattern value=""%date [%thread] %-5level %logger - %message%newline"" />
        </layout>
    </appender>
    <root>
        <level value=""INFO"" />
        <appender-ref ref=""FileAppender"" />
    </root>
</log4net>";

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(logConfig));
        log4net.Config.XmlConfigurator.Configure(logRepository, stream);

        // 设置日志文件路径
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _logFilePath = Path.Combine(baseDir, "logs", "agent-log.txt");
        
        // 确保日志目录存在
        Directory.CreateDirectory(Path.Combine(baseDir, "logs"));
    }

    private void AppendLog(string message, LogType type = LogType.Info)
    {
        Dispatcher.Invoke(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var color = type switch
            {
                LogType.Info => Brushes.Black,
                LogType.Send => Brushes.Blue,
                LogType.Receive => Brushes.Green,
                LogType.Error => Brushes.Red,
                LogType.Warning => Brushes.Orange,
                _ => Brushes.Black
            };

            var logEntry = $"[{timestamp}] {message}\n";
            
            var textRange = new TextRange(TxtLog.Document.ContentEnd, TxtLog.Document.ContentEnd);
            textRange.Text = logEntry;
            textRange.ApplyPropertyValue(TextElement.ForegroundProperty, color);

            _messageCount++;
            TxtMessageCount.Text = $"Messages: {_messageCount}";

            // 自动滚动到底部
            TxtLog.ScrollToEnd();
        });

        // 记录到 log4net
        switch (type)
        {
            case LogType.Error:
                _log.Error(message);
                break;
            case LogType.Warning:
                _log.Warn(message);
                break;
            default:
                _log.Info(message);
                break;
        }
    }

    private void UpdateStatusMessage(string message)
    {
        Dispatcher.Invoke(() => TxtStatusMessage.Text = message);
    }

    private void UpdateDeviceState(string connectionState, string equipmentState)
    {
        Dispatcher.Invoke(() =>
        {
            TxtConnectionState.Text = connectionState;
            TxtEquipmentState.Text = equipmentState;
            TxtLastUpdate.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 根据状态改变颜色
            TxtConnectionState.Background = connectionState switch
            {
                "Connected" => Brushes.LightGreen,
                "Connecting" => Brushes.Yellow,
                _ => new SolidColorBrush(Color.FromRgb(255, 224, 224))
            };

            TxtEquipmentState.Background = equipmentState switch
            {
                "ONLINE" or "RUNNING" => Brushes.LightGreen,
                "OFFLINE" => new SolidColorBrush(Color.FromRgb(255, 224, 224)),
                "FAULT" => Brushes.LightCoral,
                _ => Brushes.White
            };
        });
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateStatusMessage("Connecting...");
            AppendLog("Initiating HSMS connection...", LogType.Info);

            // 创建 HSMS 连接
            _connection = new HsmsConnection(
                host: "127.0.0.1",
                port: 5000,
                deviceId: TxtDeviceId.Text,
                onConnected: () => 
                {
                    AppendLog("HSMS Connection established successfully", LogType.Info);
                    UpdateDeviceState("Connected", "ONLINE");
                    UpdateStatusMessage("Connected");
                    EnableControlButtons(true);
                },
                onDisconnected: () => 
                {
                    AppendLog("HSMS Connection lost", LogType.Warning);
                    UpdateDeviceState("Disconnected", "OFFLINE");
                    UpdateStatusMessage("Disconnected");
                    EnableControlButtons(false);
                },
                onMessageSent: msg => AppendLog($"SEND >> {msg}", LogType.Send),
                onMessageReceived: msg => AppendLog($"RECV << {msg}", LogType.Receive),
                onError: ex => 
                {
                    AppendLog($"Error: {ex.Message}", LogType.Error);
                    UpdateStatusMessage($"Error: {ex.Message}");
                }
            );

            await _connection.ConnectAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"Connection failed: {ex.Message}", LogType.Error);
            UpdateStatusMessage($"Connection failed: {ex.Message}");
            EnableControlButtons(false);
        }
    }

    private async void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_connection != null)
            {
                AppendLog("Disconnecting...", LogType.Info);
                await _connection.DisconnectAsync();
                _connection = null;
                UpdateDeviceState("Disconnected", "OFFLINE");
                UpdateStatusMessage("Disconnected");
                EnableControlButtons(false);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Disconnect error: {ex.Message}", LogType.Error);
        }
    }

    private async void BtnGetStatus_Click(object sender, RoutedEventArgs e)
    {
        if (_connection == null) return;

        try
        {
            AppendLog("Sending S1F13 (Establish Communication Request)...", LogType.Send);
            var response = await _connection.SendStatusRequestAsync();
            AppendLog($"S1F13 Response: {response}", LogType.Receive);
            UpdateStatusMessage("Status request completed");
        }
        catch (Exception ex)
        {
            AppendLog($"Status request failed: {ex.Message}", LogType.Error);
        }
    }

    private async void BtnCollectData_Click(object sender, RoutedEventArgs e)
    {
        if (_connection == null) return;

        try
        {
            AppendLog("Sending S2F13 (Establish Data Collection Request)...", LogType.Send);
            var data = await _connection.CollectDataAsync(new[] { 1, 2, 3, 4 });
            AppendLog($"S2F13 Response: {data}", LogType.Receive);
            UpdateStatusMessage("Data collection completed");
        }
        catch (Exception ex)
        {
            AppendLog($"Data collection failed: {ex.Message}", LogType.Error);
        }
    }

    private async void BtnUploadRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (_connection == null) return;

        try
        {
            var recipeName = "RECIPE-001";
            AppendLog($"Sending S7F1 (Recipe Upload Request) for {recipeName}...", LogType.Send);
            var recipe = await _connection.UploadRecipeAsync(recipeName);
            AppendLog($"S7F1 Response: Recipe '{recipeName}' uploaded ({recipe.Length} bytes)", LogType.Receive);
            UpdateStatusMessage($"Recipe '{recipeName}' uploaded successfully");
        }
        catch (Exception ex)
        {
            AppendLog($"Recipe upload failed: {ex.Message}", LogType.Error);
        }
    }

    private async void BtnDownloadRecipe_Click(object sender, RoutedEventArgs e)
    {
        if (_connection == null) return;

        try
        {
            var recipeName = "RECIPE-001";
            var recipeData = System.Text.Encoding.UTF8.GetBytes("RECIPE_CONTENT_PLACEHOLDER");
            AppendLog($"Sending S7F5 (Recipe Download Request) for {recipeName}...", LogType.Send);
            var result = await _connection.DownloadRecipeAsync(recipeName, recipeData);
            AppendLog($"S7F5 Response: {result}", LogType.Receive);
            UpdateStatusMessage($"Recipe '{recipeName}' downloaded successfully");
        }
        catch (Exception ex)
        {
            AppendLog($"Recipe download failed: {ex.Message}", LogType.Error);
        }
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        TxtLog.Clear();
        _messageCount = 0;
        TxtMessageCount.Text = "Messages: 0";
        AppendLog("Log cleared", LogType.Info);
    }

    private void EnableControlButtons(bool enabled)
    {
        Dispatcher.Invoke(() =>
        {
            BtnConnect.IsEnabled = !enabled;
            BtnDisconnect.IsEnabled = enabled;
            BtnGetStatus.IsEnabled = enabled;
            BtnCollectData.IsEnabled = enabled;
            BtnUploadRecipe.IsEnabled = enabled;
            BtnDownloadRecipe.IsEnabled = enabled;
        });
    }
}

public enum LogType
{
    Info,
    Send,
    Receive,
    Error,
    Warning
}
