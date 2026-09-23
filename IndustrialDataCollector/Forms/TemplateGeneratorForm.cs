using System;
using System.Linq;
using System.Windows.Forms;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// 从设备生成配置模板 — 接收源设备信息，选择包含模块和匹配方式后生成模板
    /// </summary>
    public partial class TemplateGeneratorForm : Form
    {
        private readonly string _sourceDeviceId;
        private readonly string _sourceDeviceName;
        private readonly string _sourceDriverType;

        public TemplateGeneratorForm(string deviceId, string deviceName, string driverType)
        {
            InitializeComponent();

            _sourceDeviceId = deviceId;
            _sourceDeviceName = deviceName;
            _sourceDriverType = driverType;

            lblSourceDevice.Text = "源设备: " + deviceName;
            lblSourceDriver.Text = "驱动: " + driverType;

            btnGenerate.Click += BtnGenerate_Click;

            Load += Form_Load;
        }

        private void Form_Load(object sender, EventArgs e)
        {
            LoadCategories();
            LoadVariableList();
        }

        /// <summary>
        /// 从现有模板中提取分类列表，预填到 ComboBox
        /// </summary>
        private void LoadCategories()
        {
            try
            {
                var categories = TemplateService.Instance.ListCategories();

                // 先加一个默认提示
                cboCategory.Items.Clear();
                foreach (var cat in categories)
                    cboCategory.Items.Add(cat);
                cboCategory.Items.Add("新建分类...");

                // 默认选中设备驱动类型作为分类（如果该分类有模板）
                int idx = categories.IndexOf(_sourceDriverType);
                if (idx >= 0)
                    cboCategory.SelectedIndex = idx;
                else
                    cboCategory.Text = _sourceDriverType;
            }
            catch (Exception ex)
            {
                Logger.Debug("加载分类列表失败: " + ex.Message);
                cboCategory.Text = _sourceDriverType;
            }
        }

        /// <summary>
        /// 从源设备加载变量列表到预览 ListView
        /// </summary>
        private void LoadVariableList()
        {
            try
            {
                var devices = ConfigService.Instance.LoadDevices();
                var device = devices.FirstOrDefault(d => d.Id == _sourceDeviceId);
                if (device == null || device.DataPoints == null)
                    return;

                string matchRule = GetSelectedMatchMode();
                lstVariables.BeginUpdate();
                lstVariables.Items.Clear();

                foreach (var dp in device.DataPoints)
                {
                    var item = new ListViewItem(dp.Name ?? "");
                    item.SubItems.Add(dp.DataType ?? "");
                    item.SubItems.Add(dp.Unit ?? "");
                    item.SubItems.Add(matchRule);
                    lstVariables.Items.Add(item);
                }

                lstVariables.EndUpdate();

                // 更新标题显示变量数量
                gbxVarPreview.Text = string.Format("变量映射预览 ({0} 个)", device.DataPoints.Count);
            }
            catch (Exception ex)
            {
                Logger.Error("加载变量列表失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 获取当前选中的匹配模式
        /// </summary>
        private string GetSelectedMatchMode()
        {
            if (radExact.Checked) return "精确";
            if (radRegex.Checked) return "正则";
            if (radContains.Checked) return "包含";
            return "精确";
        }

        private void BtnGenerate_Click(object sender, EventArgs e)
        {
            try
            {
                // 校验模板名称
                string name = (txtTemplateName.Text ?? "").Trim();
                if (string.IsNullOrEmpty(name))
                {
                    MessageBox.Show("请输入模板名称", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtTemplateName.Focus();
                    return;
                }

                // 组装选项
                string category = cboCategory.Text.Trim();
                if (category == "新建分类...")
                    category = "";

                var options = new TemplateGenOptions
                {
                    TemplateName = name,
                    Category = category,
                    Description = (txtDescription.Text ?? "").Trim(),
                    MatchMode = GetSelectedMatchMode(),
                    IncludeSemanticRelations = chkSemanticRelations.Checked,
                    IncludeFabricConfigs = chkFabricConfigs.Checked,
                    IncludeEventRules = chkEventRules.Checked,
                    IncludeCleaningStrategies = chkCleaningStrategies.Checked
                };

                Logger.Info(string.Format("生成模板: {0} 从设备 {1}", options.TemplateName, _sourceDeviceName));

                var template = TemplateService.Instance.GenerateFromDevice(_sourceDeviceId, options);

                if (template != null)
                {
                    MessageBox.Show(
                        string.Format("模板生成成功！\n\n模板名称: {0}\n变量规则: {1} 条\n语义关系: {2} 条\n事件规则: {3} 条\n清洗策略: {4} 条",
                            template.TemplateName,
                            template.VariableCount,
                            template.SemanticRelations.Count,
                            template.EventRules.Count,
                            template.CleaningStrategies.Count),
                        "模板生成成功",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show("模板生成失败，请查看日志了解详情", "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("模板生成失败: " + ex.Message);
                MessageBox.Show("模板生成失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
