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
    private PictureBox _heartbeatIcon; // 心跳图标
    
    // _cardTable 单元格控件 (3行5列)
    private AntdUI.Label _cardCell00;
    private AntdUI.Label _cardCell01;
    private AntdUI.Label _cardCell02;
    private AntdUI.Label _cardCell03;
    private AntdUI.Label _cardCell04;
    private AntdUI.Label _cardCell10;
    private AntdUI.Label _cardCell11;
    private AntdUI.Label _cardCell12;
    private AntdUI.Label _cardCell13;
    private AntdUI.Label _cardCell14;
    private AntdUI.Label _cardCell20;
    private AntdUI.Label _cardCell21;
    private AntdUI.Label _cardCell22;
    private AntdUI.Label _cardCell23;
    private AntdUI.Label _cardCell24;
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
        _statusTag = new Tag();
        _cardTable = new TableLayoutPanel();
        _heartbeatIcon = new PictureBox(); // 心跳图标
        _cardCell00 = new AntdUI.Label();
        _cardCell01 = new AntdUI.Label();
        _cardCell02 = new AntdUI.Label();
        _cardCell03 = new AntdUI.Label();
        _cardCell04 = new AntdUI.Label();
        _cardCell10 = new AntdUI.Label();
        _cardCell11 = new AntdUI.Label();
        _cardCell12 = new AntdUI.Label();
        _cardCell13 = new AntdUI.Label();
        _cardCell14 = new AntdUI.Label();
        _cardCell20 = new AntdUI.Label();
        _cardCell21 = new AntdUI.Label();
        _cardCell22 = new AntdUI.Label();
        _cardCell23 = new AntdUI.Label();
        _cardCell24 = new AntdUI.Label();
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
        _valueOnline = new AntdUI.Label();
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
        _logTextBox = new RichTextBox();
        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel();
        _cardPanel.SuspendLayout();
        _cardTable.SuspendLayout();
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
        _cardPanel.MouseDoubleClick += _cardPanel_MouseDoubleClick;
        _cardPanel.MouseDown += _cardPanel_MouseDown;
        _cardPanel.MouseUp += _cardPanel_MouseUp;
        // 
        // _statusTag
        // 
        _statusTag.Location = new Point(280, 16);
        _statusTag.Name = "_statusTag";
        _statusTag.Size = new Size(0, 0);
        _statusTag.TabIndex = 1;
        // 
        // _cardTable
        // 
        _cardTable.ColumnCount = 5;
        _cardTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
        _cardTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
        _cardTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
        _cardTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
        _cardTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
        _cardTable.Controls.Add(_cardCell00, 0, 0);
        _cardTable.Controls.Add(_cardCell01, 1, 0);
        _cardTable.Controls.Add(_cardCell02, 2, 0);
        _cardTable.Controls.Add(_cardCell03, 3, 0);
        _cardTable.Controls.Add(_cardCell04, 4, 0);
        _cardTable.Controls.Add(_cardCell10, 0, 1);
        _cardTable.Controls.Add(_cardCell11, 1, 1);
        _cardTable.Controls.Add(_cardCell12, 2, 1);
        _cardTable.Controls.Add(_cardCell13, 3, 1);
        _cardTable.Controls.Add(_heartbeatIcon, 4, 1); // 替换 _cardCell14 为心跳图标
        _cardTable.Controls.Add(_cardCell20, 0, 2);
        _cardTable.Controls.Add(_cardCell21, 1, 2);
        _cardTable.Controls.Add(_cardCell22, 2, 2);
        _cardTable.Controls.Add(_cardCell23, 3, 2);
        _cardTable.Controls.Add(_cardCell24, 4, 2);
        _cardTable.Dock = DockStyle.Fill;
        _cardTable.Location = new Point(16, 16);
        _cardTable.Name = "_cardTable";
        _cardTable.RowCount = 3;
        _cardTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        _cardTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        _cardTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        _cardTable.Size = new Size(592, 569);
        _cardTable.TabIndex = 2;
        _cardTable.MouseDoubleClick += _cardPanel_MouseDoubleClick;
        // 
        // _cardCell00
        // 
        _cardCell00.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _cardCell00.ForeColor = Color.FromArgb(102, 102, 102);
        _cardCell00.Location = new Point(3, 3);
        _cardCell00.Name = "_cardCell00";
        _cardCell00.Size = new Size(54, 15);
        _cardCell00.TabIndex = 0;
        _cardCell00.Text = "设备ID";
        // 
        // _cardCell01
        // 
        _cardCell01.Font = new Font("Segoe UI", 8F);
        _cardCell01.Location = new Point(63, 3);
        _cardCell01.Name = "_cardCell01";
        _cardCell01.Size = new Size(74, 15);
        _cardCell01.TabIndex = 1;
        _cardCell01.Text = "-";
        // 
        // _cardCell02
        // 
        _cardCell02.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _cardCell02.ForeColor = Color.FromArgb(102, 102, 102);
        _cardCell02.Location = new Point(143, 3);
        _cardCell02.Name = "_cardCell02";
        _cardCell02.Size = new Size(54, 15);
        _cardCell02.TabIndex = 2;
        _cardCell02.Text = "设备名称";
        // 
        // _cardCell03
        // 
        _cardCell03.Font = new Font("Segoe UI", 8F);
        _cardCell03.Location = new Point(203, 3);
        _cardCell03.Name = "_cardCell03";
        _cardCell03.Size = new Size(74, 15);
        _cardCell03.TabIndex = 3;
        _cardCell03.Text = "-";
        // 
        // _cardCell04
        // 
        _cardCell04.Font = new Font("Segoe UI", 8F);
        _cardCell04.Location = new Point(283, 3);
        _cardCell04.Name = "_cardCell04";
        _cardCell04.Size = new Size(74, 15);
        _cardCell04.TabIndex = 4;
        _cardCell04.Text = "";
        // 
        // _cardCell10
        // 
        _cardCell10.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _cardCell10.ForeColor = Color.FromArgb(102, 102, 102);
        _cardCell10.Location = new Point(3, 48);
        _cardCell10.Name = "_cardCell10";
        _cardCell10.Size = new Size(54, 15);
        _cardCell10.TabIndex = 5;
        _cardCell10.Text = "启用状态";
        // 
        // _cardCell11
        // 
        _cardCell11.Font = new Font("Segoe UI", 8F);
        _cardCell11.Location = new Point(63, 48);
        _cardCell11.Name = "_cardCell11";
        _cardCell11.Size = new Size(74, 15);
        _cardCell11.TabIndex = 6;
        _cardCell11.Text = "-";
        // 
        // _cardCell12
        // 
        _cardCell12.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _cardCell12.ForeColor = Color.FromArgb(102, 102, 102);
        _cardCell12.Location = new Point(143, 48);
        _cardCell12.Name = "_cardCell12";
        _cardCell12.Size = new Size(54, 15);
        _cardCell12.TabIndex = 7;
        _cardCell12.Text = "是否在线";
        // 
        // _cardCell13
        // 
        _cardCell13.Font = new Font("Segoe UI", 8F);
        _cardCell13.Location = new Point(203, 48);
        _cardCell13.Name = "_cardCell13";
        _cardCell13.Size = new Size(74, 15);
        _cardCell13.TabIndex = 8;
        _cardCell13.Text = "-";
        // 
        // _cardCell14
        // 
        _cardCell14.Font = new Font("Segoe UI", 8F);
        _cardCell14.Location = new Point(283, 48);
        _cardCell14.Name = "_cardCell14";
        _cardCell14.Size = new Size(74, 15);
        _cardCell14.TabIndex = 9;
        _cardCell14.Text = "";
        // 
        // _heartbeatIcon
        // 
        _heartbeatIcon.BackColor = Color.Transparent;
        _heartbeatIcon.Location = new Point(283, 48);
        _heartbeatIcon.Name = "_heartbeatIcon";
        _heartbeatIcon.Size = new Size(12, 12);
        _heartbeatIcon.TabIndex = 9;
        _heartbeatIcon.TabStop = false;
        _heartbeatIcon.Visible = true;
        // 
        // _cardCell20
        // 
        _cardCell20.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _cardCell20.ForeColor = Color.FromArgb(102, 102, 102);
        _cardCell20.Location = new Point(3, 93);
        _cardCell20.Name = "_cardCell20";
        _cardCell20.Size = new Size(54, 15);
        _cardCell20.TabIndex = 10;
        _cardCell20.Text = "IP";
        // 
        // _cardCell21
        // 
        _cardCell21.Font = new Font("Segoe UI", 8F);
        _cardCell21.Location = new Point(63, 93);
        _cardCell21.Name = "_cardCell21";
        _cardCell21.Size = new Size(74, 15);
        _cardCell21.TabIndex = 11;
        _cardCell21.Text = "-";
        // 
        // _cardCell22
        // 
        _cardCell22.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        _cardCell22.ForeColor = Color.FromArgb(102, 102, 102);
        _cardCell22.Location = new Point(143, 93);
        _cardCell22.Name = "_cardCell22";
        _cardCell22.Size = new Size(54, 15);
        _cardCell22.TabIndex = 12;
        _cardCell22.Text = "端口";
        // 
        // _cardCell23
        // 
        _cardCell23.Font = new Font("Segoe UI", 8F);
        _cardCell23.Location = new Point(203, 93);
        _cardCell23.Name = "_cardCell23";
        _cardCell23.Size = new Size(74, 15);
        _cardCell23.TabIndex = 13;
        _cardCell23.Text = "-";
        // 
        // _cardCell24
        // 
        _cardCell24.Font = new Font("Segoe UI", 8F);
        _cardCell24.Location = new Point(283, 93);
        _cardCell24.Name = "_cardCell24";
        _cardCell24.Size = new Size(74, 15);
        _cardCell24.TabIndex = 14;
        _cardCell24.Text = "";
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
        _fullTable1.Controls.Add(_valueOnline, 3, 1);
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
        _valueId.Font = new Font("Segoe UI", 9F);
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
        _valueName.Font = new Font("Segoe UI", 9F);
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
        _valueEnabled.Font = new Font("Segoe UI", 9F);
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
        // _valueOnline
        // 
        _valueOnline.AutoSizeMode = TAutoSize.Auto;
        _valueOnline.Font = new Font("Segoe UI", 9F);
        _valueOnline.ForeColor = Color.Black;
        _valueOnline.Location = new Point(382, 33);
        _valueOnline.Name = "_valueOnline";
        _valueOnline.Size = new Size(5, 16);
        _valueOnline.TabIndex = 7;
        _valueOnline.Text = "-";
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
        _valueTimeout.Font = new Font("Segoe UI", 9F);
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
        _valueUpdateRate.Font = new Font("Segoe UI", 9F);
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
        _fullTable2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
        _fullTable2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        _fullTable2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
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
        _valueRow1Col1.Font = new Font("Segoe UI", 9F);
        _valueRow1Col1.ForeColor = Color.Black;
        _valueRow1Col1.Location = new Point(83, 3);
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
        _valueRow1Col2.Font = new Font("Segoe UI", 9F);
        _valueRow1Col2.ForeColor = Color.Black;
        _valueRow1Col2.Location = new Point(382, 3);
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
        _valueRow2Col1.Font = new Font("Segoe UI", 9F);
        _valueRow2Col1.ForeColor = Color.Black;
        _valueRow2Col1.Location = new Point(83, 33);
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
        _valueRow2Col2.Font = new Font("Segoe UI", 9F);
        _valueRow2Col2.ForeColor = Color.Black;
        _valueRow2Col2.Location = new Point(382, 33);
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
        _valueRow3Col1.Font = new Font("Segoe UI", 9F);
        _valueRow3Col1.ForeColor = Color.Black;
        _valueRow3Col1.Location = new Point(83, 63);
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
        _valueRow3Col2.Font = new Font("Segoe UI", 9F);
        _valueRow3Col2.ForeColor = Color.Black;
        _valueRow3Col2.Location = new Point(382, 63);
        _valueRow3Col2.Name = "_valueRow3Col2";
        _valueRow3Col2.Size = new Size(5, 16);
        _valueRow3Col2.TabIndex = 11;
        _valueRow3Col2.Text = "-";
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
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "DeviceInfo";
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        FormClosing += DeviceInfo_FormClosing;
        MouseMove += DeviceInfo_MouseMove;
        _cardPanel.ResumeLayout(false);
        _cardTable.ResumeLayout(false);
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