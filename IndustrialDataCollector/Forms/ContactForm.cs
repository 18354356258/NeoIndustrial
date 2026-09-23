using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// 联系我 — 获取 WEB 商业版 / 申请正式授权 / 查看两个开源仓库（GitHub & Gitee）
    /// 与登录页、关于页统一的品牌与视觉风格
    /// </summary>
    public class ContactForm : Form
    {
        // === 对外口径（两个开源仓库 + 联系方式） ===
        public const string RepoGithub = "https://github.com/18354356258/NeoIndustrial";
        public const string RepoGitee = "https://gitee.com/JEDI_MASTER/neoIndustrial";
        public const string ContactEmail = "751326339@qq.com";
        public const string ContactPhone = "18354356258 / 18854344113";

        // === 配色（与 LoginForm / AboutForm 保持一致） ===
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
        private static readonly Color COk = Color.FromArgb(22, 163, 74);

        // === 控件 ===
        private Panel panelLeft;
        private Panel panelRight;
        private Panel panelDecor;
        private Panel sepLine;
        private Label lblBrandSub;
        private Label lblSlogan;
        private Label lblVersion;
        private Label lblCopyright;
        private Label lblClose;
        private Label lblTitle;
        private Label lblDesc;
        private Panel cardRepos;
        private Label lblReposTitle;
        private LinkLabel lnkGithub;
        private LinkLabel lnkGitee;
        private Panel cardComm;
        private Label lblCommTitle;
        private LinkLabel lnkEmail;
        private Label lblPhone;
        private Label lblCopyTip;
        private Button btnCopyMail;
        private Button btnOk;

        public ContactForm()
        {
            InitializeForm();
            ApplyLanguage();
        }

        private void InitializeForm()
        {
            this.Text = S("Contact_Title", "联系我");
            this.Size = new Size(720, 420);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = CWhite;
            this.Padding = new Padding(1);
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(CBorder, 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            };

            this.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    AboutForm.NativeMethods.ReleaseCapture();
                    AboutForm.NativeMethods.SendMessage(this.Handle, 0xA1, 0x2, 0);
                }
            };

            // === 左侧品牌面板 ===
            panelLeft = new Panel
            {
                Location = new Point(1, 1),
                Size = new Size(300, 418),
                BackColor = Color.Transparent
            };
            panelLeft.GetType().GetMethod("SetStyle",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(panelLeft, new object[] {
                    ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint,
                    true });
            panelLeft.Paint += PanelLeft_Paint;

            lblBrandSub = new Label
            {
                Text = "企业版 · ENTERPRISE",
                Font = new Font("Microsoft YaHei UI", 11F),
                ForeColor = CTextMuted,
                BackColor = Color.Transparent,
                Location = new Point(60, 125),
                Size = new Size(215, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelDecor = new Panel
            {
                BackColor = CAccent,
                Location = new Point(36, 62),
                Size = new Size(13, 24)
            };

            sepLine = new Panel
            {
                BackColor = Color.FromArgb(51, 65, 85),
                Location = new Point(40, 150),
                Size = new Size(40, 1)
            };

            lblSlogan = new Label
            {
                Text = "工业网络数采平台",
                Font = new Font("Microsoft YaHei UI", 12F),
                ForeColor = CTextMuted,
                BackColor = Color.Transparent,
                Location = new Point(40, 170),
                Size = new Size(215, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblVersion = new Label
            {
                Text = "v" + AppVersion(),
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(71, 85, 105),
                BackColor = Color.Transparent,
                Location = new Point(40, 330),
                Size = new Size(220, 18),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblCopyright = new Label
            {
                Text = "© 2026 张成龙",
                Font = new Font("Microsoft YaHei UI", 8F),
                ForeColor = Color.FromArgb(71, 85, 105),
                BackColor = Color.Transparent,
                Location = new Point(40, 350),
                Size = new Size(220, 16),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // === 右侧内容面板 ===
            panelRight = new Panel
            {
                Location = new Point(301, 1),
                Size = new Size(418, 418),
                BackColor = CWhite
            };

            lblClose = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 11F),
                ForeColor = CCloseFore,
                BackColor = Color.Transparent,
                Location = new Point(383, 6),
                Size = new Size(28, 26),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            lblClose.Click += (s, e) => Close();
            lblClose.MouseEnter += (s, e) => { lblClose.ForeColor = CText; lblClose.BackColor = CCloseHover; };
            lblClose.MouseLeave += (s, e) => { lblClose.ForeColor = CCloseFore; lblClose.BackColor = Color.Transparent; };

            lblTitle = new Label
            {
                Text = "联系我",
                Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold),
                ForeColor = CText,
                Location = new Point(30, 28),
                Size = new Size(380, 36),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblDesc = new Label
            {
                Text = "",
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = CTextSub,
                Location = new Point(30, 70),
                Size = new Size(378, 38),
                TextAlign = ContentAlignment.TopLeft
            };

            // === 卡片一：开源仓库 ===
            cardRepos = new Panel
            {
                Location = new Point(30, 116),
                Size = new Size(378, 112),
                BackColor = CBg
            };

            lblReposTitle = new Label
            {
                Text = "开源仓库（点击直达）",
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = CText,
                BackColor = Color.Transparent,
                Location = new Point(16, 12),
                Size = new Size(320, 22)
            };

            lnkGithub = MakeLink("GitHub：" + RepoGithub.Replace("https://", ""), new Point(16, 44));
            lnkGithub.LinkClicked += (s, e) => Open(RepoGithub);

            lnkGitee = MakeLink("Gitee：" + RepoGitee.Replace("https://", ""), new Point(16, 72));
            lnkGitee.LinkClicked += (s, e) => Open(RepoGitee);

            cardRepos.Controls.Add(lblReposTitle);
            cardRepos.Controls.Add(lnkGithub);
            cardRepos.Controls.Add(lnkGitee);

            // === 卡片二：WEB 商业版授权 ===
            cardComm = new Panel
            {
                Location = new Point(30, 238),
                Size = new Size(378, 96),
                BackColor = CBg
            };

            lblCommTitle = new Label
            {
                Text = "WEB 商业版 · 按机台数量授权",
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = CText,
                BackColor = Color.Transparent,
                Location = new Point(16, 12),
                Size = new Size(340, 22)
            };

            lnkEmail = MakeLink("邮箱：" + ContactEmail, new Point(16, 42));
            lnkEmail.LinkClicked += (s, e) => Open("mailto:" + ContactEmail);

            lblPhone = new Label
            {
                Text = "电话 / 微信：18354356258 / 18854344113（张成龙）",
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = CTextSub,
                BackColor = Color.Transparent,
                Location = new Point(16, 68),
                Size = new Size(346, 20)
            };

            cardComm.Controls.Add(lblCommTitle);
            cardComm.Controls.Add(lnkEmail);
            cardComm.Controls.Add(lblPhone);

            // === 提示 + 按钮 ===
            lblCopyTip = new Label
            {
                Text = "",
                Font = new Font("Microsoft YaHei UI", 9F),
                ForeColor = COk,
                BackColor = Color.Transparent,
                Location = new Point(30, 344),
                Size = new Size(150, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            btnCopyMail = new Button
            {
                Text = "复制邮箱地址",
                Font = new Font("Microsoft YaHei UI", 9.5F),
                ForeColor = CText,
                BackColor = CBg,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(148, 34),
                Location = new Point(176, 342),
                Cursor = Cursors.Hand
            };
            btnCopyMail.FlatAppearance.BorderColor = CBorder;
            btnCopyMail.FlatAppearance.BorderSize = 1;
            btnCopyMail.Click += (s, e) => CopyEmail();
            btnCopyMail.MouseEnter += (s, e) => btnCopyMail.BackColor = CCloseHover;
            btnCopyMail.MouseLeave += (s, e) => btnCopyMail.BackColor = CBg;

            btnOk = new Button
            {
                Text = "关 闭",
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = CWhite,
                BackColor = CAccent,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(74, 34),
                Location = new Point(334, 342),
                Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) => Close();
            btnOk.MouseEnter += (s, e) => btnOk.BackColor = CAccentDim;
            btnOk.MouseLeave += (s, e) => btnOk.BackColor = CAccent;

            // === 组装 ===
            panelLeft.Controls.Add(lblBrandSub);
            panelLeft.Controls.Add(panelDecor);
            panelLeft.Controls.Add(sepLine);
            panelLeft.Controls.Add(lblSlogan);
            panelLeft.Controls.Add(lblVersion);
            panelLeft.Controls.Add(lblCopyright);

            panelRight.Controls.Add(lblClose);
            panelRight.Controls.Add(lblTitle);
            panelRight.Controls.Add(lblDesc);
            panelRight.Controls.Add(cardRepos);
            panelRight.Controls.Add(cardComm);
            panelRight.Controls.Add(lblCopyTip);
            panelRight.Controls.Add(btnCopyMail);
            panelRight.Controls.Add(btnOk);

            this.Controls.Add(panelRight);
            this.Controls.Add(panelLeft);
        }

        private LinkLabel MakeLink(string text, Point location)
        {
            var link = new LinkLabel
            {
                Text = text,
                Font = new Font("Microsoft YaHei UI", 9F),
                LinkColor = CAccent,
                ActiveLinkColor = CAccentDim,
                VisitedLinkColor = CAccent,
                LinkBehavior = LinkBehavior.HoverUnderline,
                AutoSize = true,
                Location = location
            };
            return link;
        }

        private void CopyEmail()
        {
            try
            {
                Clipboard.SetText(ContactEmail);
                lblCopyTip.Text = S("Contact_Copied", "邮箱地址已复制到剪贴板");
            }
            catch (Exception ex)
            {
                lblCopyTip.ForeColor = Color.FromArgb(220, 38, 38);
                lblCopyTip.Text = ex.Message;
            }
        }

        private void PanelLeft_Paint(object sender, PaintEventArgs e)
        {
            using (var brush = new LinearGradientBrush(
                panelLeft.ClientRectangle, CDark1, CDark2, 90F))
            {
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                e.Graphics.FillRectangle(brush, panelLeft.ClientRectangle);

                // 品牌名直接用 GDI+ 绘制（避免 Label 宽度不足裁掉末位字母）
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using (var bf = new Font("Segoe UI", 28F, FontStyle.Bold))
                {
                    e.Graphics.DrawString("NeoIndustrial", bf, Brushes.White, 40F, 51F);
                }
            }
        }

        private void ApplyLanguage()
        {
            this.Text = S("Contact_Title", "联系我");
            lblTitle.Text = S("Contact_Title", "联系我");
            lblDesc.Text = S("Contact_Subtitle", "开源仓库（Apache-2.0）与 WEB 商业版授权，都在这里");
            lblReposTitle.Text = S("Contact_ReposTitle", "开源仓库（点击直达）");
            lnkGithub.Text = S("Contact_RepoGithub", "GitHub：github.com/18354356258/NeoIndustrial");
            lnkGitee.Text = S("Contact_RepoGitee", "Gitee：gitee.com/JEDI_MASTER/neoIndustrial");
            lblCommTitle.Text = S("Contact_CommercialTitle", "WEB 商业版 · 按机台数量授权");
            lnkEmail.Text = S("Contact_Email", "邮箱：751326339@qq.com");
            lblPhone.Text = S("Contact_Phone", "电话 / 微信：18354356258 / 18854344113（张成龙）");
            btnCopyMail.Text = S("Contact_CopyEmail", "复制邮箱地址");
            btnOk.Text = S("Contact_Close", "关 闭");
        }

        /// <summary>打开链接（默认浏览器 / 邮件客户端）</summary>
        private static void Open(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开链接：" + url + "\r\n" + ex.Message,
                    "联系我", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>取语言包文本，缺失时用兜底中文</summary>
        private static string S(string key, string fallback)
        {
            try
            {
                var v = LanguageManager.Instance.GetString(key);
                return string.IsNullOrEmpty(v) || v == key ? fallback : v;
            }
            catch { return fallback; }
        }

        /// <summary>版本号取自程序集版本，避免硬编码过期</summary>
        private static string AppVersion()
        {
            try
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return v == null ? "1.0.0" : v.ToString(3);
            }
            catch { return "1.0.0"; }
        }
    }
}
