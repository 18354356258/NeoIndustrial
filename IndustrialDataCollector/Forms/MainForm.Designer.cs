namespace IndustrialDataCollection.Forms
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.文件ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.导入配置ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.导出配置ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.注销ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.退出ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.设备ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.添加设备ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.编辑设备ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.删除设备ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.启动采集ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.停止采集ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.全部启动ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.全部停止ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.工具ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mqtt配置ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.数据库配置ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.API服务配置ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.清空数据ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.导出CSVToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.帮助ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.关于ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.toolBtnAddDevice = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator10 = new System.Windows.Forms.ToolStripSeparator();
            this.toolBtnEditDevice = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator11 = new System.Windows.Forms.ToolStripSeparator();
            this.toolBtnDeleteDevice = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.toolBtnStartAll = new System.Windows.Forms.ToolStripButton();
            this.toolBtnStopAll = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            this.toolBtnMqtt = new System.Windows.Forms.ToolStripButton();
            this.toolBtnDbConfig = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.toolBtnLanguage = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator8 = new System.Windows.Forms.ToolStripSeparator();
            this.toolBtnLog = new System.Windows.Forms.ToolStripButton();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.panelLeft = new System.Windows.Forms.Panel();
            this.listViewDevices = new System.Windows.Forms.ListView();
            this.colDeviceName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colDriverType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.panelRight = new System.Windows.Forms.Panel();
            this.panelMonitorToolbar = new System.Windows.Forms.Panel();
            this.btnClear = new System.Windows.Forms.Button();
            this.btnCollect = new System.Windows.Forms.Button();
            this.btnExportCsv = new System.Windows.Forms.Button();
            this.chkAutoScroll = new System.Windows.Forms.CheckBox();
            this.dataGridViewMonitor = new System.Windows.Forms.DataGridView();
            this.colMonDevice = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMonVariable = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMonDataType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMonValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMonUnit = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMonTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusSystem = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusMqtt = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusDevices = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusRate = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusTime = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.contextMenuDevice = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.ctxStart = new System.Windows.Forms.ToolStripMenuItem();
            this.ctxStop = new System.Windows.Forms.ToolStripMenuItem();
            this.ctxSep1 = new System.Windows.Forms.ToolStripSeparator();
            this.ctxAdd = new System.Windows.Forms.ToolStripMenuItem();
            this.ctxEdit = new System.Windows.Forms.ToolStripMenuItem();
            this.ctxDelete = new System.Windows.Forms.ToolStripMenuItem();
            this.menuStrip.SuspendLayout();
            this.toolStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.panelLeft.SuspendLayout();
            this.panelRight.SuspendLayout();
            this.panelMonitorToolbar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewMonitor)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.contextMenuDevice.SuspendLayout();
            this.SuspendLayout();
            //
            // menuStrip
            //
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.文件ToolStripMenuItem,
            this.设备ToolStripMenuItem,
            this.工具ToolStripMenuItem,
            this.帮助ToolStripMenuItem});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(1200, 25);
            this.menuStrip.TabIndex = 0;
            this.menuStrip.Text = "menuStrip";
            //
            // 文件ToolStripMenuItem
            //
            this.文件ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.导入配置ToolStripMenuItem,
            this.导出配置ToolStripMenuItem,
            this.注销ToolStripMenuItem,
            this.toolStripSeparator1,
            this.退出ToolStripMenuItem});
            this.文件ToolStripMenuItem.Name = "文件ToolStripMenuItem";
            this.文件ToolStripMenuItem.Size = new System.Drawing.Size(58, 21);
            this.文件ToolStripMenuItem.Text = "文件(&F)";
            //
            // 导入配置ToolStripMenuItem
            //
            this.导入配置ToolStripMenuItem.Name = "导入配置ToolStripMenuItem";
            this.导入配置ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.导入配置ToolStripMenuItem.Text = "导入配置";
            this.导入配置ToolStripMenuItem.Click += new System.EventHandler(this.导入配置ToolStripMenuItem_Click);
            //
            // 导出配置ToolStripMenuItem
            //
            this.导出配置ToolStripMenuItem.Name = "导出配置ToolStripMenuItem";
            this.导出配置ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.导出配置ToolStripMenuItem.Text = "导出配置";
            this.导出配置ToolStripMenuItem.Click += new System.EventHandler(this.导出配置ToolStripMenuItem_Click);
            //
            // 注销ToolStripMenuItem
            //
            this.注销ToolStripMenuItem.Name = "注销ToolStripMenuItem";
            this.注销ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.注销ToolStripMenuItem.Text = "注销(&L)";
            this.注销ToolStripMenuItem.Click += new System.EventHandler(this.注销ToolStripMenuItem_Click);
            //
            // toolStripSeparator1
            //
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(121, 6);
            //
            // 退出ToolStripMenuItem
            //
            this.退出ToolStripMenuItem.Name = "退出ToolStripMenuItem";
            this.退出ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.退出ToolStripMenuItem.Text = "退出(&X)";
            this.退出ToolStripMenuItem.Click += new System.EventHandler(this.退出ToolStripMenuItem_Click);
            //
            // 设备ToolStripMenuItem
            //
            this.设备ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.添加设备ToolStripMenuItem,
            this.编辑设备ToolStripMenuItem,
            this.删除设备ToolStripMenuItem,
            this.toolStripSeparator2,
            this.启动采集ToolStripMenuItem,
            this.停止采集ToolStripMenuItem,
            this.toolStripSeparator3,
            this.全部启动ToolStripMenuItem,
            this.全部停止ToolStripMenuItem});
            this.设备ToolStripMenuItem.Name = "设备ToolStripMenuItem";
            this.设备ToolStripMenuItem.Size = new System.Drawing.Size(61, 21);
            this.设备ToolStripMenuItem.Text = "设备(&D)";
            //
            // 添加设备ToolStripMenuItem
            //
            this.添加设备ToolStripMenuItem.Name = "添加设备ToolStripMenuItem";
            this.添加设备ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.添加设备ToolStripMenuItem.Text = "添加设备";
            this.添加设备ToolStripMenuItem.Click += new System.EventHandler(this.添加设备ToolStripMenuItem_Click);
            //
            // 编辑设备ToolStripMenuItem
            //
            this.编辑设备ToolStripMenuItem.Name = "编辑设备ToolStripMenuItem";
            this.编辑设备ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.编辑设备ToolStripMenuItem.Text = "编辑设备";
            this.编辑设备ToolStripMenuItem.Click += new System.EventHandler(this.编辑设备ToolStripMenuItem_Click);
            //
            // 删除设备ToolStripMenuItem
            //
            this.删除设备ToolStripMenuItem.Name = "删除设备ToolStripMenuItem";
            this.删除设备ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.删除设备ToolStripMenuItem.Text = "删除设备";
            this.删除设备ToolStripMenuItem.Click += new System.EventHandler(this.删除设备ToolStripMenuItem_Click);
            //
            // toolStripSeparator2
            //
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(121, 6);
            //
            // 启动采集ToolStripMenuItem
            //
            this.启动采集ToolStripMenuItem.Name = "启动采集ToolStripMenuItem";
            this.启动采集ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.启动采集ToolStripMenuItem.Text = "启动采集";
            this.启动采集ToolStripMenuItem.Click += new System.EventHandler(this.启动采集ToolStripMenuItem_Click);
            //
            // 停止采集ToolStripMenuItem
            //
            this.停止采集ToolStripMenuItem.Name = "停止采集ToolStripMenuItem";
            this.停止采集ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.停止采集ToolStripMenuItem.Text = "停止采集";
            this.停止采集ToolStripMenuItem.Click += new System.EventHandler(this.停止采集ToolStripMenuItem_Click);
            //
            // toolStripSeparator3
            //
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(121, 6);
            //
            // 全部启动ToolStripMenuItem
            //
            this.全部启动ToolStripMenuItem.Name = "全部启动ToolStripMenuItem";
            this.全部启动ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.全部启动ToolStripMenuItem.Text = "全部启动";
            this.全部启动ToolStripMenuItem.Click += new System.EventHandler(this.全部启动ToolStripMenuItem_Click);
            //
            // 全部停止ToolStripMenuItem
            //
            this.全部停止ToolStripMenuItem.Name = "全部停止ToolStripMenuItem";
            this.全部停止ToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.全部停止ToolStripMenuItem.Text = "全部停止";
            this.全部停止ToolStripMenuItem.Click += new System.EventHandler(this.全部停止ToolStripMenuItem_Click);
            //
            // 工具ToolStripMenuItem
            //
            this.工具ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mqtt配置ToolStripMenuItem,
            this.数据库配置ToolStripMenuItem,
            this.API服务配置ToolStripMenuItem,
            this.toolStripSeparator4,
            this.清空数据ToolStripMenuItem,
            this.导出CSVToolStripMenuItem});
            this.工具ToolStripMenuItem.Name = "工具ToolStripMenuItem";
            this.工具ToolStripMenuItem.Size = new System.Drawing.Size(59, 21);
            this.工具ToolStripMenuItem.Text = "工具(&T)";
            //
            // mqtt配置ToolStripMenuItem
            //
            this.mqtt配置ToolStripMenuItem.Name = "mqtt配置ToolStripMenuItem";
            this.mqtt配置ToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.mqtt配置ToolStripMenuItem.Text = "MQTT 配置";
            this.mqtt配置ToolStripMenuItem.Click += new System.EventHandler(this.mqtt配置ToolStripMenuItem_Click);
            //
            // 数据库配置ToolStripMenuItem
            //
            this.数据库配置ToolStripMenuItem.Name = "数据库配置ToolStripMenuItem";
            this.数据库配置ToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.数据库配置ToolStripMenuItem.Text = "数据库配置";
            this.数据库配置ToolStripMenuItem.Click += new System.EventHandler(this.数据库配置ToolStripMenuItem_Click);
            //
            // API服务配置ToolStripMenuItem
            //
            this.API服务配置ToolStripMenuItem.Name = "API服务配置ToolStripMenuItem";
            this.API服务配置ToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.API服务配置ToolStripMenuItem.Text = "REST API 服务配置";
            this.API服务配置ToolStripMenuItem.Click += new System.EventHandler(this.API服务配置ToolStripMenuItem_Click);
            //
            // toolStripSeparator4
            //
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(177, 6);
            //
            // 清空数据ToolStripMenuItem
            //
            this.清空数据ToolStripMenuItem.Name = "清空数据ToolStripMenuItem";
            this.清空数据ToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.清空数据ToolStripMenuItem.Text = "清空数据";
            this.清空数据ToolStripMenuItem.Click += new System.EventHandler(this.清空数据ToolStripMenuItem_Click);
            //
            // 导出CSVToolStripMenuItem
            //
            this.导出CSVToolStripMenuItem.Name = "导出CSVToolStripMenuItem";
            this.导出CSVToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.导出CSVToolStripMenuItem.Text = "导出 CSV";
            this.导出CSVToolStripMenuItem.Click += new System.EventHandler(this.导出CSVToolStripMenuItem_Click);
            //
            // 帮助ToolStripMenuItem
            //
            this.帮助ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.关于ToolStripMenuItem});
            this.帮助ToolStripMenuItem.Name = "帮助ToolStripMenuItem";
            this.帮助ToolStripMenuItem.Size = new System.Drawing.Size(61, 21);
            this.帮助ToolStripMenuItem.Text = "帮助(&H)";
            //
            // 关于ToolStripMenuItem
            //
            this.关于ToolStripMenuItem.Name = "关于ToolStripMenuItem";
            this.关于ToolStripMenuItem.Size = new System.Drawing.Size(100, 22);
            this.关于ToolStripMenuItem.Text = "关于";
            this.关于ToolStripMenuItem.Click += new System.EventHandler(this.关于ToolStripMenuItem_Click);
            //
            // toolStrip
            //
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolBtnAddDevice,
            this.toolStripSeparator10,
            this.toolBtnEditDevice,
            this.toolStripSeparator11,
            this.toolBtnDeleteDevice,
            this.toolStripSeparator5,
            this.toolBtnStartAll,
            this.toolBtnStopAll,
            this.toolStripSeparator6,
            this.toolBtnMqtt,
            this.toolBtnDbConfig,
            this.toolStripSeparator7,
            this.toolBtnLanguage,
            this.toolStripSeparator8,
            this.toolBtnLog});
            this.toolStrip.Location = new System.Drawing.Point(0, 25);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(1200, 25);
            this.toolStrip.TabIndex = 1;
            this.toolStrip.Text = "toolStrip";
            //
            // toolBtnAddDevice
            //
            this.toolBtnAddDevice.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolBtnAddDevice.Name = "toolBtnAddDevice";
            this.toolBtnAddDevice.Size = new System.Drawing.Size(60, 22);
            this.toolBtnAddDevice.Text = "添加设备";
            this.toolBtnAddDevice.Click += new System.EventHandler(this.toolBtnAddDevice_Click);
            //
            // toolStripSeparator10
            //
            this.toolStripSeparator10.Name = "toolStripSeparator10";
            this.toolStripSeparator10.Size = new System.Drawing.Size(6, 25);
            //
            // toolBtnEditDevice
            //
            this.toolBtnEditDevice.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolBtnEditDevice.Name = "toolBtnEditDevice";
            this.toolBtnEditDevice.Size = new System.Drawing.Size(36, 22);
            this.toolBtnEditDevice.Text = "编辑";
            this.toolBtnEditDevice.Click += new System.EventHandler(this.toolBtnEditDevice_Click);
            //
            // toolStripSeparator11
            //
            this.toolStripSeparator11.Name = "toolStripSeparator11";
            this.toolStripSeparator11.Size = new System.Drawing.Size(6, 25);
            //
            // toolBtnDeleteDevice
            //
            this.toolBtnDeleteDevice.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolBtnDeleteDevice.Name = "toolBtnDeleteDevice";
            this.toolBtnDeleteDevice.Size = new System.Drawing.Size(36, 22);
            this.toolBtnDeleteDevice.Text = "删除";
            this.toolBtnDeleteDevice.Click += new System.EventHandler(this.toolBtnDeleteDevice_Click);
            //
            // toolStripSeparator5
            //
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new System.Drawing.Size(6, 25);
            //
            // toolBtnStartAll
            //
            this.toolBtnStartAll.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolBtnStartAll.Name = "toolBtnStartAll";
            this.toolBtnStartAll.Size = new System.Drawing.Size(60, 22);
            this.toolBtnStartAll.Text = "全部启动";
            this.toolBtnStartAll.Click += new System.EventHandler(this.toolBtnStartAll_Click);
            //
            // toolBtnStopAll
            //
            this.toolBtnStopAll.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolBtnStopAll.Name = "toolBtnStopAll";
            this.toolBtnStopAll.Size = new System.Drawing.Size(60, 22);
            this.toolBtnStopAll.Text = "全部停止";
            this.toolBtnStopAll.Click += new System.EventHandler(this.toolBtnStopAll_Click);
            //
            // toolStripSeparator6
            //
            this.toolStripSeparator6.Name = "toolStripSeparator6";
            this.toolStripSeparator6.Size = new System.Drawing.Size(6, 25);
            //
            // toolBtnMqtt
            //
            this.toolBtnMqtt.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolBtnMqtt.Name = "toolBtnMqtt";
            this.toolBtnMqtt.Size = new System.Drawing.Size(72, 22);
            this.toolBtnMqtt.Text = "MQTT配置";
            this.toolBtnMqtt.Click += new System.EventHandler(this.toolBtnMqtt_Click);
            //
            // toolBtnDbConfig
            //
            this.toolBtnDbConfig.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolBtnDbConfig.Name = "toolBtnDbConfig";
            this.toolBtnDbConfig.Size = new System.Drawing.Size(72, 22);
            this.toolBtnDbConfig.Text = "数据库配置";
            this.toolBtnDbConfig.Click += new System.EventHandler(this.toolBtnDbConfig_Click);
            //
            // toolStripSeparator7
            //
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            this.toolStripSeparator7.Size = new System.Drawing.Size(6, 25);
            //
            // toolBtnLanguage
            //
            this.toolBtnLanguage.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolBtnLanguage.Name = "toolBtnLanguage";
            this.toolBtnLanguage.Size = new System.Drawing.Size(82, 22);
            this.toolBtnLanguage.Text = "中文/English";
            this.toolBtnLanguage.Click += new System.EventHandler(this.toolBtnLanguage_Click);
            //
            // toolStripSeparator8
            //
            this.toolStripSeparator8.Name = "toolStripSeparator8";
            this.toolStripSeparator8.Size = new System.Drawing.Size(6, 25);
            //
            // toolBtnLog
            //
            this.toolBtnLog.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolBtnLog.Name = "toolBtnLog";
            this.toolBtnLog.Size = new System.Drawing.Size(36, 22);
            this.toolBtnLog.Text = "日志";
            this.toolBtnLog.Click += new System.EventHandler(this.toolBtnLog_Click);
            //
            // splitContainer
            //
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 50);
            this.splitContainer.Name = "splitContainer";
            //
            // splitContainer.Panel1
            //
            this.splitContainer.Panel1.Controls.Add(this.panelLeft);
            this.splitContainer.Panel1MinSize = 200;
            //
            // splitContainer.Panel2
            //
            this.splitContainer.Panel2.Controls.Add(this.panelRight);
            this.splitContainer.Panel2MinSize = 400;
            this.splitContainer.Size = new System.Drawing.Size(1200, 652);
            this.splitContainer.SplitterDistance = 260;
            this.splitContainer.SplitterWidth = 5;
            this.splitContainer.TabIndex = 2;
            //
            // panelLeft
            //
            this.panelLeft.Controls.Add(this.listViewDevices);
            this.panelLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLeft.Location = new System.Drawing.Point(0, 0);
            this.panelLeft.Name = "panelLeft";
            this.panelLeft.Padding = new System.Windows.Forms.Padding(3);
            this.panelLeft.Size = new System.Drawing.Size(260, 652);
            this.panelLeft.TabIndex = 0;
            //
            // listViewDevices
            //
            this.listViewDevices.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colDeviceName,
            this.colDriverType,
            this.colStatus});
            this.listViewDevices.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listViewDevices.FullRowSelect = true;
            this.listViewDevices.GridLines = true;
            this.listViewDevices.HideSelection = false;
            this.listViewDevices.Location = new System.Drawing.Point(3, 3);
            this.listViewDevices.MultiSelect = false;
            this.listViewDevices.Name = "listViewDevices";
            this.listViewDevices.Size = new System.Drawing.Size(254, 646);
            this.listViewDevices.TabIndex = 0;
            this.listViewDevices.UseCompatibleStateImageBehavior = false;
            this.listViewDevices.View = System.Windows.Forms.View.Details;
            this.listViewDevices.DoubleClick += new System.EventHandler(this.listViewDevices_DoubleClick);
            this.listViewDevices.MouseClick += new System.Windows.Forms.MouseEventHandler(this.listViewDevices_MouseClick);
            //
            // colDeviceName
            //
            this.colDeviceName.Text = "设备名称";
            this.colDeviceName.Width = 90;
            //
            // colDriverType
            //
            this.colDriverType.Text = "驱动类型";
            this.colDriverType.Width = 80;
            //
            // colStatus
            //
            this.colStatus.Text = "状态";
            this.colStatus.Width = 70;
            //
            // panelRight
            //
            this.panelRight.Controls.Add(this.panelMonitorToolbar);
            this.panelRight.Controls.Add(this.dataGridViewMonitor);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(0, 0);
            this.panelRight.Name = "panelRight";
            this.panelRight.Size = new System.Drawing.Size(935, 652);
            this.panelRight.TabIndex = 0;
            //
            // panelMonitorToolbar
            //
            this.panelMonitorToolbar.Controls.Add(this.btnClear);
            this.panelMonitorToolbar.Controls.Add(this.btnCollect);
            this.panelMonitorToolbar.Controls.Add(this.btnExportCsv);
            this.panelMonitorToolbar.Controls.Add(this.chkAutoScroll);
            this.panelMonitorToolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelMonitorToolbar.Location = new System.Drawing.Point(0, 0);
            this.panelMonitorToolbar.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.panelMonitorToolbar.Name = "panelMonitorToolbar";
            this.panelMonitorToolbar.Size = new System.Drawing.Size(935, 35);
            this.panelMonitorToolbar.TabIndex = 1;
            //
            // btnClear
            //
            this.btnClear.Location = new System.Drawing.Point(82, 4);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(80, 28);
            this.btnClear.TabIndex = 0;
            this.btnClear.Text = "清空";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            //
            // btnCollect
            //
            this.btnCollect.Location = new System.Drawing.Point(168, 4);
            this.btnCollect.Name = "btnCollect";
            this.btnCollect.Size = new System.Drawing.Size(80, 28);
            this.btnCollect.TabIndex = 3;
            this.btnCollect.Text = "采集";
            this.btnCollect.UseVisualStyleBackColor = true;
            this.btnCollect.Click += new System.EventHandler(this.btnCollect_Click);
            //
            // btnExportCsv
            //
            this.btnExportCsv.Location = new System.Drawing.Point(841, 4);
            this.btnExportCsv.Name = "btnExportCsv";
            this.btnExportCsv.Size = new System.Drawing.Size(82, 28);
            this.btnExportCsv.TabIndex = 1;
            this.btnExportCsv.Text = "导出CSV";
            this.btnExportCsv.UseVisualStyleBackColor = true;
            this.btnExportCsv.Click += new System.EventHandler(this.btnExportCsv_Click);
            //
            // chkAutoScroll
            //
            this.chkAutoScroll.AutoSize = true;
            this.chkAutoScroll.Checked = true;
            this.chkAutoScroll.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkAutoScroll.Location = new System.Drawing.Point(3, 8);
            this.chkAutoScroll.Name = "chkAutoScroll";
            this.chkAutoScroll.Size = new System.Drawing.Size(71, 20);
            this.chkAutoScroll.TabIndex = 2;
            this.chkAutoScroll.Text = "自动滚动";
            this.chkAutoScroll.UseVisualStyleBackColor = true;
            //
            // dataGridViewMonitor
            //
            this.dataGridViewMonitor.AllowUserToAddRows = false;
            this.dataGridViewMonitor.AllowUserToDeleteRows = false;
            this.dataGridViewMonitor.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridViewMonitor.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewMonitor.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewMonitor.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colMonDevice,
            this.colMonVariable,
            this.colMonDataType,
            this.colMonValue,
            this.colMonUnit,
            this.colMonTime});
            this.dataGridViewMonitor.Location = new System.Drawing.Point(3, 35);
            this.dataGridViewMonitor.Margin = new System.Windows.Forms.Padding(0);
            this.dataGridViewMonitor.Name = "dataGridViewMonitor";
            this.dataGridViewMonitor.ReadOnly = true;
            this.dataGridViewMonitor.RowHeadersVisible = false;
            this.dataGridViewMonitor.RowTemplate.Height = 24;
            this.dataGridViewMonitor.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewMonitor.Size = new System.Drawing.Size(929, 614);
            this.dataGridViewMonitor.TabIndex = 0;
            //
            // colMonDevice
            //
            this.colMonDevice.HeaderText = "设备";
            this.colMonDevice.Name = "colMonDevice";
            this.colMonDevice.ReadOnly = true;
            //
            // colMonVariable
            //
            this.colMonVariable.HeaderText = "变量名";
            this.colMonVariable.Name = "colMonVariable";
            this.colMonVariable.ReadOnly = true;
            //
            // colMonDataType
            //
            this.colMonDataType.HeaderText = "数据类型";
            this.colMonDataType.Name = "colMonDataType";
            this.colMonDataType.ReadOnly = true;
            //
            // colMonValue
            //
            this.colMonValue.HeaderText = "数值";
            this.colMonValue.Name = "colMonValue";
            this.colMonValue.ReadOnly = true;
            //
            // colMonUnit
            //
            this.colMonUnit.HeaderText = "单位";
            this.colMonUnit.Name = "colMonUnit";
            this.colMonUnit.ReadOnly = true;
            //
            // colMonTime
            //
            this.colMonTime.HeaderText = "时间戳";
            this.colMonTime.Name = "colMonTime";
            this.colMonTime.ReadOnly = true;
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusSystem,
            this.toolStripStatusMqtt,
            this.toolStripStatusDevices,
            this.toolStripStatusRate,
            this.toolStripStatusTime,
            this.toolStripStatusLabel1});
            this.statusStrip.Location = new System.Drawing.Point(0, 702);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1200, 26);
            this.statusStrip.TabIndex = 3;
            this.statusStrip.Text = "statusStrip";
            //
            // toolStripStatusSystem
            //
            this.toolStripStatusSystem.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.toolStripStatusSystem.Name = "toolStripStatusSystem";
            this.toolStripStatusSystem.Size = new System.Drawing.Size(36, 21);
            this.toolStripStatusSystem.Text = "就绪";
            //
            // toolStripStatusMqtt
            //
            this.toolStripStatusMqtt.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.toolStripStatusMqtt.Name = "toolStripStatusMqtt";
            this.toolStripStatusMqtt.Size = new System.Drawing.Size(88, 21);
            this.toolStripStatusMqtt.Text = "MQTT 未连接";
            //
            // toolStripStatusDevices
            //
            this.toolStripStatusDevices.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.toolStripStatusDevices.Name = "toolStripStatusDevices";
            this.toolStripStatusDevices.Size = new System.Drawing.Size(74, 21);
            this.toolStripStatusDevices.Text = "设备数: 0/0";
            //
            // toolStripStatusRate
            //
            this.toolStripStatusRate.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.toolStripStatusRate.Name = "toolStripStatusRate";
            this.toolStripStatusRate.Size = new System.Drawing.Size(78, 21);
            this.toolStripStatusRate.Text = "采集: 0 变量";
            //
            // toolStripStatusTime
            //
            this.toolStripStatusTime.Name = "toolStripStatusTime";
            this.toolStripStatusTime.Size = new System.Drawing.Size(0, 21);
            //
            // toolStripStatusLabel1
            //
            this.toolStripStatusLabel1.ForeColor = System.Drawing.Color.Black;
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(337, 21);
            this.toolStripStatusLabel1.Text = "© 2026 zhangchenglong. 工业网络数采平台. 保留所有权利.";
            //
            // contextMenuDevice
            //
            this.ctxSep2 = new System.Windows.Forms.ToolStripSeparator();
            this.ctxRefresh = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuDevice.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ctxStart,
            this.ctxStop,
            this.ctxSep1,
            this.ctxAdd,
            this.ctxEdit,
            this.ctxDelete,
            this.ctxSep2,
            this.ctxRefresh});
            this.contextMenuDevice.Name = "contextMenuDevice";
            this.contextMenuDevice.Size = new System.Drawing.Size(125, 120);
            //
            // ctxStart
            //
            this.ctxStart.Name = "ctxStart";
            this.ctxStart.Size = new System.Drawing.Size(124, 22);
            this.ctxStart.Text = "启动采集";
            this.ctxStart.Click += new System.EventHandler(this.启动采集ToolStripMenuItem_Click);
            //
            // ctxStop
            //
            this.ctxStop.Name = "ctxStop";
            this.ctxStop.Size = new System.Drawing.Size(124, 22);
            this.ctxStop.Text = "停止采集";
            this.ctxStop.Click += new System.EventHandler(this.停止采集ToolStripMenuItem_Click);
            //
            // ctxSep1
            //
            this.ctxSep1.Name = "ctxSep1";
            this.ctxSep1.Size = new System.Drawing.Size(121, 6);
            //
            // ctxAdd
            //
            this.ctxAdd.Name = "ctxAdd";
            this.ctxAdd.Size = new System.Drawing.Size(124, 22);
            this.ctxAdd.Text = "添加设备";
            this.ctxAdd.Click += new System.EventHandler(this.添加设备ToolStripMenuItem_Click);
            //
            // ctxEdit
            //
            this.ctxEdit.Name = "ctxEdit";
            this.ctxEdit.Size = new System.Drawing.Size(124, 22);
            this.ctxEdit.Text = "编辑设备";
            this.ctxEdit.Click += new System.EventHandler(this.编辑设备ToolStripMenuItem_Click);
            //
            // ctxDelete
            //
            this.ctxDelete.Name = "ctxDelete";
            this.ctxDelete.Size = new System.Drawing.Size(124, 22);
            this.ctxDelete.Text = "删除设备";
            this.ctxDelete.Click += new System.EventHandler(this.删除设备ToolStripMenuItem_Click);
            //
            // ctxSep2
            //
            this.ctxSep2.Name = "ctxSep2";
            this.ctxSep2.Size = new System.Drawing.Size(121, 6);
            //
            // ctxRefresh
            //
            this.ctxRefresh.Name = "ctxRefresh";
            this.ctxRefresh.Size = new System.Drawing.Size(124, 22);
            this.ctxRefresh.Text = "刷新列表";
            this.ctxRefresh.Click += new System.EventHandler(this.ctxRefresh_Click);
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 728);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.menuStrip);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.25F);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip;
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "MatriX_工业网络数采大师";
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.panelLeft.ResumeLayout(false);
            this.panelRight.ResumeLayout(false);
            this.panelMonitorToolbar.ResumeLayout(false);
            this.panelMonitorToolbar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewMonitor)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.contextMenuDevice.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        // ---- 控件字段声明 ----
        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem 文件ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 导入配置ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 导出配置ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 注销ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem 退出ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 设备ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 添加设备ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 编辑设备ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 删除设备ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem 启动采集ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 停止采集ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem 全部启动ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 全部停止ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 工具ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem mqtt配置ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 数据库配置ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem 清空数据ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 导出CSVToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem API服务配置ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 帮助ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 关于ToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuDevice;
        private System.Windows.Forms.ToolStripMenuItem ctxStart;
        private System.Windows.Forms.ToolStripMenuItem ctxStop;
        private System.Windows.Forms.ToolStripSeparator ctxSep1;
        private System.Windows.Forms.ToolStripMenuItem ctxAdd;
        private System.Windows.Forms.ToolStripMenuItem ctxEdit;
        private System.Windows.Forms.ToolStripMenuItem ctxDelete;
        private System.Windows.Forms.ToolStripSeparator ctxSep2;
        private System.Windows.Forms.ToolStripMenuItem ctxRefresh;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton toolBtnAddDevice;
        private System.Windows.Forms.ToolStripButton toolBtnEditDevice;
        private System.Windows.Forms.ToolStripButton toolBtnDeleteDevice;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.ToolStripButton toolBtnStartAll;
        private System.Windows.Forms.ToolStripButton toolBtnStopAll;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
        private System.Windows.Forms.ToolStripButton toolBtnMqtt;
        private System.Windows.Forms.ToolStripButton toolBtnLanguage;
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.Panel panelLeft;
        private System.Windows.Forms.ListView listViewDevices;
        private System.Windows.Forms.ColumnHeader colDeviceName;
        private System.Windows.Forms.ColumnHeader colDriverType;
        private System.Windows.Forms.ColumnHeader colStatus;
        private System.Windows.Forms.Panel panelRight;
        private System.Windows.Forms.Panel panelMonitorToolbar;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.Button btnCollect;
        private System.Windows.Forms.Button btnExportCsv;
        private System.Windows.Forms.CheckBox chkAutoScroll;
        private System.Windows.Forms.DataGridView dataGridViewMonitor;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMonDevice;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMonVariable;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMonDataType;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMonValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMonUnit;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMonTime;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusSystem;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusMqtt;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusDevices;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusRate;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusTime;
        private System.Windows.Forms.ToolStripButton toolBtnLog;
        private System.Windows.Forms.ToolStripButton toolBtnDbConfig;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator10;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator11;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator8;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
    }
}
