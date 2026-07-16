using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// MCP 服务配置窗口 — 与 REST API 配置窗口平行设计
    /// </summary>
    public partial class McpConfigForm : Form
    {
        // === 配色 ===
        private static readonly Color CAccent = Color.FromArgb(0, 122, 204);
        private static readonly Color CText = Color.FromArgb(33, 33, 33);
        private static readonly Color CTextSub = Color.FromArgb(120, 120, 120);
        private static readonly Color CBg = SystemColors.Control;
        private static readonly Color CBorder = Color.FromArgb(200, 200, 200);

        private const int FORM_W = 620;
        private const int FORM_H = 800;
        private const int PAD = 24;
        private const int COL1 = 24;
        private const int COL1_W = 80;
        private const int COL2 = 112;
        private const int COL2_W = 80;
        private const int CARD_W = 572;
        private const int ROW_H = 34;
        private const int BTN_W = 100;
        private const int BTN_H = 34;

        private McpService _service;
        private string _configPath;
        private McpServiceConfig _config;
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
        // 服务信息
        private Label lblInfoTitle;
        private TextBox txtTools;
        // 底部
        private Button btnSave;
        private Button btnCancel;

        public McpConfigForm(McpService service)
        {
            _service = service;
            _configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndustrialDataCollection", "config", "mcpConfig.json");

            LoadConfig();
            InitializeForm();
            BuildUI();
            ApplyLanguage();
            BindEvents();
            UpdateStatusUI();
            _initialized = true;
        }

        // ======================== 配置模型与持久化 ========================

        private class McpServiceConfig
        {
            public int Port { get; set; } = 5101;
            public bool Enabled { get; set; } = false;
            public bool TokenAuth { get; set; } = true;
            public string McpToken { get; set; } = "admin123";
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    _config = JsonConvert.DeserializeObject<McpServiceConfig>(
                        File.ReadAllText(_configPath, System.Text.Encoding.UTF8));
                }
            }
            catch { }
            if (_config == null) _config = new McpServiceConfig();
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
                Logger.Error("保存MCP配置失败: " + ex.Message);
            }
        }

        // ======================== 初始化 ========================

        private void InitializeForm()
        {
            Text = "MCP 服务配置";
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
            lblTitle = AddLabel(PAD, 12, CARD_W, 32, "MCP 服务配置", new Font("Microsoft YaHei", 13F, FontStyle.Bold), CText);

            // === 分隔线 1 ===
            AddSeparator(PAD, 48, CARD_W);

            // === 状态行 (y=56) ===
            int sy = 60;
            lblStatusDot = AddLabel(PAD, sy + 4, 16, 16, "●", new Font("Microsoft YaHei", 8F), Color.Gray);
            lblStatusText = AddLabel(PAD + 18, sy + 2, 140, 22, "○ 已停止", Font, CText);
            lblStatusPort = AddLabel(PAD + 170, sy + 2, 120, 22, "端口: 5100", Font, CTextSub);

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

            // === 基本配置 ===
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
                Size = new Size(300, 24),
                Font = Font
            };
            Controls.Add(chkEnable);

            // === 安全配置 ===
            y += ROW_H + 4;
            AddSeparator(PAD, y, CARD_W);
            y += 12;

            chkToken = new CheckBox
            {
                Checked = _config.TokenAuth,
                Location = new Point(PAD, y - 1),
                Size = new Size(320, 24),
                Font = Font
            };
            Controls.Add(chkToken);

            y += ROW_H;
            lblToken = AddLabel(PAD, y, COL1_W, 22, "MCP Token:", Font, CText);
            txtToken = new TextBox
            {
                Text = _config.McpToken,
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

            // === 协议说明 ===
            y += ROW_H + 8;
            AddSeparator(PAD, y, CARD_W);
            y += 12;

            lblInfoTitle = AddLabel(PAD, y, CARD_W, 22, "MCP 端点与可用工具:", Font, CTextSub);
            y += 28;

            txtTools = new TextBox
            {
                Multiline = true,
                ReadOnly = false,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.5F),
                ForeColor = CText,
                Location = new Point(PAD, y),
                Size = new Size(CARD_W, 420),
                ScrollBars = ScrollBars.Vertical,
                TabStop = false,
                ShortcutsEnabled = true,
                Text = "端点:  POST http://localhost:" + _config.Port + "/mcp?token={token}\r\n" +
                       "协议:  JSON-RPC 2.0 / Streamable HTTP (MCP 2025-03-26)\r\n" +
                       "\r\n" +
                       "── 实时采集 ──────────────────────────────\r\n" +
                       "  ▸ query_realtime_data    查询设备实时数据\r\n" +
                       "  ▸ list_devices           设备列表与采集状态\r\n" +
                       "  ▸ get_device_status      设备详细状态\r\n" +
                       "  ▸ get_device_config      设备完整配置\r\n" +
                       "\r\n" +
                       "── 历史与存储 ────────────────────────────\r\n" +
                       "  ▸ query_history_data     历史数据查询\r\n" +
                       "  ▸ get_database_status    数据库写入状态\r\n" +
                       "  ▸ repair_database        修复数据库连接\r\n" +
                       "  ▸ semantic_execute_query 数据源SQL直查\r\n" +
                       "\r\n" +
                       "── AI 设备管理 (读写) ────────────────────\r\n" +
                       "  ▸ add_device               添加设备\r\n" +
                       "  ▸ update_device            更新设备配置\r\n" +
                       "  ▸ add_variables            添加变量点\r\n" +
                       "  ▸ update_variables         更新变量点\r\n" +
                       "  ▸ reload_config            热重载配置(优雅8步流程)\r\n" +
                       "\r\n" +
                       "── 语义层 v2 (灵活层级树) ─────────────────\r\n" +
                       "  ▸ semantic_list_nodes           查询语义节点(按类型/父级/关键词/状态)\r\n" +
                       "  ▸ semantic_get_full_tree        完整语义树(递归,AI空间感知)\r\n" +
                       "  ▸ semantic_get_node_path        节点完整路径\r\n" +
                       "  ▸ semantic_get_realtime_snapshot 子树实时值快照(AI监控)\r\n" +
                       "  ▸ semantic_get_data_flow        数据链路追溯(设备→变量→表字段)\r\n" +
                       "  ▸ semantic_get_alarm_summary    报警聚合摘要(按变量/级别统计)\r\n" +
                       "  ▸ semantic_suggest_relations    智能推荐变量-字段映射(Jaro-Winkler)\r\n" +
                       "\r\n" +
                       "── 变量关系与事件 (读写) ──────────────────\r\n" +
                       "  ▸ semantic_list_variable_relations    变量关系查询\r\n" +
                       "  ▸ semantic_create_variable_relation   创建变量关系(17种类型)\r\n" +
                       "  ▸ semantic_list_node_relations        节点关系查询\r\n" +
                       "  ▸ semantic_list_events               节点事件(支持时间/类型/级别筛选)\r\n" +
                       "  ▸ semantic_create_event_config       创建事件配置(9种×12管线)\r\n" +
                       "  ▸ semantic_list_device_variables     设备变量+实时值\r\n" +
                       "\r\n" +
                       "── 批量操作与维护 ─────────────────────────\r\n" +
                       "  ▸ semantic_batch_update_nodes  AI批量维护标签(名称/描述/属性,单次100条)\r\n" +
                       "\r\n" +
                       "── 兼容工具(旧版) ─────────────────────────\r\n" +
                       "\r\n" +
                       "── 🔬 Fabric 声明式分析引擎 ───────────────\r\n" +
                       "  ▸ fabric_list_operators   列出可用分析算子\r\n" +
                       "  ▸ fabric_execute          执行分析(聚合/趋势/报警/日报)\r\n" +
                       "  ▸ semantic_list_workshops         车间列表\r\n" +
                       "  ▸ semantic_list_production_lines  产线列表\r\n" +
                       "  ▸ semantic_list_equipments        设备列表\r\n" +
                       "  ▸ semantic_list_tags              变量标签列表\r\n" +
                       "\r\n" +
                       "调用方式: POST JSON-RPC → {\"jsonrpc\":\"2.0\",\"method\":\"tools/call\",\"params\":{\"name\":\"工具名\",\"arguments\":{...}}}\r\n" +
                       "Token:   Header Authorization: Bearer <token> 或 URL ?token=<token>"
            };
            Controls.Add(txtTools);

            // === 底部按钮 ===
            y += 430;
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
                    // 如果端口被自动切换，同步回 UI
                    if ((int)numPort.Value != _service.Port)
                        numPort.Value = _service.Port;
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
                MessageBox.Show(L.GetString("McpService_Saved"), "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            btnCancel.Click += (s, e) => Close();

            btnGenToken.Click += (s, e) =>
            {
                txtToken.Text = GenerateToken();
            };

            numPort.ValueChanged += (s, e) =>
            {
                if (_initialized) UpdateToolsText();
            };

            chkToken.CheckedChanged += (s, e) =>
            {
                bool en = chkToken.Checked;
                lblToken.Enabled = en;
                txtToken.Enabled = en;
                btnGenToken.Enabled = en;
            };
        }

        private bool ValidateInput()
        {
            var L = LanguageManager.Instance;
            if (numPort.Value < 1 || numPort.Value > 65535)
            {
                MessageBox.Show(L.GetString("McpService_PortInvalid"), "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (chkToken.Checked && string.IsNullOrWhiteSpace(txtToken.Text))
            {
                MessageBox.Show(L.GetString("McpService_TokenEmpty"), "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private void ReadConfig()
        {
            _config.Port = (int)numPort.Value;
            _config.Enabled = chkEnable.Checked;
            _config.TokenAuth = chkToken.Checked;
            _config.McpToken = txtToken.Text.Trim();
        }

        private void ApplyToService()
        {
            _service.Port = (int)numPort.Value;
            _service.TokenAuthEnabled = chkToken.Checked;
            _service.McpToken = txtToken.Text.Trim();
        }

        private void UpdateStatusUI()
        {
            bool running = _service.IsRunning;
            var L = LanguageManager.Instance;

            lblStatusDot.Text = running ? "●" : "●";
            lblStatusDot.ForeColor = running ? Color.FromArgb(34, 197, 94) : Color.FromArgb(180, 180, 180);
            lblStatusText.Text = running ? L.GetString("McpService_Running") : L.GetString("McpService_Stopped");
            lblStatusText.ForeColor = running ? CText : CTextSub;
            lblStatusPort.Text = "端口: " + _service.Port;

            btnStart.Text = L.GetString("McpService_Start");
            btnStart.BackColor = running ? SystemColors.ControlLight : Color.FromArgb(34, 197, 94);
            btnStart.ForeColor = running ? CTextSub : Color.White;
            btnStart.FlatAppearance.BorderSize = running ? 1 : 0;
            btnStart.FlatAppearance.BorderColor = CBorder;
            btnStart.Enabled = !running;

            btnStop.Text = L.GetString("McpService_Stop");
            btnStop.BackColor = running ? Color.FromArgb(220, 38, 38) : SystemColors.ControlLight;
            btnStop.ForeColor = running ? Color.White : CTextSub;
            btnStop.FlatAppearance.BorderSize = running ? 0 : 1;
            btnStop.FlatAppearance.BorderColor = CBorder;
            btnStop.Enabled = running;

            UpdateToolsText();
        }

        private void UpdateToolsText()
        {
            int p = (int)numPort.Value;
            txtTools.Text = "端点:  POST http://localhost:" + p + "/mcp?token={token}\r\n" +
                       "协议:  JSON-RPC 2.0 / Streamable HTTP (MCP 2025-03-26)\r\n" +
                       "\r\n" +
                       "── 实时采集 ──────────────────────────────\r\n" +
                       "  ▸ query_realtime_data    查询设备实时数据\r\n" +
                       "  ▸ list_devices           设备列表与采集状态\r\n" +
                       "  ▸ get_device_status      设备详细状态\r\n" +
                       "  ▸ get_device_config      设备完整配置\r\n" +
                       "\r\n" +
                       "── 历史与存储 ────────────────────────────\r\n" +
                       "  ▸ query_history_data     历史数据查询\r\n" +
                       "  ▸ get_database_status    数据库写入状态\r\n" +
                       "  ▸ repair_database        修复数据库连接\r\n" +
                       "  ▸ semantic_execute_query 数据源SQL直查\r\n" +
                       "\r\n" +
                       "── AI 设备管理 (读写) ────────────────────\r\n" +
                       "  ▸ add_device               添加设备\r\n" +
                       "  ▸ update_device            更新设备配置\r\n" +
                       "  ▸ add_variables            添加变量点\r\n" +
                       "  ▸ update_variables         更新变量点\r\n" +
                       "  ▸ reload_config            热重载配置(优雅8步流程)\r\n" +
                       "\r\n" +
                       "── 语义层 v2 (灵活层级树) ─────────────────\r\n" +
                       "  ▸ semantic_list_nodes           查询语义节点(按类型/父级/关键词/状态)\r\n" +
                       "  ▸ semantic_get_full_tree        完整语义树(递归,AI空间感知)\r\n" +
                       "  ▸ semantic_get_node_path        节点完整路径\r\n" +
                       "  ▸ semantic_get_realtime_snapshot 子树实时值快照(AI监控)\r\n" +
                       "  ▸ semantic_get_data_flow        数据链路追溯(设备→变量→表字段)\r\n" +
                       "  ▸ semantic_get_alarm_summary    报警聚合摘要(按变量/级别统计)\r\n" +
                       "  ▸ semantic_suggest_relations    智能推荐变量-字段映射(Jaro-Winkler)\r\n" +
                       "\r\n" +
                       "── 变量关系与事件 (读写) ──────────────────\r\n" +
                       "  ▸ semantic_list_variable_relations    变量关系查询\r\n" +
                       "  ▸ semantic_create_variable_relation   创建变量关系(17种类型)\r\n" +
                       "  ▸ semantic_list_node_relations        节点关系查询\r\n" +
                       "  ▸ semantic_list_events               节点事件(支持时间/类型/级别筛选)\r\n" +
                       "  ▸ semantic_create_event_config       创建事件配置(9种×12管线)\r\n" +
                       "  ▸ semantic_list_device_variables     设备变量+实时值\r\n" +
                       "\r\n" +
                       "── 批量操作与维护 ─────────────────────────\r\n" +
                       "  ▸ semantic_batch_update_nodes  AI批量维护标签(名称/描述/属性,单次100条)\r\n" +
                       "\r\n" +
                       "── 兼容工具(旧版) ─────────────────────────\r\n" +
                       "\r\n" +
                       "── 🔬 Fabric 声明式分析引擎 ───────────────\r\n" +
                       "  ▸ fabric_list_operators   列出可用分析算子\r\n" +
                       "  ▸ fabric_execute          执行分析(聚合/趋势/报警/日报)\r\n" +
                       "  ▸ semantic_list_workshops         车间列表\r\n" +
                       "  ▸ semantic_list_production_lines  产线列表\r\n" +
                       "  ▸ semantic_list_equipments        设备列表\r\n" +
                       "  ▸ semantic_list_tags              变量标签列表\r\n" +
                       "\r\n" +
                       "调用方式: POST JSON-RPC → {\"jsonrpc\":\"2.0\",\"method\":\"tools/call\",\"params\":{\"name\":\"工具名\",\"arguments\":{...}}}\r\n" +
                       "Token:   Header Authorization: Bearer <token> 或 URL ?token=<token>";
        }

        // ======================== i18n ========================

        public void ApplyLanguage()
        {
            var L = LanguageManager.Instance;
            Text = L.GetString("McpService_Title");
            lblTitle.Text = L.GetString("McpService_Title");
            lblPort.Text = L.GetString("McpService_Port") + ":";
            chkEnable.Text = L.GetString("McpService_EnableService");
            chkToken.Text = L.GetString("McpService_EnableToken");
            lblToken.Text = L.GetString("McpService_Token") + ":";
            btnGenToken.Text = L.GetString("McpService_GenerateToken");
            lblInfoTitle.Text = L.GetString("McpService_EndpointInfo");
            btnSave.Text = L.GetString("McpService_Save");
            btnCancel.Text = L.GetString("McpService_Cancel");
            UpdateStatusUI();
        }

        private string GenerateToken()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var rnd = new Random();
            var buf = new char[16];
            for (int i = 0; i < buf.Length; i++)
                buf[i] = chars[rnd.Next(chars.Length)];
            return string.Format("mcp-{0}{1}{2}{3}-{4}{5}{6}{7}-{8}{9}{10}{11}",
                buf[0], buf[1], buf[2], buf[3],
                buf[4], buf[5], buf[6], buf[7],
                buf[8], buf[9], buf[10], buf[11]);
        }
    }
}
