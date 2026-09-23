using System.Windows.Forms;

namespace IndustrialDataCollection.Forms
{
    partial class TemplateGeneratorForm
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
            this.lblSourceDriver = new System.Windows.Forms.Label();
            this.lblTemplateName = new System.Windows.Forms.Label();
            this.txtTemplateName = new System.Windows.Forms.TextBox();
            this.lblCategory = new System.Windows.Forms.Label();
            this.cboCategory = new System.Windows.Forms.ComboBox();
            this.lblDescription = new System.Windows.Forms.Label();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.gbxModules = new System.Windows.Forms.GroupBox();
            this.chkSemanticRelations = new System.Windows.Forms.CheckBox();
            this.chkFabricConfigs = new System.Windows.Forms.CheckBox();
            this.chkEventRules = new System.Windows.Forms.CheckBox();
            this.chkCleaningStrategies = new System.Windows.Forms.CheckBox();
            this.gbxMatchMode = new System.Windows.Forms.GroupBox();
            this.radExact = new System.Windows.Forms.RadioButton();
            this.radRegex = new System.Windows.Forms.RadioButton();
            this.radContains = new System.Windows.Forms.RadioButton();
            this.gbxVarPreview = new System.Windows.Forms.GroupBox();
            this.lstVariables = new System.Windows.Forms.ListView();
            this.colVarName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colDataType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colUnit = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colMatchRule = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.btnGenerate = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.gbxModules.SuspendLayout();
            this.gbxMatchMode.SuspendLayout();
            this.gbxVarPreview.SuspendLayout();
            this.SuspendLayout();
            // 
            // TemplateGeneratorForm
            // 
            this.ClientSize = new System.Drawing.Size(620, 560);
            this.Text = "从设备生成模板";
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Name = "TemplateGeneratorForm";
            // 
            // lblSourceDevice
            // 
            this.lblSourceDevice.AutoSize = true;
            this.lblSourceDevice.Location = new System.Drawing.Point(12, 12);
            this.lblSourceDevice.Size = new System.Drawing.Size(300, 16);
            this.lblSourceDevice.Name = "lblSourceDevice";
            this.lblSourceDevice.TabIndex = 0;
            this.lblSourceDevice.Text = "源设备: --";
            // 
            // lblSourceDriver
            // 
            this.lblSourceDriver.AutoSize = true;
            this.lblSourceDriver.Location = new System.Drawing.Point(12, 34);
            this.lblSourceDriver.Size = new System.Drawing.Size(200, 16);
            this.lblSourceDriver.Name = "lblSourceDriver";
            this.lblSourceDriver.TabIndex = 1;
            this.lblSourceDriver.Text = "驱动: --";
            this.lblSourceDriver.ForeColor = System.Drawing.Color.Gray;
            // 
            // lblTemplateName
            // 
            this.lblTemplateName.AutoSize = true;
            this.lblTemplateName.Location = new System.Drawing.Point(12, 62);
            this.lblTemplateName.Size = new System.Drawing.Size(70, 16);
            this.lblTemplateName.Name = "lblTemplateName";
            this.lblTemplateName.TabIndex = 2;
            this.lblTemplateName.Text = "模板名称:";
            // 
            // txtTemplateName
            // 
            this.txtTemplateName.Location = new System.Drawing.Point(88, 59);
            this.txtTemplateName.Size = new System.Drawing.Size(420, 21);
            this.txtTemplateName.Name = "txtTemplateName";
            this.txtTemplateName.TabIndex = 3;
            this.txtTemplateName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            // 
            // lblCategory
            // 
            this.lblCategory.AutoSize = true;
            this.lblCategory.Location = new System.Drawing.Point(12, 92);
            this.lblCategory.Size = new System.Drawing.Size(70, 16);
            this.lblCategory.Name = "lblCategory";
            this.lblCategory.TabIndex = 4;
            this.lblCategory.Text = "模板分类:";
            // 
            // cboCategory
            // 
            this.cboCategory.Location = new System.Drawing.Point(88, 89);
            this.cboCategory.Size = new System.Drawing.Size(420, 22);
            this.cboCategory.Name = "cboCategory";
            this.cboCategory.TabIndex = 5;
            this.cboCategory.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            this.cboCategory.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            // 
            // lblDescription
            // 
            this.lblDescription.AutoSize = true;
            this.lblDescription.Location = new System.Drawing.Point(12, 122);
            this.lblDescription.Size = new System.Drawing.Size(70, 16);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.TabIndex = 6;
            this.lblDescription.Text = "描述:";
            // 
            // txtDescription
            // 
            this.txtDescription.Location = new System.Drawing.Point(88, 119);
            this.txtDescription.Size = new System.Drawing.Size(420, 60);
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.TabIndex = 7;
            this.txtDescription.Multiline = true;
            this.txtDescription.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDescription.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            // 
            // gbxModules
            // 
            this.gbxModules.Controls.Add(this.chkSemanticRelations);
            this.gbxModules.Controls.Add(this.chkFabricConfigs);
            this.gbxModules.Controls.Add(this.chkEventRules);
            this.gbxModules.Controls.Add(this.chkCleaningStrategies);
            this.gbxModules.Location = new System.Drawing.Point(12, 188);
            this.gbxModules.Size = new System.Drawing.Size(496, 68);
            this.gbxModules.Name = "gbxModules";
            this.gbxModules.TabIndex = 8;
            this.gbxModules.TabStop = false;
            this.gbxModules.Text = "包含模块";
            this.gbxModules.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            // 
            // chkSemanticRelations
            // 
            this.chkSemanticRelations.AutoSize = true;
            this.chkSemanticRelations.Checked = true;
            this.chkSemanticRelations.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkSemanticRelations.Location = new System.Drawing.Point(10, 22);
            this.chkSemanticRelations.Size = new System.Drawing.Size(110, 20);
            this.chkSemanticRelations.Name = "chkSemanticRelations";
            this.chkSemanticRelations.TabIndex = 0;
            this.chkSemanticRelations.Text = "包含语义关系";
            this.chkSemanticRelations.UseVisualStyleBackColor = true;
            // 
            // chkFabricConfigs
            // 
            this.chkFabricConfigs.AutoSize = true;
            this.chkFabricConfigs.Checked = true;
            this.chkFabricConfigs.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkFabricConfigs.Location = new System.Drawing.Point(160, 22);
            this.chkFabricConfigs.Size = new System.Drawing.Size(110, 20);
            this.chkFabricConfigs.Name = "chkFabricConfigs";
            this.chkFabricConfigs.TabIndex = 1;
            this.chkFabricConfigs.Text = "包含Fabric配置";
            this.chkFabricConfigs.UseVisualStyleBackColor = true;
            // 
            // chkEventRules
            // 
            this.chkEventRules.AutoSize = true;
            this.chkEventRules.Checked = true;
            this.chkEventRules.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkEventRules.Location = new System.Drawing.Point(310, 22);
            this.chkEventRules.Size = new System.Drawing.Size(100, 20);
            this.chkEventRules.Name = "chkEventRules";
            this.chkEventRules.TabIndex = 2;
            this.chkEventRules.Text = "包含事件规则";
            this.chkEventRules.UseVisualStyleBackColor = true;
            // 
            // chkCleaningStrategies
            // 
            this.chkCleaningStrategies.AutoSize = true;
            this.chkCleaningStrategies.Checked = true;
            this.chkCleaningStrategies.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCleaningStrategies.Location = new System.Drawing.Point(10, 42);
            this.chkCleaningStrategies.Size = new System.Drawing.Size(100, 20);
            this.chkCleaningStrategies.Name = "chkCleaningStrategies";
            this.chkCleaningStrategies.TabIndex = 3;
            this.chkCleaningStrategies.Text = "包含清洗策略";
            this.chkCleaningStrategies.UseVisualStyleBackColor = true;
            // 
            // gbxMatchMode
            // 
            this.gbxMatchMode.Controls.Add(this.radExact);
            this.gbxMatchMode.Controls.Add(this.radRegex);
            this.gbxMatchMode.Controls.Add(this.radContains);
            this.gbxMatchMode.Location = new System.Drawing.Point(12, 262);
            this.gbxMatchMode.Size = new System.Drawing.Size(496, 48);
            this.gbxMatchMode.Name = "gbxMatchMode";
            this.gbxMatchMode.TabIndex = 9;
            this.gbxMatchMode.TabStop = false;
            this.gbxMatchMode.Text = "变量匹配方式";
            this.gbxMatchMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            // 
            // radExact
            // 
            this.radExact.AutoSize = true;
            this.radExact.Checked = true;
            this.radExact.Location = new System.Drawing.Point(10, 20);
            this.radExact.Size = new System.Drawing.Size(120, 20);
            this.radExact.Name = "radExact";
            this.radExact.TabIndex = 0;
            this.radExact.TabStop = true;
            this.radExact.Text = "精确匹配（推荐）";
            this.radExact.UseVisualStyleBackColor = true;
            // 
            // radRegex
            // 
            this.radRegex.AutoSize = true;
            this.radRegex.Location = new System.Drawing.Point(160, 20);
            this.radRegex.Size = new System.Drawing.Size(100, 20);
            this.radRegex.Name = "radRegex";
            this.radRegex.TabIndex = 1;
            this.radRegex.Text = "正则匹配（模糊）";
            this.radRegex.UseVisualStyleBackColor = true;
            // 
            // radContains
            // 
            this.radContains.AutoSize = true;
            this.radContains.Location = new System.Drawing.Point(310, 20);
            this.radContains.Size = new System.Drawing.Size(70, 20);
            this.radContains.Name = "radContains";
            this.radContains.TabIndex = 2;
            this.radContains.Text = "包含匹配";
            this.radContains.UseVisualStyleBackColor = true;
            // 
            // gbxVarPreview
            // 
            this.gbxVarPreview.Controls.Add(this.lstVariables);
            this.gbxVarPreview.Location = new System.Drawing.Point(12, 322);
            this.gbxVarPreview.Size = new System.Drawing.Size(596, 200);
            this.gbxVarPreview.Name = "gbxVarPreview";
            this.gbxVarPreview.TabIndex = 10;
            this.gbxVarPreview.TabStop = false;
            this.gbxVarPreview.Text = "变量映射预览";
            this.gbxVarPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            // 
            // lstVariables
            // 
            this.lstVariables.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colVarName,
            this.colDataType,
            this.colUnit,
            this.colMatchRule});
            this.lstVariables.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstVariables.FullRowSelect = true;
            this.lstVariables.GridLines = true;
            this.lstVariables.HideSelection = false;
            this.lstVariables.Location = new System.Drawing.Point(3, 17);
            this.lstVariables.Name = "lstVariables";
            this.lstVariables.Size = new System.Drawing.Size(490, 122);
            this.lstVariables.TabIndex = 0;
            this.lstVariables.UseCompatibleStateImageBehavior = false;
            this.lstVariables.View = System.Windows.Forms.View.Details;
            // 
            // colVarName
            // 
            this.colVarName.Text = "变量名";
            this.colVarName.Width = 160;
            // 
            // colDataType
            // 
            this.colDataType.Text = "数据类型";
            this.colDataType.Width = 100;
            // 
            // colUnit
            // 
            this.colUnit.Text = "单位";
            this.colUnit.Width = 80;
            // 
            // colMatchRule
            // 
            this.colMatchRule.Text = "匹配规则";
            this.colMatchRule.Width = 130;
            // 
            // btnGenerate
            // 
            this.btnGenerate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnGenerate.Location = new System.Drawing.Point(452, 530);
            this.btnGenerate.Size = new System.Drawing.Size(75, 26);
            this.btnGenerate.Name = "btnGenerate";
            this.btnGenerate.TabIndex = 11;
            this.btnGenerate.Text = "确认生成";
            this.btnGenerate.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(533, 530);
            this.btnCancel.Size = new System.Drawing.Size(75, 26);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.TabIndex = 12;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            this.Controls.Add(this.lblSourceDevice);
            this.Controls.Add(this.lblSourceDriver);
            this.Controls.Add(this.lblTemplateName);
            this.Controls.Add(this.txtTemplateName);
            this.Controls.Add(this.lblCategory);
            this.Controls.Add(this.cboCategory);
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.txtDescription);
            this.Controls.Add(this.gbxModules);
            this.Controls.Add(this.gbxMatchMode);
            this.Controls.Add(this.gbxVarPreview);
            this.Controls.Add(this.btnGenerate);
            this.Controls.Add(this.btnCancel);
            this.gbxModules.ResumeLayout(false);
            this.gbxModules.PerformLayout();
            this.gbxMatchMode.ResumeLayout(false);
            this.gbxMatchMode.PerformLayout();
            this.gbxVarPreview.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label lblSourceDevice;
        private System.Windows.Forms.Label lblSourceDriver;
        private System.Windows.Forms.Label lblTemplateName;
        private System.Windows.Forms.TextBox txtTemplateName;
        private System.Windows.Forms.Label lblCategory;
        private System.Windows.Forms.ComboBox cboCategory;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.GroupBox gbxModules;
        private System.Windows.Forms.CheckBox chkSemanticRelations;
        private System.Windows.Forms.CheckBox chkFabricConfigs;
        private System.Windows.Forms.CheckBox chkEventRules;
        private System.Windows.Forms.CheckBox chkCleaningStrategies;
        private System.Windows.Forms.GroupBox gbxMatchMode;
        private System.Windows.Forms.RadioButton radExact;
        private System.Windows.Forms.RadioButton radRegex;
        private System.Windows.Forms.RadioButton radContains;
        private System.Windows.Forms.GroupBox gbxVarPreview;
        private System.Windows.Forms.ListView lstVariables;
        private System.Windows.Forms.ColumnHeader colVarName;
        private System.Windows.Forms.ColumnHeader colDataType;
        private System.Windows.Forms.ColumnHeader colUnit;
        private System.Windows.Forms.ColumnHeader colMatchRule;
        private System.Windows.Forms.Button btnGenerate;
        private System.Windows.Forms.Button btnCancel;
    }
}
