using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace IndustrialDataCollection.Utils
{
    public static class CredentialStore
    {
        private static string StoragePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndustrialDataCollection", "cred.dat");

        public static void Save(string username, string password)
        {
            var dir = Path.GetDirectoryName(StoragePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var plain = Encoding.UTF8.GetBytes(username + "\n" + password);
            var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(StoragePath, encrypted);
        }

        public static (string username, string password) Load()
        {
            if (!File.Exists(StoragePath)) return (null, null);
            try
            {
                var encrypted = File.ReadAllBytes(StoragePath);
                var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var text = Encoding.UTF8.GetString(plain);
                var idx = text.IndexOf('\n');
                if (idx < 0) return (null, null);
                return (text.Substring(0, idx), text.Substring(idx + 1));
            }
            catch { return (null, null); }
        }

        public static void Delete()
        {
            if (File.Exists(StoragePath)) File.Delete(StoragePath);
        }
    }
}
