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
        /// 应用图标（共享）
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

        [STAThread]
        static void Main()
        {
            // 防双开
            bool createdNew;
            using (var mutex = new Mutex(true, @"Global\IndustrialDataCollector_Community", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("工业数采平台（社区版）已在运行中。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.ThreadException += (s, e) =>
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "IndustrialDataCollector-CE", "crash.log"),
                        "[ThreadException] " + e.Exception.ToString() + "\n\n");
                    MessageBox.Show(e.Exception.Message, "系统错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "IndustrialDataCollector-CE", "crash.log"),
                        "[UnhandledException] " + e.ExceptionObject.ToString() + "\n\n");
                };

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 初始化日志
                Logger.Init();

                // 初始化配置服务
                ConfigService.Instance.Init();

                Logger.Info("===== 工业数采平台（社区版）启动 =====");

                // 社区版：无需登录/激活，直接进入主界面
                Application.Run(new MainForm());
            }
        }
    }
}
