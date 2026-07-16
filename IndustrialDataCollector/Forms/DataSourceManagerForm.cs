using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Controls;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// 数据源管理窗口 -- 管理数据库连接配置, 支持 MCP 暴露 (AI Agent 可查询)
    /// Navicat/DBeaver 风格: 左侧目录树 + 右侧配置面板
    /// </summary>
    public class DataSourceManagerForm : Form
    {
        #region 常量

        private static readonly Color AccentColor = Color.FromArgb(0, 122, 204);
        private static readonly Color TextColor = Color.FromArgb(33, 33, 33);
        private static readonly Color TestSuccessColor = Color.FromArgb(0, 168, 84);
        private static readonly Color TestFailColor = Color.FromArgb(220, 53, 69);
        private static readonly Color FolderColor = Color.FromArgb(255, 193, 7);
        private static readonly Color UntestedColor = Color.FromArgb(158, 158, 158);
        private static readonly Font UiFont = new Font("Microsoft YaHei", 9f, FontStyle.Regular);

        private const string UnGroupedId = "__ungrouped__";
        private const int LeftPanelWidth = 240;
        private const int ToolbarHeight = 36;
        private const int RowHeight = 24;
        private const int RowSpacing = 8;
        private const int LabelWidth = 80;
        private const int InputLeft = 100;
        private const int InputWidth = 280;
        private const int SectionSpacing = 16;

        #endregion

        #region 容器与控件字段

        private SplitContainer _splitContainer;
        private Button _btnAddFolder;
        private Button _btnAddConnection;
        private Button _btnDelete;
        private Button _btnExport;
        private Button _btnImport;
        private TreeView _treeView;
        private ContextMenuStrip _treeContextMenu;
        private ContextMenuStrip _ctxEmpty;  // 空白区域右键

        // 右侧配置控件
        private Panel _configPanel;
        private Label _lblName;
        private TextBox _txtName;
        private Label _lblDbType;
        private ComboBox _cmbDbType;
        private Label _lblServer;
        private TextBox _txtServer;
        private Label _lblPort;
        private NumericUpDown _numPort;
        private Label _lblDatabase;
        private TextBox _txtDatabase;
        private Label _lblUsername;
        private TextBox _txtUsername;
        private Label _lblPassword;
        private TextBox _txtPassword;
        private Button _btnTestConnection;
        private Label _lblFilePath;
        private TextBox _txtFilePath;
        private Label _lblTestStatus;

        // MCP 暴露区
        private GroupBox _grpMcp;
        private CheckBox _chkMcpEnabled;
        private Label _lblMcpAlias;
        private TextBox _txtMcpAlias;
        private Label _lblMcpPermission;
        private RadioButton _rdoReadOnly;
        private RadioButton _rdoFullControl;
        private Label _lblMaxRows;
        private NumericUpDown _numMaxRows;

        // 备注 & 按钮
        private Label _lblNotes;
        private TextBox _txtNotes;
        private Button _btnSave;
        private Button _btnCancel;
        private Button _btnBrowseFile;

        #endregion

        #region 状态

        private bool _isLoading;
        private string _editingSourceId;
        private string _newSourceFolderId; // 新建数据源时选择的归属文件夹
        private bool _hasTestedConnection;
        private List<DataSourceConnection> _allSources;
        private List<DataSourceFolder> _allFolders;
        private HashSet<string> _manualFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _searchFilter = "";  // 搜索过滤文本

        // 搜索框
        private TextBox _txtSearch;

        // SQLite 布局切换: 保存原始位置以在切换数据库类型时恢复
        private Point _filePathLabelPos;
        private Point _filePathTextBoxPos;
        private Point _notesLabelPos;
        private Point _notesTextBoxPos;
        private Point _cancelBtnPos;
        private Point _saveBtnPos;
        private int _mcpGroupBoxTop;
        private int _configPanelHeight;
        private Point _usernameLabelPos;
        private Point _usernameTextBoxPos;
        private Point _passwordLabelPos;
        private Point _passwordTextBoxPos;
        private Point _testConnectionBtnPos;
        private Point _testStatusPos;

        // Schema analysis panel (v1.8.0)
        private GroupBox _grpSchema;
        private Label _lblTableCount;
        private Button _btnRefreshSchema;
        private DataGridView _gridTables;
        private Button _btnSelectAll;
        private Button _btnDeselectAll;
        private Label _lblSchemaHint;
        private int _schemaGroupBoxTop;

        // 网络通道选择 (v2.0.0)
        private TunnelSelectionControl _tunnelControl;
        private GroupBox _grpTunnel;
        private int _tunnelGroupBoxTop;

        #endregion

        #region TreeNode 数据结构

        private enum NodeType { DataSource, Folder }

        private class TreeNodeData
        {
            public NodeType NodeType;
            public string SourceId;
            public string FolderId;
        }

        #endregion

        #region 构造

        public DataSourceManagerForm()
        {
            _isLoading = true;

            InitializeWindow();
            BuildLayout();
            InitializeTreeContextMenu();
            GenerateTreeIcons();
            ApplyGlobalFont(this);

            Load += DataSourceManagerForm_Load;
            _isLoading = false;
        }

        #endregion

        #region 窗口初始化

        private void InitializeWindow()
        {
            ClientSize = new Size(1050, 880);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(780, 520);
            BackColor = SystemColors.Control;
            Text = L("DataSourceManager.Title") ?? "数据源管理";
            Icon = Program.AppIcon;
        }

        private void BuildLayout()
        {
            _splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterWidth = 1,
                Panel1MinSize = 180,
                BackColor = Color.FromArgb(200, 200, 200),
                BorderStyle = BorderStyle.None
            };
            Controls.Add(_splitContainer);

            // Panel2MinSize 和 SplitterDistance 在 Load 中设置 (此时 Dock=Fill 已生效)
            BuildLeftPanel();
            BuildRightPanel();
        }

        #endregion

        #region 左侧面板: 搜索框 + 工具栏 + TreeView (模仿设备列表风格)

        private void BuildLeftPanel()
        {
            var left = _splitContainer.Panel1;
            left.BackColor = SystemColors.Control;

            // 搜索框
            _txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 9F),
                Height = 28
            };
            _txtSearch.TextChanged += (s, e) =>
            {
                var text = _txtSearch.Text.Trim();
                var placeholder = L("DataSourceManager_SearchPlaceholder") ?? "搜索数据源...";
                _searchFilter = (text == placeholder) ? "" : text;
                BuildTree();
            };
            _txtSearch.GotFocus += (s, e) =>
            {
                var placeholder = L("DataSourceManager_SearchPlaceholder") ?? "搜索数据源...";
                if (_txtSearch.Text == placeholder)
                {
                    _txtSearch.Text = "";
                    _txtSearch.ForeColor = SystemColors.WindowText;
                }
            };
            _txtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _txtSearch.Text = L("DataSourceManager_SearchPlaceholder") ?? "搜索数据源...";
                    _txtSearch.ForeColor = Color.Gray;
                }
            };

            // 图标按钮 (26×26，无边框透明背景)
            _btnAddFolder = new Button
            {
                Image = CreateDsFolderIcon(),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Margin = new Padding(2),
                Padding = new Padding(0),
                TabStop = false
            };
            _btnAddFolder.FlatAppearance.BorderSize = 0;
            var tooltip = new ToolTip();
            tooltip.SetToolTip(_btnAddFolder, L("DataSourceManager.NewFolderTooltip") ?? "添加文件夹");
            _btnAddFolder.Click += BtnAddFolder_Click;

            _btnAddConnection = new Button
            {
                Image = CreateDsConnectionIcon(),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Margin = new Padding(2),
                Padding = new Padding(0),
                TabStop = false
            };
            _btnAddConnection.FlatAppearance.BorderSize = 0;
            tooltip.SetToolTip(_btnAddConnection, L("DataSourceManager.NewConnectionTooltip") ?? "添加数据源连接");
            _btnAddConnection.Click += BtnAddConnection_Click;

            _btnDelete = new Button
            {
                Image = CreateDsDeleteIcon(),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Margin = new Padding(2),
                Padding = new Padding(0),
                TabStop = false
            };
            _btnDelete.FlatAppearance.BorderSize = 0;
            tooltip.SetToolTip(_btnDelete, L("DataSourceManager.Delete") ?? "删除");
            _btnDelete.Click += BtnDelete_Click;

            // TreeView
            _treeView = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 9F),
                BorderStyle = BorderStyle.None,
                HideSelection = false,
                FullRowSelect = true,
                ShowLines = true,
                ShowRootLines = false,
                AllowDrop = true,
                ItemHeight = 24,
                BackColor = Color.White,
                ForeColor = TextColor,
                Indent = 18
            };
            _treeView.AfterSelect += TreeView_AfterSelect;
            _treeView.NodeMouseDoubleClick += TreeView_NodeMouseDoubleClick;
            _treeView.MouseUp += TreeView_MouseUp;
            _treeView.KeyDown += TreeView_KeyDown;
            _treeView.ItemDrag += TreeView_ItemDrag;
            _treeView.DragEnter += TreeView_DragEnter;
            _treeView.DragOver += TreeView_DragOver;
            _treeView.DragDrop += TreeView_DragDrop;

            // 导出/导入按钮
            _btnExport = new Button
            {
                Image = CreateDsExportIcon(),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Margin = new Padding(2),
                Padding = new Padding(0),
                TabStop = false
            };
            _btnExport.FlatAppearance.BorderSize = 0;
            tooltip.SetToolTip(_btnExport, L("DataSourceManager_Export") ?? "导出配置");
            _btnExport.Click += BtnExport_Click;

            _btnImport = new Button
            {
                Image = CreateDsImportIcon(),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Margin = new Padding(2),
                Padding = new Padding(0),
                TabStop = false
            };
            _btnImport.FlatAppearance.BorderSize = 0;
            tooltip.SetToolTip(_btnImport, L("DataSourceManager_Import") ?? "导入配置");
            _btnImport.Click += BtnImport_Click;

            // TableLayoutPanel: Row0=工具栏(28px), Row1=TreeView(Fill)
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(3),
                ColumnCount = 6,
                RowCount = 2
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.Controls.Add(_btnAddFolder, 0, 0);
            table.Controls.Add(_btnAddConnection, 1, 0);
            table.Controls.Add(_btnDelete, 2, 0);
            table.Controls.Add(_btnExport, 3, 0);
            table.Controls.Add(_btnImport, 4, 0);
            table.Controls.Add(_txtSearch, 5, 0);
            table.Controls.Add(_treeView, 0, 1);
            table.SetColumnSpan(_treeView, 6);

            left.Controls.Add(table);

            // 搜索框初始 placeholder
            _txtSearch.Text = L("DataSourceManager_SearchPlaceholder") ?? "搜索数据源...";
            _txtSearch.ForeColor = Color.Gray;
        }

        #endregion

        #region 右键菜单

        private void InitializeTreeContextMenu()
        {
            _treeContextMenu = new ContextMenuStrip();

            var menuRename = new ToolStripMenuItem(L("DataSourceManager.Rename") ?? "重命名");
            menuRename.Click += MenuRename_Click;
            _treeContextMenu.Items.Add(menuRename);

            var menuDelete = new ToolStripMenuItem(L("DataSourceManager.Delete") ?? "删除");
            menuDelete.Click += MenuDelete_Click;
            _treeContextMenu.Items.Add(menuDelete);

            var menuNewSubFolder = new ToolStripMenuItem(L("DataSourceManager.NewSubFolder") ?? "新建子文件夹");
            menuNewSubFolder.Click += MenuNewSubFolder_Click;
            _treeContextMenu.Items.Add(menuNewSubFolder);

            _treeContextMenu.Items.Add(new ToolStripSeparator());

            var menuMoveTo = new ToolStripMenuItem(L("DataSourceManager.MoveToFolder") ?? "移动到文件夹");
            var menuMoveRoot = new ToolStripMenuItem("(根目录)");
            menuMoveRoot.Click += (s, e) => MoveSelectedSourceToFolder(null);
            menuMoveTo.DropDownItems.Add(menuMoveRoot);
            menuMoveTo.DropDownItems.Add(new ToolStripSeparator());
            menuMoveTo.DropDownOpening += (s, e) =>
            {
                while (menuMoveTo.DropDownItems.Count > 2)
                    menuMoveTo.DropDownItems.RemoveAt(2);
                var folders = DataSourceService.Instance.GetFolders();
                if (folders != null)
                {
                    foreach (var f in folders)
                    {
                        var fi = new ToolStripMenuItem(f.Name);
                        var captured = f.Id;
                        fi.Click += (s2, e2) => MoveSelectedSourceToFolder(captured);
                        menuMoveTo.DropDownItems.Add(fi);
                    }
                }
            };
            _treeContextMenu.Items.Add(menuMoveTo);

            _treeContextMenu.Items.Add(new ToolStripSeparator());

            var menuTest = new ToolStripMenuItem(L("DataSourceManager.TestConnection") ?? "测试连接");
            menuTest.Click += MenuTest_Click;
            _treeContextMenu.Items.Add(menuTest);

            // 空白区域右键菜单
            _ctxEmpty = new ContextMenuStrip();
            _ctxEmpty.Items.Add(L("DataSourceManager.NewFolder") ?? "新建文件夹", null, (s, e) => BtnAddFolder_Click(s, e));
            _ctxEmpty.Items.Add(L("DataSourceManager.NewConnection") ?? "新建连接", null, (s, e) => BtnAddConnection_Click(s, e));
        }

        #endregion

        #region 图标生成 (16x16 程序绘制)

        private void GenerateTreeIcons()
        {
            var imageList = new ImageList
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize = new Size(16, 16)
            };
            // 0: 文件夹, 1: 已测试绿圆, 2: 未测试灰圆, 3: 测试失败红圆
            imageList.Images.Add(DrawFolderIcon());
            imageList.Images.Add(DrawCircleIcon(TestSuccessColor));
            imageList.Images.Add(DrawCircleIcon(UntestedColor));
            imageList.Images.Add(DrawCircleIcon(TestFailColor));
            _treeView.ImageList = imageList;
        }

        private static Bitmap DrawFolderIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(FolderColor))
                using (var pen = new Pen(Color.FromArgb(200, 150, 0), 1f))
                {
                    var pts = new Point[] {
                        new Point(1, 5), new Point(6, 2),
                        new Point(9, 2), new Point(14, 5),
                        new Point(14, 13), new Point(1, 13)
                    };
                    g.FillPolygon(brush, pts);
                    g.DrawPolygon(pen, pts);
                }
            }
            return bmp;
        }

        private static Bitmap DrawCircleIcon(Color color)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(color))
                using (var pen = new Pen(Color.FromArgb(180, color), 1.5f))
                {
                    g.FillEllipse(brush, 2, 2, 12, 12);
                    g.DrawEllipse(pen, 2, 2, 12, 12);
                }
            }
            return bmp;
        }

        #endregion

        #region 右侧配置面板

        private void BuildRightPanel()
        {
            var right = _splitContainer.Panel2;
            right.BackColor = SystemColors.Control;
            right.AutoScroll = true;

            _configPanel = new Panel
            {
                Location = new Point(16, 12),
                Size = new Size(440, 600),
                BackColor = SystemColors.Control
            };
            right.Controls.Add(_configPanel);
            right.Resize += (s, e) =>
            {
                _configPanel.Width = Math.Max(420, right.Width - 32);
                if (_grpSchema != null)
                {
                    _grpSchema.Width = _configPanel.Width - 4;
                    if (_gridTables != null)
                        _gridTables.Width = _grpSchema.Width - 16;
                    if (_btnRefreshSchema != null)
                        _btnRefreshSchema.Left = _grpSchema.Width - 120;
                    if (_btnSelectAll != null)
                        _btnSelectAll.Left = _btnRefreshSchema.Left - 94;
                    if (_btnDeselectAll != null)
                        _btnDeselectAll.Left = _btnSelectAll.Left - 94;
                    if (_lblSchemaHint != null)
                        _lblSchemaHint.Width = _grpSchema.Width - 24;
                }
            };

            int y = 0;

            // ---- 连接名称 ----
            _lblName = MakeLabel(L("DataSourceManager.ConnectionName") ?? "连接名称:", 0, y + 2);
            _txtName = MakeTextBox(InputLeft, y);
            _txtName.TextChanged += (s, e) => { if (!_isLoading) OnConfigChanged(); };
            _configPanel.Controls.Add(_lblName);
            _configPanel.Controls.Add(_txtName);
            y += RowHeight + RowSpacing;

            // ---- 数据库类型 ----
            _lblDbType = MakeLabel(L("DataSourceManager.DbType") ?? "数据库类型:", 0, y + 2);
            _cmbDbType = new ComboBox
            {
                Location = new Point(InputLeft, y),
                Size = new Size(InputWidth, RowHeight),
                Font = UiFont,
                ForeColor = TextColor,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.System
            };
            _cmbDbType.Items.AddRange(new object[] { "MySQL", "SQL Server", "PostgreSQL", "TDengine", "SQLite", "Oracle", "ODBC" });
            _cmbDbType.SelectedIndex = 0;
            _cmbDbType.SelectedIndexChanged += CmbDbType_SelectedIndexChanged;
            _configPanel.Controls.Add(_lblDbType);
            _configPanel.Controls.Add(_cmbDbType);
            y += RowHeight + RowSpacing;

            // ---- 服务器 ----
            _lblServer = MakeLabel(L("DataSourceManager.Server") ?? "服务器:", 0, y + 2);
            _txtServer = MakeTextBox(InputLeft, y);
            _txtServer.TextChanged += (s, e) => { if (!_isLoading) OnConfigChanged(); };
            _configPanel.Controls.Add(_lblServer);
            _configPanel.Controls.Add(_txtServer);
            y += RowHeight + RowSpacing;

            // ---- 端口 ----
            _lblPort = MakeLabel(L("DataSourceManager.Port") ?? "端口:", 0, y + 2);
            _numPort = new NumericUpDown
            {
                Location = new Point(InputLeft, y),
                Size = new Size(100, RowHeight),
                Font = UiFont,
                ForeColor = TextColor,
                Minimum = 1,
                Maximum = 65535,
                Value = 3306
            };
            _numPort.ValueChanged += (s, e) => { if (!_isLoading) OnConfigChanged(); };
            _configPanel.Controls.Add(_lblPort);
            _configPanel.Controls.Add(_numPort);
            y += RowHeight + RowSpacing;

            // ---- 数据库名 ----
            _lblDatabase = MakeLabel(L("DataSourceManager.Database") ?? "数据库:", 0, y + 2);
            _txtDatabase = MakeTextBox(InputLeft, y);
            _txtDatabase.TextChanged += (s, e) => { if (!_isLoading) OnConfigChanged(); };
            _configPanel.Controls.Add(_lblDatabase);
            _configPanel.Controls.Add(_txtDatabase);
            y += RowHeight + RowSpacing;

            // ---- 用户名 ----
            _lblUsername = MakeLabel(L("DataSourceManager.Username") ?? "用户名:", 0, y + 2);
            _txtUsername = MakeTextBox(InputLeft, y);
            _txtUsername.TextChanged += (s, e) => { if (!_isLoading) OnConfigChanged(); };
            _configPanel.Controls.Add(_lblUsername);
            _configPanel.Controls.Add(_txtUsername);
            y += RowHeight + RowSpacing;

            // ---- 密码 + 测试连接按钮 ----
            _lblPassword = MakeLabel(L("DataSourceManager.Password") ?? "密码:", 0, y + 2);
            _txtPassword = MakeTextBox(InputLeft, y);
            _txtPassword.Width = 160;
            _txtPassword.UseSystemPasswordChar = true;
            _txtPassword.TextChanged += (s, e) => { if (!_isLoading) OnConfigChanged(); };
            _configPanel.Controls.Add(_lblPassword);
            _configPanel.Controls.Add(_txtPassword);

            _btnTestConnection = new Button
            {
                Text = L("DataSourceManager.TestConnection") ?? "测试连接",
                Location = new Point(InputLeft + 168, y),
                Size = new Size(86, RowHeight),
                FlatStyle = FlatStyle.Flat,
                Font = UiFont,
                UseVisualStyleBackColor = true
            };
            _btnTestConnection.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            _btnTestConnection.Click += BtnTestConnection_Click;
            _configPanel.Controls.Add(_btnTestConnection);

            _lblTestStatus = new Label
            {
                Location = new Point(InputLeft + 260, y + 2),
                Size = new Size(160, 20),
                Font = new Font("Microsoft YaHei", 9f),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };
            _configPanel.Controls.Add(_lblTestStatus);
            y += RowHeight + RowSpacing;

            // ---- 文件路径 (仅 SQLite) ----
            _lblFilePath = MakeLabel(L("DataSourceManager.FilePath") ?? "文件路径:", 0, y + 2);
            _lblFilePath.Visible = false;
            _txtFilePath = MakeTextBox(InputLeft, y);
            _txtFilePath.Width = InputWidth - 34;
            _txtFilePath.Visible = false;
            _txtFilePath.TextChanged += (s, e) => { if (!_isLoading) OnConfigChanged(); };
            _configPanel.Controls.Add(_lblFilePath);
            _configPanel.Controls.Add(_txtFilePath);

            _btnBrowseFile = new Button
            {
                Image = CreateDsFolderIcon(),
                Size = new Size(24, 24),
                Location = new Point(InputLeft + InputWidth - 28, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                TabStop = false,
                Visible = false
            };
            _btnBrowseFile.FlatAppearance.BorderSize = 0;
            _btnBrowseFile.Click += BtnBrowseFile_Click;
            _configPanel.Controls.Add(_btnBrowseFile);
            // y 不增加: 文件路径在服务器/端口/用户名/密码行位置显示, 由 AdjustControlsForDbType 控制

            // ---- MCP 设置 GroupBox ----
            int mcpTop = y + SectionSpacing;
            _grpMcp = new GroupBox
            {
                Text = L("DataSourceManager.ExposeToMcp") ?? "MCP 设置",
                Location = new Point(0, mcpTop),
                Size = new Size(InputLeft + InputWidth, 150),
                Font = UiFont,
                ForeColor = TextColor
            };

            int gy = 22;
            _chkMcpEnabled = new CheckBox
            {
                Text = L("DataSourceManager.ExposeToMcp") ?? "暴露给MCP (AI Agent可查询)",
                Location = new Point(12, gy),
                Size = new Size(320, 22),
                Font = UiFont,
                ForeColor = TextColor,
                Checked = false
            };
            _chkMcpEnabled.CheckedChanged += ChkMcpEnabled_CheckedChanged;
            _grpMcp.Controls.Add(_chkMcpEnabled);
            gy += 28;

            _lblMcpAlias = new Label
            {
                Text = (L("DataSourceManager.McpAlias") ?? "MCP别名") + ":",
                Location = new Point(12, gy),
                Size = new Size(70, 20),
                Font = UiFont,
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleRight
            };
            _grpMcp.Controls.Add(_lblMcpAlias);
            _txtMcpAlias = new TextBox
            {
                Location = new Point(86, gy - 1),
                Size = new Size(200, 22),
                Font = UiFont,
                ForeColor = TextColor,
                Enabled = false,
                BorderStyle = BorderStyle.FixedSingle
            };
            _txtMcpAlias.TextChanged += (s, e) => { if (!_isLoading) OnConfigChanged(); };
            _grpMcp.Controls.Add(_txtMcpAlias);
            gy += 28;

            _lblMcpPermission = new Label
            {
                Text = (L("DataSourceManager.PermissionMode") ?? "权限模式") + ":",
                Location = new Point(12, gy),
                Size = new Size(70, 20),
                Font = UiFont,
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleRight
            };
            _grpMcp.Controls.Add(_lblMcpPermission);
            _rdoReadOnly = new RadioButton
            {
                Text = L("DataSourceManager.ReadOnly") ?? "只读",
                Location = new Point(86, gy - 1),
                Size = new Size(64, 22),
                Font = UiFont,
                ForeColor = TextColor,
                Checked = true,
                Enabled = false
            };
            _rdoReadOnly.CheckedChanged += (s, e) => { if (!_isLoading) OnConfigChanged(); };
            _grpMcp.Controls.Add(_rdoReadOnly);
            _rdoFullControl = new RadioButton
            {
                Text = L("DataSourceManager.FullControl") ?? "完全控制",
                Location = new Point(154, gy - 1),
                Size = new Size(84, 22),
                Font = UiFont,
                ForeColor = TextColor,
                Checked = false,
                Enabled = false
            };
            _rdoFullControl.CheckedChanged += (s, e) => { if (!_isLoading) OnConfigChanged(); };
            _grpMcp.Controls.Add(_rdoFullControl);
            gy += 28;

            _lblMaxRows = new Label
            {
                Text = (L("DataSourceManager.MaxRows") ?? "最大行数") + ":",
                Location = new Point(12, gy),
                Size = new Size(70, 20),
                Font = UiFont,
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleRight
            };
            _grpMcp.Controls.Add(_lblMaxRows);
            _numMaxRows = new NumericUpDown
            {
                Location = new Point(86, gy - 2),
                Size = new Size(80, RowHeight),
                Font = UiFont,
                ForeColor = TextColor,
                Minimum = 0,
                Maximum = 999999,
                Value = 0,
                Enabled = false
            };
            _numMaxRows.ValueChanged += (s, e) => { if (!_isLoading) OnConfigChanged(); };
            _grpMcp.Controls.Add(_numMaxRows);
            var lblMaxHint = new Label
            {
                Text = "(0=不限制)",
                Location = new Point(172, gy),
                Size = new Size(80, 20),
                Font = new Font("Microsoft YaHei", 8f),
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _grpMcp.Controls.Add(lblMaxHint);

            _configPanel.Controls.Add(_grpMcp);
            y = mcpTop + 158;

            // ---- 网络通道选择 (v2.0.0) ----
            int tunnelTop = y + SectionSpacing;
            _grpTunnel = new GroupBox
            {
                Text = L("DataSourceManager_NetworkTunnel") ?? "网络通道",
                Location = new Point(0, tunnelTop),
                Size = new Size(InputLeft + InputWidth, 220),
                Font = UiFont,
                ForeColor = TextColor
            };
            _tunnelControl = new TunnelSelectionControl
            {
                Dock = DockStyle.Fill,
                Font = UiFont
            };
            _tunnelControl.TunnelChanged += (s, e) => OnConfigChanged();
            _tunnelControl.NewTunnelRequested += (s, e) =>
            {
                var dlg = new TunnelEditDialog();
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _tunnelControl.RefreshTunnelList();
            };
            _grpTunnel.Controls.Add(_tunnelControl);
            _configPanel.Controls.Add(_grpTunnel);
            _tunnelGroupBoxTop = tunnelTop;
            y = tunnelTop + 228;

            // ---- 表结构分析面板 (v1.8.0) ----
            int schemaTop = y + SectionSpacing;
            int schemaWidth = _configPanel.Width - 4;
            _grpSchema = new GroupBox
            {
                Text = L("DataSourceManager_TableStructure") ?? "表结构",
                Location = new Point(0, schemaTop),
                Size = new Size(schemaWidth, 270),
                Font = UiFont,
                ForeColor = TextColor
            };

            int sy = 20;
            _lblTableCount = new Label
            {
                Text = "",
                Location = new Point(12, sy),
                Size = new Size(200, 18),
                Font = new Font("Microsoft YaHei", 8.5f),
                ForeColor = Color.FromArgb(100, 100, 100),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _grpSchema.Controls.Add(_lblTableCount);

            _btnDeselectAll = new Button
            {
                Text = L("DataSourceManager_DeselectAll") ?? "取消全选",
                Location = new Point(schemaWidth - 118 - 94 - 94, sy - 4),
                Size = new Size(88, 26),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei", 8f),
                UseVisualStyleBackColor = true
            };
            _btnDeselectAll.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            _btnDeselectAll.Click += (s, ev) => SetAllTableCheckState(false);
            _btnDeselectAll.Enabled = false;
            _grpSchema.Controls.Add(_btnDeselectAll);

            _btnSelectAll = new Button
            {
                Text = L("DataSourceManager_SelectAll") ?? "全选",
                Location = new Point(schemaWidth - 118 - 94, sy - 4),
                Size = new Size(88, 26),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei", 8f),
                UseVisualStyleBackColor = true
            };
            _btnSelectAll.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            _btnSelectAll.Click += (s, ev) => SetAllTableCheckState(true);
            _btnSelectAll.Enabled = false;
            _grpSchema.Controls.Add(_btnSelectAll);

            _btnRefreshSchema = new Button
            {
                Text = L("DataSourceManager_AnalyzeSelected") ?? "分析选中表",
                Location = new Point(schemaWidth - 120, sy - 4),
                Size = new Size(108, 26),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei", 8f),
                UseVisualStyleBackColor = true
            };
            _btnRefreshSchema.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            _btnRefreshSchema.Click += BtnRefreshSchema_Click;
            _btnRefreshSchema.Enabled = false;
            _grpSchema.Controls.Add(_btnRefreshSchema);
            sy += 30;

            _gridTables = new DataGridView
            {
                Location = new Point(8, sy),
                Size = new Size(schemaWidth - 16, 190),
                Font = new Font("Microsoft YaHei", 8f),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
            _gridTables.CellDoubleClick += GridTables_CellDoubleClick;
            _gridTables.CellValueChanged += GridTables_CellValueChanged;
            _gridTables.CurrentCellDirtyStateChanged += GridTables_CurrentCellDirtyStateChanged;
            var chkCol = new DataGridViewCheckBoxColumn
            {
                Name = "col_select",
                HeaderText = "✓",
                Width = 30,
                Resizable = DataGridViewTriState.False,
                TrueValue = true,
                FalseValue = false
            };
            _gridTables.Columns.Add(chkCol);
            _gridTables.Columns.Add("col_name", "Table");
            _gridTables.Columns.Add("col_purpose", "Purpose");
            _gridTables.Columns.Add("col_rows", "Rows");
            _gridTables.Columns.Add("col_tag", "Tag");
            _gridTables.Columns.Add("col_tagcn", "TagCn");
            _gridTables.Columns["col_select"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _gridTables.Columns["col_select"].Width = 28;
            _gridTables.Columns["col_select"].MinimumWidth = 28;
            _gridTables.Columns["col_select"].Resizable = DataGridViewTriState.False;
            _gridTables.Columns["col_name"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridTables.Columns["col_name"].ReadOnly = true;
            _gridTables.Columns["col_purpose"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridTables.Columns["col_purpose"].ReadOnly = true;
            _gridTables.Columns["col_rows"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridTables.Columns["col_rows"].ReadOnly = true;
            _gridTables.Columns["col_tag"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridTables.Columns["col_tag"].ReadOnly = true;
            _gridTables.Columns["col_tagcn"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridTables.Columns["col_tagcn"].ReadOnly = false;
            _gridTables.Columns["col_name"].FillWeight = 34;
            _gridTables.Columns["col_purpose"].FillWeight = 16;
            _gridTables.Columns["col_rows"].FillWeight = 10;
            _gridTables.Columns["col_tag"].FillWeight = 16;
            _gridTables.Columns["col_tagcn"].FillWeight = 15;
            _grpSchema.Controls.Add(_gridTables);
            _btnRefreshSchema.BringToFront();  // 确保按钮不被表格遮挡
            _btnSelectAll.BringToFront();
            _btnDeselectAll.BringToFront();
            sy += 196;

            _lblSchemaHint = new Label
            {
                Text = L("DataSourceManager_AutoTagHint") ?? "连接测试成功后自动分析表结构并生成语义标签",
                Location = new Point(12, sy),
                Size = new Size(schemaWidth - 24, 16),
                Font = new Font("Microsoft YaHei", 7.5f),
                ForeColor = Color.FromArgb(140, 140, 140),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _grpSchema.Controls.Add(_lblSchemaHint);

            _configPanel.Controls.Add(_grpSchema);
            _schemaGroupBoxTop = schemaTop;
            y = schemaTop + 284;

            // ---- 备注 ----
            y += SectionSpacing;
            _lblNotes = MakeLabel(L("DataSourceManager.Notes") ?? "备注:", 0, y + 2);
            _txtNotes = new TextBox
            {
                Location = new Point(InputLeft, y),
                Size = new Size(InputWidth, 48),
                Font = UiFont,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.FixedSingle,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            _txtNotes.TextChanged += (s, e) => { if (!_isLoading) OnConfigChanged(); };
            _configPanel.Controls.Add(_lblNotes);
            _configPanel.Controls.Add(_txtNotes);
            y += 48 + RowSpacing;

            // ---- 按钮 ----
            y += 4;
            _btnCancel = new Button
            {
                Text = L("DataSourceManager.Cancel") ?? "取消",
                Location = new Point(InputLeft + InputWidth - 172, y),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                Font = UiFont,
                UseVisualStyleBackColor = true
            };
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            _btnCancel.Click += BtnCancel_Click;
            _configPanel.Controls.Add(_btnCancel);

            _btnSave = new Button
            {
                Text = L("DataSourceManager.Apply") ?? "应用",
                Location = new Point(InputLeft + InputWidth - 86, y),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                Font = UiFont,
                BackColor = AccentColor,
                ForeColor = Color.White,
                UseVisualStyleBackColor = false
            };
            _btnSave.FlatAppearance.BorderColor = AccentColor;
            _btnSave.Click += BtnSave_Click;
            _configPanel.Controls.Add(_btnSave);

            y += 44;
            _configPanel.Height = y + 8;

            // 存储原始位置以供 SQLite 切换时恢复
            _filePathLabelPos = _lblFilePath.Location;
            _filePathTextBoxPos = _txtFilePath.Location;
            _notesLabelPos = _lblNotes.Location;
            _notesTextBoxPos = _txtNotes.Location;
            _cancelBtnPos = _btnCancel.Location;
            _saveBtnPos = _btnSave.Location;
            _mcpGroupBoxTop = _grpMcp.Location.Y;
            _configPanelHeight = _configPanel.Height;
            _usernameLabelPos = _lblUsername.Location;
            _usernameTextBoxPos = _txtUsername.Location;
            _passwordLabelPos = _lblPassword.Location;
            _passwordTextBoxPos = _txtPassword.Location;
            _testConnectionBtnPos = _btnTestConnection.Location;
            _testStatusPos = _lblTestStatus.Location;
        }

        private Label MakeLabel(string fallback, int x, int y)
        {
            return new Label
            {
                Text = fallback,
                Location = new Point(x, y),
                Size = new Size(LabelWidth, 20),
                Font = UiFont,
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = false
            };
        }

        private TextBox MakeTextBox(int x, int y)
        {
            return new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(InputWidth, RowHeight),
                Font = UiFont,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private void ApplyGlobalFont(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c.Font == null || c.Font == Control.DefaultFont)
                    c.Font = UiFont;
                if (c.HasChildren)
                    ApplyGlobalFont(c);
            }
        }

        #endregion

        #region 树构建

        private void BuildTree()
        {
            _treeView.BeginUpdate();
            _treeView.Nodes.Clear();

            try
            {
                _allSources = DataSourceService.Instance.GetAll() ?? new List<DataSourceConnection>();
                _allFolders = DataSourceService.Instance.GetFolders() ?? new List<DataSourceFolder>();

                // 合并手动创建的文件夹 (尚无数据源), 并清理已有数据源的文件夹
                var mergedFolders = new List<DataSourceFolder>(_allFolders);
                if (_manualFolderNames != null && _manualFolderNames.Count > 0)
                {
                    var toRemove = new List<string>();
                    foreach (var mn in _manualFolderNames)
                    {
                        bool exists = false;
                        foreach (var f in mergedFolders)
                        {
                            if (string.Equals(f.Id, mn, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                        }
                        if (exists)
                            toRemove.Add(mn);
                        else
                            mergedFolders.Add(new DataSourceFolder { Id = mn, Name = mn });
                    }
                    foreach (var r in toRemove)
                        _manualFolderNames.Remove(r);
                }
                _allFolders = mergedFolders;

                var folderNodeMap = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
                var sourcesInFolder = new Dictionary<string, List<DataSourceConnection>>(StringComparer.OrdinalIgnoreCase);

                // 初始化文件夹容纳列表
                if (mergedFolders != null)
                {
                    foreach (var f in mergedFolders)
                    {
                        folderNodeMap[f.Id] = null;
                        sourcesInFolder[f.Id] = new List<DataSourceConnection>();
                    }
                }

                // 分配数据源到各文件夹（应用搜索过滤）
                var ungrouped = new List<DataSourceConnection>();
                if (_allSources != null)
                {
                    foreach (var src in _allSources)
                    {
                        // 搜索过滤：匹配名称
                        if (!string.IsNullOrEmpty(_searchFilter))
                        {
                            bool match = (src.Name != null && src.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                            if (!match) continue;
                        }
                        if (!string.IsNullOrEmpty(src.Folder) && folderNodeMap.ContainsKey(src.Folder))
                        {
                            sourcesInFolder[src.Folder].Add(src);
                        }
                        else
                        {
                            ungrouped.Add(src);
                        }
                    }
                }

                // 创建文件夹树节点 (分层嵌套)
                if (mergedFolders != null)
                {
                    var sortedFolders = mergedFolders.OrderBy(f => f.Id).ToList();
                    foreach (var f in sortedFolders)
                    {
                        TreeNode folderNode;
                        if (f.Id.Contains("/"))
                        {
                            string parentId = f.Id.Substring(0, f.Id.LastIndexOf('/'));
                            string displayName = f.Id.Substring(f.Id.LastIndexOf('/') + 1);
                            if (folderNodeMap.TryGetValue(parentId, out var parentNode) && parentNode != null)
                            {
                                folderNode = new TreeNode(displayName);
                                parentNode.Nodes.Add(folderNode);
                            }
                            else
                            {
                                folderNode = new TreeNode(displayName);
                                _treeView.Nodes.Add(folderNode);
                            }
                        }
                        else
                        {
                            folderNode = new TreeNode(f.Name);
                            _treeView.Nodes.Add(folderNode);
                        }
                        folderNode.Tag = new TreeNodeData { NodeType = NodeType.Folder, FolderId = f.Id };
                        folderNode.ImageIndex = 0;
                        folderNode.SelectedImageIndex = 0;
                        folderNodeMap[f.Id] = folderNode;

                        // 添加该文件夹下的数据源
                        if (sourcesInFolder.TryGetValue(f.Id, out var srcs))
                        {
                            foreach (var src in srcs)
                            {
                                int iconIdx = GetSourceImageIndex(src);
                                var childNode = new TreeNode(src.Name)
                                {
                                    Tag = new TreeNodeData { NodeType = NodeType.DataSource, SourceId = src.Id },
                                    ImageIndex = iconIdx,
                                    SelectedImageIndex = iconIdx
                                };
                                folderNode.Nodes.Add(childNode);
                            }
                        }
                    }
                }

                // 未分组节点 — 仅在有未分组数据源时显示
                if (ungrouped.Count > 0)
                {
                    var ungroupedNode = new TreeNode(L("DataSourceManager.Untested") ?? "未分组")
                    {
                        Tag = new TreeNodeData { NodeType = NodeType.Folder, FolderId = UnGroupedId },
                        ImageIndex = 0,
                        SelectedImageIndex = 0
                    };
                    foreach (var src in ungrouped)
                    {
                        int iconIdx = GetSourceImageIndex(src);
                        var childNode = new TreeNode(src.Name)
                        {
                            Tag = new TreeNodeData { NodeType = NodeType.DataSource, SourceId = src.Id },
                            ImageIndex = iconIdx,
                            SelectedImageIndex = iconIdx
                        };
                        ungroupedNode.Nodes.Add(childNode);
                    }
                    _treeView.Nodes.Add(ungroupedNode);
                }
            }
            catch (Exception ex)
            {
                Logger.Info("构建数据源树失败: " + ex.Message);
            }
            finally
            {
                _treeView.EndUpdate();
            }

            // 展开所有文件夹
            foreach (TreeNode node in _treeView.Nodes)
            {
                if (node.Tag is TreeNodeData td && td.NodeType == NodeType.Folder)
                    node.ExpandAll();
            }
        }

        private void RefreshTree()
        {
            string selectedId = GetSelectedSourceId();
            BuildTree();
            if (!string.IsNullOrEmpty(selectedId))
                SelectNodeBySourceId(selectedId);
        }

        private int GetSourceImageIndex(DataSourceConnection source)
        {
            if (source == null) return 2;
            if (source.LastTestedAt == null) return 2;
            return 1;
        }

        #endregion

        #region 树节点查找

        private TreeNode FindNodeBySourceId(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return null;
            foreach (TreeNode node in _treeView.Nodes)
            {
                var found = FindInNode(node, sourceId);
                if (found != null) return found;
            }
            return null;
        }

        private TreeNode FindInNode(TreeNode parent, string sourceId)
        {
            if (parent.Tag is TreeNodeData td && td.NodeType == NodeType.DataSource && td.SourceId == sourceId)
                return parent;
            foreach (TreeNode child in parent.Nodes)
            {
                var found = FindInNode(child, sourceId);
                if (found != null) return found;
            }
            return null;
        }

        private void SelectNodeBySourceId(string sourceId)
        {
            var node = FindNodeBySourceId(sourceId);
            if (node != null)
            {
                _treeView.SelectedNode = node;
                node.EnsureVisible();
            }
        }

        private string GetSelectedSourceId()
        {
            var node = _treeView.SelectedNode;
            if (node != null && node.Tag is TreeNodeData td && td.NodeType == NodeType.DataSource)
                return td.SourceId;
            return null;
        }

        private DataSourceConnection FindSourceById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return DataSourceService.Instance.Get(id);
        }

        #endregion

        #region 树事件

        private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_isLoading) return;
            if (e.Node != null && e.Node.Tag is TreeNodeData td)
            {
                if (td.NodeType == NodeType.DataSource)
                    LoadSourceConfiguration(td.SourceId);
                else if (td.NodeType == NodeType.Folder)
                {
                    ClearConfiguration();
                    _editingSourceId = null;
                }
            }
        }

        private void TreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node == null) return;
            if (e.Node.Tag is TreeNodeData td)
            {
                if (td.NodeType == NodeType.DataSource)
                    TestConnectionByIdAsync(td.SourceId);
                else if (td.NodeType == NodeType.Folder)
                {
                    if (e.Node.IsExpanded)
                        e.Node.Collapse();
                    else
                        e.Node.Expand();
                }
            }
        }

        private void TreeView_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var node = _treeView.GetNodeAt(e.X, e.Y);
            if (node != null)
            {
                _treeView.SelectedNode = node;
                AdjustContextMenuItems(node);
                _treeContextMenu.Show(_treeView, e.Location);
            }
            else
            {
                // 空白区域右键：建文件夹 / 建数据源
                _ctxEmpty.Show(_treeView, e.Location);
            }
        }

        private void AdjustContextMenuItems(TreeNode node)
        {
            bool isDataSource = false;
            bool isUngrouped = false;
            if (node.Tag is TreeNodeData td)
            {
                isDataSource = td.NodeType == NodeType.DataSource;
                isUngrouped = td.NodeType == NodeType.Folder && td.FolderId == UnGroupedId;
            }
            bool isFolder = !isDataSource && !isUngrouped;

            foreach (ToolStripItem item in _treeContextMenu.Items)
            {
                if (item is ToolStripMenuItem mi)
                {
                    string t = mi.Text;
                    // 移动到文件夹：仅数据源可见
                    if (t.Contains("移动到") || t.Contains("Move to"))
                        mi.Visible = isDataSource;
                    // 新建子文件夹：仅普通文件夹可见
                    if (t.Contains("新建子文件夹") || t.Contains("New Subfolder"))
                        mi.Visible = isFolder;
                    // 测试连接：仅数据源可见
                    if (t.Contains("测试连接") || t.Contains("Test Connection"))
                        mi.Visible = isDataSource;
                    // 未分组：禁止重命名/删除
                    if (isUngrouped)
                    {
                        if (t.Contains("重命名") || t.Contains("Rename") || t.Contains("删除") || t.Contains("Delete"))
                            mi.Enabled = false;
                        else
                            mi.Enabled = true;
                    }
                    else
                    {
                        mi.Enabled = true;
                    }
                }
                else if (item is ToolStripSeparator)
                {
                    // 分隔线：未分组时隐藏
                    item.Visible = !isUngrouped;
                }
            }
        }

        private void TreeView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && _treeView.SelectedNode != null)
            {
                DeleteSelectedNode();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            if (e.KeyCode == Keys.F2 && _treeView.SelectedNode != null)
            {
                RenameSelectedNode();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        #endregion

        #region 拖拽

        private void TreeView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            // 只允许拖拽数据源节点（不允许拖拽文件夹）
            if (e.Item is TreeNode node && node.Tag is TreeNodeData td
                && td.NodeType == NodeType.DataSource)
                _treeView.DoDragDrop(node, DragDropEffects.Move);
        }

        private void TreeView_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(TreeNode)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void TreeView_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(TreeNode))) { e.Effect = DragDropEffects.None; return; }

            var dragged = (TreeNode)e.Data.GetData(typeof(TreeNode));
            var dTag = dragged.Tag as TreeNodeData;
            if (dTag == null || dTag.NodeType != NodeType.DataSource)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            var pt = _treeView.PointToClient(new Point(e.X, e.Y));
            var target = _treeView.GetNodeAt(pt);

            // 数据源拖拽：可以拖到文件夹、其他数据源、或根层级
            if (target == null)
            {
                e.Effect = DragDropEffects.Move;
                return;
            }

            if (target.Tag is TreeNodeData td2 && td2.NodeType == NodeType.Folder)
                e.Effect = DragDropEffects.Move;
            else if (target.Tag is TreeNodeData td3 && td3.NodeType == NodeType.DataSource)
                e.Effect = DragDropEffects.Move;
            else
                e.Effect = DragDropEffects.None;
        }

        private void TreeView_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(TreeNode))) return;
            var dragged = (TreeNode)e.Data.GetData(typeof(TreeNode));
            if (!(dragged.Tag is TreeNodeData dTag && dTag.NodeType == NodeType.DataSource)) return;

            var pt = _treeView.PointToClient(new Point(e.X, e.Y));
            var target = _treeView.GetNodeAt(pt);
            // 拖到另一个数据源上：同层级重排序
            if (target != null && target.Tag is TreeNodeData tTd && tTd.NodeType == NodeType.DataSource && target != dragged)
            {
                var parent = target.Parent;
                if (parent == null)
                {
                    int targetIdx = _treeView.Nodes.IndexOf(target);
                    int draggedIdx = _treeView.Nodes.IndexOf(dragged);
                    _treeView.Nodes.Remove(dragged);
                    if (draggedIdx < targetIdx) targetIdx--;
                    _treeView.Nodes.Insert(targetIdx, dragged);
                }
                else
                {
                    int targetIdx = parent.Nodes.IndexOf(target);
                    int draggedIdx = parent.Nodes.IndexOf(dragged);
                    parent.Nodes.Remove(dragged);
                    if (draggedIdx < targetIdx) targetIdx--;
                    parent.Nodes.Insert(targetIdx, dragged);
                }
                _treeView.SelectedNode = dragged;
                return;
            }

            // 拖到文件夹上：移动到该文件夹
            string targetFolderId = null;
            if (target != null && target.Tag is TreeNodeData tTd2)
            {
                if (tTd2.NodeType == NodeType.Folder && tTd2.FolderId != UnGroupedId)
                    targetFolderId = tTd2.FolderId;
                else if (target.Parent != null && target.Parent.Tag is TreeNodeData pTd && pTd.NodeType == NodeType.Folder && pTd.FolderId != UnGroupedId)
                    targetFolderId = pTd.FolderId;
            }

            MoveDataSourceToFolder(dTag.SourceId, targetFolderId);
        }

        #endregion

        #region 右键菜单事件

        private void MenuRename_Click(object sender, EventArgs e) { RenameSelectedNode(); }
        private void MenuDelete_Click(object sender, EventArgs e) { DeleteSelectedNode(); }

        private void MenuTest_Click(object sender, EventArgs e)
        {
            var sid = GetSelectedSourceId();
            if (!string.IsNullOrEmpty(sid)) TestConnectionByIdAsync(sid);
        }

        private void MenuNewSubFolder_Click(object sender, EventArgs e)
        {
            var node = _treeView.SelectedNode;
            if (node == null || !(node.Tag is TreeNodeData td) || td.NodeType != NodeType.Folder || td.FolderId == UnGroupedId)
                return;

            string parentFolderId = td.FolderId;
            string parentFolderName = node.Text;
            string prompt = string.Format(L("DataSourceManager.SubFolderName") ?? "在 \"{0}\" 下创建子文件夹:", parentFolderName);

            var name = ShowInputDialog(
                L("DataSourceManager.NewFolder") ?? "新建文件夹",
                prompt,
                "");

            if (string.IsNullOrEmpty(name) || name.Trim().Length == 0) return;
            name = name.Trim();

            try
            {
                string folderId = parentFolderId + "/" + name;
                var folder = new DataSourceFolder { Id = folderId, Name = name };
                DataSourceService.Instance.SaveFolder(folder);
                _manualFolderNames.Add(folderId);
                RefreshTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show("创建文件夹失败: " + ex.Message, L("Common.Error") ?? "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RenameSelectedNode()
        {
            var node = _treeView.SelectedNode;
            if (node == null) return;

            var tag = node.Tag as TreeNodeData;
            if (tag == null) return;

            if (tag.NodeType == NodeType.Folder && tag.FolderId == UnGroupedId) return;

            var newName = ShowInputDialog(
                L("DataSourceManager.Rename") ?? "重命名",
                L("DataSourceManager.NewName") ?? "请输入新名称:",
                node.Text);

            if (string.IsNullOrEmpty(newName) || newName.Trim().Length == 0 || newName.Trim() == node.Text)
                return;

            newName = newName.Trim();
            node.Text = newName;

            try
            {
                if (tag.NodeType == NodeType.DataSource)
                {
                    var src = FindSourceById(tag.SourceId);
                    if (src != null)
                    {
                        src.Name = newName;
                        DataSourceService.Instance.Save(src);
                    }
                }
                else if (tag.NodeType == NodeType.Folder)
                {
                    DataSourceService.Instance.RenameFolder(tag.FolderId, newName);
                    RefreshTree();
                    McpDataSourceRegistry.Rebuild();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("重命名失败: " + ex.Message,
                    L("Common.Error") ?? "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteSelectedNode()
        {
            var node = _treeView.SelectedNode;
            if (node == null) return;

            var tag = node.Tag as TreeNodeData;
            if (tag == null) return;
            if (tag.NodeType == NodeType.Folder && tag.FolderId == UnGroupedId) return;

            string typeDesc = tag.NodeType == NodeType.Folder ? "文件夹" : "数据源";
            var confirm = MessageBox.Show(
                string.Format(L("DataSourceManager.ConfirmDelete") ?? "确定要删除 {0} \"{1}\" 吗？", typeDesc, node.Text),
                L("Common.Confirm") ?? "确认删除",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            // v1.9.3: 检查语义层绑定
            if (tag.NodeType == NodeType.DataSource)
            {
                var semNode = SemanticService.Instance.GetNodeBySource("datasource", tag.SourceId);
                if (semNode != null)
                {
                    int relCount, evtCount;
                    SemanticService.Instance.GetBindingCounts(semNode.Id, out relCount, out evtCount);
                    if (relCount > 0 || evtCount > 0)
                    {
                        var bindMsg = string.Format(
                            "数据源「{0}」已绑定 {1} 个关系、{2} 个事件，删除后将标记为「已删除」状态。\n确认继续删除？",
                            node.Text, relCount, evtCount);
                        var bindResult = MessageBox.Show(bindMsg,
                            L("Common.Confirm") ?? "确认删除",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (bindResult != DialogResult.Yes) return;
                    }
                }
            }

            try
            {
                if (tag.NodeType == NodeType.DataSource)
                {
                    DataSourceService.Instance.Delete(tag.SourceId);
                    if (_editingSourceId == tag.SourceId)
                    {
                        ClearConfiguration();
                        _editingSourceId = null;
                    }
                }
                else if (tag.NodeType == NodeType.Folder)
                {
                    DataSourceService.Instance.DeleteFolder(tag.FolderId);
                }
                McpDataSourceRegistry.Rebuild();
                RefreshTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show("删除失败: " + ex.Message,
                    L("Common.Error") ?? "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MoveDataSourceToFolder(string sourceId, string folderId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            try
            {
                DataSourceService.Instance.MoveToFolder(sourceId, folderId);
                McpDataSourceRegistry.Rebuild();
                RefreshTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show("移动失败: " + ex.Message,
                    L("Common.Error") ?? "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MoveSelectedSourceToFolder(string folderId)
        {
            var sid = GetSelectedSourceId();
            if (!string.IsNullOrEmpty(sid))
                MoveDataSourceToFolder(sid, folderId);
        }

        #endregion

        #region 工具栏按钮

        private void BtnAddFolder_Click(object sender, EventArgs e)
        {
            // 检测是否选中了文件夹节点以创建子文件夹
            string parentFolderId = null;
            string parentFolderName = null;
            var selNode = _treeView.SelectedNode;
            if (selNode != null && selNode.Tag is TreeNodeData td && td.NodeType == NodeType.Folder && td.FolderId != UnGroupedId)
            {
                parentFolderId = td.FolderId;
                parentFolderName = selNode.Text;
            }

            string prompt = parentFolderId != null
                ? string.Format(L("DataSourceManager.SubFolderName") ?? "在 \"{0}\" 下创建子文件夹:", parentFolderName)
                : (L("DataSourceManager.FolderName") ?? "请输入文件夹名称:");

            var name = ShowInputDialog(
                L("DataSourceManager.NewFolder") ?? "新建文件夹",
                prompt,
                "");

            if (string.IsNullOrEmpty(name) || name.Trim().Length == 0) return;
            name = name.Trim();

            try
            {
                string folderId = parentFolderId != null ? parentFolderId + "/" + name : name;
                var folder = new DataSourceFolder { Id = folderId, Name = name };
                DataSourceService.Instance.SaveFolder(folder);
                _manualFolderNames.Add(folderId);
                RefreshTree();
            }
            catch (Exception ex)
            {
                MessageBox.Show("创建文件夹失败: " + ex.Message,
                    L("Common.Error") ?? "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddConnection_Click(object sender, EventArgs e)
        {
            ClearConfiguration();
            _editingSourceId = null;
            _newSourceFolderId = null;
            _hasTestedConnection = false;
            _lblTestStatus.Visible = false;

            // 检测选中的文件夹，新数据源默认归属该文件夹
            var selNode = _treeView.SelectedNode;
            if (selNode != null && selNode.Tag is TreeNodeData td && td.NodeType == NodeType.Folder && td.FolderId != UnGroupedId)
            {
                _newSourceFolderId = td.FolderId;
            }

            _txtName.Focus();
        }

        private void BtnDelete_Click(object sender, EventArgs e) { DeleteSelectedNode(); }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "JSON files (*.json)|*.json";
                dlg.Title = L("DataSourceManager_Export") ?? "导出配置";
                dlg.FileName = "datasources_export.json";
                if (dlg.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    var sources = DataSourceService.Instance.GetAll();
                    if (sources == null)
                        sources = new List<DataSourceConnection>();
                    var folders = DataSourceService.Instance.GetFolders();
                    if (folders == null)
                        folders = new List<DataSourceFolder>();

                    var config = new
                    {
                        version = "2.0",
                        exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        dataSources = sources,
                        folders = folders
                    };

                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
                    System.IO.File.WriteAllText(dlg.FileName, json, System.Text.Encoding.UTF8);

                    MessageBox.Show(L("DataSourceManager_ExportSuccess") ?? "数据源配置已导出",
                        L("Common.Tip") ?? "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(L("DataSourceManager_ImportError") ?? "导出失败: " + ex.Message,
                        L("Common.Error") ?? "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnImport_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "JSON files (*.json)|*.json";
                dlg.Title = L("DataSourceManager_Import") ?? "导入配置";
                if (dlg.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    string json = System.IO.File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
                    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<ImportConfig>(json);
                    if (config == null || config.dataSources == null || config.dataSources.Count == 0)
                    {
                        MessageBox.Show(L("DataSourceManager_ImportNoData") ?? "文件中没有可导入的数据",
                            L("Common.Warning") ?? "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    int sourceCount = config.dataSources.Count;
                    int folderCount = config.folders != null ? config.folders.Count : 0;

                    bool isMerge = ShowImportPreviewDialog(sourceCount, folderCount);

                    if (isMerge)
                    {
                        // Merge: add new, skip duplicates by ID
                        var existingSources = DataSourceService.Instance.GetAll();
                        if (existingSources == null)
                            existingSources = new List<DataSourceConnection>();
                        var existingIds = new HashSet<string>();
                        foreach (var s in existingSources)
                        {
                            if (!string.IsNullOrEmpty(s.Id))
                                existingIds.Add(s.Id);
                        }

                        int imported = 0;
                        foreach (var src in config.dataSources)
                        {
                            if (!string.IsNullOrEmpty(src.Id) && existingIds.Contains(src.Id))
                                continue;
                            DataSourceService.Instance.Save(src);
                            imported++;
                        }

                        if (config.folders != null)
                        {
                            foreach (var folder in config.folders)
                            {
                                DataSourceService.Instance.SaveFolder(folder);
                            }
                        }

                        string msg = string.Format(L("DataSourceManager_ImportSuccess") ?? "已导入 {0} 个数据源", imported);
                        MessageBox.Show(msg, L("Common.Tip") ?? "提示",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        // Replace: clear all existing, import all
                        var existingSources = DataSourceService.Instance.GetAll();
                        if (existingSources != null)
                        {
                            var idsToDelete = new List<string>();
                            foreach (var s in existingSources)
                            {
                                if (!string.IsNullOrEmpty(s.Id))
                                    idsToDelete.Add(s.Id);
                            }
                            foreach (var id in idsToDelete)
                            {
                                DataSourceService.Instance.Delete(id);
                            }
                        }

                        foreach (var src in config.dataSources)
                        {
                            DataSourceService.Instance.Save(src);
                        }

                        if (config.folders != null)
                        {
                            foreach (var folder in config.folders)
                            {
                                DataSourceService.Instance.SaveFolder(folder);
                            }
                        }

                        string msg = string.Format(L("DataSourceManager_ImportSuccess") ?? "已导入 {0} 个数据源", config.dataSources.Count);
                        MessageBox.Show(msg, L("Common.Tip") ?? "提示",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    McpDataSourceRegistry.Rebuild();
                    RefreshTree();
                    ClearConfiguration();
                }
                catch (Exception ex)
                {
                    MessageBox.Show((L("DataSourceManager_ImportError") ?? "导入失败，请检查文件格式") + ": " + ex.Message,
                        L("Common.Error") ?? "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private bool ShowImportPreviewDialog(int sourceCount, int folderCount)
        {
            using (var form = new Form())
            {
                form.Text = L("DataSourceManager_ImportPreview") ?? "导入预览";
                form.ClientSize = new Size(380, 220);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.Font = UiFont;

                var lblTitle = new Label
                {
                    Text = L("DataSourceManager_ImportLabel") ?? "将导入以下内容：",
                    Location = new Point(16, 16),
                    Size = new Size(340, 20),
                    Font = UiFont,
                    ForeColor = TextColor
                };
                form.Controls.Add(lblTitle);

                string sourcesText = string.Format(L("DataSourceManager_ImportSources") ?? "数据源: {0} 个", sourceCount);
                var lblSources = new Label
                {
                    Text = sourcesText,
                    Location = new Point(32, 42),
                    Size = new Size(320, 18),
                    Font = new Font("Microsoft YaHei", 9f),
                    ForeColor = TextColor
                };
                form.Controls.Add(lblSources);

                if (folderCount > 0)
                {
                    string foldersText = string.Format(L("DataSourceManager_ImportFolders") ?? "文件夹: {0} 个", folderCount);
                    var lblFolders = new Label
                    {
                        Text = foldersText,
                        Location = new Point(32, 64),
                        Size = new Size(320, 18),
                        Font = new Font("Microsoft YaHei", 9f),
                        ForeColor = TextColor
                    };
                    form.Controls.Add(lblFolders);
                }

                int actionY = folderCount > 0 ? 95 : 72;
                var lblAction = new Label
                {
                    Text = (L("DataSourceManager_ImportAction") ?? "导入方式") + ":",
                    Location = new Point(16, actionY),
                    Size = new Size(70, 22),
                    Font = UiFont,
                    ForeColor = TextColor,
                    TextAlign = ContentAlignment.MiddleRight
                };
                form.Controls.Add(lblAction);

                var rdoMerge = new RadioButton
                {
                    Text = L("DataSourceManager_ImportMerge") ?? "合并导入（跳过重复）",
                    Location = new Point(92, actionY),
                    Size = new Size(260, 22),
                    Font = UiFont,
                    ForeColor = TextColor,
                    Checked = true
                };
                form.Controls.Add(rdoMerge);

                var rdoReplace = new RadioButton
                {
                    Text = L("DataSourceManager_ImportReplace") ?? "覆盖导入（清空现有）",
                    Location = new Point(92, actionY + 24),
                    Size = new Size(260, 22),
                    Font = UiFont,
                    ForeColor = TextColor,
                    Checked = false
                };
                form.Controls.Add(rdoReplace);

                int btnY = actionY + 56;
                var btnOk = new Button
                {
                    Text = L("Common.OK") ?? "确定",
                    Location = new Point(140, btnY),
                    Size = new Size(80, 28),
                    Font = UiFont,
                    FlatStyle = FlatStyle.System,
                    DialogResult = DialogResult.OK
                };
                form.Controls.Add(btnOk);

                var btnCancel = new Button
                {
                    Text = L("Common.Cancel") ?? "取消",
                    Location = new Point(230, btnY),
                    Size = new Size(80, 28),
                    Font = UiFont,
                    FlatStyle = FlatStyle.System,
                    DialogResult = DialogResult.Cancel
                };
                form.Controls.Add(btnCancel);

                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                if (form.ShowDialog(this) == DialogResult.OK)
                    return rdoMerge.Checked;
                return false; // cancelled, default to merge
            }
        }

        private class ImportConfig
        {
            public string version { get; set; }
            public string exportedAt { get; set; }
            public List<DataSourceConnection> dataSources { get; set; }
            public List<DataSourceFolder> folders { get; set; }
        }

        private void BtnBrowseFile_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "SQLite Database (*.db;*.sqlite;*.db3)|*.db;*.sqlite;*.db3|All Files (*.*)|*.*";
                dlg.Title = L("DataSourceManager.SelectDbFile") ?? "选择数据库文件";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _txtFilePath.Text = dlg.FileName;
                    OnConfigChanged();
                }
            }
        }

        #endregion

        #region 右侧配置读写

        private void LoadSourceConfiguration(string sourceId)
        {
            _isLoading = true;
            _editingSourceId = sourceId;
            _hasTestedConnection = false;

            var src = FindSourceById(sourceId);
            if (src == null)
            {
                ClearConfiguration();
                _editingSourceId = null;
                _isLoading = false;
                return;
            }

            _txtName.Text = src.Name ?? "";
            SetComboDbType(src.DbType);
            _txtServer.Text = src.Server ?? "";
            int.TryParse(src.Port, out int p);
            _numPort.Value = (p > 0 && p <= 65535) ? p : 3306;
            _txtDatabase.Text = src.Database ?? "";
            _txtUsername.Text = src.User ?? "";
            _txtPassword.Text = src.Password ?? "";
            _txtFilePath.Text = src.FilePath ?? "";
            _txtNotes.Text = src.Notes ?? "";

            // 测试状态
            if (src.LastTestedAt != null)
            {
                _hasTestedConnection = true;
                UpdateTestStatus("ok", null);
            }
            else
            {
                _lblTestStatus.Visible = false;
            }

            // MCP
            _chkMcpEnabled.Checked = src.ExposeToMcp;
            _txtMcpAlias.Text = src.McpAlias ?? "";
            _rdoReadOnly.Checked = src.PermissionMode != "fullcontrol";
            _rdoFullControl.Checked = src.PermissionMode == "fullcontrol";
            _numMaxRows.Value = Math.Max(0, Math.Min(999999, src.MaxRows));

            // v2.0.0: 网络通道
            if (_tunnelControl != null)
            {
                _tunnelControl.SetSelectedTunnel(src.TunnelId, null);
            }

            AdjustControlsForDbType();

            // v1.8.0: Restore saved schema
            if (src.Tables != null && src.Tables.Count > 0)
            {
                PopulateSchemaGrid(src.Tables);
                _btnRefreshSchema.Enabled = _btnSelectAll.Enabled = _btnDeselectAll.Enabled = true;
            }
            else
            {
                _gridTables.Rows.Clear();
                _lblTableCount.Text = "";
                _btnRefreshSchema.Enabled = _btnSelectAll.Enabled = _btnDeselectAll.Enabled = _hasTestedConnection;
            }

            _isLoading = false;
        }

        private void ClearConfiguration()
        {
            _isLoading = true;
            _editingSourceId = null;
            _hasTestedConnection = false;

            _txtName.Text = "";
            _cmbDbType.SelectedIndex = 0;
            _txtServer.Text = "";
            _numPort.Value = 3306;
            _txtDatabase.Text = "";
            _txtUsername.Text = "";
            _txtPassword.Text = "";
            _txtFilePath.Text = "";
            _txtNotes.Text = "";
            _chkMcpEnabled.Checked = false;
            _txtMcpAlias.Text = "";
            _rdoReadOnly.Checked = true;
            _rdoFullControl.Checked = false;
            _numMaxRows.Value = 0;
            _lblTestStatus.Visible = false;

            _gridTables.Rows.Clear();
            _lblTableCount.Text = "";
            _btnRefreshSchema.Enabled = _btnSelectAll.Enabled = _btnDeselectAll.Enabled = false;

            AdjustControlsForDbType();
            _isLoading = false;
        }

        private void SetComboDbType(string dbType)
        {
            for (int i = 0; i < _cmbDbType.Items.Count; i++)
            {
                if (string.Equals(_cmbDbType.Items[i] as string, dbType, StringComparison.OrdinalIgnoreCase))
                {
                    _cmbDbType.SelectedIndex = i;
                    return;
                }
            }
            _cmbDbType.SelectedIndex = 0;
        }

        private void UpdateTestStatus(string result, string errorMsg)
        {
            if (result == "ok")
            {
                _lblTestStatus.Text = "✓ " + (L("DataSourceManager.TestSuccess") ?? "已连接");
                _lblTestStatus.ForeColor = TestSuccessColor;
                _lblTestStatus.Visible = true;
            }
            else
            {
                _lblTestStatus.Text = "✗ " + (errorMsg ?? L("DataSourceManager.TestFailed") ?? "失败");
                _lblTestStatus.ForeColor = TestFailColor;
                _lblTestStatus.Visible = true;
            }
        }

        #endregion

        #region DbType 切换

        private void CmbDbType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;
            AdjustControlsForDbType();
            OnConfigChanged();
        }

        private void AdjustControlsForDbType()
        {
            string dbType = _cmbDbType.SelectedItem as string;
            bool isSqlite = dbType == "SQLite";

            // SQLite: 只隐藏 Server/Port/Database（文件型不需要），保留 Username/Password/Test（加密 SQLite 需要）
            _lblServer.Visible = !isSqlite;
            _txtServer.Visible = !isSqlite;
            _lblPort.Visible = !isSqlite;
            _numPort.Visible = !isSqlite;
            _lblDatabase.Visible = !isSqlite;
            _txtDatabase.Visible = !isSqlite;
            _lblUsername.Visible = true;
            _txtUsername.Visible = true;
            _lblPassword.Visible = true;
            _txtPassword.Visible = true;
            _btnTestConnection.Visible = true;

            _lblFilePath.Visible = isSqlite;
            _txtFilePath.Visible = isSqlite;

            // FilePath 替换 Server 行，Username/Password/Test 紧接其后
            int rowStep = RowHeight + RowSpacing;
            if (isSqlite)
            {
                int baseY = _txtServer.Location.Y;
                _lblFilePath.Location = new Point(0, _lblServer.Location.Y);
                _txtFilePath.Location = new Point(InputLeft, baseY);
                if (_btnBrowseFile != null)
                {
                    _btnBrowseFile.Location = new Point(InputLeft + InputWidth - 28, baseY);
                    _btnBrowseFile.Visible = true;
                }
                _lblUsername.Location = new Point(0, (_lblServer.Location.Y + rowStep) + 2);
                _txtUsername.Location = new Point(InputLeft, baseY + rowStep);
                _lblPassword.Location = new Point(0, (_lblServer.Location.Y + 2 * rowStep) + 2);
                _txtPassword.Location = new Point(InputLeft, baseY + 2 * rowStep);
                _btnTestConnection.Location = new Point(InputLeft + 168, baseY + 2 * rowStep);
                _lblTestStatus.Location = new Point(InputLeft + 260, baseY + 2 * rowStep + 2);

                // MCP/备注/按钮上移 2 行（Port + Database 两行不可见）
                int offset = 2 * rowStep;
                _grpMcp.Top = _mcpGroupBoxTop - offset;
                if (_grpTunnel != null) _grpTunnel.Top = _tunnelGroupBoxTop - offset;
                _grpSchema.Top = _schemaGroupBoxTop - offset;
                _lblNotes.Top = _notesLabelPos.Y - offset;
                _txtNotes.Top = _notesTextBoxPos.Y - offset;
                _btnCancel.Top = _cancelBtnPos.Y - offset;
                _btnSave.Top = _saveBtnPos.Y - offset;
                _configPanel.Height = _saveBtnPos.Y - offset + 44;
            }
            else
            {
                // 恢复非 SQLite 的原始位置
                _lblUsername.Location = _usernameLabelPos;
                _txtUsername.Location = _usernameTextBoxPos;
                _lblPassword.Location = _passwordLabelPos;
                _txtPassword.Location = _passwordTextBoxPos;
                _btnTestConnection.Location = _testConnectionBtnPos;
                _lblTestStatus.Location = _testStatusPos;
                _lblFilePath.Location = _filePathLabelPos;
                _txtFilePath.Location = _filePathTextBoxPos;
                if (_btnBrowseFile != null) _btnBrowseFile.Visible = false;
                _grpMcp.Top = _mcpGroupBoxTop;
                if (_grpTunnel != null) _grpTunnel.Top = _tunnelGroupBoxTop;
                _grpSchema.Top = _schemaGroupBoxTop;
                _lblNotes.Top = _notesLabelPos.Y;
                _txtNotes.Top = _notesTextBoxPos.Y;
                _btnCancel.Top = _cancelBtnPos.Y;
                _btnSave.Top = _saveBtnPos.Y;
                _configPanel.Height = _configPanelHeight;
            }
        }

        #endregion

        #region MCP 开关

        private void ChkMcpEnabled_CheckedChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;

            if (_chkMcpEnabled.Checked && !_hasTestedConnection)
            {
                MessageBox.Show(L("DataSourceManager.TestFirst") ?? "启用 MCP 前请先测试连接，确保数据源可用。",
                    L("Common.Tip") ?? "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _chkMcpEnabled.Checked = false;
                return;
            }

            bool en = _chkMcpEnabled.Checked;
            _txtMcpAlias.Enabled = en;
            _rdoReadOnly.Enabled = en;
            _rdoFullControl.Enabled = en;
            _numMaxRows.Enabled = en;
            OnConfigChanged();
        }

        #endregion

        #region 表结构分析 (v1.8.0)

        private async Task AnalyzeSchemaAsync(string sourceId)
        {
            _btnRefreshSchema.Enabled = false;
            _btnSelectAll.Enabled = false;
            _btnDeselectAll.Enabled = false;

            var src = FindSourceById(sourceId);
            if (src == null || src.Tables == null) { _btnRefreshSchema.Enabled = true; return; }

            // 收集已勾选的表名
            var checkedTables = new HashSet<string>();
            for (int i = 0; i < src.Tables.Count; i++)
            {
                if (src.Tables[i].IsAnalyzed)
                    checkedTables.Add(src.Tables[i].TableName);
            }

            if (checkedTables.Count == 0)
            {
                _lblTableCount.Text = L("DataSourceManager_NoTableSelected") ?? "请先勾选要分析的表";
                _lblTableCount.ForeColor = TestFailColor;
                _btnRefreshSchema.Enabled = true;
                _btnSelectAll.Enabled = _btnDeselectAll.Enabled = true;
                return;
            }

            string fmt = L("DataSourceManager_AnalyzingN") ?? "正在分析 {0} 个表...";
            _lblTableCount.Text = string.Format(fmt, checkedTables.Count);
            _lblTableCount.ForeColor = AccentColor;

            try
            {
                var analyzed = await DataSourceService.Instance.AnalyzeSourceAsync(sourceId, checkedTables);

                // 合并结果：已分析的表替换 Columns/RowCount/等，未分析的表保留原数据
                src = FindSourceById(sourceId);
                if (src != null && src.Tables != null)
                {
                    var analyzedDict = analyzed.ToDictionary(a => a.TableName);
                    foreach (var t in src.Tables)
                    {
                        if (analyzedDict.TryGetValue(t.TableName, out var newMeta))
                        {
                            t.Columns = newMeta.Columns;
                            t.RowCount = newMeta.RowCount;
                            t.Purpose = newMeta.Purpose;
                            t.Tag = newMeta.Tag;
                            t.TagCn = newMeta.TagCn ?? t.TagCn;
                        }
                    }
                    DataSourceService.Instance.Save(src);
                    PopulateSchemaGrid(src.Tables);

                    // 同步语义层
                    SemanticService.Instance.SyncFromDataSources(
                        DataSourceService.Instance.GetAll(),
                        DataSourceService.Instance);
                }
            }
            catch (Exception ex)
            {
                _lblTableCount.Text = "✗ " + ex.Message;
                _lblTableCount.ForeColor = TestFailColor;
                Logger.Info("分析表结构失败: " + ex.Message);
            }
            finally
            {
                _btnRefreshSchema.Enabled = true;
                _btnSelectAll.Enabled = _btnDeselectAll.Enabled = true;
            }
        }

        private async void BtnRefreshSchema_Click(object sender, EventArgs e)
        {
            string sourceId = _editingSourceId;
            if (string.IsNullOrEmpty(sourceId)) return;
            await AnalyzeSchemaAsync(sourceId);
        }

        private async Task LightListTablesAsync(string sourceId)
        {
            try
            {
                _lblTableCount.Text = L("DataSourceManager_Listing") ?? "正在列取表名...";
                _lblTableCount.ForeColor = AccentColor;
                var tables = await DataSourceService.Instance.ListTableMetasAsync(sourceId);
                PopulateSchemaGrid(tables);
                _btnRefreshSchema.Enabled = _btnSelectAll.Enabled = _btnDeselectAll.Enabled = true;
            }
            catch (Exception ex)
            {
                _lblTableCount.Text = "✗ " + ex.Message;
                _lblTableCount.ForeColor = TestFailColor;
            }
        }

        private void PopulateSchemaGrid(List<TableMeta> tables)
        {
            _gridTables.CellValueChanged -= GridTables_CellValueChanged;  // 暂停事件避免初始化触发
            _gridTables.Rows.Clear();

            if (tables == null || tables.Count == 0)
            {
                _lblTableCount.Text = L("DataSourceManager_NoTables") ?? "未发现表";
                _lblTableCount.ForeColor = TestFailColor;
                _btnSelectAll.Enabled = _btnDeselectAll.Enabled = false;
                _gridTables.CellValueChanged += GridTables_CellValueChanged;
                return;
            }

            int checkedCount = tables.Count(t => t.IsAnalyzed);
            string fmt = L("DataSourceManager_TablesFound") ?? "发现 {0} 个表，{1} 已选";
            _lblTableCount.Text = string.Format(fmt, tables.Count, checkedCount);
            _lblTableCount.ForeColor = TestSuccessColor;
            _btnSelectAll.Enabled = _btnDeselectAll.Enabled = true;

            foreach (var t in tables)
            {
                string purposeLabel = GetPurposeLabel(t.Purpose);
                string rowsText = t.RowCount >= 0 ? t.RowCount.ToString("N0") :
                    (t.Columns == null ? "?" : "0");
                int rowIdx = _gridTables.Rows.Add(
                    t.IsAnalyzed,                         // checkbox
                    t.TableName ?? "",
                    purposeLabel,
                    rowsText,
                    t.Tag ?? "",
                    t.TagCn ?? ""
                );
            }

            _gridTables.CellValueChanged += GridTables_CellValueChanged;
        }

        private void GridTables_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            string sourceId = _editingSourceId;
            if (string.IsNullOrEmpty(sourceId))
                return;

            var src = FindSourceById(sourceId);
            if (src == null || src.Tables == null || src.Tables.Count == 0)
                return;

            if (e.RowIndex >= src.Tables.Count)
                return;

            var tableMeta = src.Tables[e.RowIndex];
            if (tableMeta == null || tableMeta.Columns == null || tableMeta.Columns.Count == 0)
            {
                MessageBox.Show(
                    (L("DataSourceManager_NoTables") ?? "该表没有列信息"),
                    L("Common.Tip") ?? "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ShowEditColumnTagsDialog(tableMeta, src);
        }

        private void GridTables_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            string sourceId = _editingSourceId;
            if (string.IsNullOrEmpty(sourceId))
                return;

            var src = FindSourceById(sourceId);
            if (src == null || src.Tables == null || e.RowIndex >= src.Tables.Count)
                return;

            var tableMeta = src.Tables[e.RowIndex];
            if (tableMeta == null)
                return;

            var col = _gridTables.Columns[e.ColumnIndex];
            if (col != null && col.Name == "col_tagcn")
            {
                tableMeta.TagCn = _gridTables.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "";
                DataSourceService.Instance.Save(DataSourceService.Instance.Get(sourceId));
            }
            else if (col != null && col.Name == "col_select")
            {
                var cellValue = _gridTables.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                bool isChecked = cellValue != null && (bool)cellValue;
                tableMeta.IsAnalyzed = isChecked;
                DataSourceService.Instance.Save(DataSourceService.Instance.Get(sourceId));
                // 更新计数
                UpdateTableCountLabel();
            }
        }

        /// <summary>
        /// CheckBox 列即点即生效（不等焦点离开）
        /// </summary>
        private void GridTables_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (_gridTables.IsCurrentCellDirty && _gridTables.CurrentCell.ColumnIndex == 0)
                _gridTables.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void SetAllTableCheckState(bool isChecked)
        {
            string sourceId = _editingSourceId;
            if (string.IsNullOrEmpty(sourceId)) return;

            var src = FindSourceById(sourceId);
            if (src == null || src.Tables == null) return;

            foreach (var t in src.Tables) t.IsAnalyzed = isChecked;
            DataSourceService.Instance.Save(src);
            PopulateSchemaGrid(src.Tables);
        }

        private void UpdateTableCountLabel()
        {
            string sourceId = _editingSourceId;
            if (string.IsNullOrEmpty(sourceId)) return;
            var src = FindSourceById(sourceId);
            if (src == null || src.Tables == null) return;
            int total = src.Tables.Count;
            int sel = src.Tables.Count(t => t.IsAnalyzed);
            var fmt = L("DataSourceManager_TablesFound") ?? "发现 {0} 个表，{1} 已选";
            _lblTableCount.Text = string.Format(fmt, total, sel);
            _lblTableCount.ForeColor = TestSuccessColor;
        }

        private void ShowEditColumnTagsDialog(TableMeta tableMeta, DataSourceConnection source)
        {
            using (var form = new Form())
            {
                string title = string.Format("{0} - {1}",
                    L("DataSourceManager_EditTagTitle") ?? "编辑表结构标签",
                    tableMeta.TableName ?? "");
                form.Text = title;
                form.ClientSize = new Size(680, 445);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.Font = UiFont;

                var grid = new DataGridView
                {
                    Location = new Point(12, 12),
                    Size = new Size(656, 340),
                    Font = new Font("Microsoft YaHei", 8.5f),
                    BackgroundColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle,
                    RowHeadersVisible = false,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
                };

                var colName = new DataGridViewTextBoxColumn();
                colName.HeaderText = L("DataSourceManager_ColumnName") ?? "列名";
                colName.DataPropertyName = "ColumnName";
                colName.ReadOnly = true;
                colName.FillWeight = 30;
                grid.Columns.Add(colName);

                // 数据库注释列（只读，展示原生 COMMENT）
                var colComment = new DataGridViewTextBoxColumn();
                colComment.HeaderText = "数据库注释";
                colComment.DataPropertyName = "Comment";
                colComment.ReadOnly = true;
                colComment.FillWeight = 28;
                colComment.DefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 100);
                colComment.DefaultCellStyle.Font = new Font("Microsoft YaHei", 8f);
                grid.Columns.Add(colComment);

                var colType = new DataGridViewTextBoxColumn();
                colType.HeaderText = L("DataSourceManager.DbType") ?? "类型";
                colType.DataPropertyName = "DataType";
                colType.ReadOnly = true;
                colType.FillWeight = 18;
                grid.Columns.Add(colType);

                var colTag = new DataGridViewTextBoxColumn();
                colTag.HeaderText = L("DataSourceManager_Tag") ?? "Tag";
                colTag.DataPropertyName = "Tag";
                colTag.ReadOnly = false;
                colTag.FillWeight = 20;
                grid.Columns.Add(colTag);

                var colTagCn = new DataGridViewTextBoxColumn();
                colTagCn.HeaderText = L("DataSourceManager_TagCn") ?? "Tag (CN)";
                colTagCn.DataPropertyName = "TagCn";
                colTagCn.ReadOnly = false;
                colTagCn.FillWeight = 20;
                grid.Columns.Add(colTagCn);

                // Populate with current columns
                foreach (var col in tableMeta.Columns)
                {
                    int rowIdx = grid.Rows.Add(
                        col.ColumnName ?? "",
                        col.Comment ?? "",
                        col.DataType ?? "",
                        col.Tag ?? "",
                        col.TagCn ?? ""
                    );
                }

                form.Controls.Add(grid);

                var lblHint = new Label
                {
                    Text = L("DataSourceManager_TagHint") ?? "数据库注释已自动填入 Tag (CN) 列，您可手动修改",
                    Location = new Point(12, 358),
                    Size = new Size(656, 16),
                    Font = new Font("Microsoft YaHei", 7.5f),
                    ForeColor = Color.FromArgb(120, 120, 120),
                    AutoSize = false
                };
                form.Controls.Add(lblHint);

                int btnY = 382;
                var btnOk = new Button
                {
                    Text = L("Common.OK") ?? "确定",
                    Location = new Point(450, btnY),
                    Size = new Size(80, 28),
                    Font = UiFont,
                    FlatStyle = FlatStyle.System,
                    DialogResult = DialogResult.OK
                };
                form.Controls.Add(btnOk);

                var btnCancel = new Button
                {
                    Text = L("Common.Cancel") ?? "取消",
                    Location = new Point(540, btnY),
                    Size = new Size(80, 28),
                    Font = UiFont,
                    FlatStyle = FlatStyle.System,
                    DialogResult = DialogResult.Cancel
                };
                form.Controls.Add(btnCancel);

                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    // Apply edited tags back to model
                    for (int i = 0; i < tableMeta.Columns.Count && i < grid.Rows.Count; i++)
                    {
                        var row = grid.Rows[i];
                        if (row.Cells[3].Value != null)
                            tableMeta.Columns[i].Tag = row.Cells[3].Value.ToString();
                        if (row.Cells[4].Value != null)
                            tableMeta.Columns[i].TagCn = row.Cells[4].Value.ToString();
                    }

                    // Save through DataSourceService
                    DataSourceService.Instance.Save(source);
                    // 触发语义层同步
                    SemanticService.Instance.SyncFromDataSources(
                        DataSourceService.Instance.GetAll(),
                        DataSourceService.Instance);
                }
            }
        }

        private void RefreshSchemaFromSource()
        {
            string sourceId = _editingSourceId;
            if (string.IsNullOrEmpty(sourceId)) return;

            var src = FindSourceById(sourceId);
            if (src == null || src.Tables == null) return;

            PopulateSchemaGrid(src.Tables);
            _btnRefreshSchema.Enabled = _btnSelectAll.Enabled = _btnDeselectAll.Enabled = true;
        }

        private string GetPurposeLabel(string purpose)
        {
            switch (purpose)
            {
                case "history": return L("DataSourceManager_PurposeHistory") ?? "历史数据";
                case "cache": return L("DataSourceManager_PurposeCache") ?? "缓存";
                case "heartbeat": return L("DataSourceManager_PurposeHeartbeat") ?? "心跳";
                case "config": return L("DataSourceManager_PurposeConfig") ?? "配置";
                default: return L("DataSourceManager_PurposeUnknown") ?? "未知";
            }
        }

        #endregion

        #region 测试连接

        private async void BtnTestConnection_Click(object sender, EventArgs e)
        {
            var src = CollectCurrentSource();
            if (src == null) return;

            if (string.IsNullOrEmpty(src.Name))
            {
                MessageBox.Show(L("DataSourceManager.NameRequired") ?? "连接名称不能为空",
                    L("Common.Warning") ?? "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            await RunTestConnection(src);
        }

        private async void TestConnectionByIdAsync(string sourceId)
        {
            var src = FindSourceById(sourceId);
            if (src == null) return;
            await RunTestConnection(src);
        }

        private async Task RunTestConnection(DataSourceConnection source)
        {
            _btnTestConnection.Enabled = false;
            _lblTestStatus.Text = "…";
            _lblTestStatus.ForeColor = AccentColor;
            _lblTestStatus.Visible = true;

            try
            {
                string result = await DataSourceService.Instance.TestConnectionAsync(source);

                if (result == "ok")
                {
                    _hasTestedConnection = true;
                    source.LastTestedAt = DateTime.Now;
                    UpdateTestStatus("ok", null);

                    // 更新树节点图标
                    var node = FindNodeBySourceId(source.Id);
                    if (node != null)
                    {
                        node.ImageIndex = 1;
                        node.SelectedImageIndex = 1;
                    }

                    // 持久化测试时间
                    DataSourceService.Instance.Save(source);

                    // v2.5: 仅列表面名（不分析列结构），由用户勾选后手动分析
                    _ = LightListTablesAsync(source.Id);
                }
                else
                {
                    _hasTestedConnection = false;
                    UpdateTestStatus("fail", result);

                    var node = FindNodeBySourceId(source.Id);
                    if (node != null)
                    {
                        node.ImageIndex = 3;
                        node.SelectedImageIndex = 3;
                    }
                }
            }
            catch (Exception ex)
            {
                _hasTestedConnection = false;
                UpdateTestStatus("fail", ex.Message);
                Logger.Info("测试连接异常: " + ex.Message);
            }
            finally
            {
                _btnTestConnection.Enabled = true;
            }
        }

        #endregion

        #region 保存与取消

        private void BtnSave_Click(object sender, EventArgs e)
        {
            var src = CollectCurrentSource();
            if (src == null) return;

            if (string.IsNullOrEmpty(src.Name))
            {
                MessageBox.Show(L("DataSourceManager.NameRequired") ?? "连接名称不能为空",
                    L("Common.Warning") ?? "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            try
            {
                bool isNew = string.IsNullOrEmpty(_editingSourceId);
                if (isNew)
                {
                    src.Id = Guid.NewGuid().ToString("N").Substring(0, 8);
                }
                else
                {
                    src.Id = _editingSourceId;
                }

                DataSourceService.Instance.Save(src);
                McpDataSourceRegistry.Rebuild();
                _editingSourceId = src.Id;

                RefreshTree();
                SelectNodeBySourceId(src.Id);

                MessageBox.Show(L("Common.SaveSuccess") ?? "保存成功",
                    L("Common.Tip") ?? "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败: " + ex.Message,
                    L("Common.Error") ?? "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_editingSourceId))
                LoadSourceConfiguration(_editingSourceId);
            else
                ClearConfiguration();
        }

        private DataSourceConnection CollectCurrentSource()
        {
            var src = new DataSourceConnection();

            if (!string.IsNullOrEmpty(_editingSourceId))
            {
                var existing = FindSourceById(_editingSourceId);
                if (existing != null)
                {
                    src.Id = existing.Id;
                    src.LastTestedAt = existing.LastTestedAt;
                    src.Folder = existing.Folder;
                    src.Tables = existing.Tables;
                }
            }
            else
            {
                src.Folder = _newSourceFolderId ?? "";
            }

            src.Name = _txtName.Text.Trim();
            src.DbType = _cmbDbType.SelectedItem as string ?? "MySQL";
            src.Server = _txtServer.Text.Trim();
            src.Port = _numPort.Value.ToString();
            src.Database = _txtDatabase.Text.Trim();
            src.User = _txtUsername.Text.Trim();
            src.Password = _txtPassword.Text;
            src.FilePath = _txtFilePath.Text.Trim();
            src.Notes = _txtNotes.Text.Trim();

            src.ExposeToMcp = _chkMcpEnabled.Checked;
            src.McpAlias = _txtMcpAlias.Text.Trim();
            src.PermissionMode = _rdoFullControl.Checked ? "fullcontrol" : "readonly";
            src.MaxRows = (int)_numMaxRows.Value;

            // v2.0.0: 网络通道
            if (_tunnelControl != null)
            {
                src.TunnelId = _tunnelControl.SelectedTunnelId ?? "";
            }

            return src;
        }

        #endregion

        #region 工具按钮图标

        private static Bitmap CreateDsFolderIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // folder body
                using (var fill = new SolidBrush(Color.FromArgb(255, 193, 7)))
                using (var pen = new Pen(Color.FromArgb(245, 180, 5), 1f))
                {
                    g.FillRectangle(fill, 1, 3, 14, 12);
                    g.DrawLine(pen, 1, 3, 1, 15);
                    g.DrawLine(pen, 1, 15, 15, 15);
                    g.DrawLine(pen, 15, 3, 15, 15);
                    g.DrawLine(pen, 1, 3, 5, 3);
                    g.DrawLine(pen, 2, 1, 2, 3);
                    g.DrawLine(pen, 2, 1, 6, 1);
                    g.DrawLine(pen, 6, 1, 6, 3);
                    g.DrawLine(pen, 5, 3, 14, 3);
                }
                // plus sign
                using (var plusPen = new Pen(Color.White, 1.5f))
                {
                    g.DrawLine(plusPen, 8, 8, 8, 12);
                    g.DrawLine(plusPen, 6, 10, 10, 10);
                }
            }
            return bmp;
        }

        private static Bitmap CreateDsConnectionIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // database cylinder
                using (var fill = new SolidBrush(Color.FromArgb(0, 122, 204)))
                using (var pen = new Pen(Color.FromArgb(0, 105, 180), 1f))
                {
                    g.FillEllipse(fill, 3, 1, 10, 5);
                    g.DrawEllipse(pen, 3, 1, 10, 5);
                    g.FillRectangle(fill, 3, 3, 10, 8);
                    g.DrawLine(pen, 3, 4, 3, 12);
                    g.DrawLine(pen, 13, 4, 13, 12);
                    g.FillEllipse(fill, 3, 9, 10, 5);
                    g.DrawEllipse(pen, 3, 9, 10, 5);
                }
                // plus sign (white)
                using (var plusPen = new Pen(Color.White, 1.5f))
                {
                    g.DrawLine(plusPen, 8, 8, 8, 14);
                    g.DrawLine(plusPen, 5, 11, 11, 11);
                }
            }
            return bmp;
        }

        private static Bitmap CreateDsDeleteIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.FromArgb(180, 60, 60), 1.5f))
                {
                    g.DrawLine(pen, 4, 4, 12, 12);
                    g.DrawLine(pen, 12, 4, 4, 12);
                }
            }
            return bmp;
        }

        private static Bitmap CreateDsExportIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // Arrow pointing right/down (export →)
                using (var pen = new Pen(Color.FromArgb(0, 122, 204), 1.5f))
                {
                    // Arrow shaft
                    g.DrawLine(pen, 2, 8, 11, 8);
                    // Arrow head
                    g.DrawLine(pen, 8, 4, 13, 8);
                    g.DrawLine(pen, 8, 12, 13, 8);
                    // Box (destination)
                    g.DrawLine(pen, 13, 2, 13, 14);
                }
            }
            return bmp;
        }

        private static Bitmap CreateDsImportIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // Arrow pointing left/down (import ←)
                using (var pen = new Pen(Color.FromArgb(0, 168, 84), 1.5f))
                {
                    // Arrow shaft
                    g.DrawLine(pen, 4, 8, 13, 8);
                    // Arrow head (pointing left)
                    g.DrawLine(pen, 3, 4, 3, 12);
                    g.DrawLine(pen, 3, 4, 6, 7);
                }
            }
            return bmp;
        }

        #endregion

        #region InputDialog 辅助

        private string ShowInputDialog(string title, string prompt, string defaultValue)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.ClientSize = new Size(360, 140);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = false;
                form.Font = UiFont;

                var lblPrompt = new Label
                {
                    Text = prompt,
                    Location = new Point(16, 16),
                    Size = new Size(320, 20),
                    Font = UiFont,
                    ForeColor = TextColor,
                    AutoSize = false
                };
                form.Controls.Add(lblPrompt);

                var txtInput = new TextBox
                {
                    Location = new Point(16, 44),
                    Size = new Size(320, 24),
                    Font = UiFont,
                    Text = defaultValue ?? ""
                };
                form.Controls.Add(txtInput);

                var btnOk = new Button
                {
                    Text = L("Common.OK") ?? "确定",
                    Location = new Point(176, 80),
                    Size = new Size(75, 28),
                    Font = UiFont,
                    FlatStyle = FlatStyle.System,
                    DialogResult = DialogResult.OK
                };
                form.Controls.Add(btnOk);

                var btnCancel = new Button
                {
                    Text = L("Common.Cancel") ?? "取消",
                    Location = new Point(258, 80),
                    Size = new Size(75, 28),
                    Font = UiFont,
                    FlatStyle = FlatStyle.System,
                    DialogResult = DialogResult.Cancel
                };
                form.Controls.Add(btnCancel);

                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;
                form.ActiveControl = txtInput;
                txtInput.SelectAll();

                return form.ShowDialog(this) == DialogResult.OK ? txtInput.Text : null;
            }
        }

        #endregion

        #region 辅助

        private static string L(string key)
        {
            try
            {
                var s = LanguageManager.Instance.GetString(key);
                return s;
            }
            catch
            {
                return null;
            }
        }

        private void OnConfigChanged() { }

        #endregion

        #region 生命周期

        private void DataSourceManagerForm_Load(object sender, EventArgs e)
        {
            try
            {
                _splitContainer.Panel2MinSize = 400;
                _splitContainer.SplitterDistance = LeftPanelWidth;

                _isLoading = true;
                BuildTree();
                ClearConfiguration();
            }
            catch (Exception ex)
            {
                Logger.Info("初始化数据源管理窗口失败: " + ex.Message);
            }
            finally
            {
                _isLoading = false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UiFont?.Dispose();
                _treeContextMenu?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}