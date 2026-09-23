using System.Windows.Forms;

namespace IndustrialDataCollection.Forms
{
    partial class TemplateManagerForm
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
            this.splMain = new System.Windows.Forms.SplitContainer();
            this.pnlLeft = new System.Windows.Forms.Panel();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.trvTemplates = new System.Windows.Forms.TreeView();
            this.lblStatus = new System.Windows.Forms.Label();
            this.pnlRight = new System.Windows.Forms.Panel();
            this.gbxDetail = new System.Windows.Forms.GroupBox();
            this.lblDetailDesc = new System.Windows.Forms.Label();
            this.lblDetailUpdatedAt = new System.Windows.Forms.Label();
            this.lblDetailCreatedAt = new System.Windows.Forms.Label();
            this.lblDetailSourceDevice = new System.Windows.Forms.Label();
            this.lblDetailVersion = new System.Windows.Forms.Label();
            this.lblDetailCategory = new System.Windows.Forms.Label();
            this.lblDetailName = new System.Windows.Forms.Label();
            this.gbxContents = new System.Windows.Forms.GroupBox();
            this.lblCleanCount = new System.Windows.Forms.Label();
            this.lblEventCount = new System.Windows.Forms.Label();
            this.lblFabricCount = new System.Windows.Forms.Label();
            this.lblRelationCount = new System.Windows.Forms.Label();
            this.lblVarCount = new System.Windows.Forms.Label();
            this.pnlButtons = new System.Windows.Forms.Panel();
            this.btnNew = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnRefresh = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splMain)).BeginInit();
            this.splMain.Panel1.SuspendLayout();
            this.splMain.Panel2.SuspendLayout();
            this.splMain.SuspendLayout();
            this.pnlLeft.SuspendLayout();
            this.pnlRight.SuspendLayout();
            this.gbxDetail.SuspendLayout();
            this.gbxContents.SuspendLayout();
            this.pnlButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // Form
            // 
            this.ClientSize = new System.Drawing.Size(1200, 820);
            this.Text = "配置模板管理器";
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.MinimumSize = new System.Drawing.Size(950, 600);
            this.Name = "TemplateManagerForm";
            // 
            // splMain
            // 
            this.splMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splMain.Location = new System.Drawing.Point(0, 0);
            this.splMain.Size = new System.Drawing.Size(1200, 820);
            this.splMain.SplitterDistance = 300;
            // 
            // splMain.Panel1
            // 
            this.splMain.Panel1.Controls.Add(this.pnlLeft);
            // 
            // splMain.Panel2
            // 
            this.splMain.Panel2.Controls.Add(this.pnlRight);
            // 
            // pnlLeft
            // 
            this.pnlLeft.Controls.Add(this.txtSearch);
            this.pnlLeft.Controls.Add(this.trvTemplates);
            this.pnlLeft.Controls.Add(this.lblStatus);
            this.pnlLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLeft.Padding = new System.Windows.Forms.Padding(6);
            // 
            // txtSearch
            // 
            this.txtSearch.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtSearch.Location = new System.Drawing.Point(6, 6);
            this.txtSearch.Size = new System.Drawing.Size(268, 21);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.TabIndex = 0;
            // 
            // trvTemplates
            // 
            this.trvTemplates.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.trvTemplates.HideSelection = false;
            this.trvTemplates.Location = new System.Drawing.Point(6, 32);
            this.trvTemplates.Size = new System.Drawing.Size(268, 672);
            this.trvTemplates.Name = "trvTemplates";
            this.trvTemplates.TabIndex = 1;
            // 
            // lblStatus
            // 
            this.lblStatus.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lblStatus.ForeColor = System.Drawing.Color.Gray;
            this.lblStatus.Location = new System.Drawing.Point(6, 564);
            this.lblStatus.Size = new System.Drawing.Size(268, 18);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.TabIndex = 2;
            this.lblStatus.Text = "共 0 个模板";
            // 
            // pnlRight
            // 
            this.pnlRight.Controls.Add(this.gbxDetail);
            this.pnlRight.Controls.Add(this.gbxContents);
            this.pnlRight.Controls.Add(this.pnlButtons);
            this.pnlRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlRight.Padding = new System.Windows.Forms.Padding(8);
            // 
            // gbxDetail
            // 
            this.gbxDetail.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gbxDetail.Controls.Add(this.lblDetailDesc);
            this.gbxDetail.Controls.Add(this.lblDetailUpdatedAt);
            this.gbxDetail.Controls.Add(this.lblDetailCreatedAt);
            this.gbxDetail.Controls.Add(this.lblDetailSourceDevice);
            this.gbxDetail.Controls.Add(this.lblDetailVersion);
            this.gbxDetail.Controls.Add(this.lblDetailCategory);
            this.gbxDetail.Controls.Add(this.lblDetailName);
            this.gbxDetail.Location = new System.Drawing.Point(8, 8);
            this.gbxDetail.Size = new System.Drawing.Size(830, 260);
            this.gbxDetail.Name = "gbxDetail";
            this.gbxDetail.TabIndex = 0;
            this.gbxDetail.TabStop = false;
            this.gbxDetail.Text = "模板详情";
            // 
            // lblDetailName
            // 
            this.lblDetailName.AutoSize = true;
            this.lblDetailName.Location = new System.Drawing.Point(12, 24);
            this.lblDetailName.Size = new System.Drawing.Size(200, 16);
            this.lblDetailName.Name = "lblDetailName";
            this.lblDetailName.TabIndex = 0;
            this.lblDetailName.Text = "名称: --";
            // 
            // lblDetailCategory
            // 
            this.lblDetailCategory.AutoSize = true;
            this.lblDetailCategory.Location = new System.Drawing.Point(12, 46);
            this.lblDetailCategory.Size = new System.Drawing.Size(200, 16);
            this.lblDetailCategory.Name = "lblDetailCategory";
            this.lblDetailCategory.TabIndex = 1;
            this.lblDetailCategory.Text = "分类: --";
            // 
            // lblDetailVersion
            // 
            this.lblDetailVersion.AutoSize = true;
            this.lblDetailVersion.Location = new System.Drawing.Point(12, 68);
            this.lblDetailVersion.Size = new System.Drawing.Size(60, 16);
            this.lblDetailVersion.Name = "lblDetailVersion";
            this.lblDetailVersion.TabIndex = 2;
            this.lblDetailVersion.Text = "版本: --";
            // 
            // lblDetailSourceDevice
            // 
            this.lblDetailSourceDevice.AutoSize = true;
            this.lblDetailSourceDevice.Location = new System.Drawing.Point(12, 90);
            this.lblDetailSourceDevice.Size = new System.Drawing.Size(80, 16);
            this.lblDetailSourceDevice.Name = "lblDetailSourceDevice";
            this.lblDetailSourceDevice.TabIndex = 3;
            this.lblDetailSourceDevice.Text = "来源设备: --";
            // 
            // lblDetailCreatedAt
            // 
            this.lblDetailCreatedAt.AutoSize = true;
            this.lblDetailCreatedAt.Location = new System.Drawing.Point(12, 112);
            this.lblDetailCreatedAt.Size = new System.Drawing.Size(80, 16);
            this.lblDetailCreatedAt.Name = "lblDetailCreatedAt";
            this.lblDetailCreatedAt.TabIndex = 4;
            this.lblDetailCreatedAt.Text = "创建时间: --";
            // 
            // lblDetailUpdatedAt
            // 
            this.lblDetailUpdatedAt.AutoSize = true;
            this.lblDetailUpdatedAt.Location = new System.Drawing.Point(12, 134);
            this.lblDetailUpdatedAt.Size = new System.Drawing.Size(80, 16);
            this.lblDetailUpdatedAt.Name = "lblDetailUpdatedAt";
            this.lblDetailUpdatedAt.TabIndex = 5;
            this.lblDetailUpdatedAt.Text = "更新时间: --";
            // 
            // lblDetailDesc
            // 
            this.lblDetailDesc.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblDetailDesc.Location = new System.Drawing.Point(12, 158);
            this.lblDetailDesc.Size = new System.Drawing.Size(806, 60);
            this.lblDetailDesc.Name = "lblDetailDesc";
            this.lblDetailDesc.TabIndex = 6;
            this.lblDetailDesc.Text = "描述: --";
            // 
            // gbxContents
            // 
            this.gbxContents.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gbxContents.Controls.Add(this.lblCleanCount);
            this.gbxContents.Controls.Add(this.lblEventCount);
            this.gbxContents.Controls.Add(this.lblFabricCount);
            this.gbxContents.Controls.Add(this.lblRelationCount);
            this.gbxContents.Controls.Add(this.lblVarCount);
            this.gbxContents.Location = new System.Drawing.Point(8, 276);
            this.gbxContents.Size = new System.Drawing.Size(830, 140);
            this.gbxContents.Name = "gbxContents";
            this.gbxContents.TabIndex = 1;
            this.gbxContents.TabStop = false;
            this.gbxContents.Text = "包含内容";
            // 
            // lblVarCount
            // 
            this.lblVarCount.AutoSize = true;
            this.lblVarCount.Location = new System.Drawing.Point(12, 24);
            this.lblVarCount.Size = new System.Drawing.Size(160, 16);
            this.lblVarCount.Name = "lblVarCount";
            this.lblVarCount.TabIndex = 0;
            this.lblVarCount.Text = "• 0 个变量映射规则";
            // 
            // lblRelationCount
            // 
            this.lblRelationCount.AutoSize = true;
            this.lblRelationCount.Location = new System.Drawing.Point(12, 44);
            this.lblRelationCount.Size = new System.Drawing.Size(120, 16);
            this.lblRelationCount.Name = "lblRelationCount";
            this.lblRelationCount.TabIndex = 1;
            this.lblRelationCount.Text = "• 0 条语义关系";
            // 
            // lblFabricCount
            // 
            this.lblFabricCount.AutoSize = true;
            this.lblFabricCount.Location = new System.Drawing.Point(12, 64);
            this.lblFabricCount.Size = new System.Drawing.Size(140, 16);
            this.lblFabricCount.Name = "lblFabricCount";
            this.lblFabricCount.TabIndex = 2;
            this.lblFabricCount.Text = "• 0 个Fabric算子配置";
            // 
            // lblEventCount
            // 
            this.lblEventCount.AutoSize = true;
            this.lblEventCount.Location = new System.Drawing.Point(12, 84);
            this.lblEventCount.Size = new System.Drawing.Size(120, 16);
            this.lblEventCount.Name = "lblEventCount";
            this.lblEventCount.TabIndex = 3;
            this.lblEventCount.Text = "• 0 条事件规则";
            // 
            // lblCleanCount
            // 
            this.lblCleanCount.AutoSize = true;
            this.lblCleanCount.Location = new System.Drawing.Point(320, 24);
            this.lblCleanCount.Size = new System.Drawing.Size(120, 16);
            this.lblCleanCount.Name = "lblCleanCount";
            this.lblCleanCount.TabIndex = 4;
            this.lblCleanCount.Text = "• 0 条清洗策略";
            // 
            // pnlButtons
            // 
            this.pnlButtons.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlButtons.Controls.Add(this.btnNew);
            this.pnlButtons.Controls.Add(this.btnDelete);
            this.pnlButtons.Controls.Add(this.btnRefresh);
            this.pnlButtons.Location = new System.Drawing.Point(500, 556);
            this.pnlButtons.Size = new System.Drawing.Size(260, 30);
            this.pnlButtons.Name = "pnlButtons";
            this.pnlButtons.TabIndex = 2;
            // 
            // btnNew
            // 
            this.btnNew.Location = new System.Drawing.Point(16, 2);
            this.btnNew.Size = new System.Drawing.Size(75, 26);
            this.btnNew.Name = "btnNew";
            this.btnNew.TabIndex = 0;
            this.btnNew.Text = "新建模板";
            this.btnNew.UseVisualStyleBackColor = true;
            // 
            // btnDelete
            // 
            this.btnDelete.Location = new System.Drawing.Point(98, 2);
            this.btnDelete.Size = new System.Drawing.Size(75, 26);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.TabIndex = 1;
            this.btnDelete.Text = "删除模板";
            this.btnDelete.UseVisualStyleBackColor = true;
            // 
            // btnRefresh
            // 
            this.btnRefresh.Location = new System.Drawing.Point(180, 2);
            this.btnRefresh.Size = new System.Drawing.Size(75, 26);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.TabIndex = 2;
            this.btnRefresh.Text = "刷新";
            this.btnRefresh.UseVisualStyleBackColor = true;
            // 
            this.Controls.Add(this.splMain);
            this.splMain.Panel1.ResumeLayout(false);
            this.splMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splMain)).EndInit();
            this.splMain.ResumeLayout(false);
            this.pnlLeft.ResumeLayout(false);
            this.pnlLeft.PerformLayout();
            this.pnlRight.ResumeLayout(false);
            this.gbxDetail.ResumeLayout(false);
            this.gbxDetail.PerformLayout();
            this.gbxContents.ResumeLayout(false);
            this.gbxContents.PerformLayout();
            this.pnlButtons.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.SplitContainer splMain;
        private System.Windows.Forms.Panel pnlLeft;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.TreeView trvTemplates;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Panel pnlRight;
        private System.Windows.Forms.GroupBox gbxDetail;
        private System.Windows.Forms.Label lblDetailName;
        private System.Windows.Forms.Label lblDetailCategory;
        private System.Windows.Forms.Label lblDetailVersion;
        private System.Windows.Forms.Label lblDetailSourceDevice;
        private System.Windows.Forms.Label lblDetailCreatedAt;
        private System.Windows.Forms.Label lblDetailUpdatedAt;
        private System.Windows.Forms.Label lblDetailDesc;
        private System.Windows.Forms.GroupBox gbxContents;
        private System.Windows.Forms.Label lblVarCount;
        private System.Windows.Forms.Label lblRelationCount;
        private System.Windows.Forms.Label lblFabricCount;
        private System.Windows.Forms.Label lblEventCount;
        private System.Windows.Forms.Label lblCleanCount;
        private System.Windows.Forms.Panel pnlButtons;
        private System.Windows.Forms.Button btnNew;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnRefresh;
    }
}
