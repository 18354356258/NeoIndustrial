using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// 认证服务 - 用户登录 + 硬件绑定
    /// </summary>
    public class AuthService : IDisposable
    {
        private static readonly Lazy<AuthService> _instance =
            new Lazy<AuthService>(() => new AuthService());
        public static AuthService Instance
        {
            get { return _instance.Value; }
        }

        private SQLiteConnection _conn;
        private string _dbPath;
        private bool _disposed;

        private AuthService() { }

        /// <summary>当前登录的用户名（null 表示未登录）</summary>
        public string CurrentUser { get; private set; }

        public void Initialize()
        {
            _dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndustrialDataCollection", "auth.db");

            string dir = Path.GetDirectoryName(_dbPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string connStr = $"Data Source={_dbPath};Version=3;Pooling=True;";
            _conn = new SQLiteConnection(connStr);
            _conn.Open();

            CreateTables();
            EnsureDefaultAdmin();

            Logger.Info("AuthService 初始化完成: " + _dbPath);
        }

        private void CreateTables()
        {
            string sql = @"
                CREATE TABLE IF NOT EXISTS users (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    username    TEXT    NOT NULL UNIQUE,
                    password    TEXT    NOT NULL,
                    role        TEXT    NOT NULL DEFAULT 'user',
                    is_active   INTEGER NOT NULL DEFAULT 1,
                    created_at  TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
                    updated_at  TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );

                CREATE TABLE IF NOT EXISTS hardware_auth (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    mac_address TEXT    NOT NULL UNIQUE,
                    description TEXT    NOT NULL DEFAULT '',
                    authorized  INTEGER NOT NULL DEFAULT 1,
                    created_at  TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );
            ";
            using (var cmd = new SQLiteCommand(sql, _conn))
                cmd.ExecuteNonQuery();
        }

        /// <summary>确保默认 admin 用户存在</summary>
        private void EnsureDefaultAdmin()
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM users WHERE username='admin'", _conn))
            {
                long count = (long)cmd.ExecuteScalar();
                if (count == 0)
                {
                    string hash = HashPassword("admin");
                    using (var insert = new SQLiteCommand(
                        "INSERT INTO users (username, password, role) VALUES (@u, @p, @r)", _conn))
                    {
                        insert.Parameters.AddWithValue("@u", "admin");
                        insert.Parameters.AddWithValue("@p", hash);
                        insert.Parameters.AddWithValue("@r", "admin");
                        insert.ExecuteNonQuery();
                    }
                    Logger.Info("默认用户 admin 已创建");
                }
            }

            // 自动绑定当前机器 MAC（首次运行）
            string mac = GetMacAddress();
            if (!string.IsNullOrEmpty(mac))
            {
                using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM hardware_auth WHERE mac_address=@m", _conn))
                {
                    cmd.Parameters.AddWithValue("@m", mac);
                    long count = (long)cmd.ExecuteScalar();
                    if (count == 0)
                    {
                        using (var insert = new SQLiteCommand(
                            "INSERT INTO hardware_auth (mac_address, description, authorized) VALUES (@m, @d, 1)", _conn))
                        {
                            insert.Parameters.AddWithValue("@m", mac);
                            insert.Parameters.AddWithValue("@d", Environment.MachineName);
                            insert.ExecuteNonQuery();
                        }
                        Logger.Info("本机 MAC 已自动绑定: " + mac);
                    }
                }
            }
        }

        /// <summary>验证用户登录 + 硬件绑定</summary>
        public LoginResult Login(string username, string password)
        {
            if (_disposed) return LoginResult.DatabaseError;

            try
            {
                // 1. 验证用户名密码
                string hash = HashPassword(password);
                using (var cmd = new SQLiteCommand(
                    "SELECT id, role, is_active FROM users WHERE username=@u AND password=@p", _conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@p", hash);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return LoginResult.WrongPassword;

                        int active = reader.GetInt32(2);
                        if (active != 1)
                            return LoginResult.UserDisabled;
                    }
                }

                // 2. 验证硬件绑定
                string mac = GetMacAddress();
                if (!string.IsNullOrEmpty(mac))
                {
                    using (var cmd = new SQLiteCommand(
                        "SELECT authorized FROM hardware_auth WHERE mac_address=@m", _conn))
                    {
                        cmd.Parameters.AddWithValue("@m", mac);
                        var result = cmd.ExecuteScalar();
                        if (result == null)
                            return LoginResult.HardwareNotBound;

                        if (Convert.ToInt32(result) != 1)
                            return LoginResult.HardwareBlocked;
                    }
                }

                CurrentUser = username;
                Logger.Info("用户登录成功: " + username);
                return LoginResult.Success;
            }
            catch (Exception ex)
            {
                Logger.Error("登录验证失败: " + ex.Message);
                return LoginResult.DatabaseError;
            }
        }

        /// <summary>修改密码</summary>
        public bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
                return false;

            try
            {
                string oldHash = HashPassword(oldPassword);
                string newHash = HashPassword(newPassword);

                using (var cmd = new SQLiteCommand(
                    "UPDATE users SET password=@np, updated_at=datetime('now','localtime') WHERE username=@u AND password=@op", _conn))
                {
                    cmd.Parameters.AddWithValue("@u", username);
                    cmd.Parameters.AddWithValue("@op", oldHash);
                    cmd.Parameters.AddWithValue("@np", newHash);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("修改密码失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>获取所有已授权的 MAC 地址</summary>
        public DataTable GetAuthorizedHardware()
        {
            var dt = new DataTable();
            using (var cmd = new SQLiteCommand(
                "SELECT mac_address, description, authorized, created_at FROM hardware_auth ORDER BY created_at", _conn))
            using (var adapter = new SQLiteDataAdapter(cmd))
                adapter.Fill(dt);
            return dt;
        }

        /// <summary>添加 MAC 地址授权</summary>
        public bool AddHardwareAuth(string mac, string description)
        {
            try
            {
                // 先检查是否存在
                using (var check = new SQLiteCommand("SELECT COUNT(*) FROM hardware_auth WHERE mac_address=@m", _conn))
                {
                    check.Parameters.AddWithValue("@m", mac);
                    if ((long)check.ExecuteScalar() > 0)
                    {
                        // 更新授权状态
                        using (var update = new SQLiteCommand(
                            "UPDATE hardware_auth SET authorized=1, description=@d WHERE mac_address=@m", _conn))
                        {
                            update.Parameters.AddWithValue("@m", mac);
                            update.Parameters.AddWithValue("@d", description);
                            return update.ExecuteNonQuery() > 0;
                        }
                    }
                }

                // 新增
                using (var insert = new SQLiteCommand(
                    "INSERT INTO hardware_auth (mac_address, description, authorized) VALUES (@m, @d, 1)", _conn))
                {
                    insert.Parameters.AddWithValue("@m", mac);
                    insert.Parameters.AddWithValue("@d", description);
                    return insert.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("添加硬件授权失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>获取本机 MAC 地址</summary>
        public static string GetMacAddress()
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                             && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                             && n.GetPhysicalAddress().GetAddressBytes().Length == 6
                             && !n.Description.ToLowerInvariant().Contains("virtual")
                             && !n.Description.ToLowerInvariant().Contains("vmware")
                             && !n.Description.ToLowerInvariant().Contains("hyper-v")
                             && !n.Description.ToLowerInvariant().Contains("docker"));

                var nic = nics.FirstOrDefault();
                if (nic != null)
                    return nic.GetPhysicalAddress().ToString(); // 格式: "001122334455"

                // fallback: 返回第一个物理地址
                var fallback = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.GetPhysicalAddress().GetAddressBytes().Length == 6)
                    .FirstOrDefault();
                return fallback?.GetPhysicalAddress().ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>SHA256 + salt 哈希密码</summary>
        /// <remarks>
        /// 安全提示：SHA256 不抗暴力破解，生产环境应迁移到 bcrypt/argon2。
        /// 盐值优先级：环境变量 IND_SALT → 默认硬编码值（仅开发兼容）。
        /// </remarks>
        private string HashPassword(string password)
        {
            // 优先读取环境变量，未设置时回退硬编码盐值
            string salt = Environment.GetEnvironmentVariable("IND_SALT")
                ?? "NeoIndDC_2026_SALT";
            byte[] bytes = SHA256.Create().ComputeHash(
                Encoding.UTF8.GetBytes(salt + password));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        public void Logout()
        {
            CurrentUser = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _conn?.Close();
            _conn?.Dispose();
        }
    }

    /// <summary>登录结果枚举</summary>
    public enum LoginResult
    {
        Success,
        WrongPassword,
        UserDisabled,
        HardwareNotBound,
        HardwareBlocked,
        DatabaseError
    }
}
