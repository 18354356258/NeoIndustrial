using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using IndustrialDataCollection.Drivers;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// 主界面 - 设备列表 + 实时数据监控 + 状态栏
    /// </summary>
    public partial class MainForm : Form
    {
        private List<DeviceConfig> _devices = new List<DeviceConfig>();
        private Timer _statusTimer;
        private Timer _monitorTimer;
        private int _lastSampleCount = 0;
        private RestApiService _apiService;
        private McpService _mcpService;
        private ToolStripMenuItem _mcpMenuItem; // 程序化添加的MCP菜单项
        private ToolStripMenuItem _dsMenuItem; // 数据源管理菜单项
        private ToolStripMenuItem _dashboardMenuItem; // 看板导航菜单项
        private SemanticManagementForm _semanticForm; // 语义管理窗体引用
        private NotifyIcon _trayIcon;
        private bool _isReallyExiting = false;

        // 监控网格缓存: key="deviceId_variableName" → row index
        private readonly Dictionary<string, int> _gridRowMap = new Dictionary<string, int>();
        private int _lastGridDeviceCount = 0;
        private int _lastGridVarCount = 0;

        // 设备树
        private TreeView treeViewDevices;
        private TextBox txtDeviceSearch;
        private string _selectedDeviceId = null;
        private string _searchFilter = "";
        private ContextMenuStrip _ctxEmpty;
        /// <summary>分组信息字典（key=Path, value=DeviceGroupInfo），v2.6.1 变更为结构化模型</summary>
        private Dictionary<string, DeviceGroupInfo> _persistedGroups = new Dictionary<string, DeviceGroupInfo>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>根据分组路径获取 GUID，不存在则返回 null</summary>
        public string GetGroupId(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return _persistedGroups.TryGetValue(path, out var gi) ? gi.Id : null;
        }

        /// <summary>确保分组存在，不存在则自动创建（保留已有 GUID）</summary>
        private void EnsureGroup(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!_persistedGroups.ContainsKey(path))
                _persistedGroups[path] = new DeviceGroupInfo(path);
        }
        private RichTextBox _rtbJsonView;
        private bool _isMqttSubscribeDevice = false;

        public MainForm()
        {
            InitializeComponent();
            InitCustomComponents();
            InitTray();
            LoadDeviceList();
            InitApiService();
            InitMcpService();
            // 注册生命周期关闭步骤（按启动顺序注册，关闭时逆序执行，规则 34）
            var lifecycle = ApplicationLifecycle.Instance;
            lifecycle.Register("REST API 服务", _ => { try { RestApiService.Instance?.Stop(); } catch { } });
            lifecycle.Register("MCP 服务", _ => { try { McpService.ActiveInstance?.Stop(); } catch { } });
            lifecycle.Register("离线缓存服务", _ => { OfflineCacheService.Instance.Stop(); });
            lifecycle.Register("DB 保留清理定时器", _ => { DatabaseWriteService.Instance.StopRetentionCleanup(); });
            lifecycle.Register("保存运行状态", _ => { SaveRunningState(); });

            InitOfflineCache();
            InitStatusTimer();
            InitMonitorTimer();
            RegisterEvents();
            DatabaseWriteService.Instance.ReloadConfig();
            AutoStartIfNeeded();

            // 订阅配置变更 → UI 自动刷新（MCP add_device/reload_config 等触发）
            // 安全策略：只有文件设备数 ≥ 内存设备数时才从文件重载，防止局部保存意外截断列表
            ConfigService.OnSaved += () =>
            {
                if (IsDisposed) return;
                if (InvokeRequired)
                    BeginInvoke(new Action(() => SafeReloadFromFile()));
                else
                    SafeReloadFromFile();
            };
        }

        /// <summary>
        /// 安全从文件重载设备列表：仅当文件设备数 ≥ 内存设备数时才替换
        /// 防止部分保存（如只传了一台设备）截断完整列表
        /// </summary>
        private void SafeReloadFromFile()
        {
            var fromFile = ConfigService.Instance.LoadDevices();
            if (fromFile.Count >= _devices.Count)
            {
                _devices = fromFile;
            }
            else
            {
                Logger.Warn(string.Format(
                    "[SafeReload] 文件设备数({0}) < 内存设备数({1})，拒绝从文件覆盖，保持内存列表",
                    fromFile.Count, _devices.Count));
            }
            RefreshDeviceTree();
        }



        /// <summary>
        /// 初始化自定义组件
        /// </summary>
        private void InitCustomComponents()
        {
            LoadPersistedGroups();
            InitDeviceTree();
            LoadMqttStatus();
            InitJsonView();
        }

        private void InitJsonView()
        {
            _rtbJsonView = new RichTextBox
            {
                Name = "rtbJsonView",
                Font = new Font("Consolas", 10F),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 220),
                ReadOnly = true,
                WordWrap = false,
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = RichTextBoxScrollBars.Both,
                Visible = false,
                Location = new Point(3, 35),
                Size = dataGridViewMonitor.Size,
                Anchor = dataGridViewMonitor.Anchor,
                Margin = new Padding(0)
            };
            panelRight.Controls.Add(_rtbJsonView);
        }

        /// <summary>
        /// 初始化左侧设备树（替换 ListView）
        /// </summary>
        private void InitDeviceTree()
        {
            // 隐藏原有的 ListView
            listViewDevices.Visible = false;

            // 搜索框
            txtDeviceSearch = new TextBox
            {
                Dock = DockStyle.Top,
                Font = new Font("Microsoft YaHei UI", 9F),
                Height = 28
            };

            txtDeviceSearch.TextChanged += (s, e) =>
            {
                var text = txtDeviceSearch.Text.Trim();
                var placeholder = LanguageManager.Instance.GetString("Tree_SearchPlaceholder");
                // 占位文本不作为过滤条件，按空处理
                _searchFilter = (text == placeholder) ? "" : text;
                RefreshDeviceTree();
            };
            txtDeviceSearch.GotFocus += (s, e) =>
            {
                if (txtDeviceSearch.Text == LanguageManager.Instance.GetString("Tree_SearchPlaceholder"))
                {
                    txtDeviceSearch.Text = "";
                    txtDeviceSearch.ForeColor = SystemColors.WindowText;
                }
            };
            txtDeviceSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtDeviceSearch.Text))
                {
                    txtDeviceSearch.Text = LanguageManager.Instance.GetString("Tree_SearchPlaceholder");
                    txtDeviceSearch.ForeColor = Color.Gray;
                }
            };

            // 设备树
            treeViewDevices = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 9F),
                HideSelection = false,
                ShowLines = true,
                ShowRootLines = true,
                FullRowSelect = true,
                Indent = 16,
                ItemHeight = 22,
                AllowDrop = true
            };
            treeViewDevices.AfterSelect += TreeViewDevices_AfterSelect;
            treeViewDevices.MouseClick += TreeViewDevices_MouseClick;
            treeViewDevices.DoubleClick += TreeViewDevices_DoubleClick;
            treeViewDevices.KeyDown += TreeViewDevices_KeyDown;
            treeViewDevices.ItemDrag += TreeViewDevices_ItemDrag;
            treeViewDevices.DragEnter += TreeViewDevices_DragEnter;
            treeViewDevices.DragOver += TreeViewDevices_DragOver;
            treeViewDevices.DragDrop += TreeViewDevices_DragDrop;

            // 图标列表
            var imgList = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
            imgList.Images.Add(CreateFolderIcon());   // index 0: 文件夹
            imgList.Images.Add(CreateDeviceIcon());    // index 1: 设备
            treeViewDevices.ImageList = imgList;

            var btnAddGroup = new Button
            {
                Image = CreateFolderIcon(),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Margin = new Padding(2),
                Padding = new Padding(0),
                TabStop = false
            };
            btnAddGroup.FlatAppearance.BorderSize = 0;
            btnAddGroup.Click += (s, ev) => AddGroup();

            var btnAddDevice2 = new Button
            {
                Image = CreateDeviceIcon(),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Margin = new Padding(2),
                Padding = new Padding(0),
                TabStop = false
            };
            btnAddDevice2.FlatAppearance.BorderSize = 0;
            btnAddDevice2.Click += (s, ev) => AddDevice();

            // 分组右键菜单
            var ctxGroup = new ContextMenuStrip();
            var Lc = LanguageManager.Instance;
            ctxGroup.Items.Add(Lc.GetString("Tree_AddSubGroup"), null, (s, e) => {
                var n = treeViewDevices.SelectedNode;
                if (n != null && n.Tag is string ts && ts.StartsWith("__GROUP__"))
                    AddGroup(ts.Substring(9));
            });
            ctxGroup.Items.Add(Lc.GetString("Tree_AddDevice"), null, (s, e) => AddDeviceToSelectedGroup());
            ctxGroup.Items.Add(new ToolStripSeparator());
            ctxGroup.Items.Add(Lc.GetString("Tree_RenameGroup"), null, (s, e) => RenameGroup());
            // v2.6.0: 分组移动到...
            ctxGroup.Items.Add("移动到(&M)...", null, (s, e) => MoveGroupTo());
            ctxGroup.Items.Add(new ToolStripSeparator());
            ctxGroup.Items.Add(Lc.GetString("Tree_DeleteGroup"), null, (s, e) => DeleteGroup());
            ctxGroup.Items.Add(new ToolStripSeparator());
            ctxGroup.Items.Add(Lc.GetString("Tree_Refresh"), null, (s, e) => RefreshDeviceTree());
            ctxGroup.Name = "ctxGroup";

            // 空白区域右键菜单
            _ctxEmpty = new ContextMenuStrip();
            _ctxEmpty.Items.Add(Lc.GetString("Tree_AddGroup"), null, (s, e) => AddGroup());
            _ctxEmpty.Items.Add(Lc.GetString("Tree_AddDevice"), null, (s, e) => AddDevice());
            _ctxEmpty.Items.Add(new ToolStripSeparator());
            _ctxEmpty.Items.Add(Lc.GetString("Tree_Refresh"), null, (s, e) => RefreshDeviceTree());

            // 扩展设备右键菜单：追加添加文件夹+新建设备
            if (!contextMenuDevice.Items.ContainsKey("ctxAddGroup"))
            {
                var sep = new ToolStripSeparator { Name = "ctxSepGroup" };
                contextMenuDevice.Items.Add(sep);
                var addGroupItem = new ToolStripMenuItem(Lc.GetString("Tree_AddGroup")) { Name = "ctxAddGroup" };
                addGroupItem.Click += (s, ev) => AddGroup();
                contextMenuDevice.Items.Add(addGroupItem);
                var addDeviceItem = new ToolStripMenuItem(Lc.GetString("Tree_AddDevice")) { Name = "ctxAddDevice2" };
                addDeviceItem.Click += (s, ev) => AddDeviceToSelectedGroup();
                contextMenuDevice.Items.Add(addDeviceItem);
            }

            // 模板引擎 + 设备克隆菜单
            contextMenuDevice.Items.Add(new ToolStripSeparator { Name = "ctxSepTemplate" });
            var ctxClone = new ToolStripMenuItem("克隆设备") { Name = "ctxClone" };
            ctxClone.Click += (s, ev) => CloneSelectedDevice();
            contextMenuDevice.Items.Add(ctxClone);

            var ctxTemplate = new ToolStripMenuItem("配置模板") { Name = "ctxTemplate" };
            ctxTemplate.DropDownItems.Add("新建模板（从当前设备生成）", null, (s, ev) => GenerateTemplateFromSelectedDevice());
            ctxTemplate.DropDownItems.Add("调用模板（应用到当前设备）", null, (s, ev) => ApplyTemplateToSelectedDevice());
            ctxTemplate.DropDownItems.Add(new ToolStripSeparator());
            ctxTemplate.DropDownItems.Add("覆盖配置", null, (s, ev) => OverwriteTemplateFromSelectedDevice());
            contextMenuDevice.Items.Add(ctxTemplate);

            // v2.6.0: 移动到...
            contextMenuDevice.Items.Add(new ToolStripSeparator { Name = "ctxSepMove" });
            var ctxMoveTo = new ToolStripMenuItem("移动到(&M)...") { Name = "ctxMoveTo" };
            ctxMoveTo.Click += (s, ev) => MoveDeviceTo();
            contextMenuDevice.Items.Add(ctxMoveTo);

            // 工具菜单：配置管理入口
            工具ToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            var menuTemplateMgr = new ToolStripMenuItem("配置管理(&M)");
            menuTemplateMgr.Click += (s, ev) =>
            {
                using (var form = new TemplateManagerForm()) { form.ShowDialog(); }
            };
            工具ToolStripMenuItem.DropDownItems.Add(menuTemplateMgr);

            // 组装左侧面板：搜索框 + 树
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(3),
                ColumnCount = 3,
                RowCount = 2
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.Controls.Add(btnAddGroup, 0, 0);
            table.Controls.Add(btnAddDevice2, 1, 0);
            table.Controls.Add(txtDeviceSearch, 2, 0);
            table.Controls.Add(treeViewDevices, 0, 1);
            table.SetColumnSpan(treeViewDevices, 3);

            // 清空 panelLeft，添加新控件
            panelLeft.Controls.Clear();
            panelLeft.Controls.Add(table);

            // 保存分组菜单引用以便语言切换
            treeViewDevices.Tag = ctxGroup;

            // 帮助菜单 → 认证信息
            var licenseMenuItem = new ToolStripMenuItem(
                LanguageManager.Instance.GetString("Menu_Help_License"),
                null,
                (s, ev) => ShowLicenseInfo());
            licenseMenuItem.Name = "menuHelpLicense";
            帮助ToolStripMenuItem.DropDownItems.Insert(0, licenseMenuItem);

            // 修复监控工具栏按钮锚定：导出CSV跟随右侧
            btnExportCsv.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClear.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            SetSearchPlaceholder();
        }

        /// <summary>
        /// 初始化系统托盘
        /// </summary>
        private void InitTray()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = this.Icon,
                Text = "Neo_工业网络数采平台",
                Visible = false
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add(LanguageManager.Instance.GetString("Tray_ShowWindow"), null, (s, e) =>
            {
                ShowFromTray();
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(LanguageManager.Instance.GetString("Tray_Exit"), null, (s, e) =>
            {
                _isReallyExiting = true;
                // Force-close dashboard if open
                NavigationHelper.Dashboard?.ForceClose();
                Close();
            });
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) => ShowFromTray();
        }

        private void ShowFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
            _trayIcon.Visible = false;
        }

        /// <summary>
        /// 注册事件回调
        /// </summary>
        private void RegisterEvents()
        {
            LanguageManager.Instance.LanguageChanged += (s, e) =>
            {
                ApplyLanguage();
                _semanticForm?.ApplyLanguage();
            };

            // 初始应用一次语言
            ApplyLanguage();

            DataCollectionService.Instance.OnDataReceived += (s, e) =>
            {
                if (e != null && e.Data != null)
                {
                    DataProcessor.Instance.Process(e.Data);
                    _apiService.FeedData(e.Data);
                    // UI 渲染由 _monitorTimer 统一拉取快照，不做逐点刷新
                }
            };

            DataCollectionService.Instance.OnCycleCompleted += (s, e) =>
            {
                if (e == null || e.Batch == null) return;

                // 心跳：采集成功
                OfflineCacheService.Instance.RecordHeartbeat(
                    e.Batch.Device, e.Batch.Driver, success: true);

                // ===== v2.1 WAL: MQTT + DB 双路独立缓存 =====
                var ids = OfflineCacheService.Instance.StoreBatch(e.Batch, needMqtt: true, needDb: true);

                // v2.1: DataStreamEngine 统一路由，各自独立标记
                bool mqttOk = false, dbOk = false;
                try
                {
                    var routeTask = DataStreamEngine.Instance.RouteAsync(e.Batch);
                    if (!routeTask.Wait(8000))
                    {
                        Logger.Warn("DataStreamEngine 路由超时 (" + e.Batch.Device + ")");
                    }
                    else
                    {
                        var results = routeTask.Result;
                        mqttOk = results.ContainsKey("MQTT") && results["MQTT"];
                        dbOk = results.ContainsKey("Database") && results["Database"];
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("DataStreamEngine 路由异常: " + ex.Message);
                }

                // 各自独立清理：MQTT 通 → 标记 MQTT 缓存行并清理
                if (mqttOk) OfflineCacheService.Instance.MarkMqttSent(ids.MqttId);
                // DB 通 → 标记 DB 缓存行并清理
                if (dbOk) OfflineCacheService.Instance.MarkDbSent(ids.DbId);
            };

            DataCollectionService.Instance.OnDeviceStatusChanged += (s, e) =>
            {
                if (e == null) return;
                // 心跳：采集状态变化
                OfflineCacheService.Instance.RecordHeartbeat(
                    e.DeviceName, "", success: e.IsConnected,
                    errorMsg: e.IsConnected ? null : e.Message);

                if (this.IsHandleCreated && !this.IsDisposed) this.BeginInvoke((Action)(() =>
                {
                    RefreshDeviceList();
                    toolStripStatusSystem.Text = e.Message;
                }));
            };
        }

        /// <summary>
        /// 尝试发布到 MQTT，返回是否成功（MQTT 未配置也算成功）
        /// </summary>
        private bool TryPublishMqtt(CycleDataBatch batch)
        {
            if (batch == null) return true;

            var mqtt = MqttPublishService.Instance;
            if (!mqtt.IsConnected) return false;

            try
            {
                var config = mqtt.GetConfig();
                var task = mqtt.PublishBatchAsync(config.TopicPrefix, batch, config.Qos);
                if (!task.Wait(3000))
                {
                    Logger.Warn("MQTT发布超时 (" + batch.Device + ")，走离线缓存");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("MQTT 发布失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 尝试写入数据库，返回是否成功（DB 未启用也算成功）
        /// </summary>
        private bool TryWriteDb(CycleDataBatch batch)
        {
            if (!DatabaseWriteService.Instance.IsAnyEnabled) return true;
            string deviceName = batch?.Device ?? "";
            if (string.IsNullOrEmpty(deviceName)) return true;

            try
            {
                var task = DatabaseWriteService.Instance.WriteBatchAsync(deviceName, batch);
                if (!task.Wait(5000))
                {
                    Logger.Warn("DB写入超时 (" + deviceName + ")，走离线缓存");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("数据库写入失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 加载 MQTT 连接状态
        /// </summary>
        private async void LoadMqttStatus()
        {
            try
            {
                var mqttConfig = await ConfigService.Instance.LoadMqttConfigAsync();
                if (mqttConfig.Enabled && !string.IsNullOrEmpty(mqttConfig.BrokerHost))
                {
                    bool connected = await MqttPublishService.Instance.ConnectAsync(mqttConfig);
                    UpdateMqttStatus(connected);
                }
            }
            catch
            {
                UpdateMqttStatus(false);
            }
        }

        /// <summary>
        /// 更新 MQTT 状态栏
        /// </summary>
        private void UpdateMqttStatus(bool connected)
        {
            var L = LanguageManager.Instance;
            toolStripStatusMqtt.Text = connected ? L.GetString("Status_MqttConnected") : L.GetString("Status_MqttDisconnected");
            toolStripStatusMqtt.ForeColor = connected ? Color.Green : Color.Gray;
        }

        /// <summary>
        /// 加载设备列表
        /// </summary>
        private void LoadDeviceList()
        {
            _devices = ConfigService.Instance.LoadDevices();
            RefreshDeviceList();
            RebuildMonitorRows();
            _lastGridDeviceCount = _devices.Count;
            _lastGridVarCount = _devices.Sum(d => d.DataPoints?.Count ?? 0);
        }

        /// <summary>
        /// 刷新设备列表（兼容旧接口，转发到树）
        /// </summary>
        private void RefreshDeviceList()
        {
            RefreshDeviceTree();
        }

        /// <summary>
        /// 刷新设备树（基于分组层级）- 公开供 MCP 工具/语言切换等外部调用
        /// </summary>
        public void RefreshDeviceTree()
        {
            if (treeViewDevices == null || treeViewDevices.IsDisposed) return;
            treeViewDevices.BeginUpdate();
            treeViewDevices.Nodes.Clear();

            // Clean up empty persisted groups (no devices referencing them or their children)
            var usedPaths = new HashSet<string>();
            foreach (var device in _devices)
            {
                if (!string.IsNullOrEmpty(device.Group))
                {
                    string path = device.Group;
                    while (!string.IsNullOrEmpty(path))
                    {
                        usedPaths.Add(path);
                        int lastSlash = path.LastIndexOf('/');
                        path = lastSlash >= 0 ? path.Substring(0, lastSlash) : "";
                    }
                }
            }
            // v2.6.0: 已持久化的空分组也要保护——它们是被显式保留的（如拖拽后旧路径）
            foreach (var pg in _persistedGroups.Keys.ToList())
            {
                string path = pg;
                while (!string.IsNullOrEmpty(path))
                {
                    usedPaths.Add(path);
                    int lastSlash = path.LastIndexOf('/');
                    path = lastSlash >= 0 ? path.Substring(0, lastSlash) : "";
                }
            }
                        foreach (var pk in _persistedGroups.Keys.ToList())
                if (!usedPaths.Contains(pk))
                    _persistedGroups.Remove(pk);

            // Collect devices by group path.
            var groupedPath = new Dictionary<string, List<DeviceConfig>>();
            var ungroupedDevices = new List<DeviceConfig>();
            foreach (var device in _devices)
            {
                if (!string.IsNullOrEmpty(_searchFilter)
                    && device.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (string.IsNullOrEmpty(device.Group))
                {
                    ungroupedDevices.Add(device);
                    continue;
                }

                string group = device.Group;
                if (!groupedPath.ContainsKey(group))
                    groupedPath[group] = new List<DeviceConfig>();
                groupedPath[group].Add(device);
            }

            foreach (var emptyGroup in _persistedGroups.Keys)
            {
                if (!groupedPath.ContainsKey(emptyGroup))
                    groupedPath[emptyGroup] = new List<DeviceConfig>();
            }

            // Build nested tree from flat paths.
            var rootPaths = new SortedDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in groupedPath)
            {
                string path = kv.Key;
                var devices = kv.Value;
                string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                var current = rootPaths;
                for (int i = 0; i < parts.Length; i++)
                {
                    string seg = parts[i];
                    if (i == parts.Length - 1)
                    {
                        if (!current.ContainsKey(seg) || !(current[seg] is GroupNode))
                            current[seg] = new GroupNode();
                        ((GroupNode)current[seg]).Devices.AddRange(devices);
                    }
                    else
                    {
                        if (!current.ContainsKey(seg) || !(current[seg] is GroupNode))
                            current[seg] = new GroupNode();
                        current = ((GroupNode)current[seg]).Children;
                    }
                }
            }

            foreach (var kv in rootPaths)
                BuildNodeRecursive(treeViewDevices.Nodes, rootPaths, "", kv.Key);

            foreach (var device in ungroupedDevices)
                treeViewDevices.Nodes.Add(CreateDeviceNode(device));

            treeViewDevices.EndUpdate();

            int runningCount = DataCollectionService.Instance.RunningCount;
            toolStripStatusDevices.Text = string.Format(LanguageManager.Instance.GetString("Status_Devices"), runningCount, _devices.Count);
        }

        /// <summary>
        /// 初始化状态栏定时器
        /// </summary>
        private void InitStatusTimer()
        {
            _statusTimer = new Timer();
            _statusTimer.Interval = 1000;
            _statusTimer.Tick += (s, e) =>
            {
                toolStripStatusTime.Text = DateTime.Now.ToString("HH:mm:ss");
                toolStripStatusRate.Text = "采集: " + DataProcessor.Instance.LatestCount + " 变量";
            };
            _statusTimer.Start();
        }

        /// <summary>
        /// v2.0: 按需采集模式。不自动刷新，仅用户点击「采集」按钮时手动更新当前设备快照。
        /// 设备/变量增删时仍自动重建行结构，但填充值只在按钮点击后更新。
        /// </summary>
        private void InitMonitorTimer()
        {
            _monitorTimer = new Timer { Interval = 1000 };
            _monitorTimer.Tick += (s, e) =>
            {
                // 设备列表变更（增删设备/变量） → 重建行结构（但不拉取数据）
                int devCount = _devices.Count;
                int varCount = _devices.Sum(d => d.DataPoints?.Count ?? 0);
                if (devCount != _lastGridDeviceCount || varCount != _lastGridVarCount)
                {
                    RebuildMonitorRows();
                    _lastGridDeviceCount = devCount;
                    _lastGridVarCount = varCount;
                }
            };
            _monitorTimer.Start();
        }

        /// <summary>
        /// 重建监控网格行结构（仅在设备/变量增删时触发）
        /// </summary>
        private void RebuildMonitorRows()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            dataGridViewMonitor.SuspendLayout();
            dataGridViewMonitor.Rows.Clear();
            _gridRowMap.Clear();

            // 有选中设备时，只显示该设备的变量
            var targetDevices = _devices;
            if (!string.IsNullOrEmpty(_selectedDeviceId))
            {
                var sel = _devices.FirstOrDefault(d => d.Id == _selectedDeviceId);
                targetDevices = sel != null ? new List<DeviceConfig> { sel } : _devices;
            }

            foreach (var device in targetDevices)
            {
                if (device.DataPoints == null || device.DataPoints.Count == 0) continue;
                foreach (var point in device.DataPoints)
                {
                    string key = device.Id + "_" + point.Name;
                    int rowIdx = dataGridViewMonitor.Rows.Add(
                        device.Name,
                        point.Name,
                        point.DataType,
                        "—",   // Value placeholder
                        point.Unit ?? "",
                        ""     // Time placeholder
                    );
                    _gridRowMap[key] = rowIdx;
                }
            }

            dataGridViewMonitor.ResumeLayout(false);
            Logger.Debug($"Monitor grid rebuilt: {_gridRowMap.Count} rows in {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// 从 DataProcessor 缓存拉取最新值，逐个更新已存在的行单元格（不增删行）
        /// </summary>
        private void RefreshMonitorGrid()
        {
            var latest = DataProcessor.Instance.GetAllLatest();
            if (latest.Count == 0) return;

            dataGridViewMonitor.SuspendLayout();

            foreach (var data in latest)
            {
                string key = data.DeviceId + "_" + data.VariableName;
                if (!_gridRowMap.TryGetValue(key, out int rowIdx)) continue;
                if (rowIdx >= dataGridViewMonitor.Rows.Count) continue;

                var row = dataGridViewMonitor.Rows[rowIdx];
                // 原地更新：只改 Value 和 Timestamp 两列
                row.Cells[3].Value = data.Value;                           // Value
                row.Cells[4].Value = data.Unit ?? "";                      // Unit (可能运行时变更)
                row.Cells[5].Value = data.Timestamp.ToString("HH:mm:ss");  // Time
            }

            dataGridViewMonitor.ResumeLayout(false);
        }

        // ========== 工具栏按钮事件 ==========

        private void toolBtnAddDevice_Click(object sender, EventArgs e)
        {
            var form = new DeviceConfigForm();
            if (form.ShowDialog() == DialogResult.OK && form.DeviceConfig != null)
            {
                InsertDeviceInGroupOrder(form.DeviceConfig);
                ConfigService.Instance.SaveDevices(_devices);
                RefreshDeviceList();
                toolStripStatusSystem.Text = "设备已添加";
            }
        }

        private void toolBtnEditDevice_Click(object sender, EventArgs e)
        {
            EditSelectedDevice();
        }

        private void toolBtnDeleteDevice_Click(object sender, EventArgs e)
        {
            DeleteSelectedDevice();
        }

        private async void toolBtnStartAll_Click(object sender, EventArgs e)
        {
            await DataCollectionService.Instance.StartAllAsync(
                _devices.Where(d => d.Enabled).ToList());
            toolStripStatusSystem.Text = LanguageManager.Instance.GetString("Msg_All_Started");
            RefreshDeviceList();
            SaveRunningState();
        }

        private async void toolBtnStopAll_Click(object sender, EventArgs e)
        {
            await DataCollectionService.Instance.StopAllAsync();
            toolStripStatusSystem.Text = LanguageManager.Instance.GetString("Msg_All_Stopped");
            RefreshDeviceList();
            SaveRunningState();
        }

        private async void toolBtnMqtt_Click(object sender, EventArgs e)
        {
            var config = ConfigService.Instance.LoadMqttConfig();
            var form = new MqttConfigForm(config);
            if (form.ShowDialog() == DialogResult.OK && form.MqttConfig != null)
            {
                ConfigService.Instance.SaveMqttConfig(form.MqttConfig);
                await ConnectMqttAsync(form.MqttConfig);
            }
        }

        private async Task ConnectMqttAsync(MqttConfig config)
        {
            if (config.Enabled)
            {
                bool ok = await MqttPublishService.Instance.ConnectAsync(config);
                UpdateMqttStatus(ok);
                toolStripStatusSystem.Text = ok ? LanguageManager.Instance.GetString("Msg_Mqtt_Connected") : LanguageManager.Instance.GetString("Msg_Mqtt_ConnectFailed");
            }
            else
            {
                await MqttPublishService.Instance.DisconnectAsync();
                UpdateMqttStatus(false);
                toolStripStatusSystem.Text = "MQTT 已禁用";
            }
        }

        private void toolBtnLanguage_Click(object sender, EventArgs e)
        {
            LanguageManager.Instance.SwitchLanguage();
            var L = LanguageManager.Instance;
            Logger.Debug($"Language switched to: {L.CurrentLanguage}");
        }

        private void toolBtnAbout_Click(object sender, EventArgs e)
        {
            var form = new AboutForm();
            form.ShowDialog();
        }

        private void toolBtnLog_Click(object sender, EventArgs e)
        {
            try
            {
                string logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!System.IO.Directory.Exists(logDir))
                    System.IO.Directory.CreateDirectory(logDir);
                System.Diagnostics.Process.Start("explorer.exe", logDir);
            }
            catch (Exception ex)
            {
                Logger.Error($"打开日志目录失败: {ex.Message}");
            }
        }

        private void toolBtnDbConfig_Click(object sender, EventArgs e)
        {
            var form = new DatabaseConfigForm();
            form.ShowDialog();
        }

        /// <summary>
        /// v2.0: 按需采集 — 点击后仅为当前选中的设备重建行结构并填充实时数据快照
        /// </summary>
        private void btnCollect_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedDeviceId))
            {
                MessageBox.Show("请先在左侧设备树中选择一台设备", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // MQTT Subscribe 设备：显示格式化 JSON
            if (_isMqttSubscribeDevice)
            {
                ShowMqttJsonView();
                return;
            }

            // 其他设备：网格模式
            _rtbJsonView.Visible = false;
            dataGridViewMonitor.Visible = true;
            RebuildMonitorRows();
            RefreshMonitorGrid();
        }

        private void ShowMqttJsonView()
        {
            var device = _devices.FirstOrDefault(d => d.Id == _selectedDeviceId);
            if (device == null) return;

            var latest = DataProcessor.Instance.GetAllLatest()
                .Where(d => d.DeviceId == _selectedDeviceId)
                .ToList();

            if (latest.Count == 0)
            {
                _rtbJsonView.Visible = true;
                dataGridViewMonitor.Visible = false;
                _rtbJsonView.Text = "等待 MQTT 数据...";
                return;
            }

            var jsonObj = new
            {
                timestamp = new DateTimeOffset(latest[0].Timestamp).ToUnixTimeMilliseconds(),
                driver = device.DriverType,
                device = device.Name,
                values = latest.Select(d =>
                {
                    // 尝试解析为数值以在 JSON 中输出数字类型
                    object v = d.Value;
                    if (double.TryParse(d.Value, out double dv))
                        v = dv;
                    return new
                    {
                        id = string.Format("{0}|{1}", d.DeviceName, d.VariableName),
                        dt = d.DataType,
                        v,
                        u = d.Unit,
                        tag_id = d.Tag,
                        tag_cn = d.TagCn
                    };
                }).ToList()
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);

            _rtbJsonView.Clear();
            _rtbJsonView.Text = json;
            _rtbJsonView.Visible = true;
            dataGridViewMonitor.Visible = false;
        }

        private void 数据库配置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolBtnDbConfig_Click(sender, e);
        }

        private void API服务配置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = new ApiServiceConfigForm(_apiService);
            form.ShowDialog();
        }

        private void MCP服务配置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var form = new McpConfigForm(_mcpService);
            form.ShowDialog();
        }

        // ========== API 服务 ==========

        private void InitApiService()
        {
            _apiService = RestApiService.Instance;
            _apiService.OnLog += (msg) =>
            {
                Logger.Info("[API] " + msg);
            };

            // 尝试加载配置并自动启动
            try
            {
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "IndustrialDataCollection", "config", "apiConfig.json");

                if (File.Exists(configPath))
                {
                    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<ApiServiceConfigHelper>(
                        File.ReadAllText(configPath, System.Text.Encoding.UTF8));
                    if (config != null)
                    {
                        _apiService.Port = config.Port;
                        _apiService.TokenAuthEnabled = config.TokenAuth;
                        _apiService.ApiToken = config.ApiToken ?? "admin123";
                        _apiService.SwaggerEnabled = config.Swagger;

                        if (config.Enabled)
                        {
                            try
                            {
                                _apiService.Start();
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("API 服务自动启动失败: " + ex.Message);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private class ApiServiceConfigHelper
        {
            public int Port { get; set; } = 5000;
            public bool Enabled { get; set; } = true;
            public bool TokenAuth { get; set; } = true;
            public string ApiToken { get; set; } = "admin123";
            public bool Swagger { get; set; } = true;
        }

        // ========== MCP 服务 ==========

        private void InitMcpService()
        {
            _mcpService = new McpService();
            McpService.ActiveInstance = _mcpService;
            _mcpService.OnLog += (msg) =>
            {
                Logger.Info("[MCP] " + msg);
            };

            // 程序化添加菜单项（避免改 Designer.cs 中文变量名）
            _mcpMenuItem = new ToolStripMenuItem();
            _mcpMenuItem.Name = "MCP服务配置ToolStripMenuItem";
            _mcpMenuItem.Text = LanguageManager.Instance.GetString("Menu_Tools_McpService");
            _mcpMenuItem.Click += (s, e) => MCP服务配置ToolStripMenuItem_Click(s, e);
            // 插在 API服务配置 之后
            int insertIdx = 工具ToolStripMenuItem.DropDownItems.IndexOf(API服务配置ToolStripMenuItem) + 1;
            工具ToolStripMenuItem.DropDownItems.Insert(insertIdx, _mcpMenuItem);

            // 数据源管理菜单
            _dsMenuItem = new ToolStripMenuItem();
            _dsMenuItem.Name = "数据源管理ToolStripMenuItem";
            _dsMenuItem.Text = LanguageManager.Instance.GetString("Menu_Tools_DataSourceManager");
            _dsMenuItem.Click += (s, e) =>
            {
                var form = new DataSourceManagerForm();
                form.ShowDialog();
            };
            int dsIdx = 工具ToolStripMenuItem.DropDownItems.IndexOf(_mcpMenuItem) + 1;
            工具ToolStripMenuItem.DropDownItems.Insert(dsIdx, _dsMenuItem);

            // 尝试加载配置并自动启动
            try
            {
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "IndustrialDataCollection", "config", "mcpConfig.json");

                if (File.Exists(configPath))
                {
                    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<McpServiceConfigHelper>(
                        File.ReadAllText(configPath, System.Text.Encoding.UTF8));
                    if (config != null)
                    {
                        _mcpService.Port = config.Port;
                        _mcpService.TokenAuthEnabled = config.TokenAuth;
                        _mcpService.McpToken = config.McpToken ?? "admin123";

                        if (config.Enabled)
                        {
                            try
                            {
                                _mcpService.Start();
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("MCP 服务自动启动失败: " + ex.Message);
                            }
                        }
                    }
                }
            }
            catch { }

            // 看板导航菜单
            _dashboardMenuItem = new ToolStripMenuItem();
            _dashboardMenuItem.Name = "看板ToolStripMenuItem";
            _dashboardMenuItem.Text = LanguageManager.Instance.GetString("Menu_Dashboard");
            _dashboardMenuItem.Click += (s, ev) => NavigateToDashboard();
            menuStrip.Items.Add(_dashboardMenuItem);

            // 语义管理按钮（工具栏，放在 数据库配置 和 中文/English 之间）
            var semBtn = new ToolStripButton();
            semBtn.Name = "语义管理ToolStripButton";
            semBtn.DisplayStyle = ToolStripItemDisplayStyle.Text;
            semBtn.Text = "语义管理";
            semBtn.Click += (s, ev) =>
            {
                if (_semanticForm == null || _semanticForm.IsDisposed)
                    _semanticForm = new SemanticManagementForm();
                _semanticForm.Show();
                _semanticForm.Activate();
            };
            int dbCfgIdx = toolStrip.Items.IndexOf(toolBtnDbConfig);
            if (dbCfgIdx >= 0)
                toolStrip.Items.Insert(dbCfgIdx + 1, semBtn);

            // 配置历史版本菜单
            var historyMenuItem = new ToolStripMenuItem();
            historyMenuItem.Name = "配置历史版本ToolStripMenuItem";
            historyMenuItem.Text = "配置历史版本(&H)";
            historyMenuItem.Click += (s, ev) => ShowHistoryRestoreDialog();
            工具ToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            工具ToolStripMenuItem.DropDownItems.Add(historyMenuItem);
        }

        /// <summary>
        /// 配置历史版本回退对话框
        /// </summary>
        private void ShowHistoryRestoreDialog()
        {
            try
            {
                var versions = ConfigService.Instance.GetHistoryVersions();
                if (versions.Count == 0)
                {
                    MessageBox.Show("暂无历史版本备份。配置保存次数越多，可回退的版本越多。\n\n提示：Ctrl+Z 可直接回退到上一版本。",
                        "配置历史版本", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 构建选项列表
                var items = versions.Select(v => $"版本 {v.Version} — {v.Timestamp:yyyy-MM-dd HH:mm:ss} — {v.DeviceCount} 台设备 ({v.FileSize / 1024}KB)").ToArray();

                // 创建选择对话框
                using (var form = new Form())
                {
                    form.Text = "配置历史版本 — 选择要回退的版本";
                    form.Size = new Size(520, 400);
                    form.StartPosition = FormStartPosition.CenterParent;
                    form.FormBorderStyle = FormBorderStyle.FixedDialog;
                    form.MaximizeBox = false;
                    form.MinimizeBox = false;

                    var label = new Label()
                    {
                        Text = "选择要回退的历史版本（越靠上越新）：",
                        Location = new Point(12, 12),
                        AutoSize = true
                    };

                    var listBox = new ListBox()
                    {
                        Location = new Point(12, 36),
                        Size = new Size(480, 260),
                        IntegralHeight = false
                    };
                    listBox.Items.AddRange(items);
                    if (listBox.Items.Count > 0)
                        listBox.SelectedIndex = 0;

                    var btnOk = new Button()
                    {
                        Text = "回退到此版本",
                        Location = new Point(300, 306),
                        Size = new Size(120, 36),
                        DialogResult = DialogResult.OK
                    };

                    var btnCancel = new Button()
                    {
                        Text = "取消",
                        Location = new Point(12, 306),
                        Size = new Size(80, 36),
                        DialogResult = DialogResult.Cancel
                    };

                    var btnQuickRestore = new Button()
                    {
                        Text = "回退到上一版本 (Ctrl+Z)",
                        Location = new Point(100, 306),
                        Size = new Size(190, 36)
                    };
                    btnQuickRestore.Click += (s, ev2) =>
                    {
                        form.DialogResult = DialogResult.Yes; // Yes = quick restore
                    };

                    form.Controls.AddRange(new Control[] { label, listBox, btnOk, btnCancel, btnQuickRestore });
                    form.AcceptButton = btnOk;
                    form.CancelButton = btnCancel;

                    var result = form.ShowDialog(this);
                    if (result == DialogResult.Cancel) return;

                    int version;
                    if (result == DialogResult.Yes)
                    {
                        // 快速回退到最新版本（版本1）
                        version = versions[0].Version;
                    }
                    else if (listBox.SelectedIndex >= 0 && listBox.SelectedIndex < versions.Count)
                    {
                        version = versions[listBox.SelectedIndex].Version;
                    }
                    else
                    {
                        return;
                    }

                    // 确认对话框
                    var confirm = MessageBox.Show(
                        $"确认回退到版本 {version}？\n\n回退后当前配置将丢失，且此操作不可撤销。\n建议先确认版本 {version} 的内容正确。",
                        "确认回退", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                    if (confirm != DialogResult.OK) return;

                    bool ok = ConfigService.Instance.RestoreFromHistory(version);
                    if (ok)
                    {
                        MessageBox.Show($"已成功回退到历史版本 {version}。\n设备树即将刷新。",
                            "回退成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        // 刷新 UI
                        RefreshDeviceTree();
                    }
                    else
                    {
                        MessageBox.Show($"回退失败，请查看日志了解详情。",
                            "回退失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ShowHistoryRestoreDialog 异常: " + ex.Message);
                MessageBox.Show("显示历史版本失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Ctrl+Z 快速回退到上一版本
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z))
            {
                // 仅当焦点不在文本编辑控件中时触发（避免劫持文本框的撤销）
                var active = this.ActiveControl;
                if (active == null || !(active is TextBoxBase))
                {
                    try
                    {
                        var versions = ConfigService.Instance.GetHistoryVersions();
                        if (versions.Count > 0)
                        {
                            var confirm = MessageBox.Show(
                                $"Ctrl+Z: 回退到历史版本 {versions[0].Version}？\n\n" +
                                $"时间: {versions[0].Timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                                $"设备数: {versions[0].DeviceCount} 台\n\n" +
                                $"确认回退？",
                                "配置回退 Ctrl+Z", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                            if (confirm == DialogResult.OK)
                            {
                                if (ConfigService.Instance.RestoreFromHistory(versions[0].Version))
                                {
                                    MessageBox.Show($"已回退到版本 {versions[0].Version}。", "回退成功",
                                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    RefreshDeviceTree();
                                }
                                else
                                {
                                    MessageBox.Show("回退失败。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("暂无历史版本可回退。", "Ctrl+Z", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Ctrl+Z 回退异常: " + ex.Message);
                    }
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// 导航回到 Dashboard 看板
        /// </summary>
        private void NavigateToDashboard()
        {
            if (NavigationHelper.Dashboard == null || NavigationHelper.Dashboard.IsDisposed)
            {
                NavigationHelper.Dashboard = new DashboardForm();
            }
            NavigationHelper.Dashboard.Show();
            NavigationHelper.Dashboard.WindowState = FormWindowState.Maximized;
            NavigationHelper.Dashboard.Activate();
            this.Hide();
        }

        private class McpServiceConfigHelper
        {
            public int Port { get; set; } = 5100;
            public bool Enabled { get; set; } = false;
            public bool TokenAuth { get; set; } = true;
            public string McpToken { get; set; } = "admin123";
        }

        // ========== 离线缓存与断网补发 ==========

        private void InitOfflineCache()
        {
            var cache = OfflineCacheService.Instance;
            cache.Init();

            // v2.1: MQTT + DB 双路独立补发
            cache.IsMqttConnected = () => MqttPublishService.Instance.IsConnected;
            cache.MqttFlushHandler = async (batch) =>
            {
                try
                {
                    var config = MqttPublishService.Instance.GetConfig();
                    await MqttPublishService.Instance.PublishBatchAsync(config.TopicPrefix, batch, config.Qos);
                    return true;
                }
                catch { return false; }
            };

            cache.IsDbConnected = () => DatabaseWriteService.Instance.EnsureConnectionsHealthy();
            cache.DbFlushHandler = async (batch) =>
            {
                try
                {
                    await DatabaseWriteService.Instance.WriteBatchAsync(batch.Device, batch);
                    return true;
                }
                catch { return false; }
            };

            // MQTT 恢复时触发补发
            MqttPublishService.Instance.ConnectionStateChanged += (s, connected) =>
            {
                if (connected)
                {
                    Logger.Info("MQTT 已恢复，检查离线缓存...");
                    // 补发由 OfflineCacheService 的定时器自动触发
                }
            };

            int pending = cache.GetPendingCount();
            if (pending > 0)
                Logger.Info(string.Format("离线缓存: 发现 {0} 条待补发数据", pending));
        }

        // ========== 菜单事件 ==========

        private void 添加设备ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolBtnAddDevice_Click(sender, e);
        }

        private void 编辑设备ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditSelectedDevice();
        }

        private void 删除设备ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteSelectedDevice();
        }

        private async void 启动采集ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var device = GetSelectedDevice();
            if (device != null)
            {
                await DataCollectionService.Instance.StartDeviceAsync(device);
                RefreshDeviceList();
                SaveRunningState();
            }
        }

        private async void 停止采集ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var device = GetSelectedDevice();
            if (device != null)
            {
                await DataCollectionService.Instance.StopDeviceAsync(device.Id);
                RefreshDeviceList();
                SaveRunningState();
            }
        }

        private void 全部启动ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolBtnStartAll_Click(sender, e);
        }

        private async void 全部停止ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await DataCollectionService.Instance.StopAllAsync();
            RefreshDeviceList();
            SaveRunningState();
        }

        private void mqtt配置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolBtnMqtt_Click(sender, e);
        }

        private void 清空数据ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DataProcessor.Instance.ClearAll();
            // 清缓存后立即重建网格（重置所有值为 —）
            RebuildMonitorRows();
        }

        private void 导出CSVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportCsv();
        }

        private void 导入配置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "JSON 文件|*.json";
                dialog.Title = "导入配置";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var json = File.ReadAllText(dialog.FileName);
                        var imported = Newtonsoft.Json.JsonConvert.DeserializeObject<List<DeviceConfig>>(json);
                        if (imported != null)
                        {
                            _devices = imported;
                            ConfigService.Instance.SaveDevices(_devices);
                            RefreshDeviceList();
                            toolStripStatusSystem.Text = string.Format(LanguageManager.Instance.GetString("Msg_Import_Complete"), imported.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(LanguageManager.Instance.GetString("Msg_Import_Failed"), ex.Message),
                            LanguageManager.Instance.GetString("Msg_Error"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void 导出配置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "JSON 文件|*.json";
                dialog.FileName = "devices_config.json";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var json = Newtonsoft.Json.JsonConvert.SerializeObject(_devices,
                            Newtonsoft.Json.Formatting.Indented);
                        File.WriteAllText(dialog.FileName, json);
                        toolStripStatusSystem.Text = LanguageManager.Instance.GetString("Msg_Config_Exported");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(LanguageManager.Instance.GetString("Msg_Export_Failed"), ex.Message),
                            LanguageManager.Instance.GetString("Msg_Error"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void 退出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var L = LanguageManager.Instance;
            if (MessageBox.Show(L.GetString("Msg_ConfirmExit"),
                L.GetString("Msg_Confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _isReallyExiting = true;
            Close();
        }

        private void 注销ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var L = LanguageManager.Instance;
            if (MessageBox.Show(L.GetString("Msg_ConfirmLogout"),
                L.GetString("Msg_Confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            AuthService.Instance.Logout();
            this.Hide();

            using (var loginForm = new LoginForm())
            {
                if (loginForm.ShowDialog() != DialogResult.OK || !loginForm.LoginSuccess)
                {
                    Application.Exit();
                    return;
                }
            }

            this.Show();
        }

        private void 关于ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolBtnAbout_Click(sender, e);
        }

        // ========== 底部按钮事件 ==========

        private void btnClear_Click(object sender, EventArgs e)
        {
            DataProcessor.Instance.ClearAll();
            RebuildMonitorRows();
        }

        private void btnExportCsv_Click(object sender, EventArgs e)
        {
            ExportCsv();
        }

        private void ExportCsv()
        {
            if (dataGridViewMonitor.Rows.Count == 0)
            {
                var L = LanguageManager.Instance;
                MessageBox.Show(L.GetString("Msg_Error_NoData"), L.GetString("Msg_Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV 文件|*.csv";
                dialog.FileName = string.Format("data_{0:yyyyMMdd_HHmmss}.csv", DateTime.Now);
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (var sw = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8))
                        {
                            for (int i = 0; i < dataGridViewMonitor.Columns.Count; i++)
                            {
                                if (i > 0) sw.Write(",");
                                sw.Write(dataGridViewMonitor.Columns[i].HeaderText);
                            }
                            sw.WriteLine();

                            foreach (DataGridViewRow row in dataGridViewMonitor.Rows)
                            {
                                for (int i = 0; i < dataGridViewMonitor.Columns.Count; i++)
                                {
                                    if (i > 0) sw.Write(",");
                                    var val = row.Cells[i].Value?.ToString() ?? "";
                                    if (val.Contains(",") || val.Contains("\"") || val.Contains("\n"))
                                    {
                                        val = "\"" + val.Replace("\"", "\"\"") + "\"";
                                    }
                                    sw.Write(val);
                                }
                                sw.WriteLine();
                            }
                        }
                        toolStripStatusSystem.Text = string.Format("已导出 {0} 行到 {1}",
                            dataGridViewMonitor.Rows.Count, dialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format("导出失败: {0}", ex.Message), "错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // ========== 通用操作 ==========

        /// <summary>
        /// 获取当前选中的设备
        /// </summary>
        private DeviceConfig GetSelectedDevice()
        {
            var node = treeViewDevices.SelectedNode;
            if (node == null) return null;
            return node.Tag as DeviceConfig;
        }

        private void EditSelectedDevice()
        {
            var device = GetSelectedDevice();
            if (device == null)
            {
                var L = LanguageManager.Instance;
                MessageBox.Show(L.GetString("Msg_Error_NoDevice"),
                    L.GetString("Msg_Info"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var form = new DeviceConfigForm(device);
            if (form.ShowDialog() == DialogResult.OK && form.DeviceConfig != null)
            {
                int idx = _devices.FindIndex(d => d.Id == device.Id);
                if (idx >= 0) _devices[idx] = form.DeviceConfig;

                ConfigService.Instance.SaveDevices(_devices);
                RefreshDeviceList();
                toolStripStatusSystem.Text = "设备已更新";
            }
        }

        private async void DeleteSelectedDevice()
        {
            var device = GetSelectedDevice();
            if (device == null) return;

            var L = LanguageManager.Instance;

            // v1.9.3: 检查语义层绑定
            var semNode = SemanticService.Instance.GetNodeBySource("device", device.Id);
            if (semNode != null)
            {
                int relCount, evtCount;
                SemanticService.Instance.GetBindingCounts(semNode.Id, out relCount, out evtCount);
                if (relCount > 0 || evtCount > 0)
                {
                    var bindMsg = string.Format(
                        "设备「{0}」已绑定 {1} 个关系、{2} 个事件，删除后将标记为「已删除」状态。\n确认继续删除？",
                        device.Name, relCount, evtCount);
                    var bindResult = MessageBox.Show(bindMsg,
                        L.GetString("Msg_Confirm"),
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (bindResult != DialogResult.Yes) return;
                }
            }

            var result = MessageBox.Show(
                string.Format(L.GetString("Msg_Confirm_DeleteDevice"), device.Name),
                L.GetString("Msg_Confirm"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                await DataCollectionService.Instance.StopDeviceAsync(device.Id);
                _devices.Remove(device);
                ConfigService.Instance.SaveDevices(_devices);
                RefreshDeviceList();
                toolStripStatusSystem.Text = LanguageManager.Instance.GetString("Msg_Device_Deleted");
            }
        }

        private void listViewDevices_DoubleClick(object sender, EventArgs e)
        {
            EditSelectedDevice();
        }

        private void listViewDevices_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var item = listViewDevices.GetItemAt(e.X, e.Y);
                if (item != null)
                {
                    item.Selected = true;
                    contextMenuDevice.Show(listViewDevices, e.Location);
                }
            }
        }

        /// <summary>
        /// 应用多语言
        /// </summary>
        private void ApplyLanguage()
        {
            var L = LanguageManager.Instance;
            Logger.Debug($"MainForm ApplyLanguage called, lang={L.CurrentLanguage}");

            // 窗口标题
            this.Text = L.GetString("MainForm_Title");

            // ===== 菜单栏 =====
            文件ToolStripMenuItem.Text = L.GetString("Menu_File");
            导入配置ToolStripMenuItem.Text = L.GetString("Menu_File_Import");
            导出配置ToolStripMenuItem.Text = L.GetString("Menu_File_Export");
            注销ToolStripMenuItem.Text = L.GetString("Menu_File_Logout");
            退出ToolStripMenuItem.Text = L.GetString("Menu_File_Exit");
            设备ToolStripMenuItem.Text = L.GetString("Menu_Device");
            添加设备ToolStripMenuItem.Text = L.GetString("Menu_Device_Add");
            编辑设备ToolStripMenuItem.Text = L.GetString("Menu_Device_Edit");
            删除设备ToolStripMenuItem.Text = L.GetString("Menu_Device_Delete");
            启动采集ToolStripMenuItem.Text = L.GetString("Menu_Device_Start");
            停止采集ToolStripMenuItem.Text = L.GetString("Menu_Device_Stop");
            全部启动ToolStripMenuItem.Text = L.GetString("Menu_Device_StartAll");
            全部停止ToolStripMenuItem.Text = L.GetString("Menu_Device_StopAll");
            // 右键菜单
            ctxStart.Text = L.GetString("Menu_Device_Start");
            ctxStop.Text = L.GetString("Menu_Device_Stop");
            ctxAdd.Text = L.GetString("Menu_Device_Add");
            ctxEdit.Text = L.GetString("Menu_Device_Edit");
            ctxDelete.Text = L.GetString("Menu_Device_Delete");
            工具ToolStripMenuItem.Text = L.GetString("Menu_Tools");
            mqtt配置ToolStripMenuItem.Text = L.GetString("Menu_Tools_MqttConfig");
            数据库配置ToolStripMenuItem.Text = L.GetString("Menu_Tools_DbConfig");
            清空数据ToolStripMenuItem.Text = L.GetString("Menu_Tools_ClearData");
            导出CSVToolStripMenuItem.Text = L.GetString("Menu_Tools_ExportCsv");
            API服务配置ToolStripMenuItem.Text = L.GetString("Menu_Tools_ApiService");
            if (_mcpMenuItem != null) _mcpMenuItem.Text = L.GetString("Menu_Tools_McpService");
            if (_dsMenuItem != null) _dsMenuItem.Text = L.GetString("Menu_Tools_DataSourceManager");
            if (_dashboardMenuItem != null) _dashboardMenuItem.Text = L.GetString("Menu_Dashboard");
            帮助ToolStripMenuItem.Text = L.GetString("Menu_Help");
            关于ToolStripMenuItem.Text = L.GetString("Menu_Help_About");

            // ===== 工具栏 =====
            toolBtnAddDevice.Text = L.GetString("Toolbar_AddDevice");
            toolBtnEditDevice.Text = L.GetString("Toolbar_EditDevice");
            toolBtnDeleteDevice.Text = L.GetString("Toolbar_DeleteDevice");
            toolBtnStartAll.Text = L.GetString("Toolbar_StartAll");
            toolBtnStopAll.Text = L.GetString("Toolbar_StopAll");
            toolBtnMqtt.Text = L.GetString("Toolbar_MqttConfig");
            toolBtnLanguage.Text = L.GetString("Toolbar_Language");
            toolBtnLog.Text = L.GetString("Toolbar_Log");
            toolBtnDbConfig.Text = L.GetString("Menu_Tools_DbConfig");


            // ===== 设备树 =====
            SetSearchPlaceholder();

            // ===== 分组右键菜单 =====
            if (treeViewDevices.Tag is ContextMenuStrip ctxGroup2)
            {
                var items = ctxGroup2.Items;
                if (items.Count >= 1) items[0].Text = L.GetString("Tree_AddSubGroup");
                if (items.Count >= 2) items[1].Text = L.GetString("Tree_AddDevice");
                // index 2 = separator
                if (items.Count >= 4) items[3].Text = L.GetString("Tree_RenameGroup");
                // index 4 = separator
                if (items.Count >= 6) items[5].Text = L.GetString("Tree_DeleteGroup");
                // index 6 = separator
                if (items.Count >= 8) items[7].Text = L.GetString("Tree_Refresh");
            }

            ctxRefresh.Text = L.GetString("Tree_Refresh");

            // ===== 设备右键菜单扩展项 =====
            var ctxAddGroupItem = contextMenuDevice.Items["ctxAddGroup"] as ToolStripMenuItem;
            if (ctxAddGroupItem != null) ctxAddGroupItem.Text = L.GetString("Tree_AddGroup");
            var ctxAddDeviceItem = contextMenuDevice.Items["ctxAddDevice2"] as ToolStripMenuItem;
            if (ctxAddDeviceItem != null) ctxAddDeviceItem.Text = L.GetString("Tree_AddDevice");

            // ===== 空白区域右键菜单 =====
            if (_ctxEmpty != null && _ctxEmpty.Items.Count >= 4)
            {
                _ctxEmpty.Items[0].Text = L.GetString("Tree_AddGroup");
                _ctxEmpty.Items[1].Text = L.GetString("Tree_AddDevice");
                // index 2 = separator
                _ctxEmpty.Items[3].Text = L.GetString("Tree_Refresh");
            }

            // ===== 设备列表列头 =====
            colDeviceName.Text = L.GetString("DeviceList_Column_Name");
            colDriverType.Text = L.GetString("DeviceList_Column_Driver");
            colStatus.Text = L.GetString("DeviceList_Column_Status");

            // ===== 监控表格列头 =====
            colMonDevice.HeaderText = L.GetString("Monitor_Column_Device");
            colMonVariable.HeaderText = L.GetString("Monitor_Column_Variable");
            colMonDataType.HeaderText = L.GetString("Monitor_Column_DataType");
            colMonValue.HeaderText = L.GetString("Monitor_Column_Value");
            colMonUnit.HeaderText = L.GetString("Monitor_Column_Unit");
            colMonTime.HeaderText = L.GetString("Monitor_Column_Time");

            // ===== 监控工具栏 =====
            btnClear.Text = L.GetString("Monitor_BtnClear");
            btnCollect.Text = L.GetString("Monitor_BtnCollect");
            btnExportCsv.Text = L.GetString("Monitor_BtnExportCsv");
            chkAutoScroll.Text = L.GetString("Monitor_AutoScroll");

            // ===== 状态栏 =====
            toolStripStatusSystem.Text = L.GetString("Status_Ready");
            int runningCount = DataCollectionService.Instance.RunningCount;
            toolStripStatusDevices.Text = string.Format(L.GetString("Status_Devices"), runningCount, _devices.Count);

            // 刷新设备列表（使用当前语言的运行/停止状态）
            RefreshDeviceList();
            UpdateMqttStatusLabel();

            // 托盘菜单
            if (_trayIcon != null && _trayIcon.ContextMenuStrip != null)
            {
                var items = _trayIcon.ContextMenuStrip.Items;
                if (items.Count >= 1) items[0].Text = L.GetString("Tray_ShowWindow");
                if (items.Count >= 3) items[2].Text = L.GetString("Tray_Exit");
            }
        }

        /// <summary>
        /// 设置搜索框占位文字
        /// </summary>
        private void SetSearchPlaceholder()
        {
            if (string.IsNullOrWhiteSpace(txtDeviceSearch.Text) || txtDeviceSearch.Text == LanguageManager.Instance.GetString("Tree_SearchPlaceholder"))
            {
                txtDeviceSearch.Text = LanguageManager.Instance.GetString("Tree_SearchPlaceholder");
                txtDeviceSearch.ForeColor = Color.Gray;
            }
        }

        // ========== 设备树事件 ==========

        private void TreeViewDevices_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var device = e.Node.Tag as DeviceConfig;
            _selectedDeviceId = device?.Id;
            _isMqttSubscribeDevice = device != null &&
                string.Equals(device.DriverType, "MqttSubscribe", StringComparison.OrdinalIgnoreCase);
            if (!_isMqttSubscribeDevice)
            {
                _rtbJsonView.Visible = false;
                dataGridViewMonitor.Visible = true;
            }
            // v2.0: 选中设备不自动刷新网格，仅记录选中设备ID
        }

        private void TreeViewDevices_DoubleClick(object sender, EventArgs e)
        {
            var device = GetSelectedDevice();
            if (device != null)
                EditSelectedDevice();
        }

        private void TreeViewDevices_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            var node = treeViewDevices.GetNodeAt(e.X, e.Y);

            if (node == null)
            {
                _ctxEmpty.Show(treeViewDevices, e.Location);
                return;
            }

            treeViewDevices.SelectedNode = node;

            if (node.Tag is DeviceConfig dev)
            {
                // 设备节点：显示设备右键菜单
                contextMenuDevice.Show(treeViewDevices, e.Location);
            }
            else if (node.Tag is string tagStr && tagStr.StartsWith("__GROUP__"))
            {
                // 分组节点：显示分组右键菜单
                if (treeViewDevices.Tag is ContextMenuStrip ctx)
                    ctx.Show(treeViewDevices, e.Location);
            }
        }

        private void ctxRefresh_Click(object sender, EventArgs e)
        {
            _devices = ConfigService.Instance.LoadDevices();
            RefreshDeviceTree();
        }

        private void TreeViewDevices_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
                DeleteSelectedDevice();
            else if (e.KeyCode == Keys.F2)
                EditSelectedDevice();
        }

        /// <summary>
        /// v2.6.0: 设备右键「移动到...」— 弹出层级输入对话框
        /// </summary>
        private void MoveDeviceTo()
        {
            var selected = treeViewDevices.SelectedNode;
            var device = selected?.Tag as DeviceConfig;
            if (device == null) return;

            // 检查设备是否正在采集
            var dcService = DataCollectionService.Instance;
            if (dcService != null && dcService.IsDeviceRunning(device.Id))
            {
                var result = MessageBox.Show(
                    "设备「" + device.Name + "」正在采集中。\n调整层级将可能影响语义标签路径和数据结构。\n\n请先停止采集，再进行调整。\n\n是否现在停止采集并继续？",
                    "设备正在采集中",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                {
                    try { dcService.StopDeviceAsync(device.Name).Wait(3000); } catch { }
                }
                else
                {
                    return;
                }
            }

            string newGroup = PromptMoveTarget(device.Group ?? "");
            if (string.IsNullOrEmpty(newGroup) || newGroup == device.Group) return;

            string oldGroup = device.Group ?? "";
            device.Group = newGroup;

            // 保留旧路径
            if (!string.IsNullOrEmpty(oldGroup))
            {
                EnsureGroup(oldGroup);
                var oldParts = oldGroup.Split('/');
                var cumPath = "";
                for (int pi = 0; pi < oldParts.Length; pi++)
                {
                    cumPath = pi == 0 ? oldParts[pi] : (cumPath + "/" + oldParts[pi]);
                    EnsureGroup(cumPath);
                }
            }
            SavePersistedGroups();
            ConfigService.Instance.SaveDevices(_devices);
            RefreshDeviceTree();
        }

        /// <summary>
        /// v2.6.0: 分组右键「移动到...」— 将整个分组迁移到新的父级路径
        /// </summary>
        private void MoveGroupTo()
        {
            var selected = treeViewDevices.SelectedNode;
            if (selected == null || !(selected.Tag is string ts && ts.StartsWith("__GROUP__")))
                return;

            string oldGroupPath = ts.Substring(9);

            // 检查该分组下是否有正在采集的设备
            var dcService = DataCollectionService.Instance;
            var runningDevices = _devices
                .Where(d => (d.Group ?? "") == oldGroupPath && dcService != null && dcService.IsDeviceRunning(d.Id))
                .Select(d => d.Name).ToList();
            if (runningDevices.Count > 0)
            {
                var result = MessageBox.Show(
                    "分组「" + oldGroupPath + "」下有 " + runningDevices.Count + " 台设备正在采集中：\n" +
                    string.Join("、", runningDevices.Take(5)) +
                    (runningDevices.Count > 5 ? "...等" : "") +
                    "\n\n调整层级将可能影响语义标签路径和数据结构。\n请先停止采集，再进行调整。\n\n是否现在停止采集并继续？",
                    "设备正在采集中",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                {
                    foreach (var dev in _devices.Where(d => runningDevices.Contains(d.Name)))
                    {
                        try { dcService.StopDeviceAsync(dev.Name).Wait(2000); } catch { }
                    }
                }
                else
                {
                    return;
                }
            }

            string newParent = PromptMoveTarget(oldGroupPath);
            if (string.IsNullOrEmpty(newParent) || newParent == oldGroupPath) return;

            string leafName = oldGroupPath.Split('/').Last();
            string newGroupPath = string.IsNullOrEmpty(newParent) ? leafName
                : newParent + "/" + leafName;

            foreach (var dev in _devices.Where(d => (d.Group ?? "") == oldGroupPath))
            {
                dev.Group = newGroupPath;
            }

            // 保留旧路径
            if (!string.IsNullOrEmpty(oldGroupPath))
            {
                EnsureGroup(oldGroupPath);
                var oldParts = oldGroupPath.Split('/');
                var cumPath = "";
                for (int pi = 0; pi < oldParts.Length; pi++)
                {
                    cumPath = pi == 0 ? oldParts[pi] : (cumPath + "/" + oldParts[pi]);
                    EnsureGroup(cumPath);
                }
            }
            SavePersistedGroups();
            ConfigService.Instance.SaveDevices(_devices);
            RefreshDeviceTree();
        }

        /// <summary>
        /// v2.6.0: 显示移动到目标路径选择对话框
        /// 使用与主设备树完全相同的构建逻辑，保证层级结构一致
        /// </summary>
        private string PromptMoveTarget(string currentPath)
        {
            using (var form = new Form())
            {
                form.Text = "移动到";
                form.Size = new Size(1080, 960);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var lbl = new Label
                {
                    Text = "当前路径: " + (string.IsNullOrEmpty(currentPath) ? "(根)" : currentPath),
                    Location = new Point(16, 14),
                    AutoSize = true
                };
                form.Controls.Add(lbl);

                var lbl2 = new Label
                {
                    Text = "请选择目标父级节点（点击展开/折叠）:",
                    Location = new Point(16, 40),
                    AutoSize = true
                };
                form.Controls.Add(lbl2);

                // 树状结构 — 使用与主设备树完全相同的构建逻辑
                var tree = new TreeView
                {
                    Location = new Point(16, 66),
                    Size = new Size(1030, 750),
                    HideSelection = false,
                    Font = treeViewDevices.Font  // 与主设备树字体一致
                };

                // 根节点（始终可选）
                var rootNode = new TreeNode("(根 — 无父级)") { Tag = "" };
                rootNode.NodeFont = new Font(tree.Font, FontStyle.Bold);
                tree.Nodes.Add(rootNode);

                // === 与 RefreshDeviceTree 完全相同的树构建逻辑 ===
                // 1. 收集所有分组路径（含空分组）
                var groupedPath = new Dictionary<string, List<DeviceConfig>>();
                foreach (var device in _devices)
                {
                    if (string.IsNullOrEmpty(device.Group))
                        continue;
                    string group = device.Group;
                    if (!groupedPath.ContainsKey(group))
                        groupedPath[group] = new List<DeviceConfig>();
                    groupedPath[group].Add(device);
                }
                foreach (var emptyGroup in _persistedGroups.Keys)
                {
                    if (!groupedPath.ContainsKey(emptyGroup))
                        groupedPath[emptyGroup] = new List<DeviceConfig>();
                }

                // 2. 构建嵌套字典
                var rootPaths = new SortedDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in groupedPath)
                {
                    string path = kv.Key;
                    var devices = kv.Value;
                    string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    var current = rootPaths;
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string seg = parts[i];
                        if (i == parts.Length - 1)
                        {
                            if (!current.ContainsKey(seg) || !(current[seg] is GroupNode))
                                current[seg] = new GroupNode();
                            ((GroupNode)current[seg]).Devices.AddRange(devices);
                        }
                        else
                        {
                            if (!current.ContainsKey(seg) || !(current[seg] is GroupNode))
                                current[seg] = new GroupNode();
                            current = ((GroupNode)current[seg]).Children;
                        }
                    }
                }

                // 3. 递归构建树节点（不添加设备叶子，只显示分组层级）
                foreach (var kv in rootPaths)
                    BuildGroupOnlyNodes(tree.Nodes, rootPaths, "", kv.Key);

                // 一级分组展开
                foreach (TreeNode node in tree.Nodes)
                {
                    if (node.Tag is string s && s.StartsWith("__GROUP__"))
                        node.Expand();
                }

                var lbl3 = new Label
                {
                    Text = "目标路径（选中节点后自动填入）:",
                    Location = new Point(16, 828),
                    AutoSize = true
                };
                form.Controls.Add(lbl3);

                var txtPath = new TextBox
                {
                    Location = new Point(16, 850),
                    Size = new Size(940, 23),
                    Text = currentPath ?? ""
                };
                form.Controls.Add(txtPath);

                tree.AfterSelect += (s, e) =>
                {
                    string tag = e.Node?.Tag as string;
                    if (tag != null && tag.StartsWith("__GROUP__"))
                        txtPath.Text = tag.Substring(9);
                    else if (tag == "")
                        txtPath.Text = "";
                    else if (tag != null)
                        txtPath.Text = tag;
                };
                form.Controls.Add(tree);

                var pnl = new Panel
                {
                    Location = new Point(16, 885),
                    Size = new Size(1030, 30)
                };
                var btnOk = new Button { Text = "确定", Size = new Size(75, 28), Location = new Point(878, 0) };
                var btnCancel = new Button { Text = "取消", Size = new Size(75, 28), Location = new Point(955, 0) };
                btnCancel.Click += (s, e) => { form.DialogResult = DialogResult.Cancel; form.Close(); };
                string result = null;
                btnOk.Click += (s, e) =>
                {
                    result = txtPath.Text.Trim();
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                };
                pnl.Controls.Add(btnOk);
                pnl.Controls.Add(btnCancel);
                form.Controls.Add(pnl);

                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                form.ShowDialog();
                return result;
            }
        }

        /// <summary>
        /// v2.6.0: 与 BuildNodeRecursive 相同的递归逻辑，但不创建设备子节点
        /// </summary>
        private void BuildGroupOnlyNodes(TreeNodeCollection parentNodes,
            SortedDictionary<string, object> tree, string prefix, string seg)
        {
            string fullPath = string.IsNullOrEmpty(prefix) ? seg : prefix + "/" + seg;
            var value = tree[seg];
            var groupData = value as GroupNode;

            var treeNode = new TreeNode(seg)
            {
                Tag = "__GROUP__" + fullPath
            };
            treeNode.NodeFont = new Font(treeViewDevices.Font, FontStyle.Bold);

            if (groupData != null)
            {
                // 不添加设备子节点，仅递归构建子分组
                foreach (var childKv in groupData.Children)
                    BuildGroupOnlyNodes(treeNode.Nodes, groupData.Children, fullPath, childKv.Key);
            }

            parentNodes.Add(treeNode);
            treeNode.Expand();
        }

        // ========== 拖拽支持 ==========

        private void TreeViewDevices_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var node = e.Item as TreeNode;
                if (node == null) return;
                // 设备节点可拖拽
                if (node.Tag is DeviceConfig)
                {
                    treeViewDevices.SelectedNode = node;
                    treeViewDevices.DoDragDrop(node, DragDropEffects.Move);
                }
                // 文件夹节点可拖拽（「未分组」除外）
                else if (node.Tag is string s && s.StartsWith("__GROUP__"))
                {
                    string name = s.Substring(9);
                    if (name == LanguageManager.Instance.GetString("deviceTree.ungrouped")) return;
                    treeViewDevices.SelectedNode = node;
                    treeViewDevices.DoDragDrop(node, DragDropEffects.Move);
                }
            }
        }

        private void TreeViewDevices_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(TreeNode)) ? DragDropEffects.Move : DragDropEffects.None;
        }

        private void TreeViewDevices_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(TreeNode))) { e.Effect = DragDropEffects.None; return; }
            var pt = treeViewDevices.PointToClient(new Point(e.X, e.Y));
            var target = treeViewDevices.GetNodeAt(pt);
            var dragged = e.Data.GetData(typeof(TreeNode)) as TreeNode;
            if (dragged == null || target == null || dragged == target) { e.Effect = DragDropEffects.None; return; }

            // 判断是否拖拽的是文件夹
            bool isDraggingGroup = dragged.Tag is string ds && ds.StartsWith("__GROUP__");

            // 拖到分组节点：移动到该分组末尾
            if (target.Tag is string ts && ts.StartsWith("__GROUP__"))
            {
                // 拖文件夹时，不能拖到自己
                if (isDraggingGroup && ts == (string)dragged.Tag) { e.Effect = DragDropEffects.None; return; }
                treeViewDevices.SelectedNode = target;
                e.Effect = DragDropEffects.Move;
            }
            // 拖文件夹到设备节点：合并到目标设备所在分组
            else if (target.Tag is DeviceConfig)
            {
                treeViewDevices.SelectedNode = target;
                e.Effect = DragDropEffects.Move;
            }
            else { e.Effect = DragDropEffects.None; }
        }

        private void TreeViewDevices_DragDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(TreeNode))) return;
            var dragged = e.Data.GetData(typeof(TreeNode)) as TreeNode;
            var pt = treeViewDevices.PointToClient(new Point(e.X, e.Y));
            var target = treeViewDevices.GetNodeAt(pt);
            if (dragged == null || target == null || dragged == target) return;

            // ── 拖拽文件夹：将源分组下所有设备移到目标分组 ──
            if (dragged.Tag is string ds && ds.StartsWith("__GROUP__"))
            {
                string srcGroup = ds.Substring(9);
                var Lg = LanguageManager.Instance;
                if (srcGroup == Lg.GetString("deviceTree.ungrouped")) return;

                string tgtGroup = null;
                if (target.Tag is string ts && ts.StartsWith("__GROUP__"))
                {
                    tgtGroup = ts.Substring(9);
                    if (tgtGroup == srcGroup) return;
                }
                else if (target.Tag is DeviceConfig td)
                {
                    tgtGroup = td.Group ?? "";
                    if (tgtGroup == srcGroup) return;
                }
                if (tgtGroup == null) return;

                // 移动源组所有设备到目标组
                int moved = 0;
                foreach (var d in _devices)
                {
                    if ((d.Group ?? "") == srcGroup)
                    {
                        d.Group = tgtGroup;
                        moved++;
                    }
                }

                ConfigService.Instance.SaveDevices(_devices);
                // 保留中间路径
                if (!_persistedGroups.ContainsKey(srcGroup))
                    EnsureGroup(srcGroup);
                SavePersistedGroups();
                RefreshDeviceTree();
                Logger.Info($"[DeviceTree] 文件夹拖拽: '{srcGroup}' → '{tgtGroup}', 移动 {moved} 个设备");
                return;
            }

            // ── 拖拽设备节点（原有逻辑） ──
            var device = dragged.Tag as DeviceConfig;
            if (device == null) return;

            var L = LanguageManager.Instance;
            string dstGroup = null;
            DeviceConfig dstDevice = null;

            if (target.Tag is string tgs && tgs.StartsWith("__GROUP__"))
            {
                dstGroup = tgs.Substring(9);
            }
            else if (target.Tag is DeviceConfig targetDev)
            {
                dstDevice = targetDev;
                if (target.Parent != null && target.Parent.Tag is string ps && ps.StartsWith("__GROUP__"))
                    dstGroup = ps.Substring(9);
                else
                    dstGroup = "";
            }
            if (dstGroup == null) return;

            string oldGroup = device.Group ?? "";
            _devices.Remove(device);
            device.Group = dstGroup;

            if (dstDevice != null && dstDevice != device)
            {
                int insertIdx = _devices.IndexOf(dstDevice);
                if (insertIdx >= 0)
                    _devices.Insert(insertIdx, device);
                else
                    _devices.Add(device);
            }
            else
            {
                int lastIdx = -1;
                for (int i = _devices.Count - 1; i >= 0; i--)
                {
                    if ((_devices[i].Group ?? "") == dstGroup)
                    { lastIdx = i; break; }
                }
                if (lastIdx >= 0)
                    _devices.Insert(lastIdx + 1, device);
                else
                    _devices.Add(device);
            }

            ConfigService.Instance.SaveDevices(_devices);
            if (!string.IsNullOrEmpty(oldGroup))
            {
                if (!_persistedGroups.ContainsKey(oldGroup))
                    EnsureGroup(oldGroup);
                var oldParts = oldGroup.Split('/');
                var cumPath = "";
                for (int pi = 0; pi < oldParts.Length; pi++)
                {
                    cumPath = pi == 0 ? oldParts[pi] : (cumPath + "/" + oldParts[pi]);
                    if (!_persistedGroups.ContainsKey(cumPath))
                        EnsureGroup(cumPath);
                }
                SavePersistedGroups();
            }
            RefreshDeviceTree();

            // 恢复选中
            foreach (TreeNode rootNode in treeViewDevices.Nodes)
            {
                foreach (TreeNode child in rootNode.Nodes)
                {
                    if (child.Tag == device)
                    {
                        treeViewDevices.SelectedNode = child;
                        break;
                    }
                }
            }

            Logger.Debug(string.Format("拖拽设备 {0}: {1} → {2}", device.Name,
                string.IsNullOrEmpty(oldGroup) ? L.GetString("Tree_Ungrouped") : oldGroup,
                string.IsNullOrEmpty(dstGroup) ? L.GetString("Tree_Ungrouped") : dstGroup));
        }

        // ========== 分组管理 ==========

        private void AddGroup(string parentGroup = "")
        {
            var L = LanguageManager.Instance;
            string title = string.IsNullOrEmpty(parentGroup)
                ? L.GetString("Tree_AddGroup")
                : L.GetString("Tree_AddSubGroup");
            string prompt = string.IsNullOrEmpty(parentGroup)
                ? L.GetString("Msg_EnterGroupName")
                : L.GetString("Msg_EnterSubGroupName");
            string name = ShowInputDialog(prompt, title);
            if (string.IsNullOrWhiteSpace(name)) return;
            // Block reserved system group names
            if (name.Equals(L.GetString("Tree_Ungrouped"), StringComparison.OrdinalIgnoreCase)
                || name.Equals("Ungrouped", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(L.GetString("Msg_CannotRenameUngrouped"),
                    L.GetString("Msg_Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string fullPath = string.IsNullOrEmpty(parentGroup) ? name : parentGroup + "/" + name;
            EnsureGroup(fullPath);
            SavePersistedGroups();
            RefreshDeviceTree();
            Logger.Debug(string.Format("鏂板缓鍒嗙粍: {0}", fullPath));
        }

        private TreeNode CreateDeviceNode(DeviceConfig device)
        {
            bool running = DataCollectionService.Instance.IsDeviceRunning(device.Id);
            // v2.0: 统一使用中文名称，NameEn 已废弃
            string displayName = device.Name;
            var node = new TreeNode(displayName + "  [" + device.DriverType + "]")
            {
                Tag = device,
                ForeColor = running ? Color.Green : Color.Gray,
                ImageIndex = 1,
                SelectedImageIndex = 1
            };
            if (running)
                node.NodeFont = new Font(treeViewDevices.Font, FontStyle.Bold);
            return node;
        }

        private void AddDevice()
        {
            if (!CheckDeviceLimit()) return;
            var form = new DeviceConfigForm();
            if (form.ShowDialog(this) == DialogResult.OK && form.DeviceConfig != null)
            {
                InsertDeviceInGroupOrder(form.DeviceConfig);
                ConfigService.Instance.SaveDevices(_devices);
                RefreshDeviceTree();
                toolStripStatusSystem.Text = "设备已添加";
            }
        }

        private void AddDeviceToSelectedGroup()
        {
            if (!CheckDeviceLimit()) return;
            var node = treeViewDevices.SelectedNode;
            string groupName = "";
            if (node != null && node.Tag is string tagStr && tagStr.StartsWith("__GROUP__"))
                groupName = tagStr.Substring(9);

            var form = new DeviceConfigForm(null, groupName);
            if (form.ShowDialog(this) == DialogResult.OK && form.DeviceConfig != null)
            {
                if (!string.IsNullOrEmpty(groupName))
                    form.DeviceConfig.Group = groupName;
                // _persistedGroups 不需要移除——手动分组永久保留
                InsertDeviceInGroupOrder(form.DeviceConfig);
                ConfigService.Instance.SaveDevices(_devices);
                RefreshDeviceTree();
                toolStripStatusSystem.Text = "设备已添加" + (string.IsNullOrEmpty(groupName) ? "" : "到分组 " + groupName);
            }
        }

        private void RenameGroup()
        {
            var node = treeViewDevices.SelectedNode;
            if (node == null || !(node.Tag is string tagStr) || !tagStr.StartsWith("__GROUP__")) return;

            string oldName = tagStr.Substring(9);
            var L = LanguageManager.Instance;

            if (oldName.Equals(L.GetString("Tree_Ungrouped"), StringComparison.OrdinalIgnoreCase)
                || oldName.Equals("Ungrouped", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(L.GetString("Msg_CannotRenameUngrouped"),
                    L.GetString("Msg_Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string newName = ShowInputDialog(L.GetString("Msg_EnterGroupName"), L.GetString("Tree_RenameGroup"), oldName);
            if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;

            // Update all devices under this group (including subgroups).
            foreach (var d in _devices)
            {
                if (d.Group == oldName)
                    d.Group = newName;
                else if (d.Group.StartsWith(oldName + "/"))
                    d.Group = newName + d.Group.Substring(oldName.Length);
            }

            // Update persisted groups (including children) — 保留 GUID
            if (_persistedGroups.TryGetValue(oldName, out var oldInfo))
            {
                _persistedGroups.Remove(oldName);
                oldInfo.Path = newName;
                _persistedGroups[newName] = oldInfo;
            }
            var rnGroups = new List<DeviceGroupInfo>();
            foreach (var kvp in _persistedGroups)
            {
                if (kvp.Key.StartsWith(oldName + "/"))
                    rnGroups.Add(kvp.Value);
            }
            foreach (var gi in rnGroups)
            {
                _persistedGroups.Remove(gi.Path);
                gi.Path = newName + gi.Path.Substring(oldName.Length);
                _persistedGroups[gi.Path] = gi;
            }
            SavePersistedGroups();

            ConfigService.Instance.SaveDevices(_devices);
            RefreshDeviceTree();
            Logger.Debug(string.Format("閲嶅懡鍚嶅垎缁? {0} 鈫?{1}", oldName, newName));
        }

        private void DeleteGroup()
        {
            var node = treeViewDevices.SelectedNode;
            if (node == null || !(node.Tag is string tagStr) || !tagStr.StartsWith("__GROUP__")) return;

            string groupName = tagStr.Substring(9);
            var L = LanguageManager.Instance;

            // 绂佹鍒犻櫎绯荤粺鍒嗙粍
            if (groupName == L.GetString("Tree_Ungrouped"))
            {
                MessageBox.Show(L.GetString("Msg_CannotDeleteUngrouped"),
                    L.GetString("Msg_Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(L.GetString("Msg_ConfirmDeleteGroup"),
                L.GetString("Msg_Confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            // Clear devices under this group and all subgroups.
            foreach (var d in _devices)
            {
                if (d.Group == groupName || d.Group.StartsWith(groupName + "/"))
                    d.Group = "";
            }
            // Remove persisted groups (including children).
            var rmGroups = new List<string>();
            foreach (var pg in _persistedGroups.Keys)
            {
                if (pg == groupName || pg.StartsWith(groupName + "/"))
                    rmGroups.Add(pg);
            }
            foreach (var pg in rmGroups)
                _persistedGroups.Remove(pg);
            SavePersistedGroups();
            ConfigService.Instance.SaveDevices(_devices);
            RefreshDeviceTree();
            Logger.Debug(string.Format("鍒犻櫎鍒嗙粍: {0}", groupName));
        }

        private static string ShowInputDialog(string prompt, string title, string defaultValue = "")
        {
            var form = new Form
            {
                Width = 360, Height = 160, FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                Text = title, MaximizeBox = false, MinimizeBox = false
            };
            var label = new Label { Left = 12, Top = 12, Width = 320, Text = prompt };
            var textBox = new TextBox { Left = 12, Top = 36, Width = 320, Text = defaultValue };
            var buttonOk = new Button { Text = "OK", Left = 160, Width = 80, Top = 70, DialogResult = DialogResult.OK };
            var buttonCancel = new Button { Text = "Cancel", Left = 250, Width = 80, Top = 70, DialogResult = DialogResult.Cancel };
            form.Controls.Add(label);
            form.Controls.Add(textBox);
            form.Controls.Add(buttonOk);
            form.Controls.Add(buttonCancel);
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;
            return form.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : null;
        }

        /// <summary>
        /// 用当前语言更新 MQTT 状态文本
        /// </summary>
        private void UpdateMqttStatusLabel()
        {
            var L = LanguageManager.Instance;
            string txt = toolStripStatusMqtt.Text;
            if (txt.Contains("已连接") || txt.Contains("Connected"))
                toolStripStatusMqtt.Text = L.GetString("Status_MqttConnected");
            else if (txt.Contains("未启用") || txt.Contains("Disabled"))
                toolStripStatusMqtt.Text = L.GetString("Status_MqttDisabled");
            else
                toolStripStatusMqtt.Text = L.GetString("Status_MqttDisconnected");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isReallyExiting)
            {
                // Show exit dialog with "minimize to tray" checkbox
                bool minimizeToTray = true;
                using (var dlg = new Form())
                {
                    dlg.Text = "Neo_工业网络数采平台";
                    dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                    dlg.MaximizeBox = false;
                    dlg.MinimizeBox = false;
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.ClientSize = new Size(320, 140);
                    dlg.Icon = this.Icon;

                    var lbl = new Label()
                    {
                        Text = LanguageManager.Instance.GetString("Msg_ConfirmExit"),
                        Location = new Point(24, 18),
                        AutoSize = true,
                        Font = new Font("Microsoft YaHei UI", 10F)
                    };
                    var chkTray = new CheckBox()
                    {
                        Text = LanguageManager.Instance.GetString("ExitMinimizeToTray"),
                        Location = new Point(24, 52),
                        Checked = true,
                        AutoSize = true,
                        Font = new Font("Microsoft YaHei UI", 9F)
                    };
                    var btnOK = new Button()
                    {
                        Text = LanguageManager.Instance.GetString("Common.Confirm"),
                        Location = new Point(120, 88),
                        Size = new Size(80, 30),
                        DialogResult = DialogResult.OK,
                        Font = new Font("Microsoft YaHei UI", 9F)
                    };
                    var btnCancel = new Button()
                    {
                        Text = LanguageManager.Instance.GetString("Common.Cancel"),
                        Location = new Point(210, 88),
                        Size = new Size(80, 30),
                        DialogResult = DialogResult.Cancel,
                        Font = new Font("Microsoft YaHei UI", 9F)
                    };

                    dlg.Controls.Add(lbl);
                    dlg.Controls.Add(chkTray);
                    dlg.Controls.Add(btnOK);
                    dlg.Controls.Add(btnCancel);
                    dlg.AcceptButton = btnOK;
                    dlg.CancelButton = btnCancel;

                    var result = dlg.ShowDialog(this);
                    if (result != DialogResult.OK)
                    {
                        e.Cancel = true;
                        return;
                    }
                    minimizeToTray = chkTray.Checked;
                }

                if (minimizeToTray)
                {
                    e.Cancel = true;
                    this.Hide();
                    _trayIcon.Visible = true;
                    _trayIcon.ShowBalloonTip(2000, "Neo_工业网络数采平台",
                        LanguageManager.Instance.GetString("Status_TrayMinimized"), ToolTipIcon.Info);
                    Logger.Info("最小化到系统托盘");
                    return;
                }
                // uncheck → fall through to real exit
            }

            // 真正的退出：完整的资源清理
            _isReallyExiting = true;

            // 1. 停止 UI 定时器
            if (_statusTimer != null) _statusTimer.Stop();
            if (_monitorTimer != null) _monitorTimer.Stop();

            // 2. 关闭看板
            NavigationHelper.Dashboard?.ForceClose();

            // 3. 停止数据采集
            try
            {
                var stopTask = DataCollectionService.Instance.StopAllAsync();
                stopTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex) { Logger.Debug("StopAllAsync error: " + ex.Message); }

            // 4. 断开 MQTT
            try
            {
                var mqttTask = MqttPublishService.Instance.DisconnectAsync();
                mqttTask.Wait(TimeSpan.FromSeconds(3));
            }
            catch (Exception ex) { Logger.Debug("MQTT disconnect error: " + ex.Message); }

            // 5-9. 其余所有服务由 ApplicationLifecycle 统一关闭（规则 34）
            ApplicationLifecycle.Instance.Shutdown();

            // 10. 清理托盘
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            Logger.Info("应用程序退出");

            // 10. 终止消息泵，确保所有窗口退出
            Application.Exit();
            base.OnFormClosing(e);
        }

        // ========== 运行状态持久化 ==========

                private static string RunningStatePath()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IndustrialDataCollection", "config");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "running_state.json");
        }

        private void LoadPersistedGroups()
        {
            try
            {
                string path = ConfigService.Instance.GroupsFilePath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var list = DeviceGroupInfo.DeserializeGroups(json);
                    _persistedGroups = list.ToDictionary(g => g.Path, g => g, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { }
        }

        private class GroupNode
        {
            public List<DeviceConfig> Devices = new List<DeviceConfig>();
            public SortedDictionary<string, object> Children =
                new SortedDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        private void BuildNodeRecursive(TreeNodeCollection parentNodes,
            SortedDictionary<string, object> tree, string prefix, string seg)
        {
            string fullPath = string.IsNullOrEmpty(prefix) ? seg : prefix + "/" + seg;
            var value = tree[seg];
            var groupData = value as GroupNode;

            var treeNode = new TreeNode(seg)
            {
                Tag = "__GROUP__" + fullPath,
                ImageIndex = 0,
                SelectedImageIndex = 0
            };
            treeNode.NodeFont = new Font(treeViewDevices.Font, FontStyle.Bold);

            if (groupData != null)
            {
                foreach (var device in groupData.Devices)
                    treeNode.Nodes.Add(CreateDeviceNode(device));
                foreach (var childKv in groupData.Children)
                    BuildNodeRecursive(treeNode.Nodes, groupData.Children, fullPath, childKv.Key);
            }

            parentNodes.Add(treeNode);
            treeNode.Expand();
        }

        private void SavePersistedGroups()
        {
            try
            {
                var json = DeviceGroupInfo.SerializeGroups(_persistedGroups.Values);
                File.WriteAllText(ConfigService.Instance.GroupsFilePath, json);
            }
            catch { }
        }

        private void SaveRunningState()
        {
            try
            {
                var ids = _devices
                    .Where(d => DataCollectionService.Instance.IsDeviceRunning(d.Id))
                    .Select(d => d.Id)
                    .ToList();
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(ids);
                File.WriteAllText(RunningStatePath(), json);
                Logger.Debug(string.Format("运行状态已保存: {0} 台设备", ids.Count));
            }
            catch { }
        }

        /// <summary>
        /// 将新设备插入到同组设备之后（组内末尾）
        /// </summary>
        private void InsertDeviceInGroupOrder(DeviceConfig device)
        {
            string grp = device.Group ?? "";
            // 找到该组最后一个设备的位置
            int lastIdx = -1;
            for (int i = _devices.Count - 1; i >= 0; i--)
            {
                if ((_devices[i].Group ?? "") == grp)
                { lastIdx = i; break; }
            }
            if (lastIdx >= 0)
                _devices.Insert(lastIdx + 1, device);
            else
                _devices.Add(device);
        }

        private List<string> LoadRunningState()
        {
            try
            {
                string path = RunningStatePath();
                if (!File.Exists(path)) return new List<string>();
                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(path))
                    ?? new List<string>();
            }
            catch { return new List<string>(); }
        }

        private async void AutoStartIfNeeded()
        {
            var ids = LoadRunningState();
            if (ids.Count == 0) return;

            await Task.Delay(2000); // 等配置和服务初始化完成

            int started = 0;
            foreach (var id in ids)
            {
                try
                {
                    var device = _devices.FirstOrDefault(d => d.Id == id);
                    if (device != null)
                    {
                        await DataCollectionService.Instance.StartDeviceAsync(device);
                        started++;
                    }
                }
                catch { }
            }

            if (started > 0)
            {
                Logger.Info(string.Format("自动恢复采集: {0}/{1} 台设备", started, ids.Count));
            if (this.IsHandleCreated && !this.IsDisposed) this.BeginInvoke((Action)(() => RefreshDeviceList()));
            }
        }

        private static Bitmap CreateFolderIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // Folder body
                using (var brush = new SolidBrush(Color.FromArgb(255, 209, 128)))
                    g.FillRectangle(brush, 1, 3, 14, 11);
                // Folder tab
                using (var brush = new SolidBrush(Color.FromArgb(255, 188, 102)))
                    g.FillRectangle(brush, 1, 1, 7, 4);
                // Border
                using (var pen = new Pen(Color.FromArgb(230, 160, 70)))
                {
                    g.DrawLines(pen, new[] { new Point(1, 5), new Point(1, 1), new Point(8, 1), new Point(8, 5) });
                    g.DrawRectangle(pen, 1, 3, 13, 11);
                }
            }
            return bmp;
        }

        private static Bitmap CreateDeviceIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // Chip body
                using (var brush = new SolidBrush(Color.FromArgb(100, 140, 180)))
                    g.FillRectangle(brush, 3, 3, 10, 10);
                // Pins
                using (var pen = new Pen(Color.FromArgb(80, 110, 150), 1.5f))
                {
                    g.DrawLine(pen, 1, 5, 3, 5);
                    g.DrawLine(pen, 1, 8, 3, 8);
                    g.DrawLine(pen, 1, 11, 3, 11);
                    g.DrawLine(pen, 13, 5, 15, 5);
                    g.DrawLine(pen, 13, 8, 15, 8);
                    g.DrawLine(pen, 13, 11, 15, 11);
                }
            }
            return bmp;
        }

        /// <summary>
        /// 检查设备数是否超过授权上限
        /// </summary>
        private bool CheckDeviceLimit()
        {
            var license = LicenseService.Instance.GetCurrentLicense();
            if (license == null || !LicenseService.Instance.IsActivated())
                return true; // 未激活状态由登录流程拦截，此处防御性放行

            int currentCount = _devices.Count;
            int maxDevices = license.MaxDevices;

            if (currentCount >= maxDevices)
            {
                var Lc = LanguageManager.Instance;
                MessageBox.Show(
                    Lc.GetString("LicenseInfo_DeviceLimitReached") + "\n" +
                    Lc.GetString("LicenseInfo_MaxDevices") + "：" + maxDevices + " 台\n" +
                    Lc.GetString("LicenseInfo_CurrentDevices") + "：" + currentCount + " 台\n\n" +
                    Lc.GetString("LicenseInfo_DeviceLimitHint"),
                    Lc.GetString("Menu_Help_License"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 显示授权信息窗口
        /// </summary>
        private void ShowLicenseInfo()
        {
            var license = LicenseService.Instance.GetCurrentLicense();
            var machineId = LicenseService.Instance.GetMachineId();
            var Lc = LanguageManager.Instance;

            string info;
            if (license != null)
            {
                info = Lc.GetString("LicenseInfo_MachineId") + "：" + Environment.NewLine + machineId + Environment.NewLine + Environment.NewLine +
                       Lc.GetString("LicenseInfo_Type") + "：" + license.LicenseTypeDisplay + Environment.NewLine +
                       Lc.GetString("LicenseInfo_MaxDevices") + "：" + license.MaxDevices + " 台" + Environment.NewLine +
                       Lc.GetString("LicenseInfo_Expire") + "：" +
                       (license.IsPermanent ? Lc.GetString("LicenseInfo_Permanent") : license.ExpireDate) + Environment.NewLine +
                       Lc.GetString("LicenseInfo_Created") + "：" + license.CreatedAt;
            }
            else
            {
                info = Lc.GetString("LicenseInfo_MachineId") + "：" + Environment.NewLine + machineId + Environment.NewLine + Environment.NewLine +
                       Lc.GetString("LicenseInfo_NotActivated");
            }

            var result = MessageBox.Show(info + Environment.NewLine + Environment.NewLine + Lc.GetString("LicenseInfo_Reactivate"),
                Lc.GetString("Menu_Help_License"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                using (var activationForm = new ActivationForm())
                {
                    if (activationForm.ShowDialog() == DialogResult.OK && activationForm.ActivationSuccess)
                    {
                        MessageBox.Show(
                            Lc.GetString("Activation_Success"),
                            Lc.GetString("Activation_Title"),
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        #region 模板引擎 + 设备克隆

        private async void CloneSelectedDevice()
        {
            var node = treeViewDevices.SelectedNode;
            if (!(node?.Tag is DeviceConfig dev)) return;

            bool isCollecting = DataCollectionService.Instance.IsDeviceRunning(dev.Id);
            using (var form = new DeviceCloneForm(dev, isCollecting))
            {
                if (form.ShowDialog() == DialogResult.OK && form.Result?.Success == true)
                {
                    RefreshDeviceTree();
                    if (isCollecting)
                    {
                        var clonedDev = ConfigService.Instance.GetAllDevices()
                            .FirstOrDefault(d => d.Id == form.Result.NewDeviceId);
                        if (clonedDev != null)
                        {
                            await DataCollectionService.Instance.StartDeviceAsync(clonedDev);
                        }
                    }
                    Logger.Info($"设备克隆成功: {dev.Name} → {form.Result.NewDeviceName}");
                }
            }
        }

        private void GenerateTemplateFromSelectedDevice()
        {
            var node = treeViewDevices.SelectedNode;
            if (!(node?.Tag is DeviceConfig dev)) return;

            using (var form = new TemplateGeneratorForm(dev.Id, dev.Name, dev.DriverType))
            {
                form.ShowDialog();
            }
        }

        private void ApplyTemplateToSelectedDevice()
        {
            var node = treeViewDevices.SelectedNode;
            if (!(node?.Tag is DeviceConfig dev)) return;

            using (var form = new TemplateApplyForm(dev.Id, dev.Name))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    RefreshDeviceTree();
                }
            }
        }

        private void OverwriteTemplateFromSelectedDevice()
        {
            var node = treeViewDevices.SelectedNode;
            if (!(node?.Tag is DeviceConfig dev)) return;

            // 打开管理器让用户选择目标模板
            using (var mgr = new TemplateManagerForm())
            {
                if (mgr.ShowDialog() == DialogResult.OK && mgr.SelectedTemplate != null)
                {
                    try
                    {
                        var updated = TemplateService.Instance.OverwriteFromDevice(
                            mgr.SelectedTemplate.TemplateId, dev.Id);
                        MessageBox.Show(
                            $"模板覆盖成功！\n版本: {updated.Version}\n更新时间: {updated.UpdatedAt:yyyy-MM-dd HH:mm:ss}",
                            "覆盖配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Logger.Info($"模板覆盖成功: {updated.TemplateName} v{updated.Version} ← {dev.Name}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"模板覆盖失败: {ex.Message}", "错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Logger.Error("模板覆盖失败: " + ex.Message);
                    }
                }
            }
        }

        #endregion

    }
}
