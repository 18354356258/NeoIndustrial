using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Sockets;
using System.Windows.Forms;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// 通道编辑对话框 — 新建/编辑 VPN 或 NAT 通道
    /// Fully programmatic Form with no Designer.cs dependency.
    /// </summary>
    public class TunnelEditDialog : Form
    {
        #region Controls

        // 通道名称
        private Label _lblName;
        private TextBox _txtName;

        // 通道类型
        private Label _lblType;
        private RadioButton _rdoVpn;
        private RadioButton _rdoNat;

        // VPN 面板
        private Panel _pnlVpn;
        private Label _lblVpnType;
        private ComboBox _cboVpnType;
        private Label _lblVpnTapMode;
        private ComboBox _cboVpnTapMode;
        private Label _lblVpnLocalIp;
        private TextBox _txtVpnLocalIp;
        private Label _lblVpnRemote;
        private TextBox _txtVpnRemote;
        private Label _lblVpnConfig;
        private TextBox _txtVpnConfig;
        private Button _btnBrowseConfig;

        // NAT 面板
        private Panel _pnlNat;
        private Label _lblNatModel;
        private ComboBox _cboNatModel;
        private Label _lblNatIp;
        private TextBox _txtNatIp;
        private Label _lblNatPort;
        private TextBox _txtNatPort;
        private Label _lblNatUser;
        private TextBox _txtNatUser;
        private Label _lblNatPwd;
        private TextBox _txtNatPwd;
        private Button _btnTestConnection;
        private Button _btnAutoDiscover;

        // IP映射表 (通用)
        private Label _lblIpMapping;
        private DataGridView _gridMappings;
        private Button _btnAddMapping;
        private Button _btnDeleteMapping;

        // 按钮
        private Button _btnCancel;
        private Button _btnSave;

        #endregion

        #region State

        private NetworkTunnel _existingTunnel;
        private NetworkTunnel _savedTunnel;
        private bool _isLoading;

        #endregion

        #region Properties

        public NetworkTunnel SavedTunnel
        {
            get { return _savedTunnel; }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// 新建/编辑通道对话框
        /// </summary>
        /// <param name="existingTunnel">null = 新建，非null = 编辑</param>
        public TunnelEditDialog(NetworkTunnel existingTunnel = null)
        {
            _existingTunnel = existingTunnel;

            this.Text = _existingTunnel != null
                ? (LanguageManager.Instance.GetString("Tunnel_Edit") ?? "编辑网络通道")
                : (LanguageManager.Instance.GetString("Tunnel_New") ?? "新建网络通道");

            this.ClientSize = new Size(780, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Icon = Program.AppIcon;
            this.Font = new Font("Microsoft YaHei", 9f);

            this.SuspendLayout();
            BuildUI();
            LoadExistingData();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        #region UI Construction

        private void BuildUI()
        {
            var L = LanguageManager.Instance;
            int y = 12;
            int margin = 12;
            int labelW = 80;
            int inputX = margin + labelW + 5;
            int inputW = 200;
            int fullInputW = this.ClientSize.Width - inputX - margin;

            // ---- 通道名称 ----
            _lblName = new Label
            {
                Text = (L.GetString("Tunnel_Name") ?? "通道名称") + ":",
                Location = new Point(margin, y + 2),
                Size = new Size(labelW, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            this.Controls.Add(_lblName);

            _txtName = new TextBox
            {
                Location = new Point(inputX, y),
                Size = new Size(fullInputW, 22),
                Font = this.Font,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_txtName);
            y += 30;

            // ---- 通道类型 ----
            _lblType = new Label
            {
                Text = (L.GetString("Tunnel_Type") ?? "通道类型") + ":",
                Location = new Point(margin, y + 2),
                Size = new Size(labelW, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            this.Controls.Add(_lblType);

            _rdoVpn = new RadioButton
            {
                Text = "VPN",
                Location = new Point(inputX, y),
                Size = new Size(60, 22),
                Font = this.Font,
                Checked = true
            };
            _rdoVpn.CheckedChanged += RdoType_CheckedChanged;
            this.Controls.Add(_rdoVpn);

            _rdoNat = new RadioButton
            {
                Text = "NAT",
                Location = new Point(inputX + 70, y),
                Size = new Size(60, 22),
                Font = this.Font,
                Checked = false
            };
            _rdoNat.CheckedChanged += RdoType_CheckedChanged;
            this.Controls.Add(_rdoNat);

            y += 34;

            // 分隔线
            var sep1 = new Label
            {
                Location = new Point(margin, y),
                Size = new Size(this.ClientSize.Width - 2 * margin, 1),
                BorderStyle = BorderStyle.Fixed3D,
                Height = 2
            };
            this.Controls.Add(sep1);
            y += 8;

            // ============ VPN 面板 ============
            _pnlVpn = new Panel
            {
                Location = new Point(margin, y),
                Size = new Size(this.ClientSize.Width - 2 * margin, 170),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_pnlVpn);

            int vy = 8;

            // VPN类型
            _lblVpnType = new Label
            {
                Text = (L.GetString("Tunnel_VpnType") ?? "VPN类型") + ":",
                Location = new Point(4, vy + 2),
                Size = new Size(80, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            _pnlVpn.Controls.Add(_lblVpnType);

            _cboVpnType = new ComboBox
            {
                Location = new Point(88, vy),
                Size = new Size(150, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.System,
                Font = this.Font
            };
            _cboVpnType.Items.AddRange(new object[] { "OpenVPN", "L2TP", "IPsec", "WireGuard" });
            _cboVpnType.SelectedIndex = 0;
            _pnlVpn.Controls.Add(_cboVpnType);
            vy += 28;

            // 工作模式
            _lblVpnTapMode = new Label
            {
                Text = (L.GetString("Tunnel_VpnTapMode") ?? "工作模式") + ":",
                Location = new Point(4, vy + 2),
                Size = new Size(80, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            _pnlVpn.Controls.Add(_lblVpnTapMode);

            _cboVpnTapMode = new ComboBox
            {
                Location = new Point(88, vy),
                Size = new Size(150, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.System,
                Font = this.Font
            };
            _cboVpnTapMode.Items.AddRange(new object[] { "TUN(L3)", "TAP(L2)" });
            _cboVpnTapMode.SelectedIndex = 0;
            _pnlVpn.Controls.Add(_cboVpnTapMode);
            vy += 28;

            // 本端虚拟IP
            _lblVpnLocalIp = new Label
            {
                Text = (L.GetString("Tunnel_LocalVirtualIp") ?? "本端虚拟IP") + ":",
                Location = new Point(4, vy + 2),
                Size = new Size(80, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            _pnlVpn.Controls.Add(_lblVpnLocalIp);

            _txtVpnLocalIp = new TextBox
            {
                Location = new Point(88, vy),
                Size = new Size(150, 22),
                Font = this.Font,
                BorderStyle = BorderStyle.FixedSingle
            };
            _pnlVpn.Controls.Add(_txtVpnLocalIp);
            vy += 28;

            // 对端网络
            _lblVpnRemote = new Label
            {
                Text = (L.GetString("Tunnel_RemoteNetwork") ?? "对端网络") + ":",
                Location = new Point(4, vy + 2),
                Size = new Size(80, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            _pnlVpn.Controls.Add(_lblVpnRemote);

            _txtVpnRemote = new TextBox
            {
                Location = new Point(88, vy),
                Size = new Size(150, 22),
                Font = this.Font,
                BorderStyle = BorderStyle.FixedSingle
            };
            _pnlVpn.Controls.Add(_txtVpnRemote);

            var lblRemoteHint = new Label
            {
                Text = "(192.168.1.0/24)",
                Location = new Point(244, vy + 2),
                Size = new Size(120, 20),
                Font = new Font("Microsoft YaHei", 8f),
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _pnlVpn.Controls.Add(lblRemoteHint);
            vy += 28;

            // 配置文件
            _lblVpnConfig = new Label
            {
                Text = (L.GetString("Tunnel_ConfigFile") ?? "配置文件") + ":",
                Location = new Point(4, vy + 2),
                Size = new Size(80, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            _pnlVpn.Controls.Add(_lblVpnConfig);

            _txtVpnConfig = new TextBox
            {
                Location = new Point(88, vy),
                Size = new Size(280, 22),
                Font = this.Font,
                BorderStyle = BorderStyle.FixedSingle
            };
            _pnlVpn.Controls.Add(_txtVpnConfig);

            _btnBrowseConfig = new Button
            {
                Text = L.GetString("Tunnel_Browse") ?? "浏览...",
                Location = new Point(372, vy),
                Size = new Size(65, 24),
                FlatStyle = FlatStyle.Flat,
                Font = this.Font,
                UseVisualStyleBackColor = true
            };
            _btnBrowseConfig.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            _btnBrowseConfig.Click += BtnBrowseConfig_Click;
            _pnlVpn.Controls.Add(_btnBrowseConfig);

            y += 178;

            // ============ NAT 面板 ============
            _pnlNat = new Panel
            {
                Location = new Point(margin, y - 178),
                Size = new Size(this.ClientSize.Width - 2 * margin, 170),
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            this.Controls.Add(_pnlNat);
            _pnlNat.BringToFront();

            int ny = 8;

            // 设备型号
            _lblNatModel = new Label
            {
                Text = (L.GetString("Tunnel_NatDeviceModel") ?? "设备型号") + ":",
                Location = new Point(4, ny + 2),
                Size = new Size(80, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            _pnlNat.Controls.Add(_lblNatModel);

            _cboNatModel = new ComboBox
            {
                Location = new Point(88, ny),
                Size = new Size(150, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.System,
                Font = this.Font
            };
            _cboNatModel.Items.AddRange(new object[] { "华为AR", "Moxa", "蒲公英", "H3C", "通用" });
            _cboNatModel.SelectedIndex = 0;
            _pnlNat.Controls.Add(_cboNatModel);
            ny += 28;

            // 设备IP
            _lblNatIp = new Label
            {
                Text = (L.GetString("Tunnel_NatDeviceIp") ?? "设备IP") + ":",
                Location = new Point(4, ny + 2),
                Size = new Size(80, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            _pnlNat.Controls.Add(_lblNatIp);

            _txtNatIp = new TextBox
            {
                Location = new Point(88, ny),
                Size = new Size(130, 22),
                Font = this.Font,
                BorderStyle = BorderStyle.FixedSingle
            };
            _pnlNat.Controls.Add(_txtNatIp);

            _lblNatPort = new Label
            {
                Text = (L.GetString("Tunnel_NatDevicePort") ?? "端口") + ":",
                Location = new Point(224, ny + 2),
                Size = new Size(40, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            _pnlNat.Controls.Add(_lblNatPort);

            _txtNatPort = new TextBox
            {
                Text = "80",
                Location = new Point(268, ny),
                Size = new Size(50, 22),
                Font = this.Font,
                BorderStyle = BorderStyle.FixedSingle
            };
            _pnlNat.Controls.Add(_txtNatPort);
            ny += 28;

            // 用户名
            _lblNatUser = new Label
            {
                Text = (L.GetString("Tunnel_NatUsername") ?? "用户名") + ":",
                Location = new Point(4, ny + 2),
                Size = new Size(80, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            _pnlNat.Controls.Add(_lblNatUser);

            _txtNatUser = new TextBox
            {
                Location = new Point(88, ny),
                Size = new Size(150, 22),
                Font = this.Font,
                BorderStyle = BorderStyle.FixedSingle
            };
            _pnlNat.Controls.Add(_txtNatUser);
            ny += 28;

            // 密码
            _lblNatPwd = new Label
            {
                Text = (L.GetString("Tunnel_NatPassword") ?? "密码") + ":",
                Location = new Point(4, ny + 2),
                Size = new Size(80, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            _pnlNat.Controls.Add(_lblNatPwd);

            _txtNatPwd = new TextBox
            {
                Location = new Point(88, ny),
                Size = new Size(150, 22),
                Font = this.Font,
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = true
            };
            _pnlNat.Controls.Add(_txtNatPwd);

            // 测试连接 & 自动发现
            ny += 30;
            _btnTestConnection = new Button
            {
                Text = L.GetString("Tunnel_TestConnection") ?? "测试连接",
                Location = new Point(88, ny),
                Size = new Size(90, 26),
                FlatStyle = FlatStyle.Flat,
                Font = this.Font,
                UseVisualStyleBackColor = true
            };
            _btnTestConnection.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            _btnTestConnection.Click += BtnTestConnection_Click;
            _pnlNat.Controls.Add(_btnTestConnection);

            _btnAutoDiscover = new Button
            {
                Text = L.GetString("Tunnel_AutoDiscover") ?? "自动发现映射",
                Location = new Point(186, ny),
                Size = new Size(120, 26),
                FlatStyle = FlatStyle.Flat,
                Font = this.Font,
                UseVisualStyleBackColor = true
            };
            _btnAutoDiscover.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            _btnAutoDiscover.Click += BtnAutoDiscover_Click;
            _pnlNat.Controls.Add(_btnAutoDiscover);

            y += 8;

            // 分隔线2
            var sep2 = new Label
            {
                Location = new Point(margin, y),
                Size = new Size(this.ClientSize.Width - 2 * margin, 1),
                BorderStyle = BorderStyle.Fixed3D,
                Height = 2
            };
            this.Controls.Add(sep2);
            y += 6;

            // ============ IP映射表 (通用) ============
            _lblIpMapping = new Label
            {
                Text = L.GetString("Tunnel_IpMapping") ?? "IP映射表:",
                Location = new Point(margin, y + 2),
                Size = new Size(150, 20),
                Font = this.Font,
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.Controls.Add(_lblIpMapping);
            y += 24;

            int gridW = this.ClientSize.Width - 2 * margin;
            _gridMappings = new DataGridView
            {
                Location = new Point(margin, y),
                Size = new Size(gridW, 170),
                Font = new Font("Microsoft YaHei", 8f),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
            };
            _gridMappings.Columns.Add("col_origIp", L.GetString("Tunnel_OriginalIp") ?? "原始IP");
            _gridMappings.Columns.Add("col_origPort", L.GetString("Tunnel_OriginalPort") ?? "原始端口");
            _gridMappings.Columns.Add("col_mappedIp", L.GetString("Tunnel_MappedIp") ?? "映射IP");
            _gridMappings.Columns.Add("col_mappedPort", L.GetString("Tunnel_MappedPort") ?? "映射端口");
            _gridMappings.Columns.Add("col_desc", L.GetString("Tunnel_Description") ?? "说明");
            _gridMappings.Columns["col_origIp"].FillWeight = 20;
            _gridMappings.Columns["col_origPort"].FillWeight = 12;
            _gridMappings.Columns["col_mappedIp"].FillWeight = 20;
            _gridMappings.Columns["col_mappedPort"].FillWeight = 12;
            _gridMappings.Columns["col_desc"].FillWeight = 36;
            this.Controls.Add(_gridMappings);
            y += 146;

            // 映射表按钮
            _btnAddMapping = new Button
            {
                Text = L.GetString("Tunnel_AddMapping") ?? "添加映射",
                Location = new Point(margin, y),
                Size = new Size(90, 26),
                FlatStyle = FlatStyle.Flat,
                Font = this.Font,
                UseVisualStyleBackColor = true
            };
            _btnAddMapping.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            _btnAddMapping.Click += BtnAddMapping_Click;
            this.Controls.Add(_btnAddMapping);

            _btnDeleteMapping = new Button
            {
                Text = L.GetString("Tunnel_DeleteMapping") ?? "删除选中",
                Location = new Point(margin + 100, y),
                Size = new Size(90, 26),
                FlatStyle = FlatStyle.Flat,
                Font = this.Font,
                UseVisualStyleBackColor = true
            };
            _btnDeleteMapping.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            _btnDeleteMapping.Click += BtnDeleteMapping_Click;
            this.Controls.Add(_btnDeleteMapping);
            y += 34;

            // ---- 底部按钮 ----
            _btnCancel = new Button
            {
                Text = L.GetString("Tunnel_Cancel") ?? "取消",
                Location = new Point(this.ClientSize.Width - 186, y),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                Font = this.Font,
                UseVisualStyleBackColor = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            _btnCancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            _btnCancel.Click += BtnCancel_Click;
            this.Controls.Add(_btnCancel);

            _btnSave = new Button
            {
                Text = L.GetString("Tunnel_Save") ?? "保存",
                Location = new Point(this.ClientSize.Width - 100, y),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                Font = this.Font,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                UseVisualStyleBackColor = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            _btnSave.FlatAppearance.BorderColor = Color.FromArgb(0, 122, 204);
            _btnSave.Click += BtnSave_Click;
            this.Controls.Add(_btnSave);

            y += 40;
            this.ClientSize = new Size(this.ClientSize.Width, y);
        }

        #endregion

        #region Data Loading

        private void LoadExistingData()
        {
            if (_existingTunnel == null) return;
            _isLoading = true;

            _txtName.Text = _existingTunnel.Name ?? "";

            if (_existingTunnel.Type == TunnelType.VPN)
            {
                _rdoVpn.Checked = true;

                if (!string.IsNullOrEmpty(_existingTunnel.VpnType))
                {
                    for (int i = 0; i < _cboVpnType.Items.Count; i++)
                    {
                        if (string.Equals(_cboVpnType.Items[i] as string, _existingTunnel.VpnType, StringComparison.OrdinalIgnoreCase))
                        {
                            _cboVpnType.SelectedIndex = i;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(_existingTunnel.VpnTapMode))
                {
                    for (int i = 0; i < _cboVpnTapMode.Items.Count; i++)
                    {
                        var item = _cboVpnTapMode.Items[i] as string;
                        if (item != null && item.StartsWith(_existingTunnel.VpnTapMode))
                        {
                            _cboVpnTapMode.SelectedIndex = i;
                            break;
                        }
                    }
                }

                _txtVpnLocalIp.Text = _existingTunnel.LocalVirtualIp ?? "";
                _txtVpnRemote.Text = _existingTunnel.RemoteNetwork ?? "";
                _txtVpnConfig.Text = _existingTunnel.VpnConfigFile ?? "";
            }
            else
            {
                _rdoNat.Checked = true;

                if (!string.IsNullOrEmpty(_existingTunnel.NatDeviceModel))
                {
                    for (int i = 0; i < _cboNatModel.Items.Count; i++)
                    {
                        if (string.Equals(_cboNatModel.Items[i] as string, _existingTunnel.NatDeviceModel, StringComparison.OrdinalIgnoreCase))
                        {
                            _cboNatModel.SelectedIndex = i;
                            break;
                        }
                    }
                }

                _txtNatIp.Text = _existingTunnel.NatDeviceIp ?? "";
                _txtNatPort.Text = _existingTunnel.NatDevicePort > 0 ? _existingTunnel.NatDevicePort.ToString() : "80";
                _txtNatUser.Text = _existingTunnel.NatUsername ?? "";
                _txtNatPwd.Text = _existingTunnel.NatPassword ?? "";
            }

            // IP映射表
            if (_existingTunnel.IpMappings != null)
            {
                foreach (var m in _existingTunnel.IpMappings)
                {
                    _gridMappings.Rows.Add(
                        m.OriginalIp ?? "",
                        m.OriginalPort.ToString(),
                        m.MappedIp ?? "",
                        m.MappedPort.ToString(),
                        m.Description ?? "");
                }
            }

            _isLoading = false;
        }

        #endregion

        #region Event Handlers

        private void RdoType_CheckedChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;
            _pnlVpn.Visible = _rdoVpn.Checked;
            _pnlNat.Visible = _rdoNat.Checked;
        }

        private void BtnBrowseConfig_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = LanguageManager.Instance.GetString("Tunnel_ConfigFile") ?? "选择VPN配置文件",
                Filter = "VPN配置文件 (*.ovpn;*.conf)|*.ovpn;*.conf|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _txtVpnConfig.Text = dlg.FileName;
            }
        }

        private void BtnTestConnection_Click(object sender, EventArgs e)
        {
            var host = _txtNatIp.Text.Trim();
            var portStr = _txtNatPort.Text.Trim();
            int port;
            if (!int.TryParse(portStr, out port))
                port = 80;

            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show(this, "请先输入设备IP地址", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnTestConnection.Enabled = false;
            _btnTestConnection.Text = "连接中...";

            try
            {
                using (var tcp = new TcpClient())
                {
                    var ar = tcp.BeginConnect(host, port, null, null);
                    bool success = ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                    if (success)
                    {
                        tcp.EndConnect(ar);
                        MessageBox.Show(this, "TCP连接成功! " + host + ":" + port, "测试连接",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        throw new TimeoutException("连接超时");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "连接失败: " + ex.Message, "测试连接",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _btnTestConnection.Enabled = true;
                _btnTestConnection.Text = LanguageManager.Instance.GetString("Tunnel_TestConnection") ?? "测试连接";
            }
        }

        private void BtnAutoDiscover_Click(object sender, EventArgs e)
        {
            var L = LanguageManager.Instance;
            try
            {
                // Placeholder: 需要设备API适配
                MessageBox.Show(this,
                    L.GetString("Tunnel_AutoDiscoverNotReady") ?? "此功能需要网络可达，将在后续版本实现API适配",
                    L.GetString("Tunnel_AutoDiscover") ?? "自动发现映射",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                MessageBox.Show(this,
                    L.GetString("Tunnel_AutoDiscoverNotReady") ?? "此功能需要网络可达，将在后续版本实现API适配",
                    L.GetString("Tunnel_AutoDiscover") ?? "自动发现映射",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnAddMapping_Click(object sender, EventArgs e)
        {
            _gridMappings.Rows.Add("", "0", "", "0", "");
        }

        private void BtnDeleteMapping_Click(object sender, EventArgs e)
        {
            if (_gridMappings.SelectedRows.Count > 0)
            {
                _gridMappings.Rows.RemoveAt(_gridMappings.SelectedRows[0].Index);
            }
            else if (_gridMappings.SelectedCells.Count > 0)
            {
                _gridMappings.Rows.RemoveAt(_gridMappings.SelectedCells[0].RowIndex);
            }
            else if (_gridMappings.Rows.Count > 0)
            {
                _gridMappings.Rows.RemoveAt(_gridMappings.Rows.Count - 1);
            }
        }

        private List<IpMappingEntry> CollectMappings()
        {
            var list = new List<IpMappingEntry>();
            foreach (DataGridViewRow row in _gridMappings.Rows)
            {
                if (row.IsNewRow) continue;

                string origIp = (row.Cells[0].Value as string ?? "").Trim();
                string mappedIp = (row.Cells[2].Value as string ?? "").Trim();

                if (string.IsNullOrEmpty(origIp) && string.IsNullOrEmpty(mappedIp))
                    continue;

                int origPort;
                int mappedPort;
                int.TryParse(row.Cells[1].Value as string ?? "0", out origPort);
                int.TryParse(row.Cells[3].Value as string ?? "0", out mappedPort);

                list.Add(new IpMappingEntry
                {
                    OriginalIp = origIp,
                    OriginalPort = origPort,
                    MappedIp = mappedIp,
                    MappedPort = mappedPort,
                    Description = (row.Cells[4].Value as string ?? "").Trim()
                });
            }
            return list;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            var L = LanguageManager.Instance;
            string name = _txtName.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(this,
                    L.GetString("Tunnel_RequiredFields") ?? "请填写通道名称和必要参数",
                    L.GetString("Tunnel_Save") ?? "保存",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            // 构建 NetworkTunnel 对象
            var tunnel = _existingTunnel != null ? _existingTunnel.Clone() : new NetworkTunnel();
            tunnel.Name = name;
            tunnel.IpMappings = CollectMappings();

            if (_rdoVpn.Checked)
            {
                tunnel.Type = TunnelType.VPN;
                tunnel.VpnType = _cboVpnType.SelectedItem as string ?? "OpenVPN";

                // 解析TAP模式
                string tapMode = _cboVpnTapMode.SelectedItem as string ?? "TUN(L3)";
                tunnel.VpnTapMode = tapMode.StartsWith("TUN") ? "TUN" : "TAP";

                tunnel.LocalVirtualIp = _txtVpnLocalIp.Text.Trim();
                tunnel.RemoteNetwork = _txtVpnRemote.Text.Trim();
                tunnel.VpnConfigFile = _txtVpnConfig.Text.Trim();

                // 清除 NAT 字段
                tunnel.NatDeviceIp = "";
                tunnel.NatDevicePort = 80;
                tunnel.NatDeviceModel = "";
                tunnel.NatUsername = "";
                tunnel.NatPassword = "";
                tunnel.NatApiKey = "";
            }
            else
            {
                tunnel.Type = TunnelType.NAT;
                tunnel.NatDeviceModel = _cboNatModel.SelectedItem as string ?? "通用";
                tunnel.NatDeviceIp = _txtNatIp.Text.Trim();
                int port;
                if (int.TryParse(_txtNatPort.Text.Trim(), out port) && port > 0)
                    tunnel.NatDevicePort = port;
                else
                    tunnel.NatDevicePort = 80;
                tunnel.NatUsername = _txtNatUser.Text.Trim();
                tunnel.NatPassword = _txtNatPwd.Text;

                // 清除 VPN 字段
                tunnel.VpnType = "";
                tunnel.VpnTapMode = "";
                tunnel.LocalVirtualIp = "";
                tunnel.RemoteNetwork = "";
                tunnel.VpnConfigFile = "";
            }

            // 持久化
            TunnelPoolService.Instance.SaveTunnel(tunnel);
            _savedTunnel = tunnel.Clone();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        #endregion
    }
}
