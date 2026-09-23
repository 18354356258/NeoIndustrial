using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Services
{
    /// <summary>
    /// RSA 软件授权服务 — 机器码生成 + 授权码验签 + 授权文件管理
    /// </summary>
    public class LicenseService
    {
        private static LicenseService _instance;
        private static readonly object _lock = new object();

        public static LicenseService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new LicenseService();
                    }
                }
                return _instance;
            }
        }

        // RSA 公钥（嵌入客户端，用于验签）
        private const string PUBLIC_KEY_XML =
            "<RSAKeyValue><Modulus>zpoL5zg/Le/nFziNyTqHYpuz+9TTeSUO6TurPfOn+KcuA4JSb0AL7i9641H9qpLugzHUk3ZHirRrMxdnYZe77k7Pb0MmpRi8pVIP6wIMODtFrts1d7xc9k6m1YdflsA4Ej83++dR+up690zvI66ZAWTX4B1ehMzRibu3Gy0Ooh7nOM/nF6Ex7UGgC1xwrr2s3QX1H4ChsuSS4qxonPpykYnoJHsofnWLHNm9JshKx9/X9ma4OSTfjZqSQPP7SQ10B83+5z+rSpDTAULuzfO3w2zXIwfyvU9fYFCKsV09CganNONFu8kPAGh3bXCaib6qtdgxBmmMTnGvPe+LeYcRpQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        private string _licenseDir;
        private string _licenseFile;
        private LicenseInfo _cachedLicense;

        private LicenseService()
        {
            _licenseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndustrialDataCollection");
            _licenseFile = Path.Combine(_licenseDir, "license.dat");

            if (!Directory.Exists(_licenseDir))
                Directory.CreateDirectory(_licenseDir);
        }

        #region 机器码生成

        /// <summary>
        /// 生成机器码：CPU ID + 硬盘序列号 + MAC → SHA256 → 64位大写十六进制
        /// 任何硬件信息获取失败时使用默认值，保证始终输出固定长度
        /// </summary>
        public string GetMachineId()
        {
            string cpuId = GetCpuId();
            string diskSerial = GetDiskSerial();
            string mac = GetMacAddress();

            string raw = cpuId + "|" + diskSerial + "|" + mac;
            byte[] hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(raw));
            return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant(); // 64 chars
        }

        private string GetCpuId()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string id = obj["ProcessorId"]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(id) && id != "0000000000000000")
                            return id;
                    }
                }
            }
            catch { }
            return "CPU_UNKNOWN";
        }

        private string GetDiskSerial()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT SerialNumber FROM Win32_DiskDrive WHERE Index=0"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string serial = obj["SerialNumber"]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(serial))
                            return serial;
                    }
                }
            }
            catch { }
            return "DISK_UNKNOWN";
        }

        private string GetMacAddress()
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
                    return nic.GetPhysicalAddress().ToString().ToUpperInvariant();

                var fallback = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                             && n.GetPhysicalAddress().GetAddressBytes().Length == 6)
                    .FirstOrDefault();
                if (fallback != null)
                    return fallback.GetPhysicalAddress().ToString().ToUpperInvariant();
            }
            catch { }
            return "MAC_UNKNOWN";
        }

        #endregion

        #region 授权验证

        /// <summary>
        /// 检查是否已激活（授权文件存在且有效）
        /// </summary>
        public bool IsActivated()
        {
            if (!File.Exists(_licenseFile))
                return false;

            try
            {
                string encoded = File.ReadAllText(_licenseFile, Encoding.UTF8).Trim();
                var result = ValidateLicense(encoded);
                return result.IsValid;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 验证授权码并返回详细信息
        /// </summary>
        public LicenseValidationResult ValidateLicense(string licenseCode)
        {
            var result = new LicenseValidationResult();

            try
            {
                // 1. Base64 解码
                byte[] decoded;
                try
                {
                    decoded = Convert.FromBase64String(licenseCode.Trim());
                }
                catch (FormatException)
                {
                    result.ErrorMessage = "授权码格式无效，不是有效的Base64编码";
                    return result;
                }

                string combined;
                try
                {
                    combined = Encoding.UTF8.GetString(decoded);
                }
                catch
                {
                    result.ErrorMessage = "授权码编码错误，无法解码";
                    return result;
                }

                // 2. 分割 JSON 和签名
                int sepIdx = combined.LastIndexOf("|||");
                if (sepIdx < 0)
                {
                    result.ErrorMessage = "授权码格式无效，缺少签名分隔符";
                    return result;
                }

                string jsonPart = combined.Substring(0, sepIdx);
                string sigBase64 = combined.Substring(sepIdx + 3);

                // 3. JSON 反序列化
                LicenseInfo info;
                try
                {
                    info = JsonConvert.DeserializeObject<LicenseInfo>(jsonPart);
                    if (info == null)
                    {
                        result.ErrorMessage = "授权信息为空";
                        return result;
                    }
                }
                catch (JsonException)
                {
                    result.ErrorMessage = "授权信息JSON解析失败";
                    return result;
                }

                // 4. RSA-SHA256 验签
                byte[] sigBytes;
                try
                {
                    sigBytes = Convert.FromBase64String(sigBase64);
                }
                catch (FormatException)
                {
                    result.ErrorMessage = "授权签名格式无效";
                    return result;
                }

                bool signatureValid;
                try
                {
                    using (var rsa = RSA.Create())
                    {
                        rsa.FromXmlString(PUBLIC_KEY_XML);
                        byte[] dataBytes = Encoding.UTF8.GetBytes(jsonPart);
                        signatureValid = rsa.VerifyData(dataBytes, sigBytes,
                            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    }
                }
                catch (CryptographicException ex)
                {
                    result.ErrorMessage = "公钥验证失败：" + ex.Message;
                    return result;
                }

                if (!signatureValid)
                {
                    result.ErrorMessage = "授权码签名验证失败！授权码被篡改或无效";
                    return result;
                }

                // 5. 验证机器码
                string currentMachineId = GetMachineId();
                if (!string.Equals(info.MachineId, currentMachineId, StringComparison.OrdinalIgnoreCase))
                {
                    result.ErrorMessage = "机器码不匹配！\n\n" +
                        "本机机器码：" + currentMachineId + "\n" +
                        "授权绑定机器码：" + info.MachineId + "\n\n" +
                        "请使用本机机器码重新生成授权";
                    return result;
                }

                // 6. 验证过期时间
                if (info.ExpireDate != "PERMANENT")
                {
                    DateTime expireDate;
                    if (DateTime.TryParse(info.ExpireDate, out expireDate))
                    {
                        if (DateTime.Now > expireDate)
                        {
                            result.ErrorMessage = "授权已过期！\n" +
                                "过期时间：" + info.ExpireDate + "\n" +
                                "当前时间：" + DateTime.Now.ToString("yyyy-MM-dd") + "\n\n" +
                                "请联系供应商续期";
                            return result;
                        }
                    }
                    else
                    {
                        result.ErrorMessage = "授权过期时间格式无效：" + info.ExpireDate;
                        return result;
                    }
                }

                // 全部通过
                result.IsValid = true;
                result.Info = info;
                result.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "验证过程异常：" + ex.Message;
                Logger.Error("License验证异常: " + ex.ToString());
            }

            return result;
        }

        /// <summary>
        /// 保存授权文件
        /// </summary>
        public void SaveLicense(string licenseCode)
        {
            File.WriteAllText(_licenseFile, licenseCode.Trim(), Encoding.UTF8);
            // 刷新缓存
            var result = ValidateLicense(licenseCode.Trim());
            _cachedLicense = result.IsValid ? result.Info : null;
            Logger.Info("授权已保存: " + _licenseFile);
        }

        /// <summary>
        /// 获取当前授权信息
        /// </summary>
        public LicenseInfo GetCurrentLicense()
        {
            if (_cachedLicense != null)
                return _cachedLicense;

            if (File.Exists(_licenseFile))
            {
                try
                {
                    string encoded = File.ReadAllText(_licenseFile, Encoding.UTF8).Trim();
                    var result = ValidateLicense(encoded);
                    if (result.IsValid)
                    {
                        _cachedLicense = result.Info;
                        return result.Info;
                    }
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// 获取授权文件路径
        /// </summary>
        public string LicenseFilePath => _licenseFile;

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// 授权信息（与 LicenseGenerator 中的结构一致）
    /// </summary>
    public class LicenseInfo
    {
        [JsonProperty("MachineId")]
        public string MachineId { get; set; }

        [JsonProperty("ExpireDate")]
        public string ExpireDate { get; set; } // "PERMANENT" 或 "yyyy-MM-dd"

        [JsonProperty("MaxDevices")]
        public int MaxDevices { get; set; }

        [JsonProperty("LicenseType")]
        public string LicenseType { get; set; }

        [JsonProperty("CreatedAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("Generator")]
        public string Generator { get; set; }

        /// <summary>
        /// 是否为永久授权
        /// </summary>
        [JsonIgnore]
        public bool IsPermanent => ExpireDate == "PERMANENT";

        /// <summary>
        /// 授权类型的显示名称
        /// </summary>
        [JsonIgnore]
        public string LicenseTypeDisplay
        {
            get
            {
                switch (LicenseType)
                {
                    case "Standard": return "标准版";
                    case "Enterprise": return "企业版";
                    case "Premium": return "高级版";
                    case "Custom": return "定制版";
                    default: return LicenseType ?? "未知";
                }
            }
        }
    }

    /// <summary>
    /// 授权验证结果
    /// </summary>
    public class LicenseValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public LicenseInfo Info { get; set; }
    }

    #endregion
}
