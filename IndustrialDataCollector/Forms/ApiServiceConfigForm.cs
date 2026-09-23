using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// REST API 服务配置窗口 — 简洁专业风格
    /// </summary>
    public partial class ApiServiceConfigForm : Form
    {
        // === 配色 ===
        private static readonly Color CAccent = Color.FromArgb(0, 122, 204);
        private static readonly Color CText = Color.FromArgb(33, 33, 33);
        private static readonly Color CTextSub = Color.FromArgb(120, 120, 120);
        private static readonly Color CBg = SystemColors.Control;
        private static readonly Color CBorder = Color.FromArgb(200, 200, 200);

        private const int FORM_W = 620;
        private const int FORM_H = 460;
        private const int PAD = 24;
        private const int COL1 = 24;      // 标签列起点
        private const int COL1_W = 80;     // 标签宽度
        private const int COL2 = 112;      // 输入框列起点
        private const int COL2_W = 80;     // 端口输入框宽
        private const int CARD_W = 572;    // 内容区宽 = 620-24*2
        private const int ROW_H = 34;      // 行高
        private const int BTN_W = 100;
        private const int BTN_H = 34;

        private RestApiService _service;
        private string _configPath;
        private ApiServiceConfig _config;
        private bool _initialized;

        // === 控件 ===
        private Label lblTitle;
        // 状态行
        private Label lblStatusDot;
        private Label lblStatusText;
        private Label lblStatusPort;
        private Button btnStart;
        private Button btnStop;
        // 基本配置
        private Label lblPort;
        private NumericUpDown numPort;
        private CheckBox chkEnable;
        // 安全配置
        private CheckBox chkToken;
        private Label lblToken;
        private TextBox txtToken;
        private Button btnGenToken;
        // 文档配置
        private CheckBox chkSwagger;
        private Label lblSwaggerUrl;
        private LinkLabel linkSwagger;
        // 底部
        private Button btnSave;
        private Button btnCancel;

        public ApiServiceConfigForm(RestApiService service)
        {
            _service = service;
            _configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndustrialDataCollection", "config", "apiConfig.json");

            LoadConfig();
            InitializeForm();
            BuildUI();
            ApplyLanguage();
            BindEvents();
            UpdateStatusUI();
            _initialized = true;
        }

        // ======================== 配置模型与持久化 ========================

        private class ApiServiceConfig
        {
            public int Port { get; set; } = 5000;
            public bool Enabled { get; set; } = true;
            public bool TokenAuth { get; set; } = true;
            public string ApiToken { get; set; } = "admin123";
            public bool Swagger { get; set; } = true;
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    _config = JsonConvert.DeserializeObject<ApiServiceConfig>(
                        File.ReadAllText(_configPath, System.Text.Encoding.UTF8));
                }
            }
            catch { }
            if (_config == null) _config = new ApiServiceConfig();
        }

        private void SaveConfig()
        {
            try
            {
                string dir = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_configPath,
                    JsonConvert.SerializeObject(_config, Formatting.Indented),
                    System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error("保存API配置失败: " + ex.Message);
            }
        }

        // ======================== 初始化 ========================

        private void InitializeForm()
        {
            Text = "REST API 服务配置";
            ClientSize = new Size(FORM_W, FORM_H);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Icon = Program.AppIcon;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CBg;
            Font = new Font("Microsoft YaHei", 9F, FontStyle.Regular);
        }

        private void BuildUI()
        {
            // === 标题 ===
            lblTitle = AddLabel(PAD, 12, CARD_W, 32, "REST API 服务配置", new Font("Microsoft YaHei", 13F, FontStyle.Bold), CText);

            // === 分隔线 1 ===
            AddSeparator(PAD, 48, CARD_W);

            // === 状态行 (y=56) ===
            int sy = 60;
            lblStatusDot = AddLabel(PAD, sy + 4, 16, 16, "●", new Font("Microsoft YaHei", 8F), Color.Gray);
            lblStatusText = AddLabel(PAD + 18, sy + 2, 100, 22, "○ 已停止", Font, CText);
            lblStatusPort = AddLabel(PAD + 130, sy + 2, 120, 22, "端口: 5000", Font, CTextSub);

            btnStart = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 30),
                Location = new Point(FORM_W - PAD - 100 - 100 - 8, sy),
                Cursor = Cursors.Hand
            };
            Controls.Add(btnStart);

            btnStop = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 30),
                Location = new Point(FORM_W - PAD - 100, sy),
                Cursor = Cursors.Hand
            };
            Controls.Add(btnStop);

            // === 分隔线 2 ===
            AddSeparator(PAD, sy + 42, CARD_W);

            // === 基本配置 (y=same+50) ===
            int y = sy + 54;
            lblPort = AddLabel(PAD, y, COL1_W, 22, "监听端口:", Font, CText);
            numPort = new NumericUpDown
            {
                Minimum = 1, Maximum = 65535, Value = _config.Port,
                Location = new Point(COL2, y - 1),
                Size = new Size(COL2_W, 26),
                Font = Font,
                TextAlign = HorizontalAlignment.Center
            };
            Controls.Add(numPort);

            chkEnable = new CheckBox
            {
                Checked = _config.Enabled,
                Location = new Point(COL2 + COL2_W + 16, y - 1),
                Size = new Size(260, 24),
                Font = Font
            };
            Controls.Add(chkEnable);

            // === 安全配置 ===
            y += ROW_H;
            chkToken = new CheckBox
            {
                Checked = _config.TokenAuth,
                Location = new Point(PAD, y - 1),
                Size = new Size(260, 24),
                Font = Font
            };
            Controls.Add(chkToken);

            y += ROW_H;
            lblToken = AddLabel(PAD, y, COL1_W, 22, "API Token:", Font, CText);
            txtToken = new TextBox
            {
                Text = _config.ApiToken,
                Location = new Point(COL2, y - 1),
                Size = new Size(260, 26),
                Font = new Font("Consolas", 9F),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(txtToken);

            btnGenToken = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Text = "生成",
                Size = new Size(56, 26),
                Location = new Point(COL2 + 268, y - 1),
                Cursor = Cursors.Hand
            };
            btnGenToken.FlatAppearance.BorderColor = CBorder;
            btnGenToken.FlatAppearance.BorderSize = 1;
            Controls.Add(btnGenToken);

            // === 文档配置 ===
            y += ROW_H + 4;
            AddSeparator(PAD, y, CARD_W);
            y += 12;

            chkSwagger = new CheckBox
            {
                Checked = _config.Swagger,
                Location = new Point(PAD, y - 1),
                Size = new Size(260, 24),
                Font = Font
            };
            Controls.Add(chkSwagger);

            y += ROW_H;
            lblSwaggerUrl = AddLabel(PAD, y, COL1_W, 22, "Swagger:", Font, CText);
            linkSwagger = new LinkLabel
            {
                Text = "http://localhost:" + _config.Port + "/swagger/index.html",
                Location = new Point(COL2, y),
                Size = new Size(380, 22),
                Font = Font,
                LinkColor = CAccent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(linkSwagger);

            // === 分隔线 3 ===
            y += ROW_H + 4;
            AddSeparator(PAD, y, CARD_W);

            // === API 端点 ===
            y += 12;
            AddLabel(PAD, y, COL1_W, 22, "API 端点:", Font, CText);
            var txtEndpoints = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.5F),
                ForeColor = CText,
                Location = new Point(COL2, y),
                Size = new Size(460, 74),
                Text = "GET  /api                          — 服务基本信息\r\n" +
                       "GET  /api/devices                  — 查询所有已采集设备\r\n" +
                       "GET  /api/realtime?device={name}    — 查询设备实时数据（参数方式）\r\n" +
                       "GET  /api/device/{name}/realtime    — 查询设备实时数据（路径方式）"
            };
            Controls.Add(txtEndpoints);

            // === 底部按钮 ===
            y += 86;
            btnSave = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Size = new Size(BTN_W, BTN_H),
                Location = new Point(FORM_W - PAD - BTN_W - BTN_W - 12, y),
                Cursor = Cursors.Hand
            };
            Controls.Add(btnSave);

            btnCancel = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Size = new Size(BTN_W, BTN_H),
                Location = new Point(FORM_W - PAD - BTN_W, y),
                Cursor = Cursors.Hand
            };
            Controls.Add(btnCancel);
        }

        // ======================== 辅助方法 ========================

        private Label AddLabel(int x, int y, int w, int h, string text, Font f, Color c)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                Font = f,
                ForeColor = c,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };
            Controls.Add(lbl);
            return lbl;
        }

        private void AddSeparator(int x, int y, int w)
        {
            var sep = new Label
            {
                Location = new Point(x, y),
                Size = new Size(w, 1),
                BackColor = CBorder,
                BorderStyle = BorderStyle.None,
                AutoSize = false
            };
            Controls.Add(sep);
        }

        // ======================== 事件 ========================

        private void BindEvents()
        {
            btnStart.Click += (s, e) =>
            {
                try
                {
                    ApplyToService();
                    _service.Start();
                    UpdateStatusUI();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            btnStop.Click += (s, e) =>
            {
                _service.Stop();
                UpdateStatusUI();
            };

            btnSave.Click += (s, e) =>
            {
                if (!ValidateInput()) return;
                ReadConfig();
                SaveConfig();
                ApplyToService();
                UpdateStatusUI();
                var L = LanguageManager.Instance;
                MessageBox.Show(L.GetString("ApiService_Saved"), "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            btnCancel.Click += (s, e) => Close();

            btnGenToken.Click += (s, e) =>
            {
                txtToken.Text = GenerateToken();
            };

            numPort.ValueChanged += (s, e) =>
            {
                if (_initialized) UpdateSwaggerLink();
            };

            chkToken.CheckedChanged += (s, e) =>
            {
                bool en = chkToken.Checked;
                lblToken.Enabled = en;
                txtToken.Enabled = en;
                btnGenToken.Enabled = en;
            };

            linkSwagger.LinkClicked += (s, e) =>
            {
                try { System.Diagnostics.Process.Start(linkSwagger.Text); } catch { }
            };
        }

        private bool ValidateInput()
        {
            var L = LanguageManager.Instance;
            if (numPort.Value < 1 || numPort.Value > 65535)
            {
                MessageBox.Show(L.GetString("ApiService_PortInvalid"), "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (chkToken.Checked && string.IsNullOrWhiteSpace(txtToken.Text))
            {
                MessageBox.Show(L.GetString("ApiService_TokenEmpty"), "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private void ReadConfig()
        {
            _config.Port = (int)numPort.Value;
            _config.Enabled = chkEnable.Checked;
            _config.TokenAuth = chkToken.Checked;
            _config.ApiToken = txtToken.Text.Trim();
            _config.Swagger = chkSwagger.Checked;
        }

        private void ApplyToService()
        {
            _service.Port = (int)numPort.Value;
            _service.TokenAuthEnabled = chkToken.Checked;
            _service.ApiToken = txtToken.Text.Trim();
            _service.SwaggerEnabled = chkSwagger.Checked;
        }

        private void UpdateStatusUI()
        {
            bool running = _service.IsRunning;
            var L = LanguageManager.Instance;

            lblStatusDot.Text = running ? "●" : "●";
            lblStatusDot.ForeColor = running ? Color.FromArgb(34, 197, 94) : Color.FromArgb(180, 180, 180);
            lblStatusText.Text = running ? L.GetString("ApiService_Running") : L.GetString("ApiService_Stopped");
            lblStatusText.ForeColor = running ? CText : CTextSub;
            lblStatusPort.Text = "端口: " + _service.Port;

            btnStart.Text = L.GetString("ApiService_Start");
            btnStart.BackColor = running ? SystemColors.ControlLight : Color.FromArgb(34, 197, 94);
            btnStart.ForeColor = running ? CTextSub : Color.White;
            btnStart.FlatAppearance.BorderSize = running ? 1 : 0;
            btnStart.FlatAppearance.BorderColor = CBorder;
            btnStart.Enabled = !running;

            btnStop.Text = L.GetString("ApiService_Stop");
            btnStop.BackColor = running ? Color.FromArgb(220, 38, 38) : SystemColors.ControlLight;
            btnStop.ForeColor = running ? Color.White : CTextSub;
            btnStop.FlatAppearance.BorderSize = running ? 0 : 1;
            btnStop.FlatAppearance.BorderColor = CBorder;
            btnStop.Enabled = running;
        }

        private void UpdateSwaggerLink()
        {
            linkSwagger.Text = string.Format("http://localhost:{0}/swagger/index.html", (int)numPort.Value);
        }

        // ======================== i18n ========================

        public void ApplyLanguage()
        {
            var L = LanguageManager.Instance;
            Text = L.GetString("ApiService_Title");
            lblTitle.Text = L.GetString("ApiService_Title");
            lblPort.Text = L.GetString("ApiService_Port") + ":";
            chkEnable.Text = L.GetString("ApiService_EnableService");
            chkToken.Text = L.GetString("ApiService_EnableToken");
            lblToken.Text = L.GetString("ApiService_Token") + ":";
            btnGenToken.Text = L.GetString("ApiService_GenerateToken");
            chkSwagger.Text = L.GetString("ApiService_EnableSwagger");
            lblSwaggerUrl.Text = L.GetString("ApiService_SwaggerUrl") + ":";
            btnSave.Text = L.GetString("ApiService_Save");
            btnCancel.Text = L.GetString("ApiService_Cancel");
            UpdateStatusUI();
        }

        private string GenerateToken()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var rnd = new Random();
            var buf = new char[16];
            for (int i = 0; i < buf.Length; i++)
                buf[i] = chars[rnd.Next(chars.Length)];
            return string.Format("MatriX-{0}{1}{2}{3}-{4}{5}{6}{7}-{8}{9}{10}{11}",
                buf[0], buf[1], buf[2], buf[3],
                buf[4], buf[5], buf[6], buf[7],
                buf[8], buf[9], buf[10], buf[11]);
        }
    }
}
