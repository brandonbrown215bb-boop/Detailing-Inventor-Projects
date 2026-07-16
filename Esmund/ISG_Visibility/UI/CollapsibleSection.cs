using System;
using System.Drawing;
using System.Windows.Forms;

namespace VisTog.UI
{
    internal sealed class CollapsibleSection : UserControl
    {
        private readonly string _title;
        private readonly Panel _content;
        private readonly Label _header;
        private readonly int _rowH;
        private bool _expanded;

        public CollapsibleSection(string title)
        {
            _title = title;
            _rowH = VisTogPanel.GetPartRowHeight();
            Dock = DockStyle.Top;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Margin = new Padding(0, 0, 0, 4);
            MinimumSize = new Size(0, _rowH);
            BackColor = VisTogUiTheme.Panel;

            _header = new Label
            {
                Text = "▶ " + title,
                Dock = DockStyle.Top,
                Height = _rowH,
                Padding = new Padding(4, 0, 2, 0),
                BackColor = VisTogUiTheme.Header,
                ForeColor = VisTogUiTheme.Text,
                Font = VisTogUiTheme.PartFont,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            _header.Click += (_, __) => Toggle();

            _content = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(1, 1, 1, 2),
                Visible = false,
                BackColor = VisTogUiTheme.Panel
            };

            Controls.Add(_content);
            Controls.Add(_header);
        }

        public void AddControl(Control control)
        {
            control.Dock = DockStyle.Top;
            control.Height = _rowH;
            control.Margin = new Padding(0, 0, 0, 1);
            // Dock.Top: last added sits above earlier ones — insert at 0 so order is top-to-bottom.
            _content.Controls.Add(control);
            _content.Controls.SetChildIndex(control, 0);
        }

        private void Toggle()
        {
            _expanded = !_expanded;
            _content.Visible = _expanded;
            _header.Text = (_expanded ? "▼ " : "▶ ") + _title;
            PerformLayout();
            Parent?.PerformLayout();
        }
    }
}
