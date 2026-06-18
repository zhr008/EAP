using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AntdUI;
using EAP.Core.Configuration;
using EAP.Core.Protocol;
using EAP.Services;

namespace EAP.Client;

public partial class MainForm : Form
    {
        private readonly IDeviceManager _deviceManager;
        private readonly Dictionary<string, DeviceInfo> _deviceCards = new();
        private string _configDirectory;

        private Ribbon _ribbon;
        private System.Windows.Forms.Panel _contentPanel;
        private System.Windows.Forms.Label _statusBar;
        private System.Windows.Forms.Label _statTotal;
        private System.Windows.Forms.Label _statOnline;
        private System.Windows.Forms.Label _statOffline;

        public MainForm(IDeviceManager deviceManager, string configDirectory)
        {
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _configDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
            InitializeComponent();
            SubscribeToEvents();
            InitializeConfigDirectory();
        }

    private void SubscribeToEvents()
    {
        _deviceManager.ConnectionStatusChanged += DeviceManager_ConnectionStatusChanged;
    }

    private void InitializeConfigDirectory()
        {
            _statusBar.Text = $"配置目录: {_configDirectory}";
            _ = LoadDevicesInfoAsync();
        }

    private async Task LoadDevicesInfoAsync()
    {
        try
        {
            _statusBar.Text = "正在加载设备配置...";

            var eapConfig = ConfigurationLoader.LoadConfiguration(_configDirectory);

            ClearDeviceCards();

            var loadTasks = new List<Task>();
            
            foreach (var deviceConfig in eapConfig.Devices)
            {
                loadTasks.Add(LoadDeviceCardAsync(deviceConfig));
            }

            await Task.WhenAll(loadTasks).ConfigureAwait(false);

            ArrangeDeviceCards();
            UpdateStatusBar();
            _statusBar.Text = $"就绪 - {eapConfig.Devices.Count} 台设备";
            
            Log.Info($"Loaded {eapConfig.Devices.Count} device cards");
        }
        catch (Exception ex)
        {
            Log.Error($"加载设备配置失败: {ex.Message}", ex);
            _statusBar.Text = "加载失败";
        }
    }

    private async Task LoadDeviceCardAsync(DeviceConfig deviceConfig)
    {
        try
        {
            var card = new DeviceInfo(deviceConfig, _deviceManager, _contentPanel);
            card.ReturnedToMainForm += Card_ReturnedToMainForm;
            
            _deviceCards[deviceConfig.Id] = card;
            
            // 先显示卡片，再异步连接
            card.Show();
            
            if (deviceConfig.Enabled)
            {
                // 后台异步连接，不阻塞UI
                _ = ConnectDeviceAsyncWithRetry(deviceConfig.Id);
            }

            Log.Info($"Device card loaded: {deviceConfig.Id}");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load device card {deviceConfig.Id}: {ex.Message}", ex);
        }
    }

    private async Task ConnectDeviceAsyncWithRetry(string deviceId)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 2000;
        
        Log.Info($"Starting connection for device: {deviceId}");
        
        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                Log.Info($"Attempt {retry + 1} to connect device: {deviceId}");
                bool success = await _deviceManager.ConnectDeviceAsync(deviceId).ConfigureAwait(false);
                
                if (success)
                {
                    Log.Info($"Device {deviceId} connected successfully");
                    return;
                }
                else
                {
                    Log.Warn($"Device {deviceId} connection attempt {retry + 1} returned false (no exception)");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Device {deviceId} connection attempt {retry + 1} failed: {ex.Message}", ex);
                Log.Error($"Exception type: {ex.GetType().FullName}");
                if (ex.InnerException != null)
                {
                    Log.Error($"Inner exception: {ex.InnerException.Message}");
                }
            }
            
            if (retry < maxRetries - 1)
            {
                Log.Info($"Waiting {retryDelayMs}ms before retry for device: {deviceId}");
                await Task.Delay(retryDelayMs).ConfigureAwait(false);
            }
        }
        
        Log.Error($"Device {deviceId} failed to connect after {maxRetries} attempts");
    }

    private void ClearDeviceCards()
    {
        foreach (var card in _deviceCards.Values)
        {
            try
            {
                card.ReturnedToMainForm -= Card_ReturnedToMainForm;
                card.Close();
                card.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error($"Error disposing device card: {ex.Message}", ex);
            }
        }
        _deviceCards.Clear();
    }

    private void Card_ReturnedToMainForm(object? sender, EventArgs e)
    {
        UpdateStatusBar();
        ArrangeDeviceCards();
    }

    private void ArrangeDeviceCards()
    {
        if (_deviceCards.Count == 0 || _contentPanel == null)
            return;

        var padding = 15;
        var cardWidth = 360;
        var cardHeight = 180;
        var columns = Math.Max(1, _contentPanel.ClientSize.Width / (cardWidth + padding));
        
        int row = 0, col = 0;

        foreach (var card in _deviceCards.Values)
        {
            var x = padding + col * (cardWidth + padding);
            var y = padding + row * (cardHeight + padding);
            
            // 使用父控件坐标设置卡片位置
            card.Location = new Point(x, y);
            
            col++;
            if (col >= columns)
            {
                col = 0;
                row++;
            }
        }
    }

    private void UpdateStatusBar()
    {
        var total = _deviceCards.Count;
        var online = _deviceCards.Values.Count(c => c.IsConnected);

        _statTotal.Text = $"设备: {total}";
        _statOnline.Text = $"在线: {online}";
        _statOffline.Text = $"离线: {total - online}";
    }

    private void DeviceManager_ConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
    {
        if (_deviceCards.TryGetValue(e.ConnectionId, out var card))
        {
            card.UpdateStatus(e.IsConnected);
            UpdateStatusBar();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        
        if (e.CloseReason == CloseReason.UserClosing)
        {
            // 使用非阻塞方式断开连接，避免UI线程死锁
            _ = DisconnectAndCleanupAsync();
        }
    }

    private async Task DisconnectAndCleanupAsync()
    {
        try
        {
            await _deviceManager.DisconnectAllAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error($"Error during shutdown: {ex.Message}", ex);
        }
        
        ClearDeviceCards();
    }

    #region 菜单事件

    private void SelectConfigDir_Click(object? sender, EventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _configDirectory = dialog.SelectedPath;
            ClearDeviceCards();
            _ = LoadDevicesInfoAsync();
        }
    }

    private async void ConnectAllBtn_Click(object? sender, EventArgs e)
    {
        _statusBar.Text = "正在连接所有设备...";
        
        var connectTasks = new List<Task>();
        
        foreach (var card in _deviceCards.Values)
        {
            connectTasks.Add(ConnectDeviceWithRetryAsync(card.DeviceConfig.Id));
        }
        
        await Task.WhenAll(connectTasks).ConfigureAwait(false);
        
        UpdateStatusBar();
        _statusBar.Text = "连接完成";
    }

    private async Task ConnectDeviceWithRetryAsync(string deviceId)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 2000;
        
        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                await _deviceManager.ConnectDeviceAsync(deviceId).ConfigureAwait(false);
                break;
            }
            catch (Exception ex)
            {
                Log.Error($"Connection attempt {retry + 1} failed: {ex.Message}");
                
                if (retry < maxRetries - 1)
                {
                    await Task.Delay(retryDelayMs).ConfigureAwait(false);
                }
            }
        }
    }

    private async void DisconnectAllBtn_Click(object? sender, EventArgs e)
    {
        _statusBar.Text = "正在断开所有设备...";
        
        await _deviceManager.DisconnectAllAsync().ConfigureAwait(false);
        
        UpdateStatusBar();
        _statusBar.Text = "断开完成";
    }

    private void RefreshBtn_Click(object? sender, EventArgs e)
    {
        ClearDeviceCards();
        _ = LoadDevicesInfoAsync();
    }

    private void ToggleDarkMode(object? sender, EventArgs e)
    {
        Config.IsDark = !Config.IsDark;
    }

    private void ShowAbout(object? sender, EventArgs e)
    {
        MessageBox.Show("EAP设备管理系统\n版本 1.0.0\n\n多进程隔离架构", "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    #endregion
}
