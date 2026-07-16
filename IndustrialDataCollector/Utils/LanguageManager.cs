using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using IndustrialDataCollection.Services;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Utils
{
    /// <summary>
    /// 多语言管理器 - 支持运行时切换中/英文
    /// </summary>
    public class LanguageManager
    {
        private static LanguageManager _instance;
        public static LanguageManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new LanguageManager();
                return _instance;
            }
        }

        private Dictionary<string, string> _currentLang = new Dictionary<string, string>();
        private string _currentLangCode = "zh";
        private readonly string _langSettingFile;

        public event EventHandler LanguageChanged;

        public string CurrentLanguage => _currentLangCode;

        private LanguageManager()
        {
            _langSettingFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndustrialDataCollection", "config", "lang.json");

            // 加载保存的语言设置
            LoadLanguageSetting();
            // 加载语言包
            LoadLanguage();
        }

        /// <summary>
        /// 切换语言
        /// </summary>
        public void SwitchLanguage()
        {
            _currentLangCode = _currentLangCode == "zh" ? "en" : "zh";
            SaveLanguageSetting();
            LoadLanguage();
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 设置指定语言
        /// </summary>
        public void SetLanguage(string langCode)
        {
            if (langCode != "zh" && langCode != "en") return;
            _currentLangCode = langCode;
            SaveLanguageSetting();
            LoadLanguage();
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 获取翻译文本
        /// </summary>
        public string GetString(string key)
        {
            string val;
            if (_currentLang.TryGetValue(key, out val))
                return val;
            return key; // 找不到就返回 key 本身
        }

        /// <summary>
        /// 加载保存的语言设置
        /// </summary>
        private void LoadLanguageSetting()
        {
            try
            {
                var dir = Path.GetDirectoryName(_langSettingFile);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(_langSettingFile))
                {
                    var json = File.ReadAllText(_langSettingFile);
                    var setting = JsonConvert.DeserializeObject<LangSetting>(json);
                    if (setting != null && !string.IsNullOrEmpty(setting.LangCode))
                        _currentLangCode = setting.LangCode;
                }
            }
            catch
            {
                _currentLangCode = "zh";
            }
        }

        /// <summary>
        /// 保存语言设置
        /// </summary>
        private void SaveLanguageSetting()
        {
            try
            {
                var dir = Path.GetDirectoryName(_langSettingFile);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var setting = new LangSetting { LangCode = _currentLangCode };
                File.WriteAllText(_langSettingFile, JsonConvert.SerializeObject(setting));
            }
            catch { }
        }

        /// <summary>
        /// 加载语言包 JSON 文件
        /// </summary>
        private void LoadLanguage()
        {
            try
            {
                string langFile = Path.Combine(Application.StartupPath, "Resources", $"Lang_{_currentLangCode}.json");

                // fallback 到中文
                if (!File.Exists(langFile))
                {
                    langFile = Path.Combine(Application.StartupPath, "Resources", "Lang_zh.json");
                    if (!File.Exists(langFile))
                    {
                        _currentLang = new Dictionary<string, string>();
                        Logger.Warn("语言文件不存在");
                        return;
                    }
                }

                var json = File.ReadAllText(langFile);
                _currentLang = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>();

                Logger.Info($"语言已加载: {_currentLangCode}, {_currentLang.Count} 条");
            }
            catch (Exception ex)
            {
                _currentLang = new Dictionary<string, string>();
                Logger.Error($"加载语言包失败: {ex.Message}");
            }
        }

        private class LangSetting
        {
            public string LangCode { get; set; }
        }
    }
}
