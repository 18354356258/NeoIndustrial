namespace IndustrialDataCollection.Forms
{
    partial class DeviceConfigForm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DeviceConfigForm));
            this.groupBasic = new System.Windows.Forms.GroupBox();
            this.lblName = new System.Windows.Forms.Label();
            this.txtName = new System.Windows.Forms.TextBox();
            this.lblDriverType = new System.Windows.Forms.Label();
            this.comboDriverType = new System.Windows.Forms.ComboBox();
            this.chkEnabled = new System.Windows.Forms.CheckBox();
            this.lblPollInterval = new System.Windows.Forms.Label();
            this.txtPollInterval = new System.Windows.Forms.TextBox();
            this.groupConnection = new System.Windows.Forms.GroupBox();
            this.lblIP = new System.Windows.Forms.Label();
            this.txtIP = new System.Windows.Forms.TextBox();
            this.lblPort = new System.Windows.Forms.Label();
            this.txtPort = new System.Windows.Forms.TextBox();
            this.lblStation = new System.Windows.Forms.Label();
            this.txtStation = new System.Windows.Forms.TextBox();
            this.lblRack = new System.Windows.Forms.Label();
            this.txtRack = new System.Windows.Forms.TextBox();
            this.lblSlot = new System.Windows.Forms.Label();
            this.txtSlot = new System.Windows.Forms.TextBox();
            this.btnTestConnect = new System.Windows.Forms.Button();
            this.groupPoints = new System.Windows.Forms.GroupBox();
            this.btnAddPoint = new System.Windows.Forms.Button();
            this.btnEditPoint = new System.Windows.Forms.Button();
            this.btnDeletePoint = new System.Windows.Forms.Button();
            this.btnImportPoints = new System.Windows.Forms.Button();
            this.btnExportPoints = new System.Windows.Forms.Button();
            this.dataGridViewPoints = new System.Windows.Forms.DataGridView();
            this.colPointName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPointAddress = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPointDataType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPointUnit = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPointScale = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPointOffset = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPointLength = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPointByteOrder = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPointEdge = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPointAlarm = new System.Windows.Forms.DataGridViewTextBoxColumn();

            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.groupBasic.SuspendLayout();
            this.groupConnection.SuspendLayout();
            this.groupPoints.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewPoints)).BeginInit();
            this.SuspendLayout();
            // 
            // groupBasic
            // 
            this.groupBasic.Controls.Add(this.lblName);
            this.groupBasic.Controls.Add(this.txtName);
            this.groupBasic.Controls.Add(this.lblDriverType);
            this.groupBasic.Controls.Add(this.comboDriverType);
            this.groupBasic.Controls.Add(this.chkEnabled);
            this.groupBasic.Controls.Add(this.lblPollInterval);
            this.groupBasic.Controls.Add(this.txtPollInterval);
            this.groupBasic.Location = new System.Drawing.Point(12, 10);
            this.groupBasic.Name = "groupBasic";
            this.groupBasic.Size = new System.Drawing.Size(816, 85);
            this.groupBasic.TabIndex = 0;
            this.groupBasic.TabStop = false;
            this.groupBasic.Text = "基本配置";
            // 
            // lblName
            // 
            this.lblName.Location = new System.Drawing.Point(10, 25);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(70, 22);
            this.lblName.TabIndex = 0;
            this.lblName.Text = "设备名称:";
            // 
            // txtName
            // 
            this.txtName.Location = new System.Drawing.Point(85, 22);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(180, 21);
            this.txtName.TabIndex = 1;
            // 
            // lblDriverType
            // 
            this.lblDriverType.Location = new System.Drawing.Point(380, 21);
            this.lblDriverType.Name = "lblDriverType";
            this.lblDriverType.Size = new System.Drawing.Size(70, 22);
            this.lblDriverType.TabIndex = 2;
            this.lblDriverType.Text = "驱动类型:";
            // 
            // comboDriverType
            // 
            this.comboDriverType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboDriverType.Location = new System.Drawing.Point(455, 18);
            this.comboDriverType.Name = "comboDriverType";
            this.comboDriverType.Size = new System.Drawing.Size(150, 24);
            this.comboDriverType.TabIndex = 3;
            // 
            // chkEnabled
            // 
            this.chkEnabled.Checked = true;
            this.chkEnabled.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkEnabled.Location = new System.Drawing.Point(176, 56);
            this.chkEnabled.Name = "chkEnabled";
            this.chkEnabled.Size = new System.Drawing.Size(103, 22);
            this.chkEnabled.TabIndex = 4;
            this.chkEnabled.Text = "启用自动采集";
            // 
            // lblPollInterval
            // 
            this.lblPollInterval.Location = new System.Drawing.Point(353, 53);
            this.lblPollInterval.Name = "lblPollInterval";
            this.lblPollInterval.Size = new System.Drawing.Size(84, 22);
            this.lblPollInterval.TabIndex = 5;
            this.lblPollInterval.Text = "轮询间隔 (ms):";
            // 
            // txtPollInterval
            // 
            this.txtPollInterval.Location = new System.Drawing.Point(458, 51);
            this.txtPollInterval.Name = "txtPollInterval";
            this.txtPollInterval.Size = new System.Drawing.Size(80, 21);
            this.txtPollInterval.TabIndex = 6;
            this.txtPollInterval.Text = "1000";
            // 
            // groupConnection
            // 
            this.groupConnection.Controls.Add(this.lblIP);
            this.groupConnection.Controls.Add(this.txtIP);
            this.groupConnection.Controls.Add(this.lblPort);
            this.groupConnection.Controls.Add(this.txtPort);
            this.groupConnection.Controls.Add(this.lblStation);
            this.groupConnection.Controls.Add(this.txtStation);
            this.groupConnection.Controls.Add(this.lblRack);
            this.groupConnection.Controls.Add(this.txtRack);
            this.groupConnection.Controls.Add(this.lblSlot);
            this.groupConnection.Controls.Add(this.txtSlot);
            this.groupConnection.Controls.Add(this.btnTestConnect);
            this.groupConnection.Location = new System.Drawing.Point(12, 103);
            this.groupConnection.Name = "groupConnection";
            this.groupConnection.Size = new System.Drawing.Size(816, 115);
            this.groupConnection.TabIndex = 1;
            this.groupConnection.TabStop = false;
            this.groupConnection.Text = "连接参数";
            // 
            // lblIP
            // 
            this.lblIP.Location = new System.Drawing.Point(10, 25);
            this.lblIP.Name = "lblIP";
            this.lblIP.Size = new System.Drawing.Size(70, 22);
            this.lblIP.TabIndex = 0;
            this.lblIP.Text = "IP 地址:";
            // 
            // txtIP
            // 
            this.txtIP.Location = new System.Drawing.Point(85, 22);
            this.txtIP.Name = "txtIP";
            this.txtIP.Size = new System.Drawing.Size(140, 21);
            this.txtIP.TabIndex = 1;
            this.txtIP.Text = "127.0.0.1";
            // 
            // lblPort
            // 
            this.lblPort.Location = new System.Drawing.Point(240, 25);
            this.lblPort.Name = "lblPort";
            this.lblPort.Size = new System.Drawing.Size(50, 22);
            this.lblPort.TabIndex = 2;
            this.lblPort.Text = "端口:";
            // 
            // txtPort
            // 
            this.txtPort.Location = new System.Drawing.Point(290, 22);
            this.txtPort.Name = "txtPort";
            this.txtPort.Size = new System.Drawing.Size(70, 21);
            this.txtPort.TabIndex = 3;
            this.txtPort.Text = "502";
            // 
            // lblStation
            // 
            this.lblStation.Location = new System.Drawing.Point(380, 25);
            this.lblStation.Name = "lblStation";
            this.lblStation.Size = new System.Drawing.Size(50, 22);
            this.lblStation.TabIndex = 4;
            this.lblStation.Text = "站号:";
            // 
            // txtStation
            // 
            this.txtStation.Location = new System.Drawing.Point(430, 22);
            this.txtStation.Name = "txtStation";
            this.txtStation.Size = new System.Drawing.Size(70, 21);
            this.txtStation.TabIndex = 5;
            this.txtStation.Text = "1";
            // 
            // lblRack
            // 
            this.lblRack.Location = new System.Drawing.Point(240, 25);
            this.lblRack.Name = "lblRack";
            this.lblRack.Size = new System.Drawing.Size(50, 22);
            this.lblRack.TabIndex = 6;
            this.lblRack.Text = "机架号:";
            // 
            // txtRack
            // 
            this.txtRack.Location = new System.Drawing.Point(290, 22);
            this.txtRack.Name = "txtRack";
            this.txtRack.Size = new System.Drawing.Size(70, 21);
            this.txtRack.TabIndex = 7;
            this.txtRack.Text = "0";
            // 
            // lblSlot
            // 
            this.lblSlot.Location = new System.Drawing.Point(380, 25);
            this.lblSlot.Name = "lblSlot";
            this.lblSlot.Size = new System.Drawing.Size(50, 22);
            this.lblSlot.TabIndex = 8;
            this.lblSlot.Text = "槽号:";
            // 
            // txtSlot
            // 
            this.txtSlot.Location = new System.Drawing.Point(430, 22);
            this.txtSlot.Name = "txtSlot";
            this.txtSlot.Size = new System.Drawing.Size(70, 21);
            this.txtSlot.TabIndex = 9;
            this.txtSlot.Text = "1";
            // 
            // btnTestConnect
            // 
            this.btnTestConnect.Location = new System.Drawing.Point(646, 70);
            this.btnTestConnect.Name = "btnTestConnect";
            this.btnTestConnect.Size = new System.Drawing.Size(100, 28);
            this.btnTestConnect.TabIndex = 10;
            this.btnTestConnect.Text = "测试连接";
            this.btnTestConnect.Click += new System.EventHandler(this.btnTestConnect_Click);
            // 
            // groupPoints
            // 
            this.groupPoints.Controls.Add(this.btnAddPoint);
            this.groupPoints.Controls.Add(this.btnEditPoint);
            this.groupPoints.Controls.Add(this.btnDeletePoint);
            this.groupPoints.Controls.Add(this.btnImportPoints);
            this.groupPoints.Controls.Add(this.btnExportPoints);
            this.groupPoints.Controls.Add(this.dataGridViewPoints);
            this.groupPoints.Location = new System.Drawing.Point(12, 226);
            this.groupPoints.Name = "groupPoints";
            this.groupPoints.Size = new System.Drawing.Size(816, 378);
            this.groupPoints.TabIndex = 2;
            this.groupPoints.TabStop = false;
            this.groupPoints.Text = "变量点表";
            // 
            // btnAddPoint
            // 
            this.btnAddPoint.Location = new System.Drawing.Point(10, 20);
            this.btnAddPoint.Name = "btnAddPoint";
            this.btnAddPoint.Size = new System.Drawing.Size(90, 28);
            this.btnAddPoint.TabIndex = 0;
            this.btnAddPoint.Text = "添加变量";
            this.btnAddPoint.Click += new System.EventHandler(this.btnAddPoint_Click);
            // 
            // btnEditPoint
            // 
            this.btnEditPoint.Location = new System.Drawing.Point(106, 20);
            this.btnEditPoint.Name = "btnEditPoint";
            this.btnEditPoint.Size = new System.Drawing.Size(90, 28);
            this.btnEditPoint.TabIndex = 1;
            this.btnEditPoint.Text = "编辑变量";
            this.btnEditPoint.Click += new System.EventHandler(this.btnEditPoint_Click);
            // 
            // btnDeletePoint
            // 
            this.btnDeletePoint.Location = new System.Drawing.Point(202, 20);
            this.btnDeletePoint.Name = "btnDeletePoint";
            this.btnDeletePoint.Size = new System.Drawing.Size(90, 28);
            this.btnDeletePoint.TabIndex = 2;
            this.btnDeletePoint.Text = "删除变量";
            this.btnDeletePoint.Click += new System.EventHandler(this.btnDeletePoint_Click);
            // 
            // btnImportPoints
            // 
            this.btnImportPoints.Location = new System.Drawing.Point(556, 20);
            this.btnImportPoints.Name = "btnImportPoints";
            this.btnImportPoints.Size = new System.Drawing.Size(90, 28);
            this.btnImportPoints.TabIndex = 3;
            this.btnImportPoints.Text = "批量导入";
            this.btnImportPoints.Click += new System.EventHandler(this.btnImportPoints_Click);
            // 
            // btnExportPoints
            // 
            this.btnExportPoints.Location = new System.Drawing.Point(652, 20);
            this.btnExportPoints.Name = "btnExportPoints";
            this.btnExportPoints.Size = new System.Drawing.Size(90, 28);
            this.btnExportPoints.TabIndex = 4;
            this.btnExportPoints.Text = "批量导出";
            this.btnExportPoints.Click += new System.EventHandler(this.btnExportPoints_Click);
            // 
            // dataGridViewPoints
            // 
            this.dataGridViewPoints.AllowUserToAddRows = false;
            this.dataGridViewPoints.AllowUserToDeleteRows = false;
            this.dataGridViewPoints.BackgroundColor = System.Drawing.Color.White;
            this.dataGridViewPoints.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.dataGridViewPoints.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(242)))), ((int)(((byte)(245)))));
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.25F, System.Drawing.FontStyle.Bold);
            dataGridViewCellStyle1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(41)))), ((int)(((byte)(59)))));
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(242)))), ((int)(((byte)(245)))));
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewPoints.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridViewPoints.ColumnHeadersHeight = 28;
            this.dataGridViewPoints.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dataGridViewPoints.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colPointName,
            this.colPointAddress,
            this.colPointDataType,
            this.colPointUnit,
            this.colPointScale,
            this.colPointOffset,
            this.colPointLength,
            this.colPointByteOrder,
            this.colPointEdge,
            this.colPointAlarm});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.25F);
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(219)))), ((int)(((byte)(234)))), ((int)(((byte)(254)))));
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(41)))), ((int)(((byte)(59)))));
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewPoints.DefaultCellStyle = dataGridViewCellStyle2;
            this.dataGridViewPoints.EnableHeadersVisualStyles = false;
            this.dataGridViewPoints.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(232)))), ((int)(((byte)(240)))));
            this.dataGridViewPoints.Location = new System.Drawing.Point(10, 55);
            this.dataGridViewPoints.MultiSelect = false;
            this.dataGridViewPoints.Name = "dataGridViewPoints";
            this.dataGridViewPoints.ReadOnly = true;
            this.dataGridViewPoints.RowHeadersVisible = false;
            this.dataGridViewPoints.RowTemplate.Height = 24;
            this.dataGridViewPoints.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewPoints.Size = new System.Drawing.Size(796, 306);
            this.dataGridViewPoints.TabIndex = 0;
            // 
            // colPointName
            // 
            this.colPointName.HeaderText = "变量名";
            this.colPointName.Name = "colPointName";
            this.colPointName.ReadOnly = true;
            this.colPointName.Width = 100;
            // 
            // colPointAddress
            // 
            this.colPointAddress.HeaderText = "地址";
            this.colPointAddress.Name = "colPointAddress";
            this.colPointAddress.ReadOnly = true;
            this.colPointAddress.Width = 130;
            // 
            // colPointDataType
            // 
            this.colPointDataType.HeaderText = "数据类型";
            this.colPointDataType.Name = "colPointDataType";
            this.colPointDataType.ReadOnly = true;
            this.colPointDataType.Width = 85;
            // 
            // colPointUnit
            // 
            this.colPointUnit.HeaderText = "单位";
            this.colPointUnit.Name = "colPointUnit";
            this.colPointUnit.ReadOnly = true;
            this.colPointUnit.Width = 70;
            // 
            // colPointScale
            // 
            this.colPointScale.HeaderText = "倍率";
            this.colPointScale.Name = "colPointScale";
            this.colPointScale.ReadOnly = true;
            this.colPointScale.Width = 60;
            // 
            // colPointOffset
            // 
            this.colPointOffset.HeaderText = "偏移";
            this.colPointOffset.Name = "colPointOffset";
            this.colPointOffset.ReadOnly = true;
            this.colPointOffset.Width = 60;
            // 
            // colPointLength
            // 
            this.colPointLength.HeaderText = "长度";
            this.colPointLength.Name = "colPointLength";
            this.colPointLength.ReadOnly = true;
            this.colPointLength.Width = 60;
            // 
            // colPointByteOrder
            // 
            this.colPointByteOrder.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colPointByteOrder.HeaderText = "字节序";
            this.colPointByteOrder.Name = "colPointByteOrder";
            this.colPointByteOrder.ReadOnly = true;
            // 
            // colPointEdge
            // 
            this.colPointEdge.HeaderText = "边界计算";
            this.colPointEdge.Name = "colPointEdge";
            this.colPointEdge.ReadOnly = true;
            this.colPointEdge.Width = 72;
            // 
            // colPointAlarm
            // 
            this.colPointAlarm.HeaderText = "报警";
            this.colPointAlarm.Name = "colPointAlarm";
            this.colPointAlarm.ReadOnly = true;
            this.colPointAlarm.Width = 55;

            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(638, 610);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(85, 30);
            this.btnSave.TabIndex = 3;
            this.btnSave.Text = "保存";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(733, 610);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(85, 30);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "取消";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // DeviceConfigForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(880, 651);
            this.Controls.Add(this.groupBasic);
            this.Controls.Add(this.groupConnection);
            this.Controls.Add(this.groupPoints);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnCancel);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.25F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DeviceConfigForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "设备配置";
            this.groupBasic.ResumeLayout(false);
            this.groupBasic.PerformLayout();
            this.groupConnection.ResumeLayout(false);
            this.groupConnection.PerformLayout();
            this.groupPoints.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewPoints)).EndInit();
            this.ResumeLayout(false);

        }

        // ---- 控件声明 ----
        private System.Windows.Forms.GroupBox groupBasic;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.Label lblDriverType;
        private System.Windows.Forms.ComboBox comboDriverType;
        private System.Windows.Forms.CheckBox chkEnabled;
        private System.Windows.Forms.Label lblPollInterval;
        private System.Windows.Forms.TextBox txtPollInterval;

        private System.Windows.Forms.GroupBox groupConnection;
        private System.Windows.Forms.Label lblIP;
        private System.Windows.Forms.TextBox txtIP;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.TextBox txtPort;
        private System.Windows.Forms.Label lblStation;
        private System.Windows.Forms.TextBox txtStation;
        private System.Windows.Forms.Label lblRack;
        private System.Windows.Forms.TextBox txtRack;
        private System.Windows.Forms.Label lblSlot;
        private System.Windows.Forms.TextBox txtSlot;
        private System.Windows.Forms.Button btnTestConnect;

        private System.Windows.Forms.GroupBox groupPoints;
        private System.Windows.Forms.Button btnAddPoint;
        private System.Windows.Forms.Button btnEditPoint;
        private System.Windows.Forms.Button btnDeletePoint;
        private System.Windows.Forms.Button btnImportPoints;
        private System.Windows.Forms.Button btnExportPoints;
        private System.Windows.Forms.DataGridView dataGridViewPoints;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPointName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPointAddress;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPointDataType;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPointUnit;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPointScale;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPointOffset;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPointLength;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPointByteOrder; private System.Windows.Forms.DataGridViewTextBoxColumn colPointEdge; private System.Windows.Forms.DataGridViewTextBoxColumn colPointAlarm;

        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
    }
}
