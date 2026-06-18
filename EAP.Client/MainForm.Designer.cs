using System.Drawing;
using System.Windows.Forms;
using AntdUI;

namespace EAP.Client;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        // 窗体基本属性
        Text = "EAP 设备管理系统";
        Size = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 600);

        // ==================== Ribbon (菜单) ====================
        _ribbon = new Ribbon();
        _ribbon.Dock = DockStyle.Top;
        _ribbon.Height = 120;

        var homeTab = new RibbonTabPage("首页",
            new RibbonItemGroup("配置",
                new RibbonItem("选择配置目录", "FolderOpenOutlined", SelectConfigDir_Click) { Size = RibbonItemSize.Large },
                new RibbonItem("刷新配置", "SyncOutlined", RefreshBtn_Click) { Size = RibbonItemSize.Large }
            ),
            new RibbonItemGroup("设备",
                new RibbonItem("连接全部", "LinkOutlined", ConnectAllBtn_Click) { Size = RibbonItemSize.Large },
                new RibbonItem("断开全部", "LinkOffOutlined", DisconnectAllBtn_Click) { Size = RibbonItemSize.Large }
            )
        );

        var viewTab = new RibbonTabPage("视图",
            new RibbonItemGroup("显示",
                new RibbonItem("切换主题", "SunOutlined", ToggleDarkMode) { Size = RibbonItemSize.Large }
            )
        );

        var helpTab = new RibbonTabPage("帮助",
            new RibbonItemGroup("关于",
                new RibbonItem("关于", "InfoCircleOutlined", ShowAbout) { Size = RibbonItemSize.Large }
            )
        );

        _ribbon.TabPages.Add(homeTab);
        _ribbon.TabPages.Add(viewTab);
        _ribbon.TabPages.Add(helpTab);
        _ribbon.SelectedIndex = 0;

        // ==================== ContentPanel (内容区域) ====================
        _contentPanel = new System.Windows.Forms.Panel();
        _contentPanel.Dock = DockStyle.Fill;
        _contentPanel.BackColor = Color.FromArgb(245, 245, 245);

        // ==================== StatusBar (状态栏) ====================
        var statusPanel = new System.Windows.Forms.Panel();
        statusPanel.Dock = DockStyle.Bottom;
        statusPanel.Height = 32;
        statusPanel.BackColor = Color.White;

        _statTotal = new System.Windows.Forms.Label();
        _statTotal.Text = "设备: 0";
        _statTotal.Location = new Point(10, 8);
        _statTotal.AutoSize = true;

        _statOnline = new System.Windows.Forms.Label();
        _statOnline.Text = "在线: 0";
        _statOnline.Location = new Point(100, 8);
        _statOnline.AutoSize = true;
        _statOnline.ForeColor = Color.Green;

        _statOffline = new System.Windows.Forms.Label();
        _statOffline.Text = "离线: 0";
        _statOffline.Location = new Point(180, 8);
        _statOffline.AutoSize = true;
        _statOffline.ForeColor = Color.Red;

        _statusBar = new System.Windows.Forms.Label();
        _statusBar.Text = "就绪";
        _statusBar.Location = new Point(260, 8);
        _statusBar.AutoSize = true;

        statusPanel.Controls.Add(_statTotal);
        statusPanel.Controls.Add(_statOnline);
        statusPanel.Controls.Add(_statOffline);
        statusPanel.Controls.Add(_statusBar);

        // 添加控件到窗体
        Controls.Add(_contentPanel);
        Controls.Add(_ribbon);
        Controls.Add(statusPanel);
    }
}
