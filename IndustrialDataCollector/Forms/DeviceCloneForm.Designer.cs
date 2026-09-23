using System.Windows.Forms;

namespace IndustrialDataCollection.Forms
{
    partial class DeviceCloneForm
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
            this.lblSourceDevice = new System.Windows.Forms.Label();
            this.lblNewName = new System.Windows.Forms.Label();
            this.txtNewName = new System.Windows.Forms.TextBox();
            this.lblGroup = new System.Windows.Forms.Label();
            this.cboGroup = new System.Windows.Forms.ComboBox();
            this.gbxCloneContent = new System.Windows.Forms.GroupBox();
            this.chkDriver = new System.Windows.Forms.CheckBox();
            this.chkVars = new System.Windows.Forms.CheckBox();
            this.chkCleaning = new System.Windows.Forms.CheckBox();
            this.chkFabric = new System.Windows.Forms.CheckBox();
            this.chkEvents = new System.Windows.Forms.CheckBox();
            this.chkRelations = new System.Windows.Forms.CheckBox();
            this.gbxPostClone = new System.Windows.Forms.GroupBox();
            this.chkAutoTags = new System.Windows.Forms.CheckBox();
            this.chkAutoOpenConfig = new System.Windows.Forms.CheckBox();
            this.chkAutoCollect = new System.Windows.Forms.CheckBox();
            this.gbxPreview = new System.Windows.Forms.GroupBox();
            this.rtbPreview = new System.Windows.Forms.RichTextBox();
            this.btnClone = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblHint = new System.Windows.Forms.Label();
            this.gbxCloneContent.SuspendLayout();
            this.gbxPostClone.SuspendLayout();
            this.gbxPreview.SuspendLayout();
            this.SuspendLayout();
            // 
            // Form
            // 
            this.ClientSize = new System.Drawing.Size(650, 620);
            this.Text = "克隆设备";
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Name = "DeviceCloneForm";
            // 
            // lblSourceDevice
            // 
            this.lblSourceDevice.AutoSize = true;
            this.lblSourceDevice.Location = new System.Drawing.Point(12, 12);
            this.lblSourceDevice.Size = new System.Drawing.Size(350, 16);
            this.lblSourceDevice.Name = "lblSourceDevice";
            this.lblSourceDevice.TabIndex = 0;
            this.lblSourceDevice.Text = "源设备: --";
            // 
            // lblNewName
            // 
            this.lblNewName.AutoSize = true;
            this.lblNewName.Location = new System.Drawing.Point(12, 40);
            this.lblNewName.Size = new System.Drawing.Size(82, 16);
            this.lblNewName.Name = "lblNewName";
            this.lblNewName.TabIndex = 1;
            this.lblNewName.Text = "新设备名称:";
            // 
            // txtNewName
            // 
            this.txtNewName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.txtNewName.Location = new System.Drawing.Point(100, 37);
            this.txtNewName.Size = new System.Drawing.Size(438, 21);
            this.txtNewName.Name = "txtNewName";
            this.txtNewName.TabIndex = 2;
            // 
            // lblGroup
            // 
            this.lblGroup.AutoSize = true;
            this.lblGroup.Location = new System.Drawing.Point(12, 68);
            this.lblGroup.Size = new System.Drawing.Size(70, 16);
            this.lblGroup.Name = "lblGroup";
            this.lblGroup.TabIndex = 3;
            this.lblGroup.Text = "目标分组:";
            // 
            // cboGroup
            // 
            this.cboGroup.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.cboGroup.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboGroup.Location = new System.Drawing.Point(100, 65);
            this.cboGroup.Size = new System.Drawing.Size(438, 22);
            this.cboGroup.Name = "cboGroup";
            this.cboGroup.TabIndex = 4;
            // 
            // gbxCloneContent
            // 
            this.gbxCloneContent.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.gbxCloneContent.Controls.Add(this.chkDriver);
            this.gbxCloneContent.Controls.Add(this.chkVars);
            this.gbxCloneContent.Controls.Add(this.chkCleaning);
            this.gbxCloneContent.Controls.Add(this.chkFabric);
            this.gbxCloneContent.Controls.Add(this.chkEvents);
            this.gbxCloneContent.Controls.Add(this.chkRelations);
            this.gbxCloneContent.Location = new System.Drawing.Point(12, 96);
            this.gbxCloneContent.Size = new System.Drawing.Size(526, 100);
            this.gbxCloneContent.Name = "gbxCloneContent";
            this.gbxCloneContent.TabIndex = 5;
            this.gbxCloneContent.TabStop = false;
            this.gbxCloneContent.Text = "克隆内容";
            // 
            // chkDriver
            // 
            this.chkDriver.AutoSize = true;
            this.chkDriver.Checked = true;
            this.chkDriver.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkDriver.Location = new System.Drawing.Point(10, 22);
            this.chkDriver.Size = new System.Drawing.Size(110, 20);
            this.chkDriver.Name = "chkDriver";
            this.chkDriver.TabIndex = 0;
            this.chkDriver.Text = "驱动连接参数";
            this.chkDriver.UseVisualStyleBackColor = true;
            // 
            // chkVars
            // 
            this.chkVars.AutoSize = true;
            this.chkVars.Checked = true;
            this.chkVars.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkVars.Location = new System.Drawing.Point(160, 22);
            this.chkVars.Size = new System.Drawing.Size(80, 20);
            this.chkVars.Name = "chkVars";
            this.chkVars.TabIndex = 1;
            this.chkVars.Text = "变量列表";
            this.chkVars.UseVisualStyleBackColor = true;
            // 
            // chkCleaning
            // 
            this.chkCleaning.AutoSize = true;
            this.chkCleaning.Checked = true;
            this.chkCleaning.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCleaning.Location = new System.Drawing.Point(310, 22);
            this.chkCleaning.Size = new System.Drawing.Size(80, 20);
            this.chkCleaning.Name = "chkCleaning";
            this.chkCleaning.TabIndex = 2;
            this.chkCleaning.Text = "清洗策略";
            this.chkCleaning.UseVisualStyleBackColor = true;
            // 
            // chkFabric
            // 
            this.chkFabric.AutoSize = true;
            this.chkFabric.Checked = true;
            this.chkFabric.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkFabric.Location = new System.Drawing.Point(10, 44);
            this.chkFabric.Size = new System.Drawing.Size(90, 20);
            this.chkFabric.Name = "chkFabric";
            this.chkFabric.TabIndex = 3;
            this.chkFabric.Text = "Fabric配置";
            this.chkFabric.UseVisualStyleBackColor = true;
            // 
            // chkEvents
            // 
            this.chkEvents.AutoSize = true;
            this.chkEvents.Checked = true;
            this.chkEvents.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkEvents.Location = new System.Drawing.Point(160, 44);
            this.chkEvents.Size = new System.Drawing.Size(80, 20);
            this.chkEvents.Name = "chkEvents";
            this.chkEvents.TabIndex = 4;
            this.chkEvents.Text = "事件规则";
            this.chkEvents.UseVisualStyleBackColor = true;
            // 
            // chkRelations
            // 
            this.chkRelations.AutoSize = true;
            this.chkRelations.Checked = true;
            this.chkRelations.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkRelations.Location = new System.Drawing.Point(310, 44);
            this.chkRelations.Size = new System.Drawing.Size(80, 20);
            this.chkRelations.Name = "chkRelations";
            this.chkRelations.TabIndex = 5;
            this.chkRelations.Text = "语义关系";
            this.chkRelations.UseVisualStyleBackColor = true;
            // 
            // gbxPostClone
            // 
            this.gbxPostClone.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.gbxPostClone.Controls.Add(this.chkAutoTags);
            this.gbxPostClone.Controls.Add(this.chkAutoOpenConfig);
            this.gbxPostClone.Controls.Add(this.chkAutoCollect);
            this.gbxPostClone.Location = new System.Drawing.Point(12, 202);
            this.gbxPostClone.Size = new System.Drawing.Size(526, 76);
            this.gbxPostClone.Name = "gbxPostClone";
            this.gbxPostClone.TabIndex = 6;
            this.gbxPostClone.TabStop = false;
            this.gbxPostClone.Text = "克隆后操作";
            // 
            // chkAutoTags
            // 
            this.chkAutoTags.AutoSize = true;
            this.chkAutoTags.Checked = true;
            this.chkAutoTags.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkAutoTags.Location = new System.Drawing.Point(10, 22);
            this.chkAutoTags.Size = new System.Drawing.Size(130, 20);
            this.chkAutoTags.Name = "chkAutoTags";
            this.chkAutoTags.TabIndex = 0;
            this.chkAutoTags.Text = "自动生成中文标签";
            this.chkAutoTags.UseVisualStyleBackColor = true;
            // 
            // chkAutoOpenConfig
            // 
            this.chkAutoOpenConfig.AutoSize = true;
            this.chkAutoOpenConfig.Checked = true;
            this.chkAutoOpenConfig.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkAutoOpenConfig.Location = new System.Drawing.Point(160, 22);
            this.chkAutoOpenConfig.Size = new System.Drawing.Size(140, 20);
            this.chkAutoOpenConfig.Name = "chkAutoOpenConfig";
            this.chkAutoOpenConfig.TabIndex = 1;
            this.chkAutoOpenConfig.Text = "自动打开设备配置页面";
            this.chkAutoOpenConfig.UseVisualStyleBackColor = true;
            // 
            // chkAutoCollect
            // 
            this.chkAutoCollect.AutoSize = true;
            this.chkAutoCollect.Location = new System.Drawing.Point(10, 44);
            this.chkAutoCollect.Size = new System.Drawing.Size(200, 20);
            this.chkAutoCollect.Name = "chkAutoCollect";
            this.chkAutoCollect.TabIndex = 2;
            this.chkAutoCollect.Text = "自动启动采集（源设备在采集）";
            this.chkAutoCollect.UseVisualStyleBackColor = true;
            // 
            // gbxPreview
            // 
            this.gbxPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.gbxPreview.Controls.Add(this.rtbPreview);
            this.gbxPreview.Location = new System.Drawing.Point(12, 292);
            this.gbxPreview.Size = new System.Drawing.Size(626, 160);
            this.gbxPreview.Name = "gbxPreview";
            this.gbxPreview.TabIndex = 7;
            this.gbxPreview.TabStop = false;
            this.gbxPreview.Text = "克隆预览";
            // 
            // rtbPreview
            // 
            this.rtbPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.rtbPreview.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rtbPreview.Font = new System.Drawing.Font("Consolas", 10f);
            this.rtbPreview.Location = new System.Drawing.Point(6, 20);
            this.rtbPreview.Size = new System.Drawing.Size(614, 134);
            this.rtbPreview.Name = "rtbPreview";
            this.rtbPreview.ReadOnly = true;
            this.rtbPreview.TabIndex = 0;
            this.rtbPreview.Text = "";
            // 
            // btnClone
            // 
            this.btnClone.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClone.Location = new System.Drawing.Point(482, 554);
            this.btnClone.Size = new System.Drawing.Size(75, 26);
            this.btnClone.Name = "btnClone";
            this.btnClone.TabIndex = 8;
            this.btnClone.Text = "确认克隆";
            this.btnClone.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(563, 554);
            this.btnCancel.Size = new System.Drawing.Size(75, 26);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.TabIndex = 9;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // lblHint
            // 
            this.lblHint.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblHint.AutoSize = true;
            this.lblHint.Font = new System.Drawing.Font("Microsoft YaHei", 8f);
            this.lblHint.ForeColor = System.Drawing.Color.Gray;
            this.lblHint.Location = new System.Drawing.Point(12, 560);
            this.lblHint.Size = new System.Drawing.Size(280, 16);
            this.lblHint.Name = "lblHint";
            this.lblHint.TabIndex = 10;
            this.lblHint.Text = "提示: 克隆后请修改新设备的IP地址和变量寄存器地址";
            // 
            this.Controls.Add(this.lblSourceDevice);
            this.Controls.Add(this.lblNewName);
            this.Controls.Add(this.txtNewName);
            this.Controls.Add(this.lblGroup);
            this.Controls.Add(this.cboGroup);
            this.Controls.Add(this.gbxCloneContent);
            this.Controls.Add(this.gbxPostClone);
            this.Controls.Add(this.gbxPreview);
            this.Controls.Add(this.btnClone);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.lblHint);
            this.gbxCloneContent.ResumeLayout(false);
            this.gbxCloneContent.PerformLayout();
            this.gbxPostClone.ResumeLayout(false);
            this.gbxPostClone.PerformLayout();
            this.gbxPreview.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label lblSourceDevice;
        private System.Windows.Forms.Label lblNewName;
        private System.Windows.Forms.TextBox txtNewName;
        private System.Windows.Forms.Label lblGroup;
        private System.Windows.Forms.ComboBox cboGroup;
        private System.Windows.Forms.GroupBox gbxCloneContent;
        private System.Windows.Forms.CheckBox chkDriver;
        private System.Windows.Forms.CheckBox chkVars;
        private System.Windows.Forms.CheckBox chkCleaning;
        private System.Windows.Forms.CheckBox chkFabric;
        private System.Windows.Forms.CheckBox chkEvents;
        private System.Windows.Forms.CheckBox chkRelations;
        private System.Windows.Forms.GroupBox gbxPostClone;
        private System.Windows.Forms.CheckBox chkAutoTags;
        private System.Windows.Forms.CheckBox chkAutoOpenConfig;
        private System.Windows.Forms.CheckBox chkAutoCollect;
        private System.Windows.Forms.GroupBox gbxPreview;
        private System.Windows.Forms.RichTextBox rtbPreview;
        private System.Windows.Forms.Button btnClone;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblHint;
    }
}
