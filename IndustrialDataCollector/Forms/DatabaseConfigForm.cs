using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using IndustrialDataCollection.Utils;
using IndustrialDataCollection.Services;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Forms
{
    public partial class DatabaseConfigForm : Form
    {
        // === 配色 ===
        private static readonly Color CAccent = Color.FromArgb(37, 99, 235);
        private static readonly Color CAccentHover = Color.FromArgb(29, 78, 216);
        private static readonly Color CText = Color.FromArgb(30, 41, 59);
        private static readonly Color CTextSub = Color.FromArgb(100, 116, 139);
        private static readonly Color CSuccess = Color.FromArgb(34, 197, 94);
        private static readonly Color CError = Color.FromArgb(220, 38, 38);
        private static readonly Color CBg = Color.FromArgb(248, 250, 252);
        private static readonly Color CBorder = Color.FromArgb(226, 232, 240);

        // 布局常量
        private const int FORM_W = 560;
        private const int FORM_H = 650;
        private const int PAD_H = 32;
        private const int CTRL_H = 28;
        private const int LABEL_W = 76;
        private const int GAP = 10;

        private ComboBox comboDbType;
        private Label lblServer, lblPort, lblDatabase, lblUser, lblPassword, lblFilePath;
        private TextBox txtServer, txtPort, txtDatabase, txtUser, txtPassword, txtFilePath;
        private Button btnBrowse, btnTest, btnSave, btnCancel;
        private Label lblStatus;
        private Panel panelCard;
        private TableLayoutPanel tableGrid;
        private Panel panelBottom;
        private CheckBox chkEnableWrite;
        private CheckBox chkFabricHistory;
        private CheckedListBox clbDevices;
        private Label lblDevices;

        private DatabaseWriteService.DbConfigRoot _root = new DatabaseWriteService.DbConfigRoot();
        private string _prevDbType = "";  // 记录切换前类型

        // 当前正在编辑的 DB 类型
        private string CurrentDbType => comboDbType.SelectedItem?.ToString() ?? "SQLite";

        // 卡片内可用宽度
        private int CardInnerW => panelCard.Width - 40;
        private int Col1W => LABEL_W;
        private int Col2W => CardInnerW - Col1W - 12;

        public DatabaseConfigForm()
        {
            InitializeForm();
            AfterLoad();
            LoadConfig();
            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLanguage();
            ApplyLanguage();
        }

        private void InitializeForm()
        {
            this.Text = "数据库配置";
            this.ClientSize = new Size(FORM_W, FORM_H);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Icon = Program.AppIcon;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Microsoft YaHei UI", 9F);
            this.BackColor = Color.White;

            int x0 = PAD_H;
            int y = 24;

            // ── 数据库类型标题 ──
            var lblDbType = new Label
            {
                Text = "数据库类型",
                Location = new Point(x0, y),
                AutoSize = true,
                ForeColor = CText,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold)
            };

            comboDbType = new ComboBox
            {
                Location = new Point(x0, y + 28),
                Size = new Size(FORM_W - PAD_H * 2, CTRL_H),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            comboDbType.Items.AddRange(new[] { "SQLite", "MySQL", "SQL Server", "PostgreSQL", "TDengine" });
            comboDbType.SelectedIndex = 0;
            comboDbType.SelectedIndexChanged += OnDbTypeChanged;

            y = comboDbType.Bottom + 16;

            // ── 卡片区域 ──
            panelCard = new Panel
            {
                Location = new Point(x0, y),
                Size = new Size(FORM_W - PAD_H * 2, 226),
                BackColor = CBg
            };
            panelCard.Paint += (s, e) =>
            {
                var r = new Rectangle(0, 0, panelCard.Width - 1, panelCard.Height - 1);
                e.Graphics.DrawRectangle(new Pen(CBorder), r);
            };

            BuildCardContent();

            // ── 底部栏 ──
            panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 72,
                BackColor = Color.White
            };

            lblStatus = new Label
            {
                Location = new Point(x0, 12),
                AutoSize = true,
                ForeColor = CTextSub,
                Font = new Font("Microsoft YaHei UI", 9F)
            };

            // 三个按钮右对齐，从右往左排
            int btnW = 84, btnH = 30, btnGap = 10;
            int btnBaseY = 24;
            // 右侧留白 = PAD_H
            int rightEdge = FORM_W - PAD_H;

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(rightEdge - btnW, btnBaseY),
                Size = new Size(btnW, btnH),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = CTextSub,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            btnCancel.Click += (s, e) => Close();

            btnTest = new Button
            {
                Text = "测试连接",
                Location = new Point(rightEdge - btnW * 2 - btnGap, btnBaseY),
                Size = new Size(btnW, btnH),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = CAccent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            btnTest.FlatAppearance.BorderColor = CAccent;
            btnTest.Click += BtnTest_Click;

            btnSave = new Button
            {
                Text = "保存配置",
                Location = new Point(rightEdge - btnW * 3 - btnGap * 2, btnBaseY),
                Size = new Size(btnW, btnH),
                FlatStyle = FlatStyle.Flat,
                BackColor = CAccent,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 9F)
            };
            btnSave.FlatAppearance.BorderSize = 1;
            btnSave.FlatAppearance.BorderColor = CAccent;
            btnSave.Click += BtnSave_Click;
            btnSave.MouseEnter += (s, e) => { btnSave.BackColor = CAccentHover; btnSave.FlatAppearance.BorderColor = CAccentHover; };
            btnSave.MouseLeave += (s, e) => { btnSave.BackColor = CAccent; btnSave.FlatAppearance.BorderColor = CAccent; };

            panelBottom.Controls.Add(lblStatus);
            panelBottom.Controls.Add(btnSave);
            panelBottom.Controls.Add(btnTest);
            panelBottom.Controls.Add(btnCancel);

            // ── 写入数据库区域 ──
            int writeY = panelCard.Bottom + 12;

            chkEnableWrite = new CheckBox
            {
                Text = "启用写入数据库",
                Location = new Point(x0, writeY),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = CText,
                Cursor = Cursors.Hand
            };
            chkEnableWrite.CheckedChanged += (s, e) =>
            {
                clbDevices.Enabled = chkEnableWrite.Checked;
            };

            lblDevices = new Label
            {
                Text = "选择设备（多选）:",
                Location = new Point(x0, writeY + 28),
                AutoSize = true,
                ForeColor = CTextSub,
                Font = new Font("Microsoft YaHei UI", 9F)
            };

            clbDevices = new CheckedListBox
            {
                Location = new Point(x0, writeY + 50),
                Size = new Size(FORM_W - PAD_H * 2, 120),
                CheckOnClick = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft YaHei UI", 9F),
                IntegralHeight = false,
                Enabled = false
            };

            // ── Fabric 历史分析复选框 ──
            chkFabricHistory = new CheckBox
            {
                Text = "Fabric 历史分析（允许 Fabric 引擎从此库查询历史数据）",
                Location = new Point(x0, clbDevices.Bottom + 8),
                AutoSize = true,
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = CText,
                Cursor = Cursors.Hand
            };

            // ── 组装 ──
            this.Controls.Add(lblDbType);
            this.Controls.Add(comboDbType);
            this.Controls.Add(panelCard);
            this.Controls.Add(chkEnableWrite);
            this.Controls.Add(lblDevices);
            this.Controls.Add(clbDevices);
            this.Controls.Add(chkFabricHistory);
            this.Controls.Add(panelBottom);

            AfterLoad();
        }

        private void BuildCardContent()
        {
            int pad = 20;
            int col1 = LABEL_W;
            int col2 = col1 + 12;
            int inputW = CardInnerW - col2;

            // TableLayoutPanel — 仅用于远程数据库的 5 行网格
            tableGrid = new TableLayoutPanel
            {
                Location = new Point(pad, pad),
                Size = new Size(CardInnerW, 186),
                ColumnCount = 2,
                RowCount = 5,
                BackColor = Color.Transparent
            };
            tableGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, col1));
            tableGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++)
                tableGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            // 服务器
            lblServer = MakeGridLabel("服务器");
            txtServer = MakeGridInput("localhost");
            tableGrid.Controls.Add(lblServer, 0, 0);
            tableGrid.Controls.Add(txtServer, 1, 0);

            // 端口
            lblPort = MakeGridLabel("端口");
            txtPort = new TextBox { Font = new Font("Microsoft YaHei UI", 9F), Height = CTRL_H, Width = 90, Text = "3306", Anchor = AnchorStyles.Left };
            tableGrid.Controls.Add(lblPort, 0, 1);
            tableGrid.Controls.Add(txtPort, 1, 1);

            // 数据库名
            lblDatabase = MakeGridLabel("数据库名");
            txtDatabase = MakeGridInput("industrial_data");
            tableGrid.Controls.Add(lblDatabase, 0, 2);
            tableGrid.Controls.Add(txtDatabase, 1, 2);

            // 用户名
            lblUser = MakeGridLabel("用户名");
            txtUser = MakeGridInput("root");
            tableGrid.Controls.Add(lblUser, 0, 3);
            tableGrid.Controls.Add(txtUser, 1, 3);

            // 密码
            lblPassword = MakeGridLabel("密码");
            txtPassword = new TextBox
            {
                Font = new Font("Microsoft YaHei UI", 9F),
                Height = CTRL_H,
                PasswordChar = '●',
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
            };
            tableGrid.Controls.Add(lblPassword, 0, 4);
            tableGrid.Controls.Add(txtPassword, 1, 4);

            // SQLite 文件路径（不在 grid 里，独立摆放）
            lblFilePath = new Label
            {
                Text = "文件路径",
                ForeColor = CText,
                Font = new Font("Microsoft YaHei UI", 9F),
                AutoSize = true,
                Visible = false
            };
            txtFilePath = new TextBox
            {
                Font = new Font("Microsoft YaHei UI", 9F),
                Height = CTRL_H,
                Visible = false
            };
            btnBrowse = new Button
            {
                Text = "浏览...",
                Size = new Size(58, CTRL_H),
                FlatStyle = FlatStyle.Flat,
                ForeColor = CAccent,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 8F),
                Visible = false
            };
            btnBrowse.FlatAppearance.BorderColor = CAccent;
            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new SaveFileDialog
                {
                    Filter = "SQLite 数据库|*.db|所有文件|*.*",
                    Title = "选择或创建数据库文件",
                    FileName = "data.db"
                })
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                        txtFilePath.Text = dlg.FileName;
                }
            };

            panelCard.Controls.Add(tableGrid);
            panelCard.Controls.Add(lblFilePath);
            panelCard.Controls.Add(txtFilePath);
            panelCard.Controls.Add(btnBrowse);
        }

        private Label MakeGridLabel(string text) => new Label
        {
            Text = text,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = CText,
            AutoSize = true,
            Anchor = AnchorStyles.Right
        };

        private TextBox MakeGridInput(string placeholder) => new TextBox
        {
            Font = new Font("Microsoft YaHei UI", 9F),
            Height = CTRL_H,
            Text = placeholder,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };

        private void AfterLoad()
        {
            UpdateParamVisibility();
        }

        private void OnDbTypeChanged(object sender, EventArgs e)
        {
            // 用 _prevDbType 保存旧类型的 UI 状态（事件触发时 comboBox 已切到新值）
            if (!string.IsNullOrEmpty(_prevDbType))
                SaveCurrentToType(_prevDbType);
            _prevDbType = CurrentDbType;
            RestoreCurrentFromConfig();
            UpdateParamVisibility();
        }

        /// <summary>
        /// 把当前 UI 保存到 _root.Configs[dbType]
        /// </summary>
        private void SaveCurrentToType(string dbType)
        {
            var cfg = GetOrCreateConfig(dbType);
            cfg.Server = txtServer.Text.Trim();
            cfg.Port = txtPort.Text.Trim();
            cfg.Database = txtDatabase.Text.Trim();
            cfg.User = txtUser.Text.Trim();
            cfg.Password = txtPassword.Text;
            cfg.FilePath = txtFilePath.Text.Trim();
            cfg.EnableWrite = chkEnableWrite.Checked;
            cfg.EnableFabricHistory = chkFabricHistory.Checked;
            cfg.SelectedDevices = new List<string>();
            foreach (var item in clbDevices.CheckedItems)
                cfg.SelectedDevices.Add(item.ToString());
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // DatabaseConfigForm
            // 
            this.ClientSize = new System.Drawing.Size(477, 389);
            this.Name = "DatabaseConfigForm";
            this.ResumeLayout(false);

        }

        private void SaveCurrentToConfig() => SaveCurrentToType(CurrentDbType);

        /// <summary>
        /// 把 _root.Configs[CurrentDbType] 恢复到 UI
        /// </summary>
        private void RestoreCurrentFromConfig()
        {
            var cfg = GetOrCreateConfig(CurrentDbType);

            // 恢复时不要触发事件
            txtServer.Text = string.IsNullOrEmpty(cfg.Server) ? "localhost" : cfg.Server;
            txtPort.Text = cfg.Port ?? "";
            txtDatabase.Text = string.IsNullOrEmpty(cfg.Database) ? "industrial_data" : cfg.Database;
            txtUser.Text = cfg.User ?? "";
            txtPassword.Text = cfg.Password ?? "";
            txtFilePath.Text = cfg.FilePath ?? "";

            if (string.IsNullOrEmpty(txtPort.Text))
                ApplyDefaultPort();

            chkEnableWrite.Checked = cfg.EnableWrite;
            chkFabricHistory.Checked = cfg.EnableFabricHistory;

            if (cfg.SelectedDevices != null && cfg.SelectedDevices.Count > 0)
            {
                for (int i = 0; i < clbDevices.Items.Count; i++)
                {
                    string item = clbDevices.Items[i].ToString();
                    clbDevices.SetItemChecked(i, cfg.SelectedDevices.Contains(item));
                }
            }
            else
            {
                for (int i = 0; i < clbDevices.Items.Count; i++)
                    clbDevices.SetItemChecked(i, false);
            }
        }

        private DatabaseWriteService.DbEntryConfig GetOrCreateConfig(string dbType)
        {
            if (!_root.Configs.ContainsKey(dbType))
                _root.Configs[dbType] = new DatabaseWriteService.DbEntryConfig { DbType = dbType };
            return _root.Configs[dbType];
        }

        private void ApplyDefaultPort()
        {
            switch (CurrentDbType)
            {
                case "MySQL": txtPort.Text = "3306"; break;
                case "SQL Server": txtPort.Text = "1433"; break;
                case "PostgreSQL": txtPort.Text = "5432"; break;
                case "TDengine": txtPort.Text = "6041"; break;
            }
        }

        private void UpdateParamVisibility()
        {
            string dbType = comboDbType.SelectedItem?.ToString() ?? "SQLite";
            bool isFile = dbType == "SQLite";

            // 网格内容
            tableGrid.Visible = !isFile;

            // 文件路径
            lblFilePath.Visible = txtFilePath.Visible = btnBrowse.Visible = isFile;

            if (isFile)
            {
                panelCard.Height = 120;
                int cy = (panelCard.Height - CTRL_H) / 2;
                // 居中：标签(右对齐到卡片中线偏左) + 输入框 + 按钮
                int midX = panelCard.Width / 2;
                int totalW = LABEL_W + 12 + 260 + 8 + 58; // label + gap + text + gap + button
                int startX = midX - totalW / 2;

                lblFilePath.Location = new Point(startX, cy);
                lblFilePath.Size = new Size(LABEL_W, CTRL_H);
                lblFilePath.TextAlign = ContentAlignment.MiddleRight;

                txtFilePath.Location = new Point(startX + LABEL_W + 12, cy);
                txtFilePath.Width = 260;

                btnBrowse.Location = new Point(txtFilePath.Right + 8, cy);
            }
            else
            {
                panelCard.Height = 226;
            }

            lblStatus.Text = "";
        }

        private async void BtnTest_Click(object sender, EventArgs e)
        {
            btnTest.Enabled = false;
            SetStatus(CStatus.Neutral, "● 正在测试连接...");

            // 主线程先保存当前 UI 到 config
            SaveCurrentToConfig();
            var cfg = GetOrCreateConfig(CurrentDbType);

            bool ok = false;
            string errMsg = null;

            await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    using (var conn = DatabaseWriteService.CreateConnection(cfg))
                    {
                        conn.Open();
                        ok = conn.State == ConnectionState.Open;
                    }
                }
                catch (Exception ex)
                {
                    errMsg = ex.Message;
                }
            });

            if (ok)
                SetStatus(CStatus.Success, "✓  连接成功 — 数据库可达");
            else
                SetStatus(CStatus.Error, $"✗  {errMsg ?? "连接失败"}");

            btnTest.Enabled = true;
        }

// (removed - moved to DatabaseWriteService)

        private enum CStatus { Neutral, Success, Error }

        private void SetStatus(CStatus status, string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetStatus(status, text)));
                return;
            }
            switch (status)
            {
                case CStatus.Success: lblStatus.ForeColor = CSuccess; break;
                case CStatus.Error: lblStatus.ForeColor = CError; break;
                default: lblStatus.ForeColor = CTextSub; break;
            }
            lblStatus.Text = text;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                // 先保存当前 UI 到 config
                SaveCurrentToConfig();

                string configPath = DatabaseWriteService.GetConfigPath();
                File.WriteAllText(configPath, JsonConvert.SerializeObject(_root, Formatting.Indented));

                // 通知写入服务重新加载
                DatabaseWriteService.Instance.ReloadConfig();

                lblStatus.ForeColor = CSuccess;
                lblStatus.Text = "✓  配置已保存";
            }
            catch (Exception ex)
            {
                lblStatus.ForeColor = CError;
                lblStatus.Text = $"✗  保存失败: {ex.Message}";
            }
        }

// (removed - moved to DatabaseWriteService)

// (removed - moved to DatabaseWriteService)

        private void LoadConfig()
        {
            LoadDeviceList();

            string configPath = DatabaseWriteService.GetConfigPath();
            if (!File.Exists(configPath)) return;

            try
            {
                // 先尝试新格式
                _root = JsonConvert.DeserializeObject<DatabaseWriteService.DbConfigRoot>(
                    File.ReadAllText(configPath));
                if (_root == null) _root = new DatabaseWriteService.DbConfigRoot();
            }
            catch
            {
                _root = new DatabaseWriteService.DbConfigRoot();
            }

            // 恢复当前类型的设置
            RestoreCurrentFromConfig();
        }

        private void LoadDeviceList()
        {
            clbDevices.Items.Clear();
            try
            {
                var devices = ConfigService.Instance.LoadDevices();
                if (devices != null)
                {
                    foreach (var d in devices)
                        clbDevices.Items.Add(d.Name);
                }
            }
            catch { }
        }

        private void ApplyLanguage()
        {
            var L = LanguageManager.Instance;
            this.Text = L.GetString("Menu_Tools_DbConfig");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                comboDbType?.Dispose();
                txtServer?.Dispose();
                txtPort?.Dispose();
                txtDatabase?.Dispose();
                txtUser?.Dispose();
                txtPassword?.Dispose();
                txtFilePath?.Dispose();
                btnBrowse?.Dispose();
                btnTest?.Dispose();
                btnSave?.Dispose();
                btnCancel?.Dispose();
                lblStatus?.Dispose();
            }
            base.Dispose(disposing);
        }

// (removed - moved to DatabaseWriteService)
    }
}
