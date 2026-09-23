using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using IndustrialDataCollection.Services;

namespace IndustrialDataCollection.Forms
{
    /// <summary>
    /// 语义节点选择对话框 — 显示语义树，用户选择目标节点后返回节点ID
    /// </summary>
    public class SemanticNodePickerForm : Form
    {
        private TreeView _tree;
        private Button _btnOk;
        private Button _btnCancel;
        private Label _lblInfo;

        public string SelectedNodeId { get; private set; }

        public SemanticNodePickerForm()
        {
            Text = "选择目标节点";
            Size = new Size(460, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Icon = Program.AppIcon;
            Font = new Font("Microsoft YaHei", 9f);

            _lblInfo = new Label
            {
                Text = "请选择要移动到的目标节点：",
                Location = new Point(16, 14),
                AutoSize = true,
                Font = Font
            };

            _tree = new TreeView
            {
                Location = new Point(16, 40),
                Size = new Size(412, 370),
                Font = Font,
                HideSelection = false
            };
            _tree.AfterSelect += (s, e) => { _btnOk.Enabled = _tree.SelectedNode != null; };

            _btnOk = new Button
            {
                Text = "确定",
                Location = new Point(260, 422),
                Size = new Size(80, 32),
                Font = Font,
                Enabled = false,
                DialogResult = DialogResult.OK
            };

            _btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(348, 422),
                Size = new Size(80, 32),
                Font = Font,
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[] { _lblInfo, _tree, _btnOk, _btnCancel });
            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            Load += (s, e) => BuildTree();
        }

        private void BuildTree()
        {
            _tree.BeginUpdate();
            _tree.Nodes.Clear();
            try
            {
                var roots = SemanticService.Instance.GetRootNodes();
                foreach (var root in roots.OrderBy(n => n.SortOrder).ThenBy(n => n.Name))
                {
                    var node = new TreeNode(string.Format("{0} {1}", GetIcon(root.Kind), root.Name))
                    {
                        Tag = root.Id
                    };
                    _tree.Nodes.Add(node);
                    PopulateChildren(node, root.Id);
                    node.Expand();
                }
            }
            catch (Exception ex)
            {
                _tree.Nodes.Add("加载失败: " + ex.Message);
            }
            _tree.EndUpdate();
        }

        private void PopulateChildren(TreeNode parentNode, string parentId)
        {
            var children = SemanticService.Instance.GetChildren(parentId);
            foreach (var child in children.OrderBy(n => n.SortOrder).ThenBy(n => n.Name))
            {
                var childNode = new TreeNode(string.Format("{0} {1}", GetIcon(child.Kind), child.Name))
                {
                    Tag = child.Id
                };
                parentNode.Nodes.Add(childNode);
                PopulateChildren(childNode, child.Id);
            }
        }

        private string GetIcon(string kind)
        {
            switch (kind)
            {
                case "Company": return "\U0001F3E2";
                case "Division":
                case "Factory":
                case "Workshop": return "\U0001F3ED";
                case "Zone": return "\U0001F4CD";
                case "ProductionLine":
                case "WorkStation":
                case "Equipment": return "\u2699\uFE0F";
                case "Variable": return "\U0001F4CA";
                case "Datasource": return "\U0001F4BE";
                case "DataTable": return "\U0001F4CB";
                case "DataField": return "\U0001F4CC";
                case "Custom": return "\U0001F4C1";
                default: return "\u25CF";
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                if (_tree.SelectedNode != null)
                    SelectedNodeId = _tree.SelectedNode.Tag as string;
                else
                    e.Cancel = true;
            }
            base.OnFormClosing(e);
        }
    }
}
