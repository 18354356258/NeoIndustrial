using System;
using System.Threading;
using System.Windows.Forms;
using IndustrialDataCollection.Forms;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection
{
    static class Program
    {
        /// <summary>
        /// 共享应用图标（所有窗体和对话框统一使用）
        /// </summary>
        public static System.Drawing.Icon AppIcon
        {
            get
            {
                if (_appIcon == null)
                {
                    try
                    {
                        var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tubiao.ico");
                        if (System.IO.File.Exists(path))
                            _appIcon = new System.Drawing.Icon(path);
                    }
                    catch { }
                    _appIcon = _appIcon ?? System.Drawing.SystemIcons.Application;
                }
                return _appIcon;
            }
        }
        private static System.Drawing.Icon _appIcon;

        /// <summary>
        /// 应用程序的主入口点
        /// </summary>
        private static Mutex _singleInstanceMutex;

        [STAThread]
        static void Main()
        {
            // 防止双开
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, @"Global\IndustrialDataCollection_SingleInstance_9A7B3F", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("工业数采平台已在运行中，请勿重复启动。\n\n如需重启，请先关闭当前实例。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.ThreadException += (s, e) =>
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "IndustrialDataCollection", "crash.log"),
                    "[ThreadException] " + e.Exception.ToString() + "\n\n");
                MessageBox.Show(e.Exception.Message, "系统错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "IndustrialDataCollection", "crash.log"),
                    "[UnhandledException] " + e.ExceptionObject.ToString() + "\n\n");
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 初始化日志
            Logger.Init();

            // 初始化配置服务（确保配置目录存在）
            ConfigService.Instance.Init();

            // 初始化认证服务（数据库 + 默认用户 + 硬件绑定）
            AuthService.Instance.Initialize();

            Logger.Info("===== 工业数采平台启动 =====");

            // 显示登录窗口
            using (var loginForm = new LoginForm())
            {
                if (loginForm.ShowDialog() != DialogResult.OK || !loginForm.LoginSuccess)
                {
                    Logger.Info("用户取消登录，程序退出");
                    return;
                }
            }

            Logger.Info("用户 " + AuthService.Instance.CurrentUser + " 已登录");

            // 检查软件授权
            if (!LicenseService.Instance.IsActivated())
            {
                Logger.Info("未检测到有效授权，弹出激活窗口");
                using (var activationForm = new ActivationForm())
                {
                    if (activationForm.ShowDialog() != DialogResult.OK || !activationForm.ActivationSuccess)
                    {
                        Logger.Info("用户取消激活，程序退出");
                        return;
                    }
                }
            }
            else
            {
                Logger.Info("授权验证通过");

            // 初始化语义层
            SemanticService.Instance.Init();

            // v2.0: Tag 统一身份迁移 — 为存量变量分配 VariableId + 创建 Tag
            try
            {
                var devices = ConfigService.Instance.LoadDevices();
                var migrationResult = TagMigrationService.Migrate(devices);
                Logger.Info(migrationResult.ToString());
                if (migrationResult.VariableIdsGenerated > 0 || migrationResult.TagsCreated > 0)
                {
                    // 有变更则回写 VariableId
                    ConfigService.Instance.SaveDevices(devices);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Tag 迁移失败: " + ex.Message);
            }
            }

            var dashboard = new DashboardForm();
            NavigationHelper.Dashboard = dashboard;
            try
            {
                Application.Run(dashboard);
            }
            catch (Exception ex)
            {
                Logger.Error($"[CRITICAL] Application crashed: {ex.Message}");
                Logger.Error(ex.ToString());
                MessageBox.Show(
                    $"应用程序发生严重错误，即将退出：\n{ex.Message}\n\n详细信息已写入日志文件。\n请检查 devices.json 是否损坏。",
                    "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }
            finally
            {
                try { _singleInstanceMutex?.ReleaseMutex(); _singleInstanceMutex?.Close(); } catch { }
            }

            Logger.Info("===== 工业数采平台退出 =====");
        }
    }
}
