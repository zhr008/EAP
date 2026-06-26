using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using AntdUI;
using EAP.Core;
using EAP.Services;

namespace EAP.Client;

/// <summary>
/// 主窗体
/// 负责设备卡片的加载、布局、监控管理
/// </summary>
public partial class MainForm : Form
{
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(MainForm));

    #region 私有字段

    private readonly IDeviceManager _deviceManager;
    private readonly Dictionary<string, DeviceInfo> _deviceCards = new();
    private readonly object _deviceLock = new();

    // UI组件
    private Ribbon? _ribbon;
    private System.Windows.Forms.Panel? _contentPanel;
    private System.Windows.Forms.Label? _statusBar;
    private System.Windows.Forms.Label? _statTotal;
    private System.Windows.Forms.Label? _statOnline;
    private System.Windows.Forms.Label? _statOffline;

    // 设备监控定时器
    private System.Windows.Forms.Timer? _monitorTimer;
    private const int MonitorIntervalMs = 5000;

    #endregion

    #region 构造函数

    public MainForm(IDeviceManager deviceManager)
    {
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        InitializeComponent();
        InitializeMonitor();
    }

    #endregion

    #region 初始化

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _ = LoadDevicesAsync();
    }

    /// <summary>
    /// 加载设备列表
    /// </summary>
    private async Task LoadDevicesAsync()
    {
        try
        {
            _statusBar!.Text = "正在加载设备配置...";

            // 获取设备配置
            var devices = _deviceManager.GetDevices().ToList();

            // 清除现有卡片
            ClearAllCards();

            // 创建设备卡片（带延迟避免卡顿）
            foreach (var config in devices)
            {
                CreateCard(config);
                await Task.Delay(50);
            }

            ArrangeCards();
            UpdateStatusBar();

            // 连接所有设备
            _statusBar.Text = $"加载完成 - {devices.Count} 台设备，正在连接...";
            await _deviceManager.ConnectAllAsync();

            UpdateStatusBar();
            _statusBar.Text = $"就绪 - {devices.Count} 台设备";
        }
        catch (Exception ex)
        {
            Logger.Error($"加载设备失败: {ex.Message}", ex);
            _statusBar!.Text = "加载失败";
        }
    }

    #endregion

    #region 设备监控

    private void InitializeMonitor()
    {
        _monitorTimer = new System.Windows.Forms.Timer { Interval = MonitorIntervalMs };
        _monitorTimer.Tick += OnMonitorTick;
        _monitorTimer.Start();
        Logger.Info($"设备监控已启动，间隔: {MonitorIntervalMs}ms");
    }

    /// <summary>
    /// 监控定时器触发
    /// </summary>
    private async void OnMonitorTick(object? sender, EventArgs e)
    {
        _monitorTimer?.Stop();
        try
        {
            await SyncConfigurationAsync();
            await SyncConnectionStatusAsync();
        }
        catch (Exception ex)
        {
            Logger.Error($"设备监控异常: {ex.Message}", ex);
        }
        finally
        {
            _monitorTimer?.Start();
        }
    }

    /// <summary>
    /// 同步配置变化
    /// </summary>
    private async Task SyncConfigurationAsync()
    {
        lock (_deviceLock)
        {
            // 刷新配置
            ConfigurationLoader.Refresh();
            var currentDevices = _deviceManager.GetDevices().ToList();
            var currentIds = currentDevices.Select(d => d.DeviceId).ToHashSet();
            var existingIds = _deviceCards.Keys.ToHashSet();

            // 新增设备
            foreach (var id in currentIds.Except(existingIds))
            {
                var config = currentDevices.FirstOrDefault(d => d.DeviceId == id);
                if (config != null)
                {
                    Logger.Info($"检测到新增设备: {id}");
                    CreateCard(config);
                }
            }

            // 删除设备
            foreach (var id in existingIds.Except(currentIds))
            {
                Logger.Info($"检测到删除设备: {id}");
                RemoveCard(id);
            }

            // 更新现有设备配置
            foreach (var config in currentDevices.Where(c => _deviceCards.ContainsKey(c.DeviceId)))
            {
                _deviceCards[config.DeviceId].UpdateConfiguration(config);
            }

            if (existingIds.Except(currentIds).Any() || currentIds.Except(existingIds).Any())
            {
                ArrangeCards();
                UpdateStatusBar();
            }
        }

        await Task.Delay(100);
    }

    /// <summary>
    /// 同步连接状态
    /// 根据配置控制设备连接
    /// </summary>
    private async Task SyncConnectionStatusAsync()
    {
        foreach (var config in _deviceManager.GetDevices())
        {
            var id = config.DeviceId;
            var enabled = config.Enabled;
            var connected = _deviceManager.IsDeviceConnected(id);
            var heartbeat = _deviceManager.GetDeviceHeartbeatStatus(id);

            // 连接控制规则
            if (!enabled && connected)
            {
                // 规则1: 已禁用 → 断开
                Logger.Info($"设备已禁用，断开: {id}");
                await _deviceManager.DisconnectDeviceAsync(id);
            }
            else if (enabled && !connected)
            {
                // 规则2: 已启用但未连接 → 连接
                Logger.Info($"设备已启用，连接: {id}");
                await _deviceManager.ConnectDeviceAsync(id);
            }
            else if (enabled && connected && !heartbeat)
            {
                // 规则3: 心跳异常 → 重连
                Logger.Warn($"设备心跳异常，重连: {id}");
                await _deviceManager.DisconnectDeviceAsync(id);
                await Task.Delay(1000);
                await _deviceManager.ConnectDeviceAsync(id);
            }

            await Task.Delay(50);
        }
    }

    #endregion

    #region 设备卡片管理

    private void CreateCard(DeviceConfig config)
    {
        var card = new DeviceInfo(config, _deviceManager, _contentPanel);
        card.ReturnedToMainForm += OnCardReturned;
        card.StatusChanged += OnCardStatusChanged;

        _deviceCards[config.DeviceId] = card;
        _contentPanel!.Controls.Add(card);
        card.Show();

        Logger.Info($"创建设备卡片: {config.DeviceId}");
    }

    private void RemoveCard(string deviceId)
    {
        if (!_deviceCards.TryGetValue(deviceId, out var card)) return;

        lock (_deviceLock)
        {
            _ = _deviceManager.DisconnectDeviceAsync(deviceId);
            card.ReturnedToMainForm -= OnCardReturned;
            card.StatusChanged -= OnCardStatusChanged;
            _contentPanel!.Controls.Remove(card);
            card.Dispose();
            _deviceCards.Remove(deviceId);
        }

        Logger.Info($"移除设备卡片: {deviceId}");
    }

    private void ClearAllCards()
    {
        foreach (var card in _deviceCards.Values)
        {
            card.ReturnedToMainForm -= OnCardReturned;
            card.StatusChanged -= OnCardStatusChanged;
            _contentPanel!.Controls.Remove(card);
            card.Dispose();
        }
        _deviceCards.Clear();
    }

    private void OnCardReturned(object? sender, EventArgs e)
    {
        if (sender is not DeviceInfo card || _contentPanel!.Controls.Contains(card)) return;

        _contentPanel.Controls.Add(card);
        card.Show();
        ArrangeCards();
        UpdateStatusBar();
    }

    private void OnCardStatusChanged(object? sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnCardStatusChanged(sender, e));
            return;
        }

        UpdateStatusBar();
    }

    private void ArrangeCards()
    {
        if (_deviceCards.Count == 0 || _contentPanel == null) return;

        const int padding = 15;
        const int cardWidth = 360;
        const int cardHeight = 140;

        int col = 0, row = 0;
        int columns = Math.Max(1, _contentPanel.ClientSize.Width / (cardWidth + padding));

        foreach (var card in _deviceCards.Values)
        {
            card.Location = new Point(padding + col * (cardWidth + padding), padding + row * (cardHeight + padding));
            card.Size = new Size(cardWidth, cardHeight);

            if (++col >= columns)
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

        _statTotal!.Text = $"设备: {total}";
        _statOnline!.Text = $"在线: {online}";
        _statOffline!.Text = $"离线: {total - online}";
    }

    #endregion

    #region 事件处理
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason != CloseReason.UserClosing)
        {
            base.OnFormClosing(e);
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                await _deviceManager.DisconnectAllAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                Logger.Error($"关闭时断开连接失败: {ex.Message}", ex);
            }
            finally
            {
                ClearAllCards();
            }
        });

        base.OnFormClosing(e);
    }

    #endregion

    #region 菜单事件

    private async void OnConnectAll(object? sender, EventArgs e)
    {
        _statusBar!.Text = "正在连接所有设备...";

        var tasks = _deviceCards.Values.Select(c => ConnectWithRetryAsync(c.DeviceConfig.DeviceId));
        await Task.WhenAll(tasks);

        UpdateStatusBar();
        _statusBar.Text = "连接完成";
    }

    private async Task ConnectWithRetryAsync(string deviceId, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await _deviceManager.ConnectDeviceAsync(deviceId);
                return;
            }
            catch (Exception ex)
            {
                Logger.Error($"连接失败 (尝试 {i + 1}): {ex.Message}");
                if (i < maxRetries - 1) await Task.Delay(2000);
            }
        }
    }

    private async void OnDisconnectAll(object? sender, EventArgs e)
    {
        _statusBar!.Text = "正在断开所有设备...";
        await _deviceManager.DisconnectAllAsync();
        UpdateStatusBar();
        _statusBar.Text = "断开完成";
    }

    private async void OnRefresh(object? sender, EventArgs e)
    {
        _statusBar!.Text = "正在重新加载...";
        await _deviceManager.ReloadConfigurationAsync(string.Empty);
        await LoadDevicesAsync();
    }

    private void OnToggleDark(object? sender, EventArgs e)
    {
        Config.IsDark = !Config.IsDark;
    }

    private void OnShowAbout(object? sender, EventArgs e)
    {
        MessageBox.Show("EAP设备管理系统\n版本 1.0.0", "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    #endregion
}
