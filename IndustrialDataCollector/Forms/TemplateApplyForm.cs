using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// 选择配置模板并应用到目标设备 — 展示模板库，选中后应用并显示结果报告
    /// </summary>
    public partial class TemplateApplyForm : Form
    {
        private readonly string _targetDeviceId;
        private readonly string _targetDeviceName;
        private DeviceTemplate _selectedTemplate;
        private TemplateApplyResult _applyResult;

        public bool Applied { get; private set; }
        public TemplateApplyResult Result => _applyResult;

        public TemplateApplyForm(string deviceId, string deviceName)
        {
            InitializeComponent();

            _targetDeviceId = deviceId;
            _targetDeviceName = deviceName;

            lblTargetDevice.Text = "目标设备: " + deviceName;

            trvTemplates.AfterSelect += TrvTemplates_AfterSelect;
            btnApply.Click += BtnApply_Click;
            txtSearch.TextChanged += TxtSearch_TextChanged;

            // 搜索框占位
            txtSearch.ForeColor = Color.Gray;
            txtSearch.Text = "搜索模板...";
            txtSearch.Enter += (s, e) =>
            {
                if (txtSearch.Text == "搜索模板...")
                {
                    txtSearch.Text = "";
                    txtSearch.ForeColor = SystemColors.WindowText;
                }
            };
            txtSearch.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    txtSearch.Text = "搜索模板...";
                    txtSearch.ForeColor = Color.Gray;
                }
            };

            Load += (s, e) => RefreshTemplateTree();
        }

        private void RefreshTemplateTree()
        {
            try
            {
                var templates = TemplateService.Instance.ListAll();
                trvTemplates.BeginUpdate();
                trvTemplates.Nodes.Clear();

                var rootNode = trvTemplates.Nodes.Add("全部模板");

                var categoryTree = new Dictionary<string, TreeNode>();
                foreach (var tpl in templates)
                {
                    string cat = string.IsNullOrEmpty(tpl.Category) ? "未分类" : tpl.Category;
                    string[] parts = cat.Split('/');

                    TreeNode parentNode = rootNode;
                    string currentPath = "";
                    foreach (var part in parts)
                    {
                        currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;
                        if (!categoryTree.ContainsKey(currentPath))
                        {
                            var folderNode = new TreeNode(part) { Tag = null };
                            parentNode.Nodes.Add(folderNode);
                            categoryTree[currentPath] = folderNode;
                        }
                        parentNode = categoryTree[currentPath];
                    }

                    var tplNode = new TreeNode(tpl.TemplateName + " (v" + (tpl.Version ?? "1.0") + ")")
                    {
                        Tag = tpl
                    };
                    parentNode.Nodes.Add(tplNode);
                }

                rootNode.Expand();
                trvTemplates.EndUpdate();
            }
            catch (Exception ex)
            {
                Logger.Error("加载模板树失败: " + ex.Message);
            }
        }

        private void TrvTemplates_AfterSelect(object sender, TreeViewEventArgs e)
        {
            _selectedTemplate = e.Node?.Tag as DeviceTemplate;
            ShowPreview(_selectedTemplate);
        }

        private void ShowPreview(DeviceTemplate template)
        {
            if (template == null)
            {
                lblPrevName.Text = "名称: --";
                lblPrevCategory.Text = "分类: --";
                lblPrevVersion.Text = "版本: --";
                lblPrevSourceDevice.Text = "来源设备: --";
                lblPrevTime.Text = "创建时间: --";
                lblPrevDesc.Text = "描述: --";

                lblContVars.Text = "• 0 个变量映射规则";
                lblContRelations.Text = "• 0 条语义关系";
                lblContFabric.Text = "• 0 个Fabric算子配置";
                lblContEvents.Text = "• 0 条事件规则";
                lblContClean.Text = "• 0 条清洗策略";
                return;
            }

            lblPrevName.Text = "名称: " + (template.TemplateName ?? "--");
            lblPrevCategory.Text = "分类: " + (string.IsNullOrEmpty(template.Category) ? "未分类" : template.Category);
            lblPrevVersion.Text = "版本: " + (template.Version ?? "1.0");
            lblPrevSourceDevice.Text = "来源设备: " + (template.CreatedFromDevice ?? "--");
            lblPrevTime.Text = "创建时间: " + template.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            lblPrevDesc.Text = "描述: " + (string.IsNullOrEmpty(template.Description) ? "无" : template.Description);

            lblContVars.Text = string.Format("• {0} 个变量映射规则", template.VariablePatterns?.Count ?? 0);
            lblContRelations.Text = string.Format("• {0} 条语义关系", template.SemanticRelations?.Count ?? 0);
            lblContFabric.Text = string.Format("• {0} 个Fabric算子配置", template.FabricConfigs?.Count ?? 0);
            lblContEvents.Text = string.Format("• {0} 条事件规则", template.EventRules?.Count ?? 0);
            lblContClean.Text = string.Format("• {0} 条清洗策略", template.CleaningStrategies?.Count ?? 0);
        }

        private async void BtnApply_Click(object sender, EventArgs e)
        {
            if (_selectedTemplate == null)
            {
                MessageBox.Show("请先在左侧选择一个模板", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnApply.Enabled = false;
            btnApply.Text = "正在应用...";
            rtbResult.Visible = false;

            try
            {
                Logger.Info(string.Format("应用模板 {0} 到设备 {1}", _selectedTemplate.TemplateName, _targetDeviceName));

                _applyResult = TemplateService.Instance.ApplyToDevice(_selectedTemplate.TemplateId, _targetDeviceId);

                if (_applyResult != null && _applyResult.Success)
                {
                    Applied = true;
                }

                // 显示结果
                ShowApplyResult(_applyResult);
            }
            catch (Exception ex)
            {
                Logger.Error("模板应用失败: " + ex.Message);
                rtbResult.Text = "应用失败: " + ex.Message;
                rtbResult.Visible = true;
            }
            finally
            {
                btnApply.Enabled = true;
                btnApply.Text = "应用配置";
            }
        }

        private void ShowApplyResult(TemplateApplyResult result)
        {
            if (result == null)
            {
                rtbResult.Text = "应用结果为空";
                rtbResult.Visible = true;
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== 模板应用结果 ===");
            sb.AppendLine(string.Format("状态: {0}", result.Success ? "✓ 成功" : "✗ 失败"));
            sb.AppendLine(string.Format("模板: {0}", result.TemplateName ?? "--"));
            sb.AppendLine(string.Format("设备: {0}", result.DeviceName ?? "--"));
            sb.AppendLine(string.Format("变量匹配: {0}/{1} 成功, {2} 跳过",
                result.MatchedCount, result.TotalPatterns, result.SkippedCount));
            sb.AppendLine();

            // 变量匹配详情
            if (result.VariableMatches != null && result.VariableMatches.Count > 0)
            {
                sb.AppendLine("--- 变量匹配详情 ---");
                foreach (var m in result.VariableMatches)
                {
                    if (m.Success)
                        sb.AppendLine(string.Format("  ✓ [{0}] → {1} ({2})", m.TemplateVar, m.ActualVarName, m.MatchMethod));
                    else
                        sb.AppendLine(string.Format("  ✗ [{0}] 未匹配: {1}", m.TemplateVar, m.SkipReason ?? "无匹配"));
                }
                sb.AppendLine();
            }

            // 应用汇总
            sb.AppendLine("--- 应用汇总 ---");
            sb.AppendLine(string.Format("  语义关系: {0}", result.AppliedRelations));
            sb.AppendLine(string.Format("  Fabric配置: {0}", result.AppliedFabric));
            sb.AppendLine(string.Format("  事件规则: {0}", result.AppliedEvents));
            sb.AppendLine(string.Format("  清洗策略: {0}", result.AppliedCleaning));

            // 应用明细
            if (result.AppliedItems != null && result.AppliedItems.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("--- 应用明细 ---");
                foreach (var item in result.AppliedItems)
                {
                    string status = item.Success ? "成功" : "跳过";
                    sb.AppendLine(string.Format("  [{0}] {1} - {2}", item.ItemType, item.Description, status));
                }
            }

            if (!string.IsNullOrEmpty(result.LogPath))
            {
                sb.AppendLine();
                sb.AppendLine("日志已保存: " + result.LogPath);
            }

            rtbResult.Text = sb.ToString();
            rtbResult.Visible = true;
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            string search = (txtSearch.Text ?? "").Trim().ToLower();
            if (search == "搜索模板...") return;

            trvTemplates.BeginUpdate();

            if (string.IsNullOrEmpty(search))
            {
                // 恢复所有节点可见
                foreach (TreeNode node in trvTemplates.Nodes)
                    RestoreNodeVisibility(node);
            }
            else
            {
                FilterTreeForApply(trvTemplates.Nodes, search);
            }

            trvTemplates.EndUpdate();
        }

        private void RestoreNodeVisibility(TreeNode node)
        {
            node.ForeColor = SystemColors.WindowText;
            foreach (TreeNode child in node.Nodes)
                RestoreNodeVisibility(child);
        }

        private void FilterTreeForApply(TreeNodeCollection nodes, string search)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is DeviceTemplate)
                {
                    bool matches = node.Text.ToLower().Contains(search);
                    if (!matches)
                    {
                        // 也检查父路径
                        string path = GetNodeFullPath(node).ToLower();
                        matches = path.Contains(search);
                    }
                    node.ForeColor = matches ? SystemColors.WindowText : Color.Gray;
                }
                FilterTreeForApply(node.Nodes, search);
            }
        }

        private string GetNodeFullPath(TreeNode node)
        {
            var parts = new List<string>();
            var current = node;
            while (current != null)
            {
                if (current.Parent != null)
                    parts.Insert(0, current.Text);
                current = current.Parent;
            }
            return string.Join("/", parts);
        }
    }
}
