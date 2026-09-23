using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// 登录窗口 - 宽屏 16:9，左侧品牌区 + 右侧登录区
    /// </summary>
    public partial class LoginForm : Form
    {
        // 配色
        private static readonly Color CDark1 = Color.FromArgb(15, 23, 42);
        private static readonly Color CDark2 = Color.FromArgb(30, 58, 95);
        private static readonly Color CAccent = Color.FromArgb(56, 145, 220);
        private static readonly Color CText = Color.FromArgb(30, 41, 59);
        private static readonly Color CTextSub = Color.FromArgb(100, 116, 139);
        private static readonly Color CInputBg = Color.FromArgb(248, 250, 252);
        private static readonly Color CWhite = Color.White;
        private static readonly Color CError = Color.FromArgb(220, 38, 38);
        private static readonly Color CWarn = Color.FromArgb(202, 138, 4);
        private static readonly Color CBtnHover = Color.FromArgb(37, 99, 235);
        private static readonly Color CCloseHover = Color.FromArgb(241, 245, 249);
        private static readonly Color CCloseFore = Color.FromArgb(148, 163, 184);

        public bool LoginSuccess { get; private set; } = false;

        // 代码创建的额外控件（不在 Designer 中）
        private Label _lblEye;
        private CheckBox _chkRemember;

        public LoginForm()
        {
            InitializeComponent();

            // 托盘图标
            InitTray();
            FormClosing += (s, e) =>
            {
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
            };

            // 代码创建小眼睛和记住密码（不依赖 Designer）
            _chkRemember = new CheckBox
            {
                Text = "记住密码",
                Font = new System.Drawing.Font("Microsoft YaHei UI", 9F),
                ForeColor = CTextSub,
                Location = new System.Drawing.Point(197, 268),
                Size = new System.Drawing.Size(87, 21),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            panelRight.Controls.Add(_chkRemember);
            // 为记住密码复选框留空间
            btnLogin.Top = 298;
            lblStatus.Top = 341;

            _lblEye = new Label
            {
                Text = "👁",
                Font = new System.Drawing.Font("Segoe UI", 11F),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new System.Drawing.Point(403, 231),
                Size = new System.Drawing.Size(30, 33),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };
            panelRight.Controls.Add(_lblEye);

            // 记住密码和按钮下移需要更多高度
            this.ClientSize = new Size(769, 420);
            foreach (var tb in new[] { txtUsername, txtPassword })
            {
                tb.Multiline = true;
                tb.AcceptsReturn = false;
                tb.AcceptsTab = false;
                tb.Height = 30;
            }

            // 让 panelLeft 支持子控件透明背景
            panelLeft.GetType().GetMethod("SetStyle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(panelLeft, new object[] {
                    System.Windows.Forms.ControlStyles.SupportsTransparentBackColor | System.Windows.Forms.ControlStyles.UserPaint | System.Windows.Forms.ControlStyles.AllPaintingInWmPaint,
                    true });

            WireEvents();
            SetMacAddress();
            LoadRememberedCredentials();
            LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLanguage();
            ApplyLanguage();
            this.AcceptButton = btnLogin;
        }

        private void WireEvents()
        {
            lblClose.Click += (s, e) => { LoginSuccess = false; Close(); };
            lblClose.MouseEnter += (s, e) => { lblClose.ForeColor = CText; lblClose.BackColor = CCloseHover; };
            lblClose.MouseLeave += (s, e) => { lblClose.ForeColor = CCloseFore; lblClose.BackColor = Color.Transparent; };

            // 登录
            btnLogin.Click += btnLogin_Click;

            // 密码回车
            txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) btnLogin_Click(s, e); };

            // 小眼睛切换密码可见性
            _lblEye.Cursor = Cursors.Hand;
            _lblEye.MouseEnter += (s, e) => { _lblEye.ForeColor = Color.FromArgb(30, 41, 59); };
            _lblEye.MouseLeave += (s, e) => { _lblEye.ForeColor = Color.FromArgb(148, 163, 184); };
            _lblEye.Click += (s, e) =>
            {
                if (txtPassword.PasswordChar == '\0')
                {
                    txtPassword.PasswordChar = '●';
                    _lblEye.Text = "👁";
                }
                else
                {
                    txtPassword.PasswordChar = '\0';
                    _lblEye.Text = "🙈";
                }
            };

            // 密码框有内容时才显示小眼睛
            txtPassword.TextChanged += (s, e) =>
            {
                _lblEye.Visible = txtPassword.Text.Length > 0;
            };
            _lblEye.Visible = false; // 初始隐藏

            // 按钮悬浮
            btnLogin.MouseEnter += (s, e) => { if (btnLogin.Enabled) btnLogin.BackColor = CBtnHover; };
            btnLogin.MouseLeave += (s, e) => { if (btnLogin.Enabled) btnLogin.BackColor = CAccent; };

            // 输入框聚焦效果
            txtUsername.Enter += (s, e) => { txtUsername.BackColor = CWhite; };
            txtUsername.Leave += (s, e) => { txtUsername.BackColor = CInputBg; };
            txtPassword.Enter += (s, e) => { txtPassword.BackColor = CWhite; };
            txtPassword.Leave += (s, e) => { txtPassword.BackColor = CInputBg; };

            // 左侧渐变背景
            panelLeft.Paint += PanelLeft_Paint;

            // 无边框拖拽
            this.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    NativeMethods.ReleaseCapture();
                    NativeMethods.SendMessage(this.Handle, 0xA1, 0x2, 0);
                }
            };
        }

        private void SetMacAddress()
        {
            lblMacValue.Text = AuthService.GetMacAddress();
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
            lblSlogan.Text = L.GetString("Login_Slogan");
            lblLoginTitle.Text = L.GetString("Login_Title_Text");
            lblLoginSub.Text = L.GetString("Login_Subtitle");
            btnLogin.Text = L.GetString("Login_BtnLogin");
            _chkRemember.Text = L.GetString("Login_RememberPwd");
        }

        private NotifyIcon _trayIcon;
        private void InitTray()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = this.Icon,
                Text = "Neo_工业网络数采平台",
                Visible = true
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("显示窗口", null, (s, e) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) =>
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                Application.Exit();
            });
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            };
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                lblStatus.ForeColor = CWarn;
                lblStatus.Text = "请输入用户名和密码";
                return;
            }

            btnLogin.Enabled = false;
            btnLogin.BackColor = CBtnHover;
            btnLogin.Text = "验证中...";
            lblStatus.ForeColor = CTextSub;
            lblStatus.Text = "正在验证...";

            var result = AuthService.Instance.Login(username, password);

            switch (result)
            {
                case LoginResult.Success:
                    SaveRememberedCredentials();
                    LoginSuccess = true;
                    this.DialogResult = DialogResult.OK;
                    Close();
                    break;
                case LoginResult.WrongPassword:
                    lblStatus.ForeColor = CError;
                    lblStatus.Text = "用户名或密码错误";
                    txtPassword.Focus();
                    txtPassword.SelectAll();
                    break;
                case LoginResult.UserDisabled:
                    lblStatus.ForeColor = CError;
                    lblStatus.Text = "账户已被禁用，请联系管理员";
                    break;
                case LoginResult.HardwareNotBound:
                    lblStatus.ForeColor = CError;
                    lblStatus.Text = "硬件未授权，请联系管理员绑定本机";
                    break;
                case LoginResult.HardwareBlocked:
                    lblStatus.ForeColor = CError;
                    lblStatus.Text = "硬件已被拒绝授权，请联系管理员";
                    break;
                default:
                    lblStatus.ForeColor = CError;
                    lblStatus.Text = "系统错误，请检查数据库";
                    break;
            }

            btnLogin.Enabled = true;
            btnLogin.BackColor = CAccent;
            btnLogin.Text = "登  录";
        }

        #region 记住密码

        private void LoadRememberedCredentials()
        {
            var (savedUser, savedPwd) = CredentialStore.Load();
            if (!string.IsNullOrEmpty(savedUser))
            {
                txtUsername.Text = savedUser;
                txtPassword.Text = savedPwd;
                _chkRemember.Checked = true;
            }
        }

        private void SaveRememberedCredentials()
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text;
            if (_chkRemember.Checked)
                CredentialStore.Save(username, password);
            else
                CredentialStore.Delete();
        }

        #endregion

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            txtUsername.Focus();
        }
    }

    /// <summary>
    /// 记住密码凭据存储 — Windows DPAPI 加密（绑定当前用户+本机）
    /// </summary>
    internal static class CredentialStore
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IndustrialDataCollection", "remember.dat");

        public static (string username, string password) Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return (null, null);
                byte[] encrypted = File.ReadAllBytes(FilePath);
                byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                string[] parts = Encoding.UTF8.GetString(decrypted).Split(new[] { '\n' }, 2);
                if (parts.Length == 2)
                    return (parts[0], parts[1]);
            }
            catch
            {
                try { File.Delete(FilePath); } catch { }
            }
            return (null, null);
        }

        public static void Save(string username, string password)
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string json = username + "\n" + password;
                byte[] plain = Encoding.UTF8.GetBytes(json);
                byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(FilePath, encrypted);
            }
            catch (Exception ex)
            {
                Utils.Logger.Error("保存记住密码失败: " + ex.Message);
            }
        }

        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        }
    }

    /// <summary>Win32 API - 无边框窗口拖拽</summary>
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
    }
}
