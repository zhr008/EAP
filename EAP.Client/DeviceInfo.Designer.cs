using System.Drawing;
using System.Windows.Forms;
using AntdUI;

namespace EAP.Client;

partial class DeviceInfo
{
    private System.ComponentModel.IContainer components = null;

    // 控件声明
    private System.Windows.Forms.Panel _cardPanel;
    private AntdUI.Tag _statusTag;
    private TableLayoutPanel _cardTable;
    private PictureBox _heartbeatIconCard; // 卡片模式心跳图标
    private PictureBox _heartbeatIconFull; // 明细模式心跳图标
    
    // _cardTable 单元格控件 (3行4列)
    private AntdUI.Label _cardCell00;
    private AntdUI.Label _cardCell01;
    private AntdUI.Label _cardCell02;
    private AntdUI.Label _cardCell03;
    private AntdUI.Label _cardCell10;
    private AntdUI.Label _cardCell11;
    private AntdUI.Label _cardCell12;
    private AntdUI.Label _cardCell13;
    private AntdUI.Label _cardCell20;
    private AntdUI.Label _cardCell21;
    private AntdUI.Label _cardCell22;
    private AntdUI.Label _cardCell23;
    
    // 在线状态容器（包含文本和心跳图标）
    private System.Windows.Forms.FlowLayoutPanel _onlineStatusCardPanel; // 卡片模式在线状态容器
    private System.Windows.Forms.Label _onlineStatusCardLabel; // 卡片模式在线状态文本
    
    private System.Windows.Forms.FlowLayoutPanel _onlineStatusFullPanel; // 明细模式在线状态容器
    private System.Windows.Forms.Label _onlineStatusFullLabel; // 明细模式在线状态文本
    
    private System.Windows.Forms.Panel _fullPanel;
    private TableLayoutPanel _fullTable;
    private TableLayoutPanel _fullTable1;
    private AntdUI.Divider _Divider;
    private TableLayoutPanel _fullTable2;
    private RichTextBox _logTextBox;
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _statusLabel;
    
    // TableLayoutPanel1 静态控件 (4列3行)
    private AntdUI.Label _labelId;
    private AntdUI.Label _valueId;
    private AntdUI.Label _labelName;
    private AntdUI.Label _valueName;
    private AntdUI.Label _labelEnabled;
    private AntdUI.Label _valueEnabled;
    private AntdUI.Label _labelOnline;
    private AntdUI.Label _valueOnline;
    private AntdUI.Label _labelTimeout;
    private AntdUI.Label _valueTimeout;
    private AntdUI.Label _labelUpdateRate;
    private AntdUI.Label _valueUpdateRate;
    
    // TableLayoutPanel2 静态控件 (4列3行)
    private AntdUI.Label _labelRow1Col1;
    private AntdUI.Label _labelRow1Col2;
    private AntdUI.Label _valueRow1Col1;
    private AntdUI.Label _valueRow1Col2;
    private AntdUI.Label _labelRow2Col1;
    private AntdUI.Label _labelRow2Col2;
    private AntdUI.Label _valueRow2Col1;
    private AntdUI.Label _valueRow2Col2;
    private AntdUI.Label _labelRow3Col1;
    private AntdUI.Label _labelRow3Col2;
    private AntdUI.Label _valueRow3Col1;
    private AntdUI.Label _valueRow3Col2;
    
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
        _cardPanel = new System.Windows.Forms.Panel();
        _cardTable = new TableLayoutPanel();
        _cardCell00 = new AntdUI.Label();
        _cardCell01 = new AntdUI.Label();
        _cardCell02 = new AntdUI.Label();
        _cardCell03 = new AntdUI.Label();
        _cardCell10 = new AntdUI.Label();
        _cardCell11 = new AntdUI.Label();
        _cardCell12 = new AntdUI.Label();
        _onlineStatusCardPanel = new FlowLayoutPanel();
        _onlineStatusCardLabel = new System.Windows.Forms.Label();
        _heartbeatIconCard = new PictureBox();
        _cardCell20 = new AntdUI.Label();
        _cardCell21 = new AntdUI.Label();
        _cardCell22 = new AntdUI.Label();
        _cardCell23 = new AntdUI.Label();
        _cardCell13 = new AntdUI.Label();
        _statusTag = new Tag();
        _heartbeatIconFull = new PictureBox();
        _onlineStatusFullPanel = new FlowLayoutPanel();
        _onlineStatusFullLabel = new System.Windows.Forms.Label();
        _fullPanel = new System.Windows.Forms.Panel();
        _fullTable = new TableLayoutPanel();
        _fullTable1 = new TableLayoutPanel();
        _labelId = new AntdUI.Label();
        _valueId = new AntdUI.Label();
        _labelName = new AntdUI.Label();
        _valueName = new AntdUI.Label();
        _labelEnabled = new AntdUI.Label();
        _valueEnabled = new AntdUI.Label();
        _labelOnline = new AntdUI.Label();
        _labelTimeout = new AntdUI.Label();
        _valueTimeout = new AntdUI.Label();
        _labelUpdateRate = new AntdUI.Label();
        _valueUpdateRate = new AntdUI.Label();
        _Divider = new Divider();
        _fullTable2 = new TableLayoutPanel();
        _labelRow1Col1 = new AntdUI.Label();
        _valueRow1Col1 = new AntdUI.Label();
        _labelRow1Col2 = new AntdUI.Label();
        _valueRow1Col2 = new AntdUI.Label();
        _labelRow2Col1 = new AntdUI.Label();
        _valueRow2Col1 = new AntdUI.Label();
        _labelRow2Col2 = new AntdUI.Label();
        _valueRow2Col2 = new AntdUI.Label();
        _labelRow3Col1 = new AntdUI.Label();
        _valueRow3Col1 = new AntdUI.Label();
        _labelRow3Col2 = new AntdUI.Label();
        _valueRow3Col2 = new AntdUI.Label();
        _valueOnline = new AntdUI.Label();
        _logTextBox = new RichTextBox();
        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel();
        _cardPanel.SuspendLayout();
        _cardTable.SuspendLayout();
        _onlineStatusCardPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_heartbeatIconCard).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_heartbeatIconFull).BeginInit();
        _onlineStatusFullPanel.SuspendLayout();
        _fullPanel.SuspendLayout();
        _fullTable.SuspendLayout();
        _fullTable1.SuspendLayout();
        _fullTable2.SuspendLayout();
        _statusStrip.SuspendLayout();
        SuspendLayout();
        // 
        // _cardPanel
        // 
        _cardPanel.BackColor = Color.White;
        _cardPanel.Controls.Add(_cardTable);
        _cardPanel.Dock = DockStyle.Fill;
        _cardPanel.Location = new Point(0, 0);
        _cardPanel.Name = "_cardPanel";
        _cardPanel.Padding = new Padding(16);
        _cardPanel.Size = new Size(624, 601);
        _cardPanel.TabIndex = 2;
        _cardPanel.MouseDoubleClick += OnCardDoubleClick;
        _cardPanel.MouseDown += OnCardMouseDown;
        _cardPanel.MouseUp += OnCardMouseUp;
        // 
        // _cardTable
        // 
        _cardTable.ColumnCount = 4;
        _cardTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
        _cardTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _cardTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
        _cardTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _cardTable.Controls.Add(_cardCell00, 0, 0);
        _cardTable.Controls.Add(_cardCell01, 1, 0);
        _cardTable.Controls.Add(_cardCell02, 2, 0);
        _cardTable.Controls.Add(_cardCell03, 3, 0);
        _cardTable.Controls.Add(_cardCell10, 0, 1);
        _cardTable.Controls.Add(_cardCell11, 1, 1);
        _cardTable.Controls.Add(_cardCell12, 2, 1);
        _cardTable.Controls.Add(_onlineStatusCardPanel, 3, 1);
        _cardTable.Controls.Add(_cardCell20, 0, 2);
        _cardTable.Controls.Add(_cardCell21, 1, 2);
        _cardTable.Controls.Add(_cardCell22, 2, 2);
        _cardTable.Controls.Add(_cardCell23, 3, 2);
        _cardTable.Dock = DockStyle.Fill;
        _cardTable.Location = new Point(16, 16);
        _cardTable.Name = "_cardTable";
        _cardTable.RowCount = 3;
        _cardTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        _cardTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        _cardTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        _cardTable.Size = new Size(592, 569);
        _cardTable.TabIndex = 2;
        _cardTable.MouseDoubleClick += OnCardDoubleClick;
        // 
        // _cardCell00
        // 
        _cardCell00.Font = new Font("Segoe UI", 8F);
        _cardCell00.ForeColor = Color.Gray;
        _cardCell00.Location = new Point(3, 3);
        _cardCell00.Name = "_cardCell00";
        _cardCell00.Size = new Size(54, 15);
        _cardCell00.TabIndex = 0;
        _cardCell00.Text = "设备ID:";
        // 
        // _cardCell01
        // 
        _cardCell01.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _cardCell01.ForeColor = Color.Black;
        _cardCell01.Location = new Point(63, 3);
        _cardCell01.Name = "_cardCell01";
        _cardCell01.Size = new Size(84, 15);
        _cardCell01.TabIndex = 1;
        _cardCell01.Text = "-";
        // 
        // _cardCell02
        // 
        _cardCell02.Font = new Font("Segoe UI", 8F);
        _cardCell02.ForeColor = Color.Gray;
        _cardCell02.Location = new Point(294, 3);
        _cardCell02.Name = "_cardCell02";
        _cardCell02.Size = new Size(54, 15);
        _cardCell02.TabIndex = 2;
        _cardCell02.Text = "设备名称:";
        // 
        // _cardCell03
        // 
        _cardCell03.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _cardCell03.ForeColor = Color.Black;
        _cardCell03.Location = new Point(364, 3);
        _cardCell03.Name = "_cardCell03";
        _cardCell03.Size = new Size(84, 15);
        _cardCell03.TabIndex = 3;
        _cardCell03.Text = "-";
        // 
        // _cardCell10
        // 
        _cardCell10.Font = new Font("Segoe UI", 8F);
        _cardCell10.ForeColor = Color.Gray;
        _cardCell10.Location = new Point(3, 48);
        _cardCell10.Name = "_cardCell10";
        _cardCell10.Size = new Size(54, 15);
        _cardCell10.TabIndex = 5;
        _cardCell10.Text = "启用状态:";
        // 
        // _cardCell11
        // 
        _cardCell11.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _cardCell11.ForeColor = Color.Black;
        _cardCell11.Location = new Point(63, 48);
        _cardCell11.Name = "_cardCell11";
        _cardCell11.Size = new Size(84, 15);
        _cardCell11.TabIndex = 6;
        _cardCell11.Text = "-";
        // 
        // _cardCell12
        // 
        _cardCell12.Font = new Font("Segoe UI", 8F);
        _cardCell12.ForeColor = Color.Gray;
        _cardCell12.Location = new Point(294, 48);
        _cardCell12.Name = "_cardCell12";
        _cardCell12.Size = new Size(54, 15);
        _cardCell12.TabIndex = 7;
        _cardCell12.Text = "是否在线:";
        // 
        // _onlineStatusCardPanel
        // 
        _onlineStatusCardPanel.Controls.Add(_onlineStatusCardLabel);
        _onlineStatusCardPanel.Controls.Add(_heartbeatIconCard);
        _onlineStatusCardPanel.Dock = DockStyle.Top;
        _onlineStatusCardPanel.Location = new Point(364, 48);
        _onlineStatusCardPanel.Name = "_onlineStatusCardPanel";
        _onlineStatusCardPanel.Size = new Size(225, 15);
        _onlineStatusCardPanel.TabIndex = 0;
        _onlineStatusCardPanel.WrapContents = false;
        // 
        // _onlineStatusCardLabel
        // 
        _onlineStatusCardLabel.AutoSize = true;
        _onlineStatusCardLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _onlineStatusCardLabel.ForeColor = Color.Black;
        _onlineStatusCardLabel.Location = new Point(5, 0);
        _onlineStatusCardLabel.Margin = new Padding(5, 0, 0, 0);
        _onlineStatusCardLabel.Name = "_onlineStatusCardLabel";
        _onlineStatusCardLabel.Size = new Size(11, 13);
        _onlineStatusCardLabel.TabIndex = 8;
        _onlineStatusCardLabel.Text = "-";
        // 
        // _heartbeatIconCard
        // 
        _heartbeatIconCard.BackColor = Color.Transparent;
        _heartbeatIconCard.Location = new Point(46, 0);
        _heartbeatIconCard.Margin = new Padding(30, 0, 0, 0);
        _heartbeatIconCard.Name = "_heartbeatIconCard";
        _heartbeatIconCard.Size = new Size(12, 12);
        _heartbeatIconCard.TabIndex = 9;
        _heartbeatIconCard.TabStop = false;
        // 
        // _cardCell20
        // 
        _cardCell20.Font = new Font("Segoe UI", 8F);
        _cardCell20.ForeColor = Color.Gray;
        _cardCell20.Location = new Point(3, 93);
        _cardCell20.Name = "_cardCell20";
        _cardCell20.Size = new Size(54, 15);
        _cardCell20.TabIndex = 10;
        _cardCell20.Text = "IP:";
        // 
        // _cardCell21
        // 
        _cardCell21.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _cardCell21.ForeColor = Color.Black;
        _cardCell21.Location = new Point(63, 93);
        _cardCell21.Name = "_cardCell21";
        _cardCell21.Size = new Size(84, 15);
        _cardCell21.TabIndex = 11;
        _cardCell21.Text = "-";
        // 
        // _cardCell22
        // 
        _cardCell22.Font = new Font("Segoe UI", 8F);
        _cardCell22.ForeColor = Color.Gray;
        _cardCell22.Location = new Point(294, 93);
        _cardCell22.Name = "_cardCell22";
        _cardCell22.Size = new Size(54, 15);
        _cardCell22.TabIndex = 12;
        _cardCell22.Text = "端口:";
        // 
        // _cardCell23
        // 
        _cardCell23.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _cardCell23.ForeColor = Color.Black;
        _cardCell23.Location = new Point(364, 93);
        _cardCell23.Name = "_cardCell23";
        _cardCell23.Size = new Size(84, 15);
        _cardCell23.TabIndex = 13;
        _cardCell23.Text = "-";
        // 
        // _cardCell13
        // 
        _cardCell13.Location = new Point(0, 0);
        _cardCell13.Name = "_cardCell13";
        _cardCell13.Size = new Size(0, 0);
        _cardCell13.TabIndex = 0;
        // 
        // _statusTag
        // 
        _statusTag.Location = new Point(280, 16);
        _statusTag.Name = "_statusTag";
        _statusTag.Size = new Size(0, 0);
        _statusTag.TabIndex = 4;
        // 
        // _heartbeatIconFull
        // 
        _heartbeatIconFull.BackColor = Color.Transparent;
        _heartbeatIconFull.Location = new Point(46, 5);
        _heartbeatIconFull.Margin = new Padding(30, 5, 0, 0);
        _heartbeatIconFull.Name = "_heartbeatIconFull";
        _heartbeatIconFull.Size = new Size(12, 12);
        _heartbeatIconFull.TabIndex = 12;
        _heartbeatIconFull.TabStop = false;
        // 
        // _onlineStatusFullPanel
        // 
        _onlineStatusFullPanel.Controls.Add(_onlineStatusFullLabel);
        _onlineStatusFullPanel.Controls.Add(_heartbeatIconFull);
        _onlineStatusFullPanel.Dock = DockStyle.Fill;
        _onlineStatusFullPanel.Location = new Point(382, 33);
        _onlineStatusFullPanel.Name = "_onlineStatusFullPanel";
        _onlineStatusFullPanel.Size = new Size(213, 24);
        _onlineStatusFullPanel.TabIndex = 0;
        _onlineStatusFullPanel.WrapContents = false;
        // 
        // _onlineStatusFullLabel
        // 
        _onlineStatusFullLabel.AutoSize = true;
        _onlineStatusFullLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _onlineStatusFullLabel.ForeColor = Color.Black;
        _onlineStatusFullLabel.Location = new Point(0, 4);
        _onlineStatusFullLabel.Margin = new Padding(0, 4, 4, 0);
        _onlineStatusFullLabel.Name = "_onlineStatusFullLabel";
        _onlineStatusFullLabel.Size = new Size(12, 15);
        _onlineStatusFullLabel.TabIndex = 7;
        _onlineStatusFullLabel.Text = "-";
        // 
        // _fullPanel
        // 
        _fullPanel.BackColor = Color.White;
        _fullPanel.Controls.Add(_fullTable);
        _fullPanel.Dock = DockStyle.Top;
        _fullPanel.Location = new Point(0, 0);
        _fullPanel.Name = "_fullPanel";
        _fullPanel.Size = new Size(624, 220);
        _fullPanel.TabIndex = 1;
        _fullPanel.Visible = false;
        // 
        // _fullTable
        // 
        _fullTable.ColumnCount = 1;
        _fullTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _fullTable.Controls.Add(_fullTable1, 0, 0);
        _fullTable.Controls.Add(_Divider, 0, 1);
        _fullTable.Controls.Add(_fullTable2, 0, 2);
        _fullTable.Dock = DockStyle.Fill;
        _fullTable.Location = new Point(0, 0);
        _fullTable.Name = "_fullTable";
        _fullTable.Padding = new Padding(10);
        _fullTable.RowCount = 3;
        _fullTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
        _fullTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 10F));
        _fullTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _fullTable.Size = new Size(624, 220);
        _fullTable.TabIndex = 0;
        // 
        // _fullTable1
        // 
        _fullTable1.ColumnCount = 4;
        _fullTable1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
        _fullTable1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _fullTable1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
        _fullTable1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _fullTable1.Controls.Add(_labelId, 0, 0);
        _fullTable1.Controls.Add(_valueId, 1, 0);
        _fullTable1.Controls.Add(_labelName, 2, 0);
        _fullTable1.Controls.Add(_valueName, 3, 0);
        _fullTable1.Controls.Add(_labelEnabled, 0, 1);
        _fullTable1.Controls.Add(_valueEnabled, 1, 1);
        _fullTable1.Controls.Add(_labelOnline, 2, 1);
        _fullTable1.Controls.Add(_onlineStatusFullPanel, 3, 1);
        _fullTable1.Controls.Add(_labelTimeout, 0, 2);
        _fullTable1.Controls.Add(_valueTimeout, 1, 2);
        _fullTable1.Controls.Add(_labelUpdateRate, 2, 2);
        _fullTable1.Controls.Add(_valueUpdateRate, 3, 2);
        _fullTable1.Dock = DockStyle.Fill;
        _fullTable1.Location = new Point(13, 13);
        _fullTable1.Name = "_fullTable1";
        _fullTable1.RowCount = 3;
        _fullTable1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _fullTable1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _fullTable1.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _fullTable1.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        _fullTable1.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        _fullTable1.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        _fullTable1.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        _fullTable1.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        _fullTable1.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        _fullTable1.Size = new Size(598, 94);
        _fullTable1.TabIndex = 0;
        // 
        // _labelId
        // 
        _labelId.AutoSizeMode = TAutoSize.Auto;
        _labelId.Font = new Font("Segoe UI", 9F);
        _labelId.ForeColor = Color.Gray;
        _labelId.Location = new Point(3, 3);
        _labelId.Name = "_labelId";
        _labelId.Size = new Size(40, 16);
        _labelId.TabIndex = 0;
        _labelId.Text = "设备ID:";
        // 
        // _valueId
        // 
        _valueId.AutoSizeMode = TAutoSize.Auto;
        _valueId.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _valueId.ForeColor = Color.Black;
        _valueId.Location = new Point(83, 3);
        _valueId.Name = "_valueId";
        _valueId.Size = new Size(5, 16);
        _valueId.TabIndex = 1;
        _valueId.Text = "-";
        // 
        // _labelName
        // 
        _labelName.AutoSizeMode = TAutoSize.Auto;
        _labelName.Font = new Font("Segoe UI", 9F);
        _labelName.ForeColor = Color.Gray;
        _labelName.Location = new Point(302, 3);
        _labelName.Name = "_labelName";
        _labelName.Size = new Size(53, 16);
        _labelName.TabIndex = 2;
        _labelName.Text = "设备名称:";
        // 
        // _valueName
        // 
        _valueName.AutoSizeMode = TAutoSize.Auto;
        _valueName.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _valueName.ForeColor = Color.Black;
        _valueName.Location = new Point(382, 3);
        _valueName.Name = "_valueName";
        _valueName.Size = new Size(5, 16);
        _valueName.TabIndex = 3;
        _valueName.Text = "-";
        // 
        // _labelEnabled
        // 
        _labelEnabled.AutoSizeMode = TAutoSize.Auto;
        _labelEnabled.Font = new Font("Segoe UI", 9F);
        _labelEnabled.ForeColor = Color.Gray;
        _labelEnabled.Location = new Point(3, 33);
        _labelEnabled.Name = "_labelEnabled";
        _labelEnabled.Size = new Size(53, 16);
        _labelEnabled.TabIndex = 4;
        _labelEnabled.Text = "启用状态:";
        // 
        // _valueEnabled
        // 
        _valueEnabled.AutoSizeMode = TAutoSize.Auto;
        _valueEnabled.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _valueEnabled.ForeColor = Color.Black;
        _valueEnabled.Location = new Point(83, 33);
        _valueEnabled.Name = "_valueEnabled";
        _valueEnabled.Size = new Size(5, 16);
        _valueEnabled.TabIndex = 5;
        _valueEnabled.Text = "-";
        // 
        // _labelOnline
        // 
        _labelOnline.AutoSizeMode = TAutoSize.Auto;
        _labelOnline.Font = new Font("Segoe UI", 9F);
        _labelOnline.ForeColor = Color.Gray;
        _labelOnline.Location = new Point(302, 33);
        _labelOnline.Name = "_labelOnline";
        _labelOnline.Size = new Size(53, 16);
        _labelOnline.TabIndex = 6;
        _labelOnline.Text = "是否在线:";
        // 
        // _labelTimeout
        // 
        _labelTimeout.AutoSizeMode = TAutoSize.Auto;
        _labelTimeout.Font = new Font("Segoe UI", 9F);
        _labelTimeout.ForeColor = Color.Gray;
        _labelTimeout.Location = new Point(3, 63);
        _labelTimeout.Name = "_labelTimeout";
        _labelTimeout.Size = new Size(53, 16);
        _labelTimeout.TabIndex = 8;
        _labelTimeout.Text = "连接超时:";
        // 
        // _valueTimeout
        // 
        _valueTimeout.AutoSizeMode = TAutoSize.Auto;
        _valueTimeout.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _valueTimeout.ForeColor = Color.Black;
        _valueTimeout.Location = new Point(83, 63);
        _valueTimeout.Name = "_valueTimeout";
        _valueTimeout.Size = new Size(5, 16);
        _valueTimeout.TabIndex = 9;
        _valueTimeout.Text = "-";
        // 
        // _labelUpdateRate
        // 
        _labelUpdateRate.AutoSizeMode = TAutoSize.Auto;
        _labelUpdateRate.Font = new Font("Segoe UI", 9F);
        _labelUpdateRate.ForeColor = Color.Gray;
        _labelUpdateRate.Location = new Point(302, 63);
        _labelUpdateRate.Name = "_labelUpdateRate";
        _labelUpdateRate.Size = new Size(53, 16);
        _labelUpdateRate.TabIndex = 10;
        _labelUpdateRate.Text = "更新频率:";
        // 
        // _valueUpdateRate
        // 
        _valueUpdateRate.AutoSizeMode = TAutoSize.Auto;
        _valueUpdateRate.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _valueUpdateRate.ForeColor = Color.Black;
        _valueUpdateRate.Location = new Point(382, 63);
        _valueUpdateRate.Name = "_valueUpdateRate";
        _valueUpdateRate.Size = new Size(5, 16);
        _valueUpdateRate.TabIndex = 11;
        _valueUpdateRate.Text = "-";
        // 
        // _Divider
        // 
        _Divider.Dock = DockStyle.Top;
        _Divider.Location = new Point(10, 110);
        _Divider.Margin = new Padding(0);
        _Divider.Name = "_Divider";
        _Divider.Size = new Size(604, 10);
        _Divider.TabIndex = 1;
        // 
        // _fullTable2
        // 
        _fullTable2.ColumnCount = 4;
        _fullTable2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
        _fullTable2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _fullTable2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
        _fullTable2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _fullTable2.Controls.Add(_labelRow1Col1, 0, 0);
        _fullTable2.Controls.Add(_valueRow1Col1, 1, 0);
        _fullTable2.Controls.Add(_labelRow1Col2, 2, 0);
        _fullTable2.Controls.Add(_valueRow1Col2, 3, 0);
        _fullTable2.Controls.Add(_labelRow2Col1, 0, 1);
        _fullTable2.Controls.Add(_valueRow2Col1, 1, 1);
        _fullTable2.Controls.Add(_labelRow2Col2, 2, 1);
        _fullTable2.Controls.Add(_valueRow2Col2, 3, 1);
        _fullTable2.Controls.Add(_labelRow3Col1, 0, 2);
        _fullTable2.Controls.Add(_valueRow3Col1, 1, 2);
        _fullTable2.Controls.Add(_labelRow3Col2, 2, 2);
        _fullTable2.Controls.Add(_valueRow3Col2, 3, 2);
        _fullTable2.Dock = DockStyle.Fill;
        _fullTable2.Location = new Point(13, 123);
        _fullTable2.Name = "_fullTable2";
        _fullTable2.RowCount = 3;
        _fullTable2.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _fullTable2.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _fullTable2.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        _fullTable2.Size = new Size(598, 84);
        _fullTable2.TabIndex = 2;
        // 
        // _labelRow1Col1
        // 
        _labelRow1Col1.AutoSizeMode = TAutoSize.Auto;
        _labelRow1Col1.Font = new Font("Segoe UI", 9F);
        _labelRow1Col1.ForeColor = Color.Gray;
        _labelRow1Col1.Location = new Point(3, 3);
        _labelRow1Col1.Name = "_labelRow1Col1";
        _labelRow1Col1.Size = new Size(13, 16);
        _labelRow1Col1.TabIndex = 0;
        _labelRow1Col1.Text = "IP:";
        // 
        // _valueRow1Col1
        // 
        _valueRow1Col1.AutoSizeMode = TAutoSize.Auto;
        _valueRow1Col1.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _valueRow1Col1.ForeColor = Color.Black;
        _valueRow1Col1.Location = new Point(73, 3);
        _valueRow1Col1.Name = "_valueRow1Col1";
        _valueRow1Col1.Size = new Size(5, 16);
        _valueRow1Col1.TabIndex = 1;
        _valueRow1Col1.Text = "-";
        // 
        // _labelRow1Col2
        // 
        _labelRow1Col2.AutoSizeMode = TAutoSize.Auto;
        _labelRow1Col2.Font = new Font("Segoe UI", 9F);
        _labelRow1Col2.ForeColor = Color.Gray;
        _labelRow1Col2.Location = new Point(302, 3);
        _labelRow1Col2.Name = "_labelRow1Col2";
        _labelRow1Col2.Size = new Size(28, 16);
        _labelRow1Col2.TabIndex = 2;
        _labelRow1Col2.Text = "端口:";
        // 
        // _valueRow1Col2
        // 
        _valueRow1Col2.AutoSizeMode = TAutoSize.Auto;
        _valueRow1Col2.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _valueRow1Col2.ForeColor = Color.Black;
        _valueRow1Col2.Location = new Point(372, 3);
        _valueRow1Col2.Name = "_valueRow1Col2";
        _valueRow1Col2.Size = new Size(5, 16);
        _valueRow1Col2.TabIndex = 3;
        _valueRow1Col2.Text = "-";
        // 
        // _labelRow2Col1
        // 
        _labelRow2Col1.AutoSizeMode = TAutoSize.Auto;
        _labelRow2Col1.Font = new Font("Segoe UI", 9F);
        _labelRow2Col1.ForeColor = Color.Gray;
        _labelRow2Col1.Location = new Point(3, 33);
        _labelRow2Col1.Name = "_labelRow2Col1";
        _labelRow2Col1.Size = new Size(28, 16);
        _labelRow2Col1.TabIndex = 4;
        _labelRow2Col1.Text = "模式:";
        // 
        // _valueRow2Col1
        // 
        _valueRow2Col1.AutoSizeMode = TAutoSize.Auto;
        _valueRow2Col1.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _valueRow2Col1.ForeColor = Color.Black;
        _valueRow2Col1.Location = new Point(73, 33);
        _valueRow2Col1.Name = "_valueRow2Col1";
        _valueRow2Col1.Size = new Size(5, 16);
        _valueRow2Col1.TabIndex = 5;
        _valueRow2Col1.Text = "-";
        // 
        // _labelRow2Col2
        // 
        _labelRow2Col2.AutoSizeMode = TAutoSize.Auto;
        _labelRow2Col2.Font = new Font("Segoe UI", 9F);
        _labelRow2Col2.ForeColor = Color.Gray;
        _labelRow2Col2.Location = new Point(302, 33);
        _labelRow2Col2.Name = "_labelRow2Col2";
        _labelRow2Col2.Size = new Size(53, 16);
        _labelRow2Col2.TabIndex = 6;
        _labelRow2Col2.Text = "连接模式:";
        // 
        // _valueRow2Col2
        // 
        _valueRow2Col2.AutoSizeMode = TAutoSize.Auto;
        _valueRow2Col2.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _valueRow2Col2.ForeColor = Color.Black;
        _valueRow2Col2.Location = new Point(372, 33);
        _valueRow2Col2.Name = "_valueRow2Col2";
        _valueRow2Col2.Size = new Size(5, 16);
        _valueRow2Col2.TabIndex = 7;
        _valueRow2Col2.Text = "-";
        // 
        // _labelRow3Col1
        // 
        _labelRow3Col1.AutoSizeMode = TAutoSize.Auto;
        _labelRow3Col1.Font = new Font("Segoe UI", 9F);
        _labelRow3Col1.ForeColor = Color.Gray;
        _labelRow3Col1.Location = new Point(3, 63);
        _labelRow3Col1.Name = "_labelRow3Col1";
        _labelRow3Col1.Size = new Size(16, 16);
        _labelRow3Col1.TabIndex = 8;
        _labelRow3Col1.Text = "T3:";
        // 
        // _valueRow3Col1
        // 
        _valueRow3Col1.AutoSizeMode = TAutoSize.Auto;
        _valueRow3Col1.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _valueRow3Col1.ForeColor = Color.Black;
        _valueRow3Col1.Location = new Point(73, 63);
        _valueRow3Col1.Name = "_valueRow3Col1";
        _valueRow3Col1.Size = new Size(5, 16);
        _valueRow3Col1.TabIndex = 9;
        _valueRow3Col1.Text = "-";
        // 
        // _labelRow3Col2
        // 
        _labelRow3Col2.AutoSizeMode = TAutoSize.Auto;
        _labelRow3Col2.Font = new Font("Segoe UI", 9F);
        _labelRow3Col2.ForeColor = Color.Gray;
        _labelRow3Col2.Location = new Point(302, 63);
        _labelRow3Col2.Name = "_labelRow3Col2";
        _labelRow3Col2.Size = new Size(16, 16);
        _labelRow3Col2.TabIndex = 10;
        _labelRow3Col2.Text = "T4:";
        // 
        // _valueRow3Col2
        // 
        _valueRow3Col2.AutoSizeMode = TAutoSize.Auto;
        _valueRow3Col2.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _valueRow3Col2.ForeColor = Color.Black;
        _valueRow3Col2.Location = new Point(372, 63);
        _valueRow3Col2.Name = "_valueRow3Col2";
        _valueRow3Col2.Size = new Size(5, 16);
        _valueRow3Col2.TabIndex = 11;
        _valueRow3Col2.Text = "-";
        // 
        // _valueOnline
        // 
        _valueOnline.AutoSizeMode = TAutoSize.Auto;
        _valueOnline.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _valueOnline.ForeColor = Color.Black;
        _valueOnline.Location = new Point(382, 33);
        _valueOnline.Name = "_valueOnline";
        _valueOnline.Size = new Size(5, 16);
        _valueOnline.TabIndex = 7;
        _valueOnline.Text = "-";
        // 
        // _logTextBox
        // 
        _logTextBox.BackColor = SystemColors.Window;
        _logTextBox.Dock = DockStyle.Fill;
        _logTextBox.Font = new Font("Consolas", 9F);
        _logTextBox.ForeColor = SystemColors.WindowText;
        _logTextBox.Location = new Point(0, 220);
        _logTextBox.Name = "_logTextBox";
        _logTextBox.ReadOnly = true;
        _logTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;
        _logTextBox.Size = new Size(624, 381);
        _logTextBox.TabIndex = 0;
        _logTextBox.Text = "";
        // 
        // _statusStrip
        // 
        _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel });
        _statusStrip.Location = new Point(0, 537);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.Size = new Size(640, 22);
        _statusStrip.TabIndex = 3;
        _statusStrip.Tag = "";
        _statusStrip.Visible = false;
        // 
        // _statusLabel
        // 
        _statusLabel.Font = new Font("Segoe UI", 9F);
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new Size(33, 17);
        _statusLabel.Text = "就绪";
        // 
        // DeviceInfo
        // 
        BackColor = Color.White;
        ClientSize = new Size(624, 601);
        Controls.Add(_logTextBox);
        Controls.Add(_fullPanel);
        Controls.Add(_cardPanel);
        Controls.Add(_statusStrip);
        Controls.Add(_statusTag);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "DeviceInfo";
        StartPosition = FormStartPosition.Manual;
        FormClosing += OnFormClosing;
        MouseMove += OnCardMouseMove;
        _cardPanel.ResumeLayout(false);
        _cardTable.ResumeLayout(false);
        _onlineStatusCardPanel.ResumeLayout(false);
        _onlineStatusCardPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_heartbeatIconCard).EndInit();
        ((System.ComponentModel.ISupportInitialize)_heartbeatIconFull).EndInit();
        _onlineStatusFullPanel.ResumeLayout(false);
        _onlineStatusFullPanel.PerformLayout();
        _fullPanel.ResumeLayout(false);
        _fullTable.ResumeLayout(false);
        _fullTable1.ResumeLayout(false);
        _fullTable1.PerformLayout();
        _fullTable2.ResumeLayout(false);
        _fullTable2.PerformLayout();
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}