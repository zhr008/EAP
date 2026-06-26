using System;
using System.Drawing;
using System.Windows.Forms;

namespace EAP.Client;

/// <summary>
/// 心跳状态管理类
/// 封装设备心跳检测的逻辑
/// </summary>
public class HeartbeatManager
{
    // 心跳定时器
    private readonly System.Windows.Forms.Timer _timer;
    
    // 心跳参数
    private readonly TimeSpan _timeout;
    private DateTime _lastHeartbeatTime;
    
    // 状态属性
    public bool IsRunning { get; private set; }
    public bool IsNormal { get; private set; }
    
    // 心跳图标
    private bool _animationState;
    private readonly Action _onDrawIcon;
    
    // 日志回调
    private readonly Action<bool> _onStatusChanged;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="timeoutSeconds">心跳超时时间（秒）</param>
    /// <param name="intervalMs">心跳检查间隔（毫秒）</param>
    /// <param name="onDrawIcon">绘制心跳图标的回调</param>
    /// <param name="onStatusChanged">心跳状态变化的回调</param>
    public HeartbeatManager(int timeoutSeconds = 10, int intervalMs = 3000, 
        Action? onDrawIcon = null, Action<bool>? onStatusChanged = null)
    {
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
        _onDrawIcon = () => onDrawIcon?.Invoke();
        _onStatusChanged = onStatusChanged ?? (_ => { });
        
        _timer = new System.Windows.Forms.Timer { Interval = intervalMs };
        _timer.Tick += Timer_Tick;
    }

    /// <summary>
    /// 启动心跳检测
    /// </summary>
    public void Start()
    {
        if (!IsRunning)
        {
            IsRunning = true;
            _lastHeartbeatTime = DateTime.Now;
            _timer.Start();
            _onDrawIcon();
        }
    }

    /// <summary>
    /// 停止心跳检测
    /// </summary>
    public void Stop()
    {
        if (IsRunning)
        {
            IsRunning = false;
            IsNormal = false;
            _timer.Stop();
            _onDrawIcon();
        }
    }

    /// <summary>
    /// 更新心跳（收到新数据时调用）
    /// </summary>
    public void Pulse()
    {
        _lastHeartbeatTime = DateTime.Now;
        if (!IsNormal)
        {
            IsNormal = true;
            _onStatusChanged(true);
        }
        _onDrawIcon();
    }

    /// <summary>
    /// 检查是否超时
    /// </summary>
    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!IsRunning)
        {
            _timer.Stop();
            return;
        }

        var wasNormal = IsNormal;
        
        // 检查是否超时
        if (DateTime.Now - _lastHeartbeatTime > _timeout)
        {
            IsNormal = false;
        }
        
        // 切换动画状态
        _animationState = !_animationState;

        // 如果状态变化，触发回调
        if (wasNormal != IsNormal)
        {
            _onStatusChanged(IsNormal);
        }

        // 更新图标
        _onDrawIcon();
    }

    /// <summary>
    /// 获取心跳状态对应的颜色
    /// </summary>
    public Color GetStatusColor(bool isConnected)
    {
        if (!isConnected)
            return Color.Gray;
        
        if (!IsNormal)
            return Color.Red;
        
        // 心跳正常：绿色和灰色交替
        return _animationState ? Color.Green : Color.FromArgb(128, 128, 128);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
