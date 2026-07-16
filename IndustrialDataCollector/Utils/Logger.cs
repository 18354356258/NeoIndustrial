using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace IndustrialDataCollection.Utils
{
    public enum LogLevel
    {
        Debug = 0,  // 全部写入（含采集细节）
        Info = 1    // 仅系统日志（默认）
    }

    /// <summary>
    /// 日志工具类 - 按天滚动文件日志，自动清理
    /// </summary>
    public static class Logger
    {
        private static string _logDir;
        private static readonly object _lock = new object();
        private static LogLevel _level = LogLevel.Info;
        private const int KEEP_DAYS = 30;

        public static LogLevel Level
        {
            get { return _level; }
            set { _level = value; }
        }

        /// <summary>
        /// 初始化日志目录，清理过期文件
        /// </summary>
        public static void Init()
        {
            _logDir = Path.Combine(Application.StartupPath, "Logs");
            if (!Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);
            CleanOldLogs();
        }

        private static string GetLogFile()
        {
            return Path.Combine(_logDir, string.Format("log_{0:yyyyMMdd}.txt", DateTime.Now));
        }

        private static void Write(string level, string message)
        {
            if (string.IsNullOrEmpty(_logDir))
                Init();

            try
            {
                lock (_lock)
                {
                    string line = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}", DateTime.Now, level, message);
                    File.AppendAllText(GetLogFile(), line + Environment.NewLine);
                }
            }
            catch { }
        }

        /// <summary>
        /// 删除超过 KEEP_DAYS 天的日志文件
        /// </summary>
        public static void CleanOldLogs()
        {
            if (string.IsNullOrEmpty(_logDir) || !Directory.Exists(_logDir))
                return;
            try
            {
                var cutoff = DateTime.Now.AddDays(-KEEP_DAYS);
                foreach (var file in Directory.GetFiles(_logDir, "log_*.txt"))
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.Delete(file);
                        Debug("清理旧日志: " + Path.GetFileName(file));
                    }
                }
            }
            catch { }
        }

        public static void Debug(string message)
        {
            if (_level <= LogLevel.Debug)
                Write("DEBUG", message);
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message, Exception ex = null)
        {
            Write("ERROR", ex != null ? message + " | " + ex.ToString() : message);
        }
    }
}
