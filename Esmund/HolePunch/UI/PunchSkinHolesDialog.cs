using System;
using System.Drawing;
using System.Windows.Forms;
using SkinChannelPunch.Core;

namespace SkinChannelPunch.UI
{
    internal sealed class PunchSkinHolesDialog : Form
    {
        private readonly PatternPresetFile _config;
        private readonly NumericUpDown _topInset;
        private readonly NumericUpDown _bottomInset;
        private readonly NumericUpDown _maxSpacing;
        private readonly Label _statusLabel;

        private readonly CheckBox _rememberBox;
        private readonly Panel _wallRoofColumns;
        private readonly Panel _deckColumns;
        private readonly FlowLayoutPanel _pickerRow;

        private readonly RadioButton _surfaceWall;
        private readonly RadioButton _surfaceRoof;
        private readonly RadioButton _surfaceDeck;

        private readonly RadioButton _compBaseline;
        private readonly RadioButton _compIbc15;
        private readonly RadioButton _compNoa;
        private readonly RadioButton _compTiered;

        private readonly RadioButton _arrSingle;
        private readonly RadioButton _arrStacked;

        private readonly RadioButton _tier1;
        private readonly RadioButton _tier2;

        private readonly RadioButton _foamYes;
        private readonly RadioButton _foamNo;

        private readonly RadioButton _faceExt;
        private readonly RadioButton _faceInt;

        private RadioButton _placeExternal;
        private RadioButton _placeCorridor;

        private readonly RadioButton _runInternal;
        private readonly RadioButton _runOverlap;

        private ComboBox _deckThickness;
        private ComboBox _deckSpacing;

        private bool _suppress;

        public double TopInsetIn => (double)_topInset.Value;
        public double BottomInsetIn => (double)_bottomInset.Value;
        public double MaxSpacingIn => (double)_maxSpacing.Value;
        public double HoleDiameterIn => _config.holeDiameterIn > 0
            ? _config.holeDiameterIn
            : PatternConstants.HoleDiameterIn;

        public PunchSkinHolesDialog()
        {
            _config = PatternPresetConfig.LoadOrDefault();

            Text = "Hole Punch";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(860, 430);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 1,
                RowCount = 4,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            var values = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
            };
            values.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            values.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            int row = 0;
            AddRow(values, row++, "Top inset (in)", _topInset = CreateNumeric(7m));
            AddRow(values, row++, "Bottom inset (in)", _bottomInset = CreateNumeric(1.25m));
            AddRow(values, row++, "Max center-to-center (in)", _maxSpacing = CreateNumeric(36m));
            AddRow(values, row++, "Hole diameter (in)", CreateNumeric((decimal)HoleDiameterIn, readOnly: true));
            root.Controls.Add(values, 0, 0);

            _pickerRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(4),
                BorderStyle = BorderStyle.FixedSingle,
            };

            _rememberBox = new CheckBox
            {
                Text = "Keep last used",
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0),
            };
            _rememberBox.CheckedChanged += (_, __) => OnRememberChanged();
            var rememberHost = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
            };
            rememberHost.Controls.Add(_rememberBox);
            _pickerRow.Controls.Add(WrapColumn("Session", rememberHost, 78));

            var surfaceHost = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
            };
            _surfaceWall = MakeRadio("Wall", true, surfaceHost);
            _surfaceRoof = MakeRadio("Roof", false, surfaceHost);
            _surfaceDeck = MakeRadio("Deck", false, surfaceHost);
            WireSurface(_surfaceWall, _surfaceRoof, _surfaceDeck);
            _pickerRow.Controls.Add(WrapColumn("Surface", surfaceHost, 64));

            _deckColumns = BuildDeckColumns();
            var deckCombos = (object[])_deckColumns.Tag;
            _deckThickness = (ComboBox)deckCombos[0];
            _deckSpacing = (ComboBox)deckCombos[1];
            _deckColumns.Visible = false;
            _pickerRow.Controls.Add(_deckColumns);

            _wallRoofColumns = BuildWallRoofColumns(
                out _compBaseline, out _compIbc15, out _compNoa, out _compTiered,
                out _arrSingle, out _arrStacked,
                out _tier1, out _tier2,
                out _foamYes, out _foamNo,
                out _faceExt, out _faceInt,
                out _placeExternal, out _placeCorridor,
                out _runInternal, out _runOverlap);
            _pickerRow.Controls.Add(_wallRoofColumns);

            root.Controls.Add(_pickerRow, 0, 1);

            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                ForeColor = Color.DimGray,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            root.Controls.Add(_statusLabel, 0, 2);

            var ok = new Button { Text = "Continue to picks", DialogResult = DialogResult.OK, Width = 130, Height = 28 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 28 };
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
            };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 3);

            Controls.Add(root);
            AcceptButton = ok;
            CancelButton = cancel;

            FormClosing += (_, e) =>
            {
                if (DialogResult == DialogResult.OK || _rememberBox.Checked)
                {
                    PersistLastUsed();
                }
            };

            PatternSelection saved = PatternLastUsedStore.LoadOrNull();
            if (saved != null && saved.RememberLastUsed)
            {
                _suppress = true;
                ApplySelection(saved);
                _suppress = false;
            }

            RefreshEnabledState();
            ApplyResolvedValues();
        }

        private Panel BuildDeckColumns()
        {
            var host = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(4, 0, 4, 0),
                Width = 170,
            };

            host.Controls.Add(new Label
            {
                Text = "Deck Thickness",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 2),
            });
            var deckThickness = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 150,
                Margin = new Padding(0, 0, 0, 8),
            };
            deckThickness.Items.AddRange(new object[] { "2", "3", "4" });
            deckThickness.SelectedItem = "4";
            deckThickness.SelectedIndexChanged += (_, __) => OnSelectionChanged();
            host.Controls.Add(deckThickness);

            host.Controls.Add(new Label
            {
                Text = "Channel Spacing",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 2),
            });
            var deckSpacing = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 150,
            };
            deckSpacing.Items.AddRange(new object[] { "11.75", "12", "15.67", "16", "23.5", "24" });
            deckSpacing.SelectedItem = "24";
            deckSpacing.SelectedIndexChanged += (_, __) => OnSelectionChanged();
            host.Controls.Add(deckSpacing);

            // Assign via field init helper — constructor-only: store in Tag then pull in ctor
            host.Tag = new object[] { deckThickness, deckSpacing };
            return host;
        }

        private Panel BuildWallRoofColumns(
            out RadioButton compBaseline,
            out RadioButton compIbc15,
            out RadioButton compNoa,
            out RadioButton compTiered,
            out RadioButton arrSingle,
            out RadioButton arrStacked,
            out RadioButton tier1,
            out RadioButton tier2,
            out RadioButton foamYes,
            out RadioButton foamNo,
            out RadioButton faceExt,
            out RadioButton faceInt,
            out RadioButton placeExternal,
            out RadioButton placeCorridor,
            out RadioButton runInternal,
            out RadioButton runOverlap)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
            };

            var comp = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            compBaseline = MakeRadio("IBC Baseline", true, comp);
            compIbc15 = MakeRadio("IBC 1.5", false, comp);
            compNoa = MakeRadio("NOA", false, comp);
            compTiered = MakeRadio("Tiered", false, comp);
            WireGroup(compBaseline, compIbc15, compNoa, compTiered);
            row.Controls.Add(WrapColumn("Construction Compliance", comp, 92));

            var arr = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            arrSingle = MakeRadio("Single", true, arr);
            arrStacked = MakeRadio("Stacked", false, arr);
            WireGroup(arrSingle, arrStacked);
            row.Controls.Add(WrapColumn("Vertical Arrangement", arr, 80));

            var tier = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            tier1 = MakeRadio("Tier 1", true, tier);
            tier2 = MakeRadio("Tier 2", false, tier);
            WireGroup(tier1, tier2);
            row.Controls.Add(WrapColumn("Tier", tier, 60));

            var foam = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            foamYes = MakeRadio("Yes", true, foam);
            foamNo = MakeRadio("No", false, foam);
            WireGroup(foamYes, foamNo);
            row.Controls.Add(WrapColumn("Full Foam Construction", foam, 78));

            var face = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            faceExt = MakeRadio("Exterior", true, face);
            faceInt = MakeRadio("Interior", false, face);
            WireGroup(faceExt, faceInt);
            row.Controls.Add(WrapColumn("Face", face, 68));

            var place = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            placeExternal = MakeRadio("External", true, place);
            placeCorridor = MakeRadio("Corridor", false, place);
            WireGroup(placeExternal, placeCorridor);
            row.Controls.Add(WrapColumn("Placement", place, 70));

            var run = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            runInternal = MakeRadio("Internal Channel", true, run);
            runOverlap = MakeRadio("Overlap", false, run);
            WireGroup(runInternal, runOverlap);
            row.Controls.Add(WrapColumn("Run", run, 88));

            return row;
        }

        private void WireSurface(params RadioButton[] radios)
        {
            foreach (RadioButton r in radios)
            {
                r.CheckedChanged += (_, __) =>
                {
                    if (_suppress || !r.Checked)
                    {
                        return;
                    }

                    OnSelectionChanged();
                };
            }
        }

        private void WireGroup(params RadioButton[] radios)
        {
            foreach (RadioButton r in radios)
            {
                r.CheckedChanged += (_, __) =>
                {
                    if (_suppress || !r.Checked)
                    {
                        return;
                    }

                    OnSelectionChanged();
                };
            }
        }

        private void OnRememberChanged()
        {
            if (_suppress)
            {
                return;
            }

            if (_rememberBox.Checked)
            {
                PatternSelection saved = PatternLastUsedStore.LoadOrNull();
                if (saved != null)
                {
                    _suppress = true;
                    saved.RememberLastUsed = true;
                    ApplySelection(saved);
                    _suppress = false;
                }
            }

            PersistLastUsed();
            RefreshEnabledState();
            ApplyResolvedValues();
        }

        private void OnSelectionChanged()
        {
            if (_suppress)
            {
                return;
            }

            RefreshEnabledState();
            ApplyResolvedValues();
            if (_rememberBox.Checked)
            {
                PersistLastUsed();
            }
        }

        private void RefreshEnabledState()
        {
            PatternSelection s = CaptureSelection();
            bool isDeck = s.Surface == PatternSurfaceKind.Deck;
            _deckColumns.Visible = isDeck;
            _wallRoofColumns.Visible = !isDeck;

            if (isDeck)
            {
                return;
            }

            bool arrangementOk = PatternCatalog.ArrangementApplies(s);
            bool tierOk = PatternCatalog.TierApplies(s);
            bool foamOk = PatternCatalog.FullFoamApplies(s);
            bool interiorOk = PatternCatalog.InteriorAllowed(s);
            bool placementOk = PatternCatalog.PlacementApplies(s);

            SetColumnEnabled(_arrSingle, _arrStacked, arrangementOk);
            SetColumnEnabled(_tier1, _tier2, tierOk);
            SetColumnEnabled(_foamYes, _foamNo, foamOk);
            SetColumnEnabled(_placeExternal, _placeCorridor, placementOk);
            _faceInt.Enabled = interiorOk;

            if (!arrangementOk && !_arrSingle.Checked)
            {
                _suppress = true;
                _arrSingle.Checked = true;
                _suppress = false;
            }

            if (!interiorOk && _faceInt.Checked)
            {
                _suppress = true;
                _faceExt.Checked = true;
                _suppress = false;
            }

            if (!placementOk && !_placeExternal.Checked)
            {
                _suppress = true;
                _placeExternal.Checked = true;
                _suppress = false;
            }
        }

        private static void SetColumnEnabled(RadioButton a, RadioButton b, bool enabled)
        {
            a.Enabled = enabled;
            b.Enabled = enabled;
            if (!enabled)
            {
                // leave checked state; Resolve normalizes
            }
        }

        private void ApplyResolvedValues()
        {
            PatternResolveResult result = PatternCatalog.Resolve(CaptureSelection());
            SetNumeric(_topInset, result.TopInsetIn);
            SetNumeric(_bottomInset, result.BottomInsetIn);
            SetNumeric(_maxSpacing, result.MaxCenterToCenterIn);
            _statusLabel.Text = result.Found
                ? string.Empty
                : (result.Message ?? "Preset incomplete — values may be placeholders.");
            _statusLabel.ForeColor = result.Found ? Color.DimGray : Color.DarkOrange;
        }

        private PatternSelection CaptureSelection()
        {
            var s = new PatternSelection
            {
                RememberLastUsed = _rememberBox.Checked,
                DeckThickness = _deckThickness.SelectedItem as string ?? "4",
                DeckChannelSpacing = _deckSpacing.SelectedItem as string ?? "24",
            };

            if (_surfaceDeck.Checked)
            {
                s.Surface = PatternSurfaceKind.Deck;
            }
            else if (_surfaceRoof.Checked)
            {
                s.Surface = PatternSurfaceKind.Roof;
            }
            else
            {
                s.Surface = PatternSurfaceKind.Wall;
            }

            if (_compNoa.Checked)
            {
                s.Compliance = PatternCompliance.Noa;
            }
            else if (_compTiered.Checked)
            {
                s.Compliance = PatternCompliance.Tiered;
            }
            else if (_compIbc15.Checked)
            {
                s.Compliance = PatternCompliance.Ibc15;
            }
            else
            {
                s.Compliance = PatternCompliance.IbcBaseline;
            }

            s.Arrangement = _arrStacked.Checked
                ? PatternArrangement.Stacked
                : PatternArrangement.Single;
            s.Tier = _tier2.Checked ? PatternTier.Tier2 : PatternTier.Tier1;
            s.FullFoam = _foamNo.Checked ? PatternFullFoam.No : PatternFullFoam.Yes;
            s.Face = _faceInt.Checked ? PatternFace.Interior : PatternFace.Exterior;
            s.Placement = _placeCorridor.Checked
                ? PatternPlacement.Corridor
                : PatternPlacement.External;
            s.Run = _runOverlap.Checked ? PatternRun.Overlap : PatternRun.InternalChannel;
            PatternCatalog.Normalize(s);
            return s;
        }

        private void ApplySelection(PatternSelection s)
        {
            PatternCatalog.Normalize(s);
            _rememberBox.Checked = s.RememberLastUsed;

            _surfaceWall.Checked = s.Surface == PatternSurfaceKind.Wall;
            _surfaceRoof.Checked = s.Surface == PatternSurfaceKind.Roof;
            _surfaceDeck.Checked = s.Surface == PatternSurfaceKind.Deck;

            _compBaseline.Checked = s.Compliance == PatternCompliance.IbcBaseline;
            _compIbc15.Checked = s.Compliance == PatternCompliance.Ibc15;
            _compNoa.Checked = s.Compliance == PatternCompliance.Noa;
            _compTiered.Checked = s.Compliance == PatternCompliance.Tiered;

            _arrSingle.Checked = s.Arrangement == PatternArrangement.Single;
            _arrStacked.Checked = s.Arrangement == PatternArrangement.Stacked;
            _tier1.Checked = s.Tier == PatternTier.Tier1;
            _tier2.Checked = s.Tier == PatternTier.Tier2;
            _foamYes.Checked = s.FullFoam == PatternFullFoam.Yes;
            _foamNo.Checked = s.FullFoam == PatternFullFoam.No;
            _faceExt.Checked = s.Face == PatternFace.Exterior;
            _faceInt.Checked = s.Face == PatternFace.Interior;
            _placeExternal.Checked = s.Placement == PatternPlacement.External;
            _placeCorridor.Checked = s.Placement == PatternPlacement.Corridor;
            _runInternal.Checked = s.Run == PatternRun.InternalChannel;
            _runOverlap.Checked = s.Run == PatternRun.Overlap;

            SelectCombo(_deckThickness, s.DeckThickness ?? "4");
            SelectCombo(_deckSpacing, s.DeckChannelSpacing ?? "24");

            _deckColumns.Visible = s.Surface == PatternSurfaceKind.Deck;
            _wallRoofColumns.Visible = s.Surface != PatternSurfaceKind.Deck;
        }

        private void PersistLastUsed()
        {
            PatternSelection s = CaptureSelection();
            s.RememberLastUsed = _rememberBox.Checked;
            PatternLastUsedStore.Save(s);
        }

        private static void SelectCombo(ComboBox box, string value)
        {
            int index = box.Items.IndexOf(value);
            if (index >= 0)
            {
                box.SelectedIndex = index;
            }
        }

        private static RadioButton MakeRadio(string text, bool isChecked, Control parent)
        {
            var radio = new RadioButton
            {
                Text = text,
                AutoSize = true,
                Checked = isChecked,
                Margin = new Padding(0, 1, 0, 1),
            };
            parent.Controls.Add(radio);
            return radio;
        }

        private const int ColumnHeaderHeight = 34;

        private static Control WrapColumn(string title, Control content, int width)
        {
            var panel = new Panel
            {
                Width = width,
                Margin = new Padding(4, 0, 4, 0),
                AutoSize = false,
            };

            var titleLabel = new Label
            {
                Text = title,
                Width = width - 2,
                Height = ColumnHeaderHeight,
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                Location = new Point(0, 0),
                TextAlign = ContentAlignment.BottomLeft,
            };

            content.Location = new Point(0, ColumnHeaderHeight + 2);
            content.Width = width - 2;
            panel.Controls.Add(titleLabel);
            panel.Controls.Add(content);

            // Prefer content preferred size after AutoSize children settle
            int contentHeight = content.PreferredSize.Height;
            if (contentHeight < 40)
            {
                contentHeight = 90;
            }

            content.Height = contentHeight;
            panel.Height = ColumnHeaderHeight + 4 + contentHeight;
            return panel;
        }

        private static void SetNumeric(NumericUpDown box, double value)
        {
            decimal dec = (decimal)value;
            if (dec < box.Minimum)
            {
                dec = box.Minimum;
            }

            if (dec > box.Maximum)
            {
                dec = box.Maximum;
            }

            box.Value = dec;
        }

        private static NumericUpDown CreateNumeric(decimal value, bool readOnly = false)
        {
            return new NumericUpDown
            {
                DecimalPlaces = 3,
                Minimum = 0,
                Maximum = 500,
                Increment = 0.125m,
                Value = value,
                ReadOnly = readOnly,
                Enabled = !readOnly,
                Dock = DockStyle.Fill,
            };
        }

        private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
        {
            layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            layout.Controls.Add(control, 1, row);
        }
    }
}
