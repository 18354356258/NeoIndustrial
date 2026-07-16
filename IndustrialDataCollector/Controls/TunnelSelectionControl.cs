using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Forms;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Controls
{
    /// <summary>
    /// 通道选择控件 — 用于在数据采集点编辑界面选择VPN/NAT通道
    /// Fully programmatic UserControl with no Designer.cs dependency.
    /// </summary>
    public class TunnelSelectionControl : UserControl
    {
        #region Controls

        private Label _lblType;
        private ComboBox _cboType;
        private Label _lblTunnel;
        private ComboBox _cboTunnel;
        private Button _btnNew;
        private GroupBox _grpDetail;
        private Label _lblDetailType;
        private Label _lblDetailStatus;
        private Label _lblDetailLocalIp;
        private Label _lblDetailRemote;
        private Label _lblIpMapping;
        private DataGridView _gridMappings;
        private Panel _detailPanel;

        #endregion

        #region State

        private TunnelType? _typeFilter;
        private string _selectedTunnelId;
        private List<NetworkTunnel> _allTunnels;
        private bool _isRefreshing;

        #endregion

        #region Events

        public event EventHandler TunnelChanged;
        public event EventHandler NewTunnelRequested;

        #endregion

        #region Properties

        /// <summary>
        /// 获取当前选中的通道ID，无选择返回 null
        /// </summary>
        public string SelectedTunnelId
        {
            get
            {
                if (_cboTunnel.SelectedItem is NetworkTunnel t)
                    return t.Id;
                return null;
            }
        }

        /// <summary>
        /// 设置/获取通道类型过滤器 (null = 全部, TunnelType.VPN, TunnelType.NAT)
        /// </summary>
        public TunnelType? TunnelTypeFilter
        {
            get { return _typeFilter; }
            set
            {
                _typeFilter = value;
                UpdateTypeCombo();
                RefreshTunnelList();
            }
        }

        #endregion

        #region Constructor

        public TunnelSelectionControl()
        {
            this.SuspendLayout();

            // Design-time properties
            this.BackColor = SystemColors.Control;
            this.Font = new Font("Microsoft YaHei", 9f);
            this.MinimumSize = new Size(300, 200);

            BuildUI();
            this.ResumeLayout(false);
        }

        #endregion

        #region UI Construction

        private void BuildUI()
        {
            var L = LanguageManager.Instance;
            int y = 0;
            int controlWidth = this.Width > 300 ? this.Width : 400;

            // ---- 通道类型选择 ----
            _lblType = new Label
            {
                Text = L.GetString("Tunnel_Type") ?? "通道类型:",
                Location = new Point(0, y + 2),
                Size = new Size(70, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            this.Controls.Add(_lblType);

            _cboType = new ComboBox
            {
                Location = new Point(75, y),
                Size = new Size(130, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.System,
                Font = this.Font
            };
            _cboType.Items.Add(L.GetString("Tunnel_All") ?? "全部");
            _cboType.Items.Add(L.GetString("Tunnel_VPN") ?? "VPN通道");
            _cboType.Items.Add(L.GetString("Tunnel_NAT") ?? "NAT设备");
            _cboType.SelectedIndex = 0;
            _cboType.SelectedIndexChanged += CboType_SelectedIndexChanged;
            this.Controls.Add(_cboType);

            y += 28;

            // ---- 通道选择 + 新建按钮 ----
            _lblTunnel = new Label
            {
                Text = (L.GetString("Tunnel_Select") ?? "通道:") + " ",
                Location = new Point(0, y + 2),
                Size = new Size(70, 22),
                TextAlign = ContentAlignment.MiddleRight,
                Font = this.Font
            };
            this.Controls.Add(_lblTunnel);

            _cboTunnel = new ComboBox
            {
                Location = new Point(75, y),
                Size = new Size(180, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.System,
                Font = this.Font
            };
            _cboTunnel.SelectedIndexChanged += CboTunnel_SelectedIndexChanged;
            this.Controls.Add(_cboTunnel);

            _btnNew = new Button
            {
                Text = "+ " + (L.GetString("Tunnel_New") ?? "新建"),
                Location = new Point(260, y),
                Size = new Size(65, 24),
                FlatStyle = FlatStyle.Flat,
                Font = this.Font,
                UseVisualStyleBackColor = true
            };
            _btnNew.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            _btnNew.Click += BtnNew_Click;
            this.Controls.Add(_btnNew);

            y += 32;

            // ---- 分隔线 ----
            var sep = new Label
            {
                Location = new Point(0, y),
                Size = new Size(controlWidth, 1),
                BorderStyle = BorderStyle.Fixed3D,
                Height = 2
            };
            this.Controls.Add(sep);
            y += 6;

            // ---- 通道详情面板 ----
            _grpDetail = new GroupBox
            {
                Text = L.GetString("Tunnel_Detail") ?? "通道详情",
                Location = new Point(0, y),
                Size = new Size(controlWidth, 110),
                Font = this.Font,
                ForeColor = Color.FromArgb(33, 33, 33)
            };
            this.Controls.Add(_grpDetail);

            int dy = 20;
            _lblDetailType = new Label
            {
                Text = "",
                Location = new Point(10, dy),
                Size = new Size(controlWidth - 20, 18),
                Font = this.Font,
                AutoSize = true
            };
            _grpDetail.Controls.Add(_lblDetailType);
            dy += 20;

            _lblDetailStatus = new Label
            {
                Text = "",
                Location = new Point(10, dy),
                Size = new Size(controlWidth - 20, 18),
                Font = this.Font,
                AutoSize = true
            };
            _grpDetail.Controls.Add(_lblDetailStatus);
            dy += 20;

            _lblDetailLocalIp = new Label
            {
                Text = "",
                Location = new Point(10, dy),
                Size = new Size(controlWidth - 20, 18),
                Font = this.Font,
                AutoSize = true
            };
            _grpDetail.Controls.Add(_lblDetailLocalIp);
            dy += 20;

            _lblDetailRemote = new Label
            {
                Text = "",
                Location = new Point(10, dy),
                Size = new Size(controlWidth - 20, 18),
                Font = this.Font,
                AutoSize = true
            };
            _grpDetail.Controls.Add(_lblDetailRemote);

            y += 116;

            // ---- IP映射表 ----
            _lblIpMapping = new Label
            {
                Text = L.GetString("Tunnel_IpMapping") ?? "IP映射表:",
                Location = new Point(0, y + 2),
                Size = new Size(100, 20),
                Font = this.Font,
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.Controls.Add(_lblIpMapping);
            y += 24;

            _gridMappings = new DataGridView
            {
                Location = new Point(0, y),
                Size = new Size(controlWidth, 120),
                Font = new Font("Microsoft YaHei", 8f),
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
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

            y += 126;

            this.Height = y + 4;

            // Resize handler for dynamic width
            this.Resize += (s, e) =>
            {
                int w = this.Width > 300 ? this.Width : 400;
                if (sep != null) sep.Width = w;
                if (_grpDetail != null) _grpDetail.Width = w;
                if (_gridMappings != null) _gridMappings.Width = w;
            };
        }

        #endregion

        #region Event Handlers

        private void CboType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isRefreshing) return;
            int idx = _cboType.SelectedIndex;
            if (idx == 0)
                _typeFilter = null;
            else if (idx == 1)
                _typeFilter = TunnelType.VPN;
            else if (idx == 2)
                _typeFilter = TunnelType.NAT;

            RefreshTunnelList();
        }

        private void CboTunnel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isRefreshing) return;

            if (_cboTunnel.SelectedItem is NetworkTunnel t)
            {
                _selectedTunnelId = t.Id;
            }
            else
            {
                _selectedTunnelId = null;
            }

            UpdateDetailPanel();
            TunnelChanged?.Invoke(this, EventArgs.Empty);
        }

        private void BtnNew_Click(object sender, EventArgs e)
        {
            var L = LanguageManager.Instance;

            // 确定预设类型
            TunnelType? presetType = _typeFilter;
            if (presetType == null)
            {
                // 如果当前选了全部，新建时让用户选择
            }

            var dlg = new TunnelEditDialog();
            var result = dlg.ShowDialog(this);

            if (result == DialogResult.OK && dlg.SavedTunnel != null)
            {
                RefreshTunnelList();
                SetSelectedTunnel(dlg.SavedTunnel.Id, dlg.SavedTunnel.Name);
                TunnelChanged?.Invoke(this, EventArgs.Empty);
            }

            NewTunnelRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 加载通道列表，按当前类型过滤器筛选
        /// </summary>
        public void RefreshTunnelList()
        {
            _isRefreshing = true;
            var L = LanguageManager.Instance;

            string previousId = SelectedTunnelId;

            _allTunnels = TunnelPoolService.Instance.GetAll();

            _cboTunnel.Items.Clear();
            _cboTunnel.Items.Add("本地直连");

            foreach (var t in _allTunnels)
            {
                if (_typeFilter != null && t.Type != _typeFilter.Value)
                    continue;

                _cboTunnel.Items.Add(t);
            }

            // 尝试恢复之前的选中
            if (!string.IsNullOrEmpty(previousId))
            {
                for (int i = 0; i < _cboTunnel.Items.Count; i++)
                {
                    if (_cboTunnel.Items[i] is NetworkTunnel ti && ti.Id == previousId)
                    {
                        _cboTunnel.SelectedIndex = i;
                        _isRefreshing = false;
                        return;
                    }
                }
            }

            _cboTunnel.SelectedIndex = 0;
            _isRefreshing = false;
            UpdateDetailPanel();
        }

        /// <summary>
        /// 设置当前选中的通道（用于编辑已有采集点时的回填）
        /// </summary>
        public void SetSelectedTunnel(string tunnelId, string tunnelName)
        {
            if (string.IsNullOrEmpty(tunnelId))
            {
                if (_cboTunnel.Items.Count > 0)
                    _cboTunnel.SelectedIndex = 0;
                _selectedTunnelId = null;
                UpdateDetailPanel();
                return;
            }

            // 确保列表已加载
            if (_cboTunnel.Items.Count == 0)
                RefreshTunnelList();

            for (int i = 0; i < _cboTunnel.Items.Count; i++)
            {
                if (_cboTunnel.Items[i] is NetworkTunnel t && t.Id == tunnelId)
                {
                    _cboTunnel.SelectedIndex = i;
                    return;
                }
            }

            // 未在主列表中找到，可能是类型过滤器不匹配，扩展到全部
            TunnelType? savedFilter = _typeFilter;
            _typeFilter = null;
            UpdateTypeCombo();
            RefreshTunnelList();

            for (int i = 0; i < _cboTunnel.Items.Count; i++)
            {
                if (_cboTunnel.Items[i] is NetworkTunnel t && t.Id == tunnelId)
                {
                    _cboTunnel.SelectedIndex = i;
                    return;
                }
            }

            // 恢复过滤器
            _typeFilter = savedFilter;
            UpdateTypeCombo();
            RefreshTunnelList();
            _cboTunnel.SelectedIndex = 0;
            _selectedTunnelId = null;
            UpdateDetailPanel();
        }

        #endregion

        #region Private Helpers

        private void UpdateTypeCombo()
        {
            _isRefreshing = true;
            if (_typeFilter == null)
                _cboType.SelectedIndex = 0;
            else if (_typeFilter == TunnelType.VPN)
                _cboType.SelectedIndex = 1;
            else if (_typeFilter == TunnelType.NAT)
                _cboType.SelectedIndex = 2;
            _isRefreshing = false;
        }

        private void UpdateDetailPanel()
        {
            var L = LanguageManager.Instance;
            var Lm = LanguageManager.Instance;

            if (_cboTunnel.SelectedItem is NetworkTunnel t)
            {
                _selectedTunnelId = t.Id;

                // 类型
                string typeStr = t.Type == TunnelType.VPN
                    ? "VPN" + (string.IsNullOrEmpty(t.VpnType) ? "" : " (" + t.VpnType + ")")
                    : "NAT" + (string.IsNullOrEmpty(t.NatDeviceModel) ? "" : " (" + t.NatDeviceModel + ")");
                _lblDetailType.Text = (L.GetString("Tunnel_Type") ?? "类型") + ": " + typeStr;

                // 状态
                string statusStr = t.IsOnline
                    ? "● " + (L.GetString("Tunnel_Online") ?? "在线")
                    : "○ " + (L.GetString("Tunnel_Offline") ?? "离线");
                _lblDetailStatus.Text = (L.GetString("Tunnel_Status") ?? "状态") + ": " + statusStr;
                _lblDetailStatus.ForeColor = t.IsOnline
                    ? Color.FromArgb(0, 168, 84)
                    : Color.FromArgb(158, 158, 158);

                // VPN专用
                if (t.Type == TunnelType.VPN)
                {
                    _lblDetailLocalIp.Text = (L.GetString("Tunnel_LocalVirtualIp") ?? "本端虚拟IP") + ": " + (t.LocalVirtualIp ?? "-");
                    _lblDetailRemote.Text = (L.GetString("Tunnel_RemoteNetwork") ?? "对端网络") + ": " + (t.RemoteNetwork ?? "-");
                    _lblDetailLocalIp.Visible = true;
                    _lblDetailRemote.Visible = true;
                }
                // NAT专用
                else
                {
                    string natAddr = "";
                    if (!string.IsNullOrEmpty(t.NatDeviceIp))
                    {
                        natAddr = t.NatDeviceIp;
                        if (t.NatDevicePort > 0 && t.NatDevicePort != 80)
                            natAddr += ":" + t.NatDevicePort;
                    }
                    _lblDetailLocalIp.Text = (L.GetString("Tunnel_NatDeviceIp") ?? "设备IP") + ": " + (string.IsNullOrEmpty(natAddr) ? "-" : natAddr);
                    _lblDetailRemote.Text = (L.GetString("Tunnel_NatDeviceModel") ?? "设备型号") + ": " + (t.NatDeviceModel ?? "-");
                    _lblDetailLocalIp.Visible = true;
                    _lblDetailRemote.Visible = true;
                }

                // IP映射表
                PopulateMappingGrid(t.IpMappings);
            }
            else
            {
                _selectedTunnelId = null;
                _lblDetailType.Text = (L.GetString("Tunnel_Type") ?? "类型") + ": -";
                _lblDetailStatus.Text = (L.GetString("Tunnel_Status") ?? "状态") + ": -";
                _lblDetailStatus.ForeColor = Color.FromArgb(33, 33, 33);
                _lblDetailLocalIp.Text = (L.GetString("Tunnel_LocalVirtualIp") ?? "本端虚拟IP") + ": -";
                _lblDetailRemote.Text = (L.GetString("Tunnel_RemoteNetwork") ?? "对端网络") + ": -";
                _lblDetailLocalIp.Visible = true;
                _lblDetailRemote.Visible = true;
                _gridMappings.Rows.Clear();
            }
        }

        private void PopulateMappingGrid(List<IpMappingEntry> mappings)
        {
            _gridMappings.Rows.Clear();
            if (mappings == null || mappings.Count == 0)
                return;

            foreach (var m in mappings)
            {
                _gridMappings.Rows.Add(
                    m.OriginalIp ?? "",
                    m.OriginalPort.ToString(),
                    m.MappedIp ?? "",
                    m.MappedPort.ToString(),
                    m.Description ?? "");
            }
        }

        #endregion
    }
}
