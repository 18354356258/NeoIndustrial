namespace IndustrialDataCollection.Forms
{
    partial class MqttConfigForm
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
            this.SuspendLayout();

            int labelX = 20;
            int inputX = 130;
            int inputW = 250;
            int rowH = 32;
            int rowStart = 15;

            // Broker Host
            this.lblBrokerHost = new System.Windows.Forms.Label();
            this.lblBrokerHost.Text = "Broker 地址:";
            this.lblBrokerHost.Location = new System.Drawing.Point(labelX, rowStart);
            this.lblBrokerHost.Size = new System.Drawing.Size(100, 22);

            this.txtBrokerHost = new System.Windows.Forms.TextBox();
            this.txtBrokerHost.Location = new System.Drawing.Point(inputX, rowStart - 2);
            this.txtBrokerHost.Size = new System.Drawing.Size(inputW, 23);
            this.txtBrokerHost.Text = "localhost";

            // Broker Port
            this.lblBrokerPort = new System.Windows.Forms.Label();
            this.lblBrokerPort.Text = "端口:";
            this.lblBrokerPort.Location = new System.Drawing.Point(labelX, rowStart + rowH);
            this.lblBrokerPort.Size = new System.Drawing.Size(100, 22);

            this.txtBrokerPort = new System.Windows.Forms.TextBox();
            this.txtBrokerPort.Location = new System.Drawing.Point(inputX, rowStart + rowH - 2);
            this.txtBrokerPort.Size = new System.Drawing.Size(inputW, 23);
            this.txtBrokerPort.Text = "1883";

            // Client ID
            this.lblClientId = new System.Windows.Forms.Label();
            this.lblClientId.Text = "客户端 ID:";
            this.lblClientId.Location = new System.Drawing.Point(labelX, rowStart + rowH * 2);
            this.lblClientId.Size = new System.Drawing.Size(100, 22);

            this.txtClientId = new System.Windows.Forms.TextBox();
            this.txtClientId.Location = new System.Drawing.Point(inputX, rowStart + rowH * 2 - 2);
            this.txtClientId.Size = new System.Drawing.Size(inputW, 23);

            // Username
            this.lblUsername = new System.Windows.Forms.Label();
            this.lblUsername.Text = "用户名:";
            this.lblUsername.Location = new System.Drawing.Point(labelX, rowStart + rowH * 3);
            this.lblUsername.Size = new System.Drawing.Size(100, 22);

            this.txtUsername = new System.Windows.Forms.TextBox();
            this.txtUsername.Location = new System.Drawing.Point(inputX, rowStart + rowH * 3 - 2);
            this.txtUsername.Size = new System.Drawing.Size(inputW, 23);

            // Password
            this.lblPassword = new System.Windows.Forms.Label();
            this.lblPassword.Text = "密码:";
            this.lblPassword.Location = new System.Drawing.Point(labelX, rowStart + rowH * 4);
            this.lblPassword.Size = new System.Drawing.Size(100, 22);

            this.txtPassword = new System.Windows.Forms.TextBox();
            this.txtPassword.Location = new System.Drawing.Point(inputX, rowStart + rowH * 4 - 2);
            this.txtPassword.Size = new System.Drawing.Size(inputW, 23);
            this.txtPassword.PasswordChar = '*';

            // Topic Prefix
            this.lblTopicPrefix = new System.Windows.Forms.Label();
            this.lblTopicPrefix.Text = "主题前缀:";
            this.lblTopicPrefix.Location = new System.Drawing.Point(labelX, rowStart + rowH * 5);
            this.lblTopicPrefix.Size = new System.Drawing.Size(100, 22);

            this.txtTopicPrefix = new System.Windows.Forms.TextBox();
            this.txtTopicPrefix.Location = new System.Drawing.Point(inputX, rowStart + rowH * 5 - 2);
            this.txtTopicPrefix.Size = new System.Drawing.Size(inputW, 23);
            this.txtTopicPrefix.Text = "industrial/data";

            // QoS
            this.lblQos = new System.Windows.Forms.Label();
            this.lblQos.Text = "QoS:";
            this.lblQos.Location = new System.Drawing.Point(labelX, rowStart + rowH * 6);
            this.lblQos.Size = new System.Drawing.Size(100, 22);

            this.comboQos = new System.Windows.Forms.ComboBox();
            this.comboQos.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboQos.Items.AddRange(new object[] { "QoS 0", "QoS 1", "QoS 2" });
            this.comboQos.Location = new System.Drawing.Point(inputX, rowStart + rowH * 6 - 2);
            this.comboQos.Size = new System.Drawing.Size(inputW, 23);
            this.comboQos.SelectedIndex = 1;

            // Auto Reconnect
            this.chkAutoReconnect = new System.Windows.Forms.CheckBox();
            this.chkAutoReconnect.Text = "自动重连";
            this.chkAutoReconnect.Checked = true;
            this.chkAutoReconnect.Location = new System.Drawing.Point(inputX, rowStart + rowH * 7);
            this.chkAutoReconnect.Size = new System.Drawing.Size(120, 22);

            // Enabled
            this.chkEnabled = new System.Windows.Forms.CheckBox();
            this.chkEnabled.Text = "启用 MQTT";
            this.chkEnabled.Location = new System.Drawing.Point(inputX + 140, rowStart + rowH * 7);
            this.chkEnabled.Size = new System.Drawing.Size(120, 22);

            // Test button
            this.btnTest = new System.Windows.Forms.Button();
            this.btnTest.Text = "测试连接";
            this.btnTest.Location = new System.Drawing.Point(inputX, rowStart + rowH * 8 + 5);
            this.btnTest.Size = new System.Drawing.Size(100, 28);
            this.btnTest.Click += new System.EventHandler(this.btnTest_Click);

            // Save / Cancel
            this.btnSave = new System.Windows.Forms.Button();
            this.btnSave.Text = "保存";
            this.btnSave.Location = new System.Drawing.Point(240, rowStart + rowH * 8 + 5);
            this.btnSave.Size = new System.Drawing.Size(85, 28);
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);

            this.btnCancel = new System.Windows.Forms.Button();
            this.btnCancel.Text = "取消";
            this.btnCancel.Location = new System.Drawing.Point(335, rowStart + rowH * 8 + 5);
            this.btnCancel.Size = new System.Drawing.Size(85, 28);
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // MqttConfigForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 19F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(460, 340);
            this.Controls.Add(this.lblBrokerHost);
            this.Controls.Add(this.txtBrokerHost);
            this.Controls.Add(this.lblBrokerPort);
            this.Controls.Add(this.txtBrokerPort);
            this.Controls.Add(this.lblClientId);
            this.Controls.Add(this.txtClientId);
            this.Controls.Add(this.lblUsername);
            this.Controls.Add(this.txtUsername);
            this.Controls.Add(this.lblPassword);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.lblTopicPrefix);
            this.Controls.Add(this.txtTopicPrefix);
            this.Controls.Add(this.lblQos);
            this.Controls.Add(this.comboQos);
            this.Controls.Add(this.chkAutoReconnect);
            this.Controls.Add(this.chkEnabled);
            this.Controls.Add(this.btnTest);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnCancel);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 8.25F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MqttConfigForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "MQTT 配置";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label lblBrokerHost;
        private System.Windows.Forms.TextBox txtBrokerHost;
        private System.Windows.Forms.Label lblBrokerPort;
        private System.Windows.Forms.TextBox txtBrokerPort;
        private System.Windows.Forms.Label lblClientId;
        private System.Windows.Forms.TextBox txtClientId;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Label lblTopicPrefix;
        private System.Windows.Forms.TextBox txtTopicPrefix;
        private System.Windows.Forms.Label lblQos;
        private System.Windows.Forms.ComboBox comboQos;
        private System.Windows.Forms.CheckBox chkAutoReconnect;
        private System.Windows.Forms.CheckBox chkEnabled;
        private System.Windows.Forms.Button btnTest;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
    }
}
