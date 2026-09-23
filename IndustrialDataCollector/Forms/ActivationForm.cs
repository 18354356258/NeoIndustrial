using System;
using System.Drawing;
using System.Windows.Forms;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// 软件激活窗口 — 首次运行时弹出，要求输入授权码激活
    /// </summary>
    public class ActivationForm : Form
    {
        private static readonly Color CDark1 = Color.FromArgb(15, 23, 42);
        private static readonly Color CDark2 = Color.FromArgb(30, 58, 95);
        private static readonly Color CAccent = Color.FromArgb(56, 145, 220);
        private static readonly Color CWhite = Color.White;
        private static readonly Color CText = Color.FromArgb(30, 41, 59);
        private static readonly Color CTextSub = Color.FromArgb(100, 116, 139);
        private static readonly Color CBg = Color.FromArgb(248, 250, 252);
        private static readonly Color CBorder = Color.FromArgb(226, 232, 240);
        private static readonly Color CSuccess = Color.FromArgb(0, 200, 100);
        private static readonly Color CError = Color.FromArgb(255, 80, 80);

        private Panel panelLeft;
        private Panel panelRight;
        private Label lblBrandTitle;
        private Label lblBrandSub;
        private Label lblTitle;
        private Label lblSubtitle;

        // 机器码区域
        private Label lblMachineIdLabel;
        private TextBox txtMachineId;
        private Button btnCopyMachineId;

        // 授权码输入
        private Label lblLicenseLabel;
        private TextBox txtLicenseCode;

        // 状态
        private Label lblStatusIcon;
        private Label lblStatus;

        // 按钮
        private Button btnActivate;
        private Button btnExit;

        private bool _isActivated = false;

        public bool ActivationSuccess => _isActivated;

        public ActivationForm()
        {
            var Lc = LanguageManager.Instance;

            // 窗口设置
            this.Text = Lc.GetString("Activation_Title");
            this.Size = new Size(740, 480);
            this.MinimumSize = new Size(740, 480);
            this.MaximumSize = new Size(740, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Icon = Program.AppIcon;
            this.BackColor = CWhite;
            this.Padding = new Padding(2);

            // 左侧品牌面板
            panelLeft = new Panel
            {
                Size = new Size(240, 480),
                Location = new Point(2, 2),
                BackColor = CDark1
            };
            this.Controls.Add(panelLeft);

            // 品牌标题
            lblBrandTitle = new Label
            {
                Text = "NeoIndustrial",
                Font = new Font("Microsoft YaHei UI", 22F, FontStyle.Bold),
                ForeColor = CAccent,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(50, 120)
            };
            panelLeft.Controls.Add(lblBrandTitle);

            lblBrandSub = new Label
            {
                Text = Lc.GetString("Activation_BrandSub"),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(50, 165)
            };
            panelLeft.Controls.Add(lblBrandSub);

            // 右侧内容面板
            panelRight = new Panel
            {
                Size = new Size(496, 476),
                Location = new Point(242, 2),
                BackColor = CWhite
            };
            this.Controls.Add(panelRight);

            // 标题
            lblTitle = new Label
            {
                Text = Lc.GetString("Activation_Heading"),
                Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold),
                ForeColor = CText,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(40, 30)
            };
            panelRight.Controls.Add(lblTitle);

            lblSubtitle = new Label
            {
                Text = Lc.GetString("Activation_Subtitle"),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
                ForeColor = CTextSub,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(42, 62)
            };
            panelRight.Controls.Add(lblSubtitle);

            // ── 机器码区域 ──
            int y = 95;
            lblMachineIdLabel = new Label
            {
                Text = Lc.GetString("Activation_MachineId"),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = CText,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(40, y)
            };
            panelRight.Controls.Add(lblMachineIdLabel);

            y += 25;
            var panelMachine = new Panel
            {
                Location = new Point(40, y),
                Size = new Size(420, 40),
                BackColor = CBg,
                BorderStyle = BorderStyle.None
            };
            // 手动绘制边框
            panelMachine.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, panelMachine.ClientRectangle,
                    CBorder, ButtonBorderStyle.Solid);
            };
            panelRight.Controls.Add(panelMachine);

            txtMachineId = new TextBox
            {
                Text = LicenseService.Instance.GetMachineId(),
                Font = new Font("Consolas", 9F, FontStyle.Regular),
                ForeColor = CText,
                BackColor = CBg,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Location = new Point(8, 12),
                Size = new Size(340, 18)
            };
            panelMachine.Controls.Add(txtMachineId);

            btnCopyMachineId = new Button
            {
                Text = Lc.GetString("Activation_Copy"),
                Font = new Font("Microsoft YaHei UI", 8F, FontStyle.Regular),
                ForeColor = CWhite,
                BackColor = CAccent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(55, 26),
                Location = new Point(358, 7),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnCopyMachineId.FlatAppearance.BorderSize = 0;
            btnCopyMachineId.Click += (s, e) =>
            {
                Clipboard.SetText(txtMachineId.Text);
                btnCopyMachineId.Text = "已复制";
                btnCopyMachineId.BackColor = CSuccess;
                var tmr = new System.Windows.Forms.Timer { Interval = 2000 };
                tmr.Tick += delegate
                {
                    btnCopyMachineId.Text = Lc.GetString("Activation_Copy");
                    btnCopyMachineId.BackColor = CAccent;
                    tmr.Stop();
                    tmr.Dispose();
                };
                tmr.Start();
            };
            panelMachine.Controls.Add(btnCopyMachineId);
            y += 55;

            // ── 授权码输入 ──
            lblLicenseLabel = new Label
            {
                Text = Lc.GetString("Activation_EnterLicense"),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                ForeColor = CText,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(40, y)
            };
            panelRight.Controls.Add(lblLicenseLabel);

            y += 25;
            txtLicenseCode = new TextBox
            {
                Font = new Font("Consolas", 9F, FontStyle.Regular),
                ForeColor = CText,
                BackColor = CWhite,
                Location = new Point(40, y),
                Size = new Size(420, 60),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtLicenseCode.LostFocus += (s, e) =>
            {
                if (string.IsNullOrEmpty(txtLicenseCode.Text))
                    txtLicenseCode.BackColor = CWhite;
            };
            panelRight.Controls.Add(txtLicenseCode);
            y += 75;

            // ── 状态 ──
            lblStatusIcon = new Label
            {
                Text = "",
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
                ForeColor = CTextSub,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(40, y)
            };
            panelRight.Controls.Add(lblStatusIcon);

            lblStatus = new Label
            {
                Text = Lc.GetString("Activation_Ready"),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
                ForeColor = CTextSub,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(58, y)
            };
            panelRight.Controls.Add(lblStatus);

            y += 45;

            // ── 按钮 ──
            btnActivate = new Button
            {
                Text = Lc.GetString("Activation_Activate"),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = CWhite,
                BackColor = CAccent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(160, 38),
                Location = new Point(40, y),
                Cursor = Cursors.Hand,
                TabStop = true
            };
            btnActivate.FlatAppearance.BorderSize = 0;
            btnActivate.Click += BtnActivate_Click;
            panelRight.Controls.Add(btnActivate);

            btnExit = new Button
            {
                Text = Lc.GetString("Activation_Exit"),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular),
                ForeColor = CTextSub,
                BackColor = CBg,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 38),
                Location = new Point(210, y),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.Click += (s, e) => { _isActivated = false; this.Close(); };
            panelRight.Controls.Add(btnExit);
        }

        private void BtnActivate_Click(object sender, EventArgs e)
        {
            string code = txtLicenseCode.Text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                ShowStatus(false, LanguageManager.Instance.GetString("Activation_EmptyCode"));
                return;
            }

            btnActivate.Enabled = false;
            btnActivate.Text = LanguageManager.Instance.GetString("Activation_Verifying");

            try
            {
                var result = LicenseService.Instance.ValidateLicense(code);
                if (result.IsValid)
                {
                    LicenseService.Instance.SaveLicense(code);
                    ShowStatus(true, LanguageManager.Instance.GetString("Activation_Success"));
                    _isActivated = true;

                    var tmr = new System.Windows.Forms.Timer { Interval = 1500 };
                    tmr.Tick += delegate
                    {
                        tmr.Stop();
                        tmr.Dispose();
                        this.Close();
                    };
                    tmr.Start();
                }
                else
                {
                    ShowStatus(false, result.ErrorMessage ?? LanguageManager.Instance.GetString("Activation_Failed"));
                    btnActivate.Enabled = true;
                    btnActivate.Text = LanguageManager.Instance.GetString("Activation_Activate");
                }
            }
            catch (Exception ex)
            {
                ShowStatus(false, LanguageManager.Instance.GetString("Activation_Error") + ": " + ex.Message);
                btnActivate.Enabled = true;
                btnActivate.Text = LanguageManager.Instance.GetString("Activation_Activate");
            }
        }

        private void ShowStatus(bool success, string message)
        {
            lblStatusIcon.Text = success ? "●" : "●";
            lblStatusIcon.ForeColor = success ? CSuccess : CError;
            lblStatus.Text = message;
            lblStatus.ForeColor = success ? CSuccess : CError;
        }
    }
}
