using System.Windows.Forms;

namespace IndustrialDataCollection.Forms
{
    partial class TemplateApplyForm
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
            this.pnlRight = new System.Windows.Forms.Panel();
            this.gbxPreview = new System.Windows.Forms.GroupBox();
            this.lblPrevDesc = new System.Windows.Forms.Label();
            this.lblPrevTime = new System.Windows.Forms.Label();
            this.lblPrevSourceDevice = new System.Windows.Forms.Label();
            this.lblPrevVersion = new System.Windows.Forms.Label();
            this.lblPrevCategory = new System.Windows.Forms.Label();
            this.lblPrevName = new System.Windows.Forms.Label();
            this.gbxContents = new System.Windows.Forms.GroupBox();
            this.lblContClean = new System.Windows.Forms.Label();
            this.lblContEvents = new System.Windows.Forms.Label();
            this.lblContFabric = new System.Windows.Forms.Label();
            this.lblContRelations = new System.Windows.Forms.Label();
            this.lblContVars = new System.Windows.Forms.Label();
            this.pnlButtons = new System.Windows.Forms.Panel();
            this.btnApply = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblTargetDevice = new System.Windows.Forms.Label();
            this.rtbResult = new System.Windows.Forms.RichTextBox();
            ((System.ComponentModel.ISupportInitialize)(this.splMain)).BeginInit();
            this.splMain.Panel1.SuspendLayout();
            this.splMain.Panel2.SuspendLayout();
            this.splMain.SuspendLayout();
            this.pnlLeft.SuspendLayout();
            this.pnlRight.SuspendLayout();
            this.gbxPreview.SuspendLayout();
            this.gbxContents.SuspendLayout();
            this.pnlButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // Form
            // 
            this.ClientSize = new System.Drawing.Size(1050, 680);
            this.Text = "选择配置模板";
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.MinimumSize = new System.Drawing.Size(650, 450);
            this.Name = "TemplateApplyForm";
            // 
            // splMain
            // 
            this.splMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splMain.Location = new System.Drawing.Point(0, 0);
            this.splMain.Size = new System.Drawing.Size(1050, 680);
            this.splMain.SplitterDistance = 310;
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
            this.pnlLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLeft.Padding = new System.Windows.Forms.Padding(6);
            // 
            // txtSearch
            // 
            this.txtSearch.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtSearch.Location = new System.Drawing.Point(6, 6);
            this.txtSearch.Size = new System.Drawing.Size(304, 21);
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
            this.trvTemplates.Size = new System.Drawing.Size(304, 630);
            this.trvTemplates.Name = "trvTemplates";
            this.trvTemplates.TabIndex = 1;
            // 
            // pnlRight
            // 
            this.pnlRight.Controls.Add(this.lblTargetDevice);
            this.pnlRight.Controls.Add(this.gbxPreview);
            this.pnlRight.Controls.Add(this.gbxContents);
            this.pnlRight.Controls.Add(this.pnlButtons);
            this.pnlRight.Controls.Add(this.rtbResult);
            this.pnlRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlRight.Padding = new System.Windows.Forms.Padding(8);
            // 
            // lblTargetDevice
            // 
            this.lblTargetDevice.AutoSize = true;
            this.lblTargetDevice.Location = new System.Drawing.Point(12, 10);
            this.lblTargetDevice.Size = new System.Drawing.Size(200, 16);
            this.lblTargetDevice.Name = "lblTargetDevice";
            this.lblTargetDevice.TabIndex = 4;
            this.lblTargetDevice.Text = "目标设备: --";
            this.lblTargetDevice.Font = new System.Drawing.Font("Microsoft YaHei", 9f, System.Drawing.FontStyle.Bold);
            // 
            // gbxPreview
            // 
            this.gbxPreview.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gbxPreview.Controls.Add(this.lblPrevDesc);
            this.gbxPreview.Controls.Add(this.lblPrevTime);
            this.gbxPreview.Controls.Add(this.lblPrevSourceDevice);
            this.gbxPreview.Controls.Add(this.lblPrevVersion);
            this.gbxPreview.Controls.Add(this.lblPrevCategory);
            this.gbxPreview.Controls.Add(this.lblPrevName);
            this.gbxPreview.Location = new System.Drawing.Point(8, 32);
            this.gbxPreview.Size = new System.Drawing.Size(720, 190);
            this.gbxPreview.Name = "gbxPreview";
            this.gbxPreview.TabIndex = 0;
            this.gbxPreview.TabStop = false;
            this.gbxPreview.Text = "模板详情预览";
            // 
            // lblPrevName
            // 
            this.lblPrevName.AutoSize = true;
            this.lblPrevName.Location = new System.Drawing.Point(12, 24);
            this.lblPrevName.Size = new System.Drawing.Size(200, 16);
            this.lblPrevName.Name = "lblPrevName";
            this.lblPrevName.TabIndex = 0;
            this.lblPrevName.Text = "名称: --";
            // 
            // lblPrevCategory
            // 
            this.lblPrevCategory.AutoSize = true;
            this.lblPrevCategory.Location = new System.Drawing.Point(12, 46);
            this.lblPrevCategory.Size = new System.Drawing.Size(80, 16);
            this.lblPrevCategory.Name = "lblPrevCategory";
            this.lblPrevCategory.TabIndex = 1;
            this.lblPrevCategory.Text = "分类: --";
            // 
            // lblPrevVersion
            // 
            this.lblPrevVersion.AutoSize = true;
            this.lblPrevVersion.Location = new System.Drawing.Point(12, 68);
            this.lblPrevVersion.Size = new System.Drawing.Size(60, 16);
            this.lblPrevVersion.Name = "lblPrevVersion";
            this.lblPrevVersion.TabIndex = 2;
            this.lblPrevVersion.Text = "版本: --";
            // 
            // lblPrevSourceDevice
            // 
            this.lblPrevSourceDevice.AutoSize = true;
            this.lblPrevSourceDevice.Location = new System.Drawing.Point(12, 90);
            this.lblPrevSourceDevice.Size = new System.Drawing.Size(80, 16);
            this.lblPrevSourceDevice.Name = "lblPrevSourceDevice";
            this.lblPrevSourceDevice.TabIndex = 3;
            this.lblPrevSourceDevice.Text = "来源设备: --";
            // 
            // lblPrevTime
            // 
            this.lblPrevTime.AutoSize = true;
            this.lblPrevTime.Location = new System.Drawing.Point(12, 112);
            this.lblPrevTime.Size = new System.Drawing.Size(200, 16);
            this.lblPrevTime.Name = "lblPrevTime";
            this.lblPrevTime.TabIndex = 4;
            this.lblPrevTime.Text = "创建时间: --";
            // 
            // lblPrevDesc
            // 
            this.lblPrevDesc.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblPrevDesc.Location = new System.Drawing.Point(12, 136);
            this.lblPrevDesc.Size = new System.Drawing.Size(696, 40);
            this.lblPrevDesc.Name = "lblPrevDesc";
            this.lblPrevDesc.TabIndex = 5;
            this.lblPrevDesc.Text = "描述: --";
            // 
            // gbxContents
            // 
            this.gbxContents.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.gbxContents.Controls.Add(this.lblContClean);
            this.gbxContents.Controls.Add(this.lblContEvents);
            this.gbxContents.Controls.Add(this.lblContFabric);
            this.gbxContents.Controls.Add(this.lblContRelations);
            this.gbxContents.Controls.Add(this.lblContVars);
            this.gbxContents.Location = new System.Drawing.Point(8, 230);
            this.gbxContents.Size = new System.Drawing.Size(720, 110);
            this.gbxContents.Name = "gbxContents";
            this.gbxContents.TabIndex = 1;
            this.gbxContents.TabStop = false;
            this.gbxContents.Text = "包含内容";
            // 
            // lblContVars
            // 
            this.lblContVars.AutoSize = true;
            this.lblContVars.Location = new System.Drawing.Point(12, 24);
            this.lblContVars.Size = new System.Drawing.Size(160, 16);
            this.lblContVars.Name = "lblContVars";
            this.lblContVars.TabIndex = 0;
            this.lblContVars.Text = "• 0 个变量映射规则";
            // 
            // lblContRelations
            // 
            this.lblContRelations.AutoSize = true;
            this.lblContRelations.Location = new System.Drawing.Point(12, 44);
            this.lblContRelations.Size = new System.Drawing.Size(120, 16);
            this.lblContRelations.Name = "lblContRelations";
            this.lblContRelations.TabIndex = 1;
            this.lblContRelations.Text = "• 0 条语义关系";
            // 
            // lblContFabric
            // 
            this.lblContFabric.AutoSize = true;
            this.lblContFabric.Location = new System.Drawing.Point(12, 64);
            this.lblContFabric.Size = new System.Drawing.Size(140, 16);
            this.lblContFabric.Name = "lblContFabric";
            this.lblContFabric.TabIndex = 2;
            this.lblContFabric.Text = "• 0 个Fabric算子配置";
            // 
            // lblContEvents
            // 
            this.lblContEvents.AutoSize = true;
            this.lblContEvents.Location = new System.Drawing.Point(12, 84);
            this.lblContEvents.Size = new System.Drawing.Size(120, 16);
            this.lblContEvents.Name = "lblContEvents";
            this.lblContEvents.TabIndex = 3;
            this.lblContEvents.Text = "• 0 条事件规则";
            // 
            // lblContClean
            // 
            this.lblContClean.AutoSize = true;
            this.lblContClean.Location = new System.Drawing.Point(300, 24);
            this.lblContClean.Size = new System.Drawing.Size(120, 16);
            this.lblContClean.Name = "lblContClean";
            this.lblContClean.TabIndex = 4;
            this.lblContClean.Text = "• 0 条清洗策略";
            // 
            // pnlButtons
            // 
            this.pnlButtons.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlButtons.Controls.Add(this.btnApply);
            this.pnlButtons.Controls.Add(this.btnCancel);
            this.pnlButtons.Location = new System.Drawing.Point(390, 330);
            this.pnlButtons.Size = new System.Drawing.Size(168, 30);
            this.pnlButtons.Name = "pnlButtons";
            this.pnlButtons.TabIndex = 2;
            // 
            // btnApply
            // 
            this.btnApply.Location = new System.Drawing.Point(8, 2);
            this.btnApply.Size = new System.Drawing.Size(75, 26);
            this.btnApply.Name = "btnApply";
            this.btnApply.TabIndex = 0;
            this.btnApply.Text = "应用配置";
            this.btnApply.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(90, 2);
            this.btnCancel.Size = new System.Drawing.Size(75, 26);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // rtbResult
            // 
            this.rtbResult.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.rtbResult.Font = new System.Drawing.Font("Consolas", 10f);
            this.rtbResult.Location = new System.Drawing.Point(8, 366);
            this.rtbResult.Size = new System.Drawing.Size(720, 270);
            this.rtbResult.Name = "rtbResult";
            this.rtbResult.ReadOnly = true;
            this.rtbResult.TabIndex = 3;
            this.rtbResult.Text = "";
            this.rtbResult.Visible = false;
            // 
            this.Controls.Add(this.splMain);
            this.splMain.Panel1.ResumeLayout(false);
            this.splMain.Panel2.ResumeLayout(false);
            this.splMain.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splMain)).EndInit();
            this.splMain.ResumeLayout(false);
            this.pnlLeft.ResumeLayout(false);
            this.pnlLeft.PerformLayout();
            this.pnlRight.ResumeLayout(false);
            this.pnlRight.PerformLayout();
            this.gbxPreview.ResumeLayout(false);
            this.gbxPreview.PerformLayout();
            this.gbxContents.ResumeLayout(false);
            this.gbxContents.PerformLayout();
            this.pnlButtons.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.SplitContainer splMain;
        private System.Windows.Forms.Panel pnlLeft;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.TreeView trvTemplates;
        private System.Windows.Forms.Panel pnlRight;
        private System.Windows.Forms.Label lblTargetDevice;
        private System.Windows.Forms.GroupBox gbxPreview;
        private System.Windows.Forms.Label lblPrevName;
        private System.Windows.Forms.Label lblPrevCategory;
        private System.Windows.Forms.Label lblPrevVersion;
        private System.Windows.Forms.Label lblPrevSourceDevice;
        private System.Windows.Forms.Label lblPrevTime;
        private System.Windows.Forms.Label lblPrevDesc;
        private System.Windows.Forms.GroupBox gbxContents;
        private System.Windows.Forms.Label lblContVars;
        private System.Windows.Forms.Label lblContRelations;
        private System.Windows.Forms.Label lblContFabric;
        private System.Windows.Forms.Label lblContEvents;
        private System.Windows.Forms.Label lblContClean;
        private System.Windows.Forms.Panel pnlButtons;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.RichTextBox rtbResult;
    }
}
