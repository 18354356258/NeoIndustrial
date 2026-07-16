using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// 关于窗口 — 高端品牌展示，与登录页统一风格
    /// </summary>
    public class AboutForm : Form
    {
        // === 配色（与 LoginForm 保持一致） ===
        private static readonly Color CDark1 = Color.FromArgb(15, 23, 42);
        private static readonly Color CDark2 = Color.FromArgb(30, 58, 95);
        private static readonly Color CAccent = Color.FromArgb(56, 145, 220);
        private static readonly Color CAccentDim = Color.FromArgb(37, 99, 235);
        private static readonly Color CText = Color.FromArgb(30, 41, 59);
        private static readonly Color CTextSub = Color.FromArgb(100, 116, 139);
        private static readonly Color CTextMuted = Color.FromArgb(148, 163, 184);
        private static readonly Color CWhite = Color.White;
        private static readonly Color CBg = Color.FromArgb(248, 250, 252);
        private static readonly Color CBorder = Color.FromArgb(226, 232, 240);
        private static readonly Color CCloseHover = Color.FromArgb(241, 245, 249);
        private static readonly Color CCloseFore = Color.FromArgb(148, 163, 184);

        // === 控件 ===
        private Panel panelLeft;
        private Panel panelRight;
        private Panel panelDecor;
        private Panel sepLine;
        private Label lblBrand;
        private Label lblBrandSub;
        private Label lblSlogan;
        private Label lblVersion;
        private Label lblCopyright;
        private Label lblClose;
        private Label lblTitle;
        private Label lblDesc;
        private Panel panelDrivers;
        private FlowLayoutPanel flowDrivers;
        private Button btnOk;

        public AboutForm()
        {
            InitializeForm();
            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLanguage();
            ApplyLanguage();
        }

        private void InitializeForm()
        {
            // === 窗体 ===
            this.Text = "关于";
            this.Size = new Size(720, 420);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = CWhite;
            this.Padding = new Padding(1);
            this.DoubleBuffered = true;

            // 绘制外边框
            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(CBorder, 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            };

            // 无边框拖拽
            this.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(this.Handle, 0xA1, 0x2, 0);
                }
            };

            // === 左侧品牌面板 ===
            panelLeft = new Panel
            {
                Location = new Point(1, 1),
                Size = new Size(280, 418),
                BackColor = Color.Transparent
            };
            // 支持透明子控件
            panelLeft.GetType().GetMethod("SetStyle",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(panelLeft, new object[] {
                    ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint,
                    true });
            panelLeft.Paint += PanelLeft_Paint;

            // 品牌大标题 "NEO"
            lblBrand = new Label
            {
                Text = "MatriX",
                Font = new Font("Segoe UI", 46F, FontStyle.Bold),
                ForeColor = CWhite,
                BackColor = Color.Transparent,
                Location = new Point(40, 48),
                Size = new Size(220, 76),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 品牌副标题
            lblBrandSub = new Label
            {
                Text = "INDUSTRIAL",
                Font = new Font("Segoe UI", 11F, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                BackColor = Color.Transparent,
                Location = new Point(60, 125),
                Size = new Size(200, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 装饰竖线
            panelDecor = new Panel
            {
                BackColor = CAccent,
                Location = new Point(36, 62),
                Size = new Size(13, 24)
            };

            // 分割线
            sepLine = new Panel
            {
                BackColor = Color.FromArgb(51, 65, 85),
                Location = new Point(40, 150),
                Size = new Size(40, 1)
            };

            // 标语
            lblSlogan = new Label
            {
                Text = "工业网络数采平台",
                Font = new Font("Microsoft YaHei UI", 12F),
                ForeColor = CTextMuted,
                BackColor = Color.Transparent,
                Location = new Point(40, 170),
                Size = new Size(210, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 版本
            lblVersion = new Label
            {
                Text = "v1.2.0",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105),
                BackColor = Color.Transparent,
                Location = new Point(40, 348),
                Size = new Size(100, 18),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 版权
            lblCopyright = new Label
            {
                Text = "© 2026 张成龙",
                Font = new Font("Microsoft YaHei UI", 8F),
                ForeColor = Color.FromArgb(71, 85, 105),
                BackColor = Color.Transparent,
                Location = new Point(85, 350),
                Size = new Size(160, 16),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // === 右侧内容面板 ===
            panelRight = new Panel
            {
                Location = new Point(281, 1),
                Size = new Size(438, 418),
                BackColor = CWhite
            };

            // 标题
            lblTitle = new Label
            {
                Text = "工业数据采集大师",
                Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold),
                ForeColor = CText,
                Location = new Point(40, 40),
                Size = new Size(370, 36),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 描述
            lblDesc = new Label
            {
                Text = "插件式工业数据采集平台，7 大协议驱动\n助力工业物联网数据上云",
                Font = new Font("Microsoft YaHei UI", 10F),
                ForeColor = CTextSub,
                Location = new Point(40, 84),
                Size = new Size(370, 42),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // === 驱动能力卡片 ===
            panelDrivers = new Panel
            {
                Location = new Point(40, 148),
                Size = new Size(370, 170),
                BackColor = CBg
            };

            var lblDriversTitle = new Label
            {
                Text = "支持协议",
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = CText,
                Location = new Point(16, 12),
                Size = new Size(100, 22)
            };

            flowDrivers = new FlowLayoutPanel
            {
                Location = new Point(16, 42),
                Size = new Size(342, 118),
                BackColor = Color.Transparent
            };

            string[] drivers = { "Modbus TCP", "Modbus RTU", "Siemens S7", "OPC UA", "MQTT", "HTTP REST", "模拟器" };
            foreach (var d in drivers)
            {
                var tag = new Label
                {
                    Text = d,
                    Font = new Font("Microsoft YaHei UI", 9F),
                    ForeColor = CAccent,
                    BackColor = Color.FromArgb(219, 234, 254),
                    Size = new Size(d.Length > 6 ? 92 : (d.Length > 4 ? 80 : 68), 26),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = new Padding(0, 0, 8, 8)
                };
                flowDrivers.Controls.Add(tag);
            }

            panelDrivers.Controls.Add(lblDriversTitle);
            panelDrivers.Controls.Add(flowDrivers);

            // === 确定按钮 ===
            btnOk = new Button
            {
                Text = "确  定",
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = CWhite,
                BackColor = CAccent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(120, 36),
                Location = new Point(290, 342),
                Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) => Close();
            btnOk.MouseEnter += (s, e) => btnOk.BackColor = CAccentDim;
            btnOk.MouseLeave += (s, e) => btnOk.BackColor = CAccent;

            // === 关闭按钮 ===
            lblClose = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 11F),
                ForeColor = CCloseFore,
                BackColor = Color.Transparent,
                Location = new Point(403, 6),
                Size = new Size(28, 26),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            lblClose.Click += (s, e) => Close();
            lblClose.MouseEnter += (s, e) => { lblClose.ForeColor = CText; lblClose.BackColor = CCloseHover; };
            lblClose.MouseLeave += (s, e) => { lblClose.ForeColor = CCloseFore; lblClose.BackColor = Color.Transparent; };

            // === 组装 ===
            panelLeft.Controls.Add(lblBrand);
            panelLeft.Controls.Add(lblBrandSub);
            panelLeft.Controls.Add(panelDecor);
            panelLeft.Controls.Add(sepLine);
            panelLeft.Controls.Add(lblSlogan);
            panelLeft.Controls.Add(lblVersion);
            panelLeft.Controls.Add(lblCopyright);
            panelRight.Controls.Add(lblClose);
            panelRight.Controls.Add(lblTitle);
            panelRight.Controls.Add(lblDesc);
            panelRight.Controls.Add(panelDrivers);
            panelRight.Controls.Add(btnOk);
            this.Controls.Add(panelRight);
            this.Controls.Add(panelLeft);
        }

        private void PanelLeft_Paint(object sender, PaintEventArgs e)
        {
            using (var brush = new LinearGradientBrush(
                panelLeft.ClientRectangle, CDark1, CDark2, 90F))
            {
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                e.Graphics.FillRectangle(brush, panelLeft.ClientRectangle);
            }
        }

        private void ApplyLanguage()
        {
            var L = LanguageManager.Instance;
            this.Text = L.GetString("About_Title");
            lblSlogan.Text = L.GetString("Login_Slogan");
            lblTitle.Text = "工业数据采集大师";
            lblDesc.Text = "插件式工业数据采集平台，7 大协议驱动\n助力工业物联网数据上云";
            btnOk.Text = L.GetString("PointEdit_Ok");
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AboutForm));
            this.SuspendLayout();
            // 
            // AboutForm
            // 
            this.ClientSize = new System.Drawing.Size(820, 439);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "AboutForm";
            this.ResumeLayout(false);

        }

        // Win32 API - 无边框窗口拖拽
        internal static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ReleaseCapture();
        }
    }
}
