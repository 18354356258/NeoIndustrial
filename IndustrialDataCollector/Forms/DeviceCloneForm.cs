using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// 设备克隆对话框 — 深拷贝设备配置，支持选择性克隆、分组选择、后置操作
    /// </summary>
    public partial class DeviceCloneForm : Form
    {
        private readonly DeviceConfig _sourceDevice;
        private readonly bool _sourceIsCollecting;
        private CloneResult _cloneResult;
        private string _newDeviceId;

        public CloneResult Result => _cloneResult;
        public string NewDeviceId => _newDeviceId;

        public DeviceCloneForm(DeviceConfig sourceDevice, bool isCollecting)
        {
            InitializeComponent();

            _sourceDevice = sourceDevice;
            _sourceIsCollecting = isCollecting;

            // 初始化源设备信息显示
            lblSourceDevice.Text = string.Format("源设备: {0} ({1})",
                sourceDevice.Name ?? "未知",
                sourceDevice.DriverType ?? "未知");

            // 默认名称
            txtNewName.Text = (sourceDevice.Name ?? "设备") + "-副本";

            // 事件绑定
            chkDriver.CheckedChanged += (s, e) => RefreshPreview();
            chkVars.CheckedChanged += (s, e) => RefreshPreview();
            chkCleaning.CheckedChanged += (s, e) => RefreshPreview();
            chkFabric.CheckedChanged += (s, e) => RefreshPreview();
            chkEvents.CheckedChanged += (s, e) => RefreshPreview();
            chkRelations.CheckedChanged += (s, e) => RefreshPreview();

            btnClone.Click += BtnClone_Click;

            Load += Form_Load;
        }

        private void Form_Load(object sender, EventArgs e)
        {
            LoadGroups();

            // 如果源设备不在采集，禁用自动启动采集选项
            if (!_sourceIsCollecting)
            {
                chkAutoCollect.Checked = false;
                chkAutoCollect.Enabled = false;
            }
            else
            {
                chkAutoCollect.Checked = true;
            }

            RefreshPreview();
        }

        /// <summary>
        /// 从所有设备中提取去重分组
        /// </summary>
        private void LoadGroups()
        {
            try
            {
                var devices = ConfigService.Instance.LoadDevices();
                var groups = new HashSet<string>();

                foreach (var dev in devices)
                {
                    if (!string.IsNullOrEmpty(dev.Group))
                        groups.Add(dev.Group);
                }

                cboGroup.Items.Clear();
                cboGroup.Items.Add("（保留原组）");
                foreach (var g in groups.OrderBy(g => g))
                    cboGroup.Items.Add(g);

                // 默认选中源设备分组
                if (!string.IsNullOrEmpty(_sourceDevice.Group))
                {
                    int idx = cboGroup.Items.IndexOf(_sourceDevice.Group);
                    if (idx >= 0)
                        cboGroup.SelectedIndex = idx;
                    else
                        cboGroup.SelectedIndex = 0;
                }
                else
                {
                    cboGroup.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("加载分组列表失败: " + ex.Message);
                cboGroup.Items.Add("（保留原组）");
                cboGroup.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 刷新克隆预览面板
        /// </summary>
        private void RefreshPreview()
        {
            var sb = new StringBuilder();

            int varCount = chkVars.Checked ? (_sourceDevice.DataPoints?.Count ?? 0) : 0;
            int relCount = 0;
            int fabricCount = 0;
            int eventCount = 0;
            int cleanCount = 0;

            // 语义关系和事件规则的数量不在 DeviceConfig 对象中直接可得，
            // 这里显示为 "将尝试复制"
            if (chkRelations.Checked) relCount = -1; // -1 表示"将尝试复制"
            if (chkFabric.Checked) fabricCount = -1;
            if (chkEvents.Checked) eventCount = -1;
            if (chkCleaning.Checked)
            {
                if (_sourceDevice.DataPoints != null)
                {
                    cleanCount = _sourceDevice.DataPoints.Count(dp =>
                        dp.DeadBandEnabled || dp.ClipEnabled || dp.OutlierEnabled
                        || dp.NanFilterEnabled || dp.FreezeEnabled || dp.SpikeEnabled
                        || dp.RocLimitEnabled || dp.IqrEnabled || dp.RangeEnabled);
                }
            }

            sb.AppendLine("将复制:");
            sb.Append("  ");
            sb.Append(chkVars.Checked ? string.Format("{0} 个变量", varCount) : "无变量");
            sb.Append(" / ");
            sb.Append(chkDriver.Checked ? "驱动参数" : "无驱动参数");
            sb.AppendLine();

            sb.Append("  ");
            sb.Append(chkRelations.Checked ? "语义关系" : "无语义关系");
            sb.Append(" / ");
            sb.Append(chkCleaning.Checked ? string.Format("{0} 条清洗策略", cleanCount) : "无清洗策略");
            sb.AppendLine();

            sb.Append("  ");
            sb.Append(chkFabric.Checked ? "Fabric配置" : "无Fabric配置");
            sb.Append(" / ");
            sb.Append(chkEvents.Checked ? "事件规则" : "无事件规则");
            sb.AppendLine();

            sb.AppendLine();
            sb.Append("新设备名称: ");
            sb.AppendLine(txtNewName.Text.Trim());

            string selectedGroup = cboGroup.Text ?? "";
            if (cboGroup.SelectedIndex <= 0 || selectedGroup == "（保留原组）")
                sb.AppendLine("目标分组: （保留原组）");
            else
                sb.AppendLine("目标分组: " + selectedGroup);

            rtbPreview.Text = sb.ToString();
        }

        private void BtnClone_Click(object sender, EventArgs e)
        {
            string newName = (txtNewName.Text ?? "").Trim();

            // 1. 校验新设备名称
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("请输入新设备名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewName.Focus();
                return;
            }

            // 2. 检查名称是否与现有设备重复
            try
            {
                var devices = ConfigService.Instance.LoadDevices();
                bool nameExists = devices.Any(d =>
                    d.Id != _sourceDevice.Id
                    && string.Equals(d.Name, newName, StringComparison.OrdinalIgnoreCase));

                if (nameExists)
                {
                    MessageBox.Show(
                        string.Format("设备名称 \"{0}\" 已存在，请换一个名称", newName),
                        "名称冲突",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    txtNewName.Focus();
                    txtNewName.SelectAll();
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("加载设备列表失败: " + ex.Message);
                // 继续执行，Service 层会再次校验
            }

            // 3. 组装 CloneOptions
            string newGroupPath = "";
            if (cboGroup.SelectedIndex > 0)
                newGroupPath = cboGroup.Text;

            var options = new CloneOptions
            {
                SourceDeviceId = _sourceDevice.Id,
                NewDeviceName = newName,
                NewGroupPath = newGroupPath,
                CloneDriverParams = chkDriver.Checked,
                CloneVariables = chkVars.Checked,
                CloneCleaningStrategies = chkCleaning.Checked,
                CloneFabricConfigs = chkFabric.Checked,
                CloneEventRules = chkEvents.Checked,
                CloneSemanticRelations = chkRelations.Checked,
                AutoGenerateTags = chkAutoTags.Checked,
                AutoOpenConfig = chkAutoOpenConfig.Checked,
                AutoStartCollect = chkAutoCollect.Checked && chkAutoCollect.Enabled
            };

            try
            {
                Logger.Info(string.Format("开始克隆设备: {0} → {1}", _sourceDevice.Name, newName));

                _cloneResult = DeviceCloneService.Instance.CloneDevice(options);

                if (_cloneResult != null && _cloneResult.Success)
                {
                    _newDeviceId = _cloneResult.NewDeviceId;

                    MessageBox.Show(
                        string.Format("设备克隆成功！\n\n源设备: {0}\n新设备: {1}\n克隆变量: {2} 个\n语义关系: {3} 条",
                            _cloneResult.SourceDevice,
                            _cloneResult.NewDeviceName,
                            _cloneResult.ClonedVariables,
                            _cloneResult.ClonedRelations),
                        "克隆成功",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    string error = _cloneResult?.Error ?? "未知错误";
                    MessageBox.Show("设备克隆失败: " + error, "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("设备克隆异常: " + ex.Message);
                MessageBox.Show("设备克隆失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
