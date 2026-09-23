using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;
using Newtonsoft.Json;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// 语义管理窗口 v2 — 基于统一 SemanticNode 模型的灵活层级树管理
    /// 左侧: 语义树 (Company/Division/Factory/Workshop/Zone/ProductionLine/WorkStation/Equipment/Variable...)
    /// 右侧: TabControl (节点信息 | 节点关系 | 节点事件)
    /// Variable 节点有专用面板: 变量信息 | 变量关系 | 变量事件
    /// </summary>
    public class SemanticManagementForm : Form
    {
        #region 样式常量

        private static readonly Color AccentColor = Color.FromArgb(0, 122, 204);
        private static readonly Color SaveColor = Color.FromArgb(64, 158, 255);
        private static readonly Color DangerColor = Color.FromArgb(245, 108, 108);
        private static readonly Color TextColor = Color.FromArgb(33, 33, 33);
        private static readonly Color GrayText = Color.FromArgb(160, 160, 160);
        private static readonly Color OnlineColor = Color.FromArgb(46, 204, 113);
        private static readonly Color OfflineColor = Color.FromArgb(150, 150, 150);
        private static readonly Color StoppedColor = Color.FromArgb(243, 156, 18);
        private static readonly Color DeletedColor = Color.FromArgb(231, 76, 60);
        private static readonly Color HighlightColor = Color.FromArgb(255, 255, 180);
        private static readonly Color RowAlarmColor = Color.FromArgb(255, 245, 238);
        private static readonly Color RowFaultColor = Color.FromArgb(255, 235, 238);
        private static readonly Color RowRecoverColor = Color.FromArgb(232, 245, 233);
        private static readonly Color RowMaintenanceColor = Color.FromArgb(227, 242, 253);
        private static readonly Color RowCommColor = Color.FromArgb(255, 243, 224);
        private readonly Font UiFont = new Font("Microsoft YaHei", 9f);

        #endregion

        #region 控件

        // 菜单栏
        private MenuStrip _menuStrip;

        // SplitContainer
        private SplitContainer _split;

        // ── 左侧面板 ──
        private Panel _leftPanel;
        private TextBox _txtSearch;
        // v2.6.0: 工具栏按钮已移除（语义维护统一走数采页面）
        // private Button _btnAddFolder;
        // private Button _btnAddNode;
        // private Button _btnAddEquipment;
        // private Button _btnAddVariable;
        // private Button _btnDeleteNode;
        private TreeView _tree;
        private ContextMenuStrip _treeMenu;

        // ── 右侧面板 ──
        private TabControl _tabControl;
        private TabPage _tabInfo;
        private TabPage _tabRelations;
        private TabPage _tabEvents;

        // Tab: 节点信息（通用 + Variable 扩展 + Equipment 扩展）
        private TextBox _txtNodeName;
        private TextBox _txtNodeCode;
        private ComboBox _cmbNodeKind;
        private ComboBox _cmbNodeStatus;
        private Label _lblNodeSourceType;
        private Label _lblNodeSourceId;
        private TextBox _txtNodeDesc;
        private Button _btnSaveInfo;
        private Label _lblInfoHint;

        // Variable 扩展
        private Panel _pnlVarExt;
        private Label _lblVarParentEquip;
        private ComboBox _cmbVarRole;
        private ComboBox _cmbVarUnit;

        // Equipment 扩展
        private Panel _pnlEquipExt;
        private ComboBox _cmbEquipType;
        private Label _lblEquipDriverId;

        // Tab: 节点关系
        private DataGridView _gridRelations;
        private Button _btnAddRelation;
        private Button _btnDeleteRelation;
        private Panel _relToolbar;

        // Tab: 节点事件
        private Panel _eventToolbar;
        private DateTimePicker _dtpEventFrom;
        private DateTimePicker _dtpEventTo;
        private ComboBox _cmbEventType;
        private CheckBox _chkEventAll;
        private Button _btnAddEvent;
        private Button _btnRefreshEvents;
        private DataGridView _gridEvents;

        // Variable 专用 Tab: 变量关系
        private TabPage _tabVarRels;
        private DataGridView _gridVarRels;
        private Button _btnAddVarRel;
        private Button _btnDeleteVarRel;
        private Button _btnImpactAnalysis;
        private Panel _varRelToolbar;

        // 当前选中
        private SemanticNode _currentNode;
        private List<SemanticNode> _allNodesCache;
        private Dictionary<string, List<SemanticNode>> _childrenByParentCache;

        #endregion

        // ════════════════════════════════════════════════════════════════
        //  构造函数 & 初始化
        // ════════════════════════════════════════════════════════════════

        public SemanticManagementForm()
        {
            BackColor = Color.White;
            Font = UiFont;
            Text = "语义管理 — NeoIndustrial 工业语义层";
            var wa = Screen.PrimaryScreen.WorkingArea;
            Size = new Size((int)(wa.Width * 0.88), (int)(wa.Height * 0.92));
            MinimumSize = new Size(1024, 720);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            Icon = Program.AppIcon;

            BuildMenu();
            BuildLayout();
            WireEvents();
        }

        private static string L(string key)
        {
            try { return LanguageManager.Instance.GetString(key); }
            catch { return null; }
        }

        /// <summary>刷新所有 UI 文本（语言切换后调用）</summary>
        public void ApplyLanguage()
        {
            ApplyMenuLanguage();
            ApplyTreeMenuLanguage();
            ApplyTabLanguage();
            ApplyInfoPanelLanguage();
            ApplyButtonLanguage();
        }

        // ════════════════════════════════════════════════════════════════
        //  菜单栏
        // ════════════════════════════════════════════════════════════════

        private void BuildMenu()
        {
            _menuStrip = new MenuStrip { Font = UiFont, Dock = DockStyle.Top };

            var miImport = new ToolStripMenuItem("导入配置");
            var miImportJson = new ToolStripMenuItem("JSON 导入", null, (s, e) => ImportFromJson());
            var miImportCsv = new ToolStripMenuItem("CSV 导入", null, (s, e) => ImportFromCsv());
            miImport.DropDownItems.AddRange(new ToolStripItem[] { miImportJson, miImportCsv });

            var miExport = new ToolStripMenuItem("导出配置");
            var miExportJson = new ToolStripMenuItem("JSON 导出", null, (s, e) => ExportToJson());
            var miExportCsv = new ToolStripMenuItem("CSV 导出", null, (s, e) => ExportToCsv());
            miExport.DropDownItems.AddRange(new ToolStripItem[] { miExportJson, miExportCsv });

            var miRebuild = new ToolStripMenuItem("🔄 更新语义树", null, (s, e) => RebuildTree());

            _menuStrip.Items.AddRange(new ToolStripItem[] { miImport, miExport, miRebuild });
            MainMenuStrip = _menuStrip;
            Controls.Add(_menuStrip);
        }

        // ════════════════════════════════════════════════════════════════
        //  布局
        // ════════════════════════════════════════════════════════════════

        private void BuildLayout()
        {
            // 不用 Dock.Fill 避免与 MenuStrip 的 z-order 冲突
            // 手动计算位置：MenuStrip 下方到窗口底部
            _split = new SplitContainer
            {
                Location = new Point(0, 24),
                Size = new Size(ClientSize.Width, ClientSize.Height - 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Panel1MinSize = 240
            };

            // 左侧面板
            _leftPanel = new Panel { Dock = DockStyle.Fill };
            BuildLeftPanel();
            _split.Panel1.Controls.Add(_leftPanel);

            // 右侧面板
            BuildRightPanel();
            _split.Panel2.Controls.Add(_tabControl);

            Controls.Add(_split);
        }

        // ── 左侧面板 ──

        private void BuildLeftPanel()
        {
            // 搜索框
            _txtSearch = new TextBox
            {
                Location = new Point(8, 8),
                Size = new Size(260, 26),
                Font = UiFont,
                ForeColor = GrayText,
                Text = "搜索节点...",
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            _txtSearch.Enter += (s, e) =>
            {
                if (_txtSearch.Text == "搜索节点...")
                {
                    _txtSearch.Text = "";
                    _txtSearch.ForeColor = TextColor;
                }
            };
            _txtSearch.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _txtSearch.Text = "搜索节点...";
                    _txtSearch.ForeColor = GrayText;
                }
            };
            _txtSearch.TextChanged += (s, e) => FilterTree();
            _leftPanel.Controls.Add(_txtSearch);

            // TreeView — 工具栏已移除(v2.6.0: 语义维护统一走数采页面)
            _tree = new TreeView
            {
                Location = new Point(8, 42),
                Size = new Size(260, 530),
                Font = new Font("Microsoft YaHei", 9.5f),
                ItemHeight = 26,
                HideSelection = false,
                FullRowSelect = true,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                LabelEdit = true
            };

            // 右键菜单 — v2.6.0: 仅保留更新语义树
            _treeMenu = new ContextMenuStrip();
            var miRefresh = new ToolStripMenuItem("更新语义树", null, (s, e) => RebuildTree());
            _treeMenu.Items.Add(miRefresh);
            _tree.ContextMenuStrip = _treeMenu;

            _leftPanel.Controls.Add(_tree);
        }

        private Button NewToolBtn(string text, int x, int y, int w)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, 28),
                Font = new Font("Microsoft YaHei", 8.5f),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = TextColor,
                UseVisualStyleBackColor = true
            };
        }

        // ── 右侧面板 ──

        private void BuildRightPanel()
        {
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = UiFont
            };

            _tabInfo = new TabPage("📋 节点信息");
            BuildInfoTab();
            _tabRelations = new TabPage("🔗 节点关系");
            BuildRelationsTab();
            _tabEvents = new TabPage("📅 节点事件");
            BuildEventsTab();
            _tabVarRels = new TabPage("🔗 变量关系");
            BuildVarRelsTab();

            _tabControl.TabPages.AddRange(new[] { _tabInfo, _tabRelations, _tabEvents });

            _tabControl.SelectedIndexChanged += (s, e) => RefreshActiveTab();
        }

        // ════════════════════════════════════════════════════════════════
        //  Tab 1: 节点信息
        // ════════════════════════════════════════════════════════════════

        private void BuildInfoTab()
        {
            _tabInfo.Padding = new Padding(16);

            int y = 16;
            int lblW = 80, inputW = 400, rowH = 26, gap = 12;

            // 名称
            AddFormLabel(_tabInfo, "名称:", 16, y + 3, lblW);
            _txtNodeName = new TextBox
            {
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(inputW, rowH),
                Font = UiFont
            };
            _tabInfo.Controls.Add(_txtNodeName);
            y += rowH + gap;

            // 编码（只读）
            AddFormLabel(_tabInfo, "编码:", 16, y + 3, lblW);
            _txtNodeCode = new TextBox
            {
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(inputW, rowH),
                Font = UiFont,
                ReadOnly = true,
                BackColor = Color.FromArgb(245, 245, 245)
            };
            _tabInfo.Controls.Add(_txtNodeCode);
            y += rowH + gap;

            // 节点类型 — 可编辑下拉框
            AddFormLabel(_tabInfo, "节点类型:", 16, y + 3, lblW);
            _cmbNodeKind = new ComboBox
            {
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(inputW, rowH),
                Font = UiFont,
                DropDownStyle = ComboBoxStyle.DropDown
            };
            // 填充所有 13 种类型（中文显示）
            string[] kinds = { "Company", "Division", "Factory", "Workshop", "Zone",
                "ProductionLine", "WorkStation", "Equipment", "Variable",
                "Datasource", "DataTable", "DataField", "Custom" };
            foreach (var k in kinds)
                _cmbNodeKind.Items.Add(NodeKind.GetDisplayName(k));
            _tabInfo.Controls.Add(_cmbNodeKind);
            y += rowH + gap;

            // 状态
            AddFormLabel(_tabInfo, "状态:", 16, y + 3, lblW);
            _cmbNodeStatus = new ComboBox
            {
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(140, rowH),
                Font = UiFont,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbNodeStatus.Items.Add("在线");
            _cmbNodeStatus.Items.Add("离线");
            _cmbNodeStatus.Items.Add("停用");
            _cmbNodeStatus.Items.Add("已删除");
            _cmbNodeStatus.SelectedIndex = 0;
            _tabInfo.Controls.Add(_cmbNodeStatus);
            y += rowH + gap;

            // 来源类型
            AddFormLabel(_tabInfo, "来源类型:", 16, y + 3, lblW);
            _lblNodeSourceType = new Label
            {
                Location = new Point(16 + lblW + 5, y + 3),
                Size = new Size(inputW, 18),
                Font = UiFont,
                ForeColor = GrayText,
                Text = "-"
            };
            _tabInfo.Controls.Add(_lblNodeSourceType);
            y += rowH + gap;

            // 来源ID
            AddFormLabel(_tabInfo, "来源ID:", 16, y + 3, lblW);
            _lblNodeSourceId = new Label
            {
                Location = new Point(16 + lblW + 5, y + 3),
                Size = new Size(inputW, 18),
                Font = new Font("Consolas", 8.5f),
                ForeColor = GrayText,
                Text = "-"
            };
            _tabInfo.Controls.Add(_lblNodeSourceId);
            y += rowH + gap;

            // 描述
            AddFormLabel(_tabInfo, "描述:", 16, y + 3, lblW);
            _txtNodeDesc = new TextBox
            {
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(inputW, 60),
                Font = UiFont,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            _tabInfo.Controls.Add(_txtNodeDesc);
            y += 72;

            // ── Variable 扩展面板 ──
            _pnlVarExt = new Panel
            {
                Location = new Point(16, y),
                Size = new Size(520, 100),
                Visible = false
            };

            int vy = 0;
            // 关联设备名
            AddFormLabel(_pnlVarExt, "关联设备:", 0, vy + 3, lblW);
            _lblVarParentEquip = new Label
            {
                Location = new Point(lblW + 5, vy + 3),
                Size = new Size(inputW, 18),
                Font = UiFont,
                ForeColor = AccentColor,
                Text = "-"
            };
            _pnlVarExt.Controls.Add(_lblVarParentEquip);
            vy += rowH + gap;

            // 变量角色
            AddFormLabel(_pnlVarExt, "变量角色:", 0, vy + 3, lblW);
            _cmbVarRole = new ComboBox
            {
                Location = new Point(lblW + 5, vy),
                Size = new Size(180, rowH),
                Font = UiFont,
                DropDownStyle = ComboBoxStyle.DropDown
            };
            _cmbVarRole.Items.AddRange(new[] { "温度", "压力", "流量", "液位", "电流", "电压", "功率", "频率", "转速", "速度", "振动", "湿度", "能耗", "状态", "报警", "自定义" });
            _pnlVarExt.Controls.Add(_cmbVarRole);
            vy += rowH + gap;

            // 单位
            AddFormLabel(_pnlVarExt, "单位:", 0, vy + 3, lblW);
            _cmbVarUnit = new ComboBox
            {
                Location = new Point(lblW + 5, vy),
                Size = new Size(120, rowH),
                Font = UiFont,
                DropDownStyle = ComboBoxStyle.DropDown
            };
            _cmbVarUnit.Items.AddRange(new[] { "℃", "MPa", "kPa", "Pa", "L/min", "m³/h", "A", "V", "kW", "Hz", "RPM", "mm/s", "%RH", "kWh" });
            _pnlVarExt.Controls.Add(_cmbVarUnit);

            _tabInfo.Controls.Add(_pnlVarExt);
            y += _pnlVarExt.Height;

            // ── Equipment 扩展面板 ──
            _pnlEquipExt = new Panel
            {
                Location = new Point(16, y),
                Size = new Size(520, 80),
                Visible = false
            };

            int ey = 0;
            AddFormLabel(_pnlEquipExt, "设备类型:", 0, ey + 3, lblW);
            _cmbEquipType = new ComboBox
            {
                Location = new Point(lblW + 5, ey),
                Size = new Size(220, rowH),
                Font = UiFont,
                DropDownStyle = ComboBoxStyle.DropDown
            };
            _cmbEquipType.Items.AddRange(new[] { "挤压机", "注塑机", "冲压机", "锻压机", "CNC加工中心", "机器人", "传送带", "加热炉", "冷却塔", "压缩机", "泵", "风机", "变压器", "配电柜", "PLC控制柜", "传感器节点", "执行器", "其他" });
            _pnlEquipExt.Controls.Add(_cmbEquipType);
            ey += rowH + gap;

            AddFormLabel(_pnlEquipExt, "关联驱动:", 0, ey + 3, lblW);
            _lblEquipDriverId = new Label
            {
                Location = new Point(lblW + 5, ey + 3),
                Size = new Size(inputW, 18),
                Font = new Font("Consolas", 8.5f),
                ForeColor = GrayText,
                Text = "-"
            };
            _pnlEquipExt.Controls.Add(_lblEquipDriverId);

            _tabInfo.Controls.Add(_pnlEquipExt);
            y += _pnlEquipExt.Height + gap + 6;

            // 保存按钮
            _btnSaveInfo = new Button
            {
                Text = "保存",
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(90, 32),
                Font = UiFont,
                BackColor = SaveColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            _btnSaveInfo.Click += BtnSaveInfo_Click;
            _tabInfo.Controls.Add(_btnSaveInfo);

            _lblInfoHint = new Label
            {
                Location = new Point(16 + lblW + 5 + 100, y + 8),
                Size = new Size(300, 18),
                Font = new Font("Microsoft YaHei", 8f),
                ForeColor = Color.FromArgb(100, 100, 100)
            };
            _tabInfo.Controls.Add(_lblInfoHint);
        }

        private void AddFormLabel(Control parent, string text, int x, int y, int w)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, 18),
                Font = UiFont,
                TextAlign = ContentAlignment.MiddleRight
            });
        }

        // ════════════════════════════════════════════════════════════════
        //  Tab 2: 节点关系
        // ════════════════════════════════════════════════════════════════

        private void BuildRelationsTab()
        {
            _tabRelations.Padding = new Padding(12);

            _relToolbar = new Panel { Location = new Point(12, 12), Size = new Size(700, 34) };

            _btnAddRelation = new Button
            {
                Text = "+ 添加关系",
                Location = new Point(0, 4),
                Size = new Size(100, 28),
                Font = UiFont,
                BackColor = AccentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            _btnAddRelation.Click += BtnAddRelation_Click;

            _btnDeleteRelation = new Button
            {
                Text = "✖ 删除选中",
                Location = new Point(108, 4),
                Size = new Size(90, 28),
                Font = UiFont,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(245, 245, 245),
                ForeColor = DangerColor,
                UseVisualStyleBackColor = false
            };
            _btnDeleteRelation.Click += (s, e) => DeleteSelectedRelation();

            _relToolbar.Controls.Add(_btnAddRelation);
            _relToolbar.Controls.Add(_btnDeleteRelation);

            _gridRelations = new DataGridView
            {
                Location = new Point(12, 52),
                Size = new Size(750, 460),
                Font = UiFont,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowTemplate = { Height = 26 }
            };

            _gridRelations.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { HeaderText = "源节点", Name = "Source", FillWeight = 22 },
                new DataGridViewTextBoxColumn { HeaderText = "关系类型", Name = "Type", FillWeight = 16 },
                new DataGridViewTextBoxColumn { HeaderText = "目标节点", Name = "Target", FillWeight = 22 },
                new DataGridViewTextBoxColumn { HeaderText = "方向", Name = "Direction", FillWeight = 8 },
                new DataGridViewTextBoxColumn { HeaderText = "描述", Name = "Desc", FillWeight = 22 },
                new DataGridViewButtonColumn { HeaderText = "", Name = "DeleteBtn", Text = "✖", FillWeight = 10 }
            });
            _gridRelations.CellClick += GridRelations_CellClick;

            _tabRelations.Controls.Add(_relToolbar);
            _tabRelations.Controls.Add(_gridRelations);
        }

        // ════════════════════════════════════════════════════════════════
        //  Tab 3: 节点事件
        // ════════════════════════════════════════════════════════════════

        private void BuildEventsTab()
        {
            _tabEvents.Padding = new Padding(8);

            // ── 上部: 工具栏 + 事件表格（先加 Fill，再加 Bottom）──
            var contentPanel = new Panel { Dock = DockStyle.Fill };

            _eventToolbar = new Panel
            {
                Location = new Point(8, 8),
                Size = new Size(780, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            int tx = 0;
            var lblFrom = new Label { Text = "从:", Location = new Point(tx, 8), Size = new Size(24, 20), Font = UiFont };
            _dtpEventFrom = new DateTimePicker
            {
                Location = new Point(tx + 26, 6),
                Size = new Size(130, 24),
                Font = UiFont,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd HH:mm"
            };
            _dtpEventFrom.Value = DateTime.Now.AddDays(-7);
            tx += 162;

            var lblTo = new Label { Text = "到:", Location = new Point(tx, 8), Size = new Size(24, 20), Font = UiFont };
            _dtpEventTo = new DateTimePicker
            {
                Location = new Point(tx + 26, 6),
                Size = new Size(130, 24),
                Font = UiFont,
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd HH:mm"
            };
            tx += 162;

            var lblType = new Label { Text = "类型:", Location = new Point(tx, 8), Size = new Size(36, 20), Font = UiFont };
            _cmbEventType = new ComboBox
            {
                Location = new Point(tx + 38, 6),
                Size = new Size(120, 24),
                Font = UiFont,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbEventType.Items.Add("全部");
            foreach (var et in SemanticEventType.AllTypes)
                _cmbEventType.Items.Add(et);
            _cmbEventType.SelectedIndex = 0;
            tx += 165;

            _chkEventAll = new CheckBox
            {
                Text = "所有节点",
                Location = new Point(tx, 8),
                Size = new Size(80, 20),
                Font = UiFont
            };
            tx += 88;

            _btnAddEvent = new Button
            {
                Text = "+ 添加事件",
                Location = new Point(tx, 4),
                Size = new Size(90, 28),
                Font = UiFont,
                BackColor = AccentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            _btnAddEvent.Click += BtnAddEvent_Click;
            tx += 98;

            _btnRefreshEvents = new Button
            {
                Text = "🔄 刷新",
                Location = new Point(tx, 4),
                Size = new Size(70, 28),
                Font = UiFont,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(245, 245, 245),
                UseVisualStyleBackColor = false
            };
            _btnRefreshEvents.Click += (s, e) => LoadEvents();

            _eventToolbar.Controls.AddRange(new Control[] { lblFrom, _dtpEventFrom, lblTo, _dtpEventTo, lblType, _cmbEventType, _chkEventAll, _btnAddEvent, _btnRefreshEvents });

            _gridEvents = new DataGridView
            {
                Location = new Point(8, 50),
                Font = UiFont,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowTemplate = { Height = 26 }
            };

            _gridEvents.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { HeaderText = "时间", Name = "Time", FillWeight = 16 },
                new DataGridViewTextBoxColumn { HeaderText = "事件类型", Name = "Type", FillWeight = 14 },
                new DataGridViewTextBoxColumn { HeaderText = "处理方式", Name = "Method", FillWeight = 12 },
                new DataGridViewTextBoxColumn { HeaderText = "描述", Name = "Desc", FillWeight = 48 },
                new DataGridViewButtonColumn { HeaderText = "", Name = "DeleteBtn", Text = "✖", FillWeight = 10 }
            });
            _gridEvents.RowPrePaint += GridEvents_RowPrePaint;

            contentPanel.Controls.Add(_eventToolbar);
            contentPanel.Controls.Add(_gridEvents);

            // 随面板尺寸变化动态调整表格宽高，四边各留间距
            contentPanel.SizeChanged += (s, e) =>
            {
                _gridEvents.Width = Math.Max(200, contentPanel.Width - 24);
                _gridEvents.Height = Math.Max(60, contentPanel.Height - 62);
            };

            _tabEvents.Controls.Add(contentPanel);

            // ── 底部: 事件类型说明与示例 ──
            var examplePanel = BuildEventExamplePanel();
            examplePanel.Dock = DockStyle.Bottom;
            _tabEvents.Controls.Add(examplePanel);
        }

        /// <summary>构建事件类型说明示例面板</summary>
        private Panel BuildEventExamplePanel()
        {
            var panel = new Panel
            {
                Height = 310,
                BackColor = Color.FromArgb(250, 250, 252),
                BorderStyle = BorderStyle.None
            };

            var lblTitle = new Label
            {
                Text = "📖  事件类型说明与示例",
                Location = new Point(12, 6),
                Size = new Size(300, 22),
                Font = new Font("Microsoft YaHei", 10f, FontStyle.Bold),
                ForeColor = TextColor
            };
            panel.Controls.Add(lblTitle);

            var grid = new TableLayoutPanel
            {
                Location = new Point(8, 30),
                Size = new Size(200, 272),
                ColumnCount = 2,
                RowCount = 2,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            var g1 = BuildExampleGroup("通用事件", new[] {
                ("启动", "设备/服务启动", "PLC控制器通电启动", "—"),
                ("停止", "设备/服务停止", "采集服务正常停止", "—"),
                ("维护保养", "定期维护", "月度校准、季度检修", "—"),
                ("参数变更", "配置修改", "报警阈值从 80 → 85", "—"),
                ("通讯中断", "连接断开", "Modbus TCP 连接超时", "—"),
                ("通讯恢复", "连接恢复", "链路重新建立成功", "—"),
                ("故障", "设备/服务故障", "传感器掉线、驱动异常", "—"),
            });

            var g2 = BuildExampleGroup("变量运行事件", new[] {
                ("超上限", "超过上限阈值", "温度 85.3℃ > 80.0℃ 上限", "—"),
                ("超下限", "低于下限阈值", "压力 0.08MPa < 0.10MPa 下限", "—"),
                ("超高高限(HH)", "严重超限", "液位超 95% 高高限", "—"),
                ("超低低限(LL)", "严重低限", "电压低于 18V 低低限", "—"),
                ("偏离目标值", "偏离设定值", "转速 ±15% 超出公差", "—"),
                ("质量异常", "品质不符合", "厚度公差超 CPK 1.33", "—"),
            });

            var g3 = BuildExampleGroup("系统诊断事件", new[] {
                ("采集异常", "数据采集失败", "OPC UA 节点读取超时", "—"),
                ("数据丢失", "数据点缺失", "连续 3 周期无数据返回", "—"),
                ("冻结变化", "数值长时间不变", "流量计≥5分钟未更新", "—"),
                ("计算失败", "表达式/公式错误", "功率公式除零异常", "—"),
            });

            var g4 = BuildExampleGroup("人工介入 · 处理方式", new[] {
                ("人工确认", "操作人员确认", "报警确认后进入处置流程", "—"),
                ("人工处置", "手动干预处理", "现场更换传感器后复位", "—"),
                ("处理方式参考", "", "——————————————", ""),
                ("仅记录 → 报警 → 消息通知", "", "站内消息 → 邮件 → 短信", ""),
                ("Webhook → 调用API", "", "触发工作流 → 生成工单", ""),
                ("触发MCP → 触发AI分析", "", "智能联动分析", ""),
            });

            grid.Controls.Add(g1, 0, 0);
            grid.Controls.Add(g2, 1, 0);
            grid.Controls.Add(g3, 0, 1);
            grid.Controls.Add(g4, 1, 1);

            panel.Controls.Add(grid);

            panel.Resize += (s, e) =>
            {
                grid.Width = panel.Width - 16;
                grid.Height = panel.Height - 34;
            };

            return panel;
        }

        // ════════════════════════════════════════════════════════════════
        //  Variable 专用 Tab: 变量关系
        // ════════════════════════════════════════════════════════════════

        private void BuildVarRelsTab()
        {
            _tabVarRels.Padding = new Padding(8);

            // ── 上部: 工具栏 + 关系表格（先加 Fill，再加 Bottom，避免遮挡）──
            var contentPanel = new Panel { Dock = DockStyle.Fill };

            _varRelToolbar = new Panel
            {
                Location = new Point(8, 8),
                Size = new Size(300, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            _btnAddVarRel = new Button
            {
                Text = "+ 添加关系",
                Location = new Point(0, 4),
                Size = new Size(100, 28),
                Font = UiFont,
                BackColor = AccentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            _btnAddVarRel.Click += BtnAddVarRel_Click;

            _btnDeleteVarRel = new Button
            {
                Text = "✖ 删除",
                Location = new Point(106, 4),
                Size = new Size(70, 28),
                Font = UiFont,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(245, 245, 245),
                ForeColor = DangerColor,
                UseVisualStyleBackColor = false
            };
            _btnDeleteVarRel.Click += (s, e) => DeleteSelectedVarRel();

            _btnImpactAnalysis = new Button
            {
                Text = "🔍 影响分析",
                Location = new Point(182, 4),
                Size = new Size(110, 28),
                Font = UiFont,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 152, 0),
                ForeColor = Color.White,
                UseVisualStyleBackColor = false
            };
            _btnImpactAnalysis.Click += (s, e) => ShowImpactAnalysis();

            _varRelToolbar.Controls.Add(_btnAddVarRel);
            _varRelToolbar.Controls.Add(_btnDeleteVarRel);
            _varRelToolbar.Controls.Add(_btnImpactAnalysis);

            _gridVarRels = new DataGridView
            {
                Location = new Point(8, 50),
                Font = UiFont,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowTemplate = { Height = 26 }
            };
            _gridVarRels.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var row = _gridVarRels.Rows[e.RowIndex];
                if (row.Tag is SemanticVariableRelation existing)
                {
                    using (var dlg = new VariableRelationEditDialog(_currentNode.Id, _allNodesCache, existing))
                    {
                        if (dlg.ShowDialog() == DialogResult.OK && dlg.Result != null)
                        {
                            SemanticService.Instance.SaveVariableRelation(dlg.Result);
                            Logger.Info(string.Format("[SemanticForm] 编辑变量关系: {0} → {1}",
                                dlg.Result.RelationType, dlg.Result.TargetTableName));
                            LoadVarRelations();
                        }
                    }
                }
            };

            _gridVarRels.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { HeaderText = "关系类型", Name = "RType", FillWeight = 13 },
                new DataGridViewTextBoxColumn { HeaderText = "目标类型", Name = "TType", FillWeight = 11 },
                new DataGridViewTextBoxColumn { HeaderText = "数据源 / 表 / 字段", Name = "Target", FillWeight = 24 },
                new DataGridViewTextBoxColumn { HeaderText = "常量值 / 表达式", Name = "Value", FillWeight = 18 },
                new DataGridViewTextBoxColumn { HeaderText = "单位", Name = "Unit", FillWeight = 7 },
                new DataGridViewTextBoxColumn { HeaderText = "条件变量", Name = "CondVars", FillWeight = 14 },
                new DataGridViewTextBoxColumn { HeaderText = "描述", Name = "Desc", FillWeight = 13 }
            });

            contentPanel.Controls.Add(_varRelToolbar);
            contentPanel.Controls.Add(_gridVarRels);

            // 随面板尺寸变化动态调整表格宽高，四边各留间距
            contentPanel.SizeChanged += (s, e) =>
            {
                _gridVarRels.Width = Math.Max(200, contentPanel.Width - 24);
                _gridVarRels.Height = Math.Max(60, contentPanel.Height - 62);
            };

            _tabVarRels.Controls.Add(contentPanel);

            // ── 底部: 关系类型示例说明（后加 Dock.Bottom，在 Fill 之后不重叠）──
            var examplePanel = BuildVarRelExamplePanel();
            examplePanel.Dock = DockStyle.Bottom;
            _tabVarRels.Controls.Add(examplePanel);
        }

        /// <summary>构建变量关系类型示例说明面板</summary>
        private Panel BuildVarRelExamplePanel()
        {
            var panel = new Panel
            {
                Height = 490,
                BackColor = Color.FromArgb(250, 250, 252),
                BorderStyle = BorderStyle.None
            };

            // 标题
            var lblTitle = new Label
            {
                Text = "📖  关系类型说明与示例",
                Location = new Point(12, 6),
                Size = new Size(300, 22),
                Font = new Font("Microsoft YaHei", 10f, FontStyle.Bold),
                ForeColor = TextColor
            };
            panel.Controls.Add(lblTitle);

            // 上半部分 2×2 布局 — 4 组业务关系
            var grid = new TableLayoutPanel
            {
                Location = new Point(8, 30),
                Size = new Size(200, 290),
                ColumnCount = 2,
                RowCount = 2,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            var g1 = BuildExampleGroup("数据边界与阈值", new[] {
                ("上限", "数据源字段", "温度上限 ← quality_limits.temp_max", "℃"),
                ("下限", "数据源字段", "压力下限 ← quality_limits.pressure_min", "MPa"),
                ("目标值", "常量", "目标转速 = 1500", "RPM"),
                ("标准值", "常量", "标准温度 = 25.0", "℃"),
            });

            var g2 = BuildExampleGroup("工艺与质量标准", new[] {
                ("SOP步骤", "表达式", "升温阶段: step == 3 ? 200 : 150", "℃"),
                ("SIP要求", "数据源字段", "洁净度等级 ← process.clean_level", "级"),
                ("质量判定", "表达式", "合格: value >= lower && value <= upper", "—"),
                ("报警阈值", "常量", "高温报警线 = 80.0", "℃"),
            });

            var g3 = BuildExampleGroup("计算与业务关联", new[] {
                ("补偿系数", "常量", "温度补偿系数 = 0.00385", "—"),
                ("计算公式", "表达式", "功率 = current * voltage * 0.85", "kW"),
                ("参考变量", "变量", "参考温度 → 环境温度传感器", "℃"),
                ("业务关联", "变量", "关联产线 → 产线A总产量", "件"),
            });

            var g4 = BuildExampleGroup("语义图谱关系（变量 → 变量）", new[] {
                ("影响", "变量", "温度传感器 ↑ → 压力传感器 ↑", "—"),
                ("被约束", "变量", "被安全限值变量约束", "—"),
                ("计算来源", "变量", "功率由电流 × 电压推导", "kW"),
                ("关联设备", "变量", "关联配套设备振动状态", "—"),
            });

            grid.Controls.Add(g1, 0, 0);
            grid.Controls.Add(g2, 1, 0);
            grid.Controls.Add(g3, 0, 1);
            grid.Controls.Add(g4, 1, 1);

            panel.Controls.Add(grid);

            // 下半部分 — 历史数据追溯独立区
            var gHistory = BuildExampleGroup("🔍 历史数据追溯（变量 → 数据表）", new[] {
                ("历史数据源", "数据源字段", "温度传感器#1 历史值 ← MySQL/industrial_data.value", "—"),
                ("历史数据源", "数据源字段", "压力传感器 历史值 ← TDengine/sensor_data.p_val", "MPa"),
                ("历史数据源", "数据源字段", "能耗历史 ← SQLite/energy_log.kwh", "kWh"),
                ("", "", "AI Agent 可通过此关系定位任意变量的历史数据来源", ""),
            });
            gHistory.Location = new Point(8, 330);
            gHistory.Size = new Size(200, 138);
            gHistory.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            panel.Controls.Add(gHistory);

            // Resize 时同步 grid 与历史面板宽度
            panel.Resize += (s, e) =>
            {
                grid.Width = panel.Width - 16;
                gHistory.Width = panel.Width - 16;
            };

            return panel;
        }

        /// <summary>构建单个示例分组 GroupBox</summary>
        private GroupBox BuildExampleGroup(string title, (string rtype, string ttype, string example, string unit)[] items)
        {
            var gb = new GroupBox
            {
                Text = title,
                Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold),
                ForeColor = AccentColor,
                Padding = new Padding(6, 16, 6, 6),
                Dock = DockStyle.Fill,
                Margin = new Padding(4)
            };

            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 8.5f),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                ColumnHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                RowTemplate = { Height = 24 },
                DefaultCellStyle = { SelectionBackColor = Color.FromArgb(230, 244, 255), SelectionForeColor = TextColor }
            };

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "RType", FillWeight = 18 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "TType", FillWeight = 16 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Example", FillWeight = 48, DefaultCellStyle = { ForeColor = Color.FromArgb(80, 80, 80) } });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unit", FillWeight = 18, DefaultCellStyle = { ForeColor = GrayText, Alignment = DataGridViewContentAlignment.MiddleCenter } });

            foreach (var item in items)
            {
                int idx = dgv.Rows.Add(item.rtype, item.ttype, item.example, item.unit);
                // 目标类型列着色
                dgv.Rows[idx].Cells[0].Style.ForeColor = TextColor;
                dgv.Rows[idx].Cells[0].Style.Font = new Font("Microsoft YaHei", 8.5f, FontStyle.Bold);
                var ttypeColor = item.ttype == "数据源字段" ? Color.FromArgb(46, 139, 87) :
                                 item.ttype == "常量" ? Color.FromArgb(210, 120, 0) :
                                 item.ttype == "表达式" ? Color.FromArgb(128, 0, 128) :
                                 AccentColor;
                dgv.Rows[idx].Cells[1].Style.ForeColor = ttypeColor;
                dgv.Rows[idx].Cells[1].Style.Font = new Font("Microsoft YaHei", 8f);
            }

            gb.Controls.Add(dgv);
            return gb;
        }

        // ════════════════════════════════════════════════════════════════
        //  事件绑定 & 初始加载
        // ════════════════════════════════════════════════════════════════

        private void WireEvents()
        {
            _tree.AfterSelect += Tree_AfterSelect;
            _tree.AfterLabelEdit += Tree_AfterLabelEdit;
            _tree.BeforeExpand += Tree_BeforeExpand;

            // v2.6.0: 工具栏按钮已移除

            Shown += (s, e) =>
            {
                _split.SplitterDistance = ClientSize.Width / 4;
                UpdateSearchWidth();
                LoadTree();
            };

            // 订阅设备层级变更事件，主窗体修改设备树后自动重刷语义树（规则#60）
            ConfigService.DeviceHierarchyChanged += () =>
            {
                if (IsDisposed) return;
                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        RebuildTree();
                        Logger.Info("[SemanticForm] 设备树变更，语义树已自动刷新");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("[SemanticForm] 自动刷新失败: " + ex.Message);
                    }
                }));
            };
            Resize += (s, e) => UpdateSearchWidth();
            FormClosing += (s, e) => { /* stay */ };
        }

        private void UpdateSearchWidth()
        {
            _txtSearch.Width = _leftPanel.ClientSize.Width - 16;
            _tree.Width = _leftPanel.ClientSize.Width - 16;
            _tree.Height = _leftPanel.ClientSize.Height - _tree.Top - 8;
        }

        // ════════════════════════════════════════════════════════════════
        //  Tree 加载与刷新
        // ════════════════════════════════════════════════════════════════

        private void LoadTree()
        {
            _tree.Nodes.Clear();

            try
            {
                // 单次 SQL 加载全量节点（替代 N 次 GetChildren 的 BFS）
                var allNodes = SemanticService.Instance.GetAllNodes();
                _allNodesCache = allNodes;

                // 按 ParentId 分组用于懒加载判断
                var childrenByParent = allNodes.GroupBy(n => n.ParentId ?? "")
                    .ToDictionary(g => g.Key, g => g.ToList());
                _childrenByParentCache = childrenByParent;

                // 只构建根节点 + 占位子节点
                var roots = allNodes.Where(n => string.IsNullOrEmpty(n.ParentId))
                    .OrderBy(n => n.SortOrder).ThenBy(n => n.Name).ToList();
                foreach (var root in roots)
                {
                    var node = BuildTreeNode(root);
                    _tree.Nodes.Add(node);
                    LazyPopulateChildren(node, root.Id, childrenByParent);
                }
            }
            catch (Exception ex)
            {
                Logger.Info("[SemanticForm] 加载树异常: " + ex.Message);
            }
        }

        /// <summary>重新从数采和数据源同步语义树后刷新</summary>
        private void RebuildTree()
        {
            try
            {
                var devices = ConfigService.Instance.LoadDevices();
                SemanticService.Instance.SyncFromDeviceConfigs(devices);
                var dsSources = DataSourceService.Instance.GetAll();
                SemanticService.Instance.SyncFromDataSources(dsSources, DataSourceService.Instance);
                LoadTree();
                Logger.Info("[SemanticForm] 语义树已更新");
            }
            catch (Exception ex)
            {
                Logger.Error("[SemanticForm] 更新语义树失败: " + ex.Message, ex);
                MessageBox.Show("更新失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>懒加载一级子节点 — 优先走缓存，缓存未命中走 DB</summary>
        private void LazyPopulateChildren(TreeNode parentNode, string parentId,
            Dictionary<string, List<SemanticNode>> cache = null)
        {
            List<SemanticNode> children;
            if (cache != null && cache.TryGetValue(parentId, out var cached))
                children = cached;
            else if (_childrenByParentCache != null && _childrenByParentCache.TryGetValue(parentId, out var cached2))
                children = cached2;
            else
                children = SemanticService.Instance.GetChildren(parentId);

            foreach (var child in children.OrderBy(n => n.SortOrder).ThenBy(n => n.Name))
            {
                var childNode = BuildTreeNode(child);
                parentNode.Nodes.Add(childNode);
                // 如果该子节点还有孙节点，加占位子节点以显示 [+] 图标
                bool hasGrandchildren;
                if (_childrenByParentCache != null)
                    hasGrandchildren = _childrenByParentCache.ContainsKey(child.Id);
                else
                    hasGrandchildren = SemanticService.Instance.HasChildren(child.Id);
                if (hasGrandchildren)
                {
                    childNode.Nodes.Add(new TreeNode(PLACEHOLDER_TEXT) { Tag = new SemanticNode { Kind = "__placeholder__" } });
                }
            }
        }
        // 占位文本（不可见，仅用于触发 [+]）
        private const string PLACEHOLDER_TEXT = "...";

        private void Tree_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            // 检查是否有占位子节点
            if (node.Nodes.Count == 1 && node.Nodes[0].Tag is SemanticNode sn && sn.Kind == "__placeholder__")
            {
                node.Nodes.Clear();
                var semanticNode = node.Tag as SemanticNode;
                if (semanticNode != null)
                {
                    LazyPopulateChildren(node, semanticNode.Id);
                }
            }
        }

        private TreeNode BuildTreeNode(SemanticNode node)
        {
            string emoji = GetKindEmoji(node.Kind);
            // 仅设备和数据源体系叶子节点显示状态标签，上层层级不显示
            bool showStatus = IsLeafStatusNode(node.Kind);
            string statusTag = showStatus ? GetStatusTag(node.Status) : "";
            // DataTable / DataField 节点附带中文注释
            string displayName = node.Name;
            string tagCn = "";
            if (node.Kind == NodeKind.DataTable || node.Kind == NodeKind.DataField)
            {
                tagCn = node.GetProperty("TagCn", "");
                if (!string.IsNullOrEmpty(tagCn) && tagCn != node.Name)
                    displayName = string.Format("{0} ({1})", tagCn, node.Name);
            }
            string text = string.Format("{0} {1} {2}", emoji, displayName, statusTag);

            var treeNode = new TreeNode(text)
            {
                Tag = node,
                ForeColor = showStatus ? GetNodeColor(node.Status) : TextColor
            };

            if (node.Status == NodeStatus.Deleted)
            {
                Font strikeFont = new Font(treeNode.NodeFont ?? UiFont, FontStyle.Strikeout);
                // Strikeout will be applied in DrawNode
            }

            return treeNode;
        }

        /// <summary>需要显示在线/离线状态的节点类型（设备和数据源的叶子层）</summary>
        private bool IsLeafStatusNode(string kind)
        {
            switch (kind)
            {
                case "Equipment":
                case "Variable":
                case "Datasource":
                case "DataTable":
                case "DataField":
                    return true;
                default:
                    return false;
            }
        }

        private string GetKindEmoji(string kind)
        {
            switch (kind)
            {
                case "Company": return "\U0001F3E2";
                case "Division":
                case "Factory":
                case "Workshop": return "\U0001F3ED";
                case "Zone": return "\U0001F4CD";
                case "ProductionLine":
                case "WorkStation":
                case "Equipment": return "\u2699\uFE0F";
                case "Variable": return "\U0001F4CA";
                case "Datasource": return "\U0001F4BE";
                case "DataTable": return "\U0001F4CB";
                case "DataField": return "\U0001F4CC";
                case "Custom": return "\U0001F4C1";
                default: return "\u25CF";
            }
        }

        private string GetStatusTag(string status)
        {
            switch (status)
            {
                case "Online": return "[在线]";
                case "Offline": return "[离线]";
                case "Stopped": return "[停用]";
                case "Deleted": return "[已删除]";
                default: return "";
            }
        }

        private Color GetNodeColor(string status)
        {
            switch (status)
            {
                case "Online": return OnlineColor;
                case "Offline": return OfflineColor;
                case "Stopped": return StoppedColor;
                case "Deleted": return DeletedColor;
                default: return TextColor;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  搜索过滤
        // ════════════════════════════════════════════════════════════════

        private void FilterTree()
        {
            var keyword = _txtSearch.Text.Trim();
            if (keyword == "搜索节点..." || string.IsNullOrEmpty(keyword))
            {
                RestoreTreeColors(_tree.Nodes);
                return;
            }

            keyword = keyword.ToLower();
            _tree.BeginUpdate();

            // 在 _allNodesCache 中搜索匹配节点，按路径展开
            var matches = _allNodesCache.Where(n =>
                (n.Name != null && n.Name.ToLower().Contains(keyword)) ||
                (n.Code != null && n.Code.ToLower().Contains(keyword))
            ).Take(50).ToList();

            // 收集需要展开的路径（节点ID集合）
            var nodesToExpand = new HashSet<string>();
            foreach (var match in matches)
            {
                nodesToExpand.Add(match.Id);
                string pid = match.ParentId;
                while (!string.IsNullOrEmpty(pid))
                {
                    nodesToExpand.Add(pid);
                    var parent = _allNodesCache.FirstOrDefault(n => n.Id == pid);
                    pid = parent?.ParentId ?? "";
                }
            }

            // 按路径展开树节点（触发懒加载）
            foreach (TreeNode rootNode in _tree.Nodes)
            {
                FilterAndExpandPath(rootNode, keyword, nodesToExpand, matches.Select(m => m.Id).ToHashSet());
            }

            _tree.EndUpdate();
        }

        private bool FilterAndExpandPath(TreeNode node, string keyword, HashSet<string> expandSet, HashSet<string> matchSet)
        {
            var sn = node.Tag as SemanticNode;
            if (sn == null) return false;

            bool inExpandPath = expandSet.Contains(sn.Id);
            bool isMatch = matchSet.Contains(sn.Id);

            // 如果此节点在展开路径上且尚未加载子节点，先懒加载
            if (inExpandPath && node.Nodes.Count == 1 && node.Nodes[0].Tag is SemanticNode pn && pn.Kind == "__placeholder__")
            {
                node.Nodes.Clear();
                LazyPopulateChildren(node, sn.Id);
            }

            // 递归子节点
            bool childMatch = false;
            foreach (TreeNode child in node.Nodes)
            {
                if (FilterAndExpandPath(child, keyword, expandSet, matchSet))
                    childMatch = true;
            }

            if (inExpandPath)
                node.Expand();
            if (isMatch)
                node.BackColor = HighlightColor;
            else if (childMatch)
                node.BackColor = Color.White;
            else if (!inExpandPath)
            {
                node.BackColor = Color.White;
                node.Collapse();
            }
            else
                node.BackColor = Color.White;

            return isMatch || childMatch;
        }

        private void RestoreTreeColors(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                node.BackColor = Color.White;
                RestoreTreeColors(node.Nodes);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Tree 事件
        // ════════════════════════════════════════════════════════════════

        private void Tree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is SemanticNode node)
            {
                _currentNode = node;
                RefreshRightPanel();
            }
            else
            {
                _currentNode = null;
                ClearInfoTab();
            }
        }

        private void Tree_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Label))
            {
                e.CancelEdit = true;
                return;
            }
            if (e.Node?.Tag is SemanticNode node)
            {
                node.Name = e.Label;
                node.UpdatedAt = DateTime.Now;
                SemanticService.Instance.SaveNode(node);
                Logger.Info(string.Format("[SemanticForm] 重命名节点: {0} → {1}", node.Id, e.Label));
                LoadTree();
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  节点操作
        // ════════════════════════════════════════════════════════════════

        private void AddNode(string kind)
        {
            string parentId = "";
            string parentName = "";

            if (_currentNode != null)
            {
                parentId = _currentNode.Id;
                parentName = _currentNode.Name;
            }

            // 验证: 变量只能添加到设备下
            if (kind == NodeKind.Variable && _currentNode != null && _currentNode.Kind != NodeKind.Equipment)
            {
                MessageBox.Show("变量节点只能添加到设备节点下，请先选中一个设备节点。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string defaultName = GetDefaultName(kind);
            string name = ShowInputDialog(
                string.Format("添加{0}", NodeKind.GetDisplayName(kind)),
                string.Format("名称:", NodeKind.GetDisplayName(kind)),
                defaultName);

            if (string.IsNullOrWhiteSpace(name)) return;

            var node = new SemanticNode
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                ParentId = parentId,
                Name = name,
                Code = string.Format("{0}_{1}", kind.Substring(0, 2).ToUpper(), DateTime.Now.Ticks % 100000),
                Kind = kind,
                Status = NodeStatus.Online,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            SemanticService.Instance.SaveNode(node);
            Logger.Info(string.Format("[SemanticForm] 添加{0}: {1} (parent={2})",
                NodeKind.GetDisplayName(kind), name, parentName));

            LoadTree();

            // 选中新节点
            SelectNodeById(node.Id);
        }

        private string GetDefaultName(string kind)
        {
            switch (kind)
            {
                case "Company": return "新公司";
                case "Division": return "新事业部";
                case "Factory": return "新工厂";
                case "Workshop": return "新车间";
                case "Zone": return "新区域";
                case "ProductionLine": return "新产线";
                case "WorkStation": return "新工段";
                case "Equipment": return "新设备";
                case "Variable": return "新变量";
                case "Datasource": return "新数据源";
                case "DataTable": return "新数据表";
                case "DataField": return "新字段";
                default: return "新节点";
            }
        }

        private void ShowNodeKindSelector()
        {
            using (var dlg = new NodeKindSelectorDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    AddNode(dlg.SelectedKind);
                }
            }
        }

        private void AddChildNode()
        {
            if (_currentNode == null) return;
            ShowNodeKindSelector();
        }

        private void AddRootNode()
        {
            // 在根目录创建新节点（无父节点）
            using (var dlg = new NodeKindSelectorDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var node = new SemanticNode
                    {
                        Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                        ParentId = "",
                        Name = GetDefaultName(dlg.SelectedKind),
                        Kind = dlg.SelectedKind,
                        Status = NodeStatus.Online,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    SemanticService.Instance.SaveNode(node);
                    LoadTree();
                }
            }
        }

        private void RenameNode()
        {
            if (_currentNode != null && _tree.SelectedNode != null)
                _tree.SelectedNode.BeginEdit();
        }

        private void DeleteSelectedNode()
        {
            if (_currentNode == null) return;

            // 统计级联影响
            int relCount, evtCount;
            SemanticService.Instance.GetBindingCounts(_currentNode.Id, out relCount, out evtCount);
            var descendants = SemanticService.Instance.GetDescendants(_currentNode.Id, false);

            string msg;
            if (descendants.Count > 0)
                msg = string.Format("确定删除「{0}」吗？\n将级联删除 {1} 个子节点、{2} 个关系和 {3} 个事件。",
                    _currentNode.Name, descendants.Count, relCount, evtCount);
            else
                msg = string.Format("确定删除「{0}」吗？\n关联: {1} 个关系, {2} 个事件。",
                    _currentNode.Name, relCount, evtCount);

            if (MessageBox.Show(msg, "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            SemanticService.Instance.DeleteNode(_currentNode.Id, true);
            Logger.Info(string.Format("[SemanticForm] 删除节点: {0} ({1}), 级联 {2} 个子节点",
                _currentNode.Name, _currentNode.Id, descendants.Count));

            _currentNode = null;
            LoadTree();
            ClearInfoTab();
        }

        private void MoveNodeUp()
        {
            if (_currentNode == null) return;
            int newOrder = Math.Max(0, _currentNode.SortOrder - 1);
            _currentNode.SortOrder = newOrder;
            _currentNode.UpdatedAt = DateTime.Now;
            SemanticService.Instance.SaveNode(_currentNode);
            Logger.Info(string.Format("[SemanticForm] 上移节点: {0} sort={1}", _currentNode.Name, newOrder));
            LoadTree();
            SelectNodeById(_currentNode.Id);
        }

        private void MoveNodeDown()
        {
            if (_currentNode == null) return;
            _currentNode.SortOrder = _currentNode.SortOrder + 1;
            _currentNode.UpdatedAt = DateTime.Now;
            SemanticService.Instance.SaveNode(_currentNode);
            Logger.Info(string.Format("[SemanticForm] 下移节点: {0} sort={1}", _currentNode.Name, _currentNode.SortOrder));
            LoadTree();
            SelectNodeById(_currentNode.Id);
        }

        private void MoveNodeTo()
        {
            if (_currentNode == null) return;

            string targetParentId;
            using (var dlg = new SemanticNodePickerForm())
            {
                dlg.Text = "移动到目标节点 — 当前: " + _currentNode.Name;
                if (dlg.ShowDialog() != DialogResult.OK) return;
                targetParentId = dlg.SelectedNodeId;
            }

            if (string.IsNullOrEmpty(targetParentId) || targetParentId == _currentNode.Id)
            {
                MessageBox.Show("不能移动到自身。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 防止环：目标不能是自身的后代
            var descendants = SemanticService.Instance.GetDescendants(_currentNode.Id);
            if (descendants.Any(d => d.Id == targetParentId))
            {
                MessageBox.Show("不能移动到自身子节点下，会形成循环。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                SemanticService.Instance.MoveNode(_currentNode.Id, targetParentId);
                Logger.Info(string.Format("[SemanticForm] 移动节点: {0} → {1}", _currentNode.Id, targetParentId));
                LoadTree();
                SelectNodeById(_currentNode.Id);

                // v2.0: 同步刷新数据采集页面的设备树
                var mainForm = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
                if (mainForm != null) mainForm.BeginInvoke(new Action(() => mainForm.RefreshDeviceTree()));
            }
            catch (Exception ex)
            {
                Logger.Error("[SemanticForm] 移动节点失败: " + ex.Message, ex);
                MessageBox.Show("移动失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SelectNodeById(string id)
        {
            TreeNode found = FindNodeById(_tree.Nodes, id);
            if (found != null)
            {
                _tree.SelectedNode = found;
                found.EnsureVisible();
            }
        }

        /// <summary>外部调用：导航到指定节点（影响分析双击跳转）</summary>
        public void NavigateToNode(string id)
        {
            SelectNodeById(id);
            Activate();
        }

        private TreeNode FindNodeById(TreeNodeCollection nodes, string id)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is SemanticNode sn && sn.Id == id)
                    return node;
                var found = FindNodeById(node.Nodes, id);
                if (found != null) return found;
            }
            return null;
        }

        // ════════════════════════════════════════════════════════════════
        //  右侧面板刷新
        // ════════════════════════════════════════════════════════════════

        private void RefreshRightPanel()
        {
            if (_currentNode == null) return;

            // 调Tab显隐
            AdjustTabsForNode();

            LoadNodeInfo();
            RefreshActiveTab();
        }

        private void AdjustTabsForNode()
        {
            if (_currentNode == null) return;

            bool isVar = _currentNode.Kind == NodeKind.Variable;
            bool hasRels = _currentNode.Kind == NodeKind.Equipment ||
                           _currentNode.Kind == NodeKind.Variable ||
                           _currentNode.Kind == NodeKind.Custom;

            _tabControl.TabPages.Clear();

            if (isVar)
            {
                _tabControl.TabPages.Add(_tabInfo);   // 变量信息
                _tabControl.TabPages.Add(_tabVarRels); // 变量关系
                _tabControl.TabPages.Add(_tabEvents);  // 变量事件
            }
            else
            {
                _tabControl.TabPages.Add(_tabInfo);
                if (hasRels)
                    _tabControl.TabPages.Add(_tabRelations);
                _tabControl.TabPages.Add(_tabEvents);
            }
        }

        private void RefreshActiveTab()
        {
            if (_tabControl.SelectedTab == _tabInfo) LoadNodeInfo();
            else if (_tabControl.SelectedTab == _tabRelations) LoadRelations();
            else if (_tabControl.SelectedTab == _tabEvents) LoadEvents();
            else if (_tabControl.SelectedTab == _tabVarRels) LoadVarRelations();
        }

        private void ClearInfoTab()
        {
            _txtNodeName.Text = "";
            _txtNodeCode.Text = "";
            _cmbNodeKind.Text = "-";
            _cmbNodeStatus.SelectedIndex = 0;
            _lblNodeSourceType.Text = "-";
            _lblNodeSourceId.Text = "-";
            _txtNodeDesc.Text = "";
            _pnlVarExt.Visible = false;
            _pnlEquipExt.Visible = false;
            _lblInfoHint.Text = "";
            _btnSaveInfo.Enabled = false;
        }

        // ════════════════════════════════════════════════════════════════
        //  Tab 1: 加载节点信息
        // ════════════════════════════════════════════════════════════════

        private void LoadNodeInfo()
        {
            if (_currentNode == null) { ClearInfoTab(); return; }

            _btnSaveInfo.Enabled = true;
            _txtNodeName.Text = _currentNode.Name ?? "";
            _txtNodeCode.Text = _currentNode.Code ?? "";
            _cmbNodeKind.Text = NodeKind.GetDisplayName(_currentNode.Kind);

            // 状态 — 对同步节点查询真实状态，手动节点保持存储值
            bool isSynced = !string.IsNullOrEmpty(_currentNode.SourceType);
            _cmbNodeStatus.Enabled = !isSynced; // 同步节点状态由外部驱动，不可手动改
            string realStatus = _currentNode.Status;
            if (isSynced)
            {
                realStatus = GetRealNodeStatus(_currentNode);
                // 如果真实状态与存储状态不同，更新到数据库
                if (realStatus != _currentNode.Status)
                {
                    _currentNode.Status = realStatus;
                    _currentNode.UpdatedAt = DateTime.Now;
                    SemanticService.Instance.SaveNode(_currentNode);
                }
            }
            switch (realStatus)
            {
                case "Online": _cmbNodeStatus.Text = "在线"; break;
                case "Offline": _cmbNodeStatus.Text = "离线"; break;
                case "Stopped": _cmbNodeStatus.Text = "停用"; break;
                case "Deleted": _cmbNodeStatus.Text = "已删除"; break;
                default: _cmbNodeStatus.Text = "离线"; break;
            }

            // 来源信息
            if (string.IsNullOrEmpty(_currentNode.SourceType))
            {
                _lblNodeSourceType.Text = "手动创建";
            }
            else if (_currentNode.SourceType == "device")
            {
                _lblNodeSourceType.Text = "数采设备";
            }
            else if (_currentNode.SourceType == "datasource")
            {
                _lblNodeSourceType.Text = "数据源";
            }
            else
            {
                _lblNodeSourceType.Text = _currentNode.SourceType;
            }
            _lblNodeSourceId.Text = string.IsNullOrEmpty(_currentNode.SourceId) ? "无" : _currentNode.SourceId;

            _txtNodeDesc.Text = _currentNode.Description ?? "";

            // Variable 扩展
            bool isVar = _currentNode.Kind == NodeKind.Variable;
            _pnlVarExt.Visible = isVar;
            if (isVar)
            {
                // 查找父设备节点
                var parentEquip = FindParentOfKind(_currentNode, NodeKind.Equipment);
                _lblVarParentEquip.Text = parentEquip != null ? parentEquip.Name : "—";
                _cmbVarRole.Text = _currentNode.GetProperty("VariableRole", "");
                _cmbVarUnit.Text = _currentNode.GetProperty("Unit", "");
            }

            // Equipment 扩展
            bool isEquip = _currentNode.Kind == NodeKind.Equipment;
            _pnlEquipExt.Visible = isEquip;
            if (isEquip)
            {
                _cmbEquipType.Text = _currentNode.GetProperty("EquipmentType", "");
                string driverId = _currentNode.SourceType == "device" ? _currentNode.SourceId : "";
                _lblEquipDriverId.Text = string.IsNullOrEmpty(driverId) ? "—" : driverId;
            }

            AdjustYPositions();
        }

        private SemanticNode FindParentOfKind(SemanticNode node, string kind)
        {
            if (node == null || string.IsNullOrEmpty(node.ParentId)) return null;
            var parent = SemanticService.Instance.GetNode(node.ParentId);
            while (parent != null)
            {
                if (parent.Kind == kind) return parent;
                if (string.IsNullOrEmpty(parent.ParentId)) break;
                parent = SemanticService.Instance.GetNode(parent.ParentId);
            }
            return null;
        }

        /// <summary>查询节点的真实运行状态（非存储值）</summary>
        private string GetRealNodeStatus(SemanticNode node)
        {
            if (node == null) return "Offline";
            if (node.Status == "Deleted") return "Deleted";

            // 设备/变量节点：查数采服务是否正在采集
            if (node.SourceType == "device")
            {
                string srcId = node.SourceId;
                // 变量节点：用父设备的SourceId
                if (node.Kind == NodeKind.Variable)
                {
                    var parentEquip = FindParentOfKind(node, NodeKind.Equipment);
                    if (parentEquip != null && !string.IsNullOrEmpty(parentEquip.SourceId))
                        srcId = parentEquip.SourceId;
                }
                if (!string.IsNullOrEmpty(srcId))
                {
                    try
                    {
                        return DataCollectionService.Instance.IsDeviceRunning(srcId) ? "Online" : "Offline";
                    }
                    catch { return "Offline"; }
                }
                return "Stopped"; // 无SourceId = 已解绑
            }

            // 数据源节点：查真实连接状态
            if (node.SourceType == "datasource")
            {
                string srcId = node.SourceId;
                // 子节点（表/字段）：向上找数据源节点的 SourceId
                if (node.Kind == NodeKind.DataTable || node.Kind == NodeKind.DataField)
                {
                    var parentDs = FindParentOfKind(node, NodeKind.Datasource);
                    if (parentDs != null && !string.IsNullOrEmpty(parentDs.SourceId))
                        srcId = parentDs.SourceId;
                }
                if (!string.IsNullOrEmpty(srcId) && !srcId.StartsWith("folder:"))
                {
                    try
                    {
                        return DataSourceService.Instance.IsConnected(srcId) ? "Online" : "Offline";
                    }
                    catch { return "Offline"; }
                }
                return node.Status == "Online" ? "Online" : "Offline";
            }

            // 手动节点：返回存储的状态
            return node.Status;
        }

        private void AdjustYPositions()
        {
            int y = 16;
            int rowH = 26;
            int gap = 12;
            int lblW = 80;
            // 7 个通用行: 名称, 编码, 类型, 状态, 来源类型, 来源ID, 描述(60+12)
            y += (rowH + gap) * 6; // 到描述
            y += 72; // 描述框

            if (_pnlVarExt.Visible)
            {
                _pnlVarExt.Location = new Point(16, y);
                y += _pnlVarExt.Height;
            }

            if (_pnlEquipExt.Visible)
            {
                _pnlEquipExt.Location = new Point(16, y);
                y += _pnlEquipExt.Height;
            }

            y += gap + 6; // 面板与按钮之间留足边距
            _btnSaveInfo.Location = new Point(16 + lblW + 5, y);
            _lblInfoHint.Location = new Point(16 + lblW + 5 + 100, y + 8);
        }

        private void BtnSaveInfo_Click(object sender, EventArgs e)
        {
            if (_currentNode == null) return;

            _currentNode.Name = _txtNodeName.Text.Trim();
            _currentNode.Code = _txtNodeCode.Text.Trim();

            // 节点类型 — 中→英映射后存储
            string kindInput = _cmbNodeKind.Text.Trim();
            _currentNode.Kind = NodeKind.ToCode(kindInput);

            // 状态 — 仅手动节点允许用户修改
            bool isSynced = !string.IsNullOrEmpty(_currentNode.SourceType);
            if (!isSynced)
            {
                switch (_cmbNodeStatus.Text)
                {
                    case "在线": _currentNode.Status = NodeStatus.Online; break;
                    case "离线": _currentNode.Status = NodeStatus.Offline; break;
                    case "停用": _currentNode.Status = NodeStatus.Stopped; break;
                    case "已删除": _currentNode.Status = NodeStatus.Deleted; break;
                }
            }

            _currentNode.Description = _txtNodeDesc.Text.Trim();
            _currentNode.UpdatedAt = DateTime.Now;

            // Variable 属性
            if (_currentNode.Kind == NodeKind.Variable)
            {
                _currentNode.SetProperty("VariableRole", _cmbVarRole.Text.Trim());
                _currentNode.SetProperty("Unit", _cmbVarUnit.Text.Trim());
            }

            // Equipment 属性
            if (_currentNode.Kind == NodeKind.Equipment)
            {
                _currentNode.SetProperty("EquipmentType", _cmbEquipType.Text.Trim());
            }

            SemanticService.Instance.SaveNode(_currentNode);
            _lblInfoHint.Text = "✓ 已保存";
            _lblInfoHint.ForeColor = Color.FromArgb(0, 168, 84);
            Logger.Info(string.Format("[SemanticForm] 保存节点: {0} ({1})", _currentNode.Name, _currentNode.Id));

            // 同步刷新树
            LoadTree();
            SelectNodeById(_currentNode.Id);
        }

        // ════════════════════════════════════════════════════════════════
        //  Tab 2: 节点关系
        // ════════════════════════════════════════════════════════════════

        private void LoadRelations()
        {
            _gridRelations.Rows.Clear();
            if (_currentNode == null) return;

            var relations = SemanticService.Instance.GetNodeRelations(_currentNode.Id);

            foreach (var rel in relations)
            {
                string direction = rel.SourceNodeId == _currentNode.Id ? "→" : "←";
                var sourceNode = SemanticService.Instance.GetNode(rel.SourceNodeId);
                var targetNode = SemanticService.Instance.GetNode(rel.TargetNodeId);
                string sourceName = sourceNode != null ? sourceNode.Name : rel.SourceNodeId;
                string targetName = targetNode != null ? targetNode.Name : rel.TargetNodeId;

                int idx = _gridRelations.Rows.Add(sourceName, rel.RelationType, targetName, direction, rel.Description ?? "");
                _gridRelations.Rows[idx].Tag = rel;
            }
        }

        private void GridRelations_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_gridRelations.Columns[e.ColumnIndex].Name == "DeleteBtn")
            {
                if (_gridRelations.Rows[e.RowIndex].Tag is SemanticEquipmentRelation rel)
                {
                    if (MessageBox.Show("确定删除此关系？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        SemanticService.Instance.DeleteNodeRelation(rel.Id);
                        Logger.Info(string.Format("[SemanticForm] 删除节点关系: {0}", rel.Id));
                        LoadRelations();
                    }
                }
            }
        }

        private void DeleteSelectedRelation()
        {
            if (_gridRelations.SelectedRows.Count == 0) return;
            if (MessageBox.Show("确定删除选中的关系？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            foreach (DataGridViewRow row in _gridRelations.SelectedRows)
            {
                if (row.Tag is SemanticEquipmentRelation rel)
                    SemanticService.Instance.DeleteNodeRelation(rel.Id);
            }
            Logger.Info("[SemanticForm] 批量删除节点关系");
            LoadRelations();
        }

        private void BtnAddRelation_Click(object sender, EventArgs e)
        {
            if (_currentNode == null)
            {
                MessageBox.Show("请先在左侧树中选中一个节点", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dlg = new RelationEditDialog(_currentNode.Id, _allNodesCache))
            {
                if (dlg.ShowDialog() == DialogResult.OK && dlg.Result != null)
                {
                    SemanticService.Instance.SaveNodeRelation(dlg.Result);
                    Logger.Info(string.Format("[SemanticForm] 添加节点关系: {0}→{1}",
                        dlg.Result.SourceNodeId, dlg.Result.TargetNodeId));
                    LoadRelations();
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Tab 3: 节点事件
        // ════════════════════════════════════════════════════════════════

        private void LoadEvents()
        {
            _gridEvents.Rows.Clear();

            string nodeId = _chkEventAll.Checked ? null : (_currentNode?.Id);
            if (!_chkEventAll.Checked && nodeId == null) return;

            var from = _dtpEventFrom.Value;
            var to = _dtpEventTo.Value;

            string eventType = null;
            if (_cmbEventType.SelectedIndex > 0)
                eventType = _cmbEventType.SelectedItem.ToString();

            var events = SemanticService.Instance.GetEvents(nodeId, from, to, eventType, 500);

            foreach (var evt in events)
            {
                int idx = _gridEvents.Rows.Add(
                    evt.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    evt.EventType,
                    evt.ProcessingMethod ?? "仅记录",
                    evt.Description ?? "");
                _gridEvents.Rows[idx].Tag = evt;

                // 按类型着色
                Color rowColor = Color.White;
                switch (evt.EventType)
                {
                    case "超上限":
                    case "超下限":
                    case "超高高限(HH)":
                    case "超低低限(LL)":
                    case "报警":
                        rowColor = RowAlarmColor; break;
                    case "故障":
                        rowColor = RowFaultColor; break;
                    case "恢复":
                    case "通讯恢复":
                        rowColor = RowRecoverColor; break;
                    case "维护保养":
                        rowColor = RowMaintenanceColor; break;
                    case "通讯中断":
                    case "采集异常":
                        rowColor = RowCommColor; break;
                }
                _gridEvents.Rows[idx].Tag = evt;

                // 直接着色（也处理 RowPrePaint 中覆盖的情况）
                foreach (DataGridViewCell cell in _gridEvents.Rows[idx].Cells)
                {
                    if (cell is DataGridViewButtonCell) continue;
                    cell.Style.BackColor = rowColor;
                }
            }
        }

        private void GridEvents_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = _gridEvents.Rows[e.RowIndex];
            if (row.Tag is SemanticVariableEvent evt)
            {
                Color rowColor = Color.White;
                switch (evt.EventType)
                {
                    case "超上限":
                    case "超下限":
                    case "超高高限(HH)":
                    case "超低低限(LL)":
                    case "报警":
                        rowColor = RowAlarmColor; break;
                    case "故障":
                        rowColor = RowFaultColor; break;
                    case "恢复":
                    case "通讯恢复":
                        rowColor = RowRecoverColor; break;
                    case "维护保养":
                        rowColor = RowMaintenanceColor; break;
                    case "通讯中断":
                    case "采集异常":
                        rowColor = RowCommColor; break;
                }
                if (rowColor != Color.White)
                {
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (!(cell is DataGridViewButtonCell))
                            cell.Style.BackColor = rowColor;
                    }
                }
            }
        }

        private void BtnAddEvent_Click(object sender, EventArgs e)
        {
            string nodeId = _chkEventAll.Checked ? "" : (_currentNode?.Id ?? "");
            if (!_chkEventAll.Checked && string.IsNullOrEmpty(nodeId))
            {
                MessageBox.Show("请先在左侧树中选中一个节点，或勾选「所有节点」", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dlg = new EventEditDialog(nodeId, _allNodesCache))
            {
                if (dlg.ShowDialog() == DialogResult.OK && dlg.Result != null)
                {
                    SemanticService.Instance.SaveEvent(dlg.Result);
                    Logger.Info(string.Format("[SemanticForm] 添加事件: {0} → 节点 {1}",
                        dlg.Result.EventType, dlg.Result.NodeId));
                    LoadEvents();
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Variable Tab: 变量关系
        // ════════════════════════════════════════════════════════════════

        private void LoadVarRelations()
        {
            _gridVarRels.Rows.Clear();
            if (_currentNode == null || _currentNode.Kind != NodeKind.Variable) return;

            var relations = SemanticService.Instance.GetVariableRelations(_currentNode.Id);

            foreach (var rel in relations)
            {
                string targetDisplay;
                switch (rel.TargetType)
                {
                    case "datasource_field":
                        targetDisplay = string.Format("{0} / {1} / {2}",
                            rel.TargetDatasourceId, rel.TargetTableName, rel.TargetFieldName);
                        break;
                    case "constant":
                        targetDisplay = "常量";
                        break;
                    case "expression":
                        targetDisplay = "表达式";
                        break;
                    case "variable":
                        targetDisplay = "变量";
                        break;
                    default:
                        targetDisplay = rel.TargetType;
                        break;
                }

                string valueDisplay;
                switch (rel.TargetType)
                {
                    case "datasource_field":
                        valueDisplay = rel.TargetFieldName ?? "";
                        break;
                    case "constant":
                        valueDisplay = rel.ConstantValue ?? "";
                        break;
                    case "expression":
                        valueDisplay = (rel.Expression ?? "").Length > 40
                            ? rel.Expression.Substring(0, 40) + "..."
                            : rel.Expression;
                        break;
                    case "variable":
                        valueDisplay = ResolveVariableName(rel.TargetVariableNodeId);
                        break;
                    default:
                        valueDisplay = "";
                        break;
                }

                string condDisplay = rel.ConditionVariableIds.Count > 0
                    ? string.Join(", ", rel.ConditionVariableIds)
                    : "-";

                int idx = _gridVarRels.Rows.Add(
                    rel.RelationType,
                    rel.TargetType == "datasource_field" ? "数据源字段" :
                    rel.TargetType == "constant" ? "常量" : "表达式",
                    targetDisplay,
                    valueDisplay,
                    rel.Unit ?? "",
                    condDisplay,
                    rel.Description ?? "");
                _gridVarRels.Rows[idx].Tag = rel;
            }
        }

        private string ResolveVariableName(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return "";
            var n = _allNodesCache.FirstOrDefault(x => x.Id == nodeId);
            if (n != null)
                return string.Format("{0} ({1})", n.Name, n.Code);
            var sn = SemanticService.Instance.GetNode(nodeId);
            if (sn != null)
                return string.Format("{0} ({1})", sn.Name, sn.Code);
            return nodeId;
        }

        private void ShowImpactAnalysis()
        {
            if (_currentNode == null || _currentNode.Kind != NodeKind.Variable)
            {
                MessageBox.Show("请先选中一个变量节点", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new ImpactGraphDialog(_currentNode, _allNodesCache))
                dlg.ShowDialog(this);
        }

        private void BtnAddVarRel_Click(object sender, EventArgs e)
        {
            if (_currentNode == null || _currentNode.Kind != NodeKind.Variable)
            {
                MessageBox.Show("请先选中一个变量节点", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dlg = new VariableRelationEditDialog(_currentNode.Id, _allNodesCache))
            {
                if (dlg.ShowDialog() == DialogResult.OK && dlg.Result != null)
                {
                    SemanticService.Instance.SaveVariableRelation(dlg.Result);
                    Logger.Info(string.Format("[SemanticForm] 添加变量关系: {0} → {1}",
                        dlg.Result.RelationType, dlg.Result.VariableNodeId));
                    LoadVarRelations();
                }
            }
        }

        private void DeleteSelectedVarRel()
        {
            if (_gridVarRels.SelectedRows.Count == 0) return;
            if (MessageBox.Show("确定删除选中的变量关系？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            foreach (DataGridViewRow row in _gridVarRels.SelectedRows)
            {
                if (row.Tag is SemanticVariableRelation rel)
                    SemanticService.Instance.DeleteVariableRelation(rel.Id);
            }
            Logger.Info("[SemanticForm] 批量删除变量关系");
            LoadVarRelations();
        }

        // ════════════════════════════════════════════════════════════════
        //  导入导出
        // ════════════════════════════════════════════════════════════════

        #region JSON 导入

        private void ImportFromJson()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "选择 JSON 导入文件";
                ofd.Filter = "JSON 文件|*.json|所有文件|*.*";
                ofd.FilterIndex = 1;
                if (ofd.ShowDialog() != DialogResult.OK) return;

                var mode = ShowImportModeDialog();
                if (mode == null) return;

                try
                {
                    SemanticService.Instance.ImportFromJson(ofd.FileName, mode.Value);
                    Logger.Info(string.Format("[SemanticForm] JSON 导入完成: {0} mode={1}", ofd.FileName, mode));
                    MessageBox.Show("导入完成！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadTree();
                }
                catch (Exception ex)
                {
                    Logger.Error("[SemanticForm] JSON 导入失败: " + ex.Message, ex);
                    MessageBox.Show("导入失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region CSV 导入

        private void ImportFromCsv()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "选择 CSV 导入文件";
                ofd.Filter = "CSV 文件|*.csv|所有文件|*.*";
                ofd.FilterIndex = 1;
                if (ofd.ShowDialog() != DialogResult.OK) return;

                var mode = ShowImportModeDialog();
                if (mode == null) return;

                try
                {
                    ImportFromCsvInternal(ofd.FileName, mode.Value);
                    Logger.Info(string.Format("[SemanticForm] CSV 导入完成: {0} mode={1}", ofd.FileName, mode));
                    MessageBox.Show("CSV 导入完成！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadTree();
                }
                catch (Exception ex)
                {
                    Logger.Error("[SemanticForm] CSV 导入失败: " + ex.Message, ex);
                    MessageBox.Show("导入失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportFromCsvInternal(string filePath, SemanticService.ImportMode mode)
        {
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            if (lines.Length < 2) return;

            // 解析表头
            var header = lines[0].Trim().Trim('\"').Split(new[] { "\",\"" }, StringSplitOptions.None);

            var nodes = new List<SemanticNode>();
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var parts = ParseCsvLine(line);
                if (parts.Length < 5) continue;

                var node = new SemanticNode();
                // 预期列: 路径,节点ID,父节点ID,名称,编码,类型,状态,来源类型,来源ID,描述,属性,排序,创建时间,更新时间
                if (parts.Length > 1) node.Id = parts[1].Trim('\"');
                if (parts.Length > 2) node.ParentId = parts[2].Trim('\"');
                if (parts.Length > 3) node.Name = parts[3].Trim('\"');
                if (parts.Length > 4) node.Code = parts[4].Trim('\"');
                if (parts.Length > 5) node.Kind = MapKindNameToCode(parts[5].Trim('\"'));
                if (parts.Length > 6) node.Status = MapStatusNameToCode(parts[6].Trim('\"'));
                if (parts.Length > 7) node.SourceType = parts[7].Trim('\"');
                if (parts.Length > 8) node.SourceId = parts[8].Trim('\"');
                if (parts.Length > 9) node.Description = parts[9].Trim('\"');
                if (parts.Length > 10) node.PropertiesJson = parts[10].Trim('\"');
                if (parts.Length > 11) { int.TryParse(parts[11].Trim('\"'), out int so); node.SortOrder = so; }

                if (!string.IsNullOrEmpty(node.Id) && !string.IsNullOrEmpty(node.Name))
                    nodes.Add(node);
            }

            if (mode == SemanticService.ImportMode.FullReplace)
            {
                // 清空手动节点后导入
                foreach (var node in nodes)
                {
                    node.CreatedAt = DateTime.Now;
                    node.UpdatedAt = DateTime.Now;
                    SemanticService.Instance.SaveNode(node);
                }
            }
            else if (mode == SemanticService.ImportMode.Append)
            {
                foreach (var node in nodes)
                {
                    var existing = SemanticService.Instance.GetNode(node.Id);
                    if (existing == null)
                    {
                        node.CreatedAt = DateTime.Now;
                        node.UpdatedAt = DateTime.Now;
                        SemanticService.Instance.SaveNode(node);
                    }
                }
            }
            else if (mode == SemanticService.ImportMode.Update)
            {
                foreach (var node in nodes)
                {
                    var existing = SemanticService.Instance.GetNode(node.Id);
                    if (existing != null)
                    {
                        node.CreatedAt = existing.CreatedAt;
                        node.UpdatedAt = DateTime.Now;
                        SemanticService.Instance.SaveNode(node);
                    }
                }
            }
            else // Merge
            {
                foreach (var node in nodes)
                {
                    var existing = SemanticService.Instance.GetNode(node.Id);
                    if (existing != null)
                    {
                        node.CreatedAt = existing.CreatedAt;
                        node.UpdatedAt = DateTime.Now;
                        SemanticService.Instance.SaveNode(node);
                    }
                    else
                    {
                        node.CreatedAt = DateTime.Now;
                        node.UpdatedAt = DateTime.Now;
                        SemanticService.Instance.SaveNode(node);
                    }
                }
            }
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        current.Append('\"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result.ToArray();
        }

        private string MapKindNameToCode(string displayName)
        {
            foreach (var kind in NodeKind.AllKinds)
            {
                if (NodeKind.GetDisplayName(kind) == displayName) return kind;
            }
            return "Custom";
        }

        private string MapStatusNameToCode(string displayName)
        {
            if (displayName == "在线") return "Online";
            if (displayName == "离线") return "Offline";
            if (displayName == "已停止") return "Stopped";
            if (displayName == "已删除") return "Deleted";
            return "Online";
        }

        #endregion

        #region JSON 导出

        private void ExportToJson()
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = "导出 JSON";
                sfd.Filter = "JSON 文件|*.json|所有文件|*.*";
                sfd.FilterIndex = 1;
                sfd.FileName = string.Format("语义层导出_{0:yyyyMMdd_HHmmss}.json", DateTime.Now);
                if (sfd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    SemanticService.Instance.ExportToJson(sfd.FileName, true, true);
                    Logger.Info("[SemanticForm] JSON 导出完成: " + sfd.FileName);
                    MessageBox.Show("导出完成！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Logger.Error("[SemanticForm] JSON 导出失败: " + ex.Message, ex);
                    MessageBox.Show("导出失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region CSV 导出

        private void ExportToCsv()
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = "导出 CSV";
                sfd.Filter = "CSV 文件|*.csv|所有文件|*.*";
                sfd.FilterIndex = 1;
                sfd.FileName = string.Format("语义层导出_{0:yyyyMMdd_HHmmss}.csv", DateTime.Now);
                if (sfd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    SemanticService.Instance.ExportToExcel(sfd.FileName);
                    Logger.Info("[SemanticForm] CSV 导出完成: " + sfd.FileName);
                    MessageBox.Show("导出完成！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Logger.Error("[SemanticForm] CSV 导出失败: " + ex.Message, ex);
                    MessageBox.Show("导出失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region ImportMode 对话框

        private SemanticService.ImportMode? ShowImportModeDialog()
        {
            using (var form = new Form())
            {
                form.Text = "导入模式选择";
                form.Size = new Size(350, 220);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.Font = UiFont;

                var group = new GroupBox
                {
                    Text = "选择导入模式",
                    Location = new Point(16, 12),
                    Size = new Size(310, 120),
                    Font = UiFont
                };

                var rb1 = new RadioButton { Text = "全量覆盖（清空后导入）", Location = new Point(16, 22), Size = new Size(280, 22), Font = UiFont, Checked = true };
                var rb2 = new RadioButton { Text = "增量追加（只添加新）", Location = new Point(16, 46), Size = new Size(280, 22), Font = UiFont };
                var rb3 = new RadioButton { Text = "增量更新（只更新已有）", Location = new Point(16, 70), Size = new Size(280, 22), Font = UiFont };
                var rb4 = new RadioButton { Text = "合并模式（添加+更新）", Location = new Point(16, 94), Size = new Size(280, 22), Font = UiFont };

                group.Controls.AddRange(new Control[] { rb1, rb2, rb3, rb4 });
                form.Controls.Add(group);

                var btnOK = new Button
                {
                    Text = "确定",
                    Location = new Point(140, 142),
                    Size = new Size(80, 30),
                    Font = UiFont,
                    DialogResult = DialogResult.OK,
                    BackColor = AccentColor,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    UseVisualStyleBackColor = false
                };
                var btnCancel = new Button
                {
                    Text = "取消",
                    Location = new Point(228, 142),
                    Size = new Size(80, 30),
                    Font = UiFont,
                    DialogResult = DialogResult.Cancel,
                    FlatStyle = FlatStyle.Flat
                };
                form.Controls.Add(btnOK);
                form.Controls.Add(btnCancel);
                form.AcceptButton = btnOK;
                form.CancelButton = btnCancel;

                if (form.ShowDialog() != DialogResult.OK) return null;

                if (rb1.Checked) return SemanticService.ImportMode.FullReplace;
                if (rb2.Checked) return SemanticService.ImportMode.Append;
                if (rb3.Checked) return SemanticService.ImportMode.Update;
                if (rb4.Checked) return SemanticService.ImportMode.Merge;
                return null;
            }
        }

        #endregion

        // ════════════════════════════════════════════════════════════════
        //  辅助
        // ════════════════════════════════════════════════════════════════

        private string ShowInputDialog(string title, string prompt, string defaultValue)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.Size = new Size(380, 170);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.Font = UiFont;

                var lbl = new Label { Text = prompt, Location = new Point(16, 16), Size = new Size(340, 20), Font = UiFont };
                var txt = new TextBox { Text = defaultValue, Location = new Point(16, 42), Size = new Size(340, 24), Font = UiFont };
                var btnOk = new Button
                {
                    Text = "确定",
                    Location = new Point(200, 78),
                    Size = new Size(75, 28),
                    DialogResult = DialogResult.OK,
                    Font = UiFont,
                    BackColor = AccentColor,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    UseVisualStyleBackColor = false
                };
                var btnCancel = new Button
                {
                    Text = "取消",
                    Location = new Point(281, 78),
                    Size = new Size(75, 28),
                    DialogResult = DialogResult.Cancel,
                    Font = UiFont,
                    FlatStyle = FlatStyle.Flat
                };

                form.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                return form.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : null;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  语言切换
        // ════════════════════════════════════════════════════════════════

        private void ApplyMenuLanguage()
        {
            if (_menuStrip == null) return;
            foreach (ToolStripItem item in _menuStrip.Items)
            {
                if (item.Text.Contains("导入") || item.Text.Contains("Import"))
                    item.Text = L("Semantic.Menu.Import") ?? "导入";
                else if (item.Text.Contains("导出") || item.Text.Contains("Export"))
                    item.Text = L("Semantic.Menu.Export") ?? "导出";
                else if (item.Text.Contains("更新") || item.Text.Contains("Rebuild"))
                    item.Text = L("Semantic.Menu.Rebuild") ?? "🔄 更新语义树";
            }
        }

        private void ApplyTreeMenuLanguage()
        {
            if (_treeMenu == null) return;
            foreach (ToolStripItem item in _treeMenu.Items)
            {
                if (item is ToolStripMenuItem mi)
                {
                    if (mi.Text.Contains("更新") || mi.Text.Contains("Rebuild") || mi.Text.Contains("Refresh"))
                        mi.Text = L("Semantic.Tree.Refresh") ?? "更新语义树";
                }
            }
        }

        private void ApplyTabLanguage()
        {
            if (_tabControl == null) return;
            if (_tabControl.TabPages.Count >= 1)
                _tabControl.TabPages[0].Text = L("Semantic.Tab.Info") ?? "节点信息";
            if (_tabControl.TabPages.Count >= 2)
                _tabControl.TabPages[1].Text = L("Semantic.Tab.Relations") ?? "节点关系";
            if (_tabControl.TabPages.Count >= 3)
                _tabControl.TabPages[2].Text = L("Semantic.Tab.Events") ?? "节点事件";
        }

        private void ApplyInfoPanelLanguage()
        {
            if (_btnSaveInfo != null)
                _btnSaveInfo.Text = L("Semantic.Info.Save") ?? "保存";
            if (_cmbNodeStatus != null)
            {
                int idx = _cmbNodeStatus.SelectedIndex;
                _cmbNodeStatus.Items.Clear();
                _cmbNodeStatus.Items.Add(L("Semantic.Status.Online") ?? "在线");
                _cmbNodeStatus.Items.Add(L("Semantic.Status.Offline") ?? "离线");
                _cmbNodeStatus.Items.Add(L("Semantic.Status.Stopped") ?? "停用");
                _cmbNodeStatus.Items.Add(L("Semantic.Status.Deleted") ?? "已删除");
                if (idx >= 0 && idx < 4) _cmbNodeStatus.SelectedIndex = idx;
            }
        }

        private void ApplyButtonLanguage()
        {
            // v2.6.0: 工具栏按钮已移除
        }

        /// <summary>
        /// 释放资源——清理树节点缓存，防止内存泄漏
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _allNodesCache?.Clear();
                _allNodesCache = null;
                _childrenByParentCache?.Clear();
                _childrenByParentCache = null;
            }
            base.Dispose(disposing);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  NodeKind 选择对话框
    // ════════════════════════════════════════════════════════════════

    internal class NodeKindSelectorDialog : Form
    {
        public string SelectedKind { get; private set; }

        private static readonly Tuple<string, string, string>[] KindOptions = new Tuple<string, string, string>[]
        {
            Tuple.Create("Company", "\U0001F3E2 公司", "企业最高层级"),
            Tuple.Create("Division", "\U0001F3ED 事业部", "组织架构"),
            Tuple.Create("Factory", "\U0001F3ED 工厂", "生产工厂"),
            Tuple.Create("Workshop", "\U0001F3ED 车间", "生产车间"),
            Tuple.Create("Zone", "\U0001F4CD 区域", "物理区域"),
            Tuple.Create("ProductionLine", "\u2699\uFE0F 产线", "生产流水线"),
            Tuple.Create("WorkStation", "\u2699\uFE0F 工段", "工艺工段"),
            Tuple.Create("Equipment", "\u2699\uFE0F 设备", "单台设备"),
            Tuple.Create("Variable", "\U0001F4CA 变量", "采集变量/信号"),
            Tuple.Create("Datasource", "\U0001F4BE 数据源", "外部数据源"),
            Tuple.Create("DataTable", "\U0001F4CB 数据表", "数据库表"),
            Tuple.Create("DataField", "\U0001F4CC 字段", "表字段"),
            Tuple.Create("Custom", "\U0001F4C1 自定义", "自定义节点"),
        };

        public NodeKindSelectorDialog()
        {
            Icon = Program.AppIcon;
            Text = "选择节点类型";
            Size = new Size(400, 380);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Microsoft YaHei", 9f);

            var lbl = new Label
            {
                Text = "请选择要创建的节点类型:",
                Location = new Point(16, 12),
                Size = new Size(360, 20),
                Font = Font
            };
            Controls.Add(lbl);

            var listBox = new ListBox
            {
                Location = new Point(16, 36),
                Size = new Size(360, 230),
                Font = Font,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 32
            };

            foreach (var opt in KindOptions)
            {
                listBox.Items.Add(opt);
            }

            listBox.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                e.DrawBackground();
                var item = listBox.Items[e.Index] as Tuple<string, string, string>;
                if (item == null) return;

                var g = e.Graphics;
                var iconRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 6, 20, 20);
                var titleRect = new Rectangle(e.Bounds.X + 28, e.Bounds.Y + 4, 200, 16);
                var descRect = new Rectangle(e.Bounds.X + 28, e.Bounds.Y + 18, 300, 14);

                using (var titleFont = new Font("Microsoft YaHei", 9f, FontStyle.Bold))
                using (var descFont = new Font("Microsoft YaHei", 7.5f))
                using (var descBrush = new SolidBrush(Color.FromArgb(140, 140, 140)))
                {
                    g.DrawString(item.Item2, titleFont, Brushes.Black, titleRect);
                    g.DrawString(item.Item3, descFont, descBrush, descRect);
                }

                if ((e.State & DrawItemState.Selected) != 0)
                {
                    using (var selBrush = new SolidBrush(Color.FromArgb(220, 235, 255)))
                    {
                        g.FillRectangle(selBrush, e.Bounds);
                        g.DrawString(item.Item2, new Font("Microsoft YaHei", 9f, FontStyle.Bold), Brushes.Black, titleRect);
                        g.DrawString(item.Item3, new Font("Microsoft YaHei", 7.5f),
                            new SolidBrush(Color.FromArgb(80, 80, 80)), descRect);
                    }
                }
            };

            listBox.SelectedIndexChanged += (s, e) =>
            {
                if (listBox.SelectedItem is Tuple<string, string, string> sel)
                    SelectedKind = sel.Item1;
            };

            Controls.Add(listBox);

            var btnOK = new Button
            {
                Text = "确定",
                Location = new Point(180, 280),
                Size = new Size(80, 30),
                Font = Font,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            var btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(270, 280),
                Size = new Size(80, 30),
                Font = Font,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };
            Controls.Add(btnOK);
            Controls.Add(btnCancel);

            AcceptButton = btnOK;
            CancelButton = btnCancel;

            if (KindOptions.Length > 0)
            {
                listBox.SelectedIndex = 0;
                SelectedKind = KindOptions[0].Item1;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  节点关系编辑对话框
    // ════════════════════════════════════════════════════════════════

    internal class RelationEditDialog : Form
    {
        public SemanticEquipmentRelation Result { get; private set; }

        private ComboBox _cmbTarget;
        private ComboBox _cmbType;
        private TextBox _txtDesc;
        private readonly string _sourceNodeId;
        private readonly List<SemanticNode> _allNodes;

        public RelationEditDialog(string sourceNodeId, List<SemanticNode> allNodes)
        {
            _sourceNodeId = sourceNodeId;
            _allNodes = allNodes ?? new List<SemanticNode>();

            Text = "添加节点关系";
            Size = new Size(460, 240);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Icon = Program.AppIcon;

            var sourceNode = SemanticService.Instance.GetNode(sourceNodeId);
            var sourceName = sourceNode != null ? sourceNode.Name : sourceNodeId;

            int y = 16, lblW = 80;

            // 源节点
            Controls.Add(new Label
            {
                Text = "源节点:",
                Location = new Point(16, y + 3),
                Size = new Size(lblW, 18),
                Font = Font,
                TextAlign = ContentAlignment.MiddleRight
            });
            Controls.Add(new Label
            {
                Text = sourceName,
                Location = new Point(16 + lblW + 5, y + 3),
                Size = new Size(320, 18),
                Font = Font,
                ForeColor = Color.FromArgb(0, 122, 204)
            });
            y += 30;

            // 关系类型
            Controls.Add(new Label
            {
                Text = "关系类型:",
                Location = new Point(16, y + 3),
                Size = new Size(lblW, 18),
                Font = Font,
                TextAlign = ContentAlignment.MiddleRight
            });
            _cmbType = new ComboBox
            {
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(320, 24),
                Font = Font,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbType.Items.AddRange(new[] { "上游设备", "下游设备", "供电", "供气", "供水", "控制", "监控", "备用", "组成", "通讯" });
            _cmbType.SelectedIndex = 0;
            Controls.Add(_cmbType);
            y += 30;

            // 目标节点
            Controls.Add(new Label
            {
                Text = "目标节点:",
                Location = new Point(16, y + 3),
                Size = new Size(lblW, 18),
                Font = Font,
                TextAlign = ContentAlignment.MiddleRight
            });
            _cmbTarget = new ComboBox
            {
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(320, 24),
                Font = Font,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // 填充除自身外的所有节点
            var others = _allNodes.Where(n => n.Id != sourceNodeId).OrderBy(n => n.Kind).ThenBy(n => n.Name).ToList();
            foreach (var n in others)
            {
                _cmbTarget.Items.Add(new ComboItem
                {
                    Text = string.Format("{0} {1} ({2})",
                        n.Name, NodeKind.GetDisplayName(n.Kind), n.Id),
                    Value = n.Id
                });
            }
            if (_cmbTarget.Items.Count > 0) _cmbTarget.SelectedIndex = 0;
            Controls.Add(_cmbTarget);
            y += 30;

            // 描述
            Controls.Add(new Label
            {
                Text = "描述:",
                Location = new Point(16, y + 3),
                Size = new Size(lblW, 18),
                Font = Font,
                TextAlign = ContentAlignment.MiddleRight
            });
            _txtDesc = new TextBox
            {
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(320, 24),
                Font = Font
            };
            Controls.Add(_txtDesc);
            y += 40;

            // 按钮
            var btnOK = new Button
            {
                Text = "确定",
                Location = new Point(16 + lblW + 5 + 150, y),
                Size = new Size(80, 30),
                Font = Font,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            var btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(16 + lblW + 5 + 240, y),
                Size = new Size(80, 30),
                Font = Font,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };
            Controls.Add(btnOK);
            Controls.Add(btnCancel);

            btnOK.Click += (s, e) =>
            {
                if (_cmbTarget.SelectedItem is ComboItem cbi)
                {
                    Result = new SemanticEquipmentRelation
                    {
                        SourceNodeId = _sourceNodeId,
                        TargetNodeId = cbi.Value,
                        RelationType = _cmbType.SelectedItem?.ToString() ?? "",
                        Description = _txtDesc.Text.Trim()
                    };
                }
            };
        }

        private class ComboItem
        {
            public string Text { get; set; }
            public string Value { get; set; }
            public override string ToString() { return Text; }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  事件编辑对话框
    // ════════════════════════════════════════════════════════════════

    internal class EventEditDialog : Form
    {
        public SemanticVariableEvent Result { get; private set; }

        private ComboBox _cmbNode;
        private ComboBox _cmbType;
        private ComboBox _cmbMethod;
        private DateTimePicker _dtpTime;
        private TextBox _txtDesc;
        private Panel _pnlConfig;
        private readonly string _presetNodeId;
        private readonly List<SemanticNode> _allNodes;

        // 各处理方式的配置子面板
        private Panel _pnlAlarm, _pnlNotify, _pnlInSite, _pnlEmail, _pnlSMS;
        private Panel _pnlWebhook, _pnlAPI, _pnlWorkflow, _pnlWorkOrder, _pnlMCP, _pnlAI;

        // 当前方法对应的面板缓存
        private readonly Dictionary<string, Panel> _configPanels = new Dictionary<string, Panel>();

        private static readonly string[] _allMethods = EventProcessingMethod.AllMethods;

        public EventEditDialog(string presetNodeId, List<SemanticNode> allNodes)
        {
            _presetNodeId = presetNodeId;
            _allNodes = allNodes ?? new List<SemanticNode>();

            Text = "添加节点事件";
            Size = new Size(520, 640);
            MinimumSize = new Size(480, 550);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Icon = Program.AppIcon;

            int y = 16, lblW = 80;
            int inputW = 380;

            // 节点选择
            Controls.Add(new Label { Text = "节点:", Location = new Point(16, y + 3), Size = new Size(lblW, 18), Font = Font, TextAlign = ContentAlignment.MiddleRight });
            _cmbNode = new ComboBox { Location = new Point(16 + lblW + 5, y), Size = new Size(inputW, 24), Font = Font, DropDownStyle = ComboBoxStyle.DropDownList };
            var nodeList = _allNodes.OrderBy(n => n.Kind).ThenBy(n => n.Name).ToList();
            foreach (var n in nodeList)
                _cmbNode.Items.Add(new ComboItem { Text = string.Format("{0} {1} ({2})", n.Name, NodeKind.GetDisplayName(n.Kind), n.Kind), Value = n.Id });
            if (!string.IsNullOrEmpty(presetNodeId))
            {
                for (int i = 0; i < _cmbNode.Items.Count; i++)
                    if (_cmbNode.Items[i] is ComboItem ci && ci.Value == presetNodeId) { _cmbNode.SelectedIndex = i; _cmbNode.Enabled = false; break; }
            }
            if (_cmbNode.SelectedIndex < 0 && _cmbNode.Items.Count > 0) _cmbNode.SelectedIndex = 0;
            Controls.Add(_cmbNode);
            y += 34;

            // 事件类型
            Controls.Add(new Label { Text = "事件类型:", Location = new Point(16, y + 3), Size = new Size(lblW, 18), Font = Font, TextAlign = ContentAlignment.MiddleRight });
            _cmbType = new ComboBox { Location = new Point(16 + lblW + 5, y), Size = new Size(inputW, 24), Font = Font, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var et in SemanticEventType.AllTypes) _cmbType.Items.Add(et);
            _cmbType.SelectedIndex = 0;
            Controls.Add(_cmbType);
            y += 34;

            // 处理方式
            Controls.Add(new Label { Text = "处理方式:", Location = new Point(16, y + 3), Size = new Size(lblW, 18), Font = Font, TextAlign = ContentAlignment.MiddleRight });
            _cmbMethod = new ComboBox { Location = new Point(16 + lblW + 5, y), Size = new Size(inputW, 24), Font = Font, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var m in _allMethods) _cmbMethod.Items.Add(m);
            _cmbMethod.SelectedIndex = 0;
            _cmbMethod.SelectedIndexChanged += OnMethodChanged;
            Controls.Add(_cmbMethod);
            y += 34;

            // 时间
            Controls.Add(new Label { Text = "发生时间:", Location = new Point(16, y + 3), Size = new Size(lblW, 18), Font = Font, TextAlign = ContentAlignment.MiddleRight });
            _dtpTime = new DateTimePicker { Location = new Point(16 + lblW + 5, y), Size = new Size(inputW, 24), Font = Font, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss" };
            Controls.Add(_dtpTime);
            y += 34;

            // 描述
            Controls.Add(new Label { Text = "描述:", Location = new Point(16, y + 3), Size = new Size(lblW, 18), Font = Font, TextAlign = ContentAlignment.MiddleRight });
            _txtDesc = new TextBox { Location = new Point(16 + lblW + 5, y), Size = new Size(inputW, 24), Font = Font };
            Controls.Add(_txtDesc);
            y += 36;

            // 配置面板区域
            _pnlConfig = new Panel
            {
                Location = new Point(16, y),
                Size = new Size(478, 210),
                BorderStyle = BorderStyle.FixedSingle,
                Font = Font
            };
            BuildConfigPanels(_pnlConfig);
            Controls.Add(_pnlConfig);
            y += 220;

            // 按钮
            var btnOK = new Button { Text = "确定", Location = new Point(16 + lblW + 5 + 200, y), Size = new Size(80, 32), Font = Font, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false };
            var btnCancel = new Button { Text = "取消", Location = new Point(16 + lblW + 5 + 290, y), Size = new Size(80, 32), Font = Font, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };
            Controls.Add(btnOK);
            Controls.Add(btnCancel);

            btnOK.Click += (s, e) =>
            {
                string nodeId = "";
                if (_cmbNode.SelectedItem is ComboItem cbi) nodeId = cbi.Value;
                var method = _cmbMethod.SelectedItem?.ToString() ?? "仅记录";
                var configJson = CollectConfig(method);

                Result = new SemanticVariableEvent
                {
                    NodeId = nodeId,
                    EventType = _cmbType.SelectedItem?.ToString() ?? "",
                    ProcessingMethod = method,
                    ProcessingConfig = configJson,
                    OccurredAt = _dtpTime.Value,
                    Description = _txtDesc.Text.Trim()
                };
            };
        }

        #region 配置面板构建

        private void BuildConfigPanels(Panel container)
        {
            // 无配置（仅记录）
            var pnlNone = NewConfigPanel("此处理方式无需额外配置，事件将记录到系统日志。");
            _configPanels["仅记录"] = pnlNone;

            // 报警
            _pnlAlarm = NewConfigPanel("");
            BuildAlarmPanel(_pnlAlarm);
            _configPanels["报警"] = _pnlAlarm;

            // 消息通知
            _pnlNotify = NewConfigPanel("");
            BuildNotifyPanel(_pnlNotify);
            _configPanels["消息通知"] = _pnlNotify;

            // 站内消息
            _pnlInSite = NewConfigPanel("");
            BuildInSitePanel(_pnlInSite);
            _configPanels["站内消息"] = _pnlInSite;

            // 邮件
            _pnlEmail = NewConfigPanel("");
            BuildEmailPanel(_pnlEmail);
            _configPanels["邮件"] = _pnlEmail;

            // 短信
            _pnlSMS = NewConfigPanel("");
            BuildSMSPanel(_pnlSMS);
            _configPanels["短信"] = _pnlSMS;

            // Webhook
            _pnlWebhook = NewConfigPanel("");
            BuildWebhookPanel(_pnlWebhook);
            _configPanels["Webhook"] = _pnlWebhook;

            // 调用API
            _pnlAPI = NewConfigPanel("");
            BuildAPIPanel(_pnlAPI);
            _configPanels["调用API"] = _pnlAPI;

            // 触发工作流
            _pnlWorkflow = NewConfigPanel("");
            BuildWorkflowPanel(_pnlWorkflow);
            _configPanels["触发工作流"] = _pnlWorkflow;

            // 生成工单
            _pnlWorkOrder = NewConfigPanel("");
            BuildWorkOrderPanel(_pnlWorkOrder);
            _configPanels["生成工单"] = _pnlWorkOrder;

            // 触发MCP任务
            _pnlMCP = NewConfigPanel("");
            BuildMCPPanel(_pnlMCP);
            _configPanels["触发MCP任务"] = _pnlMCP;

            // 触发AI分析
            _pnlAI = NewConfigPanel("");
            BuildAIPanel(_pnlAI);
            _configPanels["触发AI分析"] = _pnlAI;

            // 默认显示第一个
            container.Controls.Add(pnlNone);
            pnlNone.Visible = true;
        }

        private Panel NewConfigPanel(string tip)
        {
            var p = new Panel { Dock = DockStyle.Fill, Visible = false, Font = Font };
            if (!string.IsNullOrEmpty(tip))
            {
                p.Controls.Add(new Label
                {
                    Text = tip,
                    Location = new Point(12, 20),
                    Size = new Size(450, 40),
                    ForeColor = Color.Gray,
                    Font = Font
                });
            }
            return p;
        }

        private Label AddLbl(Panel p, string text, int x, int y)
        {
            var lbl = new Label { Text = text, Location = new Point(x, y + 2), Size = new Size(72, 18), Font = Font, TextAlign = ContentAlignment.MiddleRight };
            p.Controls.Add(lbl);
            return lbl;
        }
        private TextBox AddTxt(Panel p, int x, int y, int w, string placeholder = "")
        {
            var tb = new TextBox { Location = new Point(x, y), Size = new Size(w, 22), Font = Font };
            if (!string.IsNullOrEmpty(placeholder)) { tb.ForeColor = Color.Gray; tb.Text = placeholder; tb.Enter += (s, e) => { if (tb.Text == placeholder) { tb.Text = ""; tb.ForeColor = SystemColors.WindowText; } }; tb.Leave += (s, e) => { if (string.IsNullOrEmpty(tb.Text)) { tb.Text = placeholder; tb.ForeColor = Color.Gray; } }; }
            p.Controls.Add(tb);
            return tb;
        }
        private ComboBox AddCmb(Panel p, int x, int y, int w, string[] items)
        {
            var cmb = new ComboBox { Location = new Point(x, y), Size = new Size(w, 22), Font = Font, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var it in items) cmb.Items.Add(it);
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
            p.Controls.Add(cmb);
            return cmb;
        }

        // === 各处理方式配置面板 ===

        private void BuildAlarmPanel(Panel p)
        {
            AddLbl(p, "报警级别:", 8, 10);
            AddCmb(p, 80, 8, 90, new[] { "HH", "H", "L", "LL" });
            AddLbl(p, "目标:", 180, 10);
            AddTxt(p, 252, 8, 210, "{NodeName}（模板占位）");
            p.Controls.Add(new Label { Text = "可用模板: {EventType} {NodeName} {Description} {OccurredAt}", Location = new Point(10, 38), Size = new Size(450, 16), Font = new Font(Font.Name, 8f), ForeColor = Color.DarkGray });
        }

        private void BuildNotifyPanel(Panel p)
        {
            AddLbl(p, "通道:", 8, 10);
            AddTxt(p, 80, 8, 120, "default");
            AddLbl(p, "标题:", 210, 10);
            AddTxt(p, 282, 8, 180, "[通知] {EventType}");
            AddLbl(p, "正文:", 8, 38);
            AddTxt(p, 80, 36, 382, "{Description}");
        }

        private void BuildInSitePanel(Panel p)
        {
            AddLbl(p, "标题:", 8, 10);
            AddTxt(p, 80, 8, 382, "{EventType}");
            AddLbl(p, "正文:", 8, 38);
            AddTxt(p, 80, 36, 382, "{Description}");
            AddLbl(p, "级别:", 8, 66);
            AddCmb(p, 80, 64, 90, new[] { "info", "warning", "error" });
        }

        private void BuildEmailPanel(Panel p)
        {
            int y0 = 6, dy = 28;
            AddLbl(p, "收件人:", 8, y0); AddTxt(p, 80, y0 - 2, 180, "admin@example.com");
            AddLbl(p, "主题:", 270, y0); AddTxt(p, 342, y0 - 2, 120, "[{EventType}] {NodeName}");
            AddLbl(p, "正文:", 8, y0 + dy); AddTxt(p, 80, y0 + dy - 2, 382, "{Description}");
            AddLbl(p, "SMTP:", 8, y0 + dy * 2); AddTxt(p, 80, y0 + dy * 2 - 2, 130, "smtp.example.com");
            AddLbl(p, "端口:", 220, y0 + dy * 2); AddTxt(p, 280, y0 + dy * 2 - 2, 60, "25");
            AddLbl(p, "用户:", 8, y0 + dy * 3); AddTxt(p, 80, y0 + dy * 3 - 2, 160, "user@example.com");
            AddLbl(p, "密码:", 250, y0 + dy * 3); AddTxt(p, 310, y0 + dy * 3 - 2, 120, "");
            p.Controls.Add(new Label { Text = "留空 SMTP 则通过 MQTT 发送邮件请求，由外部邮件服务处理", Location = new Point(10, y0 + dy * 4 + 2), Size = new Size(450, 16), Font = new Font(Font.Name, 8f), ForeColor = Color.DarkGray });
        }

        private void BuildSMSPanel(Panel p)
        {
            int y0 = 6, dy = 28;
            AddLbl(p, "手机号:", 8, y0); AddTxt(p, 80, y0 - 2, 200, "13800138000,13900139000");
            AddLbl(p, "模板ID:", 8, y0 + dy); AddTxt(p, 80, y0 + dy - 2, 140, "SMS_123456");
            AddLbl(p, "API:", 8, y0 + dy * 2); AddTxt(p, 80, y0 + dy * 2 - 2, 250, "https://sms-gateway.example.com/send");
            AddLbl(p, "Key:", 8, y0 + dy * 3); AddTxt(p, 80, y0 + dy * 3 - 2, 250, "");
            AddLbl(p, "内容:", 8, y0 + dy * 4); AddTxt(p, 80, y0 + dy * 4 - 2, 382, "{Description}");
            p.Controls.Add(new Label { Text = "留空 API 则通过 MQTT 发送短信请求，由外部短信网关处理", Location = new Point(10, y0 + dy * 5 + 2), Size = new Size(450, 16), Font = new Font(Font.Name, 8f), ForeColor = Color.DarkGray });
        }

        private void BuildWebhookPanel(Panel p)
        {
            int y0 = 8, dy = 28;
            AddLbl(p, "URL:", 8, y0); AddTxt(p, 80, y0 - 2, 382, "https://hooks.example.com/webhook");
            AddLbl(p, "方法:", 8, y0 + dy); AddCmb(p, 80, y0 + dy - 2, 90, new[] { "POST", "PUT", "GET", "DELETE" });
            AddLbl(p, "Headers:", 8, y0 + dy * 2); AddTxt(p, 80, y0 + dy * 2 - 2, 382, "{\"Authorization\":\"Bearer xxx\"}");
            AddLbl(p, "Body:", 8, y0 + dy * 3); AddTxt(p, 80, y0 + dy * 3 - 2, 382, "{\"eventType\":\"{EventType}\",\"description\":\"{Description}\"}");
        }

        private void BuildAPIPanel(Panel p)
        {
            int y0 = 8, dy = 28;
            AddLbl(p, "URL:", 8, y0); AddTxt(p, 80, y0 - 2, 382, "https://api.example.com/endpoint");
            AddLbl(p, "方法:", 8, y0 + dy); AddCmb(p, 80, y0 + dy - 2, 90, new[] { "POST", "PUT", "GET", "DELETE" });
            AddLbl(p, "Headers:", 8, y0 + dy * 2); AddTxt(p, 80, y0 + dy * 2 - 2, 382, "{\"Authorization\":\"Bearer xxx\"}");
            AddLbl(p, "Body:", 8, y0 + dy * 3); AddTxt(p, 80, y0 + dy * 3 - 2, 382, "{\"eventType\":\"{EventType}\",\"nodeName\":\"{NodeName}\"}");
            AddLbl(p, "超时:", 8, y0 + dy * 4); AddTxt(p, 80, y0 + dy * 4 - 2, 60, "15");
            p.Controls.Add(new Label { Text = "秒", Location = new Point(144, y0 + dy * 4), Size = new Size(30, 18), Font = Font });
        }

        private void BuildWorkflowPanel(Panel p)
        {
            int y0 = 12, dy = 28;
            AddLbl(p, "工作流URL:", 8, y0); AddTxt(p, 100, y0 - 2, 360, "https://workflow.example.com/api/trigger");
            AddLbl(p, "工作流ID:", 8, y0 + dy); AddTxt(p, 100, y0 + dy - 2, 200, "wf-alarm-001");
            p.Controls.Add(new Label { Text = "触发时自动附加事件上下文（nodeId, eventType, description...）", Location = new Point(10, y0 + dy * 2 + 8), Size = new Size(450, 16), Font = new Font(Font.Name, 8f), ForeColor = Color.DarkGray });
        }

        private void BuildWorkOrderPanel(Panel p)
        {
            int y0 = 8, dy = 28;
            AddLbl(p, "标题:", 8, y0); AddTxt(p, 80, y0 - 2, 382, "[{EventType}] {NodeName}");
            AddLbl(p, "优先级:", 8, y0 + dy); AddCmb(p, 80, y0 + dy - 2, 90, new[] { "low", "normal", "high", "urgent" });
            AddLbl(p, "负责人:", 180, y0 + dy); AddTxt(p, 252, y0 + dy - 2, 210, "admin");
            AddLbl(p, "分类:", 8, y0 + dy * 2); AddCmb(p, 80, y0 + dy * 2 - 2, 130, new[] { "设备维护", "质量异常", "安全告警", "系统故障", "其他" });
            p.Controls.Add(new Label { Text = "工单将存储在本地 SQLite 并发布到 MQTT workorder/new", Location = new Point(10, y0 + dy * 3 + 6), Size = new Size(450, 16), Font = new Font(Font.Name, 8f), ForeColor = Color.DarkGray });
        }

        private void BuildMCPPanel(Panel p)
        {
            int y0 = 8, dy = 28;
            AddLbl(p, "工具名:", 8, y0); AddTxt(p, 80, y0 - 2, 200, "notify_alarm");
            AddLbl(p, "输入:", 8, y0 + dy); AddTxt(p, 80, y0 + dy - 2, 382, "{\"eventType\":\"{EventType}\",\"description\":\"{Description}\"}");
            p.Controls.Add(new Label { Text = "MCP 工具通过 McpService 注册表执行，结果写回日志", Location = new Point(10, y0 + dy * 2 + 8), Size = new Size(450, 16), Font = new Font(Font.Name, 8f), ForeColor = Color.DarkGray });
        }

        private void BuildAIPanel(Panel p)
        {
            int y0 = 8, dy = 28;
            AddLbl(p, "分析类型:", 8, y0);
            AddCmb(p, 80, y0 - 2, 130, new[] { "general", "root_cause", "prediction", "optimization", "summary" });
            AddLbl(p, "分析提示:", 8, y0 + dy);
            AddTxt(p, 80, y0 + dy - 2, 382, "节点 {NodeName} 触发事件 [{EventType}]，描述: {Description}。请分析原因并给出建议。");
            p.Controls.Add(new Label { Text = "AI 分析通过 MCP ai_analyze_event 工具执行，结果追写到事件描述", Location = new Point(10, y0 + dy * 2 + 14), Size = new Size(450, 16), Font = new Font(Font.Name, 8f), ForeColor = Color.DarkGray });
        }

        #endregion

        #region 切换与收集

        private void OnMethodChanged(object sender, EventArgs e)
        {
            var method = _cmbMethod.SelectedItem?.ToString() ?? "仅记录";
            foreach (Control c in _pnlConfig.Controls) c.Visible = false;
            if (_configPanels.TryGetValue(method, out var pnl))
            {
                if (!_pnlConfig.Controls.Contains(pnl))
                    _pnlConfig.Controls.Add(pnl);
                pnl.Visible = true;
                pnl.BringToFront();
            }
        }

        /// <summary>从当前可见配置面板收集参数，序列化为 JSON</summary>
        private string CollectConfig(string method)
        {
            var cfg = new Dictionary<string, object>();

            if (!_configPanels.TryGetValue(method, out var pnl)) return "{}";

            switch (method)
            {
                case "报警":
                    CollectFromPanel(pnl, cfg, new[] { "alarmLevel", "target" });
                    break;
                case "消息通知":
                    CollectFromPanel(pnl, cfg, new[] { "channel", "title", "body" });
                    break;
                case "站内消息":
                    CollectFromPanel(pnl, cfg, new[] { "title", "body", "level" });
                    break;
                case "邮件":
                    CollectFromPanel(pnl, cfg, new[] { "to", "subject", "body", "smtpHost", "smtpPort", "smtpUser", "smtpPass" });
                    break;
                case "短信":
                    CollectFromPanel(pnl, cfg, new[] { "phones", "templateId", "apiUrl", "apiKey", "content" });
                    break;
                case "Webhook":
                    CollectFromPanel(pnl, cfg, new[] { "url", "httpMethod", "headers", "body" });
                    break;
                case "调用API":
                    CollectFromPanel(pnl, cfg, new[] { "url", "httpMethod", "headers", "body", "timeout" });
                    break;
                case "触发工作流":
                    CollectFromPanel(pnl, cfg, new[] { "workflowUrl", "workflowId" });
                    break;
                case "生成工单":
                    CollectFromPanel(pnl, cfg, new[] { "title", "priority", "assignee", "category" });
                    break;
                case "触发MCP任务":
                    CollectFromPanel(pnl, cfg, new[] { "taskName", "taskInput" });
                    break;
                case "触发AI分析":
                    CollectFromPanel(pnl, cfg, new[] { "analysisType", "contextPrompt" });
                    break;
                default:
                    return "{}";
            }
            return JsonConvert.SerializeObject(cfg, Formatting.None);
        }

        private void CollectFromPanel(Panel pnl, Dictionary<string, object> cfg, string[] keys)
        {
            // 按照顺序从面板中提取 TextBox 和 ComboBox 的值
            int ti = 0, ci = 0;
            foreach (var key in keys)
            {
                var ctrl = FindNthInputControl(pnl, ref ti, ref ci);
                if (ctrl == null) continue;
                var val = ctrl is TextBox tb ? tb.Text.Trim()
                    : ctrl is ComboBox cmb ? (cmb.SelectedItem?.ToString() ?? "")
                    : "";
                // 过滤 placeholder 文本
                if (ctrl is TextBox t && t.ForeColor == Color.Gray) val = "";
                cfg[key] = CleanValue(key, val);
            }
        }

        private Control FindNthInputControl(Panel pnl, ref int ti, ref int ci)
        {
            int idx = ti + ci;
            int textIdx = 0, comboIdx = 0;
            foreach (Control c in pnl.Controls)
            {
                if (c is TextBox)
                {
                    if (textIdx == ti) { ti++; return c; }
                    textIdx++;
                }
                else if (c is ComboBox)
                {
                    if (comboIdx == ci) { ci++; return c; }
                    comboIdx++;
                }
            }
            return null;
        }

        private string CleanValue(string key, string val)
        {
            if (string.IsNullOrEmpty(val)) return "";
            // phones: 手机号转数组格式（存字符串，用逗号分隔）
            if (key == "phones") return val;
            return val;
        }

        #endregion

        private class ComboItem
        {
            public string Text { get; set; }
            public string Value { get; set; }
            public override string ToString() { return Text; }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  变量关系编辑对话框
    // ════════════════════════════════════════════════════════════════



    internal class VariableRelationEditDialog : Form
    {
        public SemanticVariableRelation Result { get; private set; }

        private ComboBox _cmbRType;
        private ComboBox _cmbTType;
        private ComboBox _cmbDataSource;
        private ComboBox _cmbTableName;
        private CheckedListBox _clbFields;
        private TextBox _txtConstValue;
        private TextBox _txtExpression;
        private TextBox _txtDesc;
        private Label _lblStatus;
        private HashSet<string> _selectedCondVarIds = new HashSet<string>();

        private Panel _pnlDataSource;
        private Panel _pnlConstant;
        private Panel _pnlExpression;
        private Panel _pnlVariable;
        private ComboBox _cmbVariable;

        private readonly string _variableNodeId;
        private readonly List<SemanticNode> _allNodes;
        private string _existingId; // 编辑模式下的已有关系ID

        public VariableRelationEditDialog(string variableNodeId, List<SemanticNode> allNodes)
            : this(variableNodeId, allNodes, null) { }

        public VariableRelationEditDialog(string variableNodeId, List<SemanticNode> allNodes, SemanticVariableRelation existing)
        {
            _variableNodeId = variableNodeId;
            _allNodes = allNodes ?? new List<SemanticNode>();
            bool isEdit = existing != null;
            _existingId = isEdit ? existing.Id : null;

            Text = isEdit ? "编辑变量关系" : "添加变量关系";
            Size = new Size(520, 680);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Icon = Program.AppIcon;

            int y = 12, lblW = 80, inputW = 340;

            // 关系类型
            AddLabel(this, "关系类型:", 16, y + 3, lblW);
            _cmbRType = new ComboBox
            {
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(inputW, 24),
                Font = Font,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var rt in VariableRelationType.AllTypes)
                _cmbRType.Items.Add(rt);
            if (_cmbRType.Items.Count > 0) _cmbRType.SelectedIndex = 0;
            _cmbRType.SelectedIndexChanged += OnRelationTypeChanged;
            Controls.Add(_cmbRType);
            y += 30;

            // 关系类型切换时的提示标签（独立行，不遮挡下方控件）
            var _lblRTypeHint = new Label
            {
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(inputW, 16),
                Font = new Font(Font.Name, 8f),
                ForeColor = Color.FromArgb(0, 122, 204),
                Text = ""
            };
            Controls.Add(_lblRTypeHint);
            y += 20;

            // 目标类型
            AddLabel(this, "目标类型:", 16, y + 3, lblW);
            _cmbTType = new ComboBox
            {
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(inputW, 24),
                Font = Font,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbTType.Items.AddRange(new[] { "数据源字段", "常量", "表达式", "变量" });
            _cmbTType.SelectedIndex = 0;
            _cmbTType.SelectedIndexChanged += (s, e) => UpdateTargetPanels();
            Controls.Add(_cmbTType);
            y += 34;

            // ── 面板共用起始Y（数据源/常量/表达式同一位置，只显示一个）──
            int panelPanelY = y;

            _pnlDataSource = new Panel { Location = new Point(16, panelPanelY), Size = new Size(460, 280), Visible = true };
            int py = 0;
            AddLabel(_pnlDataSource, "数据源:", 0, py + 3, lblW);
            _cmbDataSource = new ComboBox
            {
                Location = new Point(lblW + 5, py),
                Size = new Size(inputW, 24),
                Font = Font,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            // 用 SourceId（真实数据源ID）做 Value，不是 SemanticNode.Id
            var dsNodes = _allNodes.Where(n => n.Kind == NodeKind.Datasource).OrderBy(n => n.Name).ToList();
            foreach (var ds in dsNodes)
                _cmbDataSource.Items.Add(new ComboItem { Text = ds.Name, Value = ds.SourceId });
            if (_cmbDataSource.Items.Count > 0) _cmbDataSource.SelectedIndex = 0;
            _cmbDataSource.SelectedIndexChanged += OnDatasourceChanged;
            _pnlDataSource.Controls.Add(_cmbDataSource);
            py += 30;

            // 表名 — 级联筛选
            AddLabel(_pnlDataSource, "表名:", 0, py + 3, lblW);
            _cmbTableName = new ComboBox
            {
                Location = new Point(lblW + 5, py),
                Size = new Size(inputW, 24),
                Font = Font,
                DropDownStyle = ComboBoxStyle.DropDown
            };
            _cmbTableName.SelectedIndexChanged += OnTableChanged;
            _pnlDataSource.Controls.Add(_cmbTableName);
            py += 30;

            // 字段 — 多选（CheckedListBox 支持单选/多选）
            AddLabel(_pnlDataSource, "字段(可多选):", 0, py + 3, lblW);
            _clbFields = new CheckedListBox
            {
                Location = new Point(lblW + 5, py),
                Size = new Size(inputW, 130),
                Font = Font,
                CheckOnClick = true,
                IntegralHeight = false
            };
            _pnlDataSource.Controls.Add(_clbFields);
            py += 136;

            // 状态提示标签
            _lblStatus = new Label
            {
                Location = new Point(lblW + 5, py),
                Size = new Size(inputW, 18),
                Font = new Font("Microsoft YaHei", 8f, FontStyle.Italic),
                ForeColor = Color.DarkGray,
                Text = ""
            };
            _pnlDataSource.Controls.Add(_lblStatus);

            Controls.Add(_pnlDataSource);
            y += _pnlDataSource.Height;

            // ── 常量/表达式面板，与数据源面板同位置（同一时刻只显示一个）──
            _pnlConstant = new Panel { Location = new Point(16, panelPanelY), Size = new Size(460, 60), Visible = false };
            AddLabel(_pnlConstant, "常量值:", 0, 3, lblW);
            _txtConstValue = new TextBox
            {
                Location = new Point(lblW + 5, 0),
                Size = new Size(inputW, 24),
                Font = Font
            };
            _pnlConstant.Controls.Add(_txtConstValue);
            Controls.Add(_pnlConstant);

            _pnlExpression = new Panel { Location = new Point(16, panelPanelY), Size = new Size(460, 60), Visible = false };
            AddLabel(_pnlExpression, "表达式:", 0, 3, lblW);
            _txtExpression = new TextBox
            {
                Location = new Point(lblW + 5, 0),
                Size = new Size(inputW, 48),
                Font = new Font("Consolas", 9f),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            _pnlExpression.Controls.Add(_txtExpression);
            Controls.Add(_pnlExpression);

            // ── 变量选择面板（语义图谱：变量→变量关系）──
            _pnlVariable = new Panel { Location = new Point(16, panelPanelY), Size = new Size(460, 60), Visible = false };
            AddLabel(_pnlVariable, "目标变量:", 0, 3, lblW);
            _cmbVariable = new ComboBox
            {
                Location = new Point(lblW + 5, 0),
                Size = new Size(inputW, 24),
                Font = Font,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };
            var varNodes = _allNodes.Where(n => n.Kind == NodeKind.Variable).OrderBy(n => n.Name).ToList();
            foreach (var vn in varNodes)
                _cmbVariable.Items.Add(new ComboItem { Text = string.Format("{0} ({1})", vn.Name, vn.Code), Value = vn.Id });
            _pnlVariable.Controls.Add(_cmbVariable);
            Controls.Add(_pnlVariable);
            y += 8;

            // 条件变量
            var btnCondVar = new Button
            {
                Text = "选择条件变量...",
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(140, 26),
                Font = Font,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(245, 245, 245),
                UseVisualStyleBackColor = false
            };
            btnCondVar.Click += (s, e) => SelectConditionVariables();
            Controls.Add(btnCondVar);
            y += 32;

            // 描述
            AddLabel(this, "描述:", 16, y + 3, lblW);
            _txtDesc = new TextBox
            {
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(inputW, 24),
                Font = Font
            };
            Controls.Add(_txtDesc);
            y += 38;

            // 按钮行: [保存并继续] [确定] [取消]
            var btnApply = new Button
            {
                Text = "保存并继续",
                Location = new Point(16 + lblW + 5, y),
                Size = new Size(110, 30),
                Font = Font,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                UseVisualStyleBackColor = false
            };
            btnApply.Click += (s, e) =>
            {
                BuildResult();
                if (Result != null)
                {
                    SemanticService.Instance.SaveVariableRelation(Result);
                    _lblStatus.Text = "已保存，可继续添加";
                    _lblStatus.ForeColor = Color.Green;
                    // 清除已勾选的字段，防止重复保存
                    var checkedIndices = new List<int>();
                    foreach (int idx in _clbFields.CheckedIndices)
                        checkedIndices.Add(idx);
                    foreach (int idx in checkedIndices)
                        _clbFields.SetItemCheckState(idx, CheckState.Unchecked);
                    Result = null;
                }
            };
            Controls.Add(btnApply);

            var btnOK = new Button
            {
                Text = "确定",
                Location = new Point(16 + lblW + 5 + 120, y),
                Size = new Size(80, 30),
                Font = Font,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            var btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(16 + lblW + 5 + 210, y),
                Size = new Size(80, 30),
                Font = Font,
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat
            };
            Controls.Add(btnOK);
            Controls.Add(btnCancel);

            btnOK.Click += (s, e) => BuildResult();

            // 编辑模式：预填已有数据
            if (isEdit)
                PrefillFrom(existing);

            // 初始触发加载表列表
            if (_cmbDataSource.SelectedIndex >= 0)
                OnDatasourceChanged(null, null);
        }

        private void OnRelationTypeChanged(object sender, EventArgs e)
        {
            var rtype = _cmbRType.SelectedItem?.ToString() ?? "";
            var hintLabel = Controls.OfType<Label>().FirstOrDefault(l => l.ForeColor == Color.FromArgb(0, 122, 204) && l.Font.Size == 8f);

            if (rtype == "历史数据源")
            {
                // 锁定目标类型为「数据源字段」
                for (int i = 0; i < _cmbTType.Items.Count; i++)
                    if (_cmbTType.Items[i].ToString() == "数据源字段")
                    { _cmbTType.SelectedIndex = i; break; }
                _cmbTType.Enabled = false;
                if (hintLabel != null)
                    hintLabel.Text = "💡 历史数据源：绑定采集变量与存储历史数据的数据库表，AI Agent 可据此自动定位历史数据来源";
            }
            else
            {
                _cmbTType.Enabled = true;
                if (hintLabel != null) hintLabel.Text = "";
            }
        }

        private void OnDatasourceChanged(object sender, EventArgs e)
        {
            _cmbTableName.Items.Clear();
            _cmbTableName.Text = "";
            _clbFields.Items.Clear();
            _lblStatus.Text = "";

            if (!(_cmbDataSource.SelectedItem is ComboItem cbi)) return;

            try
            {
                var tables = DataSourceService.Instance.ListTables(cbi.Value);
                foreach (var t in tables)
                    _cmbTableName.Items.Add(t);
                if (tables.Count > 0)
                    _lblStatus.Text = string.Format("已加载 {0} 张表", tables.Count);
                else
                    _lblStatus.Text = "未找到表 — 请检查数据源连通状态";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "加载失败: " + ex.Message;
            }
        }

        private void OnTableChanged(object sender, EventArgs e)
        {
            _clbFields.Items.Clear();
            _lblStatus.Text = "";

            if (!(_cmbDataSource.SelectedItem is ComboItem cbi)) return;
            string tableName = _cmbTableName.Text.Trim();
            if (string.IsNullOrEmpty(tableName)) return;

            try
            {
                var cols = DataSourceService.Instance.DescribeTable(cbi.Value, tableName);
                foreach (var col in cols)
                    _clbFields.Items.Add(col.name + " (" + col.type + ")", false);
                _lblStatus.Text = string.Format("{0} 个字段", cols.Count);
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "加载字段失败: " + ex.Message;
            }
        }

        private void UpdateTargetPanels()
        {
            string sel = _cmbTType.SelectedItem?.ToString() ?? "";
            _pnlDataSource.Visible = sel == "数据源字段";
            _pnlConstant.Visible = sel == "常量";
            _pnlExpression.Visible = sel == "表达式";
            _pnlVariable.Visible = sel == "变量";
        }

        private void SelectConditionVariables()
        {
            using (var form = new Form())
            {
                form.Text = "选择条件变量（可多选）";
                form.Size = new Size(380, 350);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false; form.MinimizeBox = false;
                form.Font = new Font("Microsoft YaHei", 9f);

                var clb = new CheckedListBox
                {
                    Location = new Point(16, 12), Size = new Size(340, 240),
                    Font = Font, CheckOnClick = true
                };
                var vns = _allNodes.Where(n => n.Kind == NodeKind.Variable).OrderBy(n => n.Name).ToList();
                foreach (var vn in vns)
                    clb.Items.Add(new ComboItem { Text = vn.Name + " (" + vn.Id + ")", Value = vn.Id },
                        _selectedCondVarIds.Contains(vn.Id));
                form.Controls.Add(clb);

                var bok = new Button { Text = "确定", Location = new Point(180, 264), Size = new Size(80, 30),
                    Font = Font, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(0,122,204),
                    ForeColor = Color.White, FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false };
                var bcl = new Button { Text = "取消", Location = new Point(268, 264), Size = new Size(80, 30),
                    Font = Font, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };
                form.Controls.Add(bok); form.Controls.Add(bcl);

                if (form.ShowDialog() == DialogResult.OK)
                {
                    _selectedCondVarIds.Clear();
                    foreach (var item in clb.CheckedItems)
                        if (item is ComboItem ci) _selectedCondVarIds.Add(ci.Value);
                }
            }
        }

        private void BuildResult()
        {
            string sel = _cmbTType.SelectedItem?.ToString() ?? "数据源字段";
            string targetType = sel == "数据源字段" ? "datasource_field" : sel == "常量" ? "constant" : sel == "表达式" ? "expression" : "variable";
            string dsId = (_cmbDataSource.SelectedItem is ComboItem cbi) ? cbi.Value : "";

            var fieldNames = new List<string>();
            foreach (var item in _clbFields.CheckedItems)
            {
                string s = item.ToString(); int p = s.IndexOf('(');
                fieldNames.Add(p > 0 ? s.Substring(0, p).Trim() : s.Trim());
            }

            // 数据源字段模式：未勾选任何字段 → 不生成结果（防止"保存并继续"后再点"确定"产生空记录）
            if (targetType == "datasource_field" && fieldNames.Count == 0)
            {
                _lblStatus.Text = "请至少勾选一个字段";
                _lblStatus.ForeColor = Color.Red;
                return;
            }

            Result = new SemanticVariableRelation
            {
                Id = _existingId ?? Guid.NewGuid().ToString("N").Substring(0, 8),
                VariableNodeId = _variableNodeId,
                RelationType = _cmbRType.SelectedItem?.ToString() ?? "",
                TargetType = targetType, TargetDatasourceId = dsId,
                TargetTableName = _cmbTableName.Text.Trim(),
                TargetFieldName = string.Join(",", fieldNames),
                ConstantValue = _txtConstValue.Text.Trim(),
                Expression = _txtExpression.Text.Trim(),
                TargetVariableNodeId = (_cmbVariable?.SelectedItem is ComboItem ci2) ? ci2.Value : "",
                Description = _txtDesc.Text.Trim(),
                ConditionVariableIds = new List<string>(_selectedCondVarIds)
            };
        }

        private void PrefillFrom(SemanticVariableRelation rel)
        {
            if (rel == null) return;

            // 关系类型
            for (int i = 0; i < _cmbRType.Items.Count; i++)
            {
                if (_cmbRType.Items[i].ToString() == rel.RelationType)
                { _cmbRType.SelectedIndex = i; break; }
            }

            // 目标类型
            string ttypeDisplay = rel.TargetType == "datasource_field" ? "数据源字段"
                : rel.TargetType == "constant" ? "常量"
                : rel.TargetType == "expression" ? "表达式" : "变量";
            for (int i = 0; i < _cmbTType.Items.Count; i++)
            {
                if (_cmbTType.Items[i].ToString() == ttypeDisplay)
                { _cmbTType.SelectedIndex = i; break; }
            }

            if (rel.TargetType == "datasource_field")
            {
                // 数据源
                for (int i = 0; i < _cmbDataSource.Items.Count; i++)
                {
                    if (_cmbDataSource.Items[i] is ComboItem ci && ci.Value == rel.TargetDatasourceId)
                    { _cmbDataSource.SelectedIndex = i; break; }
                }
                // 表名和字段需要在 Shown 后设置（构造时无窗口句柄，不能用 BeginInvoke）
                if (!string.IsNullOrEmpty(rel.TargetTableName))
                {
                    string tableName = rel.TargetTableName;
                    string fieldNames = rel.TargetFieldName;
                    this.Shown += (s2, e2) =>
                    {
                        for (int i = 0; i < _cmbTableName.Items.Count; i++)
                        {
                            if (_cmbTableName.Items[i].ToString() == tableName)
                            { _cmbTableName.SelectedIndex = i; break; }
                        }
                        _cmbTableName.Text = tableName;
                        // 勾选字段
                        if (!string.IsNullOrEmpty(fieldNames))
                        {
                            var fieldList = fieldNames.Split(',').Select(f => f.Trim()).ToList();
                            for (int fi = 0; fi < _clbFields.Items.Count; fi++)
                            {
                                string itemName = _clbFields.Items[fi].ToString();
                                int p = itemName.IndexOf('(');
                                string fname = p > 0 ? itemName.Substring(0, p).Trim() : itemName.Trim();
                                if (fieldList.Contains(fname))
                                    _clbFields.SetItemCheckState(fi, CheckState.Checked);
                            }
                        }
                    };
                }
            }
            else if (rel.TargetType == "constant")
            {
                _txtConstValue.Text = rel.ConstantValue ?? "";
            }
            else if (rel.TargetType == "expression")
            {
                _txtExpression.Text = rel.Expression ?? "";
            }
            else if (rel.TargetType == "variable" && !string.IsNullOrEmpty(rel.TargetVariableNodeId))
            {
                // 选中目标变量
                for (int i = 0; i < _cmbVariable.Items.Count; i++)
                {
                    if (_cmbVariable.Items[i] is ComboItem ci && ci.Value == rel.TargetVariableNodeId)
                    { _cmbVariable.SelectedIndex = i; break; }
                }
            }

            _txtDesc.Text = rel.Description ?? "";
            if (rel.ConditionVariableIds != null && rel.ConditionVariableIds.Count > 0)
                _selectedCondVarIds = new HashSet<string>(rel.ConditionVariableIds);
        }

        private void AddLabel(Control parent, string text, int x, int y, int w)
        {
            parent.Controls.Add(new Label
            {
                Text = text, Location = new Point(x, y), Size = new Size(w, 18),
                Font = Font, TextAlign = ContentAlignment.MiddleRight
            });
        }

        private class ComboItem
        {
            public string Text { get; set; }
            public string Value { get; set; }
            public override string ToString() { return Text; }
        }
    }

    /// <summary>
    /// 影响图谱对话框 — TreeView 可视化 BFS 上下游关系
    /// </summary>
    internal class ImpactGraphDialog : Form
    {
        private TreeView _tree;
        private readonly SemanticNode _rootNode;
        private readonly Dictionary<string, string> _nameMap;

        public ImpactGraphDialog(SemanticNode rootNode, List<SemanticNode> allNodes)
        {
            _rootNode = rootNode;
            Text = string.Format("影响分析: {0}", rootNode.Name);
            Size = new Size(960, 810);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei", 12f);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true; MinimizeBox = false;
            Icon = Program.AppIcon;
            Padding = new Padding(16);

            int pad = 16, hdrH = 42, footerH = 40, btnW = 110, btnH = 32;

            var headerPanel = new Panel
            {
                Location = new Point(pad, pad),
                Size = new Size(910, hdrH),
                BackColor = Color.FromArgb(240, 248, 255),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var lblHeader = new Label
            {
                Text = string.Format("📍 {0} ({1})", rootNode.Name, rootNode.Code),
                Location = new Point(12, 10),
                Size = new Size(880, 24),
                Font = new Font("Microsoft YaHei", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 122, 204)
            };
            headerPanel.Controls.Add(lblHeader);
            Controls.Add(headerPanel);

            _tree = new TreeView
            {
                Location = new Point(pad, pad + hdrH + 10),
                Font = new Font("Microsoft YaHei", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ShowLines = true, ShowRootLines = true,
                FullRowSelect = true, HideSelection = false,
                Indent = 28
            };
            // Width/Height computed from form size minus padding and footer
            _tree.Size = new Size(910, 612);
            Controls.Add(_tree);

            var footerLabel = new Label
            {
                Text = "双击节点跳转到语义树  |  右键刷新",
                Location = new Point(pad, 710),
                Size = new Size(700, 22),
                Font = new Font("Microsoft YaHei", 9f),
                ForeColor = Color.DarkGray,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            Controls.Add(footerLabel);

            var btnClose = new Button
            {
                Text = "关闭",
                Size = new Size(btnW, btnH),
                Font = new Font("Microsoft YaHei", 11f),
                DialogResult = DialogResult.Cancel,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = SystemColors.ControlLight,
                UseVisualStyleBackColor = true
            };
            btnClose.Location = new Point(960 - btnW - 30, 706);
            Controls.Add(btnClose);

            _nameMap = new Dictionary<string, string>();
            if (allNodes != null)
            {
                foreach (var n in allNodes)
                    if (!_nameMap.ContainsKey(n.Id)) _nameMap[n.Id] = n.Name;
            }

            Load += (s, e) => BuildImpactTree();
            _tree.NodeMouseDoubleClick += (s, e) =>
            {
                if (e.Node != null && e.Node.Tag is string nodeId)
                {
                    var mainForm = Application.OpenForms.OfType<SemanticManagementForm>().FirstOrDefault();
                    if (mainForm != null)
                    {
                        mainForm.NavigateToNode(nodeId);
                        Close();
                    }
                }
            };
        }

        private void BuildImpactTree()
        {
            _tree.Nodes.Clear();
            var graph = SemanticService.Instance.GetImpactGraph(_rootNode.Id, 5);

            // 补充名称映射
            var allIds = new HashSet<string>();
            foreach (var p in graph.Upstream) allIds.Add(p.VariableNodeId);
            foreach (var p in graph.Downstream) allIds.Add(p.VariableNodeId);
            var names = SemanticService.Instance.GetVariableNames(allIds);
            foreach (var kv in names) _nameMap[kv.Key] = kv.Value;

            // 根节点
            var rootTn = new TreeNode(string.Format("📍 {0}", _rootNode.Name)) { NodeFont = new Font("Microsoft YaHei", 12f, FontStyle.Bold) };
            rootTn.ForeColor = Color.FromArgb(0, 122, 204);
            _tree.Nodes.Add(rootTn);

            // 上游依赖
            var upNode = new TreeNode(string.Format("⬆ 上游依赖 ({0})", graph.Upstream.Count))
            {
                NodeFont = new Font("Microsoft YaHei", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(156, 39, 176)
            };
            if (graph.Upstream.Count == 0)
                upNode.Nodes.Add(new TreeNode("(无 — 此变量不依赖任何其他变量)") { ForeColor = Color.DarkGray });
            else
            {
                foreach (var group in graph.Upstream.OrderBy(p => p.Depth).GroupBy(p => p.Depth))
                {
                    var depthNode = new TreeNode(string.Format("第 {0} 层", group.Key)) { ForeColor = Color.FromArgb(100, 100, 100) };
                    foreach (var p in group)
                    {
                        string vname = _nameMap.ContainsKey(p.VariableNodeId) ? _nameMap[p.VariableNodeId] : p.VariableNodeId;
                        string fname = _nameMap.ContainsKey(p.ViaSourceId) ? _nameMap[p.ViaSourceId] : p.ViaSourceId;
                        depthNode.Nodes.Add(new TreeNode(string.Format("{0} ← {1} [{2}]", vname, p.RelationType, fname))
                            { Tag = p.VariableNodeId, ToolTipText = p.RelationDescription });
                    }
                    upNode.Nodes.Add(depthNode);
                }
            }
            rootTn.Nodes.Add(upNode);

            // 下游影响
            var downNode = new TreeNode(string.Format("⬇ 下游影响 ({0})", graph.Downstream.Count))
            {
                NodeFont = new Font("Microsoft YaHei", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(233, 30, 99)
            };
            if (graph.Downstream.Count == 0)
                downNode.Nodes.Add(new TreeNode("(无 — 此变量不影响任何其他变量)") { ForeColor = Color.DarkGray });
            else
            {
                foreach (var group in graph.Downstream.OrderBy(p => p.Depth).GroupBy(p => p.Depth))
                {
                    var depthNode = new TreeNode(string.Format("第 {0} 层", group.Key)) { ForeColor = Color.FromArgb(100, 100, 100) };
                    foreach (var p in group)
                    {
                        string vname = _nameMap.ContainsKey(p.VariableNodeId) ? _nameMap[p.VariableNodeId] : p.VariableNodeId;
                        string fname = _nameMap.ContainsKey(p.ViaSourceId) ? _nameMap[p.ViaSourceId] : p.ViaSourceId;
                        depthNode.Nodes.Add(new TreeNode(string.Format("{0} → {1} [{2}]", vname, p.RelationType, fname))
                            { Tag = p.VariableNodeId, ToolTipText = p.RelationDescription });
                    }
                    downNode.Nodes.Add(depthNode);
                }
            }
            rootTn.Nodes.Add(downNode);

            // 总结
            rootTn.Nodes.Add(new TreeNode(string.Format("📊 {0} → 影响 {1} 个下游, ← 依赖 {2} 个上游",
                _rootNode.Name, graph.Downstream.Count, graph.Upstream.Count))
                { ForeColor = Color.DarkGreen, NodeFont = new Font("Microsoft YaHei", 10f, FontStyle.Italic) });

            // 默认全部展开
            rootTn.ExpandAll();
        }
    }

}
