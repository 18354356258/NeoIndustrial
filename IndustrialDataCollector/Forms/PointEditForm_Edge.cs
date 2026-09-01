using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using IndustrialDataCollection.Models;
using IndustrialDataCollection.Services;
using IndustrialDataCollection.Utils;

namespace IndustrialDataCollection.Forms
{
    public class PointEditForm_Edge : Form
    {
        private DataPoint _original;
        private bool _isEdit;
        private readonly DeviceConfig _parentDevice;

        // ═══════ Tab 1: 基本配置 ═══════
        private TextBox txtName, txtAddress;
        private ComboBox comboDataType, comboUnit, comboByteOrder;
        private NumericUpDown numScale, numOffset, numLength;
        private CheckBox chkEnabled;

        // ── 语义标签 ──
        private TextBox _txtTagZh;

        private Button _btnAutoGenTag;
        // ═══════ Tab 2: 边缘计算 ═══════
        // 修约
        private CheckBox chkRounding;
        private ComboBox comboRoundMode;
        private NumericUpDown numRoundDecimals;
        // 滤波
        private CheckBox chkFilter;
        private ComboBox comboFilterMode;
        private NumericUpDown numFilterWindow, numFilterAlpha;
        // 信号变换
        private CheckBox chkSqrt, chkAbs, chkRate;
        // 数据清洗
        private CheckBox chkClean, chkDeadBand, chkClip, chkOutlier;
        private NumericUpDown numDeadBand, numClipMin, numClipMax, numSigma;
        // 新增清洗
        private CheckBox chkNanFilter, chkNanNaN, chkNanInf, chkNanNeg, chkFreeze, chkSpike, chkRocLimit, chkIqr, chkRange;
        private NumericUpDown numNanReplacement, numFreezeWindow, numSpikeWindow, numSpikeThresh, numRocMax, numIqrMult, numRangeMin, numRangeMax;

        // ═══════ Tab 3: 报警设置 ═══════
        private CheckBox chkAlarm;
        private NumericUpDown numAlarmDelay;
        private CheckBox chkHH, chkH, chkL, chkLL;
        private NumericUpDown numHH, numH, numL, numLL;

        // ═══════ Tab 4: 公式计算 ═══════
        private CheckBox chkCalc;
        private TextBox txtExpression;
        private Label lblCalcHint;

        // ═══════ Tab 5: 自定义脚本 ═══════
        private CheckBox chkScript;
        private ComboBox comboScriptLang;
        private TextBox txtScriptPath;
        private Button btnBrowseScript, btnTestScript;
        private TextBox txtScriptArgs;
        private RadioButton rdoPreProcess, rdoPostProcess;

        // ═══════ Tab 6: 存储策略 ═══════
        private CheckBox chkStoreDb, chkStoreChange;
        private TextBox txtStoreTopic;
        private NumericUpDown numStorePrecision, numStoreDeadband;

        // ═══════ Tab 7: SPC 统计过程控制 ═══════
        private CheckBox chkSpc;
        private NumericUpDown numSpcUcl, numSpcLcl, numSpcUsl, numSpcLsl, numSpcTarget, numSpcSubgroup;

        private Button btnOk, btnCancel;

        public DataPoint DataPoint { get; private set; }
        public bool IsSaved { get; private set; }
        private bool _isLoading;

        // ═══════ 构造函数 ═══════
        public PointEditForm_Edge(DataPoint existing, DeviceConfig parentDevice = null)
        {
            _original = existing;
            _isEdit = existing != null;
            _parentDevice = parentDevice;
            var L = LanguageManager.Instance;

            this.Text = _isEdit ? L.GetString("PointEdit_Title_Edit") : L.GetString("PointEdit_Title_Add");
            this.Size = new Size(800, 930);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false; this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Icon = Program.AppIcon;
            this.Font = new Font("Microsoft YaHei UI", 9F);
            this.Padding = new Padding(6);

            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                DrawMode = TabDrawMode.OwnerDrawFixed,
                ItemSize = new Size(100, 32),
                Padding = new Point(8, 6)
            };
            tabControl.DrawItem += TabControl_DrawItem;

            tabControl.TabPages.Add(CreateBasicTab(L));
            tabControl.TabPages.Add(CreateEdgeTab(L));
            tabControl.TabPages.Add(CreateAlarmTab(L));
            tabControl.TabPages.Add(CreateFormulaTab(L));
            tabControl.TabPages.Add(CreateScriptTab(L));
            tabControl.TabPages.Add(CreateStorageTab(L));
            tabControl.TabPages.Add(CreateSpcTab(L));

            var btnPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                Padding = new Padding(0, 10, 16, 0)
            };
            btnOk = new Button { Text = L.GetString("PointEdit_Ok"), Size = new Size(100, 34), Font = new Font("Microsoft YaHei UI", 9F), Anchor = AnchorStyles.Right | AnchorStyles.Bottom, Location = new Point(666, 10) };
            btnCancel = new Button { Text = L.GetString("PointEdit_Cancel"), Size = new Size(100, 34), Font = new Font("Microsoft YaHei UI", 9F), Anchor = AnchorStyles.Right | AnchorStyles.Bottom, Location = new Point(558, 10) };
            btnOk.Click += (s, e) => Save();
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            btnPanel.Controls.Add(btnOk);
            btnPanel.Controls.Add(btnCancel);

            this.Controls.Add(tabControl);
            this.Controls.Add(btnPanel);
            this.AcceptButton = btnOk;

            if (existing != null)
            {
                _isLoading = true;
                try { LoadPoint(existing); }
                finally { _isLoading = false; }
            }
        }

        // ╔══════════════════════════════════════════════════════╗
        // ║  Tab 1: 基本配置                                    ║
        // ╚══════════════════════════════════════════════════════╝
        private TabPage CreateBasicTab(LanguageManager L)
        {
            var tab = new TabPage { Text = L.GetString("EdgeTab_Basic"), Padding = new Padding(10) };

            var gbId = NewGroupBox(L.GetString("Basic_Group_Id"), 12, 10, 756, 514);

            int y = 30, rowH = 34;
            void AddRow(string label, Control ctrl)
            {
                var lbl = new Label { Text = label, Location = new Point(16, y), Size = new Size(100, 23), TextAlign = ContentAlignment.MiddleRight };
                ctrl.Location = new Point(126, y - 2);
                gbId.Controls.Add(lbl);
                gbId.Controls.Add(ctrl);
                y += rowH;
            }

            txtName = new TextBox { Size = new Size(260, 28), Font = this.Font };
            AddRow(L.GetString("PointEdit_Name") + ":", txtName);

            // v2.0: NameEn 输入框已移除，英文标签统一废弃

            txtAddress = new TextBox { Size = new Size(260, 28), Font = this.Font };
            AddRow(L.GetString("PointEdit_Address") + ":", txtAddress);

            comboDataType = new ComboBox { Size = new Size(200, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = this.Font };
            comboDataType.Items.AddRange(new[] { "byte", "int16", "uint16", "int32", "uint32", "int64", "uint64", "float", "double", "bool", "coil", "string", "word", "dword", "real" });
            comboDataType.SelectedIndex = 1;
            comboDataType.SelectedIndexChanged += OnDataTypeChanged;
            AddRow(L.GetString("PointEdit_DataType") + ":", comboDataType);

            numLength = new NumericUpDown { Size = new Size(100, 28), Minimum = 1, Maximum = 1024, Value = 1, Font = this.Font };
            AddRow(L.GetString("PointEdit_Length") + ":", numLength);

            comboByteOrder = new ComboBox { Size = new Size(260, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = this.Font };
            comboByteOrder.Items.AddRange(new[] { "ABCD (大端)", "DCBA (小端)", "BADC (字内交换)", "CDAB (字间交换)" });
            comboByteOrder.SelectedIndex = 0;
            AddRow(L.GetString("PointEdit_ByteOrder") + ":", comboByteOrder);

            comboUnit = new ComboBox { Size = new Size(200, 28), DropDownStyle = ComboBoxStyle.DropDown, Font = this.Font };
            comboUnit.Items.AddRange(new[] { "", "°C","°F","K","MPa","kPa","Pa","bar","KN","N","kgf","L/min","L/h","L/s","m³/h","m³/min","m³/s","RPM","Hz","kHz","%","m","cm","mm","μm","V","mV","A","mA","kW","W","MW","kWh","kg","g","t","m/s","km/h","mm/s" });
            AddRow(L.GetString("PointEdit_Unit") + ":", comboUnit);

            numScale = new NumericUpDown { Size = new Size(120, 28), DecimalPlaces = 3, Minimum = -100000, Maximum = 100000, Value = 1, Font = this.Font };
            AddRow(L.GetString("PointEdit_Scale") + ":", numScale);

            numOffset = new NumericUpDown { Size = new Size(120, 28), DecimalPlaces = 3, Minimum = -100000, Maximum = 100000, Font = this.Font };
            AddRow(L.GetString("PointEdit_Offset") + ":", numOffset);

            chkEnabled = new CheckBox { Text = L.GetString("PointEdit_Enabled"), Location = new Point(126, y), Size = new Size(90, 24), Font = this.Font, Checked = true };
            gbId.Controls.Add(chkEnabled);

            // ── 语义标签分隔线 ──
            y += rowH + 6;
            var lblTagSep = new Label { Text = "── " + L.GetString("SemanticTag") + " ──", Location = new Point(16, y), Size = new Size(400, 23), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei UI", 8F) };
            gbId.Controls.Add(lblTagSep);
            y += rowH - 2;

            y += rowH;

            // Chinese alias
            var lblTagCn = new Label { Text = L.GetString("SemanticTagCn") + ":", Location = new Point(16, y), Size = new Size(100, 23), TextAlign = ContentAlignment.MiddleRight };
            _txtTagZh = new TextBox { Size = new Size(380, 28), Font = this.Font };
            _txtTagZh.Location = new Point(126, y - 2);
            _btnAutoGenTag = new Button { Text = L.GetString("TagAutoGenerate"), Location = new Point(520, y - 2), Size = new Size(90, 28), Font = this.Font, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            _btnAutoGenTag.FlatAppearance.BorderColor = Color.FromArgb(56, 145, 220);
            _btnAutoGenTag.FlatAppearance.BorderSize = 1;
            _btnAutoGenTag.Click += (s, e2) => AutoGenTags();
            gbId.Controls.Add(lblTagCn); gbId.Controls.Add(_txtTagZh); gbId.Controls.Add(_btnAutoGenTag);

            // rename->update last segment of Chinese tag
            txtName.TextChanged += (s, e2) =>
            {
                if (_isLoading) return;
                UpdateTagLastSegment();
            };

            tab.Controls.Add(gbId);
            // 保存配置按钮
            var sp = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.FromArgb(248, 250, 252) };
            var b = new Button { Text = L.GetString("PointEdit_SaveTab"), Size = new Size(80, 28), Dock = DockStyle.Right, BackColor = Color.White, ForeColor = Color.FromArgb(56, 145, 220), FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 9F) };
            b.FlatAppearance.BorderColor = Color.FromArgb(56, 145, 220);
            b.FlatAppearance.BorderSize = 1;
            b.Cursor = Cursors.Hand;
            b.Click += (s2, e2) => SaveToPoint();
            b.MouseEnter += (s2, e2) => { b.BackColor = Color.FromArgb(56, 145, 220); b.ForeColor = Color.White; };
            b.MouseLeave += (s2, e2) => { b.BackColor = Color.White; b.ForeColor = Color.FromArgb(56, 145, 220); };
            sp.Controls.Add(b);
            tab.Controls.Add(sp);
            return tab;
        }

        // ╔══════════════════════════════════════════════════════╗
        // ║  Tab 2: 边缘计算                                    ║
        // ╚══════════════════════════════════════════════════════╝
        private TabPage CreateEdgeTab(LanguageManager L)
        {
            var tab = new TabPage { Text = L.GetString("EdgeTab_Edge"), Padding = new Padding(10) };
            int yOff = 0;

            // ── GroupBox: 修约 ──
            var gbRound = NewGroupBox(L.GetString("Edge_Rounding"), 12, 10, 756, 80);
            chkRounding = new CheckBox { Text = L.GetString("Edge_Rounding"), Location = new Point(16, 30), Size = new Size(80, 24), Font = this.Font };
            comboRoundMode = new ComboBox { Location = new Point(100, 28), Size = new Size(130, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = this.Font, Enabled = false };
            comboRoundMode.Items.AddRange(new[] { L.GetString("Edge_Round_Normal"), L.GetString("Edge_Round_Floor"), L.GetString("Edge_Round_Ceil"), L.GetString("Edge_Round_Trunc") });
            comboRoundMode.SelectedIndex = 0;
            var lblDec = new Label { Text = L.GetString("Edge_Decimals") + ":", Location = new Point(245, 32), Size = new Size(50, 23), TextAlign = ContentAlignment.MiddleLeft };
            numRoundDecimals = new NumericUpDown { Location = new Point(300, 28), Size = new Size(70, 28), Minimum = 0, Maximum = 6, Value = 2, Font = this.Font, Enabled = false };
            chkRounding.CheckedChanged += (s, e) => { if (_isLoading) return; bool en = chkRounding.Checked; comboRoundMode.Enabled = numRoundDecimals.Enabled = en; };
            gbRound.Controls.Add(chkRounding);
            gbRound.Controls.Add(comboRoundMode);
            gbRound.Controls.Add(lblDec);
            gbRound.Controls.Add(numRoundDecimals);
            tab.Controls.Add(gbRound);
            yOff += 95;

            // ── GroupBox: 滤波 ──
            var gbFilter = NewGroupBox(L.GetString("Edge_Filter"), 12, 10 + yOff, 756, 80);
            chkFilter = new CheckBox { Text = L.GetString("Edge_Filter"), Location = new Point(16, 30), Size = new Size(80, 24), Font = this.Font };
            comboFilterMode = new ComboBox { Location = new Point(100, 28), Size = new Size(130, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = this.Font, Enabled = false };
            comboFilterMode.Items.AddRange(new[] { L.GetString("Edge_Filter_MovingAvg"), L.GetString("Edge_Filter_Median"), L.GetString("Edge_Filter_ExpSmooth") });
            comboFilterMode.SelectedIndex = 0;
            var lblWin = new Label { Text = L.GetString("Edge_Window") + ":", Location = new Point(245, 32), Size = new Size(45, 23), TextAlign = ContentAlignment.MiddleLeft };
            numFilterWindow = new NumericUpDown { Location = new Point(295, 28), Size = new Size(70, 28), Minimum = 2, Maximum = 60, Value = 5, Font = this.Font, Enabled = false };
            var lblAlpha = new Label { Text = "α:", Location = new Point(380, 32), Size = new Size(25, 23), TextAlign = ContentAlignment.MiddleLeft };
            numFilterAlpha = new NumericUpDown { Location = new Point(405, 28), Size = new Size(70, 28), Minimum = 0.1M, Maximum = 0.9M, Value = 0.3M, DecimalPlaces = 2, Increment = 0.1M, Font = this.Font, Enabled = false };
            chkFilter.CheckedChanged += (s, e) => { if (_isLoading) return; bool en = chkFilter.Checked; comboFilterMode.Enabled = numFilterWindow.Enabled = en; bool exp = en && comboFilterMode.SelectedIndex == 2; numFilterAlpha.Enabled = exp; lblAlpha.Visible = exp; lblWin.Text = exp ? L.GetString("Edge_Window") + ":" : L.GetString("Edge_Window") + ":"; };
            comboFilterMode.SelectedIndexChanged += (s, e) => { if (_isLoading) return; bool exp = chkFilter.Checked && comboFilterMode.SelectedIndex == 2; numFilterAlpha.Enabled = exp; lblAlpha.Visible = exp; };
            gbFilter.Controls.Add(chkFilter); gbFilter.Controls.Add(comboFilterMode);
            gbFilter.Controls.Add(lblWin); gbFilter.Controls.Add(numFilterWindow);
            gbFilter.Controls.Add(lblAlpha); gbFilter.Controls.Add(numFilterAlpha);
            tab.Controls.Add(gbFilter);
            yOff += 95;

            // ── GroupBox: 信号变换 ──
            var gbXform = NewGroupBox(L.GetString("Edge_Transform"), 12, 10 + yOff, 756, 80);
            chkSqrt = new CheckBox { Text = L.GetString("Edge_Sqrt"), Location = new Point(16, 30), Size = new Size(160, 24), Font = this.Font };
            chkAbs = new CheckBox { Text = L.GetString("Edge_Abs"), Location = new Point(190, 30), Size = new Size(160, 24), Font = this.Font };
            chkRate = new CheckBox { Text = L.GetString("Edge_RateOfChange"), Location = new Point(370, 30), Size = new Size(220, 24), Font = this.Font };
            var lblXformHint = new Label { Text = L.GetString("Edge_Transform_Hint"), Location = new Point(16, 54), Size = new Size(600, 20), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei UI", 8F) };
            gbXform.Controls.Add(chkSqrt); gbXform.Controls.Add(chkAbs); gbXform.Controls.Add(chkRate);
            gbXform.Controls.Add(lblXformHint);
            tab.Controls.Add(gbXform);
            yOff += 95;

            // ── GroupBox: 数据清洗 ──
            // ── 四、数据清洗 A: 值修正 ──
            var gbCleanA = NewGroupBox(L.GetString("Edge_CleanFix"), 12, 10 + yOff, 756, 165);
            chkClean = new CheckBox { Text = L.GetString("Edge_Cleaning"), Location = new Point(16, 28), Size = new Size(140, 22), Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) };

            // Row 1: 空值过滤 (全宽)
            chkNanFilter = new CheckBox { Text = L.GetString("Edge_NanFilter"), Location = new Point(16, 55), Size = new Size(80, 22), Font = this.Font, Enabled = false };
            chkNanNaN = new CheckBox { Text = "NaN", Location = new Point(100, 55), Size = new Size(48, 22), Font = this.Font, Enabled = false };
            chkNanInf = new CheckBox { Text = "Inf", Location = new Point(152, 55), Size = new Size(42, 22), Font = this.Font, Enabled = false };
            chkNanNeg = new CheckBox { Text = L.GetString("Edge_NanNeg"), Location = new Point(198, 55), Size = new Size(52, 22), Font = this.Font, Enabled = false };
            var lblNanRep = new Label { Text = L.GetString("Edge_NanReplace") + ":", Location = new Point(270, 57), Size = new Size(50, 20), TextAlign = ContentAlignment.MiddleRight };
            numNanReplacement = new NumericUpDown { Location = new Point(324, 53), Size = new Size(70, 24), Minimum = -100000, Maximum = 100000, DecimalPlaces = 3, Font = this.Font, Enabled = false };

            // Row 2: 死区抑制 | 限幅 (左右并排)
            chkDeadBand = new CheckBox { Text = L.GetString("Edge_DeadBand"), Location = new Point(16, 82), Size = new Size(80, 22), Font = this.Font, Enabled = false };
            numDeadBand = new NumericUpDown { Location = new Point(100, 79), Size = new Size(75, 24), Minimum = 0, Maximum = 100000, DecimalPlaces = 3, Value = 0.1M, Font = this.Font, Enabled = false };
            chkClip = new CheckBox { Text = L.GetString("Edge_Clip"), Location = new Point(390, 82), Size = new Size(55, 22), Font = this.Font, Enabled = false };
            var lblMin = new Label { Text = "Min:", Location = new Point(448, 84), Size = new Size(30, 20), TextAlign = ContentAlignment.MiddleRight };
            numClipMin = new NumericUpDown { Location = new Point(480, 79), Size = new Size(75, 24), Minimum = -100000, Maximum = 100000, DecimalPlaces = 3, Font = this.Font, Enabled = false };
            var lblMax = new Label { Text = "Max:", Location = new Point(565, 84), Size = new Size(30, 20), TextAlign = ContentAlignment.MiddleRight };
            numClipMax = new NumericUpDown { Location = new Point(598, 79), Size = new Size(75, 24), Minimum = -100000, Maximum = 100000, DecimalPlaces = 3, Value = 100, Font = this.Font, Enabled = false };

            // Row 3: 尖峰抑制 | 变化率限制 (左右并排)
            chkSpike = new CheckBox { Text = L.GetString("Edge_Spike"), Location = new Point(16, 109), Size = new Size(80, 22), Font = this.Font, Enabled = false };
            var lblSpikeW = new Label { Text = L.GetString("Edge_Window") + ":", Location = new Point(100, 111), Size = new Size(38, 20), TextAlign = ContentAlignment.MiddleRight };
            numSpikeWindow = new NumericUpDown { Location = new Point(140, 106), Size = new Size(55, 24), Minimum = 3, Maximum = 30, Value = 5, Font = this.Font, Enabled = false };
            var lblSpikeS = new Label { Text = "σ=", Location = new Point(205, 111), Size = new Size(22, 20), TextAlign = ContentAlignment.MiddleRight };
            numSpikeThresh = new NumericUpDown { Location = new Point(230, 106), Size = new Size(55, 24), Minimum = 1.0M, Maximum = 10.0M, DecimalPlaces = 1, Value = 3.0M, Font = this.Font, Enabled = false };
            chkRocLimit = new CheckBox { Text = L.GetString("Edge_RocLimit"), Location = new Point(390, 109), Size = new Size(85, 22), Font = this.Font, Enabled = false };
            var lblRoc = new Label { Text = L.GetString("Edge_RocMax") + ":", Location = new Point(478, 111), Size = new Size(50, 20), TextAlign = ContentAlignment.MiddleRight };
            numRocMax = new NumericUpDown { Location = new Point(530, 106), Size = new Size(75, 24), Minimum = 0, Maximum = 100000, DecimalPlaces = 3, Value = 1.0M, Font = this.Font, Enabled = false };
            var lblRocUnit = new Label { Text = "/s", Location = new Point(610, 111), Size = new Size(20, 20), ForeColor = Color.Gray };

            // Row 4: 异常值剔除
            chkOutlier = new CheckBox { Text = L.GetString("Edge_Outlier"), Location = new Point(16, 136), Size = new Size(95, 22), Font = this.Font, Enabled = false };
            var lblSigma2 = new Label { Text = "σ=", Location = new Point(110, 138), Size = new Size(22, 20), TextAlign = ContentAlignment.MiddleRight };
            numSigma = new NumericUpDown { Location = new Point(135, 133), Size = new Size(60, 24), Minimum = 1.0M, Maximum = 10.0M, DecimalPlaces = 1, Value = 3.0M, Increment = 0.5M, Font = this.Font, Enabled = false };

            // Event wiring for Clean A
            chkClean.CheckedChanged += (s, e) => {
                if (_isLoading) return; bool en = chkClean.Checked;
                chkNanFilter.Enabled = chkDeadBand.Enabled = chkClip.Enabled = chkOutlier.Enabled = en;
                chkSpike.Enabled = chkRocLimit.Enabled = chkFreeze.Enabled = chkIqr.Enabled = chkRange.Enabled = en;
                if (!en) DisableAllCleaningControls();
            };
            chkNanFilter.CheckedChanged += (s, e) => { if (_isLoading) return; bool en = chkClean.Checked && chkNanFilter.Checked; chkNanNaN.Enabled = chkNanInf.Enabled = chkNanNeg.Enabled = numNanReplacement.Enabled = en; };
            chkDeadBand.CheckedChanged += (s, e) => { if (_isLoading) return; numDeadBand.Enabled = chkClean.Checked && chkDeadBand.Checked; };
            chkClip.CheckedChanged += (s, e) => { if (_isLoading) return; bool en = chkClean.Checked && chkClip.Checked; numClipMin.Enabled = numClipMax.Enabled = en; };
            chkSpike.CheckedChanged += (s, e) => { if (_isLoading) return; bool en = chkClean.Checked && chkSpike.Checked; numSpikeWindow.Enabled = numSpikeThresh.Enabled = en; };
            chkRocLimit.CheckedChanged += (s, e) => { if (_isLoading) return; numRocMax.Enabled = chkClean.Checked && chkRocLimit.Checked; };
            chkOutlier.CheckedChanged += (s, e) => { if (_isLoading) return; numSigma.Enabled = chkClean.Checked && chkOutlier.Checked; };

            gbCleanA.Controls.Add(chkClean);
            gbCleanA.Controls.Add(chkNanFilter); gbCleanA.Controls.Add(chkNanNaN); gbCleanA.Controls.Add(chkNanInf); gbCleanA.Controls.Add(chkNanNeg);
            gbCleanA.Controls.Add(lblNanRep); gbCleanA.Controls.Add(numNanReplacement);
            gbCleanA.Controls.Add(chkDeadBand); gbCleanA.Controls.Add(numDeadBand);
            gbCleanA.Controls.Add(chkClip); gbCleanA.Controls.Add(lblMin); gbCleanA.Controls.Add(numClipMin);
            gbCleanA.Controls.Add(lblMax); gbCleanA.Controls.Add(numClipMax);
            gbCleanA.Controls.Add(chkSpike); gbCleanA.Controls.Add(lblSpikeW); gbCleanA.Controls.Add(numSpikeWindow);
            gbCleanA.Controls.Add(lblSpikeS); gbCleanA.Controls.Add(numSpikeThresh);
            gbCleanA.Controls.Add(chkRocLimit); gbCleanA.Controls.Add(lblRoc); gbCleanA.Controls.Add(numRocMax); gbCleanA.Controls.Add(lblRocUnit);
            gbCleanA.Controls.Add(chkOutlier); gbCleanA.Controls.Add(lblSigma2); gbCleanA.Controls.Add(numSigma);
            tab.Controls.Add(gbCleanA);
            yOff += 180;

            // ── 五、数据清洗 B: 质量检测 ──
            var gbCleanB = NewGroupBox(L.GetString("Edge_CleanCheck"), 12, 10 + yOff, 756, 105);
            var lblCleanHint = new Label { Text = L.GetString("Edge_CleanHint"), Location = new Point(16, 25), Size = new Size(500, 18), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei UI", 8F) };

            // Row 1: 冻结检测 | IQR检测 (左右并排)
            chkFreeze = new CheckBox { Text = L.GetString("Edge_Freeze"), Location = new Point(16, 48), Size = new Size(80, 22), Font = this.Font, Enabled = false };
            numFreezeWindow = new NumericUpDown { Location = new Point(100, 45), Size = new Size(55, 24), Minimum = 3, Maximum = 300, Value = 10, Font = this.Font, Enabled = false };
            var lblFreeze = new Label { Text = L.GetString("Edge_FreezeHint"), Location = new Point(160, 48), Size = new Size(50, 20), TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gray };
            chkIqr = new CheckBox { Text = L.GetString("Edge_Iqr"), Location = new Point(390, 48), Size = new Size(80, 22), Font = this.Font, Enabled = false };
            var lblIqr = new Label { Text = L.GetString("Edge_IqrMult") + ":", Location = new Point(473, 50), Size = new Size(38, 20), TextAlign = ContentAlignment.MiddleRight };
            numIqrMult = new NumericUpDown { Location = new Point(513, 45), Size = new Size(55, 24), Minimum = 0.5M, Maximum = 5.0M, DecimalPlaces = 1, Value = 1.5M, Increment = 0.5M, Font = this.Font, Enabled = false };

            // Row 2: 量程合理性
            chkRange = new CheckBox { Text = L.GetString("Edge_Range"), Location = new Point(16, 75), Size = new Size(85, 22), Font = this.Font, Enabled = false };
            var lblRangeMin = new Label { Text = "Min:", Location = new Point(105, 77), Size = new Size(28, 20), TextAlign = ContentAlignment.MiddleRight };
            numRangeMin = new NumericUpDown { Location = new Point(135, 74), Size = new Size(65, 24), Minimum = -100000, Maximum = 100000, DecimalPlaces = 3, Font = this.Font, Enabled = false };
            var lblRangeMax = new Label { Text = "Max:", Location = new Point(210, 77), Size = new Size(28, 20), TextAlign = ContentAlignment.MiddleRight };
            numRangeMax = new NumericUpDown { Location = new Point(240, 74), Size = new Size(65, 24), Minimum = -100000, Maximum = 100000, DecimalPlaces = 3, Value = 100, Font = this.Font, Enabled = false };

            // Event wiring for Clean B
            chkFreeze.CheckedChanged += (s, e) => { if (_isLoading) return; numFreezeWindow.Enabled = chkClean.Checked && chkFreeze.Checked; };
            chkIqr.CheckedChanged += (s, e) => { if (_isLoading) return; numIqrMult.Enabled = chkClean.Checked && chkIqr.Checked; };
            chkRange.CheckedChanged += (s, e) => { if (_isLoading) return; bool en = chkClean.Checked && chkRange.Checked; numRangeMin.Enabled = numRangeMax.Enabled = en; };

            gbCleanB.Controls.Add(lblCleanHint);
            gbCleanB.Controls.Add(chkFreeze); gbCleanB.Controls.Add(numFreezeWindow); gbCleanB.Controls.Add(lblFreeze);
            gbCleanB.Controls.Add(chkIqr); gbCleanB.Controls.Add(lblIqr); gbCleanB.Controls.Add(numIqrMult);
            gbCleanB.Controls.Add(chkRange); gbCleanB.Controls.Add(lblRangeMin); gbCleanB.Controls.Add(numRangeMin);
            gbCleanB.Controls.Add(lblRangeMax); gbCleanB.Controls.Add(numRangeMax);
            tab.Controls.Add(gbCleanB);
            yOff += 120;

            // 保存配置按钮
            var sp = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.FromArgb(248, 250, 252) };
            var b = new Button { Text = L.GetString("PointEdit_SaveTab"), Size = new Size(80, 28), Dock = DockStyle.Right, BackColor = Color.White, ForeColor = Color.FromArgb(56, 145, 220), FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 9F) };
            b.FlatAppearance.BorderColor = Color.FromArgb(56, 145, 220);
            b.FlatAppearance.BorderSize = 1;
            b.Cursor = Cursors.Hand;
            b.Click += (s2, e2) => SaveToPoint();
            b.MouseEnter += (s2, e2) => { b.BackColor = Color.FromArgb(56, 145, 220); b.ForeColor = Color.White; };
            b.MouseLeave += (s2, e2) => { b.BackColor = Color.White; b.ForeColor = Color.FromArgb(56, 145, 220); };
            sp.Controls.Add(b);
            tab.Controls.Add(sp);
            return tab;
        }

        // ╔══════════════════════════════════════════════════════╗
        // ║  Tab 3: 报警设置                                    ║
        // ╚══════════════════════════════════════════════════════╝
        private TabPage CreateAlarmTab(LanguageManager L)
        {
            var tab = new TabPage { Text = L.GetString("EdgeTab_Alarm"), Padding = new Padding(10) };

            var gbAlarm = NewGroupBox(L.GetString("Edge_Alarm_Enable"), 12, 10, 756, 280);

            chkAlarm = new CheckBox { Text = L.GetString("Edge_Alarm_Enable"), Location = new Point(16, 30), Size = new Size(120, 24), Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) };
            var lblDelay = new Label { Text = L.GetString("Edge_Alarm_Delay") + ":", Location = new Point(150, 32), Size = new Size(50, 23), TextAlign = ContentAlignment.MiddleLeft };
            numAlarmDelay = new NumericUpDown { Location = new Point(205, 29), Size = new Size(80, 28), Minimum = 0, Maximum = 3600, Value = 0, Font = this.Font, Enabled = false };
            var lblSec = new Label { Text = L.GetString("Edge_Seconds"), Location = new Point(290, 32), Size = new Size(40, 23), TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gray };

            gbAlarm.Controls.Add(chkAlarm); gbAlarm.Controls.Add(lblDelay); gbAlarm.Controls.Add(numAlarmDelay); gbAlarm.Controls.Add(lblSec);

            // 四级报警行
            int rowY = 70;
            chkHH = new CheckBox { Text = "HH " + L.GetString("Edge_Alarm_HH"), Location = new Point(24, rowY), Size = new Size(90, 24), Font = this.Font, Enabled = false };
            numHH = new NumericUpDown { Location = new Point(125, rowY - 1), Size = new Size(150, 28), Minimum = -100000, Maximum = 100000, DecimalPlaces = 3, Value = 100, Font = this.Font, Enabled = false };
            chkHH.CheckedChanged += (s, e) => { if (_isLoading) return; numHH.Enabled = chkAlarm.Checked && chkHH.Checked; };
            gbAlarm.Controls.Add(chkHH); gbAlarm.Controls.Add(numHH);
            rowY += 44;

            chkH = new CheckBox { Text = "H  " + L.GetString("Edge_Alarm_H"), Location = new Point(24, rowY), Size = new Size(90, 24), Font = this.Font, Enabled = false };
            numH = new NumericUpDown { Location = new Point(125, rowY - 1), Size = new Size(150, 28), Minimum = -100000, Maximum = 100000, DecimalPlaces = 3, Value = 80, Font = this.Font, Enabled = false };
            chkH.CheckedChanged += (s, e) => { if (_isLoading) return; numH.Enabled = chkAlarm.Checked && chkH.Checked; };
            gbAlarm.Controls.Add(chkH); gbAlarm.Controls.Add(numH);
            rowY += 44;

            chkL = new CheckBox { Text = "L  " + L.GetString("Edge_Alarm_L"), Location = new Point(24, rowY), Size = new Size(90, 24), Font = this.Font, Enabled = false };
            numL = new NumericUpDown { Location = new Point(125, rowY - 1), Size = new Size(150, 28), Minimum = -100000, Maximum = 100000, DecimalPlaces = 3, Value = 20, Font = this.Font, Enabled = false };
            chkL.CheckedChanged += (s, e) => { if (_isLoading) return; numL.Enabled = chkAlarm.Checked && chkL.Checked; };
            gbAlarm.Controls.Add(chkL); gbAlarm.Controls.Add(numL);
            rowY += 44;

            chkLL = new CheckBox { Text = "LL " + L.GetString("Edge_Alarm_LL"), Location = new Point(24, rowY), Size = new Size(90, 24), Font = this.Font, Enabled = false };
            numLL = new NumericUpDown { Location = new Point(125, rowY - 1), Size = new Size(150, 28), Minimum = -100000, Maximum = 100000, DecimalPlaces = 3, Value = 0, Font = this.Font, Enabled = false };
            chkLL.CheckedChanged += (s, e) => { if (_isLoading) return; numLL.Enabled = chkAlarm.Checked && chkLL.Checked; };
            gbAlarm.Controls.Add(chkLL); gbAlarm.Controls.Add(numLL);

            chkAlarm.CheckedChanged += (s, e) => { if (_isLoading) return; bool en = chkAlarm.Checked; numAlarmDelay.Enabled = chkHH.Enabled = chkH.Enabled = chkL.Enabled = chkLL.Enabled = en; if (!en) { numHH.Enabled = numH.Enabled = numL.Enabled = numLL.Enabled = false; } };

            tab.Controls.Add(gbAlarm);
            // 保存配置按钮
            var sp = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.FromArgb(248, 250, 252) };
            var b = new Button { Text = L.GetString("PointEdit_SaveTab"), Size = new Size(80, 28), Dock = DockStyle.Right, BackColor = Color.White, ForeColor = Color.FromArgb(56, 145, 220), FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 9F) };
            b.FlatAppearance.BorderColor = Color.FromArgb(56, 145, 220);
            b.FlatAppearance.BorderSize = 1;
            b.Cursor = Cursors.Hand;
            b.Click += (s2, e2) => SaveToPoint();
            b.MouseEnter += (s2, e2) => { b.BackColor = Color.FromArgb(56, 145, 220); b.ForeColor = Color.White; };
            b.MouseLeave += (s2, e2) => { b.BackColor = Color.White; b.ForeColor = Color.FromArgb(56, 145, 220); };
            sp.Controls.Add(b);
            tab.Controls.Add(sp);
            return tab;
        }

        // ╔══════════════════════════════════════════════════════╗
        // ║  Tab 4: 公式计算                                    ║
        // ╚══════════════════════════════════════════════════════╝
        private TabPage CreateFormulaTab(LanguageManager L)
        {
            var tab = new TabPage { Text = L.GetString("EdgeTab_Calc"), Padding = new Padding(10) };

            var gbCalc = NewGroupBox(L.GetString("Edge_Calc_Enable"), 12, 10, 756, 560);

            chkCalc = new CheckBox { Text = L.GetString("Edge_Calc_Enable"), Location = new Point(16, 30), Size = new Size(150, 24), Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) };
            txtExpression = new TextBox { Location = new Point(16, 62), Size = new Size(710, 60), Font = new Font("Consolas", 10F), Multiline = true };
            txtExpression.Enabled = false;
            chkCalc.CheckedChanged += (s, e) => { if (_isLoading) return; txtExpression.Enabled = chkCalc.Checked; };

            lblCalcHint = new Label { Text = L.GetString("Edge_Calc_Hint"), Location = new Point(16, 130), Size = new Size(700, 20), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei UI", 8F) };

            var lblExamples = new Label { Text = L.GetString("Edge_Calc_Examples"), Location = new Point(16, 158), Size = new Size(200, 23), Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) };

            // 24 formula example buttons in a 4×6 grid
            int bw = 175, bh = 34, gapX = 8, gapY = 6;
            int startX = 16, startY = 190;
            var examples = new (string label, string expr)[] {
                ("℃ → ℉   x*1.8+32", "x * 1.8 + 32"),
                ("℉ → ℃   (x-32)/1.8", "(x - 32) / 1.8"),
                ("K → ℃   x-273.15", "x - 273.15"),
                ("℃ → K   x+273.15", "x + 273.15"),
                ("Pa → kPa   x/1000", "x / 1000"),
                ("kPa → Pa   x*1000", "x * 1000"),
                ("bar → kPa   x*100", "x * 100"),
                ("MPa → kPa   x*1000", "x * 1000"),
                ("PSI → kPa   x*6.895", "x * 6.895"),
                ("kPa → PSI   x/6.895", "x / 6.895"),
                ("mm → m   x/1000", "x / 1000"),
                ("m → mm   x*1000", "x * 1000"),
                ("kg → t   x/1000", "x / 1000"),
                ("t → kg   x*1000", "x * 1000"),
                ("W → kW   x/1000", "x / 1000"),
                ("kW → W   x*1000", "x * 1000"),
                ("m/s → km/h   x*3.6", "x * 3.6"),
                ("L/s → m³/h   x*3.6", "x * 3.6"),
                ("4-20mA→0-100%", "(x - 4) / 16 * 100"),
                ("0-10V→0-100%", "x / 10 * 100"),
                ("0-27648→0-100%", "x / 27648 * 100"),
                ("0-27648→-50~150", "x / 27648 * 200 - 50"),
                ("x²   x*x", "x * x"),
                ("x³   x*x*x", "x * x * x"),
                ("√x   Sqrt", "Math.Sqrt(x)"),
                ("|x|   Abs", "Math.Abs(x)"),
                ("ln(x)   Log", "Math.Log(x)"),
                ("lg(x)   Log10", "Math.Log10(x)"),
                ("eˣ   Exp", "Math.Exp(x)"),
                ("多项式  ax²+bx+c", "x * x * 0.01 + x * 1.5 + 0.5"),
                ("线性  y=ax+b", "2.5 * x + 10"),
                ("sin(x)", "Math.Sin(x)")
            };
            for (int i = 0; i < examples.Length; i++)
            {
                int col = i % 4, row = i / 4;
                var btn = new Button
                {
                    Text = examples[i].label,
                    Location = new Point(startX + col * (bw + gapX), startY + row * (bh + gapY)),
                    Size = new Size(bw, bh),
                    Font = new Font("Microsoft YaHei UI", 7.5F),
                    FlatStyle = FlatStyle.Flat,
                    Tag = examples[i].expr
                };
                btn.Click += (s, e) => { txtExpression.Text = ((Button)s).Tag.ToString(); chkCalc.Checked = true; };
                gbCalc.Controls.Add(btn);
            }

            gbCalc.Controls.Add(chkCalc); gbCalc.Controls.Add(txtExpression);
            gbCalc.Controls.Add(lblCalcHint); gbCalc.Controls.Add(lblExamples);
            tab.Controls.Add(gbCalc);
            // 保存配置按钮
            var sp = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.FromArgb(248, 250, 252) };
            var b = new Button { Text = L.GetString("PointEdit_SaveTab"), Size = new Size(80, 28), Dock = DockStyle.Right, BackColor = Color.White, ForeColor = Color.FromArgb(56, 145, 220), FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 9F) };
            b.FlatAppearance.BorderColor = Color.FromArgb(56, 145, 220);
            b.FlatAppearance.BorderSize = 1;
            b.Cursor = Cursors.Hand;
            b.Click += (s2, e2) => SaveToPoint();
            b.MouseEnter += (s2, e2) => { b.BackColor = Color.FromArgb(56, 145, 220); b.ForeColor = Color.White; };
            b.MouseLeave += (s2, e2) => { b.BackColor = Color.White; b.ForeColor = Color.FromArgb(56, 145, 220); };
            sp.Controls.Add(b);
            tab.Controls.Add(sp);
            return tab;
        }

        // ╔══════════════════════════════════════════════════════╗
        // ║  Tab 5: 自定义脚本                                  ║
        // ╚══════════════════════════════════════════════════════╝
        private TabPage CreateScriptTab(LanguageManager L)
        {
            var tab = new TabPage { Text = L.GetString("EdgeTab_Script"), Padding = new Padding(10) };

            var gbScript = NewGroupBox(L.GetString("Edge_Script_Group"), 12, 10, 756, 320);

            chkScript = new CheckBox { Text = L.GetString("Edge_Script_Enable"), Location = new Point(16, 30), Size = new Size(150, 24), Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) };

            // 脚本语言
            var lblLang = new Label { Text = L.GetString("Edge_Script_Lang") + ":", Location = new Point(16, 68), Size = new Size(80, 23), TextAlign = ContentAlignment.MiddleLeft };
            comboScriptLang = new ComboBox { Location = new Point(100, 65), Size = new Size(160, 28), DropDownStyle = ComboBoxStyle.DropDownList, Font = this.Font, Enabled = false };
            comboScriptLang.Items.AddRange(new[] { "Python (.py)", "Lua (.lua)", "Batch (.bat)", "PowerShell (.ps1)" });
            comboScriptLang.SelectedIndex = 0;

            // 脚本路径
            var lblPath = new Label { Text = L.GetString("Edge_Script_Path") + ":", Location = new Point(280, 68), Size = new Size(80, 23), TextAlign = ContentAlignment.MiddleLeft };
            txtScriptPath = new TextBox { Location = new Point(365, 65), Size = new Size(280, 28), Font = this.Font, Enabled = false };
            btnBrowseScript = new Button { Text = "...", Location = new Point(650, 65), Size = new Size(40, 28), Font = this.Font, Enabled = false };
            btnBrowseScript.Click += (s, e) => {
                using (var dlg = new OpenFileDialog { Filter = "脚本文件|*.py;*.lua;*.bat;*.ps1;*.vbs;*.js|所有文件|*.*", Title = L.GetString("Edge_Script_Browse") })
                { if (dlg.ShowDialog() == DialogResult.OK) txtScriptPath.Text = dlg.FileName; }
            };

            // 参数
            var lblArgs = new Label { Text = L.GetString("Edge_Script_Args") + ":", Location = new Point(16, 108), Size = new Size(80, 23), TextAlign = ContentAlignment.MiddleLeft };
            txtScriptArgs = new TextBox { Location = new Point(100, 105), Size = new Size(590, 28), Font = this.Font, Enabled = false };

            // 前/后处理选择
            var lblWhen = new Label { Text = L.GetString("Edge_Script_When") + ":", Location = new Point(16, 148), Size = new Size(80, 23), TextAlign = ContentAlignment.MiddleLeft };
            rdoPreProcess = new RadioButton { Text = L.GetString("Edge_Script_Pre"), Location = new Point(100, 147), Size = new Size(110, 24), Font = this.Font, Enabled = false, Checked = false };
            rdoPostProcess = new RadioButton { Text = L.GetString("Edge_Script_Post"), Location = new Point(220, 147), Size = new Size(120, 24), Font = this.Font, Enabled = false, Checked = true };

            // 测试按钮
            btnTestScript = new Button { Text = L.GetString("Edge_Script_Test"), Location = new Point(16, 190), Size = new Size(120, 34), Font = this.Font, Enabled = false, FlatStyle = FlatStyle.Flat };
            btnTestScript.Click += (s, e) => TestScript();

            var lblTestHint = new Label { Text = L.GetString("Edge_Script_TestHint"), Location = new Point(150, 196), Size = new Size(500, 23), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei UI", 8F) };

            // 脚本说明
            var lblNote = new Label { Text = L.GetString("Edge_Script_Note"), Location = new Point(16, 240), Size = new Size(710, 60), ForeColor = Color.DimGray, Font = new Font("Microsoft YaHei UI", 8F) };

            chkScript.CheckedChanged += (s, e) => { if (_isLoading) return; bool en = chkScript.Checked; comboScriptLang.Enabled = txtScriptPath.Enabled = btnBrowseScript.Enabled = txtScriptArgs.Enabled = rdoPreProcess.Enabled = rdoPostProcess.Enabled = btnTestScript.Enabled = en; };

            gbScript.Controls.Add(chkScript); gbScript.Controls.Add(lblLang); gbScript.Controls.Add(comboScriptLang);
            gbScript.Controls.Add(lblPath); gbScript.Controls.Add(txtScriptPath); gbScript.Controls.Add(btnBrowseScript);
            gbScript.Controls.Add(lblArgs); gbScript.Controls.Add(txtScriptArgs);
            gbScript.Controls.Add(lblWhen); gbScript.Controls.Add(rdoPreProcess); gbScript.Controls.Add(rdoPostProcess);
            gbScript.Controls.Add(btnTestScript); gbScript.Controls.Add(lblTestHint);
            gbScript.Controls.Add(lblNote);
            tab.Controls.Add(gbScript);
            // 保存配置按钮
            var sp = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.FromArgb(248, 250, 252) };
            var b = new Button { Text = L.GetString("PointEdit_SaveTab"), Size = new Size(80, 28), Dock = DockStyle.Right, BackColor = Color.White, ForeColor = Color.FromArgb(56, 145, 220), FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 9F) };
            b.FlatAppearance.BorderColor = Color.FromArgb(56, 145, 220);
            b.FlatAppearance.BorderSize = 1;
            b.Cursor = Cursors.Hand;
            b.Click += (s2, e2) => SaveToPoint();
            b.MouseEnter += (s2, e2) => { b.BackColor = Color.FromArgb(56, 145, 220); b.ForeColor = Color.White; };
            b.MouseLeave += (s2, e2) => { b.BackColor = Color.White; b.ForeColor = Color.FromArgb(56, 145, 220); };
            sp.Controls.Add(b);
            tab.Controls.Add(sp);
            return tab;
        }

        // ╔══════════════════════════════════════════════════════╗
        // ║  Tab 6: 存储策略                                    ║
        // ╚══════════════════════════════════════════════════════╝
        private TabPage CreateStorageTab(LanguageManager L)
        {
            var tab = new TabPage { Text = L.GetString("EdgeTab_Storage"), Padding = new Padding(10) };

            var gbDb = NewGroupBox(L.GetString("Edge_Storage_Db"), 12, 10, 370, 150);
            chkStoreDb = new CheckBox { Text = L.GetString("Edge_Storage_DbWrite"), Location = new Point(16, 30), Size = new Size(180, 24), Font = this.Font, Checked = true };
            var lblPrec = new Label { Text = L.GetString("Edge_Storage_Precision") + ":", Location = new Point(16, 64), Size = new Size(120, 23), TextAlign = ContentAlignment.MiddleLeft };
            numStorePrecision = new NumericUpDown { Location = new Point(140, 62), Size = new Size(80, 28), Minimum = 0, Maximum = 8, Value = 3, Font = this.Font };
            var lblPrecHint = new Label { Text = L.GetString("Edge_Storage_PrecisionHint"), Location = new Point(16, 95), Size = new Size(300, 30), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei UI", 8F) };
            gbDb.Controls.Add(chkStoreDb); gbDb.Controls.Add(lblPrec); gbDb.Controls.Add(numStorePrecision); gbDb.Controls.Add(lblPrecHint);
            tab.Controls.Add(gbDb);

            var gbMqtt = NewGroupBox(L.GetString("Edge_Storage_Mqtt"), 398, 10, 370, 150);
            var lblTopic = new Label { Text = L.GetString("Edge_Storage_Topic") + ":", Location = new Point(16, 30), Size = new Size(120, 23), TextAlign = ContentAlignment.MiddleLeft };
            txtStoreTopic = new TextBox { Location = new Point(16, 58), Size = new Size(330, 28), Font = this.Font };
            var lblTopicHint = new Label { Text = L.GetString("Edge_Storage_TopicHint"), Location = new Point(16, 92), Size = new Size(330, 30), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei UI", 8F) };
            gbMqtt.Controls.Add(lblTopic); gbMqtt.Controls.Add(txtStoreTopic); gbMqtt.Controls.Add(lblTopicHint);
            tab.Controls.Add(gbMqtt);

            var gbChange = NewGroupBox(L.GetString("Edge_Storage_Change"), 12, 175, 756, 120);
            chkStoreChange = new CheckBox { Text = L.GetString("Edge_Storage_ChangeOnly"), Location = new Point(16, 30), Size = new Size(260, 24), Font = this.Font };
            var lblCDead = new Label { Text = L.GetString("Edge_Storage_ChangeDeadband") + ":", Location = new Point(290, 32), Size = new Size(90, 23), TextAlign = ContentAlignment.MiddleLeft };
            numStoreDeadband = new NumericUpDown { Location = new Point(385, 29), Size = new Size(80, 28), Minimum = 0, Maximum = 100, DecimalPlaces = 2, Value = 0.1M, Font = this.Font, Enabled = false };
            var lblPercent = new Label { Text = "%", Location = new Point(470, 32), Size = new Size(20, 23), TextAlign = ContentAlignment.MiddleLeft };
            var lblChangeHint = new Label { Text = L.GetString("Edge_Storage_ChangeHint"), Location = new Point(16, 64), Size = new Size(700, 40), ForeColor = Color.Gray, Font = new Font("Microsoft YaHei UI", 8F) };
            chkStoreChange.CheckedChanged += (s, e) => { if (_isLoading) return; numStoreDeadband.Enabled = chkStoreChange.Checked; };
            gbChange.Controls.Add(chkStoreChange); gbChange.Controls.Add(lblCDead); gbChange.Controls.Add(numStoreDeadband);
            gbChange.Controls.Add(lblPercent); gbChange.Controls.Add(lblChangeHint);
            tab.Controls.Add(gbChange);

            // 保存配置按钮
            var sp = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.FromArgb(248, 250, 252) };
            var b = new Button { Text = L.GetString("PointEdit_SaveTab"), Size = new Size(80, 28), Dock = DockStyle.Right, BackColor = Color.White, ForeColor = Color.FromArgb(56, 145, 220), FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 9F) };
            b.FlatAppearance.BorderColor = Color.FromArgb(56, 145, 220);
            b.FlatAppearance.BorderSize = 1;
            b.Cursor = Cursors.Hand;
            b.Click += (s2, e2) => SaveToPoint();
            b.MouseEnter += (s2, e2) => { b.BackColor = Color.FromArgb(56, 145, 220); b.ForeColor = Color.White; };
            b.MouseLeave += (s2, e2) => { b.BackColor = Color.White; b.ForeColor = Color.FromArgb(56, 145, 220); };
            sp.Controls.Add(b);
            tab.Controls.Add(sp);
            return tab;
        }

        // ═══════ 辅助方法 ═══════

        // ╔══════════════════════════════════════════════════════╗
        // ║  Tab 7: SPC 统计过程控制                            ║
        // ╚══════════════════════════════════════════════════════╝
        private TabPage CreateSpcTab(LanguageManager L)
        {
            var tab = new TabPage { Text = L.GetString("EdgeTab_Spc"), Padding = new Padding(10) };

            // ── GroupBox: 控制限 ──
            var gbCl = NewGroupBox(L.GetString("Edge_Spc_ControlLimits"), 12, 10, 370, 220);

            chkSpc = new CheckBox { Text = L.GetString("Edge_Spc_Enable"), Location = new Point(16, 30), Size = new Size(200, 24), Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) };

            int y = 66, rh = 36;
            void AddClRow(string label, ref NumericUpDown num, decimal defVal)
            {
                var lbl = new Label { Text = label, Location = new Point(16, y), Size = new Size(80, 23), TextAlign = ContentAlignment.MiddleRight };
                num = new NumericUpDown { Location = new Point(105, y - 2), Size = new Size(120, 28), Minimum = -100000, Maximum = 100000, DecimalPlaces = 3, Value = defVal, Font = this.Font, Enabled = false };
                var lblHint = new Label { Location = new Point(235, y), Size = new Size(120, 23), TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Gray };
                gbCl.Controls.Add(lbl); gbCl.Controls.Add(num); gbCl.Controls.Add(lblHint);
                y += rh;
            }

            AddClRow("UCL:", ref numSpcUcl, 0); gbCl.Controls[gbCl.Controls.Count - 1].Text = L.GetString("Edge_Spc_Upper");
            AddClRow("LCL:", ref numSpcLcl, 0); gbCl.Controls[gbCl.Controls.Count - 1].Text = L.GetString("Edge_Spc_Lower");
            y -= 4;
            var lblTarget = new Label { Text = L.GetString("Edge_Spc_Target") + ":", Location = new Point(16, y), Size = new Size(80, 23), TextAlign = ContentAlignment.MiddleRight };
            numSpcTarget = new NumericUpDown { Location = new Point(105, y - 2), Size = new Size(120, 28), Minimum = -100000, Maximum = 100000, DecimalPlaces = 3, Font = this.Font, Enabled = false };
            gbCl.Controls.Add(lblTarget); gbCl.Controls.Add(numSpcTarget);

            chkSpc.CheckedChanged += (s, e) => { if (_isLoading) return; bool en = chkSpc.Checked; numSpcUcl.Enabled = numSpcLcl.Enabled = numSpcTarget.Enabled = numSpcUsl.Enabled = numSpcLsl.Enabled = numSpcSubgroup.Enabled = en; };

            gbCl.Controls.Add(chkSpc);
            tab.Controls.Add(gbCl);

            // ── GroupBox: 规格限 ──
            var gbSl = NewGroupBox(L.GetString("Edge_Spc_SpecLimits"), 398, 10, 370, 220);

            y = 30; rh = 42;
            void AddSlRow(string label, ref NumericUpDown num, decimal defVal)
            {
                var lbl = new Label { Text = label, Location = new Point(16, y), Size = new Size(80, 23), TextAlign = ContentAlignment.MiddleRight };
                num = new NumericUpDown { Location = new Point(105, y - 2), Size = new Size(120, 28), Minimum = -100000, Maximum = 100000, DecimalPlaces = 3, Value = defVal, Font = this.Font, Enabled = false };
                gbSl.Controls.Add(lbl); gbSl.Controls.Add(num);
                y += rh;
            }
            AddSlRow("USL:", ref numSpcUsl, 0);
            AddSlRow("LSL:", ref numSpcLsl, 0);

            var lblSub = new Label { Text = L.GetString("Edge_Spc_Subgroup") + ":", Location = new Point(16, y), Size = new Size(80, 23), TextAlign = ContentAlignment.MiddleRight };
            numSpcSubgroup = new NumericUpDown { Location = new Point(105, y - 2), Size = new Size(80, 28), Minimum = 1, Maximum = 25, Value = 5, Font = this.Font, Enabled = false };
            gbSl.Controls.Add(lblSub); gbSl.Controls.Add(numSpcSubgroup);

            var lblSpcNote = new Label
            {
                Text = L.GetString("Edge_Spc_Note"),
                Location = new Point(16, y + 40),
                Size = new Size(340, 80),
                Font = new Font("Microsoft YaHei UI", 8F),
                ForeColor = Color.DimGray
            };
            gbSl.Controls.Add(lblSpcNote);

            tab.Controls.Add(gbSl);

            // ── GroupBox: 判异规则 ──
            var gbRules = NewGroupBox(L.GetString("Edge_Spc_Rules"), 12, 245, 756, 90);
            var lblRules = new Label
            {
                Text = L.GetString("Edge_Spc_RulesHint"),
                Location = new Point(16, 28),
                Size = new Size(720, 50),
                Font = new Font("Microsoft YaHei UI", 8F),
                ForeColor = Color.DimGray
            };
            gbRules.Controls.Add(lblRules);
            tab.Controls.Add(gbRules);

            // 保存配置按钮
            var sp = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.FromArgb(248, 250, 252) };
            var b = new Button { Text = L.GetString("PointEdit_SaveTab"), Size = new Size(80, 28), Dock = DockStyle.Right, BackColor = Color.White, ForeColor = Color.FromArgb(56, 145, 220), FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 9F) };
            b.FlatAppearance.BorderColor = Color.FromArgb(56, 145, 220);
            b.FlatAppearance.BorderSize = 1;
            b.Cursor = Cursors.Hand;
            b.Click += (s2, e2) => SaveToPoint();
            b.MouseEnter += (s2, e2) => { b.BackColor = Color.FromArgb(56, 145, 220); b.ForeColor = Color.White; };
            b.MouseLeave += (s2, e2) => { b.BackColor = Color.White; b.ForeColor = Color.FromArgb(56, 145, 220); };
            sp.Controls.Add(b);
            tab.Controls.Add(sp);
            return tab;
        }

        private static GroupBox NewGroupBox(string title, int x, int y, int w, int h)
        {
            return new GroupBox
            {
                Text = title, Location = new Point(x, y), Size = new Size(w, h),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular),
                Padding = new Padding(6)
            };
        }

        private static Label AddLabeledControl(Control parent, string text, int x, int y, int w)
        {
            var lbl = new Label { Text = text, Location = new Point(x, y), Size = new Size(w, 23), TextAlign = ContentAlignment.MiddleRight };
            parent.Controls.Add(lbl);
            return lbl;
        }

        private void OnDataTypeChanged(object sender, EventArgs e)
        {
            string dtype = comboDataType.SelectedItem?.ToString() ?? "int16";
            int defaultLen = GetDefaultLength(dtype);
            if (sender != null || numLength.Value == 1 || _original == null)
                numLength.Value = defaultLen;
            bool isString = (dtype == "string");
            numLength.Enabled = isString;
            numLength.BackColor = isString ? SystemColors.Window : SystemColors.Control;
            bool needsByteOrder = (dtype != "string" && dtype != "byte" && dtype != "bool" && dtype != "coil" && defaultLen >= 2);
            comboByteOrder.Enabled = needsByteOrder;
            comboByteOrder.BackColor = needsByteOrder ? SystemColors.Window : SystemColors.Control;
        }

        // ── 语义标签 自动生成 ──

        private void AutoGenTags()
        {
            string varName = txtName.Text.Trim();
            if (string.IsNullOrEmpty(varName)) return;

            if (_parentDevice != null)
            {
                string deviceTagPathCn = _parentDevice.TagPathCn;
                string deviceName = _parentDevice.Name;

                _txtTagZh.Text = !string.IsNullOrEmpty(deviceTagPathCn)
                    ? deviceTagPathCn + "/" + varName
                    : deviceName + "/" + varName;
            }
            else
            {
                _txtTagZh.Text = varName;
            }
        }

        private void UpdateTagLastSegment()
        {
            string varName = txtName.Text.Trim();
            if (string.IsNullOrEmpty(varName)) return;

            string currentTagCn = _txtTagZh.Text;
            if (string.IsNullOrEmpty(currentTagCn)) { AutoGenTags(); return; }
            int lastSlash = currentTagCn.LastIndexOf('/');
            if (lastSlash >= 0)
                _txtTagZh.Text = currentTagCn.Substring(0, lastSlash + 1) + varName;
            else
                _txtTagZh.Text = varName;
        }

        private string TranslateAndSlug(string chineseSegment)
        {
            string translated = IndustrialVocabulary.TranslateCompound(chineseSegment);
            return Slugify(translated);
        }

        private static string Slugify(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in name.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == '/' || c == '-') sb.Append(c);
                else sb.Append('_');
            }
            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "_+", "_").Trim('_');
        }

        private static int GetDefaultLength(string dataType)
        {
            switch (dataType)
            {
                case "byte": case "int16": case "uint16": case "word": case "bool": case "coil": return 1;
                case "int32": case "uint32": case "dword": case "float": case "real": return 2;
                case "int64": case "uint64": case "double": return 4;
                case "string": return 1;
                default: return 1;
            }
        }

        private void LoadPoint(DataPoint point)
        {
            Logger.Info($"[PointEdit.LoadPoint] rounding={point.RoundingEnabled} filter={point.FilterEnabled} clean={point.CleanEnabled} calc={point.CalculationEnabled}");
            txtName.Text = point.Name;
            // v2.0: NameEn 已废弃，不再加载
            txtAddress.Text = point.Address;
            for (int i = 0; i < comboDataType.Items.Count; i++)
                if (comboDataType.Items[i].ToString() == point.DataType) { comboDataType.SelectedIndex = i; break; }
            comboUnit.Text = point.Unit;
            numScale.Value = (decimal)point.ScaleFactor;
            numOffset.Value = (decimal)point.Offset;
            numLength.Value = point.Length > 0 ? point.Length : GetDefaultLength(point.DataType);
            comboByteOrder.SelectedIndex = (int)point.ByteOrder;
            chkEnabled.Checked = point.IsActive;

            // 修约
            chkRounding.Checked = point.RoundingEnabled;
            if (point.RoundingEnabled) { comboRoundMode.SelectedIndex = point.RoundingMode; numRoundDecimals.Value = point.RoundingDecimals; comboRoundMode.Enabled = numRoundDecimals.Enabled = true; }
            // 滤波
            chkFilter.Checked = point.FilterEnabled;
            if (point.FilterEnabled) { comboFilterMode.SelectedIndex = point.FilterMode; numFilterWindow.Value = point.FilterWindow; numFilterAlpha.Value = (decimal)point.FilterAlpha; comboFilterMode.Enabled = numFilterWindow.Enabled = true; }
            // 信号变换
            chkSqrt.Checked = point.SquareRootEnabled;
            chkAbs.Checked = point.AbsValueEnabled;
            chkRate.Checked = point.RateOfChangeEnabled;
            // 清洗
            chkClean.Checked = point.CleanEnabled;
            if (point.CleanEnabled) { chkDeadBand.Enabled = chkClip.Enabled = chkOutlier.Enabled = true; }
            chkDeadBand.Checked = point.DeadBandEnabled;
            if (point.DeadBandEnabled) { numDeadBand.Value = (decimal)point.DeadBand; numDeadBand.Enabled = true; }
            chkClip.Checked = point.ClipEnabled;
            if (point.ClipEnabled) { numClipMin.Value = (decimal)point.ClipMin; numClipMax.Value = (decimal)point.ClipMax; numClipMin.Enabled = numClipMax.Enabled = true; }
            chkOutlier.Checked = point.OutlierEnabled;
            if (point.OutlierEnabled) { numSigma.Value = (decimal)point.SigmaThreshold; numSigma.Enabled = true; }
            // 新增清洗
            chkNanFilter.Checked = point.NanFilterEnabled;
            if (point.NanFilterEnabled) { chkNanNaN.Checked = point.NanFilterNaN; chkNanInf.Checked = point.NanFilterInf; chkNanNeg.Checked = point.NanFilterNegative; numNanReplacement.Value = (decimal)point.NanFilterReplacement; chkNanNaN.Enabled = chkNanInf.Enabled = chkNanNeg.Enabled = numNanReplacement.Enabled = true; }
            chkSpike.Checked = point.SpikeEnabled;
            if (point.SpikeEnabled) { numSpikeWindow.Value = point.SpikeWindow; numSpikeThresh.Value = (decimal)point.SpikeThreshold; numSpikeWindow.Enabled = numSpikeThresh.Enabled = true; }
            chkRocLimit.Checked = point.RocLimitEnabled;
            if (point.RocLimitEnabled) { numRocMax.Value = (decimal)point.RocLimitMax; numRocMax.Enabled = true; }
            chkFreeze.Checked = point.FreezeEnabled;
            if (point.FreezeEnabled) { numFreezeWindow.Value = point.FreezeWindow; numFreezeWindow.Enabled = true; }
            chkIqr.Checked = point.IqrEnabled;
            if (point.IqrEnabled) { numIqrMult.Value = (decimal)point.IqrMultiplier; numIqrMult.Enabled = true; }
            chkRange.Checked = point.RangeEnabled;
            if (point.RangeEnabled) { numRangeMin.Value = (decimal)point.RangeMin; numRangeMax.Value = (decimal)point.RangeMax; numRangeMin.Enabled = numRangeMax.Enabled = true; }
            // 报警
            chkAlarm.Checked = point.AlarmEnabled;
            if (point.AlarmEnabled) { numAlarmDelay.Enabled = chkHH.Enabled = chkH.Enabled = chkL.Enabled = chkLL.Enabled = true; numAlarmDelay.Value = point.AlarmDelay; }
            chkHH.Checked = point.AlarmHH_Enabled; if (point.AlarmHH_Enabled) { numHH.Value = (decimal)point.AlarmHH; numHH.Enabled = true; }
            chkH.Checked = point.AlarmH_Enabled; if (point.AlarmH_Enabled) { numH.Value = (decimal)point.AlarmH; numH.Enabled = true; }
            chkL.Checked = point.AlarmL_Enabled; if (point.AlarmL_Enabled) { numL.Value = (decimal)point.AlarmL; numL.Enabled = true; }
            chkLL.Checked = point.AlarmLL_Enabled; if (point.AlarmLL_Enabled) { numLL.Value = (decimal)point.AlarmLL; numLL.Enabled = true; }
            // 公式
            chkCalc.Checked = point.CalculationEnabled;
            if (point.CalculationEnabled) { txtExpression.Text = point.CalculationExpression; txtExpression.Enabled = true; }
            // 脚本
            chkScript.Checked = point.ScriptEnabled;
            if (point.ScriptEnabled)
            {
                comboScriptLang.SelectedIndex = Math.Max(0, Math.Min(3, point.ScriptLanguage == "python" ? 0 : point.ScriptLanguage == "lua" ? 1 : point.ScriptLanguage == "bat" ? 2 : 3));
                txtScriptPath.Text = point.ScriptPath; txtScriptArgs.Text = point.ScriptArgs;
                rdoPreProcess.Checked = !point.ScriptPostProcess; rdoPostProcess.Checked = point.ScriptPostProcess;
                comboScriptLang.Enabled = txtScriptPath.Enabled = btnBrowseScript.Enabled = txtScriptArgs.Enabled = rdoPreProcess.Enabled = rdoPostProcess.Enabled = btnTestScript.Enabled = true;
            }
            // 存储
            chkStoreDb.Checked = point.StorageDbWriteEnabled;
            txtStoreTopic.Text = point.StorageCustomTopic;
            chkStoreChange.Checked = point.StorageChangeOnly;
            numStoreDeadband.Value = (decimal)point.StorageChangeDeadband;
            numStorePrecision.Value = point.StoragePrecision;

            // SPC
            chkSpc.Checked = point.SpcEnabled;
            if (point.SpcEnabled) { numSpcUcl.Enabled = numSpcLcl.Enabled = numSpcTarget.Enabled = numSpcUsl.Enabled = numSpcLsl.Enabled = numSpcSubgroup.Enabled = true; }
            numSpcUcl.Value = (decimal)point.SpcUcl; numSpcLcl.Value = (decimal)point.SpcLcl;
            numSpcUsl.Value = (decimal)point.SpcUsl; numSpcLsl.Value = (decimal)point.SpcLsl;
            numSpcTarget.Value = (decimal)point.SpcTarget; numSpcSubgroup.Value = point.SpcSubgroupSize;

            // 语义标签
            _txtTagZh.Text = point.TagCn;

            // v2.0: 新增变量自动生成中文标签（编辑模式不覆盖已保存的标签）
            if (!_isEdit && string.IsNullOrEmpty(_txtTagZh.Text) && !string.IsNullOrEmpty(txtName.Text))
            {
                AutoGenTags();
            }
        }

        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            var tab = (TabControl)sender;
            var tabPage = tab.TabPages[e.Index];
            var rect = tab.GetTabRect(e.Index);
            bool selected = e.Index == tab.SelectedIndex;

            // 背景
            using (var bg = new SolidBrush(selected ? Color.White : Color.FromArgb(241, 245, 249)))
                e.Graphics.FillRectangle(bg, rect);

            // 文字
            Color textColor = selected ? Color.FromArgb(30, 41, 59) : Color.FromArgb(100, 116, 139);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var textBrush = new SolidBrush(textColor))
                e.Graphics.DrawString(tabPage.Text, tab.Font, textBrush, rect, sf);

            // 选中态底部指示条
            if (selected)
            {
                using (var accent = new SolidBrush(Color.FromArgb(56, 145, 220)))
                    e.Graphics.FillRectangle(accent, rect.X + 12, rect.Bottom - 3, rect.Width - 24, 3);
            }
        }

        private void DisableAllCleaningControls()
        {
            numDeadBand.Enabled = numClipMin.Enabled = numClipMax.Enabled = numSigma.Enabled = false;
            numNanReplacement.Enabled = numFreezeWindow.Enabled = numSpikeWindow.Enabled = false;
            numSpikeThresh.Enabled = numRocMax.Enabled = numIqrMult.Enabled = numRangeMin.Enabled = numRangeMax.Enabled = false;
            chkNanNaN.Enabled = chkNanInf.Enabled = chkNanNeg.Enabled = false;
        }

        private void SaveToPoint()
        {
            var point = _original;
            if (point == null) { point = new DataPoint(); _original = point; }

            point.Name = txtName.Text.Trim();
            // v2.0: NameEn 已废弃，不再保存
            point.Address = txtAddress.Text.Trim();
            point.DataType = comboDataType.SelectedItem?.ToString() ?? "int16";
            point.Unit = comboUnit.Text.Trim();
            point.ScaleFactor = (double)numScale.Value;
            point.Offset = (double)numOffset.Value;
            point.IsActive = chkEnabled.Checked;
            point.ByteOrder = (ByteOrder)comboByteOrder.SelectedIndex;
            point.Length = (point.DataType == "string") ? (int)numLength.Value : 0;

            point.RoundingEnabled = chkRounding.Checked;
            point.RoundingMode = comboRoundMode.SelectedIndex;
            point.RoundingDecimals = (int)numRoundDecimals.Value;
            point.FilterEnabled = chkFilter.Checked;
            point.FilterMode = comboFilterMode.SelectedIndex;
            point.FilterWindow = (int)numFilterWindow.Value;
            point.FilterAlpha = (double)numFilterAlpha.Value;
            point.SquareRootEnabled = chkSqrt.Checked;
            point.AbsValueEnabled = chkAbs.Checked;
            point.RateOfChangeEnabled = chkRate.Checked;
            point.CleanEnabled = chkClean.Checked;
            point.DeadBandEnabled = chkDeadBand.Checked;
            point.DeadBand = (double)numDeadBand.Value;
            point.ClipEnabled = chkClip.Checked;
            point.ClipMin = (double)numClipMin.Value;
            point.ClipMax = (double)numClipMax.Value;
            point.OutlierEnabled = chkOutlier.Checked;
            point.SigmaThreshold = (double)numSigma.Value;
            point.NanFilterEnabled = chkNanFilter.Checked;
            point.NanFilterNaN = chkNanNaN.Checked;
            point.NanFilterInf = chkNanInf.Checked;
            point.NanFilterNegative = chkNanNeg.Checked;
            point.NanFilterReplacement = (double)numNanReplacement.Value;
            point.SpikeEnabled = chkSpike.Checked;
            point.SpikeWindow = (int)numSpikeWindow.Value;
            point.SpikeThreshold = (double)numSpikeThresh.Value;
            point.RocLimitEnabled = chkRocLimit.Checked;
            point.RocLimitMax = (double)numRocMax.Value;
            point.FreezeEnabled = chkFreeze.Checked;
            point.FreezeWindow = (int)numFreezeWindow.Value;
            point.IqrEnabled = chkIqr.Checked;
            point.IqrMultiplier = (double)numIqrMult.Value;
            point.RangeEnabled = chkRange.Checked;
            point.RangeMin = (double)numRangeMin.Value;
            point.RangeMax = (double)numRangeMax.Value;
            point.AlarmEnabled = chkAlarm.Checked;
            point.AlarmDelay = (int)numAlarmDelay.Value;
            point.AlarmHH_Enabled = chkHH.Checked; point.AlarmHH = (double)numHH.Value;
            point.AlarmH_Enabled = chkH.Checked; point.AlarmH = (double)numH.Value;
            point.AlarmL_Enabled = chkL.Checked; point.AlarmL = (double)numL.Value;
            point.AlarmLL_Enabled = chkLL.Checked; point.AlarmLL = (double)numLL.Value;
            point.CalculationEnabled = chkCalc.Checked;
            point.CalculationExpression = txtExpression.Text.Trim();
            point.ScriptEnabled = chkScript.Checked;
            point.ScriptLanguage = comboScriptLang.SelectedIndex == 0 ? "python" : comboScriptLang.SelectedIndex == 1 ? "lua" : comboScriptLang.SelectedIndex == 2 ? "bat" : "ps1";
            point.ScriptPath = txtScriptPath.Text.Trim();
            point.ScriptArgs = txtScriptArgs.Text.Trim();
            point.ScriptPostProcess = rdoPostProcess.Checked;
            point.StorageDbWriteEnabled = chkStoreDb.Checked;
            point.StorageCustomTopic = txtStoreTopic.Text.Trim();
            point.StorageChangeOnly = chkStoreChange.Checked;
            point.StorageChangeDeadband = (double)numStoreDeadband.Value;
            point.StoragePrecision = (int)numStorePrecision.Value;
            point.SpcEnabled = chkSpc.Checked;
            point.SpcUcl = (double)numSpcUcl.Value;
            point.SpcLcl = (double)numSpcLcl.Value;
            point.SpcUsl = (double)numSpcUsl.Value;
            point.SpcLsl = (double)numSpcLsl.Value;
            point.SpcTarget = (double)numSpcTarget.Value;
            point.SpcSubgroupSize = (int)numSpcSubgroup.Value;

            // 语义标签
            point.TagCn = _txtTagZh.Text.Trim();
            point.OutputTagCn = true;

            IsSaved = true;
            DataPoint = _original;

            // 立即落盘 + 热生效（如果父设备已传递）
            if (_parentDevice != null && !string.IsNullOrEmpty(_parentDevice.Id))
            {
                // 确保 _parentDevice.DataPoints 非空
                if (_parentDevice.DataPoints == null)
                    _parentDevice.DataPoints = new System.Collections.Generic.List<DataPoint>();

                // 同步 _workingPoints 的修改回 _parentDevice.DataPoints（否则保存和热更新都是旧数据）
                int idx = _parentDevice.DataPoints.FindIndex(p => p.Name == point.Name);
                if (idx >= 0)
                    _parentDevice.DataPoints[idx] = point;
                else
                    _parentDevice.DataPoints.Add(point);

                try
                {

                    ConfigService.Instance.SaveDevices(new System.Collections.Generic.List<DeviceConfig> { _parentDevice });
                    DataProcessor.Instance.UnregisterDevicePoints(_parentDevice.Id);
                    DataProcessor.Instance.RegisterDevicePoints(_parentDevice);
                    Logger.Info(string.Format("[Apply] {0} 配置已保存并热生效", _parentDevice.Name));
                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format("[Apply] {0} 保存失败: {1}", _parentDevice.Name, ex.Message));
                }
            }

            Logger.Info($"[PointEdit.SaveToPoint] rounding={point.RoundingEnabled} filter={point.FilterEnabled} clean={point.CleanEnabled} calc={point.CalculationEnabled}");
        }

        private void Save()
        {
            var L = LanguageManager.Instance;
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show(L.GetString("Msg_Error_NameEmpty"), L.GetString("Msg_Info"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SaveToPoint();
            DataPoint = _original ?? new DataPoint();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void TestScript()
        {
            var L = LanguageManager.Instance;
            string path = txtScriptPath.Text.Trim();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show(L.GetString("Edge_Script_NotFound"), L.GetString("Msg_Info"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                string lang = comboScriptLang.SelectedItem?.ToString() ?? "";
                string args = txtScriptArgs.Text.Trim();
                var psi = new ProcessStartInfo();
                if (lang.Contains("Python")) { psi.FileName = "python"; psi.Arguments = "\"" + path + "\" " + args; }
                else if (lang.Contains("Lua")) { psi.FileName = "lua"; psi.Arguments = "\"" + path + "\" " + args; }
                else if (lang.Contains("PowerShell")) { psi.FileName = "powershell"; psi.Arguments = "-ExecutionPolicy Bypass -File \"" + path + "\" " + args; }
                else { psi.FileName = path; psi.Arguments = args; }
                psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true; psi.CreateNoWindow = true;
                var proc = Process.Start(psi);
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit(5000);
                string result = "Exit code: " + proc.ExitCode + "\n" + (string.IsNullOrEmpty(output) ? "" : output) + (string.IsNullOrEmpty(error) ? "" : "\nSTDERR:\n" + error);
                MessageBox.Show(result, L.GetString("Edge_Script_TestResult"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(L.GetString("Edge_Script_Error") + ": " + ex.Message, L.GetString("Msg_Info"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
