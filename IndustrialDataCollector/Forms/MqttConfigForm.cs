using System;
using System.Windows.Forms;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// MQTT 配置界面
    /// </summary>
    public partial class MqttConfigForm : Form
    {
        private MqttConfig _config;

        public MqttConfig MqttConfig { get; private set; }

        public MqttConfigForm() : this(new MqttConfig()) { }

        public MqttConfigForm(MqttConfig config)
        {
            InitializeComponent();
            Icon = Program.AppIcon;
            _config = config;
            LoadConfig();

            LanguageManager.Instance.LanguageChanged += (s, ev) => ApplyLanguage();
            ApplyLanguage();
        }

        /// <summary>
        /// 应用多语言
        /// </summary>
        private void ApplyLanguage()
        {
            var L = LanguageManager.Instance;
            this.Text = L.GetString("MqttConfig_Title");
            lblBrokerHost.Text = L.GetString("MqttConfig_BrokerHost");
            lblBrokerPort.Text = L.GetString("MqttConfig_BrokerPort");
            lblClientId.Text = L.GetString("MqttConfig_ClientId");
            lblUsername.Text = L.GetString("MqttConfig_Username");
            lblPassword.Text = L.GetString("MqttConfig_Password");
            lblTopicPrefix.Text = L.GetString("MqttConfig_TopicPrefix");
            lblQos.Text = L.GetString("MqttConfig_Qos");
            chkAutoReconnect.Text = L.GetString("MqttConfig_AutoReconnect");
            chkEnabled.Text = L.GetString("MqttConfig_Enabled");
            btnTest.Text = L.GetString("MqttConfig_Test");
            btnSave.Text = L.GetString("MqttConfig_Save");
            btnCancel.Text = L.GetString("MqttConfig_Cancel");
        }

        /// <summary>
        /// 加载配置到 UI
        /// </summary>
        private void LoadConfig()
        {
            if (_config == null) return;
            txtBrokerHost.Text = _config.BrokerHost;
            txtBrokerPort.Text = _config.BrokerPort.ToString();
            txtClientId.Text = _config.ClientId;
            txtUsername.Text = _config.Username;
            txtPassword.Text = _config.Password;
            txtTopicPrefix.Text = _config.TopicPrefix;
            comboQos.SelectedIndex = _config.Qos >= 0 && _config.Qos <= 2 ? _config.Qos : 1;
            chkAutoReconnect.Checked = _config.AutoReconnect;
            chkEnabled.Checked = _config.Enabled;
        }

        /// <summary>
        /// 测试 MQTT 连接
        /// </summary>
        private async void btnTest_Click(object sender, EventArgs e)
        {
            if (!ValidateInput()) return;

            btnTest.Enabled = false;
            btnTest.Text = "...";

            try
            {
                var testConfig = BuildConfig();
                bool success = await MqttPublishService.Instance.ConnectAsync(testConfig);

                var L = LanguageManager.Instance;
                if (success)
                {
                    MessageBox.Show(L.GetString("Msg_Success_TestConnect"),
                        L.GetString("MqttConfig_Test"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await MqttPublishService.Instance.DisconnectAsync();
                }
                else
                {
                    MessageBox.Show(L.GetString("Msg_Fail_TestConnect"),
                        L.GetString("MqttConfig_Test"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                var L = LanguageManager.Instance;
                MessageBox.Show(string.Format(L.GetString("Msg_Fail_TestConnect"), ex.Message),
                    L.GetString("MqttConfig_Test"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTest.Enabled = true;
                btnTest.Text = LanguageManager.Instance.GetString("MqttConfig_Test");
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!ValidateInput()) return;

            MqttConfig = BuildConfig();
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// 从 UI 构建 MQTT 配置
        /// </summary>
        private MqttConfig BuildConfig()
        {
            var config = new MqttConfig();
            config.BrokerHost = txtBrokerHost.Text.Trim();
            int portVal;
            if (int.TryParse(txtBrokerPort.Text, out portVal))
                config.BrokerPort = portVal;
            else
                config.BrokerPort = 1883;
            config.ClientId = txtClientId.Text.Trim();
            config.Username = txtUsername.Text.Trim();
            config.Password = txtPassword.Text;
            config.TopicPrefix = txtTopicPrefix.Text.Trim();
            config.Qos = comboQos.SelectedIndex >= 0 ? comboQos.SelectedIndex : 1;
            config.AutoReconnect = chkAutoReconnect.Checked;
            config.Enabled = chkEnabled.Checked;
            return config;
        }

        /// <summary>
        /// 验证输入
        /// </summary>
        private bool ValidateInput()
        {
            var L = LanguageManager.Instance;
            if (string.IsNullOrWhiteSpace(txtBrokerHost.Text))
            {
                MessageBox.Show(L.GetString("MqttConfig_Validation_BrokerEmpty"),
                    L.GetString("Msg_Info"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtBrokerHost.Focus();
                return false;
            }

            int checkPort;
            if (!int.TryParse(txtBrokerPort.Text, out checkPort) || checkPort < 1 || checkPort > 65535)
            {
                MessageBox.Show(L.GetString("MqttConfig_Validation_PortInvalid"),
                    L.GetString("Msg_Info"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtBrokerPort.Focus();
                return false;
            }

            return true;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
