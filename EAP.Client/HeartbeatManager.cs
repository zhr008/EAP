using System;
using System.Drawing;
using System.Windows.Forms;

namespace EAP.Client;

/// <summary>
/// 心跳动画管理器
/// 仅负责UI心跳图标的动画效果，心跳状态由外部通过SetNormal()设置
/// 心跳检测逻辑在Core层ProtocolClientBase中实现
/// </summary>
public class HeartbeatManager : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Action _onDrawIcon;

    private bool _animationState;
    private bool _isNormal;
    private bool _isRunning;

    public bool IsNormal => _isNormal;
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="intervalMs">动画切换间隔（毫秒）</param>
    /// <param name="onDrawIcon">重绘图标的回调</param>
    public HeartbeatManager(int intervalMs = 1000, Action? onDrawIcon = null)
    {
        _onDrawIcon = onDrawIcon ?? (() => { });

        _timer = new System.Windows.Forms.Timer { Interval = intervalMs };
        _timer.Tick += Timer_Tick;
    }

    /// <summary>
    /// 启动心跳动画
    /// </summary>
    public void Start()
    {
        if (!_isRunning)
        {
            _isRunning = true;
            _animationState = false;
            _timer.Start();
            _onDrawIcon();
        }
    }

    /// <summary>
    /// 停止心跳动画
    /// </summary>
    public void Stop()
    {
        if (_isRunning)
        {
            _isRunning = false;
            _isNormal = false;
            _timer.Stop();
            _onDrawIcon();
        }
    }

    /// <summary>
    /// 设置心跳状态（由外部业务逻辑驱动）
    /// </summary>
    /// <param name="isNormal">心跳是否正常</param>
    public void SetNormal(bool isNormal)
    {
        if (_isNormal != isNormal)
        {
            _isNormal = isNormal;
            _onDrawIcon();
        }
    }

    /// <summary>
    /// 定时器回调 - 仅用于切换动画状态
    /// </summary>
    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_isRunning)
        {
            _timer.Stop();
            return;
        }

        _animationState = !_animationState;
        _onDrawIcon();
    }

    /// <summary>
    /// 获取心跳状态对应的颜色
    /// </summary>
    /// <param name="isConnected">设备是否连接</param>
    /// <returns>图标颜色</returns>
    public Color GetStatusColor(bool isConnected)
    {
        if (!isConnected || !_isRunning)
            return Color.Gray;

        if (!_isNormal)
            return Color.Red;

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
