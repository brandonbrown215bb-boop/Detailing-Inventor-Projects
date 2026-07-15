using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using InvApp = Inventor.Application;
using Inventor;
using VisTog.Core;
using VisTog.Data;

namespace VisTog.UI
{
    /// <summary>
    /// Standard WinForms dock panel: All On / Shark Cage, Simple|Advanced, Roof/Wall/Base tabs,
    /// zone part buttons, surface toggle + appearance gear.
    /// </summary>
    internal sealed class VisTogPanel : UserControl
    {
        private static readonly Dictionary<string, ZoneSurfaceSpec> ZoneSurfaces =
            new Dictionary<string, ZoneSurfaceSpec>(StringComparer.OrdinalIgnoreCase)
            {
                { "Roof", new ZoneSurfaceSpec("Roofs", "YC SURF ROOF") },
                { "Wall", new ZoneSurfaceSpec("Walls", "YC SURF WALL") },
                { "Base", new ZoneSurfaceSpec("Bases", "YC SURF BASE") }
            };

        public static int DefaultDockWidth
        {
            get
            {
                VisTogUiSettings s = VisTogUiSettings.Current;
                return Math.Max(140, s.width);
            }
        }

        private readonly InvApp _app;
        private readonly VisibilityToggleService _service = new VisibilityToggleService();
        private readonly List<SurfaceToggleControl> _surfaceToggles = new List<SurfaceToggleControl>();
        private readonly List<ZoneModePanels> _zoneModePanels = new List<ZoneModePanels>();
        private readonly VisTogUiSettings _settings;
        private readonly int _btnH;
        private readonly int _pad;
        private readonly int _gap;
        private bool _suppressSurfaceToggleEvents;
        private bool _granularMode;
        private Button _simpleModeButton;
        private Button _advancedModeButton;
        private Image _sharkIcon;

        public VisTogPanel(InvApp app)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            VisTogUiTheme.Apply(VisTogUiSettings.Current);
            _settings = VisTogUiSettings.Current;
            _btnH = GetPartRowHeight();
            _pad = Math.Max(4, _settings.pad);
            _gap = Math.Max(2, _settings.gap);

            Font = VisTogUiTheme.UiFont;
            BackColor = VisTogUiTheme.Back;
            Dock = DockStyle.Fill;
            Padding = new Padding(_pad);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                BackColor = VisTogUiTheme.Back
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, _btnH + _gap));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, _btnH + _gap));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(CreateMasterRow(), 0, 0);
            root.Controls.Add(CreateModeSwitch(), 0, 1);

            int tabW = Math.Max(36, (DefaultDockWidth - (_pad * 2) - 8) / 3);
            var zoneTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(tabW, Math.Max(22, _btnH)),
                Padding = new System.Drawing.Point(4, 3),
                Margin = Padding.Empty,
                BackColor = VisTogUiTheme.Back
            };
            BuildZoneTabs(zoneTabs);
            root.Controls.Add(zoneTabs, 0, 2);

            Controls.Add(root);
            ApplyModeVisuals();
        }

        internal static int GetPartRowHeight()
        {
            VisTogUiSettings settings = VisTogUiSettings.Current;
            Font font = VisTogUiTheme.PartFont ?? VisTogUiTheme.UiFont;
            int configured = Math.Max(22, settings.btnH);
            int fromFont = (int)Math.Ceiling(font.GetHeight()) + 8;
            return Math.Max(configured, fromFont);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sharkIcon?.Dispose();
                _sharkIcon = null;
            }

            base.Dispose(disposing);
        }

        public void RefreshForActiveAssembly()
        {
            RefreshSurfaceToggleStates();
        }

        private Control CreateMasterRow()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, _gap),
                Padding = Padding.Empty,
                BackColor = VisTogUiTheme.Back
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));

            Button allOn = CreateAccentButton("All On");
            allOn.Margin = new Padding(0, 0, 2, 0);
            allOn.Click += (_, __) => RunAllOn();

            Button shark = CreateSharkCageButton();
            shark.Margin = new Padding(2, 0, 0, 0);
            shark.Click += (_, __) => RunSharkCage();

            row.Controls.Add(allOn, 0, 0);
            row.Controls.Add(shark, 1, 0);
            return row;
        }

        private Control CreateModeSwitch()
        {
            var row = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, _gap),
                Padding = Padding.Empty,
                BackColor = VisTogUiTheme.Back
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            _simpleModeButton = CreateSegmentButton("Simple");
            _advancedModeButton = CreateSegmentButton("Advanced");
            _simpleModeButton.Margin = new Padding(0, 0, 2, 0);
            _advancedModeButton.Margin = new Padding(2, 0, 0, 0);
            _simpleModeButton.Click += (_, __) => SetGranularMode(false);
            _advancedModeButton.Click += (_, __) => SetGranularMode(true);

            row.Controls.Add(_simpleModeButton, 0, 0);
            row.Controls.Add(_advancedModeButton, 1, 0);
            return row;
        }

        private Button CreateSharkCageButton()
        {
            bool iconOnly = _settings.sharkIconOnly;
            bool showIcon = _settings.sharkShowIcon;
            Button button = CreateAccentButton(iconOnly && showIcon ? string.Empty : "Shark Cage");
            if (showIcon)
            {
                _sharkIcon = LoadSharkIcon(Math.Max(14, _btnH - 6));
                if (_sharkIcon != null)
                {
                    button.Image = _sharkIcon;
                    button.ImageAlign = ContentAlignment.MiddleCenter;
                    if (iconOnly)
                    {
                        button.Text = string.Empty;
                    }
                    else
                    {
                        button.TextImageRelation = TextImageRelation.ImageBeforeText;
                    }
                }
                else if (iconOnly)
                {
                    button.Text = "Cage";
                }
            }

            return button;
        }

        private static Image LoadSharkIcon(int targetHeight)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                string path = System.IO.Path.Combine(dir, "assets", "sharkcage.png");
                if (!System.IO.File.Exists(path))
                {
                    path = System.IO.Path.Combine(dir, "sharkcage.png");
                }

                if (!System.IO.File.Exists(path))
                {
                    return null;
                }

                using (var src = new Bitmap(path))
                {
                    int h = Math.Max(10, targetHeight);
                    int w = Math.Max(8, (int)Math.Round(src.Width * (h / (double)src.Height)));
                    var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(System.Drawing.Color.Transparent);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.DrawImage(src, 0, 0, w, h);
                    }

                    for (int y = 0; y < bmp.Height; y++)
                    {
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            System.Drawing.Color px = bmp.GetPixel(x, y);
                            if (px.A < 16)
                            {
                                continue;
                            }

                            bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(px.A, 255, 255, 255));
                        }
                    }

                    return bmp;
                }
            }
            catch
            {
                return null;
            }
        }

        private Button CreateAccentButton(string text)
        {
            var button = new CenteredFlatButton
            {
                Text = text,
                Dock = DockStyle.Fill,
                Height = _btnH,
                Font = VisTogUiTheme.HeaderFont,
                BackColor = VisTogUiTheme.Accent,
                ForeColor = System.Drawing.Color.White,
                PreferEllipsis = false,
                ShrinkTextToFit = true
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private Button CreateSegmentButton(string text)
        {
            var button = new CenteredFlatButton
            {
                Text = text,
                Dock = DockStyle.Fill,
                Height = _btnH,
                Font = VisTogUiTheme.HeaderFont,
                PreferEllipsis = false,
                ShrinkTextToFit = true
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void SetGranularMode(bool granular)
        {
            if (_granularMode == granular)
            {
                return;
            }

            _granularMode = granular;
            ApplyModeVisuals();
        }

        private void ApplyModeVisuals()
        {
            ApplySegmentVisual(_simpleModeButton, !_granularMode);
            ApplySegmentVisual(_advancedModeButton, _granularMode);

            foreach (ZoneModePanels zone in _zoneModePanels)
            {
                zone.SimpleHost.Visible = !_granularMode;
                zone.GranularHost.Visible = _granularMode;
            }
        }

        private static void ApplySegmentVisual(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            if (selected)
            {
                button.BackColor = VisTogUiTheme.Accent;
                button.ForeColor = System.Drawing.Color.White;
            }
            else
            {
                button.BackColor = VisTogUiTheme.Header;
                button.ForeColor = VisTogUiTheme.Muted;
            }
        }

        private void BuildZoneTabs(TabControl zoneTabs)
        {
            VisTogRulesDocument rules = VisTogRulesCatalog.Load();
            foreach (string zone in new[] { "Roof", "Wall", "Base" })
            {
                var zonePage = new TabPage(zone)
                {
                    BackColor = VisTogUiTheme.Back,
                    Padding = new Padding(2)
                };

                Panel simpleHost = CreateScrollStack();
                Panel granularHost = CreateScrollStack();
                var simpleStack = (TableLayoutPanel)simpleHost.Tag;
                var granularStack = (TableLayoutPanel)granularHost.Tag;

                foreach (Control button in BuildSimpleButtons(
                    rules.simpleButtons.Where(b => string.Equals(b.zone, zone, StringComparison.OrdinalIgnoreCase))))
                {
                    AddStackRow(simpleStack, button, autoHeight: false);
                }

                foreach (Control section in BuildZoneSections(
                    rules.buttons.Where(b => b.path != null
                        && b.path.StartsWith(zone + "/", StringComparison.OrdinalIgnoreCase))))
                {
                    AddStackRow(granularStack, section, autoHeight: true);
                }

                simpleHost.Visible = true;
                granularHost.Visible = false;
                _zoneModePanels.Add(new ZoneModePanels(simpleHost, granularHost));

                var contentHost = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = VisTogUiTheme.Back
                };
                contentHost.Controls.Add(granularHost);
                contentHost.Controls.Add(simpleHost);

                zonePage.Controls.Add(CreateSurfaceFooter(zone));
                zonePage.Controls.Add(contentHost);
                zoneTabs.TabPages.Add(zonePage);
            }
        }

        private Control CreateSurfaceFooter(string zone)
        {
            ZoneSurfaceSpec spec = ZoneSurfaces[zone];

            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = _btnH + 6,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 3, 0, 0),
                Margin = Padding.Empty,
                BackColor = VisTogUiTheme.Back
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, _btnH + 4));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var toggle = new CheckBox
            {
                Text = spec.Label,
                Dock = DockStyle.Fill,
                Checked = true,
                Appearance = Appearance.Button,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = VisTogUiTheme.HeaderFont,
                ForeColor = System.Drawing.Color.White,
                BackColor = VisTogUiTheme.Accent,
                Margin = new Padding(0, 0, 4, 0),
                UseCompatibleTextRendering = true
            };
            toggle.FlatAppearance.BorderSize = 0;
            toggle.FlatAppearance.CheckedBackColor = VisTogUiTheme.Accent;
            ApplyToggleVisual(toggle, spec.Label);
            toggle.CheckedChanged += (_, __) =>
            {
                ApplyToggleVisual(toggle, spec.Label);
                if (_suppressSurfaceToggleEvents)
                {
                    return;
                }

                RunSurfaceToggle(spec, toggle.Checked);
            };

            var gear = new CenteredFlatButton
            {
                Text = "⚙",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI Symbol", Math.Max(11F, _btnH * 0.55F), FontStyle.Regular),
                BackColor = VisTogUiTheme.Header,
                ForeColor = VisTogUiTheme.Text,
                PreferEllipsis = false
            };
            gear.FlatAppearance.BorderSize = 1;
            gear.FlatAppearance.BorderColor = VisTogUiTheme.ButtonBorder;
            gear.Click += (_, __) => OpenAppearanceSettings();

            _surfaceToggles.Add(new SurfaceToggleControl(toggle, spec, zone));
            host.Controls.Add(toggle, 0, 0);
            host.Controls.Add(gear, 1, 0);
            return host;
        }

        private void OpenAppearanceSettings()
        {
            using (var form = new AppearanceSettingsForm(VisTogUiSettings.Current))
            {
                if (form.ShowDialog(FindForm()) != DialogResult.OK)
                {
                    return;
                }

                VisTogUiSettings updated = form.ResultSettings;
                updated.width = _settings.width;
                updated.height = _settings.height;
                updated.pad = _settings.pad;
                updated.gap = _settings.gap;
                updated.btnH = _settings.btnH;
                updated.borderW = _settings.borderW;
                updated.sharkShowIcon = _settings.sharkShowIcon;
                updated.sharkIconOnly = _settings.sharkIconOnly;
                updated.layout = null;
                updated.Save();
                VisTogUiSettings.Replace(updated);
                InventorUiHelper.ShowMessage(
                    _app,
                    "Appearance saved. Close and reopen ISG Visibility to apply font/color changes.",
                    "ISG Visibility");
            }
        }

        private void AddStackRow(TableLayoutPanel stack, Control control, bool autoHeight)
        {
            int row = stack.RowCount;
            stack.RowCount = row + 1;
            if (autoHeight)
            {
                stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            else
            {
                stack.RowStyles.Add(new RowStyle(SizeType.Absolute, _btnH + 2));
            }

            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 0, 0, 2);
            stack.Controls.Add(control, 0, row);
        }

        private Panel CreateScrollStack()
        {
            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = VisTogUiTheme.Back
            };

            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 0,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
                BackColor = VisTogUiTheme.Back,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            void SyncWidth()
            {
                int width = Math.Max(0, scrollPanel.ClientSize.Width);
                if (width <= 0)
                {
                    return;
                }

                stack.MaximumSize = new Size(width, 0);
                stack.Width = width;
            }

            scrollPanel.SizeChanged += (_, __) => SyncWidth();
            scrollPanel.Layout += (_, __) => SyncWidth();
            scrollPanel.Controls.Add(stack);
            scrollPanel.Tag = stack;
            return scrollPanel;
        }

        private IEnumerable<Control> BuildSimpleButtons(IEnumerable<SimpleButtonSpec> simpleButtons)
        {
            foreach (SimpleButtonSpec spec in simpleButtons)
            {
                if (spec == null || string.IsNullOrWhiteSpace(spec.label) || spec.ruleIds == null || spec.ruleIds.Count == 0)
                {
                    continue;
                }

                string label = spec.label;
                List<string> ruleIds = spec.ruleIds.ToList();
                Button button = CreateCompactButton(label);
                button.Click += (_, __) => RunSimpleButton(ruleIds);
                yield return button;
            }
        }

        private static void ApplyToggleVisual(CheckBox toggle, string label)
        {
            if (toggle.Checked)
            {
                toggle.BackColor = VisTogUiTheme.Accent;
                toggle.ForeColor = System.Drawing.Color.White;
                toggle.Text = label;
            }
            else
            {
                toggle.BackColor = VisTogUiTheme.Header;
                toggle.ForeColor = VisTogUiTheme.Muted;
                toggle.Text = label + " (off)";
            }
        }

        private IEnumerable<Control> BuildZoneSections(IEnumerable<ButtonLayoutSpec> zoneButtons)
        {
            var ordered = zoneButtons
                .Where(b => !string.IsNullOrWhiteSpace(b.path) && !string.IsNullOrWhiteSpace(b.ruleId))
                .GroupBy(b => b.ruleId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Select(b => new { Spec = b, Parts = b.path.Split('/') })
                .Where(x => x.Parts.Length >= 2)
                .ToList();

            var sections = new List<CollapsibleSection>();
            var sectionIndex = new Dictionary<string, CollapsibleSection>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in ordered)
            {
                string sectionKey = GetSectionKey(entry.Parts);
                if (!sectionIndex.TryGetValue(sectionKey, out CollapsibleSection section))
                {
                    section = new CollapsibleSection(sectionKey);
                    sectionIndex[sectionKey] = section;
                    sections.Add(section);
                }

                section.AddControl(CreateRuleButton(entry.Spec));
            }

            return sections;
        }

        private static string GetSectionKey(string[] parts)
        {
            if (parts.Length == 2)
            {
                return parts[1];
            }

            return parts[1] + " / " + parts[2];
        }

        private Button CreateRuleButton(ButtonLayoutSpec spec)
        {
            Button button = CreateCompactButton(spec.label);
            button.Click += (_, __) => RunRule(spec.ruleId);
            return button;
        }

        private Button CreateCompactButton(string text)
        {
            var button = new CenteredFlatButton
            {
                Text = text,
                Height = _btnH,
                Dock = DockStyle.Fill,
                Font = VisTogUiTheme.PartFont,
                ForeColor = VisTogUiTheme.Text,
                BackColor = VisTogUiTheme.Header,
                PreferEllipsis = true
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = VisTogUiTheme.ButtonBorder;
            return button;
        }

        private AssemblyDocument GetAssemblyDocument()
        {
            try
            {
                return _app.ActiveDocument as AssemblyDocument;
            }
            catch
            {
                return null;
            }
        }

        private void RefreshSurfaceToggleStates()
        {
            AssemblyDocument assemblyDocument = GetAssemblyDocument();
            if (assemblyDocument == null)
            {
                return;
            }

            _suppressSurfaceToggleEvents = true;
            try
            {
                foreach (SurfaceToggleControl entry in _surfaceToggles)
                {
                    bool? visible = _service.GetSurfaceDescriptionVisibility(
                        assemblyDocument,
                        entry.Spec.Description);
                    if (visible == null)
                    {
                        continue;
                    }

                    entry.Toggle.Checked = visible.Value;
                    ApplyToggleVisual(entry.Toggle, entry.Spec.Label);
                }
            }
            finally
            {
                _suppressSurfaceToggleEvents = false;
            }
        }

        private void SetSurfaceToggleChecked(SurfaceToggleControl entry, bool checkedState)
        {
            if (entry.Toggle.Checked == checkedState)
            {
                ApplyToggleVisual(entry.Toggle, entry.Spec.Label);
                return;
            }

            _suppressSurfaceToggleEvents = true;
            try
            {
                entry.Toggle.Checked = checkedState;
                ApplyToggleVisual(entry.Toggle, entry.Spec.Label);
            }
            finally
            {
                _suppressSurfaceToggleEvents = false;
            }
        }

        private void RunSurfaceToggle(ZoneSurfaceSpec spec, bool visible)
        {
            AssemblyDocument assemblyDocument = GetAssemblyDocument();
            if (assemblyDocument == null)
            {
                InventorUiHelper.ShowMessage(_app, "Open an assembly before using ISG Visibility.", "ISG Visibility");
                return;
            }

            try
            {
                _service.SetVisibilityBySurfaceDescription(
                    assemblyDocument,
                    spec.Description,
                    visible,
                    _app);
            }
            catch (Exception ex)
            {
                InventorUiHelper.ShowMessage(_app, ex.Message, "ISG Visibility", MessageBoxIcon.Error);
            }
        }

        private void RunAllOn()
        {
            AssemblyDocument assemblyDocument = GetAssemblyDocument();
            if (assemblyDocument == null)
            {
                InventorUiHelper.ShowMessage(_app, "Open an assembly before using ISG Visibility.", "ISG Visibility");
                return;
            }

            try
            {
                _service.RunAllOn(
                    assemblyDocument,
                    _surfaceToggles.Select(entry => entry.Spec.Description),
                    _app);
                foreach (SurfaceToggleControl entry in _surfaceToggles)
                {
                    SetSurfaceToggleChecked(entry, true);
                }
            }
            catch (Exception ex)
            {
                InventorUiHelper.ShowMessage(_app, ex.Message, "ISG Visibility", MessageBoxIcon.Error);
            }
        }

        private static readonly string[] SharkCageRuleIds =
        {
            "VTP_Roof_Horizontal Channel",
            "VTP_Roof_Vertical Channel",
            "VTP_Wall_Horizontal Channel",
            "VTP_Wall_Vertical Channel",
            "VTP_Base_Formed Angle",
            "VTP_Base_Formed Channel",
            "VTP_Base_Structural Steel Angle",
            "VTP_Base_Structural Steel Channel",
            "VTP_Base_Structural Aluminum Angle",
            "VTP_Base_Structural Aluminum Channel"
        };

        private void RunSharkCage()
        {
            AssemblyDocument assemblyDocument = GetAssemblyDocument();
            if (assemblyDocument == null)
            {
                InventorUiHelper.ShowMessage(_app, "Open an assembly before using ISG Visibility.", "ISG Visibility");
                return;
            }

            try
            {
                var cageStocks = new HashSet<string>(
                    VisTogRulesCatalog.GetStocksForRuleIds(SharkCageRuleIds),
                    StringComparer.OrdinalIgnoreCase);

                // Hide only non-cage VisTog stocks; leave cage stocks alone if already on.
                IEnumerable<string> unneeded = VisTogRulesCatalog.GetAllKnownStocks()
                    .Where(stock => !cageStocks.Contains(stock));
                _service.SetStocksVisibility(assemblyDocument, unneeded, false);

                // Turn on any cage parts that are currently off.
                _service.SetStocksVisibility(assemblyDocument, cageStocks, true);
            }
            catch (Exception ex)
            {
                InventorUiHelper.ShowMessage(_app, ex.Message, "ISG Visibility", MessageBoxIcon.Error);
            }
        }

        private void RunSimpleButton(IList<string> ruleIds)
        {
            AssemblyDocument assemblyDocument = GetAssemblyDocument();
            if (assemblyDocument == null)
            {
                InventorUiHelper.ShowMessage(_app, "Open an assembly before using ISG Visibility.", "ISG Visibility");
                return;
            }

            try
            {
                _service.RunRuleGroup(assemblyDocument, ruleIds, _app);
            }
            catch (Exception ex)
            {
                InventorUiHelper.ShowMessage(_app, ex.Message, "ISG Visibility", MessageBoxIcon.Error);
            }
        }

        private void RunRule(string ruleId)
        {
            AssemblyDocument assemblyDocument = GetAssemblyDocument();
            if (assemblyDocument == null)
            {
                InventorUiHelper.ShowMessage(_app, "Open an assembly before using ISG Visibility.", "ISG Visibility");
                return;
            }

            try
            {
                _service.RunRule(assemblyDocument, ruleId, _app);
            }
            catch (Exception ex)
            {
                InventorUiHelper.ShowMessage(_app, ex.Message, "ISG Visibility", MessageBoxIcon.Error);
            }
        }

        private sealed class ZoneSurfaceSpec
        {
            public ZoneSurfaceSpec(string label, string description)
            {
                Label = label;
                Description = description;
            }

            public string Label { get; }
            public string Description { get; }
        }

        private sealed class SurfaceToggleControl
        {
            public SurfaceToggleControl(CheckBox toggle, ZoneSurfaceSpec spec, string zoneName)
            {
                Toggle = toggle;
                Spec = spec;
                ZoneName = zoneName;
            }

            public CheckBox Toggle { get; }
            public ZoneSurfaceSpec Spec { get; }
            public string ZoneName { get; }
        }

        private sealed class ZoneModePanels
        {
            public ZoneModePanels(Panel simpleHost, Panel granularHost)
            {
                SimpleHost = simpleHost;
                GranularHost = granularHost;
            }

            public Panel SimpleHost { get; }
            public Panel GranularHost { get; }
        }
    }
}
