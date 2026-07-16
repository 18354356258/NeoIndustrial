using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using IndustrialDataCollection.Drivers;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Controls;
using IndustrialDataCollection.Utils;
using IndustrialDataCollection.Services;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// 设备配置界面 - 添加/编辑设备
    /// </summary>
    public partial class DeviceConfigForm : Form
    {
        private DeviceConfig _originalConfig;
        private bool _isEdit;
        private string _defaultGroup;
        private Label lblSimInfo;
        private ComboBox comboDriverCategory;
        private ComboBox cmbGroup;
        private Label lblGroup;
        private TextBox _txtTagPathCn;
        private System.Windows.Forms.Timer _tagGenTimer;
        private string _pendingTagName;
        // v2.0: txtNameEn 已移除，英文标签统一废弃
        private ComboBox _cmbMqttMode;
        private Label _lblMqttMode;
        private List<DataPoint> _workingPoints;
        private List<Control> _dynamicControls = new List<Control>();

        // 动态控件引用（按驱动类型不同）
        private ComboBox _comboPortName;
        private ComboBox _comboBaudRate;
        private ComboBox _comboParity;
        private ComboBox _comboDataBits;
        private ComboBox _comboStopBits;
        private TextBox _txtBrokerHost;
        private TextBox _txtBrokerPort;
        private TextBox _txtTopicFilter;
        private ComboBox _comboQos;
        private TextBox _txtMqttUser;
        private TextBox _txtMqttPwd;
        private TextBox _txtServerUrl;
        private TextBox _txtOpcUaUser;
        private TextBox _txtOpcUaPwd;
        private ComboBox _comboOpcUaSecurity;
        private ComboBox _comboOpcUaPolicy;
        private TextBox _txtBaseUrl;
        private ComboBox _comboCpuType;
        private ComboBox _comboFrameType;
        private ComboBox _comboFinsTransport;
        private ComboBox _comboHostLinkTransport;
        private ComboBox _comboPlcType;
        private TextBox _txtNetworkNo;
        private TextBox _txtMcStationNo;
        private TextBox _txtSourceNode;
        private TextBox _txtDestNode;
        private TextBox _txtSourceUnit;
        private TextBox _txtDestUnit;
        private TextBox _txtHostUnitNo;
        private TunnelSelectionControl _tunnelControl;
        private GroupBox _grpTunnel;
        private bool _suppressCategoryChanged;   // 防止 SelectDriverTypeInCategory 重复触发 PopulateDriversForCategory

        public DeviceConfig DeviceConfig { get; private set; }

        public DeviceConfigForm() : this(null, null) { }

        public DeviceConfigForm(DeviceConfig existingConfig, string defaultGroup = null)
        {
            InitializeComponent();

            // Widen form and controls for column visibility (v1.7.0)
            this.ClientSize = new Size(1100, this.ClientSize.Height);
            groupBasic.Width = 1076;
            groupConnection.Width = 1076;
            groupPoints.Width = 1076;
            dataGridViewPoints.Width = 1056;

            // Fix ByteOrder column: remove AutoSizeMode.Fill, give fixed width
            colPointByteOrder.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colPointByteOrder.Width = 80;

            // Enable word wrap for long data + taller rows
            dataGridViewPoints.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dataGridViewPoints.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

            // 添加语义标签列 (v2.0) — 插入到 ByteOrder 之后、Edge 之前
            int insertAfter = dataGridViewPoints.Columns["colPointByteOrder"]?.Index ?? dataGridViewPoints.Columns.Count - 2;
            var colTagCn = new DataGridViewTextBoxColumn
            {
                Name = "colTagCn",
                HeaderText = LanguageManager.Instance.GetString("TagCnColumn"),
                Width = 200,
                MinimumWidth = 60
            };
            dataGridViewPoints.Columns.Insert(insertAfter + 2, colTagCn);

            _defaultGroup = defaultGroup ?? "";
            _originalConfig = existingConfig?.Clone();
            _isEdit = existingConfig != null;
            _workingPoints = existingConfig?.DataPoints
                .Select(p => p.Clone()).ToList() ?? new List<DataPoint>();

            InitSimulatorInfoLabel();
            InitDriverCategoryCombo();
            InitCombo();
            InitGroupCombo();
            InitTunnelGroup();
            InitMqttPublishMode();
            LoadConfig();

            OnDriverTypeChanged();

            // 注册语言切换事件
            LanguageManager.Instance.LanguageChanged += (s, ev) =>
            {
                ApplyLanguage();
                if (_originalConfig != null)
                    SelectDriverTypeInCategory(_originalConfig.DriverType);
                RestoreDynamicValues();
            };
            ApplyLanguage();

            // 驱动类型回显修复：ApplyLanguage 中 PopulateDriversForCategory 会重置 SelectedIndex=0，
            // 必须在此之后重新选中保存的驱动类型，否则所有已配置设备打开都显示第一个驱动（ModbusTcp）
            if (_originalConfig != null)
                SelectDriverTypeInCategory(_originalConfig.DriverType);

            // 必须在 ApplyLanguage 之后调用！（ApplyLanguage 会触发 OnDriverTypeChanged 重建控件，覆盖值）
            RestoreDynamicValues();

            // 双击变量行 → 打开编辑
            dataGridViewPoints.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    dataGridViewPoints.Rows[e.RowIndex].Selected = true;
                    btnEditPoint_Click(s, e);
                }
            };

            colPointName.Width = 120;
            colPointAddress.Width = 140;
        }

        /// <summary>
        /// 应用多语言 - 刷新设备配置界面所有文字元素
        /// </summary>
        public void ApplyLanguage()
        {
            var L = LanguageManager.Instance;

            // 窗口标题
            this.Text = _isEdit ? L.GetString("DeviceConfig_Title_Edit") : L.GetString("DeviceConfig_Title_Add");

            // GroupBox
            groupConnection.Text = L.GetString("DeviceConfig_Group_Connection");
            groupBasic.Text = L.GetString("DeviceConfig_Group_Basic");

            // 基本配置
            lblName.Text = L.GetString("DeviceConfig_Name");
            lblDriverType.Text = L.GetString("DeviceConfig_DriverType");
            lblPollInterval.Text = L.GetString("DeviceConfig_PollInterval");
            chkEnabled.Text = L.GetString("DeviceConfig_Enabled");
            if (_lblMqttMode != null) _lblMqttMode.Text = L.GetString("MqttPublishMode") ?? "MQTT发布模式:";
            if (_cmbMqttMode != null)
            {
                _cmbMqttMode.Items[0] = L.GetString("MqttPublishMode_Resolved") ?? "Resolved (规范化 tag_id)";
                _cmbMqttMode.Items[1] = L.GetString("MqttPublishMode_Original") ?? "Original (原始格式)";
            }

            // 静态连接参数标签
            lblIP.Text = L.GetString("DeviceConfig_IP");
            lblPort.Text = L.GetString("DeviceConfig_Port");
            lblStation.Text = L.GetString("DeviceConfig_Station");
            lblRack.Text = L.GetString("DeviceConfig_Rack");
            lblSlot.Text = L.GetString("DeviceConfig_Slot");

            // 按钮
            btnTestConnect.Text = L.GetString("DeviceConfig_TestConnection");
            groupPoints.Text = L.GetString("DeviceConfig_PointTable");
            btnAddPoint.Text = L.GetString("DeviceConfig_AddPoint");
            btnEditPoint.Text = L.GetString("DeviceConfig_EditPoint");
            btnDeletePoint.Text = L.GetString("DeviceConfig_DeletePoint");
            btnImportPoints.Text = L.GetString("DeviceConfig_ImportPoints");
            btnExportPoints.Text = L.GetString("DeviceConfig_ExportPoints");
            btnSave.Text = L.GetString("DeviceConfig_Save");
            btnCancel.Text = L.GetString("DeviceConfig_Cancel");

            // 数据表格列头
            colPointName.HeaderText = L.GetString("DeviceConfig_PointName");
            colPointAddress.HeaderText = L.GetString("DeviceConfig_PointAddress");
            colPointDataType.HeaderText = L.GetString("DeviceConfig_PointDataType");
            colPointUnit.HeaderText = L.GetString("DeviceConfig_PointUnit");
            colPointScale.HeaderText = L.GetString("DeviceConfig_PointScale");
            colPointOffset.HeaderText = L.GetString("DeviceConfig_PointOffset");
            colPointLength.HeaderText = L.GetString("DeviceConfig_PointLength");
            colPointByteOrder.HeaderText = L.GetString("DeviceConfig_PointByteOrder"); colPointEdge.HeaderText = L.GetString("DeviceConfig_PointEdge"); colPointAlarm.HeaderText = L.GetString("DeviceConfig_PointAlarm");

            // 语义标签列头 (v1.7.0, 程序化添加的列)
            foreach (DataGridViewColumn col in dataGridViewPoints.Columns)
            {
                if (col.Name == "colTagCn") col.HeaderText = LanguageManager.Instance.GetString("TagCnColumn");
            }

            // 驱动类型下拉框 - 使用分类重新填充
            PopulateDriversForCategory();

            // 模拟器说明
            if (lblSimInfo != null)
                lblSimInfo.Text = L.GetString("DeviceConfig_SimInfo");
        }

        /// <summary>
        /// 初始化模拟器参数说明标签
        /// </summary>
        private void InitSimulatorInfoLabel()
        {
            var L = LanguageManager.Instance;
            lblSimInfo = new Label
            {
                Location = new Point(10, 25),
                Size = new Size(490, 60),
                Text = L.GetString("DeviceConfig_SimInfo"),
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            groupConnection.Controls.Add(lblSimInfo);
        }

        /// <summary>
        /// 初始化驱动分类下拉框
        /// </summary>
        private void InitDriverCategoryCombo()
        {
            var L = LanguageManager.Instance;
            // Category label - Row 1 right (Y=18), swapped above Driver Type
            // NOT added to _dynamicControls — must survive ClearDynamicControls
            var lblCategory = new Label { Text = L.GetString("DeviceConfig_DriverCategory"), Location = new Point(380, 21), Size = new Size(65, 22) };
            groupBasic.Controls.Add(lblCategory);

            comboDriverCategory = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(460, 18),
                Size = new Size(130, 23)
            };
            comboDriverCategory.Items.Add("工业常用");
            comboDriverCategory.Items.Add("PLC/工控");
            comboDriverCategory.Items.Add("数控系统");
            comboDriverCategory.Items.Add("楼宇自动化");
            comboDriverCategory.Items.Add("电力/能源");
            comboDriverCategory.Items.Add("半导体");
            comboDriverCategory.Items.Add("通用/其他");
            comboDriverCategory.SelectedIndex = 0;
            comboDriverCategory.SelectedIndexChanged += (s, e) =>
            {
                if (_suppressCategoryChanged) return;
                PopulateDriversForCategory();
            };
            groupBasic.Controls.Add(comboDriverCategory);
            // NOT added to _dynamicControls — survives ClearDynamicControls
        }

        /// <summary>
        /// 初始化驱动类型下拉框
        /// </summary>
        private void InitCombo()
        {
            comboDriverType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboDriverType.SelectedIndexChanged += (s, e) => OnDriverTypeChanged();
            PopulateDriversForCategory();
        }

        /// <summary>
        /// 获取指定分类下的驱动名列表
        /// </summary>
        private List<string> GetDriverNamesForCategory(int cat)
        {
            List<string> all;
            switch (cat)
            {
                case 0: all = new List<string> { "ModbusTcp","ModbusRtu","SiemensS7","EtherNetIp","OpcUa","OpcUaPubSub","MqttSubscribe","Simulator" }; break;
                case 1: all = new List<string> { "MELSECMc","Fins","HostLink","KeyenceKV","CODESYS","BeckhoffADS","MitsubishiFX","PanasonicMewtocol" }; break;
                case 2: all = new List<string> { "FanucFocas","Heidenhain","MTConnect","HaasCNC","Siemens840D","Mazak" }; break;
                case 3: all = new List<string> { "BACnet","KNX","LonWorks","DALI","MBus" }; break;
                case 4: all = new List<string> { "IEC61850","IEC104","DNP3","DLMS","HARTIP" }; break;
                case 5: all = new List<string> { "PROFIBUS","DeviceNet","CCLink","Profinet" }; break;
                case 6: all = new List<string> { "HttpRest","OPCDA","SparkplugB","SecsGem" }; break;
                default: all = new List<string>(); break;
            }
            // 仅展示生产就绪驱动（规则 50）
            return all.Where(n => DriverManager.IsProductionReady(n)).ToList();
        }

        /// <summary>
        /// 根据选择的分类填充驱动类型列表
        /// </summary>
        private void PopulateDriversForCategory()
        {
            var L = LanguageManager.Instance;
            int cat = comboDriverCategory != null ? comboDriverCategory.SelectedIndex : 0;
            var names = GetDriverNamesForCategory(cat);
            comboDriverType.Items.Clear();
            foreach (var name in names)
            {
                string key = "Driver_" + name;
                comboDriverType.Items.Add(L.GetString(key));
            }
            if (comboDriverType.Items.Count > 0) comboDriverType.SelectedIndex = 0;
        }

        /// <summary>
        /// 获取当前选择的驱动类型名称（字符串）
        /// 用分类索引直接映射，不依赖 i18n 字符串匹配
        /// </summary>
        private string GetSelectedDriverType()
        {
            int selIdx = comboDriverType.SelectedIndex;
            if (selIdx < 0) return "";

            int cat = comboDriverCategory?.SelectedIndex ?? 0;
            var catNames = GetDriverNamesForCategory(cat);
            if (selIdx >= 0 && selIdx < catNames.Count)
            {
                string result = catNames[selIdx];
                Logger.Info($"[GetSelectedDriverType] index={selIdx} -> {result}");
                return result;
            }

            // Fallback: string matching
            string selected = comboDriverType.SelectedItem.ToString();
            var L = LanguageManager.Instance;
            foreach (var key in catNames)
            {
                if (L.GetString("Driver_" + key) == selected) return key;
            }
            return "";
        }

        private void InitGroupCombo()
        {
            // v2.0: 移除 NameEn 行，groupBasic 高度 218→190
            int deltaY = 190 - 85; // 105px
            groupBasic.Height = 190;

            // 下推后续 GroupBox 避免重叠
            groupConnection.Top += deltaY;
            groupPoints.Top += deltaY;
            btnSave.Top += deltaY;
            btnCancel.Top += deltaY;
            this.ClientSize = new Size(this.ClientSize.Width, this.ClientSize.Height + deltaY);

            // Row 1 (Y=18): lblName/txtName | lblCategory/comboDriverCategory | chkEnabled
            comboDriverCategory.Size = new Size(130, 23);
            chkEnabled.Location = new Point(610, 18);
            chkEnabled.Size = new Size(130, 22);

            // Row 1b (Y=49): lblDriverType/comboDriverType — 跟在分类下方，与分类控件对齐
            lblDriverType.Location = new Point(380, 49);
            comboDriverType.Location = new Point(460, 46);
            comboDriverType.Width = 130;

            // Row 2 (Y=52→79): lblPollInterval / txtPollInterval
            lblPollInterval.Location = new Point(10, 82);
            txtPollInterval.Location = new Point(95, 79);
            txtPollInterval.Width = 80;

            // Row 3 (Y=80→107): lblGroup / cmbGroup
            lblGroup = new Label
            {
                Text = LanguageManager.Instance.GetString("DeviceConfig_Group"),
                Location = new Point(10, 110),
                AutoSize = true
            };
            cmbGroup = new ComboBox
            {
                Location = new Point(95, 107),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDown
            };
            RefreshGroupList();

            if (!string.IsNullOrEmpty(_defaultGroup))
                cmbGroup.Text = _defaultGroup;

            groupBasic.Controls.Add(lblGroup);
            groupBasic.Controls.Add(cmbGroup);

            int yTagCn = 137;
            var lblTagPathCn = new Label { Text = LanguageManager.Instance.GetString("TagPathCn") + ":", Location = new Point(10, yTagCn), AutoSize = true };
            _txtTagPathCn = new TextBox { Location = new Point(95, yTagCn - 2), Width = 200, Font = this.Font };
            var btnAutoTagPathCn = new Button { Text = "自 " + LanguageManager.Instance.GetString("TagAutoGenerate"), Location = new Point(305, yTagCn - 2), Size = new Size(90, 28), Font = this.Font, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleLeft };
            btnAutoTagPathCn.FlatAppearance.BorderColor = Color.FromArgb(56, 145, 220);
            btnAutoTagPathCn.FlatAppearance.BorderSize = 1;
            btnAutoTagPathCn.Click += (s, ev) =>
            {
                // 手动重新生成 TagPathCn （group + 设备名）
                string group = cmbGroup?.Text?.Trim() ?? "";
                string name = txtName?.Text?.Trim() ?? "";
                string hierarchyPrefix = GetDataSourceHierarchy(name);
                if (!string.IsNullOrEmpty(hierarchyPrefix))
                    _txtTagPathCn.Text = hierarchyPrefix + "/" + name;
                else if (!string.IsNullOrEmpty(group))
                    _txtTagPathCn.Text = group + "/" + name;
                else
                    _txtTagPathCn.Text = name;
            };
            groupBasic.Controls.Add(lblTagPathCn); groupBasic.Controls.Add(_txtTagPathCn); groupBasic.Controls.Add(btnAutoTagPathCn);

            // 设备名变更时自动更新中文标签路径（1秒防抖）
            txtName.TextChanged += (s, ev) =>
            {
                _pendingTagName = txtName.Text.Trim();
                _tagGenTimer.Stop();
                _tagGenTimer.Start();
            };

            // Group 变更时自动填充中文标签路径（仅在为空时首次填充）
            cmbGroup.TextChanged += (s, ev) =>
            {
                if (!string.IsNullOrEmpty(cmbGroup.Text) && string.IsNullOrEmpty(_txtTagPathCn.Text))
                {
                    _txtTagPathCn.Text = cmbGroup.Text.Trim();
                }
            };

            // 1秒防抖定时器：自动生成中文标签路径
            _tagGenTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _tagGenTimer.Tick += (s, ev) =>
            {
                _tagGenTimer.Stop();
                if (string.IsNullOrEmpty(_pendingTagName)) return;

                string group = cmbGroup?.Text?.Trim() ?? "";
                string hierarchyPrefix = GetDataSourceHierarchy(_pendingTagName);
                string newTagPathCn;
                if (!string.IsNullOrEmpty(hierarchyPrefix))
                    newTagPathCn = hierarchyPrefix + "/" + _pendingTagName;
                else if (!string.IsNullOrEmpty(group))
                    newTagPathCn = group + "/" + _pendingTagName;
                else
                    newTagPathCn = _pendingTagName;

                string oldTagPathCn = _txtTagPathCn?.Text ?? "";
                if (newTagPathCn != oldTagPathCn || string.IsNullOrEmpty(oldTagPathCn))
                {
                    if (_txtTagPathCn != null) _txtTagPathCn.Text = newTagPathCn;

                    // Cascade: update all child variable tags to reflect new device prefix
                    if (_workingPoints != null && !string.IsNullOrEmpty(oldTagPathCn))
                    {
                        foreach (var pt in _workingPoints)
                        {
                            if (!string.IsNullOrEmpty(pt.TagCn) && pt.TagCn.StartsWith(oldTagPathCn + "/"))
                            {
                                pt.TagCn = newTagPathCn + pt.TagCn.Substring(oldTagPathCn.Length);
                            }
                        }
                        RefreshPointGrid();
                    }
                }
            };
        }

        /// <summary>
        /// 从数据源树解析设备层级路径（车间/线体/设备），用于自动生成语义标签前缀
        /// </summary>
        private string GetDataSourceHierarchy(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName)) return null;
            try
            {
                var sources = DataSourceService.Instance.GetAll();
                var match = sources.FirstOrDefault(s =>
                    string.Equals(s.Name?.Trim(), deviceName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match == null || string.IsNullOrEmpty(match.Folder)) return null;

                var segments = match.Folder.Split('/');
                var names = new System.Collections.Generic.List<string>();
                for (int i = 0; i < segments.Length; i++)
                {
                    var folder = DataSourceService.Instance.GetFolders()
                        .FirstOrDefault(f => string.Equals(f.Id, segments[i], StringComparison.OrdinalIgnoreCase));
                    names.Add(folder?.Name ?? segments[i]);
                }
                return string.Join("/", names);
            }
            catch { return null; }
        }

        private string SlugifyDevice(string chinese)
        {
            if (string.IsNullOrEmpty(chinese)) return "";
            var parts = chinese.Split('/');
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(TranslateAndSlug(parts[i]));
            }
            return sb.ToString();
        }

        private string TranslateAndSlug(string segment)
        {
            string translated = Utils.IndustrialVocabulary.TranslateCompound(segment);
            return SlugifyString(translated);
        }

        private string SlugifyString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in input.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == '/' || c == '-') sb.Append(c);
                else sb.Append('_');
            }
            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
        }

        /// <summary>
        /// 初始化网络通道选择面板 (v2.0.0)
        /// </summary>
        private void InitTunnelGroup()
        {
            var L = LanguageManager.Instance;
            int tunnelHeight = 220;

            // 计算隧道面板位置：groupConnection 底部 + 间隔
            int tunnelTop = groupConnection.Location.Y + groupConnection.Height + 10;

            _grpTunnel = new GroupBox
            {
                Text = L.GetString("DataSourceManager_NetworkTunnel") ?? "网络通道",
                Location = new Point(12, tunnelTop),
                Size = new Size(groupConnection.Width, tunnelHeight),
                Font = this.Font
            };

            _tunnelControl = new TunnelSelectionControl
            {
                Dock = DockStyle.Fill,
                Font = this.Font
            };
            _grpTunnel.Controls.Add(_tunnelControl);
            this.Controls.Add(_grpTunnel);

            // 下推下游控件
            int shiftY = tunnelHeight + 10;
            groupPoints.Top += shiftY;
            btnSave.Top += shiftY;
            btnCancel.Top += shiftY;
            this.ClientSize = new Size(this.ClientSize.Width, this.ClientSize.Height + shiftY);
        }

        /// <summary>
        /// v2.0 MQTT 发布模式选择下拉框（Resolved/Original）
        /// </summary>
        private void InitMqttPublishMode()
        {
            var L = LanguageManager.Instance;

            _lblMqttMode = new Label
            {
                Text = L.GetString("MqttPublishMode") ?? "MQTT发布模式:",
                Location = new Point(620, 53),
                Size = new Size(110, 22),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _cmbMqttMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(735, 51),
                Size = new Size(170, 24),
                TabIndex = 7
            };
            _cmbMqttMode.Items.Add(L.GetString("MqttPublishMode_Resolved") ?? "Resolved (规范化 tag_id)");
            _cmbMqttMode.Items.Add(L.GetString("MqttPublishMode_Original") ?? "Original (原始格式)");
            _cmbMqttMode.SelectedIndex = 1;

            groupBasic.Controls.Add(_lblMqttMode);
            groupBasic.Controls.Add(_cmbMqttMode);
        }

        private void RefreshGroupList()
        {
            cmbGroup.Items.Clear();
            cmbGroup.Items.Add("");
            var groups = ConfigService.Instance.LoadDevices()
                .Select(d => d.Group)
                .Where(g => !string.IsNullOrEmpty(g))
                .Distinct()
                .OrderBy(g => g)
                .ToList();
            foreach (var g in groups)
                cmbGroup.Items.Add(g);
        }

        /// <summary>
        /// 根据已有配置加载 UI
        /// </summary>
        private void LoadConfig()
        {
            if (_originalConfig == null) return;

            txtName.Text = _originalConfig.Name;
            chkEnabled.Checked = _originalConfig.Enabled;
            // v2.0: MQTT 发布模式
            if (_cmbMqttMode != null)
                _cmbMqttMode.SelectedIndex = (_originalConfig.MqttPublishMode == "Original") ? 1 : 0;
            if (cmbGroup != null && _originalConfig != null) cmbGroup.Text = _originalConfig.Group ?? "";
            if (_tunnelControl != null && _originalConfig != null)
            {
                _tunnelControl.SetSelectedTunnel(_originalConfig.TunnelId, null);
            }
            if (_txtTagPathCn != null)
            {
                if (!string.IsNullOrEmpty(_originalConfig.TagPathCn))
                    _txtTagPathCn.Text = _originalConfig.TagPathCn;
                else
                {
                    // v2.0: 新建设备时自动生成中文标签路径
                    string hierarchyPrefix = GetDataSourceHierarchy(_originalConfig.Name);
                    if (!string.IsNullOrEmpty(hierarchyPrefix))
                        _txtTagPathCn.Text = hierarchyPrefix + "/" + _originalConfig.Name;
                    else
                        _txtTagPathCn.Text = _originalConfig.Name;
                }
            }

            // 设置驱动类型选择（分类感知，v2.1.0）
            SelectDriverTypeInCategory(_originalConfig.DriverType);

            // 加载通用参数
            txtIP.Text = _originalConfig.GetParam("IP", "127.0.0.1");
            txtPort.Text = _originalConfig.GetParam("Port", "502");
            txtStation.Text = _originalConfig.GetParam("Station", "1");
            txtRack.Text = _originalConfig.GetParam("Rack", "0");
            txtSlot.Text = _originalConfig.GetParam("Slot", "1");
            txtPollInterval.Text = _originalConfig.GetParam("PollInterval", "1000");

             RefreshPointGrid();
        }

        /// <summary>
        /// 从 _originalConfig 恢复动态控件的值（编辑模式）
        /// </summary>
        private void RestoreDynamicValues()
        {
            if (_originalConfig == null) return;

            switch (_originalConfig.DriverType)
            {
                case "ModbusRtu":
                    if (_comboPortName != null)
                        _comboPortName.Text = _originalConfig.GetParam("PortName", "COM1");
                    if (_comboBaudRate != null)
                        _comboBaudRate.Text = _originalConfig.GetParam("BaudRate", "9600");
                    if (_comboParity != null)
                        _comboParity.Text = _originalConfig.GetParam("Parity", "None");
                    if (_comboDataBits != null)
                        _comboDataBits.Text = _originalConfig.GetParam("DataBits", "8");
                    if (_comboStopBits != null)
                        _comboStopBits.Text = _originalConfig.GetParam("StopBits", "1");
                    // Station TextBox
                    foreach (var c in _dynamicControls)
                    {
                        if (c is TextBox tb && (string)tb.Tag == "Station")
                            tb.Text = _originalConfig.GetParam("Station", "1");
                    }
                    break;
                case "MqttSubscribe":
                    if (_txtBrokerHost != null)
                        _txtBrokerHost.Text = _originalConfig.GetParam("BrokerHost", "localhost");
                    if (_txtBrokerPort != null)
                        _txtBrokerPort.Text = _originalConfig.GetParam("BrokerPort", "1883");
                    if (_txtTopicFilter != null)
                        _txtTopicFilter.Text = _originalConfig.GetParam("TopicFilter", "#");
                    if (_comboQos != null)
                        _comboQos.Text = _originalConfig.GetParam("Qos", "1");
                    if (_txtMqttUser != null)
                        _txtMqttUser.Text = _originalConfig.GetParam("Username", "");
                    if (_txtMqttPwd != null)
                        _txtMqttPwd.Text = _originalConfig.GetParam("Password", "");
                    break;
                case "OpcUa":
                    if (_txtServerUrl != null)
                        _txtServerUrl.Text = _originalConfig.GetParam("ServerUrl", "opc.tcp://localhost:4840");
                    if (_txtOpcUaUser != null)
                        _txtOpcUaUser.Text = _originalConfig.GetParam("Username", "");
                    if (_txtOpcUaPwd != null)
                        _txtOpcUaPwd.Text = _originalConfig.GetParam("Password", "");
                    if (_comboOpcUaSecurity != null)
                    {
                        string secMode = _originalConfig.GetParam("SecurityMode", "None");
                        for (int i = 0; i < _comboOpcUaSecurity.Items.Count; i++)
                            if (_comboOpcUaSecurity.Items[i].ToString() == secMode)
                            { _comboOpcUaSecurity.SelectedIndex = i; break; }
                    }
                    if (_comboOpcUaPolicy != null)
                    {
                        string secPolicy = _originalConfig.GetParam("SecurityPolicy", "None");
                        for (int i = 0; i < _comboOpcUaPolicy.Items.Count; i++)
                            if (_comboOpcUaPolicy.Items[i].ToString() == secPolicy)
                            { _comboOpcUaPolicy.SelectedIndex = i; break; }
                    }
                    break;
                case "HttpRest":
                    if (_txtBaseUrl != null)
                        _txtBaseUrl.Text = _originalConfig.GetParam("BaseUrl", "");
                    break;
                case "SiemensS7":
                    txtPort.Text = _originalConfig.GetParam("Port", "102");
                    if (_comboCpuType != null)
                    {
                        string cpuType = _originalConfig.GetParam("CpuType", "S7-1500");
                        for (int i = 0; i < _comboCpuType.Items.Count; i++)
                        {
                            if (_comboCpuType.Items[i].ToString() == cpuType)
                            {
                                _comboCpuType.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                    break;
                case "MELSECMc":
                    txtPort.Text = _originalConfig.GetParam("Port", "5000");
                    if (_comboFrameType != null)
                        _comboFrameType.Text = _originalConfig.GetParam("FrameType", "3E");
                    if (_txtNetworkNo != null)
                        _txtNetworkNo.Text = _originalConfig.GetParam("NetworkNo", "0");
                    if (_txtMcStationNo != null)
                        _txtMcStationNo.Text = _originalConfig.GetParam("StationNo", "0");
                    break;
                case "Fins":
                    txtPort.Text = _originalConfig.GetParam("Port", "9600");
                    if (_comboFinsTransport != null)
                        _comboFinsTransport.Text = _originalConfig.GetParam("Transport", "UDP");
                    if (_txtSourceNode != null)
                        _txtSourceNode.Text = _originalConfig.GetParam("SourceNode", "0");
                    if (_txtDestNode != null)
                        _txtDestNode.Text = _originalConfig.GetParam("DestNode", "1");
                    if (_txtSourceUnit != null)
                        _txtSourceUnit.Text = _originalConfig.GetParam("SourceUnit", "0");
                    if (_txtDestUnit != null)
                        _txtDestUnit.Text = _originalConfig.GetParam("DestUnit", "0");
                    break;
                case "HostLink":
                    txtPort.Text = _originalConfig.GetParam("Port", "9600");
                    if (_comboHostLinkTransport != null)
                        _comboHostLinkTransport.Text = _originalConfig.GetParam("Transport", "TCP");
                    if (_txtHostUnitNo != null)
                        _txtHostUnitNo.Text = _originalConfig.GetParam("UnitNo", "0");
                    break;
                case "KeyenceKV":
                    txtPort.Text = _originalConfig.GetParam("Port", "8501");
                    if (_comboPlcType != null)
                        _comboPlcType.Text = _originalConfig.GetParam("PLCType", "KV-8000");
                    break;
            }

             RefreshPointGrid();
        }

        /// <summary>
        /// 根据驱动类型名称，自动切换到正确的分类并选择驱动
        /// </summary>
        private void SelectDriverTypeInCategory(string driverType)
        {
            if (string.IsNullOrEmpty(driverType) || comboDriverCategory == null) return;

            // Find which category and index this driver belongs to
            int categoryIndex = -1;
            int driverIndex = -1;
            for (int cat = 0; cat < 7; cat++)
            {
                var names = GetDriverNamesForCategory(cat);
                int idx = names.IndexOf(driverType);
                if (idx >= 0)
                {
                    categoryIndex = cat;
                    driverIndex = idx;
                    break;
                }
            }
            if (categoryIndex < 0) return;

            // 用 flag 抑制事件而非 lambda -= 无效操作，防止 handler 泄漏
            _suppressCategoryChanged = true;
            if (comboDriverCategory.SelectedIndex != categoryIndex)
            {
                comboDriverCategory.SelectedIndex = categoryIndex;
                PopulateDriversForCategory();
            }
            _suppressCategoryChanged = false;

            // Set by index — same list as PopulateDriversForCategory, guaranteed consistent
            if (driverIndex >= 0 && driverIndex < comboDriverType.Items.Count)
            {
                comboDriverType.SelectedIndex = driverIndex;
            }
        }

        /// <summary>
        /// OPC DA 参数设置（无 IP/Port，仅 ProgID）
        /// </summary>
        private void SetupOPCDAControls()
        {
            lblIP.Text = "ProgID:";
            lblIP.Visible = true;
            txtIP.Visible = true;
            txtIP.MaxLength = 256;
            if (string.IsNullOrEmpty(txtIP.Text)) txtIP.Text = "OPC.SimaticNET.1";
        }

        /// <summary>
        /// 驱动类型切换时，显示/隐藏对应的参数控件
        /// </summary>
        private void OnDriverTypeChanged()
        {
            string driverType = GetSelectedDriverType();
            var L = LanguageManager.Instance;

            // 隐藏所有静态控件
            lblIP.Visible = false;
            txtIP.Visible = false;
            lblPort.Visible = false;
            txtPort.Visible = false;
            lblStation.Visible = false;
            txtStation.Visible = false;
            lblRack.Visible = false;
            txtRack.Visible = false;
            lblSlot.Visible = false;
            txtSlot.Visible = false;
            btnTestConnect.Visible = false;
            lblSimInfo.Visible = false;
            groupConnection.Size = new Size(1076, 115);
            // groupBasic 增高了 35px + 隧道面板 230px，所有下游控件需同步偏移
            int tunnelShift = _grpTunnel != null ? _grpTunnel.Height + 10 : 0;
            groupPoints.Location = new Point(12, 331 + tunnelShift);
            btnSave.Location = new Point(638, 715 + tunnelShift);
            btnCancel.Location = new Point(733, 715 + tunnelShift);
            this.ClientSize = new Size(1100, 756 + tunnelShift);
            if (_grpTunnel != null)
            {
                _grpTunnel.Location = new Point(12, groupConnection.Location.Y + groupConnection.Height + 10);
            }

            // 清除上次的动态控件
            ClearDynamicControls();

            switch (driverType)
            {
                case "ModbusTcp":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    lblStation.Text = L.GetString("DeviceConfig_Station");
                    lblStation.Visible = true; txtStation.Visible = true;
                    // 恢复按钮位置
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_ModbusTcp_Params");
                    break;

                case "ModbusRtu":
                    groupConnection.Text = L.GetString("Driver_ModbusRtu_Params");
                    SetupModbusRtuControls();
                    btnTestConnect.Location = new Point(646, 90);
                    btnTestConnect.Visible = true;
                    break;

                case "SiemensS7":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;

                    // 端口 — 复用静态控件
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;

                    // 机架号/槽号 移到第二行
                    lblRack.Location = new Point(10, 55);
                    txtRack.Location = new Point(85, 52);
                    lblSlot.Location = new Point(240, 55);
                    txtSlot.Location = new Point(290, 52);
                    lblRack.Visible = true; txtRack.Visible = true;
                    lblSlot.Visible = true; txtSlot.Visible = true;

                    // 测试按钮移到第三行
                    btnTestConnect.Location = new Point(646, 82);
                    btnTestConnect.Visible = true;

                    SetupS7CpuTypeControl();
                    groupConnection.Text = L.GetString("Driver_SiemensS7_Params");
                    break;

                case "MqttSubscribe":
                    groupConnection.Text = L.GetString("Driver_MqttSubscribe_Params");
                    SetupMqttSubscribeControls();
                    groupConnection.Size = new Size(1076, 130);
                    int mqttShift = _grpTunnel != null ? _grpTunnel.Height + 10 : 0;
                    groupPoints.Location = new Point(12, 345 + mqttShift);
                    btnSave.Location = new Point(638, 730 + mqttShift);
                    btnCancel.Location = new Point(733, 730 + mqttShift);
                    this.ClientSize = new Size(1100, 771 + mqttShift);
                    if (_grpTunnel != null)
                    {
                        _grpTunnel.Location = new Point(12, groupConnection.Location.Y + groupConnection.Height + 10);
                    }
                    btnTestConnect.Location = new Point(646, 90);
                    btnTestConnect.Visible = true;
                    break;

                case "OpcUa":
                    groupConnection.Text = L.GetString("Driver_OpcUa_Params");
                    SetupOpcUaControls();
                    groupConnection.Size = new Size(1076, 160);
                    int opcShift = _grpTunnel != null ? _grpTunnel.Height + 10 : 0;
                    groupPoints.Location = new Point(12, 375 + opcShift);
                    btnSave.Location = new Point(638, 760 + opcShift);
                    btnCancel.Location = new Point(733, 760 + opcShift);
                    this.ClientSize = new Size(1100, 801 + opcShift);
                    if (_grpTunnel != null)
                    {
                        _grpTunnel.Location = new Point(12, groupConnection.Location.Y + groupConnection.Height + 10);
                    }
                    btnTestConnect.Location = new Point(646, 115);
                    btnTestConnect.Visible = true;
                    break;

                case "HttpRest":
                    groupConnection.Text = L.GetString("Driver_HttpRest_Params");
                    SetupHttpRestControls();
                    btnTestConnect.Location = new Point(646, 60);
                    btnTestConnect.Visible = true;
                    break;

                case "Simulator":
                    groupConnection.Text = L.GetString("Driver_Simulator_Params");
                    lblSimInfo.Visible = true;
                    break;

                case "EtherNetIp":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    lblSlot.Text = L.GetString("DeviceConfig_Slot");
                    lblSlot.Location = new Point(10, 55);
                    txtSlot.Location = new Point(85, 52);
                    lblSlot.Visible = true; txtSlot.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_EtherNetIp_Params");
                    break;

                case "Profinet":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblSlot.Text = L.GetString("DeviceConfig_Slot");
                    lblSlot.Location = new Point(10, 55);
                    txtSlot.Location = new Point(85, 52);
                    lblSlot.Visible = true; txtSlot.Visible = true;
                    lblStation.Text = L.GetString("DeviceConfig_SubSlot");
                    lblStation.Location = new Point(240, 55);
                    txtStation.Location = new Point(310, 52);
                    lblStation.Visible = true; txtStation.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_Profinet_Params");
                    break;

                case "BACnet":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    lblStation.Text = L.GetString("DeviceConfig_DeviceID");
                    lblStation.Location = new Point(10, 55);
                    txtStation.Location = new Point(85, 52);
                    lblStation.Visible = true; txtStation.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_BACnet_Params");
                    break;

                case "IEC104":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    lblStation.Text = L.GetString("DeviceConfig_CommonAddress");
                    lblStation.Location = new Point(10, 55);
                    txtStation.Location = new Point(85, 52);
                    lblStation.Visible = true; txtStation.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_IEC104_Params");
                    break;

                case "MELSECMc":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    SetupMELSECMcControls();
                    groupConnection.Size = new Size(1076, 125);
                    btnTestConnect.Location = new Point(646, 82);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_MELSECMc_Params");
                    break;

                case "Fins":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    SetupFinsControls();
                    groupConnection.Size = new Size(1076, 155);
                    int finsShift = _grpTunnel != null ? _grpTunnel.Height + 10 : 0;
                    groupPoints.Location = new Point(12, 371 + finsShift);
                    btnSave.Location = new Point(638, 755 + finsShift);
                    btnCancel.Location = new Point(733, 755 + finsShift);
                    this.ClientSize = new Size(1100, 796 + finsShift);
                    if (_grpTunnel != null)
                    {
                        _grpTunnel.Location = new Point(12, groupConnection.Location.Y + groupConnection.Height + 10);
                    }
                    btnTestConnect.Location = new Point(646, 110);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_Fins_Params");
                    break;

                case "HostLink":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    SetupHostLinkControls();
                    groupConnection.Size = new Size(1076, 125);
                    btnTestConnect.Location = new Point(646, 82);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_HostLink_Params");
                    break;

                case "KeyenceKV":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    SetupKeyenceKVControls();
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_KeyenceKV_Params");
                    break;

                case "IEC61850":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    lblStation.Text = L.GetString("DeviceConfig_Domain");
                    lblStation.Location = new Point(10, 55);
                    txtStation.Location = new Point(85, 52);
                    lblStation.Visible = true; txtStation.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_IEC61850_Params");
                    break;

                case "DNP3":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    lblStation.Text = L.GetString("DeviceConfig_SourceAddress");
                    lblStation.Location = new Point(10, 55);
                    txtStation.Location = new Point(85, 52);
                    lblStation.Visible = true; txtStation.Visible = true;
                    lblRack.Text = L.GetString("DeviceConfig_DestAddress");
                    lblRack.Location = new Point(240, 55);
                    txtRack.Location = new Point(310, 52);
                    lblRack.Visible = true; txtRack.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_DNP3_Params");
                    break;

                case "LonWorks":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    lblStation.Text = L.GetString("DeviceConfig_NetworkID");
                    lblStation.Location = new Point(10, 55);
                    txtStation.Location = new Point(85, 52);
                    lblStation.Visible = true; txtStation.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_LonWorks_Params");
                    break;

                case "KNX":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    lblStation.Text = L.GetString("DeviceConfig_IndividualAddress");
                    lblStation.Location = new Point(10, 55);
                    txtStation.Location = new Point(85, 52);
                    lblStation.Visible = true; txtStation.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_KNX_Params");
                    break;

                case "SecsGem":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    lblStation.Text = L.GetString("DeviceConfig_DeviceID");
                    lblStation.Location = new Point(10, 55);
                    txtStation.Location = new Point(85, 52);
                    lblStation.Visible = true; txtStation.Visible = true;
                    lblRack.Text = L.GetString("DeviceConfig_SessionID");
                    lblRack.Location = new Point(240, 55);
                    txtRack.Location = new Point(310, 52);
                    lblRack.Visible = true; txtRack.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_SecsGem_Params");
                    break;

                case "FanucFocas":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    lblStation.Text = L.GetString("DeviceConfig_CNCType");
                    lblStation.Location = new Point(10, 55);
                    txtStation.Location = new Point(85, 52);
                    lblStation.Visible = true; txtStation.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_FanucFocas_Params");
                    break;

                case "MTConnect":
                    groupConnection.Text = L.GetString("Driver_MTConnect_Params");
                    SetupMTConnectControls();
                    btnTestConnect.Location = new Point(646, 60);
                    btnTestConnect.Visible = true;
                    break;

                case "Heidenhain":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    lblStation.Text = L.GetString("DeviceConfig_CNCType");
                    lblStation.Location = new Point(10, 55);
                    txtStation.Location = new Point(85, 52);
                    lblStation.Visible = true; txtStation.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_Heidenhain_Params");
                    break;

                case "OpcUaPubSub":
                    groupConnection.Text = L.GetString("Driver_OpcUaPubSub_Params");
                    SetupOpcUaPubSubControls();
                    groupConnection.Size = new Size(1076, 130);
                    int oupsShift = _grpTunnel != null ? _grpTunnel.Height + 10 : 0;
                    groupPoints.Location = new Point(12, 345 + oupsShift);
                    btnSave.Location = new Point(638, 730 + oupsShift);
                    btnCancel.Location = new Point(733, 730 + oupsShift);
                    this.ClientSize = new Size(1100, 771 + oupsShift);
                    if (_grpTunnel != null)
                    {
                        _grpTunnel.Location = new Point(12, groupConnection.Location.Y + groupConnection.Height + 10);
                    }
                    btnTestConnect.Location = new Point(646, 90);
                    btnTestConnect.Visible = true;
                    break;

                case "SparkplugB":
                    groupConnection.Text = L.GetString("Driver_SparkplugB_Params");
                    SetupSparkplugBControls();
                    groupConnection.Size = new Size(1076, 130);
                    int spbShift = _grpTunnel != null ? _grpTunnel.Height + 10 : 0;
                    groupPoints.Location = new Point(12, 345 + spbShift);
                    btnSave.Location = new Point(638, 730 + spbShift);
                    btnCancel.Location = new Point(733, 730 + spbShift);
                    this.ClientSize = new Size(1100, 771 + spbShift);
                    if (_grpTunnel != null)
                    {
                        _grpTunnel.Location = new Point(12, groupConnection.Location.Y + groupConnection.Height + 10);
                    }
                    btnTestConnect.Location = new Point(646, 90);
                    btnTestConnect.Visible = true;
                    break;

                // ── 15 new drivers (v2.1.0) ──
                case "CODESYS":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_CODESYS_Params");
                    break;

                case "BeckhoffADS":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    lblStation.Text = "AMS Net ID:";
                    lblStation.Location = new Point(10, 55);
                    txtStation.Location = new Point(85, 52);
                    lblStation.Visible = true; txtStation.Visible = true;
                    groupConnection.Size = new Size(1076, 125);
                    btnTestConnect.Location = new Point(646, 82);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_BeckhoffADS_Params");
                    break;

                case "CCLink":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_CCLink_Params");
                    break;

                case "PROFIBUS":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_PROFIBUS_Params");
                    break;

                case "DeviceNet":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_DeviceNet_Params");
                    break;

                case "MitsubishiFX":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_MitsubishiFX_Params");
                    break;

                case "PanasonicMewtocol":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_PanasonicMewtocol_Params");
                    break;

                case "HaasCNC":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_HaasCNC_Params");
                    break;

                case "Siemens840D":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    lblStation.Text = "NCU:";
                    lblStation.Location = new Point(10, 55);
                    txtStation.Location = new Point(85, 52);
                    lblStation.Visible = true; txtStation.Visible = true;
                    groupConnection.Size = new Size(1076, 125);
                    btnTestConnect.Location = new Point(646, 82);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_Siemens840D_Params");
                    break;

                case "Mazak":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_Mazak_Params");
                    break;

                case "DALI":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_DALI_Params");
                    break;

                case "MBus":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_MBus_Params");
                    break;

                case "DLMS":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_DLMS_Params");
                    break;

                case "HARTIP":
                    lblIP.Text = L.GetString("DeviceConfig_IP");
                    lblIP.Visible = true; txtIP.Visible = true;
                    lblPort.Text = L.GetString("DeviceConfig_Port");
                    lblPort.Visible = true; txtPort.Visible = true;
                    btnTestConnect.Location = new Point(646, 72);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_HARTIP_Params");
                    break;

                case "OPCDA":
                    SetupOPCDAControls();
                    btnTestConnect.Location = new Point(646, 60);
                    btnTestConnect.Visible = true;
                    groupConnection.Text = L.GetString("Driver_OPCDA_Params");
                    break;
            }
        }

        /// <summary>
        /// 清除上一次创建的动态控件
        /// </summary>
        private void ClearDynamicControls()
        {
            foreach (var ctrl in _dynamicControls)
            {
                groupConnection.Controls.Remove(ctrl);
                ctrl.Dispose();
            }
            _dynamicControls.Clear();
            _comboPortName = null;
            _comboBaudRate = null;
            _comboParity = null;
            _comboDataBits = null;
            _comboStopBits = null;
            _txtBrokerHost = null;
            _txtBrokerPort = null;
            _txtTopicFilter = null;
            _comboQos = null;
            _txtServerUrl = null;
            _txtOpcUaUser = null;
            _txtOpcUaPwd = null;
            _comboOpcUaSecurity = null;
            _comboOpcUaPolicy = null;
            _txtBaseUrl = null;
            _comboCpuType = null;
            _comboFrameType = null;
            _comboFinsTransport = null;
            _comboHostLinkTransport = null;
            _comboPlcType = null;
            _txtNetworkNo = null;
            _txtMcStationNo = null;
            _txtSourceNode = null;
            _txtDestNode = null;
            _txtSourceUnit = null;
            _txtDestUnit = null;
            _txtHostUnitNo = null;
        }

        private void SetupModbusRtuControls()
        {
            var L = LanguageManager.Instance;
            int y = 25;
            
            AddLabel(L.GetString("DeviceConfig_Rtu_PortName"), 10, y, 70);
            _comboPortName = AddCombo(new[] { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6" }, 85, y, 110);
            _comboPortName.DropDownStyle = ComboBoxStyle.DropDown;

            AddLabel(L.GetString("DeviceConfig_Rtu_BaudRate"), 215, y, 50);
            _comboBaudRate = AddCombo(new[] { "9600", "19200", "38400", "57600", "115200" }, 270, y, 90);
            _comboBaudRate.SelectedIndex = 0;

            AddLabel(L.GetString("DeviceConfig_Rtu_Station"), 380, y, 50);
            var txtRtuStation = AddTextBox("1", 430, y, 70);
            txtRtuStation.Tag = "Station";

            y = 60;
            AddLabel(L.GetString("DeviceConfig_Rtu_Parity"), 10, y, 70);
            _comboParity = AddCombo(new[] { "None", "Odd", "Even", "Mark", "Space" }, 85, y, 100);

            AddLabel(L.GetString("DeviceConfig_Rtu_DataBits"), 215, y, 50);
            _comboDataBits = AddCombo(new[] { "8", "7" }, 270, y, 90);
            _comboDataBits.SelectedIndex = 0;

            AddLabel(L.GetString("DeviceConfig_Rtu_StopBits"), 380, y, 50);
            _comboStopBits = AddCombo(new[] { "1", "2" }, 430, y, 70);
        }

        private void SetupMqttSubscribeControls()
        {
            var L = LanguageManager.Instance;
            int y = 25;
            AddLabel(L.GetString("DeviceConfig_Mqtt_Broker"), 10, y, 70);
            _txtBrokerHost = AddTextBox("localhost", 85, y, 180);

            AddLabel(L.GetString("DeviceConfig_Mqtt_Port"), 280, y, 50);
            _txtBrokerPort = AddTextBox("1883", 330, y, 70);

            y = 60;
            AddLabel(L.GetString("DeviceConfig_Mqtt_Topic"), 10, y, 70);
            _txtTopicFilter = AddTextBox("#", 85, y, 180);

            AddLabel(L.GetString("DeviceConfig_Mqtt_Qos"), 280, y, 50);
            _comboQos = AddCombo(new[] { "0", "1", "2" }, 330, y, 70);
            _comboQos.SelectedIndex = 1;

            y = 95;
            AddLabel(L.GetString("DeviceConfig_Mqtt_Username"), 10, y, 70);
            _txtMqttUser = AddTextBox("", 85, y, 130);

            AddLabel(L.GetString("DeviceConfig_Mqtt_Password"), 285, y, 55);
            _txtMqttPwd = AddTextBox("", 340, y, 130);
            _txtMqttPwd.PasswordChar = '●';
        }

        private void SetupOpcUaControls()
        {
            var L = LanguageManager.Instance;
            // 第一行: 服务器 URL
            AddLabel(L.GetString("DeviceConfig_OpcUa_ServerUrl"), 10, 25, 70);
            _txtServerUrl = AddTextBox("opc.tcp://localhost:4840", 80, 22, 425);

            // 第二行: 用户名 + 密码
            AddLabel(L.GetString("DeviceConfig_OpcUa_Username"), 10, 55, 70);
            _txtOpcUaUser = AddTextBox("", 80, 52, 145);
            AddLabel(L.GetString("DeviceConfig_OpcUa_Password"), 240, 55, 70);
            _txtOpcUaPwd = AddTextBox("", 310, 52, 175);
            _txtOpcUaPwd.PasswordChar = '*';

            // 第三行: 安全模式 + 安全策略 (与第二行对齐)
            AddLabel(L.GetString("DeviceConfig_OpcUa_Security"), 10, 85, 70);
            _comboOpcUaSecurity = AddCombo(
                new[] { "None", "Sign", "SignAndEncrypt" }, 80, 82, 145);
            _comboOpcUaSecurity.SelectedIndex = 0;
            AddLabel(L.GetString("DeviceConfig_OpcUa_Policy"), 240, 85, 70);
            _comboOpcUaPolicy = AddCombo(new[] {
                "None", "Basic256Sha256", "Aes128_Sha256_RsaOaep",
                "Aes256_Sha256_RsaPss", "Basic128Rsa15", "Basic256"
            }, 310, 82, 175);
            _comboOpcUaPolicy.SelectedIndex = 0;
        }

        private void SetupHttpRestControls()
        {
            var L = LanguageManager.Instance;
            AddLabel(L.GetString("DeviceConfig_Http_BaseUrl"), 10, 30, 80);
            _txtBaseUrl = AddTextBox("http://localhost/api/data", 95, 27, 380);
        }

        /// <summary>
        /// Siemens S7 CPU 型号下拉框
        /// </summary>
        private void SetupS7CpuTypeControl()
        {
            var L = LanguageManager.Instance;
            _comboCpuType = AddCombo(new[] { "S7-1200", "S7-1500", "S7-300", "S7-400" }, 85, 82, 150);
            _comboCpuType.SelectedIndex = 1; // 默认 S7-1500
            AddLabel(L.GetString("DeviceConfig_CpuType"), 10, 85, 80);
        }

        private void SetupMELSECMcControls()
        {
            var L = LanguageManager.Instance;
            // Row 2 (below IP/Port): Frame type
            AddLabel("FrameType:", 10, 55, 70);
            _comboFrameType = AddCombo(new[] { "3E", "4E" }, 85, 52, 100);
            _comboFrameType.SelectedIndex = 0;
            // Row 3: Network No + Station No
            AddLabel("NetworkNo:", 10, 85, 70);
            _txtNetworkNo = AddTextBox("0", 85, 82, 70);
            AddLabel("StationNo:", 195, 85, 70);
            _txtMcStationNo = AddTextBox("0", 270, 82, 70);
        }

        private void SetupFinsControls()
        {
            var L = LanguageManager.Instance;
            // Row 2 (below IP/Port): Transport
            AddLabel("Transport:", 10, 55, 70);
            _comboFinsTransport = AddCombo(new[] { "UDP", "TCP" }, 85, 52, 100);
            _comboFinsTransport.SelectedIndex = 0;
            // Row 3: Source Node + Dest Node
            AddLabel("SourceNode:", 10, 85, 80);
            _txtSourceNode = AddTextBox("0", 95, 82, 70);
            AddLabel("DestNode:", 195, 85, 70);
            _txtDestNode = AddTextBox("1", 265, 82, 70);
            // Row 4: Source Unit + Dest Unit
            AddLabel("SourceUnit:", 10, 115, 80);
            _txtSourceUnit = AddTextBox("0", 95, 112, 70);
            AddLabel("DestUnit:", 195, 115, 70);
            _txtDestUnit = AddTextBox("0", 265, 112, 70);
        }

        private void SetupHostLinkControls()
        {
            var L = LanguageManager.Instance;
            // Row 2 (below IP/Port): Transport
            AddLabel("Transport:", 10, 55, 70);
            _comboHostLinkTransport = AddCombo(new[] { "TCP", "Serial" }, 85, 52, 100);
            _comboHostLinkTransport.SelectedIndex = 0;
            // Row 3: Unit No
            AddLabel("UnitNo:", 10, 85, 70);
            _txtHostUnitNo = AddTextBox("0", 85, 82, 70);
        }

        private void SetupKeyenceKVControls()
        {
            var L = LanguageManager.Instance;
            // Row 2 (below IP/Port): PLC Type
            AddLabel("PLC Type:", 10, 55, 70);
            _comboPlcType = AddCombo(new[] { "KV-8000", "KV-7500", "KV-7300", "KV-5000", "KV-3000", "KV-NC32T" }, 85, 52, 140);
            _comboPlcType.SelectedIndex = 0;
        }

        private void SetupMTConnectControls()
        {
            var L = LanguageManager.Instance;
            AddLabel(L.GetString("DeviceConfig_Http_Url"), 10, 30, 80);
            _txtBaseUrl = AddTextBox("http://127.0.0.1:5000", 95, 27, 380);
        }

        private void SetupOpcUaPubSubControls()
        {
            var L = LanguageManager.Instance;
            int y = 25;
            AddLabel(L.GetString("DeviceConfig_Mqtt_Broker"), 10, y, 70);
            _txtBrokerHost = AddTextBox("127.0.0.1", 85, y, 180);

            AddLabel(L.GetString("DeviceConfig_Mqtt_Port"), 280, y, 50);
            _txtBrokerPort = AddTextBox("1883", 330, y, 70);

            y = 60;
            AddLabel(L.GetString("DeviceConfig_Mqtt_Topic"), 10, y, 70);
            _txtTopicFilter = AddTextBox("opcua/pubsub", 85, y, 180);
        }

        private void SetupSparkplugBControls()
        {
            var L = LanguageManager.Instance;
            int y = 25;
            AddLabel(L.GetString("DeviceConfig_Mqtt_Broker"), 10, y, 70);
            _txtBrokerHost = AddTextBox("127.0.0.1", 85, y, 180);

            AddLabel(L.GetString("DeviceConfig_Mqtt_Port"), 280, y, 50);
            _txtBrokerPort = AddTextBox("1883", 330, y, 70);

            y = 60;
            AddLabel(L.GetString("DeviceConfig_GroupID"), 10, y, 70);
            _txtTopicFilter = AddTextBox("SparkplugB", 85, y, 180);

            AddLabel(L.GetString("DeviceConfig_NodeID"), 280, y, 50);
            var txtNodeId = AddTextBox("Node1", 330, y, 70);
            txtNodeId.Tag = "NodeID";
        }

        // ===== 动态控件辅助方法 =====

        private Label AddLabel(string text, int x, int y, int width)
        {
            var lbl = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, 22) };
            groupConnection.Controls.Add(lbl);
            _dynamicControls.Add(lbl);
            return lbl;
        }

        private TextBox AddTextBox(string text, int x, int y, int width)
        {
            var txt = new TextBox { Text = text, Location = new Point(x, y), Size = new Size(width, 23) };
            groupConnection.Controls.Add(txt);
            _dynamicControls.Add(txt);
            return txt;
        }

        private ComboBox AddCombo(string[] items, int x, int y, int width)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(x, y),
                Size = new Size(width, 23)
            };
            combo.Items.AddRange(items);
            combo.SelectedIndex = 0;
            groupConnection.Controls.Add(combo);
            _dynamicControls.Add(combo);
            return combo;
        }

        /// <summary>
        /// 刷新变量点表格
        /// </summary>
        private void RefreshPointGrid()
        {
            dataGridViewPoints.Rows.Clear();
            var points = GetCurrentPoints();
            foreach (var p in points)
            {
                int idx = dataGridViewPoints.Rows.Add(
                    p.Name, p.Address, p.DataType, p.Unit, p.ScaleFactor, p.Offset,
                    p.Length > 0 ? p.Length.ToString() : "-",
                    p.ByteOrder.ToString()
                );
                var dr = dataGridViewPoints.Rows[idx];
                dr.Tag = p;
                // 语义标签列 — 按列名精确写入，不依赖 Cells 序号
                if (dataGridViewPoints.Columns["colTagCn"] != null)
                    dr.Cells["colTagCn"].Value = p.TagCn;
                var edgeSb = new System.Text.StringBuilder(); if (p.FilterEnabled) edgeSb.Append("F"); if (p.RoundingEnabled) edgeSb.Append("R"); if (p.CleanEnabled) edgeSb.Append("C"); if (p.CalculationEnabled) edgeSb.Append("X"); if (p.SquareRootEnabled) edgeSb.Append("S"); if (p.ScriptEnabled) edgeSb.Append("Sc");
                if (dataGridViewPoints.Columns["colPointEdge"] != null)
                    dr.Cells["colPointEdge"].Value = edgeSb.ToString();
                if (dataGridViewPoints.Columns["colPointAlarm"] != null)
                    dr.Cells["colPointAlarm"].Value = p.AlarmEnabled ? "ON" : "-";
            }
        }

        /// <summary>
        /// 获取当前编辑中的变量列表
        /// </summary>
        private List<DataPoint> GetCurrentPoints()
        {
            return _workingPoints;
        }

        /// <summary>
        /// 添加变量点
        /// </summary>
        private void btnAddPoint_Click(object sender, EventArgs e)
        {
            using (var dialog = new PointEditForm_Edge(null, _originalConfig))
            {
                dialog.ShowDialog();
                if (dialog.IsSaved && dialog.DataPoint != null)
                {
                    var points = GetCurrentPoints();
                    if (!points.Contains(dialog.DataPoint))
                        points.Add(dialog.DataPoint);
                    RefreshPointGrid();
                }
            }
        }

        /// <summary>
        /// 编辑变量点
        /// </summary>
        private void btnEditPoint_Click(object sender, EventArgs e)
        {
            if (dataGridViewPoints.SelectedRows.Count == 0) return;
            var point = dataGridViewPoints.SelectedRows[0].Tag as DataPoint;
            if (point == null) return;

            using (var dialog = new PointEditForm_Edge(point, _originalConfig))
            {
                dialog.ShowDialog();
                // _original == point (same _workingPoints object), per-tab SaveToPoint() writes directly to _workingPoints
                RefreshPointGrid();
            }
        }

        /// <summary>
        /// 删除变量点
        /// </summary>
        private void btnDeletePoint_Click(object sender, EventArgs e)
        {
            if (dataGridViewPoints.SelectedRows.Count == 0) return;
            var point = dataGridViewPoints.SelectedRows[0].Tag as DataPoint;
            if (point == null) return;

            var L = LanguageManager.Instance;
            if (MessageBox.Show(
                string.Format(L.GetString("Msg_Confirm_DeletePoint"), point.Name),
                L.GetString("Msg_Confirm"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var points = GetCurrentPoints();
                points.Remove(point);
                 RefreshPointGrid();
            }
        }

        /// <summary>
        /// 批量导入变量点（CSV 格式）
        /// CSV 格式: 变量名,地址,数据类型,单位,倍率,偏移
        /// 首行为表头则自动跳过
        /// </summary>
        private void btnImportPoints_Click(object sender, EventArgs e)
        {
            var L = LanguageManager.Instance;
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "CSV 文件|*.csv|Excel CSV|*.csv|文本文件|*.txt";
                dialog.Title = L.GetString("DeviceConfig_ImportPoints_Title");
                if (dialog.ShowDialog() != DialogResult.OK) return;

                try
                {
                    string[] lines = File.ReadAllLines(dialog.FileName, System.Text.Encoding.GetEncoding("UTF-8"));
                    if (lines.Length == 0)
                    {
                        MessageBox.Show(L.GetString("Msg_ImportPoints_Empty"),
                            L.GetString("Msg_Info"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    int startLine = 0;
                    // 检测首行是否为表头（非空且不含纯数字地址）
                    if (lines[0].Split(',').Length >= 2)
                    {
                        string firstField = lines[0].Split(',')[0].Trim().TrimStart('\uFEFF');
                        // 含中文或非纯数字则视为表头
                        if (!string.IsNullOrEmpty(firstField) && !IsLikelyDataLine(firstField))
                            startLine = 1;
                    }

                    var points = GetCurrentPoints();
                    int imported = 0;
                    int skipped = 0;

                    for (int i = startLine; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (string.IsNullOrEmpty(line)) continue;

                        string[] parts = line.Split(',');
                        if (parts.Length < 2)
                        {
                            skipped++;
                            continue;
                        }

                        var pt = new DataPoint
                        {
                            Name = parts[0].Trim(),
                            Address = parts[1].Trim(),
                            DataType = parts.Length > 2 ? parts[2].Trim() : "int",
                            Unit = parts.Length > 3 ? parts[3].Trim() : "",
                            ScaleFactor = parts.Length > 4 && double.TryParse(parts[4].Trim(), out double scale) ? scale : 1.0,
                            Offset = parts.Length > 5 && double.TryParse(parts[5].Trim(), out double offset) ? offset : 0.0,
                            Length = parts.Length > 6 && int.TryParse(parts[6].Trim(), out int len) ? len : 0,
                            ByteOrder = parts.Length > 7 && int.TryParse(parts[7].Trim(), out int bo) ? (ByteOrder)bo : ByteOrder.ABCD
                        };

                        if (string.IsNullOrEmpty(pt.Name))
                        {
                            skipped++;
                            continue;
                        }

                        // 同名覆盖
                        int existIdx = points.FindIndex(p => p.Name == pt.Name);
                        if (existIdx >= 0)
                            points[existIdx] = pt;
                        else
                            points.Add(pt);

                        imported++;
                    }

                     RefreshPointGrid();

                    string msg = string.Format(L.GetString("Msg_ImportPoints_Result"),
                        imported, skipped);
                    MessageBox.Show(msg, L.GetString("Msg_Info"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        string.Format(L.GetString("Msg_Import_Failed"), ex.Message),
                        L.GetString("Msg_Error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 判断字符串是否像数据行（纯数字地址）
        /// </summary>
        private static bool IsLikelyDataLine(string firstField)
        {
            // 纯数字（如 "40001"）或以 DB 开头 → 数据行
            if (System.Text.RegularExpressions.Regex.IsMatch(firstField, @"^\d+$"))
                return true;
            if (firstField.StartsWith("DB", StringComparison.OrdinalIgnoreCase))
                return true;
            if (firstField.StartsWith("ns=", StringComparison.OrdinalIgnoreCase))
                return true;
            if (firstField.StartsWith("$.", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        /// <summary>
        /// 批量导出变量点为 CSV
        /// </summary>
        private void btnExportPoints_Click(object sender, EventArgs e)
        {
            var L = LanguageManager.Instance;
            var points = GetCurrentPoints();
            if (points.Count == 0)
            {
                MessageBox.Show(L.GetString("Msg_ImportPoints_Empty"),
                    L.GetString("Msg_Info"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "CSV 文件|*.csv";
                dialog.Title = L.GetString("DeviceConfig_ExportPoints_Title");
                dialog.FileName = "变量点表";
                if (dialog.ShowDialog() != DialogResult.OK) return;

                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine(L.GetString("DeviceConfig_ExportPoints_Header"));
                    foreach (var p in points)
                    {
                        sb.AppendLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7}",
                            p.Name, p.Address, p.DataType, p.Unit,
                            p.ScaleFactor, p.Offset,
                            p.Length > 0 ? p.Length.ToString() : "",
                            (int)p.ByteOrder));
                    }
                    File.WriteAllText(dialog.FileName, sb.ToString(), System.Text.Encoding.UTF8);

                    MessageBox.Show(
                        string.Format(L.GetString("Msg_Export_Complete"), dialog.FileName),
                        L.GetString("Msg_Info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        string.Format(L.GetString("Msg_Export_Failed"), ex.Message),
                        L.GetString("Msg_Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 测试连接
        /// </summary>
        private async void btnTestConnect_Click(object sender, EventArgs e)
        {
            if (!ValidateInput()) return;

            btnTestConnect.Enabled = false;
            btnTestConnect.Text = "...";

            try
            {
                var testConfig = BuildConfig();
                var driver = DriverManager.CreateDriver(testConfig);
                bool success = await driver.ConnectAsync(testConfig);

                var L = LanguageManager.Instance;
                if (success)
                {
                    MessageBox.Show(L.GetString("Msg_Success_TestConnect"),
                        L.GetString("DeviceConfig_TestConnection"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await driver.DisconnectAsync();
                }
                else
                {
                    MessageBox.Show(L.GetString("Msg_Fail_TestConnect"),
                        L.GetString("DeviceConfig_TestConnection"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                driver.Dispose();
            }
            catch (Exception ex)
            {
                var L = LanguageManager.Instance;
                MessageBox.Show(string.Format(L.GetString("Msg_Fail_TestConnect"), ex.Message),
                    L.GetString("DeviceConfig_TestConnection"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTestConnect.Enabled = true;
                btnTestConnect.Text = LanguageManager.Instance.GetString("DeviceConfig_TestConnection");
            }
        }

        /// <summary>
        /// 保存
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!ValidateInput()) return;

            DeviceConfig = BuildConfig();
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// 验证输入
        /// </summary>
        private bool ValidateInput()
        {
            var L = LanguageManager.Instance;
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show(L.GetString("Msg_Error_NameEmpty"),
                    L.GetString("Msg_Info"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return false;
            }
            return true;
        }

        /// <summary>
        /// 从 UI 构建设备配置
        /// </summary>
        private DeviceConfig BuildConfig()
        {
            var config = _originalConfig?.Clone() ?? new DeviceConfig();
            config.Name = txtName.Text.Trim();
            // v2.0: NameEn 已废弃，不再赋值
            config.TunnelId = _tunnelControl != null ? (_tunnelControl.SelectedTunnelId ?? "") : "";
            config.Enabled = chkEnabled.Checked;
            config.MqttPublishMode = (_cmbMqttMode != null && _cmbMqttMode.SelectedIndex == 1) ? "Original" : "Resolved";
            config.Group = cmbGroup?.Text?.Trim() ?? "";
            config.TagPathCn = _txtTagPathCn?.Text?.Trim() ?? "";

            config.DriverType = GetSelectedDriverType();
            Logger.Info($"[BuildConfig] DriverType = '{config.DriverType}' (Device='{config.Name}')");

            config.ConnectionParams["PollInterval"] = txtPollInterval.Text;
            config.DataPoints = _workingPoints.Select(p => p.Clone()).ToList();

            switch (config.DriverType)
            {
                case "ModbusTcp":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["Station"] = txtStation.Text;
                    break;
                case "ModbusRtu":
                    config.ConnectionParams["PortName"] = _comboPortName?.Text ?? "COM1";
                    config.ConnectionParams["BaudRate"] = _comboBaudRate?.Text ?? "9600";
                    config.ConnectionParams["Parity"] = _comboParity?.Text ?? "None";
                    config.ConnectionParams["DataBits"] = _comboDataBits?.Text ?? "8";
                    config.ConnectionParams["StopBits"] = _comboStopBits?.Text ?? "1";
                    config.ConnectionParams["Station"] = FindDynamicText("Station") ?? "1";
                    break;
                case "SiemensS7":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["Rack"] = txtRack.Text;
                    config.ConnectionParams["Slot"] = txtSlot.Text;
                    config.ConnectionParams["CpuType"] = _comboCpuType?.Text ?? "S7-1500";
                    break;
                case "MqttSubscribe":
                    config.ConnectionParams["BrokerHost"] = _txtBrokerHost?.Text ?? "localhost";
                    config.ConnectionParams["BrokerPort"] = _txtBrokerPort?.Text ?? "1883";
                    config.ConnectionParams["TopicFilter"] = _txtTopicFilter?.Text ?? "#";
                    config.ConnectionParams["Qos"] = _comboQos?.Text ?? "1";
                    config.ConnectionParams["Username"] = _txtMqttUser?.Text ?? "";
                    config.ConnectionParams["Password"] = _txtMqttPwd?.Text ?? "";
                    break;
                case "OpcUa":
                    config.ConnectionParams["ServerUrl"] = _txtServerUrl?.Text ?? "opc.tcp://localhost:4840";
                    config.ConnectionParams["Username"] = _txtOpcUaUser?.Text ?? "";
                    config.ConnectionParams["Password"] = _txtOpcUaPwd?.Text ?? "";
                    config.ConnectionParams["SecurityMode"] = _comboOpcUaSecurity?.Text ?? "None";
                    config.ConnectionParams["SecurityPolicy"] = _comboOpcUaPolicy?.Text ?? "None";
                    break;
                case "HttpRest":
                    config.ConnectionParams["BaseUrl"] = _txtBaseUrl?.Text ?? "";
                    break;
                case "EtherNetIp":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["Slot"] = txtSlot.Text;
                    break;
                case "Profinet":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Slot"] = txtSlot.Text;
                    config.ConnectionParams["SubSlot"] = txtStation.Text;
                    break;
                case "BACnet":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["DeviceID"] = txtStation.Text;
                    break;
                case "IEC104":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["CommonAddress"] = txtStation.Text;
                    break;
                case "MELSECMc":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["FrameType"] = _comboFrameType?.Text ?? "3E";
                    config.ConnectionParams["NetworkNo"] = _txtNetworkNo?.Text ?? "0";
                    config.ConnectionParams["StationNo"] = _txtMcStationNo?.Text ?? "0";
                    break;
                case "Fins":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["Transport"] = _comboFinsTransport?.Text ?? "UDP";
                    config.ConnectionParams["SourceNode"] = _txtSourceNode?.Text ?? "0";
                    config.ConnectionParams["DestNode"] = _txtDestNode?.Text ?? "1";
                    config.ConnectionParams["SourceUnit"] = _txtSourceUnit?.Text ?? "0";
                    config.ConnectionParams["DestUnit"] = _txtDestUnit?.Text ?? "0";
                    break;
                case "HostLink":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["Transport"] = _comboHostLinkTransport?.Text ?? "TCP";
                    config.ConnectionParams["UnitNo"] = _txtHostUnitNo?.Text ?? "0";
                    break;
                case "KeyenceKV":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["PLCType"] = _comboPlcType?.Text ?? "KV-8000";
                    break;
                case "IEC61850":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["Domain"] = txtStation.Text;
                    break;
                case "DNP3":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["SourceAddress"] = txtStation.Text;
                    config.ConnectionParams["DestAddress"] = txtRack.Text;
                    break;
                case "LonWorks":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["NetworkID"] = txtStation.Text;
                    break;
                case "KNX":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["IndividualAddress"] = txtStation.Text;
                    break;
                case "SecsGem":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["DeviceID"] = txtStation.Text;
                    config.ConnectionParams["SessionID"] = txtRack.Text;
                    break;
                case "FanucFocas":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["CNCType"] = txtStation.Text;
                    break;
                case "MTConnect":
                    config.ConnectionParams["URL"] = _txtBaseUrl?.Text ?? "http://127.0.0.1:5000";
                    config.ConnectionParams["Interval"] = txtPollInterval.Text;
                    break;
                case "Heidenhain":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["CNCType"] = txtStation.Text;
                    break;
                case "OpcUaPubSub":
                    config.ConnectionParams["BrokerHost"] = _txtBrokerHost?.Text ?? "127.0.0.1";
                    config.ConnectionParams["BrokerPort"] = _txtBrokerPort?.Text ?? "1883";
                    config.ConnectionParams["Topic"] = _txtTopicFilter?.Text ?? "opcua/pubsub";
                    break;
                case "SparkplugB":
                    config.ConnectionParams["BrokerHost"] = _txtBrokerHost?.Text ?? "127.0.0.1";
                    config.ConnectionParams["BrokerPort"] = _txtBrokerPort?.Text ?? "1883";
                    config.ConnectionParams["GroupID"] = _txtTopicFilter?.Text ?? "SparkplugB";
                    config.ConnectionParams["NodeID"] = FindDynamicText("NodeID") ?? "Node1";
                    break;
                case "CODESYS":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    break;
                case "BeckhoffADS":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["AmsNetId"] = txtStation.Text;
                    break;
                case "CCLink":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    break;
                case "PROFIBUS":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    break;
                case "DeviceNet":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    break;
                case "MitsubishiFX":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    break;
                case "PanasonicMewtocol":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    break;
                case "HaasCNC":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    break;
                case "Siemens840D":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    config.ConnectionParams["NCU"] = txtStation.Text;
                    break;
                case "Mazak":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    break;
                case "DALI":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    break;
                case "MBus":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    break;
                case "DLMS":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    break;
                case "HARTIP":
                    config.ConnectionParams["IP"] = txtIP.Text;
                    config.ConnectionParams["Port"] = txtPort.Text;
                    break;
                case "OPCDA":
                    config.ConnectionParams["ProgID"] = txtIP.Text;
                    break;
            }

            return config;
        }

        /// <summary>
        /// 从动态控件中按 Tag 查找 TextBox 的值
        /// </summary>
        private string FindDynamicText(string tag)
        {
            foreach (var c in _dynamicControls)
            {
                if (c is TextBox tb && (string)tb.Tag == tag)
                    return tb.Text;
            }
            return null;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
