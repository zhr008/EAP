using System;
using System.Drawing;
using System.Windows.Forms;
using AntdUI;
using EAP.Core;
using EAP.Services;

namespace EAP.Client;

/// <summary>
/// 设备卡片/明细窗体
/// 支持两种显示模式：
/// 1. 卡片模式（嵌入MainForm）
/// 2. 窗体模式（独立窗口显示详细信息）
/// </summary>
public partial class DeviceInfo : Form
{
    #region 公共属性

    public DeviceConfig DeviceConfig { get; set; }
    public bool IsConnected { get; private set; }
    public bool IsInsideMainForm { get; private set; }
    public bool HeartbeatStatus => _heartbeatManager.IsNormal;

    public event EventHandler? ReturnedToMainForm;

    #endregion

    #region 私有字段

    private readonly IDeviceManager _deviceManager;
    private readonly System.Windows.Forms.Panel? _containerPanel;
    private readonly HeartbeatManager _heartbeatManager;
    
    private bool _isDragging;
    private Point _dragStartPoint;

    // 状态变更防重复
    private DateTime _lastStatusChangeTime = DateTime.MinValue;
    private const int StatusChangeCooldownMs = 500;

    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(DeviceInfo));

    #endregion

    #region 构造函数

    public DeviceInfo(DeviceConfig config, IDeviceManager deviceManager, System.Windows.Forms.Panel? containerPanel = null)
    {
        DeviceConfig = config;
        _deviceManager = deviceManager;
        _containerPanel = containerPanel;
        IsConnected = _deviceManager.GetConnectedDevices().Contains(DeviceConfig.DeviceId);
        IsInsideMainForm = true;

        InitializeComponent();

        // 初始化心跳管理器
        _heartbeatManager = new HeartbeatManager(
            timeoutSeconds: 10,
            intervalMs: 3000,
            onDrawIcon: DrawHeartbeatIcon,
            onStatusChanged: OnHeartbeatStatusChanged
        );

        // 窗体基础设置
        TopLevel = false;
        FormBorderStyle = FormBorderStyle.None;
        Size = new Size(360, 140);

        // 订阅设备事件
        _deviceManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        _deviceManager.HeartbeatStatusChanged += OnHeartbeatStatusEvent;

        // 初始化显示
        LoadDynamicContent();
        UpdateDisplayMode(true);

        // 如果已连接，启动心跳
        if (IsConnected)
            _heartbeatManager.Start();
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 更新设备配置
    /// </summary>
    public void UpdateConfiguration(DeviceConfig newConfig)
    {
        if (newConfig == null) return;

        DeviceConfig = newConfig;
        LoadCardInfo();
        LoadFullInfo();
        Logger.Info($"设备配置已更新: {newConfig.DeviceId}");
    }

    /// <summary>
    /// 更新连接状态
    /// </summary>
    public void UpdateStatus(bool connected)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateStatus(connected));
            return;
        }

        // 防重复
        var now = DateTime.Now;
        if ((now - _lastStatusChangeTime).TotalMilliseconds < StatusChangeCooldownMs 
            && IsConnected == connected)
            return;

        _lastStatusChangeTime = now;
        IsConnected = connected;

        UpdateStatusUI();

        // 根据连接状态启动/停止心跳
        if (connected)
            _heartbeatManager.Start();
        else
            _heartbeatManager.Stop();
    }

    /// <summary>
    /// 更新心跳状态
    /// 注意：只要设备已连接，心跳管理器就保持运行，
    /// 心跳正常时调用 Pulse() 更新时间，心跳异常时通过颜色变化体现
    /// </summary>
    public void UpdateHeartbeat(bool isNormal)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateHeartbeat(isNormal));
            return;
        }

        // 设备未连接时不处理心跳
        if (!IsConnected) return;

        if (isNormal)
        {
            // 心跳正常：更新最后心跳时间
            _heartbeatManager.Pulse();
        }
        // 心跳不正常时，不调用 Stop()，让定时器继续运行
        // 这样心跳图标会持续显示红色（通过 GetStatusColor 方法判断）
    }

    /// <summary>
    /// 添加日志信息（同时写入文件和显示到UI）
    /// </summary>
    public void AddLogInfo(string message)
    {
        AppendLogToUI(message, "INFO");
        EAP.Core.DeviceLogger.Info(DeviceConfig.DeviceId, message);
    }

    /// <summary>
    /// 添加警告日志
    /// </summary>
    public void AddLogWarn(string message)
    {
        AppendLogToUI(message, "WARN");
        EAP.Core.DeviceLogger.Warn(DeviceConfig.DeviceId, message);
    }

    /// <summary>
    /// 添加错误日志
    /// </summary>
    public void AddLogError(string message, Exception? ex = null)
    {
        var fullMsg = ex != null ? $"{message}: {ex.Message}" : message;
        AppendLogToUI(fullMsg, "ERROR");
        EAP.Core.DeviceLogger.Error(DeviceConfig.DeviceId, message, ex);
    }

    /// <summary>
    /// 将日志添加到UI文本框
    /// </summary>
    private void AppendLogToUI(string message, string level)
    {
        if (_logTextBox == null) return;

        var logLine = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";

        if (InvokeRequired)
            BeginInvoke(() => _logTextBox.AppendText(logLine + "\n"));
        else
            _logTextBox.AppendText(logLine + "\n");

        _logTextBox.ScrollToCaret();
    }

    #endregion

    #region 显示模式切换

    public void UpdateDisplayMode(bool isInside)
    {
        IsInsideMainForm = isInside;

        if (isInside)
        {
            // 卡片模式
            TopLevel = false;
            Size = new Size(360, 140);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            Text = string.Empty;
            TopMost = false;
            _logTextBox.Visible = false;
            _fullPanel.Visible = false;
            _statusStrip.Visible = false;
            _cardPanel.Visible = true;
        }
        else
        {
            // 窗体模式
            TopLevel = true;
            Size = new Size(640, 640);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            ShowInTaskbar = true;
            Text = $"{DeviceConfig.FolderName} ({DeviceConfig.FilePath})";
            TopMost = true;
            _logTextBox.Visible = true;
            _fullPanel.Visible = true;
            _statusStrip.Visible = true;
            _cardPanel.Visible = false;
            StartPosition = FormStartPosition.CenterScreen;
            CenterToScreen();
        }
    }

    #endregion

    #region 事件处理

    private void OnConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
    {
        if (e.ConnectionId != DeviceConfig.DeviceId) return;
        UpdateStatus(e.IsConnected);
    }

    private void OnHeartbeatStatusEvent(object? sender, HeartbeatStatusChangedEventArgs e)
    {
        if (e.ConnectionId != DeviceConfig.DeviceId) return;
        UpdateHeartbeat(e.IsNormal);
    }

    private void OnHeartbeatStatusChanged(bool isNormal)
    {
        // 心跳状态变化时的额外处理
    }

    private void OnCardDoubleClick(object? sender, MouseEventArgs e)
    {
        if (!IsInsideMainForm) return;

        IsInsideMainForm = false;
        _containerPanel?.Controls.Remove(this);
        UpdateDisplayMode(false);
        Show();
    }

    private void OnCardMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !IsInsideMainForm) return;
        _isDragging = true;
        _dragStartPoint = e.Location;
        BringToFront();
    }

    private void OnCardMouseUp(object? sender, MouseEventArgs e)
    {
        _isDragging = false;
    }

    private void OnCardMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isDragging || !IsInsideMainForm) return;

        var newX = Location.X + e.X - _dragStartPoint.X;
        var newY = Location.Y + e.Y - _dragStartPoint.Y;

        if (_containerPanel != null)
        {
            var bounds = _containerPanel.RectangleToScreen(_containerPanel.ClientRectangle);
            newX = Math.Max(bounds.Left, Math.Min(newX, bounds.Right - Width));
            newY = Math.Max(bounds.Top, Math.Min(newY, bounds.Bottom - Height - 56));
        }

        Location = new Point(newX, newY);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason != CloseReason.UserClosing) return;

        if (!IsInsideMainForm)
        {
            e.Cancel = true;
            ReturnToCardMode();
        }
        else
        {
            Cleanup();
        }
    }

    #endregion

    #region 私有方法

    private void ReturnToCardMode()
    {
        IsInsideMainForm = true;
        UpdateDisplayMode(true);
        Location = _containerPanel?.PointToScreen(new Point(20, 20)) ?? new Point(20, 20);
        ReturnedToMainForm?.Invoke(this, EventArgs.Empty);
    }

    private void Cleanup()
    {
        _deviceManager.ConnectionStatusChanged -= OnConnectionStatusChanged;
        _deviceManager.HeartbeatStatusChanged -= OnHeartbeatStatusEvent;
        _heartbeatManager.Stop();
        _heartbeatManager.Dispose();
    }

    private void UpdateStatusUI()
    {
        var statusText = IsConnected ? "在线" : "离线";
        _statusTag.Text = statusText;
        _statusTag.Type = IsConnected ? TTypeMini.Success : TTypeMini.Error;
        _statusLabel.Text = $"设备: {DeviceConfig.DeviceName} | 状态: {statusText} | 更新时间: {DateTime.Now:HH:mm:ss}";

        // 更新卡片模式在线状态
        _onlineStatusCardLabel.Text = statusText;
        _onlineStatusCardLabel.ForeColor = IsConnected ? Color.Green : Color.Red;

        // 更新明细模式在线状态
        _onlineStatusFullLabel.Text = statusText;
        _onlineStatusFullLabel.ForeColor = IsConnected ? Color.Green : Color.Red;

        // 重新绘制心跳图标
        DrawHeartbeatIcon();
        AddLogInfo($"设备状态变更: {statusText}");
    }

    /// <summary>
    /// 绘制心跳图标（同时更新卡片模式和明细模式的图标）
    /// </summary>
    private void DrawHeartbeatIcon()
    {
        var bitmap = new Bitmap(12, 12);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(_heartbeatManager.GetStatusColor(IsConnected));
            g.FillEllipse(brush, 0, 0, 12, 12);
        }

        // 更新卡片模式心跳图标
        if (_heartbeatIconCard != null)
            _heartbeatIconCard.Image = bitmap;

        // 更新明细模式心跳图标（使用相同图像）
        if (_heartbeatIconFull != null)
            _heartbeatIconFull.Image = bitmap;
    }

    private void SetCellText(int row, int col, string text)
    {
        var control = _cardTable.GetControlFromPosition(col, row) as AntdUI.Label;
        if (control == null) return;

        control.Text = text;
        control.ForeColor = text switch
        {
            "在线" or "心跳正常" => Color.Green,
            "离线" or "心跳停止" => Color.Red,
            _ => Color.Black
        };
    }

    private void LoadDynamicContent()
    {
        _statusTag.Text = IsConnected ? "在线" : "离线";
        _statusTag.Type = IsConnected ? TTypeMini.Success : TTypeMini.Error;
        _statusLabel.Text = $"设备: {DeviceConfig.DeviceName} | 状态: {(IsConnected ? "在线" : "离线")}";

        LoadCardInfo();
        LoadFullInfo();

        AddLogInfo("日志系统已启动");
        AddLogInfo($"设备: {DeviceConfig.DeviceName}");
    }

    private void LoadCardInfo()
    {
        SetCellText(0, 0, "设备ID");
        SetCellText(0, 1, DeviceConfig.DeviceId);
        SetCellText(0, 2, "设备名称");
        SetCellText(0, 3, DeviceConfig.DeviceName);
        SetCellText(1, 0, "启用状态");
        SetCellText(1, 1, DeviceConfig.Enabled ? "是" : "否");
        SetCellText(1, 2, "是否在线");
        
        // 更新卡片模式在线状态
        var cardStatusText = IsConnected ? "在线" : "离线";
        _onlineStatusCardLabel.Text = cardStatusText;
        _onlineStatusCardLabel.ForeColor = IsConnected ? Color.Green : Color.Red;
        
        // 初始化心跳图标
        DrawHeartbeatIcon();
        
        SetCellText(2, 0, "IP");
        SetCellText(2, 1, GetHostText());
        SetCellText(2, 2, "端口");
        SetCellText(2, 3, GetPortText());
    }

    private void LoadFullInfo()
    {
        _valueId.Text = DeviceConfig.DeviceId;
        _valueName.Text = DeviceConfig.DeviceName;
        _valueEnabled.Text = DeviceConfig.Enabled ? "是" : "否";
        
        // 更新明细模式在线状态
        var fullStatusText = IsConnected ? "在线" : "离线";
        _onlineStatusFullLabel.Text = fullStatusText;
        _onlineStatusFullLabel.ForeColor = IsConnected ? Color.Green : Color.Red;
        
        // 初始化心跳图标
        DrawHeartbeatIcon();
        
        _valueTimeout.Text = $"{DeviceConfig.ConnectionTimeout} ms";
        _valueUpdateRate.Text = $"{DeviceConfig.UpdateRate} ms";

        LoadProtocolConfig();
    }

    private void LoadProtocolConfig()
    {
        switch (DeviceConfig.ProtocolType)
        {
            case ProtocolType.Hsms when DeviceConfig.HsmsConfig != null:
                var hsms = DeviceConfig.HsmsConfig;
                _labelRow1Col1.Text = "IP:";
                _valueRow1Col1.Text = hsms.Host ?? "N/A";
                _labelRow1Col2.Text = "端口:";
                _valueRow1Col2.Text = hsms.Port.ToString();
                _labelRow2Col1.Text = "模式:";
                _valueRow2Col1.Text = hsms.Mode == HsmsMode.Host ? "Host" : "Equipment";
                _labelRow2Col2.Text = "连接模式:";
                _valueRow2Col2.Text = hsms.ConnectionMode == HsmsConnectionMode.Active ? "主动" : "被动";
                _labelRow3Col1.Text = "T3:";
                _valueRow3Col1.Text = $"{hsms.T3Timeout} ms";
                _labelRow3Col2.Text = "T4:";
                _valueRow3Col2.Text = $"{hsms.T4Timeout} ms";
                break;

            case ProtocolType.OpcDa when DeviceConfig.OpcDaConfig != null:
                var opcDa = DeviceConfig.OpcDaConfig;
                _labelRow1Col1.Text = "Server:";
                _valueRow1Col1.Text = opcDa.ServerProgId ?? "N/A";
                _labelRow1Col2.Text = "Auth:";
                _valueRow1Col2.Text = opcDa.UseAnonymousAuth ? "匿名" : "用户名";
                ClearProtocolRows();
                break;

            case ProtocolType.OpcUa when DeviceConfig.OpcUaConfig != null:
                var opcUa = DeviceConfig.OpcUaConfig;
                _labelRow1Col1.Text = "Endpoint:";
                _valueRow1Col1.Text = opcUa.EndpointUrl ?? "N/A";
                _labelRow1Col2.Text = "Timeout:";
                _valueRow1Col2.Text = $"{opcUa.SessionTimeout} ms";
                ClearProtocolRows();
                break;

            case ProtocolType.Modbus when DeviceConfig.ModbusConfig != null:
                var modbus = DeviceConfig.ModbusConfig;
                _labelRow1Col1.Text = "IP:";
                _valueRow1Col1.Text = modbus.Host ?? "N/A";
                _labelRow1Col2.Text = "端口:";
                _valueRow1Col2.Text = modbus.Port.ToString();
                _labelRow2Col1.Text = "模式:";
                _valueRow2Col1.Text = modbus.Mode == ModbusMode.Tcp ? "TCP" : (modbus.Mode == ModbusMode.Rtu ? "RTU" : "ASCII");
                _labelRow2Col2.Text = "从站ID:";
                _valueRow2Col2.Text = modbus.SlaveId.ToString();
                _labelRow3Col1.Text = "串口:";
                _valueRow3Col1.Text = modbus.SerialPort;
                _labelRow3Col2.Text = "波特率:";
                _valueRow3Col2.Text = modbus.BaudRate.ToString();
                break;

            default:
                ClearProtocolRows();
                break;
        }
    }

    private void ClearProtocolRows()
    {
        _labelRow2Col1.Text = _valueRow2Col1.Text = string.Empty;
        _labelRow2Col2.Text = _valueRow2Col2.Text = string.Empty;
        _labelRow3Col1.Text = _valueRow3Col1.Text = string.Empty;
        _labelRow3Col2.Text = _valueRow3Col2.Text = string.Empty;
    }

    private string GetHostText() => DeviceConfig.ProtocolType switch
    {
        ProtocolType.OpcUa => DeviceConfig.OpcUaConfig?.EndpointUrl ?? "N/A",
        ProtocolType.OpcDa => DeviceConfig.OpcDaConfig?.ServerProgId ?? "N/A",
        ProtocolType.Hsms => DeviceConfig.HsmsConfig?.Host ?? "N/A",
        ProtocolType.Modbus => DeviceConfig.ModbusConfig?.Host ?? "N/A",
        _ => "N/A"
    };

    private string GetPortText() => DeviceConfig.ProtocolType switch
    {
        ProtocolType.Hsms => DeviceConfig.HsmsConfig?.Port.ToString() ?? "N/A",
        ProtocolType.Modbus => DeviceConfig.ModbusConfig?.Port.ToString() ?? "N/A",
        _ => "N/A"
    };

    #endregion
}
