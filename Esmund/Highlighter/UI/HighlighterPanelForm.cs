using System;
using System.Drawing;
using System.Windows.Forms;
using InvApp = Inventor.Application;
using Highlighter.Core;

namespace Highlighter.UI
{
    internal sealed class HighlighterPanelForm : Form
    {
        private readonly HighlightController _controller;
        private readonly Button[] _typeButtons;
        private readonly Button[] _colorButtons;
        private readonly Button _allModeBtn;
        private readonly Button _selectiveModeBtn;
        private readonly Button _normalBtn;
        private readonly ToolTip _tips = new ToolTip();
        private readonly HighlightPartType[] _types =
        {
            HighlightPartType.WallSkin,
            HighlightPartType.WallLiner,
            HighlightPartType.RoofSkin,
            HighlightPartType.RoofLiner,
            HighlightPartType.BaseFloor,
            HighlightPartType.BaseSubfloor
        };

        private bool _allowClose;

        public HighlighterPanelForm(InvApp app, HighlightController controller)
        {
            _controller = controller;

            Text = "Highlighter";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            ClientSize = new Size(260, 348);
            BackColor = Color.FromArgb(245, 245, 247);
            Font = new Font("Segoe UI", 9f);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 9,
                Padding = new Padding(10),
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // mode
            for (int i = 0; i < 6; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // normal
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var modeRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 6),
            };
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _allModeBtn = MakeModeButton("All");
            _allModeBtn.Click += (_, __) =>
            {
                _controller.SetScopeMode(HighlightScopeMode.All);
                SyncModeButtons();
                SyncTypeButtonLooks();
            };
            _selectiveModeBtn = MakeModeButton("Selective");
            _selectiveModeBtn.Click += (_, __) =>
            {
                _controller.SetScopeMode(HighlightScopeMode.Selective);
                SyncModeButtons();
                SyncTypeButtonLooks();
            };
            modeRow.Controls.Add(_allModeBtn, 0, 0);
            modeRow.Controls.Add(_selectiveModeBtn, 1, 0);
            layout.SetColumnSpan(modeRow, 2);
            layout.Controls.Add(modeRow, 0, 0);

            _typeButtons = new Button[_types.Length];
            _colorButtons = new Button[_types.Length];
            for (int i = 0; i < _types.Length; i++)
            {
                HighlightPartType type = _types[i];
                Button typeBtn = new Button
                {
                    Text = HighlightTypeCatalog.DisplayName(type),
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 0, 4, 4),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(8, 0, 0, 0),
                    Tag = type
                };
                typeBtn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 186);
                typeBtn.Click += TypeButton_Click;
                _typeButtons[i] = typeBtn;
                layout.Controls.Add(typeBtn, 0, i + 1);

                Button colorBtn = new Button
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 0, 0, 4),
                    FlatStyle = FlatStyle.Flat,
                    Tag = type,
                    Cursor = Cursors.Hand,
                    TabStop = true
                };
                colorBtn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 85);
                colorBtn.FlatAppearance.BorderSize = 2;
                colorBtn.Click += ColorButton_Click;
                colorBtn.MouseUp += ColorButton_MouseUp;
                _colorButtons[i] = colorBtn;
                layout.Controls.Add(colorBtn, 1, i + 1);
            }

            _normalBtn = new Button
            {
                Text = "Normal",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
            };
            _normalBtn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 186);
            _normalBtn.Click += (_, __) =>
            {
                _controller.RestoreNormal();
                SyncModeButtons();
                SyncTypeButtonLooks();
                SyncColorButtons();
            };
            layout.SetColumnSpan(_normalBtn, 2);
            layout.Controls.Add(_normalBtn, 0, 7);

            Controls.Add(layout);
            FormClosing += HighlighterPanelForm_FormClosing;
            SyncModeButtons();
            SyncTypeButtonLooks();
            SyncColorButtons();
        }

        public void RefreshButtonStates()
        {
            SyncModeButtons();
            SyncTypeButtonLooks();
            SyncColorButtons();
        }

        public void PlaceNearInventor()
        {
            try
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                Left = wa.Right - Width - 24;
                Top = wa.Top + 80;
            }
            catch
            {
                StartPosition = FormStartPosition.CenterScreen;
            }
        }

        private static Button MakeModeButton(string text)
        {
            var btn = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 4, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 186);
            return btn;
        }

        private void TypeButton_Click(object sender, EventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is HighlightPartType type))
            {
                return;
            }

            bool next = !_controller.IsTypeOn(type);
            _controller.SetType(type, next);
            SyncTypeButtonLooks();
        }

        private void ColorButton_Click(object sender, EventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is HighlightPartType type))
            {
                return;
            }

            _controller.CycleTypeColor(type);
            SyncColorButtons();
            SyncTypeButtonLooks();
        }

        private void ColorButton_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            if (!(sender is Button btn) || !(btn.Tag is HighlightPartType type))
            {
                return;
            }

            bool wasTop = TopMost;
            try
            {
                TopMost = false;
                HighlightRgb current = _controller.GetColor(type);
                using (var menu = new ContextMenuStrip())
                {
                    foreach (HighlightRgb opt in HighlightColorPalette.Options)
                    {
                        HighlightRgb captured = opt;
                        var item = new ToolStripMenuItem(captured.Name)
                        {
                            Checked = captured.R == current.R && captured.G == current.G && captured.B == current.B
                        };
                        item.Click += (_, __) =>
                        {
                            _controller.SetTypeColor(type, captured.R, captured.G, captured.B);
                            SyncColorButtons();
                            SyncTypeButtonLooks();
                        };
                        menu.Items.Add(item);
                    }

                    menu.Show(btn, new Point(0, btn.Height));
                }
            }
            finally
            {
                TopMost = wasTop;
            }
        }

        private void SyncModeButtons()
        {
            bool selective = _controller.ScopeMode == HighlightScopeMode.Selective
                || _controller.IsPicking
                || _controller.SelectiveApplied;
            StyleToggle(_allModeBtn, !selective);
            StyleToggle(_selectiveModeBtn, selective);
        }

        private static void StyleToggle(Button btn, bool on)
        {
            if (on)
            {
                btn.BackColor = Color.FromArgb(210, 228, 245);
                btn.FlatAppearance.BorderColor = Color.FromArgb(70, 120, 170);
                btn.Font = new Font(btn.Font, FontStyle.Bold);
            }
            else
            {
                btn.BackColor = Color.White;
                btn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 186);
                btn.Font = new Font(btn.Font, FontStyle.Regular);
            }
        }

        private void SyncTypeButtonLooks()
        {
            for (int i = 0; i < _typeButtons.Length; i++)
            {
                Button btn = _typeButtons[i];
                if (!(btn.Tag is HighlightPartType type))
                {
                    continue;
                }

                bool on = _controller.IsTypeOn(type);
                HighlightRgb rgb = _controller.GetColor(type);
                if (on)
                {
                    btn.BackColor = Color.FromArgb(
                        255,
                        Math.Min(255, rgb.R / 2 + 128),
                        Math.Min(255, rgb.G / 2 + 128),
                        Math.Min(255, rgb.B / 2 + 128));
                    btn.FlatAppearance.BorderColor = Color.FromArgb(rgb.R, rgb.G, rgb.B);
                    btn.Font = new Font(Font, FontStyle.Bold);
                }
                else
                {
                    btn.BackColor = Color.White;
                    btn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 186);
                    btn.Font = new Font(Font, FontStyle.Regular);
                }
            }
        }

        private void SyncColorButtons()
        {
            for (int i = 0; i < _colorButtons.Length; i++)
            {
                Button btn = _colorButtons[i];
                if (!(btn.Tag is HighlightPartType type))
                {
                    continue;
                }

                HighlightRgb rgb = _controller.GetColor(type);
                btn.BackColor = Color.FromArgb(rgb.R, rgb.G, rgb.B);
                btn.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 65);
                _tips.SetToolTip(btn, rgb.Name + " — click to cycle, right-click to pick");
            }
        }

        public void ForceClose()
        {
            _controller.SaveColors();
            _allowClose = true;
            Close();
        }

        private void HighlighterPanelForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _controller.SaveColors();
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }
    }
}
