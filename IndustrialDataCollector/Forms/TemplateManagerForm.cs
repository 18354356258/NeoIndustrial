using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Forms
{
    public partial class TemplateManagerForm : Form
    {
        private List<DeviceTemplate> _allTemplates;
        private DeviceTemplate _selectedTemplate;
        private TabControl _tabContent;
        // 布局常量
        private const int PAD = 12, GAP = 8;
        private const int BTN_H = 44, DETAIL_H = 200;
        // 标题
        private static readonly string[] TabTitles = { "变量映射", "语义关系", "Fabric 算子", "事件规则", "清洗策略" };

        public DeviceTemplate SelectedTemplate => _selectedTemplate;

        public TemplateManagerForm()
        {
            InitializeComponent();
            SetupEventHandlers();
            SetupSearchBox();
            Load += OnLoad;
        }

        private void OnLoad(object sender, EventArgs e)
        {
            BuildContentTabs();
            RefreshTree();
            // 窗口尺寸变化时自动重新算布局
            pnlRight.Resize += (s, ev) => DoLayout();
        }

        private void SetupEventHandlers()
        {
            trvTemplates.AfterSelect += TrvTemplates_AfterSelect;
            btnRefresh.Click += (s, e) => RefreshTree();
            btnDelete.Click += BtnDelete_Click;
            btnNew.Visible = false; // 已废弃，仅保留控件声明兼容性
        }

        private void SetupSearchBox()
        {
            txtSearch.ForeColor = Color.Gray;
            txtSearch.Text = "搜索模板...";
            txtSearch.Enter += (s, e) =>
            {
                if (txtSearch.Text == "搜索模板...") { txtSearch.Text = ""; txtSearch.ForeColor = SystemColors.WindowText; }
            };
            txtSearch.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text)) { txtSearch.Text = "搜索模板..."; txtSearch.ForeColor = Color.Gray; }
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;
        }

        // ═════════════════════════════════════════════════════════════
        //  零 Dock 布局 — 直接算坐标，永不遮挡
        // ═════════════════════════════════════════════════════════════

        private void BuildContentTabs()
        {
            pnlRight.SuspendLayout();
            gbxContents.SuspendLayout();

            // 详情标签重排
            ReflowDetailLabels();

            // 选项卡（先建控件，后 DoLayout 算位置）
            _tabContent = new TabControl
            {
                Font = new Font("Microsoft YaHei", 10f),
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(120, 32)
            };

            AddTab(0, new (string, float)[] {
                ("模板变量名", 0.22f), ("匹配规则", 0.12f), ("数据类型", 0.12f),
                ("单位", 0.10f), ("中文标签", 0.44f)
            });
            AddTab(1, new (string, float)[] {
                ("源变量", 0.20f), ("目标变量", 0.20f), ("关系类型", 0.14f),
                ("目标类型", 0.14f), ("描述", 0.32f)
            });
            AddTab(2, new (string, float)[] {
                ("算子名称", 0.24f), ("目标变量", 0.20f), ("参数", 0.46f), ("启用", 0.10f)
            });
            AddTab(3, new (string, float)[] {
                ("规则名称", 0.24f), ("条件类型", 0.14f), ("目标变量", 0.18f),
                ("触发条件", 0.24f), ("动作数", 0.20f)
            });
            AddTab(4, new (string, float)[] {
                ("目标变量", 0.20f), ("策略名称", 0.26f), ("参数", 0.44f), ("启用", 0.10f)
            });

            gbxContents.Controls.Clear();
            gbxContents.Controls.Add(_tabContent);

            // 按钮样式（仅删除+刷新，新建已隐藏）
            btnNew.Visible = false;
            btnNew.Width = btnDelete.Width = btnRefresh.Width = 100;
            btnNew.Height = btnDelete.Height = btnRefresh.Height = 30;
            btnNew.Font = btnDelete.Font = btnRefresh.Font = new Font("Microsoft YaHei", 9.5f);

            gbxContents.ResumeLayout(false);
            pnlRight.ResumeLayout(false);

            DoLayout();
        }

        /// <summary>核心：所有控件坐标算术计算，不依赖任何 Dock/Anchor</summary>
        private void DoLayout()
        {
            int W = pnlRight.ClientSize.Width;
            int H = pnlRight.ClientSize.Height;

            // ── 模板详情区 ──
            gbxDetail.SetBounds(PAD, PAD, W - PAD * 2, DETAIL_H);

            // ── 按钮栏 ──
            pnlButtons.SetBounds(PAD, H - PAD - BTN_H, W - PAD * 2, BTN_H);
            // 两按钮居中
            int btnTotal = btnDelete.Width * 2 + GAP;
            btnDelete.Location = new Point((pnlButtons.Width - btnTotal) / 2, (BTN_H - btnDelete.Height) / 2);
            btnRefresh.Location = new Point(btnDelete.Right + GAP, btnDelete.Top);

            // ── 包含内容 → 在详情区和按钮栏之间，上下各留 GAP ──
            int contentY = gbxDetail.Bottom + GAP;
            int contentH = pnlButtons.Top - GAP - contentY;
            gbxContents.SetBounds(PAD, contentY, W - PAD * 2, contentH);

            // TabControl 留内边距，y从20开始避开GroupBox标题
            _tabContent.SetBounds(4, 20, gbxContents.ClientSize.Width - 8, gbxContents.ClientSize.Height - 24);

            // 列宽按比例
            int lvW = _tabContent.ClientSize.Width - 24;
            foreach (TabPage page in _tabContent.TabPages)
            {
                var lv = page.Controls[0] as ListView;
                if (lv == null || lv.Columns.Count == 0) continue;
                float[] ratios = GetColumnRatios(page.Text, lv.Columns.Count);
                for (int i = 0; i < lv.Columns.Count; i++)
                    lv.Columns[i].Width = Math.Max(50, (int)(lvW * ratios[i]));
            }
        }

        private float[] GetColumnRatios(string pageTitle, int colCount)
        {
            if (colCount == 5 && pageTitle.StartsWith("变量映射"))
                return new[] { 0.22f, 0.12f, 0.12f, 0.10f, 0.44f };
            if (colCount == 5 && pageTitle.StartsWith("语义关系"))
                return new[] { 0.20f, 0.20f, 0.14f, 0.14f, 0.32f };
            if (colCount == 4 && pageTitle.StartsWith("Fabric"))
                return new[] { 0.24f, 0.20f, 0.46f, 0.10f };
            if (colCount == 5 && pageTitle.StartsWith("事件规则"))
                return new[] { 0.24f, 0.14f, 0.18f, 0.24f, 0.20f };
            if (colCount == 4 && pageTitle.StartsWith("清洗策略"))
                return new[] { 0.20f, 0.26f, 0.44f, 0.10f };
            // fallback: equal split
            var r = new float[colCount];
            for (int i = 0; i < colCount; i++) r[i] = 1f / colCount;
            return r;
        }

        private void AddTab(int idx, (string name, float ratio)[] cols)
        {
            var page = new TabPage(TabTitles[idx]);
            var lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Clickable,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft YaHei", 10f)
            };
            foreach (var (name, _) in cols)
                lv.Columns.Add(name, 100);
            page.Controls.Add(lv);
            _tabContent.TabPages.Add(page);
        }

        private void ReflowDetailLabels()
        {
            int xL = 14, xR = 400, y0 = 30, dy = 32;

            var left = new (Label lb, FontStyle style, int fontSize)[]
            {
                (lblDetailName,         FontStyle.Bold, 11),
                (lblDetailSourceDevice, FontStyle.Regular, 10),
                (lblDetailCreatedAt,    FontStyle.Regular, 10),
            };
            var right = new (Label lb, FontStyle style, int fontSize)[]
            {
                (lblDetailCategory,  FontStyle.Regular, 10),
                (lblDetailVersion,   FontStyle.Regular, 10),
                (lblDetailUpdatedAt, FontStyle.Regular, 10),
            };

            foreach (var (lb, style, sz) in left.Concat(right))
            {
                lb.AutoSize = false;
                lb.Height = 26;
                lb.TextAlign = ContentAlignment.MiddleLeft;
                lb.Font = new Font("Microsoft YaHei", sz, style);
            }

            lblDetailName.SetBounds(xL, y0, 360, 28);
            lblDetailSourceDevice.SetBounds(xL, y0 + dy, 360, 26);
            lblDetailCreatedAt.SetBounds(xL, y0 + dy * 2, 360, 26);

            lblDetailCategory.SetBounds(xR, y0, 440, 26);
            lblDetailVersion.SetBounds(xR, y0 + dy, 440, 26);
            lblDetailUpdatedAt.SetBounds(xR, y0 + dy * 2, 440, 26);

            // 描述
            lblDetailDesc.SetBounds(xR, y0 + dy * 3, 440, 40);
            lblDetailDesc.Font = new Font("Microsoft YaHei", 9.5f);
        }

        // ═════════════════════════════════════════════════════════════
        //  树刷新
        // ═════════════════════════════════════════════════════════════

        public void RefreshTree()
        {
            try
            {
                _allTemplates = TemplateService.Instance.ListAll();
                trvTemplates.BeginUpdate();
                trvTemplates.Nodes.Clear();
                trvTemplates.Font = new Font("Microsoft YaHei", 10f);

                var rootNode = trvTemplates.Nodes.Add("全部模板");
                var categoryTree = new Dictionary<string, TreeNode>();

                foreach (var tpl in _allTemplates)
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
                            var fn = new TreeNode(part) { Tag = null };
                            parentNode.Nodes.Add(fn);
                            categoryTree[currentPath] = fn;
                        }
                        parentNode = categoryTree[currentPath];
                    }
                    parentNode.Nodes.Add(new TreeNode(tpl.TemplateName) { Tag = tpl });
                }

                rootNode.Expand();
                trvTemplates.EndUpdate();
            }
            catch (Exception ex) { Logger.Error("刷新模板树失败: " + ex.Message); }
        }

        // ═════════════════════════════════════════════════════════════
        //  选中 → 显示详情
        // ═════════════════════════════════════════════════════════════

        private void TrvTemplates_AfterSelect(object sender, TreeViewEventArgs e)
        {
            _selectedTemplate = e.Node?.Tag as DeviceTemplate;
            if (_selectedTemplate != null) ShowTemplateDetail(_selectedTemplate);
            else ClearDetail();
        }

        private void ShowTemplateDetail(DeviceTemplate tpl)
        {
            lblDetailName.Text = tpl.TemplateName ?? "--";
            lblDetailSourceDevice.Text = "来源设备  " + (tpl.CreatedFromDevice ?? "--") +
                (string.IsNullOrEmpty(tpl.CreatedFromDriver) ? "" : "  [" + tpl.CreatedFromDriver + "]");
            lblDetailCreatedAt.Text = "创建时间  " + tpl.CreatedAt.ToString("yyyy-MM-dd HH:mm");
            lblDetailCategory.Text = "分类  " + (tpl.Category ?? "未分类");
            lblDetailVersion.Text = "版本  " + (tpl.Version ?? "1.0") + "    模板ID  " + (tpl.TemplateId ?? "--");
            lblDetailUpdatedAt.Text = "更新时间  " + tpl.UpdatedAt.ToString("yyyy-MM-dd HH:mm");
            lblDetailDesc.Text = string.IsNullOrEmpty(tpl.Description) ? "" : tpl.Description;

            FillTab(0, tpl.VariablePatterns, vp => new[] {
                vp.TemplateVarName, vp.MatchRule ?? "精确", vp.DataType, vp.Unit, vp.TagCn
            });
            FillTab(1, tpl.SemanticRelations, sr => new[] {
                sr.SourceVar, sr.TargetVar, sr.RelationType, sr.TargetType, sr.Description
            });
            FillTab(2, tpl.FabricConfigs, fc => new[] {
                fc.Operator, fc.TargetVar,
                JsonConvert.SerializeObject(fc.Params ?? new Dictionary<string, object>()),
                fc.Enabled ? "✓ 启用" : "✗ 停用"
            });
            FillTab(3, tpl.EventRules, er => new[] {
                er.RuleName, er.Condition?.Type ?? "", er.Condition?.TargetVar ?? "",
                er.Condition?.Trigger ?? "", (er.Actions?.Count ?? 0) + " 项"
            });
            FillTab(4, tpl.CleaningStrategies, cs => new[] {
                cs.TargetVar, cs.Strategy,
                JsonConvert.SerializeObject(cs.Params ?? new Dictionary<string, object>()),
                cs.Enabled ? "✓ 启用" : "✗ 停用"
            });

            for (int i = 0; i < _tabContent.TabPages.Count; i++)
            {
                var lv = _tabContent.TabPages[i].Controls[0] as ListView;
                _tabContent.TabPages[i].Text = TabTitles[i] + (lv != null && lv.Items.Count > 0 ? $"  ({lv.Items.Count})" : "");
            }
        }

        private void FillTab<T>(int idx, List<T> items, Func<T, string[]> arr)
        {
            if (items == null || idx >= _tabContent.TabPages.Count) return;
            var lv = _tabContent.TabPages[idx].Controls[0] as ListView;
            if (lv == null) return;
            lv.BeginUpdate();
            lv.Items.Clear();
            foreach (var it in items)
            {
                var row = arr(it);
                var lvi = new ListViewItem(row[0] ?? "");
                for (int c = 1; c < row.Length; c++)
                    lvi.SubItems.Add(row[c] ?? "");
                lv.Items.Add(lvi);
            }
            lv.EndUpdate();
        }

        private void ClearDetail()
        {
            lblDetailName.Text = "(未选择模板)";
            lblDetailSourceDevice.Text = "来源设备  --";
            lblDetailCreatedAt.Text = "创建时间  --";
            lblDetailCategory.Text = "分类  --";
            lblDetailVersion.Text = "版本  --";
            lblDetailUpdatedAt.Text = "更新时间  --";
            lblDetailDesc.Text = "";
            for (int i = 0; i < _tabContent.TabPages.Count; i++)
            {
                var lv = _tabContent.TabPages[i].Controls[0] as ListView;
                lv?.Items.Clear();
                _tabContent.TabPages[i].Text = TabTitles[i];
            }
        }

        // ═════════════════════════════════════════════════════════════
        //  按钮
        // ═════════════════════════════════════════════════════════════

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_selectedTemplate == null)
            {
                MessageBox.Show("请先在左侧树中选择一个模板", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(string.Format("确定要删除模板 \"{0}\" 吗？", _selectedTemplate.TemplateName),
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    var name = _selectedTemplate.TemplateName;
                    TemplateService.Instance.Delete(_selectedTemplate.TemplateId);
                    _selectedTemplate = null;
                    RefreshTree();
                    ClearDetail();
                    Logger.Info("用户已删除模板: " + name);
                }
                catch (Exception ex)
                {
                    Logger.Error("删除模板失败: " + ex.Message);
                    MessageBox.Show("删除模板失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            string search = (txtSearch.Text ?? "").Trim().ToLower();
            if (search == "搜索模板...") return;
            trvTemplates.BeginUpdate();
            FilterTreeNodes(trvTemplates.Nodes, search);
            trvTemplates.EndUpdate();
        }

        private void FilterTreeNodes(TreeNodeCollection nodes, string search)
        {
            foreach (TreeNode node in nodes)
            {
                bool isTemplate = node.Tag is DeviceTemplate;
                bool matches = search.Length == 0 || node.Text.ToLower().Contains(search);
                if (isTemplate)
                {
                    string fullPath = GetNodeFullPath(node).ToLower();
                    matches = matches || fullPath.Contains(search);
                    node.ForeColor = matches ? SystemColors.WindowText : Color.Gray;
                    node.BackColor = search.Length > 0 && !matches ? Color.LightGray : Color.White;
                }
                else FilterTreeNodes(node.Nodes, search);
            }
        }

        private string GetNodeFullPath(TreeNode node)
        {
            var parts = new List<string>();
            var current = node;
            while (current != null) { if (current.Parent != null) parts.Insert(0, current.Text); current = current.Parent; }
            return string.Join("/", parts);
        }
    }
}
