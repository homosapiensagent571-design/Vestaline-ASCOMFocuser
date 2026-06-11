using System;
using System.Drawing;
using System.Windows.Forms;

public class MainForm : Form
{
    private Config _config;
    private SerialService _serial;
    private ASCOMFocuserService _ascom;        // NEW: ASCOM mode service
    private Timer _pollTimer;
    private int _currentPosition;
    private bool _isMoving;

    // ── 控件声明 ───────────────────────────────────────

    // 左面板 - 当前位置
    private Label lblPosition, lblTemp, lblStatus, lblFocalPlane;
    private PictureBox pbStatusDot;

    // 左面板 - 点动按钮
    private Button btnBigIn, btnBigOut, btnMidIn, btnMidOut, btnSmallIn, btnSmallOut;
    private Button btnFocalSave, btnFocalReturn;
    private TextBox txtGotoPos;
    private Button btnGoto;
    private Button btnHalt, btnHome, btnSetZero;
    private CheckBox chkReverseDir;

    // 右面板 - 连接
    private RadioButton rbDirect, rbASCOM;        // NEW: mode toggle
    private ComboBox cbPort, cbBaudRate, cbASCOMProgID;  // cbASCOMProgID NEW
    private Button btnScan, btnConnect, btnDisconnect;

    // 右面板 - 设置
    private NumericUpDown numSmallStep, numMidStep, numBigStep, numMaxTravel;
    private CheckBox chkAutoConnect;
    private Button btnSaveSettings;

    // 日志 Tab
    private RichTextBox rtLog;
    private CheckBox chkAutoScroll;
    private Button btnClearLog, btnCopyLog;

    // 状态栏
    private StatusStrip statusStrip;
    private ToolStripStatusLabel sbStatus, sbPosTemp;

    // ── 构造函数 ────────────────────────────────────────

    public MainForm()
    {
        _config = Config.Load("autofocus.config");
        _serial = new SerialService();
        _serial.OnLog += Serial_OnLog;

        _ascom = new ASCOMFocuserService();       // NEW
        _ascom.OnLog += Serial_OnLog;             // NEW: share same log handler

        Text = "VestalFocuser beta 0.6.10";
        Size = new Size(900, 700);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 600);
        FormClosing += MainForm_FormClosing;

        BuildUI();
        LoadSettingsToUI();
        UpdateConnectionState(false);

        _pollTimer = new Timer { Interval = 500 };
        _pollTimer.Tick += PollTimer_Tick;

        if (_config.AutoConnect) this.Load += (s, e) => AutoConnect();
    }

    // ── 主界面构建 ──────────────────────────────────────

    private void BuildUI()
    {
        var tabControl = new TabControl { Dock = DockStyle.Fill };
        Controls.Add(tabControl);
        Controls.Add(CreateStatusBar());

        // ── 控制台 Tab ──
        var tabConsole = new TabPage("控制台");
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            IsSplitterFixed = true,
            FixedPanel = FixedPanel.Panel1
        };
        split.Panel1.BackColor = Color.White;
        split.Panel2.BackColor = Color.White;
        BuildControlPanel(split.Panel1);
        BuildDevicePanel(split.Panel2);
        tabConsole.Controls.Add(split);
        tabControl.TabPages.Add(tabConsole);

        this.Load += (sender, e) =>
        {
            split.SplitterDistance = 490;
            split.Panel1MinSize = 400;
            split.Panel2MinSize = 200;
        };

        // ── 日志 Tab ──
        var tabLog = new TabPage("日志");
        BuildLogTab(tabLog);
        tabControl.TabPages.Add(tabLog);
    }

    // ── 左面板：当前位置 + 手动控制 ──────────────────────

    private void BuildControlPanel(Control parent)
    {
        int y = 10;

        // 状态圆点
        pbStatusDot = new PictureBox
        {
            Location = new Point(14, y + 16),
            Size = new Size(24, 24),
            SizeMode = PictureBoxSizeMode.CenterImage
        };
        pbStatusDot.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Color c = _isMoving ? Color.OrangeRed : Color.FromArgb(0, 120, 212);
            using (var b = new SolidBrush(c))
                e.Graphics.FillEllipse(b, 2, 2, 20, 20);
        };
        parent.Controls.Add(pbStatusDot);

        // 位置数字
        lblPosition = new Label
        {
            Text = "0",
            Location = new Point(46, y + 10),
            Size = new Size(160, 38),
            Font = new Font("Microsoft YaHei", 26, FontStyle.Bold),
            ForeColor = Color.Black
        };
        parent.Controls.Add(lblPosition);

        AddLabel(parent, "步", 210, y + 28, 30, 18);

        // 温度
        lblTemp = new Label
        {
            Text = "温度: -- °C",
            Location = new Point(14, y + 56),
            Size = new Size(220, 20),
            Font = new Font("Microsoft YaHei", 11),
            ForeColor = Color.DarkOrange
        };
        parent.Controls.Add(lblTemp);

        // 状态
        lblStatus = new Label
        {
            Text = "空闲",
            Location = new Point(400, y + 14),
            Size = new Size(60, 24),
            Font = new Font("Microsoft YaHei", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 120, 212),
            TextAlign = ContentAlignment.MiddleCenter
        };
        lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        parent.Controls.Add(lblStatus);

        // 分割线
        y += 95;
        var line = new Label
        {
            Location = new Point(4, y),
            Size = new Size(470, 2),
            BorderStyle = BorderStyle.Fixed3D,
            BackColor = Color.LightGray
        };
        parent.Controls.Add(line);

        // ── 点动控制 ──
        Label lblJog = new Label
        {
            Text = "点动控制",
            Location = new Point(12, y + 10),
            Font = new Font("Microsoft YaHei", 11, FontStyle.Bold),
            Size = new Size(80, 20)
        };
        parent.Controls.Add(lblJog);

        y += 35;
        // 点动按钮 — 使用设置中的步长值
        AddLabel(parent, "大", 14, y + 5, 48, 20, ContentAlignment.MiddleRight);
        btnBigIn = StyledButton("< " + _config.BigStep, 70, y, 70, 26, Color.White, Color.Black);
        btnBigOut = StyledButton(_config.BigStep + " >", 145, y, 70, 26, Color.White, Color.Black);
        btnBigIn.Click += (s, ev) => MoveRelative(-_config.BigStep);
        btnBigOut.Click += (s, ev) => MoveRelative(_config.BigStep);
        parent.Controls.Add(btnBigIn);
        parent.Controls.Add(btnBigOut);
        y += 31;

        AddLabel(parent, "中", 14, y + 5, 48, 20, ContentAlignment.MiddleRight);
        btnMidIn = StyledButton("< " + _config.MidStep, 70, y, 70, 26, Color.White, Color.Black);
        btnMidOut = StyledButton(_config.MidStep + " >", 145, y, 70, 26, Color.White, Color.Black);
        btnMidIn.Click += (s, ev) => MoveRelative(-_config.MidStep);
        btnMidOut.Click += (s, ev) => MoveRelative(_config.MidStep);
        parent.Controls.Add(btnMidIn);
        parent.Controls.Add(btnMidOut);
        y += 31;

        AddLabel(parent, "小", 14, y + 5, 48, 20, ContentAlignment.MiddleRight);
        btnSmallIn = StyledButton("< " + _config.SmallStep, 70, y, 70, 26, Color.White, Color.Black);
        btnSmallOut = StyledButton(_config.SmallStep + " >", 145, y, 70, 26, Color.White, Color.Black);
        btnSmallIn.Click += (s, ev) => MoveRelative(-_config.SmallStep);
        btnSmallOut.Click += (s, ev) => MoveRelative(_config.SmallStep);
        parent.Controls.Add(btnSmallIn);
        parent.Controls.Add(btnSmallOut);
        y += 31;

        // 焦平面
        y += 5;
        lblFocalPlane = new Label
        {
            Text = "焦平面: " + (_config.FocalPlane >= 0 ? _config.FocalPlane.ToString() : "未设置"),
            Location = new Point(12, y),
            Size = new Size(180, 22),
            ForeColor = _config.FocalPlane >= 0 ? Color.Black : Color.Gray
        };
        parent.Controls.Add(lblFocalPlane);

        btnFocalSave = new Button { Text = "保存", Location = new Point(195, y - 1), Size = new Size(55, 24) };
        btnFocalSave.Click += (s, e) => SaveFocalPlane();
        parent.Controls.Add(btnFocalSave);

        btnFocalReturn = new Button { Text = "返回", Location = new Point(255, y - 1), Size = new Size(55, 24) };
        btnFocalReturn.Click += (s, e) => GotoFocalPlane();
        parent.Controls.Add(btnFocalReturn);

        // ── 定位到 ──
        y += 32;
        var line2 = new Label
        {
            Location = new Point(4, y),
            Size = new Size(470, 2),
            BorderStyle = BorderStyle.Fixed3D,
            BackColor = Color.LightGray
        };
        parent.Controls.Add(line2);

        y += 10;
        AddLabel(parent, "前往:", 12, y + 5, 40, 20);
        txtGotoPos = new TextBox { Text = "3600", Location = new Point(55, y + 2), Size = new Size(80, 22) };
        parent.Controls.Add(txtGotoPos);
        btnGoto = StyledButton("执行", 145, y, 65, 28, Color.FromArgb(0, 120, 212), Color.White);
        btnGoto.Click += (s, e) => {
            int target;
            if (int.TryParse(txtGotoPos.Text, out target))
                GotoPosition(target);
        };
        parent.Controls.Add(btnGoto);

        // ── 操作按钮 ──
        y += 40;
        btnHalt = StyledButton("HALT", 12, y, 85, 32, Color.DarkRed, Color.White);
        btnHalt.Click += (s, e) => {
            int posBefore = _currentPosition;
            GetActiveService().SendCommand(":FQ#", false);
            AddLog("── HALT (位置: " + posBefore + ") ──", LogType.Info);
        };
        parent.Controls.Add(btnHalt);

        btnHome = StyledButton("归零", 105, y, 85, 32, Color.FromArgb(0, 120, 212), Color.White);
        btnHome.Click += (s, e) => GotoPosition(0);
        parent.Controls.Add(btnHome);

        btnSetZero = StyledButton("设为零位", 198, y, 85, 32, Color.SteelBlue, Color.White);
        btnSetZero.Click += (s, e) => { GotoPosition(0); };
        parent.Controls.Add(btnSetZero);

        // ── 反转方向 ──
        y += 42;
        chkReverseDir = new CheckBox
        {
            Text = "反转方向",
            Location = new Point(14, y),
            Size = new Size(80, 22)
        };
        chkReverseDir.CheckedChanged += (s, e) =>
        {
            AddLog("反转方向: " + (chkReverseDir.Checked ? "开启" : "关闭"), LogType.Info);
        };
        parent.Controls.Add(chkReverseDir);
    }

    // ── 右面板：连接 + 设置 ─────────────────────────────

    private void BuildDevicePanel(Control parent)
    {
        int y = 10;

        // 设备连接
        var gbConn = new GroupBox { Text = "设备连接", Location = new Point(10, y), Size = new Size(248, 160) };

        // Mode toggle
        rbDirect = new RadioButton { Text = "直连串口 (Vestaline)", Location = new Point(14, 22), Size = new Size(160, 20), Checked = !_config.UseASCOM };
        rbASCOM  = new RadioButton { Text = "ASCOM 驱动", Location = new Point(14, 42), Size = new Size(100, 20), Checked = _config.UseASCOM };
        rbDirect.CheckedChanged += ModeChanged;
        rbASCOM.CheckedChanged += ModeChanged;
        gbConn.Controls.Add(rbDirect);
        gbConn.Controls.Add(rbASCOM);

        // Serial port panel
        int connY = 67;

        AddLabel(gbConn, "串口号:", 12, connY, 50, 20);
        cbPort = new ComboBox { Location = new Point(65, connY - 2), Size = new Size(100, 22), DropDownStyle = ComboBoxStyle.DropDownList };
        gbConn.Controls.Add(cbPort);
        btnScan = new Button { Text = "扫描", Location = new Point(170, connY - 3), Size = new Size(60, 24) };
        btnScan.Click += (s, e) => ScanPorts();
        gbConn.Controls.Add(btnScan);

        connY += 30;
        AddLabel(gbConn, "波特率:", 12, connY, 50, 20);
        cbBaudRate = new ComboBox { Location = new Point(65, connY - 2), Size = new Size(100, 22), DropDownStyle = ComboBoxStyle.DropDownList };
        cbBaudRate.Items.AddRange(new object[] { "115200", "57600", "38400", "19200", "9600" });
        cbBaudRate.SelectedIndex = 0;
        gbConn.Controls.Add(cbBaudRate);

        // ASCOM ProgID panel
        AddLabel(gbConn, "驱动:", 12, connY, 50, 20);
        cbASCOMProgID = new ComboBox { Location = new Point(65, connY - 2), Size = new Size(165, 22), DropDownStyle = ComboBoxStyle.DropDownList };
        cbASCOMProgID.Visible = _config.UseASCOM;
        gbConn.Controls.Add(cbASCOMProgID);

        connY += 35;
        btnConnect = StyledButton("连接", 14, connY, 82, 28, Color.FromArgb(0, 120, 212), Color.White);
        btnConnect.Click += (s, e) => ConnectDevice();
        gbConn.Controls.Add(btnConnect);

        btnDisconnect = StyledButton("断开", 102, connY, 82, 28, Color.White, Color.Black);
        btnDisconnect.Click += (s, e) => DisconnectDevice();
        gbConn.Controls.Add(btnDisconnect);

        parent.Controls.Add(gbConn);

        // 设置
        y += 170;
        var gbSettings = new GroupBox { Text = "设置", Location = new Point(10, y), Size = new Size(248, 280) };

        int sy = 20;
        AddLabel(gbSettings, "点动步数", 12, sy, 80, 20, fontBold: true);
        sy += 25;
        AddLabel(gbSettings, "小 (步):", 20, sy + 5, 48, 20);
        numSmallStep = new NumericUpDown { Location = new Point(72, sy + 3), Size = new Size(70, 22), Minimum = 1, Maximum = 99999 };
        gbSettings.Controls.Add(numSmallStep);
        sy += 28;
        AddLabel(gbSettings, "中 (步):", 20, sy + 5, 48, 20);
        numMidStep = new NumericUpDown { Location = new Point(72, sy + 3), Size = new Size(70, 22), Minimum = 1, Maximum = 99999 };
        gbSettings.Controls.Add(numMidStep);
        sy += 28;
        AddLabel(gbSettings, "大 (步):", 20, sy + 5, 48, 20);
        numBigStep = new NumericUpDown { Location = new Point(72, sy + 3), Size = new Size(70, 22), Minimum = 1, Maximum = 99999 };
        gbSettings.Controls.Add(numBigStep);

        sy += 38;
        var line3 = new Label
        {
            Location = new Point(10, sy),
            Size = new Size(225, 2),
            BorderStyle = BorderStyle.Fixed3D,
            BackColor = Color.LightGray
        };
        gbSettings.Controls.Add(line3);

        sy += 10;
        AddLabel(gbSettings, "硬件", 12, sy, 60, 20, fontBold: true);
        sy += 25;
        AddLabel(gbSettings, "最大行程 (步):", 20, sy + 5, 85, 20);
        numMaxTravel = new NumericUpDown { Location = new Point(108, sy + 3), Size = new Size(75, 22), Minimum = 100, Maximum = 16384 };
        gbSettings.Controls.Add(numMaxTravel);
        sy += 28;
        chkAutoConnect = new CheckBox { Text = "启动时自动连接", Location = new Point(20, sy), Size = new Size(130, 22) };
        gbSettings.Controls.Add(chkAutoConnect);

        sy += 38;
        btnSaveSettings = StyledButton("保存设置", 40, sy, 140, 32, Color.FromArgb(0, 120, 212), Color.White);
        btnSaveSettings.Click += (s, e) => SaveSettings();
        gbSettings.Controls.Add(btnSaveSettings);

        parent.Controls.Add(gbSettings);
    }

    // ── 日志 Tab ─────────────────────────────────────────

    private void BuildLogTab(TabPage tab)
    {
        rtLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.White,
            Font = new Font("Consolas", 9),
            WordWrap = false
        };
        tab.Controls.Add(rtLog);

        var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 36 };
        chkAutoScroll = new CheckBox { Text = "自动回卷", Location = new Point(8, 8), Size = new Size(80, 22), Checked = true };
        pnlBottom.Controls.Add(chkAutoScroll);

        btnClearLog = new Button { Text = "清空日志", Location = new Point(100, 6), Size = new Size(75, 26) };
        btnClearLog.Click += (s, e) => { rtLog.Clear(); };
        pnlBottom.Controls.Add(btnClearLog);

        btnCopyLog = new Button { Text = "复制全部", Location = new Point(182, 6), Size = new Size(75, 26) };
        btnCopyLog.Click += (s, e) => CopyLog();
        pnlBottom.Controls.Add(btnCopyLog);

        var btnSaveLog = new Button { Text = "保存日志", Location = new Point(264, 6), Size = new Size(75, 26) };
        btnSaveLog.Click += (s, e) => SaveLog();
        pnlBottom.Controls.Add(btnSaveLog);

        tab.Controls.Add(pnlBottom);
    }

    // ── 状态栏 ───────────────────────────────────────────

    private StatusStrip CreateStatusBar()
    {
        statusStrip = new StatusStrip();
        sbStatus = new ToolStripStatusLabel("串口已断开");
        sbPosTemp = new ToolStripStatusLabel("位置: 0 步 | 温度: -- °C") { Alignment = ToolStripItemAlignment.Right };
        statusStrip.Items.Add(sbStatus);
        statusStrip.Items.Add(sbPosTemp);
        return statusStrip;
    }

    // ── 模式切换 ─────────────────────────────────────────

    private void ModeChanged(object sender, EventArgs e)
    {
        bool ascom = rbASCOM.Checked;
        cbPort.Visible = !ascom;
        cbBaudRate.Visible = !ascom;
        btnScan.Visible = !ascom;
        cbASCOMProgID.Visible = ascom;

        _config.UseASCOM = ascom;
    }

    // ── 串口操作 ─────────────────────────────────────────

    private void ConnectDevice()
    {
        try
        {
            if (rbASCOM.Checked)
            {
                if (cbASCOMProgID.SelectedItem == null) { AddLog("请选择 ASCOM 驱动", LogType.Error); return; }
                string progId = cbASCOMProgID.SelectedItem.ToString();
                AddLog("══════ ASCOM 模式·连接 ══════", LogType.Info);
                AddLog("驱动: " + progId, LogType.Info);
                _ascom.Connect(progId);
                UpdateConnectionState(true);
                _pollTimer.Start();
                sbStatus.Text = "ASCOM: " + _ascom.PortName;
                AddLog("已连接 — " + _ascom.PortName, LogType.Info);
                AddLog("══════════════════════════", LogType.Info);
            }
            else
            {
                if (cbPort.SelectedItem == null) { AddLog("请选择串口", LogType.Error); return; }
                string port = cbPort.SelectedItem.ToString();
                int baud = int.Parse(cbBaudRate.SelectedItem.ToString());
                AddLog("══════ 直连模式·连接 ══════", LogType.Info);
                AddLog("串口: " + port + " @ " + baud + " bps", LogType.Info);
                _serial.Connect(port, baud);
                UpdateConnectionState(true);
                _pollTimer.Start();
                sbStatus.Text = "已连接 " + port + "@" + baud;
                AddLog("已连接 — Vestaline 协议直连", LogType.Info);
                AddLog("══════════════════════════", LogType.Info);
            }
        }
        catch (Exception ex)
        {
            AddLog("连接失败: " + ex.Message, LogType.Error);
        }
    }

    private void DisconnectDevice()
    {
        _pollTimer.Stop();
        AddLog("── 断开连接 ──", LogType.Info);
        if (rbASCOM.Checked)
            _ascom.Disconnect();
        else
            _serial.Disconnect();
        _currentPosition = 0;
        _isMoving = false;
        UpdateConnectionState(false);
        sbStatus.Text = "串口已断开";
        sbPosTemp.Text = "位置: -- 步 | 温度: -- °C";
        lblTemp.Text = "温度: -- °C";
        lblPosition.Text = "0";
        lblStatus.Text = "空闲";
        System.Threading.Thread.Sleep(500);
    }

    private void AutoConnect()
    {
        if (!_config.AutoConnect) return;
        if (_config.UseASCOM && !string.IsNullOrEmpty(_config.ASCOMProgID))
        {
            foreach (var item in cbASCOMProgID.Items)
            {
                if (item.ToString() == _config.ASCOMProgID)
                {
                    cbASCOMProgID.SelectedItem = item;
                    ConnectDevice();
                    break;
                }
            }
        }
        else if (!string.IsNullOrEmpty(_config.PortName))
        {
            foreach (var item in cbPort.Items)
            {
                if (item.ToString() == _config.PortName)
                {
                    cbPort.SelectedItem = item;
                    ConnectDevice();
                    break;
                }
            }
        }
    }

    private void ScanPorts()
    {
        cbPort.Items.Clear();
        var ports = _serial.GetAvailablePorts();
        foreach (var p in ports) cbPort.Items.Add(p);
        if (ports.Length > 0) cbPort.SelectedIndex = 0;
        else AddLog("未扫描到可用串口", LogType.Info);
    }

    private DateTime _moveStartTime;

    private void MoveRelative(int steps)
    {
        try
        {
            int dir = chkReverseDir.Checked ? -1 : 1;
            int actual = steps * dir;
            int target = _currentPosition + actual;
            if (!CheckBounds(target)) return;

            AddLog(string.Format("── 点动: {0} → {1} (Δ={2})", _currentPosition, target, actual), LogType.Info);
            var svc = GetActiveService();
            svc.SendCommand(":SN" + target.ToString("X8") + "#", false);
            svc.SendCommand(":FG#", false);
            _moveStartTime = DateTime.Now;
            _currentPosition = target;
            lblPosition.Text = _currentPosition.ToString();
        }
        catch (Exception ex)
        {
            AddLog("移动失败: " + ex.Message, LogType.Error);
        }
    }

    private void GotoPosition(int target)
    {
        try
        {
            if (!CheckBounds(target)) return;
            int delta = target - _currentPosition;
            AddLog(string.Format("── 前往: {0} → {1} (Δ={2})", _currentPosition, target, delta), LogType.Info);
            var svc = GetActiveService();
            svc.SendCommand(":SN" + target.ToString("X8") + "#", false);
            svc.SendCommand(":FG#", false);
            _moveStartTime = DateTime.Now;
            _currentPosition = target;
            lblPosition.Text = _currentPosition.ToString();
        }
        catch (Exception ex)
        {
            AddLog("移动失败: " + ex.Message, LogType.Error);
        }
    }

    private IFocusService GetActiveService()
    {
        return rbASCOM.Checked ? (IFocusService)_ascom : (IFocusService)_serial;
    }

    private bool CheckBounds(int target)
    {
        if (target < 0)
        {
            ShowWarn(string.Format("超出零位！\n\n目标位置: {0}\n当  前: {1}", target, _currentPosition));
            return false;
        }
        if (target > _config.MaxTravel)
        {
            ShowWarn(string.Format("超出限位！\n\n目标位置: {0}\n限  位: {1}\n当  前: {2}", target, _config.MaxTravel, _currentPosition));
            return false;
        }
        return true;
    }

    private static void ShowWarn(string message)
    {
        var f = new Form
        {
            Text = "限位警告",
            Size = new Size(380, 240),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            TopMost = true
        };
        var lbl = new Label
        {
            Text = message,
            Location = new Point(24, 16),
            Size = new Size(316, 130),
            Font = new Font("Microsoft YaHei", 14),
            TextAlign = ContentAlignment.MiddleCenter
        };
        var btn = new Button
        {
            Text = "确定",
            Location = new Point(150, 156),
            Size = new Size(80, 32),
            Font = new Font("Microsoft YaHei", 11)
        };
        btn.Click += (s, e) => f.Close();
        f.Controls.Add(lbl);
        f.Controls.Add(btn);
        f.ShowDialog();
    }

    // ── 焦平面 ──────────────────────────────────────────

    private string _focalPlaneValue
    {
        get { return _config.FocalPlane >= 0 ? _config.FocalPlane.ToString() : "未设置"; }
    }

    private void SaveFocalPlane()
    {
        _config.FocalPlane = _currentPosition;
        lblFocalPlane.Text = "焦平面: " + _currentPosition.ToString();
        lblFocalPlane.ForeColor = Color.Black;
        Config.Save("autofocus.config", _config);
        AddLog("★ 焦平面已保存: " + _currentPosition + " 步", LogType.Info);
    }

    private void GotoFocalPlane()
    {
        if (_config.FocalPlane < 0)
        {
            AddLog("焦平面未设置", LogType.Error);
            return;
        }
        GotoPosition(_config.FocalPlane);
    }

    // ── 设置 ────────────────────────────────────────────

    private void LoadSettingsToUI()
    {
        if (cbPort.Items.Count == 0) ScanPorts();
        for (int i = 0; i < cbPort.Items.Count; i++)
            if (cbPort.Items[i].ToString() == _config.PortName) { cbPort.SelectedIndex = i; break; }
        for (int i = 0; i < cbBaudRate.Items.Count; i++)
            if (cbBaudRate.Items[i].ToString() == _config.BaudRate.ToString()) { cbBaudRate.SelectedIndex = i; break; }
        rbASCOM.Checked = _config.UseASCOM;
        rbDirect.Checked = !_config.UseASCOM;

        // Populate ASCOM ProgID dropdown
        cbASCOMProgID.Items.Clear();
        try
        {
            var ids = _ascom.GetAvailableProgIds();
            foreach (var id in ids) cbASCOMProgID.Items.Add(id);
        }
        catch { }
        for (int i = 0; i < cbASCOMProgID.Items.Count; i++)
            if (cbASCOMProgID.Items[i].ToString() == _config.ASCOMProgID) { cbASCOMProgID.SelectedIndex = i; break; }
        if (cbASCOMProgID.SelectedIndex < 0 && cbASCOMProgID.Items.Count > 0)
            cbASCOMProgID.SelectedIndex = 0;

        numSmallStep.Value = _config.SmallStep;
        numMidStep.Value = _config.MidStep;
        numBigStep.Value = _config.BigStep;
        numMaxTravel.Value = Math.Min(_config.MaxTravel, 16384);
        chkAutoConnect.Checked = _config.AutoConnect;
        lblFocalPlane.Text = "焦平面: " + _focalPlaneValue;
        lblFocalPlane.ForeColor = _config.FocalPlane >= 0 ? Color.Black : Color.Gray;

        ModeChanged(null, null);
    }

    private void SaveSettings()
    {
        int oldMaxTravel = _config.MaxTravel;
        int newMaxTravel = (int)numMaxTravel.Value;

        if (newMaxTravel < _currentPosition)
        {
            ShowWarn(string.Format("限位不能小于当前位置！\n\n限位: {0}\n当前位置: {1}", newMaxTravel, _currentPosition));
            numMaxTravel.Value = oldMaxTravel;
            return;
        }

        _config.SmallStep = (int)numSmallStep.Value;
        _config.MidStep = (int)numMidStep.Value;
        _config.BigStep = (int)numBigStep.Value;
        _config.MaxTravel = newMaxTravel;
        _config.AutoConnect = chkAutoConnect.Checked;
        _config.UseASCOM = rbASCOM.Checked;
        if (cbASCOMProgID.SelectedItem != null) _config.ASCOMProgID = cbASCOMProgID.SelectedItem.ToString();

        string oldPort = _config.PortName;
        int oldBaud = _config.BaudRate;
        if (cbPort.SelectedItem != null) _config.PortName = cbPort.SelectedItem.ToString();
        if (cbBaudRate.SelectedItem != null) _config.BaudRate = int.Parse(cbBaudRate.SelectedItem.ToString());

        Config.Save("autofocus.config", _config);
        AddLog("设置已保存", LogType.Info);

        UpdateJogButtons();

        if (IsServiceConnected() && (_config.PortName != oldPort || _config.BaudRate != oldBaud))
            AddLog("连接参数已变更，需重新连接", LogType.Info);
    }

    private bool IsServiceConnected()
    {
        if (rbASCOM.Checked) return _ascom.IsConnected;
        return _serial.IsConnected;
    }

    // ── 日志 ────────────────────────────────────────────

    private void Serial_OnLog(object sender, LogEventArgs e)
    {
        if (rtLog.IsDisposed) return;
        if (rtLog.InvokeRequired)
        {
            rtLog.BeginInvoke(new Action(() => AddLog(e.Message, e.Type)));
            return;
        }
        AddLog(e.Message, e.Type);
    }

    private void AddLog(string message, LogType type)
    {
        string time = DateTime.Now.ToString("HH:mm:ss");
        string prefix;
        Color color;

        switch (type)
        {
            case LogType.Send:
                prefix = "TX  "; color = Color.DodgerBlue; break;
            case LogType.Receive:
                prefix = "RX  "; color = Color.SeaGreen; break;
            case LogType.Error:
                prefix = "ERR "; color = Color.Crimson; break;
            default:
                prefix = "    "; color = Color.DimGray; break;
        }

        string line = prefix + "[" + time + "] " + message + "\n";
        rtLog.SelectionStart = rtLog.TextLength;
        rtLog.SelectionLength = 0;
        rtLog.SelectionColor = color;
        rtLog.AppendText(line);
        rtLog.SelectionColor = rtLog.ForeColor;

        if (chkAutoScroll.Checked)
        {
            rtLog.SelectionStart = rtLog.TextLength;
            rtLog.ScrollToCaret();
        }

        // Also mirror to debug output
        System.Diagnostics.Debug.WriteLine(prefix + "[" + time + "] " + message);
    }

    private void SaveLog()
    {
        if (string.IsNullOrEmpty(rtLog.Text)) return;
        var dlg = new SaveFileDialog
        {
            Filter = "Log Files (*.log)|*.log|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = "log",
            FileName = "autofocus_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            try
            {
                System.IO.File.WriteAllText(dlg.FileName, rtLog.Text, System.Text.Encoding.UTF8);
                AddLog("日志已保存: " + dlg.FileName, LogType.Info);
            }
            catch (Exception ex)
            {
                AddLog("保存日志失败: " + ex.Message, LogType.Error);
            }
        }
    }

    private void CopyLog()
    {
        if (string.IsNullOrEmpty(rtLog.Text)) return;
        Clipboard.SetText(rtLog.Text);
        btnCopyLog.Text = "[已复制]";
        var t = new Timer { Interval = 1500 };
        t.Tick += (s, e) => { btnCopyLog.Text = "复制全部"; t.Stop(); t.Dispose(); };
        t.Start();
    }

    // ── 轮询 ────────────────────────────────────────────

    private int _pollPhase;

    private void PollTimer_Tick(object sender, EventArgs e)
    {
        var svc = GetActiveService();
        if (!svc.IsConnected) return;

        _pollPhase = (_pollPhase + 1) % 100;
        if (_pollPhase % 2 == 1)
        {
            try
            {
                string resp = svc.SendCommand(":GP#", true);
                if (resp != null && resp.Length >= 2)
                {
                    int pos;
                    if (int.TryParse(resp, System.Globalization.NumberStyles.HexNumber, null, out pos))
                    {
                        _currentPosition = pos;
                        lblPosition.Text = _currentPosition.ToString();
                    }
                }
            }
            catch { } // poll errors shouldn't crash
        }
        else
        {
            try
            {
                string resp = svc.SendCommand(":GT#", true);
                if (resp != null && resp.Length >= 2)
                {
                    int raw;
                    if (int.TryParse(resp, System.Globalization.NumberStyles.HexNumber, null, out raw))
                    {
                        double temp = raw / 2.0;
                        lblTemp.Text = "温度: " + temp.ToString("F1") + " °C";
                        sbPosTemp.Text = "位置: " + _currentPosition + " 步 | 温度: " + temp.ToString("F1") + " °C";
                    }
                }
                string sr = svc.SendCommand(":GI#", true);
                if (sr != null && sr.Length == 2)
                {
                    bool wasMoving = _isMoving;
                    _isMoving = (sr == "01");
                    lblStatus.Text = _isMoving ? "运动中" : "空闲";
                    lblStatus.ForeColor = _isMoving ? Color.OrangeRed : Color.FromArgb(0, 120, 212);
                    pbStatusDot.Invalidate();

                    // Log completion when movement finishes
                    if (wasMoving && !_isMoving && _moveStartTime != DateTime.MinValue)
                    {
                        var duration = (DateTime.Now - _moveStartTime).TotalSeconds;
                        AddLog(string.Format("  ✓ 完成 → {0} ({1:F1}s)", _currentPosition, duration), LogType.Info);
                        _moveStartTime = DateTime.MinValue;
                    }
                }
            }
            catch { } // poll errors shouldn't crash
        }
    }

    // ── UI 辅助 ──────────────────────────────────────────

    private void UpdateJogButtons()
    {
        if (btnBigIn != null) { btnBigIn.Text = "< " + _config.BigStep; btnBigOut.Text = _config.BigStep + " >"; }
        if (btnMidIn != null) { btnMidIn.Text = "< " + _config.MidStep; btnMidOut.Text = _config.MidStep + " >"; }
        if (btnSmallIn != null) { btnSmallIn.Text = "< " + _config.SmallStep; btnSmallOut.Text = _config.SmallStep + " >"; }
    }

    private void UpdateConnectionState(bool connected)
    {
        btnConnect.Enabled = !connected;
        btnDisconnect.Enabled = connected;
        cbPort.Enabled = !connected && rbDirect.Checked;
        cbBaudRate.Enabled = !connected && rbDirect.Checked;
        btnScan.Enabled = !connected && rbDirect.Checked;
        cbASCOMProgID.Enabled = !connected && rbASCOM.Checked;
        rbDirect.Enabled = !connected;
        rbASCOM.Enabled = !connected;
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        _pollTimer.Stop();
        if (rbASCOM.Checked)
            _ascom.Disconnect();
        else
            _serial.Disconnect();
        Config.Save("autofocus.config", _config);
    }

    // ── 静态辅助方法 ─────────────────────────────────────

    private static Button StyledButton(string text, int x, int y, int w, int h, Color back, Color fore)
    {
        return new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = back,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Microsoft YaHei", 11)
        };
    }

    private static void AddLabel(Control parent, string text, int x, int y, int w, int h,
        ContentAlignment align = ContentAlignment.MiddleLeft, bool fontBold = false)
    {
        var lbl = new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, h),
            TextAlign = align
        };
        if (fontBold) lbl.Font = new Font("Microsoft YaHei", 11, FontStyle.Bold);
        parent.Controls.Add(lbl);
    }
}
