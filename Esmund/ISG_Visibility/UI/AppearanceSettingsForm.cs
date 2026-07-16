using System;
using System.Drawing;
using System.Windows.Forms;

namespace VisTog.UI
{
    internal sealed class AppearanceSettingsForm : Form
    {
        private readonly ComboBox _fontFamily;
        private readonly NumericUpDown _fontSize;
        private readonly ComboBox _fontWeight;
        private readonly Button _backColor;
        private readonly Button _headerColor;
        private readonly Button _accentColor;
        private readonly Button _textColor;
        private readonly Button _mutedColor;
        private readonly Button _borderColor;
        private readonly Button _btnBorderColor;
        private VisTogUiSettings _draft;

        public AppearanceSettingsForm(VisTogUiSettings current)
        {
            _draft = Clone(current);

            Text = "ISG Visibility appearance";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(280, 340);
            BackColor = VisTogUiTheme.Back;
            ForeColor = VisTogUiTheme.Text;
            Font = VisTogUiTheme.UiFont;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 10,
                Padding = new Padding(10),
                BackColor = VisTogUiTheme.Back
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));

            int row = 0;
            _fontFamily = AddCombo(layout, row++, "Font", new[] { "Calibri", "Segoe UI", "Tahoma", "Arial", "Verdana", "Consolas" }, _draft.fontFamily);
            _fontSize = AddNumeric(layout, row++, "Size", (decimal)_draft.fontSize, 7M, 18M, 0.25M);
            _fontWeight = AddCombo(layout, row++, "Weight", new[] { "400", "600", "700" }, _draft.fontWeight ?? "400");
            _backColor = AddColor(layout, row++, "Background", _draft.colors.back);
            _headerColor = AddColor(layout, row++, "Button", _draft.colors.header);
            _accentColor = AddColor(layout, row++, "Accent", _draft.colors.accent);
            _textColor = AddColor(layout, row++, "Text", _draft.colors.text);
            _mutedColor = AddColor(layout, row++, "Muted", _draft.colors.muted);
            _borderColor = AddColor(layout, row++, "Outer border", _draft.colors.border);
            _btnBorderColor = AddColor(layout, row++, "Btn border", _draft.colors.btnBorder);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8, 4, 8, 4),
                BackColor = VisTogUiTheme.Back
            };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 70, Height = 24 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 70, Height = 24 };
            ok.Click += (_, __) => ApplyDraftFromControls();
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);

            Controls.Add(layout);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        public VisTogUiSettings ResultSettings => _draft;

        private void ApplyDraftFromControls()
        {
            _draft.fontFamily = _fontFamily.SelectedItem as string ?? "Calibri";
            _draft.fontSize = (float)_fontSize.Value;
            _draft.fontWeight = _fontWeight.SelectedItem as string ?? "400";
            _draft.colors.back = ColorToHex(_backColor.BackColor);
            _draft.colors.header = ColorToHex(_headerColor.BackColor);
            _draft.colors.accent = ColorToHex(_accentColor.BackColor);
            _draft.colors.text = ColorToHex(_textColor.BackColor);
            _draft.colors.muted = ColorToHex(_mutedColor.BackColor);
            _draft.colors.border = ColorToHex(_borderColor.BackColor);
            _draft.colors.btnBorder = ColorToHex(_btnBorderColor.BackColor);
        }

        private static ComboBox AddCombo(TableLayoutPanel layout, int row, string label, string[] items, string selected)
        {
            layout.Controls.Add(MakeLabel(label), 0, row);
            var box = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat
            };
            box.Items.AddRange(items);
            int index = Array.FindIndex(items, i => string.Equals(i, selected, StringComparison.OrdinalIgnoreCase));
            box.SelectedIndex = index >= 0 ? index : 0;
            layout.Controls.Add(box, 1, row);
            return box;
        }

        private static NumericUpDown AddNumeric(TableLayoutPanel layout, int row, string label, decimal value, decimal min, decimal max, decimal increment)
        {
            layout.Controls.Add(MakeLabel(label), 0, row);
            var box = new NumericUpDown
            {
                DecimalPlaces = 2,
                Minimum = min,
                Maximum = max,
                Increment = increment,
                Value = Math.Min(max, Math.Max(min, value)),
                Dock = DockStyle.Fill
            };
            layout.Controls.Add(box, 1, row);
            return box;
        }

        private Button AddColor(TableLayoutPanel layout, int row, string label, string hex)
        {
            layout.Controls.Add(MakeLabel(label), 0, row);
            Color color = VisTogUiTheme.ParseColor(hex, VisTogUiTheme.Header);
            var button = new Button
            {
                Dock = DockStyle.Fill,
                BackColor = color,
                FlatStyle = FlatStyle.Flat,
                Text = hex,
                ForeColor = ContrastText(color)
            };
            button.FlatAppearance.BorderSize = 1;
            button.Click += (_, __) =>
            {
                using (var dialog = new ColorDialog { Color = button.BackColor, FullOpen = true })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        button.BackColor = dialog.Color;
                        button.Text = ColorToHex(dialog.Color);
                        button.ForeColor = ContrastText(dialog.Color);
                    }
                }
            };
            layout.Controls.Add(button, 1, row);
            return button;
        }

        private static Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = VisTogUiTheme.Text
            };
        }

        private static string ColorToHex(Color color)
        {
            return "#" + color.R.ToString("x2") + color.G.ToString("x2") + color.B.ToString("x2");
        }

        private static Color ContrastText(Color background)
        {
            int luminance = (background.R * 299 + background.G * 587 + background.B * 114) / 1000;
            return luminance > 140 ? Color.Black : Color.White;
        }

        private static VisTogUiSettings Clone(VisTogUiSettings source)
        {
            VisTogUiSettings defaults = VisTogUiSettings.FromLayoutExportDefaults();
            if (source == null)
            {
                return defaults;
            }

            return new VisTogUiSettings
            {
                width = source.width > 0 ? source.width : defaults.width,
                height = source.height,
                pad = source.pad,
                gap = source.gap,
                btnH = source.btnH > 0 ? source.btnH : defaults.btnH,
                borderW = source.borderW,
                fontFamily = source.fontFamily ?? defaults.fontFamily,
                fontSize = source.fontSize > 0 ? source.fontSize : defaults.fontSize,
                fontWeight = source.fontWeight ?? defaults.fontWeight,
                sharkShowIcon = source.sharkShowIcon,
                sharkIconOnly = source.sharkIconOnly,
                layout = null,
                colors = new VisTogUiColors
                {
                    back = source.colors?.back ?? defaults.colors.back,
                    header = source.colors?.header ?? defaults.colors.header,
                    accent = source.colors?.accent ?? defaults.colors.accent,
                    text = source.colors?.text ?? defaults.colors.text,
                    muted = source.colors?.muted ?? defaults.colors.muted,
                    border = source.colors?.border ?? defaults.colors.border,
                    btnBorder = source.colors?.btnBorder ?? defaults.colors.btnBorder
                }
            };
        }
    }
}
