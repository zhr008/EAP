using System;
using System.Drawing;
using System.Windows.Forms;
using AntdUI;
using EAP.Core.Configuration;
using EAP.Core.Protocol;
using EAP.Services;

namespace EAP.Client;

public partial class DeviceInfo : Form
{
    public DeviceConfig DeviceConfig { get; }
    public bool IsConnected { get; private set; }
    public bool IsInsideMainForm { get; private set; }
    public bool HeartbeatStatus { get; private set; } // 心跳状态

    public event EventHandler? ReturnedToMainForm;

    private readonly IDeviceManager _deviceManager;
    private readonly System.Windows.Forms.Panel? _containerPanel;
    private bool _isDragging;
    private Point _dragStartPoint;
    
    // 心跳相关
    private System.Windows.Forms.Timer? _heartbeatTimer;
    private bool _heartbeatAnimationState = false;
    private DateTime _lastHeartbeatTime = DateTime.MinValue;
    private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(10); // 心跳超时时间

    public DeviceInfo(DeviceConfig config, IDeviceManager deviceManager, System.Windows.Forms.Panel? containerPanel = null)
    {
        DeviceConfig = config;
        _deviceManager = deviceManager;
        _containerPanel = containerPanel;

        InitializeComponent();

        IsConnected = _deviceManager.GetConnectedDevices().Contains(DeviceConfig.Id);
        IsInsideMainForm = true;
        HeartbeatStatus = false;

        // 订阅设备连接状态变化事件
        _deviceManager.ConnectionStatusChanged += DeviceManager_ConnectionStatusChanged;
        _deviceManager.HeartbeatStatusChanged += DeviceManager_HeartbeatStatusChanged;
        
        // 初始化心跳定时器
        InitializeHeartbeat();

        LoadDynamicContent();
        UpdateDisplayMode(true);
    }

    private void DeviceManager_ConnectionStatusChanged(object? sender, ConnectionStatusChangedEventArgs e)
    {
        // 只处理当前设备的状态变化
        if (e.ConnectionId == DeviceConfig.Id)
        {
            UpdateStatus(e.IsConnected);
            // 连接状态变化时更新心跳状态
            if (e.IsConnected)
            {
                UpdateHeartbeat(true);
            }
            else
            {
                UpdateHeartbeat(false);
            }
        }
    }
    
    private void DeviceManager_HeartbeatStatusChanged(object? sender, HeartbeatStatusChangedEventArgs e)
    {
        // 只处理当前设备的心跳状态变化
        if (e.ConnectionId == DeviceConfig.Id)
        {
            UpdateHeartbeat(e.IsNormal);
        }
    }

    private void InitializeHeartbeat()
    {
        _heartbeatTimer = new System.Windows.Forms.Timer();
        _heartbeatTimer.Interval = 3000; // 3秒跳动一次
        _heartbeatTimer.Tick += HeartbeatTimer_Tick;
        
        // 初始化心跳图标
        DrawHeartbeatIcon();
        
        // 如果设备已连接，启动心跳定时器
        if (IsConnected)
        {
            _heartbeatTimer.Start();
            _lastHeartbeatTime = DateTime.Now;
        }
    }

    private void HeartbeatTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsConnected)
        {
            _heartbeatTimer?.Stop();
            return;
        }

        // 检查心跳超时
        if (DateTime.Now - _lastHeartbeatTime > _heartbeatTimeout)
        {
            HeartbeatStatus = false;
        }

        // 切换动画状态
        _heartbeatAnimationState = !_heartbeatAnimationState;
        
        // 更新心跳图标
        DrawHeartbeatIcon();
        
        // 如果心跳正常，更新最后心跳时间
        if (HeartbeatStatus)
        {
            _lastHeartbeatTime = DateTime.Now;
        }
    }

    private void DrawHeartbeatIcon()
    {
        if (_heartbeatIcon == null) return;

        // 创建位图
        var bitmap = new Bitmap(12, 12);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            // 根据心跳状态和动画状态选择颜色
            Color color;
            if (!IsConnected)
            {
                color = Color.Gray; // 未连接：灰色
            }
            else if (!HeartbeatStatus)
            {
                color = Color.Red; // 连接但心跳异常：红色
            }
            else
            {
                // 心跳正常：绿色和灰色交替
                color = _heartbeatAnimationState ? Color.Green : Color.FromArgb(128, 128, 128);
            }

            // 绘制圆形
            using (var brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, 0, 0, 12, 12);
            }
        }

        _heartbeatIcon.Image = bitmap;
    }

    private void LoadDynamicContent()
    {
        _statusTag.Text = IsConnected ? "在线" : "离线";
        _statusTag.Type = IsConnected ? TTypeMini.Success : TTypeMini.Error;
        _statusLabel.Text = $"设备: {DeviceConfig.Name} | 状态: {(IsConnected ? "在线" : "离线")}";

        LoadCardInfo();
        LoadFullInfo();

        AddLog($"日志系统已启动");
        AddLog($"设备: {DeviceConfig.Name}");
    }

    private void LoadCardInfo()
    {
        // 第一行：设备ID、Id、设备名称、Name
        SetCellText(0, 0, "设备ID");
        SetCellText(0, 1, DeviceConfig.Id);
        SetCellText(0, 2, "设备名称");
        SetCellText(0, 3, DeviceConfig.Name);

        // 第二行：启用状态、Enabled、是否在线、动态获取、心跳图标
        SetCellText(1, 0, "启用状态");
        SetCellText(1, 1, DeviceConfig.Enabled ? "是" : "否");
        SetCellText(1, 2, "是否在线");
        SetCellText(1, 3, IsConnected ? "在线" : "离线");
        // 心跳图标在位置 (1,4)，由 _heartbeatIcon 显示

        // 第三行：IP、Host、端口、Port
        SetCellText(2, 0, "IP");
        SetCellText(2, 1, GetHostText());
        SetCellText(2, 2, "端口");
        SetCellText(2, 3, GetPortText());
    }

    private void SetCellText(int row, int col, string text)
    {
        var control = _cardTable.GetControlFromPosition(col, row) as AntdUI.Label;
        if (control != null)
        {
            control.Text = text;
            // 根据内容设置颜色
            if (text == "在线" || text == "心跳正常")
                control.ForeColor = Color.Green;
            else if (text == "离线" || text == "心跳停止")
                control.ForeColor = Color.Red;
            else
                control.ForeColor = Color.Black;
        }
    }

    private void LoadFullInfo()
    {
        _valueId.Text = DeviceConfig.Id;
        _valueName.Text = DeviceConfig.Name;
        _valueEnabled.Text = DeviceConfig.Enabled ? "是" : "否";
        _valueOnline.Text = IsConnected ? "在线 ✓" : "离线 ✗";
        _valueOnline.ForeColor = IsConnected ? Color.Green : Color.Red;
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
        }
    }

    private void ClearProtocolRows()
    {
        _labelRow2Col1.Text = _valueRow2Col1.Text = string.Empty;
        _labelRow2Col2.Text = _valueRow2Col2.Text = string.Empty;
        _labelRow3Col1.Text = _valueRow3Col1.Text = string.Empty;
        _labelRow3Col2.Text = _valueRow3Col2.Text = string.Empty;
    }

    private string GetHostText()
    {
        return DeviceConfig.ProtocolType switch
        {
            ProtocolType.OpcUa => DeviceConfig.OpcUaConfig?.EndpointUrl ?? "N/A",
            ProtocolType.OpcDa => DeviceConfig.OpcDaConfig?.ServerProgId ?? "N/A",
            ProtocolType.Hsms => DeviceConfig.HsmsConfig?.Host ?? "N/A",
            ProtocolType.Modbus => DeviceConfig.ModbusConfig?.Host ?? "N/A",
            _ => "N/A"
        };
    }

    private string GetPortText()
    {
        return DeviceConfig.ProtocolType switch
        {
            ProtocolType.Hsms => DeviceConfig.HsmsConfig?.Port.ToString() ?? "N/A",
            ProtocolType.Modbus => DeviceConfig.ModbusConfig?.Port.ToString() ?? "N/A",
            _ => "N/A"
        };
    }

    #region 窗体事件

    private void _cardPanel_MouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (IsInsideMainForm)
        {
            IsInsideMainForm = false;
            UpdateDisplayMode(false);
        }
    }

    private void _cardPanel_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && IsInsideMainForm)
        {
            _isDragging = true;
            _dragStartPoint = e.Location;
            BringToFront();
        }
    }

    private void _cardPanel_MouseUp(object? sender, MouseEventArgs e)
    {
        _isDragging = false;
    }

    private void DeviceInfo_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isDragging && IsInsideMainForm)
        {
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
    }

    private void DeviceInfo_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !IsInsideMainForm)
        {
            e.Cancel = true;
            IsInsideMainForm = true;
            UpdateDisplayMode(true);

            if (_containerPanel != null)
                Location = _containerPanel.PointToScreen(new Point(20, 20));

            ReturnedToMainForm?.Invoke(this, EventArgs.Empty);
        }
        else if (e.CloseReason == CloseReason.UserClosing && IsInsideMainForm)
        {
            // 完全关闭时取消订阅事件
            _deviceManager.ConnectionStatusChanged -= DeviceManager_ConnectionStatusChanged;
            _deviceManager.HeartbeatStatusChanged -= DeviceManager_HeartbeatStatusChanged;
            _heartbeatTimer?.Stop();
            _heartbeatTimer?.Dispose();
        }
    }

    #endregion

    #region 公共方法

    public void UpdateDisplayMode(bool isInside)
    {
        IsInsideMainForm = isInside;

        if (isInside)
        {
            Size = new Size(360, 180);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            Text = string.Empty;
            TopMost = false;

            _logTextBox.Visible = false;
            _fullPanel.Visible = false;
            _statusStrip.Visible = false;
            _cardPanel.Visible = true;

            if (_containerPanel != null)
            {
                NativeMethods.SetParent(Handle, _containerPanel.Handle);
                Location = _containerPanel.PointToScreen(new Point(20, 20));
            }
        }
        else
        {
            Size = new Size(640, 640);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            ShowInTaskbar = true;
            Text = $"{DeviceConfig.FolderName} ({DeviceConfig.FilePath})";
            TopMost = true;

            _logTextBox.Visible = true;
            _fullPanel.Visible = true;
            _statusStrip.Visible = true;
            _cardPanel.Visible = false;

            NativeMethods.SetParent(Handle, IntPtr.Zero);
            StartPosition = FormStartPosition.CenterScreen;
            CenterToScreen();
        }
    }

    public void AddLog(string message)
    {
        if (_logTextBox != null && !IsInsideMainForm)
        {
            _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            _logTextBox.ScrollToCaret();
        }
        
        Log.Info(DeviceConfig.Id, message);
    }

    public void AddLogWarn(string message)
    {
        if (_logTextBox != null && !IsInsideMainForm)
        {
            _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] [WARN] {message}\n");
            _logTextBox.ScrollToCaret();
        }
        
        Log.Warn(DeviceConfig.Id, message);
    }

    public void AddLogError(string message, Exception? ex = null)
    {
        if (_logTextBox != null && !IsInsideMainForm)
        {
            _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] [ERROR] {message}\n");
            _logTextBox.ScrollToCaret();
        }
        
        Log.Error(DeviceConfig.Id, message, ex);
    }

    public void UpdateStatus(bool connected)
    {
        IsConnected = connected;

        if (InvokeRequired)
            Invoke(UpdateStatusUI);
        else
            UpdateStatusUI();
    }

    public void UpdateHeartbeat(bool isNormal)
    {
        if (InvokeRequired)
            Invoke(() => UpdateHeartbeatInternal(isNormal));
        else
            UpdateHeartbeatInternal(isNormal);
    }

    private void UpdateHeartbeatInternal(bool isNormal)
    {
        HeartbeatStatus = isNormal;
        if (isNormal)
        {
            _lastHeartbeatTime = DateTime.Now;
            _heartbeatTimer?.Start();
        }
        else
        {
            _heartbeatTimer?.Stop();
        }
        DrawHeartbeatIcon();
    }

    private void UpdateStatusUI()
    {
        // 更新状态标签
        _statusTag.Text = IsConnected ? "在线" : "离线";
        _statusTag.Type = IsConnected ? TTypeMini.Success : TTypeMini.Error;
        _statusLabel.Text = $"设备: {DeviceConfig.Name} | 状态: {(IsConnected ? "在线" : "离线")} | 更新时间: {DateTime.Now:HH:mm:ss}";
        
        // 更新卡片信息中的状态
        SetCellText(1, 3, IsConnected ? "在线" : "离线");
        
        // 更新完整信息面板中的状态
        if (_valueOnline != null)
        {
            _valueOnline.Text = IsConnected ? "在线 ✓" : "离线 ✗";
            _valueOnline.ForeColor = IsConnected ? Color.Green : Color.Red;
        }
        
        // 更新心跳图标
        DrawHeartbeatIcon();
        
        AddLog($"设备状态变更: {(IsConnected ? "在线" : "离线")}");
    }

    #endregion

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    }


}