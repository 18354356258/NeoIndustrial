using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Forms
{
    public class ThroughputPoint
    {
        public DateTime Time { get; set; }
        public int TotalPoints { get; set; }
        public int MqttPublished { get; set; }
    }

    public class AlarmEntry
    {
        public DateTime Time { get; set; }
        public string Device { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public int Count { get; set; } = 1;
    }

    public static class NavigationHelper
    {
        public static DashboardForm Dashboard { get; set; }
        public static MainForm Main { get; set; }
    }

    public class DashboardForm : Form
    {
        // ======================== WIN32 ========================
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);
        private const int WM_SETREDRAW = 0x000B;

        // ======================== TIMERS ========================
        private Timer _refreshTimer;
        private Timer _clockTimer;

        // ======================== DATA BUFFERS ========================
        private bool _forceClose = false;
        private Queue<ThroughputPoint> _throughputHistory = new Queue<ThroughputPoint>();
        private int _pointsThisMinute = 0;
        private int _mqttThisMinute = 0;
        private int _totalPointsLifetime = 0;
        private int _totalMqttLifetime = 0;
        private TableLayoutPanel _tblDbConn;
        private List<AlarmEntry> _recentAlarms = new List<AlarmEntry>();
        private Dictionary<string, AlarmEntry> _activeVarAlarms = new Dictionary<string, AlarmEntry>();
        private string _lastDbConnHash = "";
        private long _logFilePosition = 0;
        private string _logFilePath = "";
        private string _lastAlarmsHash = "";
        private long _logLineCount = 0;            // 日志行号追踪,增量解析
        private Dictionary<string, DateTime> _alarmLastSeen = new Dictionary<string, DateTime>();

        // ======================== SERVICE DURATION TRACKING ========================
        private bool _wasMqttConnected = false;
        private bool _wasDbConnected = false;
        private bool _wasMcpRunning = false;
        private bool _wasRestRunning = false;
        private DateTime _mqttConnectedSince = DateTime.MinValue;
        private DateTime _dbConnectedSince = DateTime.MinValue;
        private DateTime _mcpRunningSince = DateTime.MinValue;
        private DateTime _restRunningSince = DateTime.MinValue;
        private DateTime _mqttDisconnectNoticed = DateTime.MinValue;

        // ======================== COLOR CONSTANTS - Enterprise Dark Theme ========================
        private static readonly Color ClrBg = ColorTranslator.FromHtml("#0a0e17");
        private static readonly Color ClrCard = ColorTranslator.FromHtml("#111827");
        private static readonly Color ClrBorder = ColorTranslator.FromHtml("#1a2332");
        private static readonly Color ClrAccent = ColorTranslator.FromHtml("#3b82f6");
        private static readonly Color ClrGreen = ColorTranslator.FromHtml("#10b981");
        private static readonly Color ClrAmber = ColorTranslator.FromHtml("#f59e0b");
        private static readonly Color ClrRed = ColorTranslator.FromHtml("#ef4444");
        private static readonly Color ClrPurple = ColorTranslator.FromHtml("#8b5cf6");
        private static readonly Color ClrCyan = ColorTranslator.FromHtml("#06b6d4");
        private static readonly Color ClrText = ColorTranslator.FromHtml("#f1f5f9");
        private static readonly Color ClrTextMuted = ColorTranslator.FromHtml("#94a3b8");
        private static readonly Color ClrTextDim = ColorTranslator.FromHtml("#64748b");
        private static readonly Color ClrGrid = ColorTranslator.FromHtml("#1e293b");
        private static readonly Color ClrLogBg = ColorTranslator.FromHtml("#0d1117");

        // Chart palette colors
        private static readonly Color ClrChartBlue = Color.FromArgb(59, 130, 246);
        private static readonly Color ClrChartGreen = Color.FromArgb(16, 185, 129);
        private static readonly Color ClrChartAmber = Color.FromArgb(245, 158, 11);
        private static readonly Color ClrChartRed = Color.FromArgb(239, 68, 68);
        private static readonly Color ClrChartPurple = Color.FromArgb(139, 92, 246);
        private static readonly Color ClrChartCyan = Color.FromArgb(6, 182, 212);

        // ======================== HEADER CONTROLS ========================
        private Panel _headerPanel;
        private Label _lblHeaderTitle;
        private Label _lblClock;
        private Button _btnCollection;

        // ======================== CARD 1: DEVICE OVERVIEW ========================
        private Panel _cardOverview;
        private Label _lblCard1Title;
        private Label _lblTotalValue;
        private Label _lblTotalLabel;
        private Label _lblOnlineValue;
        private Label _lblOnlineLabel;
        private Label _lblOfflineValue;
        private Label _lblOfflineLabel;
        private Label _lblErrorValue;
        private Label _lblErrorLabel;

        // ======================== CARD 2: THROUGHPUT CHART ========================
        private Panel _cardThroughput;
        private Label _lblCard2Title;
        private Chart _chartThroughput;

        // ======================== CARD 3: SERVICE STATUS ========================
        private Panel _cardService;
        private Label _lblCard3Title;
        private Panel _dotMqtt;
        private Panel _dotDb;
        private Panel _dotMcp;
        private Panel _dotRest;
        private Label _lblMqttName;
        private Label _lblDbName;
        private Label _lblMcpName;
        private Label _lblRestName;
        private Label _lblMqttState;
        private Label _lblDbState;
        private Label _lblMcpState;
        private Label _lblRestState;
        private Label _lblMqttUptime;
        private Label _lblDbUptime;
        private Label _lblMcpUptime;
        private Label _lblRestUptime;

        // ======================== CARD 4: DRIVER DISTRIBUTION ========================
        private Panel _cardDriverDist;
        private Label _lblCard4Title;
        private Chart _chartDriverDist;

        // ======================== CARD 5: RECENT ALARMS ========================
        private Panel _cardAlarms;
        private Label _lblCard5Title;
        private RichTextBox _rtbAlarms;

        // ======================== CARD 6: DB WRITE STATS ========================
        private Panel _cardDbConn;
        private Label _lblCard6Title;

        // ======================== CARD 7: SYSTEM LOG ========================
        private Panel _cardSysLog;
        private Label _lblCard7Title;
        private RichTextBox _txtSysLog;

        // ======================== CARD 8: DEVICE STATUS ========================
        private Panel _cardDevStatus;
        private Label _lblCard8Title;
        private TableLayoutPanel _tblDevHdr;
        private Panel _pnlDevScroll;
        private TableLayoutPanel _tblDevStatus;
        private Chart _chartConnHealth;

        // ======================== DRAGGING ========================


        // ======================== MAIN LAYOUT ========================
        private TableLayoutPanel _mainLayout;

        // ======================== CONSTRUCTOR ========================
        public DashboardForm()
        {
            this.Text = "MatriX Industrial Data Collector - Dashboard";
            var workingArea = Screen.PrimaryScreen.WorkingArea;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size((int)(workingArea.Width * 0.92), (int)(workingArea.Height * 0.90));
            this.WindowState = FormWindowState.Normal;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.Icon = Program.AppIcon;
            this.BackColor = ClrBg;
            this.DoubleBuffered = true;
            this.KeyPreview = true;

            BuildUI();
            ApplyLanguage();

            _clockTimer = new Timer();
            _clockTimer.Interval = 1000;
            _clockTimer.Tick += ClockTimer_Tick;

            _refreshTimer = new Timer();
            _refreshTimer.Interval = 2000;
            _refreshTimer.Tick += RefreshTimer_Tick;

            this.Load += DashboardForm_Load;
            this.Shown += DashboardForm_Shown;
            this.FormClosing += DashboardForm_FormClosing;
            this.KeyDown += DashboardForm_KeyDown;
            this.Resize += DashboardForm_Resize;

            LanguageManager.Instance.LanguageChanged += OnLanguageChanged;
            RegisterDataEvents();

            NavigationHelper.Dashboard = this;
        }

        // ======================== EVENT HANDLERS ========================
        private void DashboardForm_Load(object sender, EventArgs e)
        {
            _clockTimer.Start();
            _refreshTimer.Start();
            RefreshDashboard();
        }

        private void DashboardForm_Shown(object sender, EventArgs e)
        {
            // Add charts after all layout is complete (avoid height=0 crash)
            _cardThroughput.Controls.Add(_chartThroughput);
            _cardDriverDist.Controls.Add(_chartDriverDist);
            // DB connection table uses TableLayoutPanel, no chart
            // Device status table uses TableLayoutPanel, added in BuildDevStatusTable

            // Init core services (normally done by MainForm; when dashboard opens first,
            // services aren't running yet - start them here so status indicators work)
            InitServices();

            // Auto-start data collection from last saved running state
            AutoStartCollection();
        }

        private void DashboardForm_Resize(object sender, EventArgs e)
        {
            // TableLayoutPanel with Percent sizing handles card resize automatically.
            // Reposition header elements.
            if (_headerPanel != null && !_headerPanel.IsDisposed)
            {
                PositionHeaderControls();
            }
        }

        private void DashboardForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_forceClose)
            {
                if (_refreshTimer != null) _refreshTimer.Stop();
                if (_clockTimer != null) _clockTimer.Stop();
                Cleanup();
                return;
            }
            // Minimize or close button → switch to MainForm
            e.Cancel = true;
            this.Hide();
            ShowOrCreateMainForm();
            Logger.Info("看板关闭,切换到数采页面");
        }

        public void ForceClose()
        {
            _forceClose = true;
            try { this.Close(); } catch { }
        }

        private void ShowOrCreateMainForm()
        {
            if (NavigationHelper.Main == null || NavigationHelper.Main.IsDisposed)
                NavigationHelper.Main = new MainForm();
            NavigationHelper.Main.Show();
            NavigationHelper.Main.WindowState = FormWindowState.Normal;
            NavigationHelper.Main.Activate();
        }

        private void DashboardForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (NavigationHelper.Main != null && !NavigationHelper.Main.IsDisposed)
                {
                    NavigationHelper.Main.WindowState = FormWindowState.Normal;
                    NavigationHelper.Main.Show();
                    NavigationHelper.Main.Activate();
                }
                this.Hide();
            }
        }

        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            if (_lblClock != null && !_lblClock.IsDisposed)
            {
                _lblClock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            RefreshDashboard();
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke((Action)(() => ApplyLanguage()));
                return;
            }
            ApplyLanguage();
        }

        // ======================== DATA EVENTS ========================
        private void RegisterDataEvents()
        {
            DataCollectionService.Instance.OnDataReceived += (s, ev) =>
            {
                _pointsThisMinute++;
                _totalPointsLifetime++;
                if (MqttPublishService.Instance != null && MqttPublishService.Instance.IsConnected)
                {
                    _mqttThisMinute++;
                    _totalMqttLifetime++;
                }
            };
        }

        // ======================== SERVICE INIT (dashboard-first startup) ========================
        private void InitServices()
        {
            try
            {
                // REST API
                if (RestApiService.Instance != null && !RestApiService.Instance.IsRunning)
                {
                    string apiCfg = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "IndustrialDataCollection", "config", "apiConfig.json");
                    if (File.Exists(apiCfg))
                    {
                        var cfg = JsonConvert.DeserializeObject<ApiServiceConfig>(File.ReadAllText(apiCfg, Encoding.UTF8));
                        if (cfg != null && cfg.Enabled)
                        {
                            RestApiService.Instance.Port = cfg.Port;
                            RestApiService.Instance.TokenAuthEnabled = cfg.TokenAuth;
                            RestApiService.Instance.ApiToken = cfg.ApiToken ?? "admin123";
                            RestApiService.Instance.SwaggerEnabled = cfg.Swagger;
                            RestApiService.Instance.Start();
                        }
                    }
                }

                // MCP
                if (McpService.ActiveInstance == null || !McpService.ActiveInstance.IsRunning)
                {
                    string mcpCfg = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "IndustrialDataCollection", "config", "mcpConfig.json");
                    if (File.Exists(mcpCfg))
                    {
                        var mc = JsonConvert.DeserializeObject<McpCfg>(File.ReadAllText(mcpCfg, Encoding.UTF8));
                        var mcp = new McpService();
                        McpService.ActiveInstance = mcp;
                        mcp.Port = mc.Port;
                        mcp.TokenAuthEnabled = mc.TokenAuth;
                        mcp.McpToken = mc.McpToken ?? "admin123";
                        mcp.Start();
                    }
                }

                // DB write service - ensure config is loaded
                DatabaseWriteService.Instance.ReloadConfig();

                // MQTT auto-connect (fire-and-forget, non-blocking)
                Task.Run(async () =>
                {
                    try
                    {
                        var mqttConfig = await ConfigService.Instance.LoadMqttConfigAsync();
                        if (mqttConfig.Enabled && !string.IsNullOrEmpty(mqttConfig.BrokerHost))
                        {
                            await MqttPublishService.Instance.ConnectAsync(mqttConfig);
                        }
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                Logger.Debug("Dashboard InitServices error: " + ex.Message);
            }
        }

        private class ApiServiceConfig
        {
            public int Port { get; set; } = 5000;
            public bool Enabled { get; set; } = true;
            public bool TokenAuth { get; set; } = true;
            public string ApiToken { get; set; } = "admin123";
            public bool Swagger { get; set; } = true;
        }

        private class McpCfg
        {
            public int Port { get; set; } = 9000;
            public bool TokenAuth { get; set; } = true;
            public string McpToken { get; set; } = "admin123";
        }

        // ======================== AUTO-START COLLECTION ========================
        private async void AutoStartCollection()
        {
            try
            {
                var ids = LoadRunningState();
                if (ids.Count == 0) return;

                await Task.Delay(1500);

                var devices = ConfigService.Instance.LoadDevices();
                int started = 0;
                foreach (var id in ids)
                {
                    try
                    {
                        DeviceConfig device = null;
                        foreach (var d in devices)
                        {
                            if (d.Id == id) { device = d; break; }
                        }
                        if (device != null && !DataCollectionService.Instance.IsDeviceRunning(device.Id))
                        {
                            await DataCollectionService.Instance.StartDeviceAsync(device);
                            started++;
                        }
                    }
                    catch { }
                }

                if (started > 0)
                {
                    Logger.Info(string.Format("Dashboard auto-started collection: {0}/{1} devices", started, ids.Count));
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("Dashboard auto-start error: " + ex.Message);
            }
        }

        private static string RunningStatePath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndustrialDataCollection", "config");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "running_state.json");
        }

        private List<string> LoadRunningState()
        {
            try
            {
                string path = RunningStatePath();
                if (!File.Exists(path)) return new List<string>();
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        // ======================== CLEANUP ========================
        private void Cleanup()
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }
            if (_clockTimer != null)
            {
                _clockTimer.Stop();
                _clockTimer.Dispose();
                _clockTimer = null;
            }
            LanguageManager.Instance.LanguageChanged -= OnLanguageChanged;
        }

        // ======================== BUILD UI ========================
        private void BuildUI()
        {
            _mainLayout = new TableLayoutPanel();
            _mainLayout.Dock = DockStyle.Fill;
            _mainLayout.ColumnCount = 1;
            _mainLayout.RowCount = 2;
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _mainLayout.Padding = new Padding(0);
            _mainLayout.Margin = new Padding(0);
            _mainLayout.BackColor = ClrBg;
            this.Controls.Add(_mainLayout);

            BuildHeader(_mainLayout);
            BuildCardGrid(_mainLayout);
        }

        private void BuildHeader(TableLayoutPanel parent)
        {
            _headerPanel = new Panel();
            _headerPanel.Dock = DockStyle.Fill;
            _headerPanel.BackColor = ColorTranslator.FromHtml("#0f172a");
            _headerPanel.Margin = new Padding(0);
            _headerPanel.Padding = new Padding(0);

            // Header bottom border
            _headerPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(ClrBorder, 1))
                {
                    e.Graphics.DrawLine(pen, 0, _headerPanel.Height - 1, _headerPanel.Width, _headerPanel.Height - 1);
                }
            };


            // Title
            _lblHeaderTitle = new Label();
            _lblHeaderTitle.Text = "MatriX Industrial Data Collector";
            _lblHeaderTitle.ForeColor = ClrText;
            _lblHeaderTitle.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            _lblHeaderTitle.AutoSize = true;
            _lblHeaderTitle.Location = new Point(20, 14);
            _lblHeaderTitle.BackColor = Color.Transparent;
            _headerPanel.Controls.Add(_lblHeaderTitle);

            // Clock
            _lblClock = new Label();
            _lblClock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _lblClock.ForeColor = ClrTextMuted;
            _lblClock.Font = new Font("Consolas", 10F);
            _lblClock.AutoSize = true;
            _lblClock.BackColor = Color.Transparent;
            _headerPanel.Controls.Add(_lblClock);

            // Navigation button: Collection
            _btnCollection = new Button();
            _btnCollection.Text = "Collection";
            _btnCollection.FlatStyle = FlatStyle.Flat;
            _btnCollection.FlatAppearance.BorderSize = 1;
            _btnCollection.FlatAppearance.BorderColor = ClrBorder;
            _btnCollection.BackColor = ClrCard;
            _btnCollection.ForeColor = ClrText;
            _btnCollection.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            _btnCollection.Size = new Size(100, 30);
            _btnCollection.Cursor = Cursors.Hand;
            _btnCollection.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 64, 175);
            _btnCollection.Click += (s, e) => NavigateToMainForm();
            _headerPanel.Controls.Add(_btnCollection);


            parent.Controls.Add(_headerPanel, 0, 0);
            PositionHeaderControls();
        }

        private void PositionHeaderControls()
        {
            if (_headerPanel == null || _headerPanel.IsDisposed) return;
            int w = _headerPanel.Width;
            _btnCollection.Left = w - 202;
            _btnCollection.Top = 12;
            _lblClock.Left = w - 420;
            _lblClock.Top = 16;
        }

        private void BuildCardGrid(TableLayoutPanel parent)
        {
            var grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = 3;
            grid.RowCount = 3;
            grid.Padding = new Padding(6, 3, 6, 6);
            grid.Margin = new Padding(0);
            grid.BackColor = ClrBg;

            for (int i = 0; i < 3; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));

            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 32));

            // Row 0
            _cardOverview = CreateCard("Device Overview", ClrGreen, grid, 0, 0, out _lblCard1Title);
            BuildDeviceOverview(_cardOverview);

            _cardThroughput = CreateCard("Data Throughput", ClrCyan, grid, 1, 0, out _lblCard2Title);
            BuildThroughputChart(_cardThroughput);

            _cardService = CreateCard("Service Status", ClrAmber, grid, 2, 0, out _lblCard3Title);
            BuildServiceStatus(_cardService);

            // Row 1
            _cardDriverDist = CreateCard("Driver Distribution", ClrPurple, grid, 0, 1, out _lblCard4Title);
            BuildDriverDistribution(_cardDriverDist);

            _cardAlarms = CreateCard("Recent Alarms", ClrRed, grid, 1, 1, out _lblCard5Title);
            BuildAlarmsPanel(_cardAlarms);

            _cardDbConn = CreateCard(LanguageManager.Instance.GetString("Dashboard_DbConnTitle"), ClrCyan, grid, 2, 1, out _lblCard6Title);
            BuildDbConnectionTable(_cardDbConn);

            // Row 2
            _cardSysLog = CreateCard("System Log", ClrTextMuted, grid, 0, 2, out _lblCard7Title);
            grid.SetColumnSpan(_cardSysLog, 2);
            BuildSystemLog(_cardSysLog);

            _cardDevStatus = CreateCard(LanguageManager.Instance.GetString("Dashboard_DevStatusTitle"), ClrGreen, grid, 2, 2, out _lblCard8Title);
            BuildDevStatusTable(_cardDevStatus);

            parent.Controls.Add(grid, 0, 1);
        }

        // ======================== CARD BUILDER ========================
        private Panel CreateCard(string title, Color accentColor, TableLayoutPanel parent, int col, int row, out Label titleLabel)
        {
            var card = new Panel();
            card.Dock = DockStyle.Fill;
            card.BackColor = ClrCard;
            card.Margin = new Padding(3);
            card.Padding = new Padding(0);

            // Card border via Paint
            card.Paint += (s, e) =>
            {
                using (var pen = new Pen(ClrBorder, 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                }
            };

            // Title bar (28px)
            var titleBar = new Panel();
            titleBar.Height = 28;
            titleBar.Dock = DockStyle.Top;
            titleBar.BackColor = Color.Transparent;
            titleBar.Padding = new Padding(0);

            // Accent bar (left, 4px wide)
            var accentBar = new Panel();
            accentBar.Width = 4;
            accentBar.Height = 22;
            accentBar.Left = 0;
            accentBar.Top = 3;
            accentBar.BackColor = accentColor;
            titleBar.Controls.Add(accentBar);

            titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Left = 16;
            titleLabel.Top = 4;
            titleLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            titleLabel.ForeColor = ClrText;
            titleLabel.AutoSize = true;
            titleLabel.BackColor = Color.Transparent;
            titleBar.Controls.Add(titleLabel);

            card.Controls.Add(titleBar);

            // Spacer: 6px gap between title bar and card content
            var spacer = new Panel();
            spacer.Height = 6;
            spacer.Dock = DockStyle.Top;
            spacer.BackColor = Color.Transparent;
            card.Controls.Add(spacer);

            parent.Controls.Add(card, col, row);

            return card;
        }

        // ======================== CARD 1: DEVICE OVERVIEW (4-column TableLayoutPanel) ========================
        private void BuildDeviceOverview(Panel card)
        {
            var tbl = new TableLayoutPanel();
            tbl.Dock = DockStyle.Fill;
            tbl.ColumnCount = 4;
            tbl.RowCount = 3;
            tbl.BackColor = Color.Transparent;
            tbl.Padding = new Padding(8, 4, 8, 4);
            tbl.Margin = new Padding(0);

            for (int i = 0; i < 4; i++)
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 55F)); // values row
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 12F));  // spacer
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 45F)); // labels row

            card.Controls.Add(tbl);

            // Column 0: Total (white) - big value + small label
            _lblTotalValue = NewValueLabel("0", ClrText);
            tbl.Controls.Add(_lblTotalValue, 0, 0);
            _lblTotalLabel = NewDescLabel("Total");
            tbl.Controls.Add(_lblTotalLabel, 0, 2);

            // Column 1: Online (green)
            _lblOnlineValue = NewValueLabel("0", ClrGreen);
            tbl.Controls.Add(_lblOnlineValue, 1, 0);
            _lblOnlineLabel = NewDescLabel("Online");
            tbl.Controls.Add(_lblOnlineLabel, 1, 2);

            // Column 2: Offline (amber)
            _lblOfflineValue = NewValueLabel("0", ClrAmber);
            tbl.Controls.Add(_lblOfflineValue, 2, 0);
            _lblOfflineLabel = NewDescLabel("Offline");
            tbl.Controls.Add(_lblOfflineLabel, 2, 2);

            // Column 3: Error (red)
            _lblErrorValue = NewValueLabel("0", ClrRed);
            tbl.Controls.Add(_lblErrorValue, 3, 0);
            _lblErrorLabel = NewDescLabel("Error");
            tbl.Controls.Add(_lblErrorLabel, 3, 2);
        }

        private Label NewValueLabel(string text, Color color)
        {
            return new Label()
            {
                Text = text,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0),
                AutoEllipsis = false
            };
        }

        private Label NewDescLabel(string text)
        {
            return new Label()
            {
                Text = text,
                Font = new Font("Segoe UI", 9F),
                ForeColor = ClrTextMuted,
                BackColor = Color.Transparent,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0)
            };
        }

        // ======================== CARD 2: THROUGHPUT CHART ========================
        private void BuildThroughputChart(Panel card)
        {
            _chartThroughput = CreateThroughputChart();
            _chartThroughput.Dock = DockStyle.Fill;
            // Added in Shown event (avoid height=0 crash)
        }

        // ======================== CARD 3: SERVICE STATUS ========================
        private void BuildServiceStatus(Panel card)
        {
            var content = new Panel();
            content.Dock = DockStyle.Fill;
            content.BackColor = Color.Transparent;
            card.Controls.Add(content);

            int yStart = 44;
            int rowH = 44;
            int nameX = 38;
            int stateX = 210;
            int uptimeX = 210;

            // MQTT
            _dotMqtt = BuildStatusDot(content, 14, yStart + 4, ClrRed);
            _lblMqttName = BuildStatusLabel(content, "MQTT Broker", nameX, yStart, ClrText, 11F);
            _lblMqttState = BuildStatusLabel(content, "Disconnected", stateX, yStart, ClrRed, 9F, FontStyle.Bold);
            _lblMqttUptime = BuildStatusLabel(content, "", uptimeX, yStart + 20, ClrTextDim, 8F);
            yStart += rowH;

            // Database
            _dotDb = BuildStatusDot(content, 14, yStart + 4, ClrRed);
            _lblDbName = BuildStatusLabel(content, "Database Write", nameX, yStart, ClrText, 11F);
            _lblDbState = BuildStatusLabel(content, "Offline", stateX, yStart, ClrRed, 9F, FontStyle.Bold);
            _lblDbUptime = BuildStatusLabel(content, "", uptimeX, yStart + 20, ClrTextDim, 8F);
            yStart += rowH;

            // MCP
            _dotMcp = BuildStatusDot(content, 14, yStart + 4, ClrRed);
            _lblMcpName = BuildStatusLabel(content, "MCP Service", nameX, yStart, ClrText, 11F);
            _lblMcpState = BuildStatusLabel(content, "Stopped", stateX, yStart, ClrRed, 9F, FontStyle.Bold);
            _lblMcpUptime = BuildStatusLabel(content, "", uptimeX, yStart + 20, ClrTextDim, 8F);
            yStart += rowH;

            // REST API
            _dotRest = BuildStatusDot(content, 14, yStart + 4, ClrRed);
            _lblRestName = BuildStatusLabel(content, "REST API", nameX, yStart, ClrText, 11F);
            _lblRestState = BuildStatusLabel(content, "Stopped", stateX, yStart, ClrRed, 9F, FontStyle.Bold);
            _lblRestUptime = BuildStatusLabel(content, "", uptimeX, yStart + 20, ClrTextDim, 8F);

            // Row separators
            content.Paint += (s, e) =>
            {
                var pen = new Pen(Color.FromArgb(30, 41, 59), 1);
                int y = 8;
                for (int i = 0; i < 3; i++)
                {
                    y += rowH;
                    e.Graphics.DrawLine(pen, 12, y, content.Width - 24, y);
                }
                pen.Dispose();
            };
        }

        private Panel BuildStatusDot(Panel parent, int x, int y, Color color)
        {
            var dot = new Panel();
            dot.Size = new Size(14, 14);
            dot.Location = new Point(x, y);
            dot.BackColor = Color.Transparent;
            dot.Tag = color;
            dot.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush((Color)dot.Tag))
                    e.Graphics.FillEllipse(brush, 1, 1, 12, 12);
                using (var pen = new Pen(Color.FromArgb(40, (Color)dot.Tag), 2))
                    e.Graphics.DrawEllipse(pen, 1, 1, 12, 12);
            };
            parent.Controls.Add(dot);
            return dot;
        }

        private Label BuildStatusLabel(Panel parent, string text, int x, int y, Color color, float fontSize, FontStyle style = FontStyle.Regular)
        {
            var lbl = new Label();
            lbl.Text = text;
            lbl.AutoSize = true;
            lbl.Font = new Font("Segoe UI", fontSize, style);
            lbl.ForeColor = color;
            lbl.BackColor = Color.Transparent;
            lbl.Location = new Point(x, y);
            parent.Controls.Add(lbl);
            return lbl;
        }

        // ======================== CARD 4: DRIVER DISTRIBUTION ========================
        private void BuildDriverDistribution(Panel card)
        {
            _chartDriverDist = CreatePieChart();
            _chartDriverDist.Dock = DockStyle.Fill;
            // Added in Shown event
        }

        // ======================== CARD 5: RECENT ALARMS ========================
        private void BuildAlarmsPanel(Panel card)
        {
            _rtbAlarms = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = ClrLogBg,
                ForeColor = ClrTextMuted,
                Font = new Font("Consolas", 9F),
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Margin = new Padding(0)
            };
            card.Controls.Add(_rtbAlarms);
        }

        // ======================== CARD 6: DB CONNECTION STATUS TABLE ========================
        private void BuildDbConnectionTable(Panel card)
        {
            _tblDbConn = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ClrCard,
                ColumnCount = 3,
                RowCount = 6,
                Padding = new Padding(10, 6, 10, 6),
                AutoSize = false
            };
            _tblDbConn.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));  // type
            _tblDbConn.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // status
            _tblDbConn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // devices (fill)

            // Header
            AddDbHeader(_tblDbConn, "Type", 0);
            AddDbHeader(_tblDbConn, "Status", 1);
            AddDbHeader(_tblDbConn, "Devices", 2);
            _tblDbConn.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));

            string[] dbTypes = { "SQLite", "MySQL", "SQL Server", "PostgreSQL", "TDengine" };
            for (int r = 0; r < dbTypes.Length; r++)
            {
                _tblDbConn.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));

                var lblType = new Label
                {
                    Text = dbTypes[r],
                    Font = new Font("Consolas", 9F),
                    ForeColor = ClrText,
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 0, 0, 0)
                };
                _tblDbConn.Controls.Add(lblType, 0, r + 1);

                var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
                var dot = new Panel { Size = new Size(10, 10), Location = new Point(0, 6), BackColor = ClrRed };
                dot.Paint += (s, ev) => {
                    Color c = (s as Panel).BackColor;
                    ev.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (var br = new SolidBrush(c)) ev.Graphics.FillEllipse(br, 1, 1, 8, 8);
                };
                statusPanel.Controls.Add(dot);
                var lblStatus = new Label
                {
                    Text = "--", Font = new Font("Segoe UI", 9F),
                    ForeColor = ClrTextMuted, BackColor = Color.Transparent,
                    AutoSize = false, Size = new Size(90, 22), Location = new Point(14, 1),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                statusPanel.Controls.Add(lblStatus);
                _tblDbConn.Controls.Add(statusPanel, 1, r + 1);

                var lblDevices = new Label
                {
                    Text = "--", Font = new Font("Consolas", 9F),
                    ForeColor = ClrTextDim, BackColor = Color.Transparent,
                    Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 0, 0, 0)
                };
                _tblDbConn.Controls.Add(lblDevices, 2, r + 1);
            }
            card.Controls.Add(_tblDbConn);
        }

        private void AddDbHeader(TableLayoutPanel tbl, string text, int col)
        {
            var hdr = new Label
            {
                Text = text, Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = ClrTextDim, BackColor = Color.Transparent,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            };
            tbl.Controls.Add(hdr, col, 0);
        }
        // ======================== CARD 7: SYSTEM LOG ========================
        private void BuildSystemLog(Panel card)
        {
            var container = new Panel();
            container.Dock = DockStyle.Fill;
            container.BackColor = ClrLogBg;
            container.Padding = new Padding(8, 4, 8, 4);
            card.Controls.Add(container);

            _txtSysLog = new RichTextBox();
            _txtSysLog.Dock = DockStyle.Fill;
            _txtSysLog.BackColor = ClrLogBg;
            _txtSysLog.ForeColor = ClrTextMuted;
            _txtSysLog.Font = new Font("Consolas", 9F);
            _txtSysLog.BorderStyle = BorderStyle.None;
            _txtSysLog.ReadOnly = true;
            _txtSysLog.WordWrap = false;
            _txtSysLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            container.Controls.Add(_txtSysLog);
        }

        private void BuildDevStatusTable(Panel card)
        {
            // Fixed header row (never scrolls)
            _tblDevHdr = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 24,
                BackColor = ClrCard,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(10, 0, 10, 0)
            };
            _tblDevHdr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            _tblDevHdr.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            _tblDevHdr.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            _tblDevHdr.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            _tblDevHdr.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));

            var L = LanguageManager.Instance;
            AddDevHdr(_tblDevHdr, L.GetString("Dashboard_DevHdr_Device"), 0);
            AddDevHdr(_tblDevHdr, L.GetString("Dashboard_DevHdr_Collect"), 1);
            AddDevHdr(_tblDevHdr, L.GetString("Dashboard_DevHdr_MQTT"), 2);
            AddDevHdr(_tblDevHdr, L.GetString("Dashboard_DevHdr_API"), 3);

            card.Controls.Add(_tblDevHdr);

            // Scrollable data area below the fixed header
            _pnlDevScroll = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ClrCard,
                AutoScroll = true
            };

            _tblDevStatus = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = ClrCard,
                ColumnCount = 4,
                RowCount = 0,
                Padding = new Padding(10, 0, 10, 6)
            };
            _tblDevStatus.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            _tblDevStatus.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            _tblDevStatus.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            _tblDevStatus.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));

            _pnlDevScroll.Controls.Add(_tblDevStatus);
            card.Controls.Add(_pnlDevScroll);
        }

        private void AddDevHdr(TableLayoutPanel tbl, string text, int col)
        {
            var hdr = new Label
            {
                Text = text, Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = ClrTextDim, BackColor = Color.Transparent,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0)
            };
            tbl.Controls.Add(hdr, col, 0);
        }

        // ======================== CHART FACTORIES ========================
        private Chart CreateThroughputChart()
        {
            var chart = new Chart();
            chart.BackColor = ClrCard;
            chart.AntiAliasing = AntiAliasingStyles.All;

            var area = new ChartArea("Main");
            area.BackColor = ClrCard;
            area.AxisX.LabelStyle.ForeColor = ClrTextDim;
            area.AxisX.LabelStyle.Font = new Font("Consolas", 7F);
            area.AxisY.LabelStyle.ForeColor = ClrTextDim;
            area.AxisY.LabelStyle.Font = new Font("Consolas", 8F);
            area.AxisX.LineColor = ClrGrid;
            area.AxisY.LineColor = ClrGrid;
            area.AxisX.MajorGrid.LineColor = ClrGrid;
            area.AxisY.MajorGrid.LineColor = ClrGrid;
            area.AxisX.MinorGrid.Enabled = false;
            area.AxisY.MinorGrid.Enabled = false;
            area.AxisX.Interval = 10;
            area.AxisX.LabelStyle.Format = "HH:mm";
            area.AxisX.MajorTickMark.Enabled = false;
            area.AxisY.MajorTickMark.Enabled = false;
            area.AxisX.TitleForeColor = ClrTextDim;
            area.AxisY.TitleForeColor = ClrTextDim;
            chart.ChartAreas.Add(area);

            var legend = new Legend("Legend");
            legend.ForeColor = ClrTextMuted;
            legend.BackColor = Color.Transparent;
            legend.Font = new Font("Segoe UI", 8F);
            legend.Docking = Docking.Bottom;
            legend.Position.Auto = true;
            chart.Legends.Add(legend);
            return chart;
        }

        private Chart CreatePieChart()
        {
            var chart = new Chart();
            chart.BackColor = ClrCard;
            chart.AntiAliasing = AntiAliasingStyles.All;

            var area = new ChartArea("Main");
            area.BackColor = ClrCard;
            area.BorderColor = Color.Transparent;
            area.Position = new ElementPosition(5, 5, 90, 90);
            area.InnerPlotPosition = new ElementPosition(5, 5, 90, 90);
            chart.ChartAreas.Add(area);

            var legend2 = new Legend("Legend");
            legend2.ForeColor = ClrTextMuted;
            legend2.BackColor = Color.Transparent;
            legend2.Font = new Font("Segoe UI", 8F);
            legend2.Docking = Docking.Bottom;
            chart.Legends.Add(legend2);

            return chart;
        }

        private Chart CreateBarChart()
        {
            var chart = new Chart();
            chart.BackColor = ClrCard;
            chart.AntiAliasing = AntiAliasingStyles.All;

            var area = new ChartArea("Main");
            area.BackColor = ClrCard;
            area.AxisX.LabelStyle.ForeColor = ClrTextDim;
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 8F);
            area.AxisY.LabelStyle.ForeColor = ClrTextDim;
            area.AxisY.LabelStyle.Font = new Font("Consolas", 8F);
            area.AxisX.LineColor = ClrGrid;
            area.AxisY.LineColor = ClrGrid;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY.MajorGrid.LineColor = ClrGrid;
            area.AxisX.MajorTickMark.Enabled = false;
            area.AxisY.MajorTickMark.Enabled = false;
            area.AxisY.IsStartedFromZero = true;
            area.AxisY.Minimum = 0;
            chart.ChartAreas.Add(area);

            chart.Legends.Clear();
            return chart;
        }

        private Chart CreateHorizontalBarChart()
        {
            var chart = new Chart();
            chart.BackColor = ClrCard;
            chart.AntiAliasing = AntiAliasingStyles.All;

            var area = new ChartArea("Main");
            area.BackColor = ClrCard;
            area.AxisX.LabelStyle.ForeColor = ClrTextDim;
            area.AxisX.LabelStyle.Font = new Font("Segoe UI", 8F);
            area.AxisY.LabelStyle.ForeColor = ClrTextDim;
            area.AxisY.LabelStyle.Font = new Font("Segoe UI", 8F);
            area.AxisX.LineColor = ClrGrid;
            area.AxisY.LineColor = ClrGrid;
            area.AxisX.MajorGrid.LineColor = ClrGrid;
            area.AxisY.MajorGrid.Enabled = false;
            area.AxisX.MajorTickMark.Enabled = false;
            area.AxisY.MajorTickMark.Enabled = false;
            area.AxisX.Minimum = 0;
            area.AxisX.Maximum = 100;
            area.AxisX.Interval = 25;
            area.AxisX.Title = "%";
            area.AxisX.TitleForeColor = ClrTextDim;
            area.AxisX.TitleFont = new Font("Segoe UI", 8F);
            chart.ChartAreas.Add(area);

            chart.Legends.Clear();
            return chart;
        }

        // ======================== NAVIGATION ========================
        private void NavigateToMainForm()
        {
            if (NavigationHelper.Main == null || NavigationHelper.Main.IsDisposed)
            {
                NavigationHelper.Main = new MainForm();
            }
            NavigationHelper.Main.Show();
            NavigationHelper.Main.WindowState = FormWindowState.Normal;
            NavigationHelper.Main.Activate();
        }

        // ======================== REFRESH ORCHESTRATOR ========================
        private void RefreshDashboard()
        {
            try
            {
                RefreshDeviceOverview();
                RefreshThroughputChart();
                RefreshServiceStatus();
                RefreshDriverDistribution();
                RefreshAlarms();
                RefreshDbWriteStats();
                RefreshSystemLogs();
                RefreshConnectionHealth();
            }
            catch (Exception ex)
            {
                Logger.Debug("Dashboard refresh error: " + ex.Message);
            }
        }

        // ======================== REFRESH: DEVICE OVERVIEW ========================
        private void RefreshDeviceOverview()
        {
            try
            {
                var devices = ConfigService.Instance.LoadDevices();
                int total = devices.Count;
                int online = DataCollectionService.Instance.RunningCount;
                int offline = total - online;
                int errors = 0;

                if (_lblTotalValue != null && !_lblTotalValue.IsDisposed)
                    _lblTotalValue.Text = total.ToString();
                if (_lblOnlineValue != null && !_lblOnlineValue.IsDisposed)
                    _lblOnlineValue.Text = online.ToString();
                if (_lblOfflineValue != null && !_lblOfflineValue.IsDisposed)
                    _lblOfflineValue.Text = offline.ToString();
                if (_lblErrorValue != null && !_lblErrorValue.IsDisposed)
                    _lblErrorValue.Text = errors.ToString();
            }
            catch (Exception ex)
            {
                Logger.Debug("RefreshDeviceOverview error: " + ex.Message);
            }
        }

        // ======================== REFRESH: THROUGHPUT CHART ========================
        private void RefreshThroughputChart()
        {
            try
            {
                var point = new ThroughputPoint
                {
                    Time = DateTime.Now,
                    TotalPoints = _pointsThisMinute,
                    MqttPublished = _mqttThisMinute
                };
                _throughputHistory.Enqueue(point);
                while (_throughputHistory.Count > 10)
                    _throughputHistory.Dequeue();
                _pointsThisMinute = 0;
                _mqttThisMinute = 0;

                if (_chartThroughput == null || _chartThroughput.IsDisposed) return;

                _chartThroughput.Series.Clear();
                var points = _throughputHistory.ToList();

                if (points.Count == 0) return;

                // Data Points series (blue)
                var seriesData = new Series("Data Points");
                seriesData.ChartType = SeriesChartType.SplineArea;
                seriesData.BorderWidth = 2;
                seriesData.Color = ClrChartBlue;
                seriesData.ShadowOffset = 0;
                _chartThroughput.Series.Add(seriesData);

                // MQTT Published series (green)
                var seriesMqtt = new Series("MQTT Published");
                seriesMqtt.ChartType = SeriesChartType.SplineArea;
                seriesMqtt.BorderWidth = 2;
                seriesMqtt.Color = ClrChartGreen;
                seriesMqtt.ShadowOffset = 0;
                _chartThroughput.Series.Add(seriesMqtt);

                for (int i = 0; i < points.Count; i++)
                {
                    seriesData.Points.AddXY(points[i].Time, points[i].TotalPoints);
                    seriesMqtt.Points.AddXY(points[i].Time, points[i].MqttPublished);
                }

                if (_chartThroughput.ChartAreas.Count > 0)
                {
                    var area = _chartThroughput.ChartAreas[0];
                    area.AxisX.Minimum = points[0].Time.ToOADate();
                    area.AxisX.Maximum = points[points.Count - 1].Time.ToOADate();
                    area.RecalculateAxesScale();
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("RefreshThroughputChart error: " + ex.Message);
            }
        }

        // ======================== REFRESH: SERVICE STATUS ========================
        private void RefreshServiceStatus()
        {
            try
            {
                // MQTT - debounce: only show disconnected after 5s to avoid heartbeat flicker
                bool mqttConnected = MqttPublishService.Instance != null && MqttPublishService.Instance.IsConnected;
                if (!mqttConnected)
                {
                    if (_mqttDisconnectNoticed == DateTime.MinValue)
                        _mqttDisconnectNoticed = DateTime.Now;
                    if ((DateTime.Now - _mqttDisconnectNoticed).TotalSeconds < 5)
                        mqttConnected = _wasMqttConnected; // keep old state during debounce window
                }
                else
                {
                    _mqttDisconnectNoticed = DateTime.MinValue;
                }
                UpdateServiceStatus(_dotMqtt, _lblMqttState, _lblMqttUptime,
                    mqttConnected, ref _wasMqttConnected, ref _mqttConnectedSince,
                    "Dashboard_Connected", "Dashboard_Disconnected");

                // Database
                bool dbConnected = DatabaseWriteService.Instance != null && DatabaseWriteService.Instance.IsAnyConnected;
                UpdateServiceStatus(_dotDb, _lblDbState, _lblDbUptime,
                    dbConnected, ref _wasDbConnected, ref _dbConnectedSince,
                    "Dashboard_Connected", "Dashboard_Disconnected");

                // MCP
                bool mcpRunning = McpService.ActiveInstance != null && McpService.ActiveInstance.IsRunning;
                UpdateServiceStatus(_dotMcp, _lblMcpState, _lblMcpUptime,
                    mcpRunning, ref _wasMcpRunning, ref _mcpRunningSince,
                    "Dashboard_Running", "Dashboard_Stopped");

                // REST API
                bool restRunning = RestApiService.Instance != null && RestApiService.Instance.IsRunning;
                UpdateServiceStatus(_dotRest, _lblRestState, _lblRestUptime,
                    restRunning, ref _wasRestRunning, ref _restRunningSince,
                    "Dashboard_Running", "Dashboard_Stopped");
            }
            catch (Exception ex)
            {
                Logger.Debug("RefreshServiceStatus error: " + ex.Message);
            }
        }

        private void UpdateServiceStatus(Panel dotPanel, Label stateLabel, Label uptimeLabel,
            bool isActive, ref bool wasActive, ref DateTime activeSince,
            string activeKey, string inactiveKey)
        {
            if (dotPanel == null || dotPanel.IsDisposed) return;

            Color dotColor = isActive ? ClrGreen : ClrRed;
            dotPanel.Tag = dotColor;
            dotPanel.Invalidate();

            string stateText = LanguageManager.Instance.GetString(isActive ? activeKey : inactiveKey);
            if (stateLabel != null && !stateLabel.IsDisposed)
            {
                stateLabel.Text = stateText;
                stateLabel.ForeColor = isActive ? ClrGreen : ClrRed;
            }

            if (isActive)
            {
                if (!wasActive)
                {
                    activeSince = DateTime.Now;
                    wasActive = true;
                }
                if (uptimeLabel != null && !uptimeLabel.IsDisposed)
                {
                    var span = DateTime.Now - activeSince;
                    uptimeLabel.Text = string.Format("up {0}d {1}h {2}m {3}s", span.Days, span.Hours, span.Minutes, span.Seconds);
                }
            }
            else
            {
                wasActive = false;
                activeSince = DateTime.MinValue;
                if (uptimeLabel != null && !uptimeLabel.IsDisposed)
                    uptimeLabel.Text = "";
            }
        }

        // ======================== REFRESH: DRIVER DISTRIBUTION ========================
        private void RefreshDriverDistribution()
        {
            try
            {
                if (_chartDriverDist == null || _chartDriverDist.IsDisposed) return;

                var devices = ConfigService.Instance.LoadDevices();
                var driverCounts = new Dictionary<string, int>();

                foreach (var dev in devices)
                {
                    string driverKey = dev.DriverType ?? "Unknown";
                    string driverName = LanguageManager.Instance.GetString("Driver_" + driverKey);
                    if (driverName == "Driver_" + driverKey) driverName = driverKey;
                    if (driverCounts.ContainsKey(driverName))
                        driverCounts[driverName]++;
                    else
                        driverCounts[driverName] = 1;
                }

                _chartDriverDist.Series.Clear();
                if (driverCounts.Count == 0) return;

                var series = new Series("DriverDist");
                series.ChartType = SeriesChartType.Doughnut;
                series.IsValueShownAsLabel = false;
                series["DoughnutRadius"] = "40";
                series["PieDrawingStyle"] = "Concave";

                Color[] palette = new Color[] {
                    ClrChartBlue, ClrChartGreen, ClrChartAmber,
                    ClrChartPurple, ClrChartCyan, ClrChartRed,
                    Color.FromArgb(244, 114, 182)
                };

                int colorIdx = 0;
                foreach (var kv in driverCounts)
                {
                    int ptIdx = series.Points.AddXY(kv.Key, kv.Value);
                    series.Points[ptIdx].Color = palette[colorIdx % palette.Length];
                    series.Points[ptIdx].LegendText = kv.Key + " (" + kv.Value + ")";
                    series.Points[ptIdx].LabelForeColor = ClrText;
                    series.Points[ptIdx].Font = new Font("Segoe UI", 7F);
                    colorIdx++;
                }

                _chartDriverDist.Series.Add(series);
            }
            catch (Exception ex)
            {
                Logger.Debug("RefreshDriverDistribution error: " + ex.Message);
            }
        }



        private void ParseAlarmsFromLog()
        {
            try
            {
                // ── Variable alarms: use live DataProcessor state (source of truth) ──
                var liveSnapshots = DataProcessor.Instance.GetActiveAlarmsSnapshot();
                
                // Rebuild _activeVarAlarms from live state only
                var liveKeys = new HashSet<string>();
                foreach (var snap in liveSnapshots)
                {
                    string key = snap.Device + "|" + snap.Level;
                    liveKeys.Add(key);
                    _activeVarAlarms[key] = new AlarmEntry
                    {
                        Time = DateTime.Now,
                        Device = snap.Device,
                        Level = snap.Level,
                        Message = snap.VariableName,
                        Count = snap.Counter
                    };
                }
                // Remove entries not in live DataProcessor state
                var stale = _activeVarAlarms.Keys.Where(k => !liveKeys.Contains(k)).ToList();
                foreach (var k in stale) _activeVarAlarms.Remove(k);

                // ── System alarms: parse [ERROR] / [WARN] from log ──
                string logPath = Path.Combine(Application.StartupPath, "Logs",
                    string.Format("log_{0:yyyyMMdd}.txt", DateTime.Now));
                var newAlarms = new List<AlarmEntry>();

                if (File.Exists(logPath))
                {
                    var fileLines = File.ReadAllLines(logPath);
                    int startIdx = Math.Max(0, fileLines.Length - 400);
                    for (int idx = startIdx; idx < fileLines.Length; idx++)
                    {
                        string line = fileLines[idx];

                        if (!line.Contains("[ERROR]") && !line.Contains("[WARN]"))
                            continue;

                        if (IsHeartbeatNoise(line)) continue;

                        if (line.Contains("在创建窗口句柄之前") || line.Contains("不能在控件")
                            || line.Contains("线程间操作无效") || line.Contains("Invoke")
                            || line.Contains("模拟器采集异常") || line.Contains("采集异常"))
                            continue;

                        string level = line.Contains("[ERROR]") ? "ERROR" : "WARN";
                        DateTime time = DateTime.Now;
                        string message = line;
                        string sysDev = "System";

                        int bIdx = line.IndexOf('[');
                        if (bIdx > 0)
                        {
                            string ts2 = line.Substring(0, bIdx).Trim();
                            DateTime.TryParse(ts2, out time);
                        }
                        int lEnd = line.IndexOf(']', bIdx + 1);
                        if (lEnd > 0 && lEnd + 1 < line.Length)
                            message = line.Substring(lEnd + 1).Trim();

                        if (message.Contains("采集已启动"))
                            sysDev = "Collector";
                        else if (message.Contains("deviceId="))
                        {
                            int di = message.IndexOf("deviceId=");
                            if (di >= 0)
                            {
                                string sub = message.Substring(di + 9);
                                int sp = sub.IndexOfAny(new char[] { ' ', ',', ']' });
                                sysDev = sp > 0 ? sub.Substring(0, sp) : sub;
                            }
                        }

                        newAlarms.Add(new AlarmEntry
                        {
                            Time = time, Device = sysDev, Level = level,
                            Message = message.Length > 80 ? message.Substring(0, 80) : message,
                            Count = 1
                        });
                    }
                }

                var combined = new List<AlarmEntry>(newAlarms);
                foreach (var va in _activeVarAlarms.Values)
                    combined.Add(va);
                combined.Sort((a, b) => b.Time.CompareTo(a.Time));
                _recentAlarms = combined;
                while (_recentAlarms.Count > 20)
                    _recentAlarms.RemoveAt(0);
            }
            catch { }
        }

        private bool IsVarAlarm(string line)
        {
            if (!line.Contains("[ALARM ")) return false;
            if (line.Contains("[ALARM OK]")) return false;
            if (line.Contains("[ALARM HH]")) return true;
            if (line.Contains("[ALARM H]")) return true;
            if (line.Contains("[ALARM LL]")) return true;
            if (line.Contains("[ALARM L]")) return true;
            return false;
        }
        // ======================== REFRESH: RECENT ALARMS ========================
        // ======================== REFRESH: RECENT ALARMS (log-style) ========================
        private void RefreshAlarms()
        {
            try
            {
                if (_rtbAlarms == null || _rtbAlarms.IsDisposed) return;

                ParseAlarmsFromLog();

                DateTime cutoff = DateTime.Now.AddMinutes(-20);
                var visible = new List<AlarmEntry>();
                foreach (var alarm in _recentAlarms)
                {
                    if (alarm.Time >= cutoff)
                        visible.Add(alarm);
                }

                int maxDisplay = Math.Min(visible.Count, 12);
                if (visible.Count == 0)
                {
                    if (_rtbAlarms.TextLength == 0) return;
                    _rtbAlarms.Clear();
                    _rtbAlarms.SelectionColor = ClrTextDim;
                    _rtbAlarms.AppendText("No active alarms.");
                    return;
                }

                // Build full text to avoid flicker
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < maxDisplay; i++)
                {
                    var a = visible[i];
                    string lvl = GetAlarmLevelSymbol(a.Level);
                    string time = a.Time.ToString("HH:mm:ss");
                    string dev = (a.Device ?? "").PadRight(16);
                    string msg = a.Message ?? "";
                    string cnt = a.Count > 1 ? string.Format(" x{0}", a.Count) : "";
                    sb.AppendFormat("{0}  [{1}]  {2}  {3}{4}\n",
                        time, lvl.PadRight(4), dev, msg, cnt);
                }

                string newText = sb.ToString();
                // Only update if changed
                if (_rtbAlarms.Lines.Length >= maxDisplay + 2)
                {
                    var existingEnd = string.Join("\n", _rtbAlarms.Lines.Take(maxDisplay + 1));
                    if (existingEnd == newText.TrimEnd('\n')) return;
                }

                SendMessage(_rtbAlarms.Handle, WM_SETREDRAW, 0, 0);
                _rtbAlarms.ReadOnly = false;
                _rtbAlarms.Clear();

                // Append with level coloring
                foreach (string line in newText.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int bracketStart = line.IndexOf('[');
                    int bracketEnd = line.IndexOf(']');
                    if (bracketStart >= 0 && bracketEnd > bracketStart)
                    {
                        string before = line.Substring(0, bracketStart);
                        string bracket = line.Substring(bracketStart, bracketEnd - bracketStart + 1);
                        string after = line.Substring(bracketEnd + 1);

                        // Determine level color
                        Color lc = ClrTextDim;
                        if (bracket.Contains("HH") || bracket.Contains("ERR")) lc = ClrRed;
                        else if (bracket.Contains("H") || bracket.Contains("WRN")) lc = ClrAmber;

                        _rtbAlarms.SelectionStart = _rtbAlarms.TextLength;
                        _rtbAlarms.SelectionColor = ClrTextDim;
                        _rtbAlarms.AppendText(before);

                        _rtbAlarms.SelectionStart = _rtbAlarms.TextLength;
                        _rtbAlarms.SelectionColor = lc;
                        _rtbAlarms.AppendText(bracket);

                        _rtbAlarms.SelectionStart = _rtbAlarms.TextLength;
                        _rtbAlarms.SelectionColor = ClrTextMuted;
                        _rtbAlarms.AppendText(after + "\n");
                    }
                    else
                    {
                        _rtbAlarms.SelectionStart = _rtbAlarms.TextLength;
                        _rtbAlarms.SelectionColor = ClrTextDim;
                        _rtbAlarms.AppendText(line + "\n");
                    }
                }

                _rtbAlarms.ReadOnly = true;
                SendMessage(_rtbAlarms.Handle, WM_SETREDRAW, 1, 0);
                _rtbAlarms.Invalidate();
            }
            catch (Exception ex)
            {
                Logger.Debug("RefreshAlarms error: " + ex.Message);
            }
        }

        private Color GetLogAlarmColor(string level)
        {
            if (level.Contains("ERROR") || level.Contains("Error")) return ClrRed;
            if (level.Contains("WARN") || level.Contains("Warn")) return ClrAmber;
            return ClrTextMuted;
        }

        private string GetAlarmLevelSymbol(string level)
        {
            if (level.Contains("ERROR") || level.Contains("Error")) return "ERR";
            if (level.Contains("WARN") || level.Contains("Warn")) return "WRN";
            if (level == "HH") return "HH";
            if (level == "H") return "H ";
            if (level == "L") return "L ";
            if (level == "LL") return "LL";
            return "INF";
        }
        // ======================== REFRESH: DB CONNECTION TABLE ========================
        private void RefreshDbWriteStats()
        {
            try
            {
                if (_tblDbConn == null || _tblDbConn.IsDisposed) return;

                var statuses = DatabaseWriteService.Instance.GetConnectionStatuses();

                // Build hash to prevent flicker
                var sb = new StringBuilder();
                string[] dbKeys = { "SQLite", "MySQL", "SQLServer", "PostgreSQL", "TDengine" };
                foreach (var key in dbKeys)
                {
                    if (statuses.TryGetValue(key, out var s))
                        sb.Append(s.enabled ? "1" : "0").Append(s.connected ? "1" : "0").Append(s.deviceCount).Append("|");
                    else
                        sb.Append("00-|");
                }
                string hash = sb.ToString();
                if (hash == _lastDbConnHash) return;
                _lastDbConnHash = hash;

                // Update each row (row index = 1..5, skip header row 0)
                for (int r = 0; r < dbKeys.Length; r++)
                {
                    string dbKey = dbKeys[r];
                    bool enabled = false, connected = false;
                    int devCount = 0;
                    if (statuses.TryGetValue(dbKey, out var s))
                    {
                        enabled = s.enabled;
                        connected = s.connected;
                        devCount = s.deviceCount;
                    }

                    // Status panel (column 1) - contains dot Panel at index 0 and Label at index 1
                    var statusPanel = _tblDbConn.GetControlFromPosition(1, r + 1) as Panel;
                    if (statusPanel != null && statusPanel.Controls.Count >= 2)
                    {
                        var dot = statusPanel.Controls[0] as Panel;
                        var lbl = statusPanel.Controls[1] as Label;
                        if (dot != null && lbl != null)
                        {
                            if (!enabled)
                            {
                                dot.BackColor = ClrTextDim;
                                lbl.Text = "Disabled";
                                lbl.ForeColor = ClrTextDim;
                            }
                            else if (connected)
                            {
                                dot.BackColor = ClrGreen;
                                lbl.Text = "Connected";
                                lbl.ForeColor = ClrGreen;
                            }
                            else
                            {
                                dot.BackColor = ClrRed;
                                lbl.Text = "Disconnected";
                                lbl.ForeColor = ClrRed;
                            }
                        }
                    }

                    // Device count (column 2)
                    var lblDev = _tblDbConn.GetControlFromPosition(2, r + 1) as Label;
                    if (lblDev != null)
                    {
                        lblDev.Text = enabled ? devCount.ToString() : "--";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("RefreshDbConnTable error: " + ex.Message);
            }
        }

        // ======================== REFRESH: SYSTEM LOG ========================
        private void RefreshSystemLogs()
        {
            try
            {
                if (_txtSysLog == null || _txtSysLog.IsDisposed) return;

                string logPath = Path.Combine(Application.StartupPath, "Logs",
                    string.Format("log_{0:yyyyMMdd}.txt", DateTime.Now));

                // Log file changed (new day) → reset
                if (logPath != _logFilePath)
                {
                    _txtSysLog.Clear();
                    _logFilePosition = 0;
                    _logFilePath = logPath;
                }

                if (!File.Exists(logPath))
                {
                    if (_txtSysLog.TextLength == 0)
                    {
                        _txtSysLog.AppendText("No log file found.");
                        _txtSysLog.SelectAll();
                        _txtSysLog.SelectionColor = ClrTextDim;
                    }
                    return;
                }

                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (_logFilePosition > fs.Length)
                    {
                        // File was truncated/rotated
                        _txtSysLog.Clear();
                        _logFilePosition = 0;
                    }

                    if (_logFilePosition < fs.Length)
                    {
                        fs.Seek(_logFilePosition, SeekOrigin.Begin);
                        using (var reader = new StreamReader(fs))
                        {
                            string newContent = reader.ReadToEnd();
                            _logFilePosition = fs.Position;

                            if (!string.IsNullOrEmpty(newContent))
                            {
                                // Suppress redraw during batch updates to eliminate flicker
                                SendMessage(_txtSysLog.Handle, WM_SETREDRAW, 0, 0);
                                try
                                {
                                    string[] lines = newContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                                    foreach (string rawLine in lines)
                                    {
                                        if (string.IsNullOrEmpty(rawLine)) continue;
                                        string line = rawLine.TrimEnd('\r', '\n');
                                        if (IsHeartbeatNoise(line)) continue;

                                        Color lineColor = GetLogColor(line);
                                        _txtSysLog.SelectionStart = _txtSysLog.TextLength;
                                        _txtSysLog.SelectionLength = 0;
                                        _txtSysLog.SelectionColor = lineColor;
                                        _txtSysLog.AppendText(line + Environment.NewLine);
                                    }

                                    // Keep only last 200 lines to prevent memory bloat
                                    int totalLines = _txtSysLog.Lines.Length;
                                    if (totalLines > 250)
                                    {
                                        int trimToLine = totalLines - 200;
                                        int trimCharIdx = _txtSysLog.GetFirstCharIndexFromLine(trimToLine);
                                        if (trimCharIdx > 0)
                                        {
                                            _txtSysLog.ReadOnly = false;
                                            _txtSysLog.Select(0, trimCharIdx);
                                            _txtSysLog.SelectedText = "";
                                            _txtSysLog.ReadOnly = true;
                                        }
                                        _logFilePosition = Math.Max(0, _logFilePosition - 4096);
                                    }

                                    _txtSysLog.SelectionStart = _txtSysLog.TextLength;
                                    _txtSysLog.ScrollToCaret();
                                }
                                finally
                                {
                                    SendMessage(_txtSysLog.Handle, WM_SETREDRAW, 1, 0);
                                    _txtSysLog.Invalidate();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("RefreshSystemLogs error: " + ex.Message);
            }
        }

        private bool IsHeartbeatNoise(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string lower = line.ToLowerInvariant();
            // ALL Debug-level lines (collection details, read values, etc.)
            if (lower.Contains("[debug]")) return true;
            // Edge computing data lines (raw→processed values)
            if (lower.Contains("[edge]") || (lower.Contains("raw=") && lower.Contains("processed="))) return true;
            // Heartbeat lines (not real disconnects)
            if (lower.Contains("心跳")) return true;
            // MQTT transient disconnect / reconnect (heartbeat, keep-alive, auto-reconnect)
            if (lower.Contains("mqtt"))
            {
                if (lower.Contains("断开") || lower.Contains("reconnect") || lower.Contains("重连")
                    || lower.Contains("disconnect") || lower.Contains("心跳")
                    || lower.Contains("自动重连") || lower.Contains("keep")
                    || lower.Contains("连接失败") || lower.Contains("连接异常"))
                    return true;
            }
            // Database transient disconnect / auto-reconnect
            if ((lower.Contains("数据库") || lower.Contains("database") || lower.Contains("db"))
                && (lower.Contains("重连") || lower.Contains("reconnect") || lower.Contains("已恢复")
                    || lower.Contains("仍不可用")))
                return true;
            // Offline cache retry / heartbeat
            if (lower.Contains("离线缓存") && (lower.Contains("补发") || lower.Contains("重试")))
                return true;
            if (lower.Contains("mqtt 补发") || lower.Contains("db 补发") || lower.Contains("补发失败"))
                return true;
            // Dashboard internal refresh errors
            if (lower.Contains("dashboard") || lower.Contains("refreshdriverdistribution")
                || lower.Contains("refreshsystemlog") || lower.Contains("refreshdevices")
                || lower.Contains("refreshtrou"))
                return true;
            return false;
        }

        private Color GetLogColor(string line)
        {
            if (line.Contains("[ERROR]")) return ClrRed;
            if (line.Contains("[WARN]")) return ClrAmber;
            if (line.Contains("[INFO]")) return ClrTextMuted;
            if (line.Contains("[DEBUG]")) return ColorTranslator.FromHtml("#4b5563");
            return ClrTextDim;
        }

        // ======================== REFRESH: CONNECTION HEALTH ========================
        private int _lastDevRowCount = 0;

        // ======================== REFRESH: DEVICE COLLECTION STATUS ========================
        private void RefreshConnectionHealth()
        {
            try
            {
                if (_tblDevStatus == null || _tblDevStatus.IsDisposed) return;

                var devices = ConfigService.Instance.LoadDevices();
                if (devices.Count == 0) { ClearDevTable(); return; }

                int rowCount = devices.Count;
                // Full rebuild only when device count changes
                if (rowCount != _lastDevRowCount)
                {
                    RebuildDevTable(devices);
                    _lastDevRowCount = rowCount;
                    return;
                }

                // Incremental: update status cells for existing rows
                bool mqttOk = MqttPublishService.Instance != null && MqttPublishService.Instance.IsConnected;
                bool apiOk = RestApiService.Instance != null && RestApiService.Instance.IsRunning;

                _tblDevStatus.SuspendLayout();
                for (int i = 0; i < devices.Count; i++)
                {
                    var dev = devices[i];
                    int row = i;

                    var lblCol = _tblDevStatus.GetControlFromPosition(1, row) as Label;
                    if (lblCol != null)
                    {
                        bool running = DataCollectionService.Instance.IsDeviceRunning(dev.Id);
                        lblCol.Text = running ? LanguageManager.Instance.GetString("Dashboard_DevStatus_Running") : LanguageManager.Instance.GetString("Dashboard_DevStatus_Stopped");
                        lblCol.ForeColor = running ? ClrGreen : ClrRed;
                    }

                    var lblMqtt = _tblDevStatus.GetControlFromPosition(2, row) as Label;
                    if (lblMqtt != null)
                    {
                        lblMqtt.Text = dev.Enabled ? (mqttOk ? LanguageManager.Instance.GetString("Dashboard_DevStatus_OK") : LanguageManager.Instance.GetString("Dashboard_DevStatus_Off")) : "--";
                        lblMqtt.ForeColor = dev.Enabled && mqttOk ? ClrGreen : ClrTextDim;
                    }

                    var lblApi = _tblDevStatus.GetControlFromPosition(3, row) as Label;
                    if (lblApi != null)
                    {
                        lblApi.Text = apiOk ? LanguageManager.Instance.GetString("Dashboard_DevStatus_OK") : LanguageManager.Instance.GetString("Dashboard_DevStatus_Off");
                        lblApi.ForeColor = apiOk ? ClrGreen : ClrTextDim;
                    }
                }
                _tblDevStatus.ResumeLayout(false);
            }
            catch (Exception ex)
            {
                Logger.Debug("RefreshDevStatus error: " + ex.Message);
            }
        }

        private void ClearDevTable()
        {
            for (int r = _tblDevStatus.RowCount - 1; r >= 0; r--)
            {
                for (int c = 0; c < _tblDevStatus.ColumnCount; c++)
                {
                    var ctrl = _tblDevStatus.GetControlFromPosition(c, r);
                    if (ctrl != null) _tblDevStatus.Controls.Remove(ctrl);
                }
            }
            _tblDevStatus.RowCount = 0;
            _tblDevStatus.RowStyles.Clear();
            _lastDevRowCount = 0;
        }

        private void RebuildDevTable(List<DeviceConfig> devices)
        {
            ClearDevTable();
            int rowCount = devices.Count;
            _tblDevStatus.RowCount = rowCount;

            bool mqttOk = MqttPublishService.Instance != null && MqttPublishService.Instance.IsConnected;
            bool apiOk = RestApiService.Instance != null && RestApiService.Instance.IsRunning;

            _tblDevStatus.SuspendLayout();
            for (int i = 0; i < devices.Count; i++)
            {
                var dev = devices[i];
                int row = i;
                _tblDevStatus.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));

                var lblName = new Label
                {
                    Text = dev.Name ?? dev.Id,
                    Font = new Font("Segoe UI", 8F),
                    ForeColor = ClrText, BackColor = Color.Transparent,
                    Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 0, 0, 0), AutoEllipsis = true
                };
                _tblDevStatus.Controls.Add(lblName, 0, row);

                bool running = DataCollectionService.Instance.IsDeviceRunning(dev.Id);
                var lblCol = new Label
                {
                    Text = running ? LanguageManager.Instance.GetString("Dashboard_DevStatus_Running") : LanguageManager.Instance.GetString("Dashboard_DevStatus_Stopped"),
                    Font = new Font("Segoe UI", 8F),
                    ForeColor = running ? ClrGreen : ClrRed, BackColor = Color.Transparent,
                    Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
                };
                _tblDevStatus.Controls.Add(lblCol, 1, row);

                var lblMqtt = new Label
                {
                    Text = dev.Enabled ? (mqttOk ? LanguageManager.Instance.GetString("Dashboard_DevStatus_OK") : LanguageManager.Instance.GetString("Dashboard_DevStatus_Off")) : "--",
                    Font = new Font("Segoe UI", 8F),
                    ForeColor = dev.Enabled && mqttOk ? ClrGreen : ClrTextDim,
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
                };
                _tblDevStatus.Controls.Add(lblMqtt, 2, row);

                var lblApi = new Label
                {
                    Text = apiOk ? LanguageManager.Instance.GetString("Dashboard_DevStatus_OK") : LanguageManager.Instance.GetString("Dashboard_DevStatus_Off"),
                    Font = new Font("Segoe UI", 8F),
                    ForeColor = apiOk ? ClrGreen : ClrTextDim,
                    BackColor = Color.Transparent,
                    Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter
                };
                _tblDevStatus.Controls.Add(lblApi, 3, row);
            }
            _tblDevStatus.ResumeLayout(false);
        }
        private string TruncateText(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= maxLen) return text;
            return text.Substring(0, maxLen - 2) + "..";
        }

        // ======================== I18N ========================
        private void ApplyLanguage()
        {
            try
            {
                var L = LanguageManager.Instance;
                this.Text = L.GetString("Dashboard_Title");

                if (_lblHeaderTitle != null && !_lblHeaderTitle.IsDisposed)
                    _lblHeaderTitle.Text = L.GetString("Dashboard_Title");
                if (_btnCollection != null && !_btnCollection.IsDisposed)
                    _btnCollection.Text = L.GetString("Dashboard_OpenCollection");

                // Card titles
                if (_lblCard1Title != null && !_lblCard1Title.IsDisposed)
                    _lblCard1Title.Text = L.GetString("Dashboard_DeviceOverview");
                if (_lblCard2Title != null && !_lblCard2Title.IsDisposed)
                    _lblCard2Title.Text = L.GetString("Dashboard_Throughput");
                if (_lblCard3Title != null && !_lblCard3Title.IsDisposed)
                    _lblCard3Title.Text = L.GetString("Dashboard_ServiceStatus");
                if (_lblCard4Title != null && !_lblCard4Title.IsDisposed)
                    _lblCard4Title.Text = L.GetString("Dashboard_DriverDistribution");
                if (_lblCard5Title != null && !_lblCard5Title.IsDisposed)
                    _lblCard5Title.Text = L.GetString("Dashboard_RecentAlarms");
                if (_lblCard6Title != null && !_lblCard6Title.IsDisposed)
                    _lblCard6Title.Text = L.GetString("Dashboard_DbConnTitle");
                if (_lblCard7Title != null && !_lblCard7Title.IsDisposed)
                    _lblCard7Title.Text = L.GetString("Dashboard_SystemLog");
                if (_lblCard8Title != null && !_lblCard8Title.IsDisposed)
                    _lblCard8Title.Text = L.GetString("Dashboard_DevStatusTitle");

                // Device overview labels
                if (_lblTotalLabel != null && !_lblTotalLabel.IsDisposed)
                    _lblTotalLabel.Text = L.GetString("Dashboard_TotalDevices");
                if (_lblOnlineLabel != null && !_lblOnlineLabel.IsDisposed)
                    _lblOnlineLabel.Text = L.GetString("Dashboard_Online");
                if (_lblOfflineLabel != null && !_lblOfflineLabel.IsDisposed)
                    _lblOfflineLabel.Text = L.GetString("Dashboard_Offline");
                if (_lblErrorLabel != null && !_lblErrorLabel.IsDisposed)
                    _lblErrorLabel.Text = L.GetString("Dashboard_Error");

                // Service names
                if (_lblMqttName != null && !_lblMqttName.IsDisposed)
                    _lblMqttName.Text = L.GetString("Dashboard_MqttBroker");
                if (_lblDbName != null && !_lblDbName.IsDisposed)
                    _lblDbName.Text = L.GetString("Dashboard_DatabaseWrite");
                if (_lblMcpName != null && !_lblMcpName.IsDisposed)
                    _lblMcpName.Text = L.GetString("Dashboard_McpService");
                if (_lblRestName != null && !_lblRestName.IsDisposed)
                    _lblRestName.Text = L.GetString("Dashboard_RestApi");
            }
            catch { }
        }

        /// <summary>
        /// 释放资源——清理定时器、事件订阅，防止内存泄漏
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
                _clockTimer?.Stop();
                _clockTimer?.Dispose();
                _refreshTimer = null;
                _clockTimer = null;
            }
            base.Dispose(disposing);
        }
    }
}
