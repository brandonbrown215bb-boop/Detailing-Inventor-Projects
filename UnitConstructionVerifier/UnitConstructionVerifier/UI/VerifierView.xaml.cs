using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UnitConstructionVerifier.Engine;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.Operations;
using UnitConstructionVerifier.Persistence;

namespace UnitConstructionVerifier.UI
{
    public partial class VerifierView : UserControl
    {
        private readonly UnitConstructionData _data;
        private readonly string               _iamPath;
        private IptScanResult?                _iptResult;
        private UnitSnapshot?                 _snapshot;
        private bool                          _isUpdatingUi;
        private readonly Inventor.Application _inventorApp;
        private readonly Dictionary<string, StagedEdit> _pendingEdits = new Dictionary<string, StagedEdit>(StringComparer.OrdinalIgnoreCase);
        private List<WriteReviewItem> _reviewItems = new List<WriteReviewItem>();
        private List<RegenerateReviewItem> _regenerateItems = new List<RegenerateReviewItem>();
        private YcShellBridge? _ycShell;
        private byte _xRayColorR = XRayHighlightColors.Default.R;
        private byte _xRayColorG = XRayHighlightColors.Default.G;
        private byte _xRayColorB = XRayHighlightColors.Default.B;
        private PartPreviewWindow? _partPreviewWindow;
        private bool _isRefreshingPartsGrid;
        private bool _overridesDirty;
        private List<string> _gauges = new List<string>();
        private List<string> _materials = new List<string>();
        private bool _keepVerifierAboveInventor;
        private bool _pendingVerifierStackRestore;
        private bool _pendingVerifierFocusRestore;
        private HighlightMode _highlightMode = HighlightMode.Box;
        private readonly SurfaceStatusBoard _statusBoard;
        private readonly System.Windows.Threading.DispatcherTimer _statusTimer;
        private readonly System.Windows.Threading.DispatcherTimer _noticeTimer;
        private readonly Dictionary<DataGrid, string> _partsFilters = new Dictionary<DataGrid, string>();

        private enum HighlightMode { Box, XRay, Off }

        public VerifierView(UnitConstructionData data, string iamPath, Inventor.Application inventorApp)
        {
            // Must exist before the XAML loads: the surface list templates reference it as a StaticResource.
            _statusBoard = new SurfaceStatusBoard();
            Resources["StatusBoard"] = _statusBoard;

            InitializeComponent();

            _data    = data;
            _iamPath = iamPath;
            _inventorApp = inventorApp;

            // Expectation edits re-run the whole-unit check after typing pauses.
            _statusTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _statusTimer.Tick += (_, __) => { _statusTimer.Stop(); RefreshUnitStatus(); };
            _noticeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _noticeTimer.Tick += (_, __) => { _noticeTimer.Stop(); NoticeText.Text = string.Empty; };

            IamPathLabel.Text = Path.GetFileName(iamPath);

            // Load config from materials_config.json
            MaterialsConfig.Initialize();

            // Populate dropdowns with standard config options
            PopulateDropdowns(WindowLayout.OrderGauges(MaterialsConfig.Gauges), MaterialsConfig.Materials);

            // Populate list boxes
            RoofSurfaceList.ItemsSource = _data.RoofRows;
            WallSurfaceList.ItemsSource = _data.WallRows;
            BaseSurfaceList.ItemsSource = _data.BaseRows;

            foreach (DataGrid grid in new[] { RoofPartsGrid, WallPartsGrid, BasePartsGrid })
            {
                _partsFilters[grid] = "All";
                grid.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopyPartNumbersCommand));
            }

            foreach (ListBox list in new[] { RoofSurfaceList, WallSurfaceList, BaseSurfaceList })
            {
                list.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopySurfaceNumberCommand));
            }

            // Load global other specs
            PopulateGlobalSpecs();
            InitializeXRayColorPicker();

            // Inventor hosts this view without a WPF Application, so an uncaught exception in any handler
            // terminates Inventor. Log it and keep running instead. Removed in Shutdown.
            Dispatcher.UnhandledException += OnDispatcherUnhandledException;

            // Docked beside the model the view is narrow: fold to one column there, back to two when wide.
            SizeChanged += (_, __) => ApplyResponsiveLayout();
        }

        /// <summary>Raised by the header's Dock / Pop out button; the presenter moves the view.</summary>
        public event Action? DockToggleRequested;

        /// <summary>Shows the Dock / Pop out button for the host the view is in now.</summary>
        public void SetHostMode(bool docked)
        {
            DockToggleButton.Visibility = Visibility.Visible;
            DockToggleButton.Content = docked ? "Pop out" : "Dock";
            DockToggleButton.ToolTip = docked
                ? "Move the verifier to its own window"
                : "Dock the verifier inside Inventor, beside the model";
        }

        private void OnDockToggleClick(object sender, RoutedEventArgs e) => DockToggleRequested?.Invoke();

        /// <summary>Raised by the bar's Check this unit button: check the assembly now active in Inventor instead.</summary>
        public event Action? CheckActiveUnitRequested;

        /// <summary>
        /// Says when Inventor is on a different assembly than the unit being checked, and offers to check that one.
        /// Hidden for this unit and for its own surface sub-assemblies (opened directly, they are still this unit).
        /// Nothing is re-scanned until the user asks, so staged edits are never dropped behind their back.
        /// </summary>
        public void ShowActiveAssembly(string activeIamPath)
        {
            if (IsThisUnit(activeIamPath))
            {
                OtherUnitBanner.Visibility = Visibility.Collapsed;
                return;
            }

            OtherUnitText.Text = $"Inventor is on {Path.GetFileName(activeIamPath)}. This verifier is still checking {Path.GetFileName(_iamPath)}.";
            OtherUnitBanner.Visibility = Visibility.Visible;
        }

        private bool IsThisUnit(string iamPath)
        {
            bool Same(string? other) => !string.IsNullOrWhiteSpace(other) && string.Equals(other, iamPath, StringComparison.OrdinalIgnoreCase);
            return Same(_iamPath)
                || _data.RoofRows.Any(r => Same(r.SourceSurfaceIam))
                || _data.WallRows.Any(r => Same(r.SourceSurfaceIam))
                || _data.BaseRows.Any(r => Same(r.SourceSurfaceIam))
                || (_iptResult?.Parts.Any(p => Same(p.OwnerIamPath)) ?? false);
        }

        private void OnCloseOtherUnitBannerClick(object sender, RoutedEventArgs e) => OtherUnitBanner.Visibility = Visibility.Collapsed;

        private void OnCheckActiveUnitClick(object sender, RoutedEventArgs e) => CheckActiveUnitRequested?.Invoke();

        /// <summary>The unit assembly this view checks.</summary>
        public string IamPath => _iamPath;

        /// <summary>The pop-out window was activated: keep it above Inventor after COM selection calls.</summary>
        public void NoteHostActivated() => _keepVerifierAboveInventor = true;

        /// <summary>
        /// Asks about unsaved expectations and staged part edits before the view closes. False means the user
        /// cancelled and the view must stay open.
        /// </summary>
        public bool ConfirmClose()
        {
            string? message = UnsavedWorkPrompt.BuildMessage(_overridesDirty, _pendingEdits.Count);
            if (message == null) return true;

            MessageBoxResult answer = Dialogs.Show(this, message, "Unsaved Changes",
                _overridesDirty ? MessageBoxButton.YesNoCancel : MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (answer == MessageBoxResult.Cancel) return false;

            if (answer == MessageBoxResult.Yes)
            {
                PersistenceManager.SaveOverrides(_iamPath, _data);
                ClearOverridesDirty();
            }

            return true;
        }

        private bool? _narrow;

        /// <summary>True while the view uses the one-column layout for a narrow dock.</summary>
        public bool IsNarrow => _narrow == true;

        /// <summary>
        /// Below <see cref="ResponsiveLayout.NarrowBelow"/> the surface list sits above the detail, each expectation
        /// form reads top to bottom (collapsed until opened), header and footer stack, the part action buttons move under
        /// the filters, and the grid drops Description, Type and Checks (Expected and In part show what each check is).
        /// </summary>
        private void ApplyResponsiveLayout()
        {
            if (ActualWidth <= 0) return;
            bool narrow = ActualWidth < ResponsiveLayout.NarrowBelow;
            if (_narrow == narrow) return;
            _narrow = narrow;

            var columnLabel = TryFindResource("ColumnLabel") as Style;
            Func<UIElement, bool> repeatedHeader = e => columnLabel != null && e is TextBlock text && text.Style == columnLabel;

            foreach (Grid body in new[] { RoofBody, WallBody, BaseBody })
            {
                if (narrow)
                    ResponsiveLayout.StackColumns(body,
                        new RowDefinition { Height = new GridLength(190), MinHeight = 110 },
                        new RowDefinition { Height = new GridLength(5) },
                        new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 220 });
                else
                    ResponsiveLayout.Restore(body);
            }

            // The forms are tall once folded; start them collapsed so the parts grid keeps its room.
            foreach (System.Windows.Controls.Primitives.ToggleButton toggle in new[] { RoofSpecToggle, WallSpecToggle, BaseSpecToggle })
            {
                toggle.IsChecked = !narrow;
            }

            foreach (Grid form in new[] { RoofSpecGrid, WallSpecGrid, BaseSpecGrid })
            {
                if (narrow) ResponsiveLayout.FoldColumns(form, 4, repeatedHeader);
                else ResponsiveLayout.Restore(form);
            }

            foreach (Grid bar in new[] { HeaderGrid, FooterGrid })
            {
                if (narrow) ResponsiveLayout.FoldColumns(bar, 1);
                else ResponsiveLayout.Restore(bar);
            }

            foreach (WrapPanel actions in new[] { RoofPartsActions, WallPartsActions, BasePartsActions })
            {
                DockPanel.SetDock(actions, narrow ? Dock.Bottom : Dock.Right);
                actions.Margin = narrow ? new Thickness(0, 6, 0, 0) : new Thickness(0);
            }

            foreach (DataGrid grid in new[] { RoofPartsGrid, WallPartsGrid, BasePartsGrid })
            {
                foreach (DataGridColumn column in grid.Columns)
                {
                    string header = column.Header as string ?? string.Empty;
                    if (header == "Description" || header == "Type" || header == "Checks")
                    {
                        column.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
                    }
                    else if (header == "Status" || header == "Expected" || header == "In part")
                    {
                        // Let the remaining columns share a ~400 px dock instead of scrolling sideways.
                        if (!_wideColumnSizes.ContainsKey(column)) _wideColumnSizes[column] = (column.Width, column.MinWidth);
                        (DataGridLength width, double minWidth) = _wideColumnSizes[column];
                        column.MinWidth = narrow ? 76 : minWidth;
                        column.Width = narrow && header == "Status" ? new DataGridLength(86) : width;
                    }
                }
            }
        }

        private readonly Dictionary<DataGridColumn, (DataGridLength Width, double MinWidth)> _wideColumnSizes =
            new Dictionary<DataGridColumn, (DataGridLength Width, double MinWidth)>();

        /// <summary>Called once when the view is closed for good, after <see cref="ConfirmClose"/>.</summary>
        public void Shutdown()
        {
            Dispatcher.UnhandledException -= OnDispatcherUnhandledException;
            try { _partPreviewWindow?.Close(); } catch { }
            InventorSelectionHelper.RestoreNormal(_inventorApp);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            DebugLogger.Log(e.Exception, "VerifierView (unhandled)");
            e.Handled = true;
            try { ShowNotice($"Something went wrong: {e.Exception.Message} (details in ucv_debug.txt)"); } catch { }
        }

        public void SetIptScanResult(IptScanResult iptResult)
        {
            _iptResult = iptResult;
            RefreshUnitStatus();
            UpdateRegenerateButton();
        }

        public void SetSnapshot(UnitSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        // ── Global Other Specs ────────────────────────────────────────────────

        private void PopulateGlobalSpecs()
        {
            _isUpdatingUi = true;
            try
            {
                var other = _data.OtherConstruction;
                LipCheckBox.IsChecked = other.UpturnedLip;
                LipHeightBox.Text = other.UpturnedLipHeight;
                LipHeightBox.Visibility = other.UpturnedLip ? Visibility.Visible : Visibility.Collapsed;

                CurbCheckBox.IsChecked = other.CurbRest;
                CurbHeightBox.Text = other.CurbRestHeight;
                CurbHeightBox.Visibility = other.CurbRest ? Visibility.Visible : Visibility.Collapsed;
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        private void OnLipCheckChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            _data.OtherConstruction.UpturnedLip = LipCheckBox.IsChecked == true;
            LipHeightBox.Visibility = _data.OtherConstruction.UpturnedLip ? Visibility.Visible : Visibility.Collapsed;
            MarkOverridesDirty();
        }

        private void OnLipHeightChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            _data.OtherConstruction.UpturnedLipHeight = LipHeightBox.Text;
            MarkOverridesDirty();
        }

        private void OnCurbCheckChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            _data.OtherConstruction.CurbRest = CurbCheckBox.IsChecked == true;
            CurbHeightBox.Visibility = _data.OtherConstruction.CurbRest ? Visibility.Visible : Visibility.Collapsed;
            MarkOverridesDirty();
        }

        private void OnCurbHeightChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            _data.OtherConstruction.CurbRestHeight = CurbHeightBox.Text;
            MarkOverridesDirty();
        }

        // ── Unsaved Work ──────────────────────────────────────────────────────

        private void MarkOverridesDirty()
        {
            _overridesDirty = true;
            UnsavedOverridesLabel.Visibility = Visibility.Visible;
            ScheduleStatusRefresh();
        }

        private void ClearOverridesDirty()
        {
            _overridesDirty = false;
            UnsavedOverridesLabel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Shows the surface's stored value in a dropdown even when it is not a standard option,
        /// so reading the dropdown back never replaces it with "".
        /// </summary>
        private static void ShowStoredValue(ComboBox combo, IReadOnlyList<string> standard, string? stored)
        {
            combo.ItemsSource = ExpectationChoices.WithStoredValue(standard, stored, out string? selected);
            combo.SelectedItem = selected;
        }

        // ── Roof Tab Event Handlers ───────────────────────────────────────────

        private void OnRoofSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = RoofSurfaceList.SelectedItem as RoofSurfaceRow;
            if (selected == null)
            {
                RoofDetailPanel.Visibility = Visibility.Collapsed;
                RoofPlaceholderText.Visibility = Visibility.Visible;
                return;
            }

            RoofPlaceholderText.Visibility = Visibility.Collapsed;
            RoofDetailPanel.Visibility = Visibility.Visible;

            _isUpdatingUi = true;
            try
            {
                RoofThicknessText.Text = selected.Thickness;
                ShowStoredValue(RoofExteriorGaugeCombo, _gauges, selected.ExteriorSkinGauge);
                ShowStoredValue(RoofExteriorMaterialCombo, _materials, selected.ExteriorSkinMaterial);
                ShowStoredValue(RoofInteriorGaugeCombo, _gauges, selected.InteriorLinerGauge);
                ShowStoredValue(RoofInteriorMaterialCombo, _materials, selected.InteriorLinerMaterial);
                ShowStoredValue(RoofChannelGaugeCombo, _gauges, selected.ChannelSkinGauge);
                ShowStoredValue(RoofChannelMaterialCombo, _materials, selected.ChannelSkinMaterial);
                ShowStoredValue(RoofTrimGaugeCombo, _gauges, selected.TrimSkinGauge);
                ShowStoredValue(RoofTrimMaterialCombo, _materials, selected.TrimSkinMaterial);
                RoofInsulationText.Text = selected.InsulationThicknessAndMaterial;

                ShowStoredValue(RoofThermalCombo, ExpectationChoices.YesNo, selected.ThermalBreak);
            }
            finally
            {
                _isUpdatingUi = false;
            }

            CaptureStackStateBeforeInventor();
            RefreshPartsGrid(selected);
            HighlightSurfaceInInventor(selected);
            TryRefreshWireframePreview();
            RestoreStackAfterInventor();
        }

        private void OnRoofExpectationsChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            var selected = RoofSurfaceList.SelectedItem as RoofSurfaceRow;
            if (selected == null) return;

            selected.Thickness = RoofThicknessText.Text;
            selected.ExteriorSkinGauge = RoofExteriorGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.ExteriorSkinMaterial = RoofExteriorMaterialCombo.SelectedItem as string ?? string.Empty;
            selected.InteriorLinerGauge = RoofInteriorGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.InteriorLinerMaterial = RoofInteriorMaterialCombo.SelectedItem as string ?? string.Empty;
            selected.ChannelSkinGauge = RoofChannelGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.ChannelSkinMaterial = RoofChannelMaterialCombo.SelectedItem as string ?? string.Empty;
            selected.TrimSkinGauge = RoofTrimGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.TrimSkinMaterial = RoofTrimMaterialCombo.SelectedItem as string ?? string.Empty;
            selected.InsulationThicknessAndMaterial = RoofInsulationText.Text;

            MarkOverridesDirty();
            RefreshPartsGrid(selected);
        }

        private void OnRoofThermalComboChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            var selected = RoofSurfaceList.SelectedItem as RoofSurfaceRow;
            if (selected == null) return;

            selected.ThermalBreak = RoofThermalCombo.SelectedItem as string ?? string.Empty;
            MarkOverridesDirty();
            RefreshPartsGrid(selected);
        }

        private static readonly ExpectationField<RoofSurfaceRow>[] RoofCopyFields =
        {
            new ExpectationField<RoofSurfaceRow>("Panel thickness", r => r.Thickness, (r, v) => r.Thickness = v),
            new ExpectationField<RoofSurfaceRow>("Exterior gauge", r => r.ExteriorSkinGauge, (r, v) => r.ExteriorSkinGauge = v),
            new ExpectationField<RoofSurfaceRow>("Exterior material", r => r.ExteriorSkinMaterial, (r, v) => r.ExteriorSkinMaterial = v),
            new ExpectationField<RoofSurfaceRow>("Interior gauge", r => r.InteriorLinerGauge, (r, v) => r.InteriorLinerGauge = v),
            new ExpectationField<RoofSurfaceRow>("Interior material", r => r.InteriorLinerMaterial, (r, v) => r.InteriorLinerMaterial = v),
            new ExpectationField<RoofSurfaceRow>("Channel gauge", r => r.ChannelSkinGauge, (r, v) => r.ChannelSkinGauge = v),
            new ExpectationField<RoofSurfaceRow>("Channel material", r => r.ChannelSkinMaterial, (r, v) => r.ChannelSkinMaterial = v),
            new ExpectationField<RoofSurfaceRow>("Trim gauge", r => r.TrimSkinGauge, (r, v) => r.TrimSkinGauge = v),
            new ExpectationField<RoofSurfaceRow>("Trim material", r => r.TrimSkinMaterial, (r, v) => r.TrimSkinMaterial = v),
            new ExpectationField<RoofSurfaceRow>("Insulation", r => r.InsulationThicknessAndMaterial, (r, v) => r.InsulationThicknessAndMaterial = v),
            new ExpectationField<RoofSurfaceRow>("Thermal break", r => r.ThermalBreak, (r, v) => r.ThermalBreak = v),
        };

        private void OnApplyRoofToAll(object sender, RoutedEventArgs e)
        {
            if (RoofSurfaceList.SelectedItem is RoofSurfaceRow selected)
                CopyToAll(selected, _data.RoofRows, RoofCopyFields, r => r.PartNumber, "roof", "roofs");
        }

        // ── Wall Tab Event Handlers ───────────────────────────────────────────

        private void OnWallSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = WallSurfaceList.SelectedItem as WallSurfaceRow;
            if (selected == null)
            {
                WallDetailPanel.Visibility = Visibility.Collapsed;
                WallPlaceholderText.Visibility = Visibility.Visible;
                return;
            }

            WallPlaceholderText.Visibility = Visibility.Collapsed;
            WallDetailPanel.Visibility = Visibility.Visible;

            _isUpdatingUi = true;
            try
            {
                WallThicknessText.Text = selected.Thickness;
                WallPaintText.Text = selected.ExteriorPaint;
                ShowStoredValue(WallExteriorGaugeCombo, _gauges, selected.ExteriorSkinGauge);
                ShowStoredValue(WallExteriorMaterialCombo, _materials, selected.ExteriorSkinMaterial);
                ShowStoredValue(WallInteriorGaugeCombo, _gauges, selected.InteriorLinerGauge);
                ShowStoredValue(WallInteriorMaterialCombo, _materials, selected.InteriorLinerMaterial);
                ShowStoredValue(WallChannelGaugeCombo, _gauges, selected.ChannelSkinGauge);
                ShowStoredValue(WallChannelMaterialCombo, _materials, selected.ChannelSkinMaterial);
                WallInsulationText.Text = selected.InsulationThicknessAndMaterial;

                ShowStoredValue(WallThermalCombo, ExpectationChoices.YesNo, selected.ThermalBreak);
            }
            finally
            {
                _isUpdatingUi = false;
            }

            CaptureStackStateBeforeInventor();
            RefreshPartsGrid(selected);
            HighlightSurfaceInInventor(selected);
            TryRefreshWireframePreview();
            RestoreStackAfterInventor();
        }

        private void OnWallExpectationsChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            var selected = WallSurfaceList.SelectedItem as WallSurfaceRow;
            if (selected == null) return;

            selected.Thickness = WallThicknessText.Text;
            selected.ExteriorPaint = WallPaintText.Text;
            selected.ExteriorSkinGauge = WallExteriorGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.ExteriorSkinMaterial = WallExteriorMaterialCombo.SelectedItem as string ?? string.Empty;
            selected.InteriorLinerGauge = WallInteriorGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.InteriorLinerMaterial = WallInteriorMaterialCombo.SelectedItem as string ?? string.Empty;
            selected.ChannelSkinGauge = WallChannelGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.ChannelSkinMaterial = WallChannelMaterialCombo.SelectedItem as string ?? string.Empty;
            selected.InsulationThicknessAndMaterial = WallInsulationText.Text;

            MarkOverridesDirty();
            RefreshPartsGrid(selected);
        }

        private void OnWallThermalComboChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            var selected = WallSurfaceList.SelectedItem as WallSurfaceRow;
            if (selected == null) return;

            selected.ThermalBreak = WallThermalCombo.SelectedItem as string ?? string.Empty;
            MarkOverridesDirty();
            RefreshPartsGrid(selected);
        }

        private static readonly ExpectationField<WallSurfaceRow>[] WallCopyFields =
        {
            new ExpectationField<WallSurfaceRow>("Panel thickness", r => r.Thickness, (r, v) => r.Thickness = v),
            new ExpectationField<WallSurfaceRow>("Exterior paint", r => r.ExteriorPaint, (r, v) => r.ExteriorPaint = v),
            new ExpectationField<WallSurfaceRow>("Exterior gauge", r => r.ExteriorSkinGauge, (r, v) => r.ExteriorSkinGauge = v),
            new ExpectationField<WallSurfaceRow>("Exterior material", r => r.ExteriorSkinMaterial, (r, v) => r.ExteriorSkinMaterial = v),
            new ExpectationField<WallSurfaceRow>("Interior gauge", r => r.InteriorLinerGauge, (r, v) => r.InteriorLinerGauge = v),
            new ExpectationField<WallSurfaceRow>("Interior material", r => r.InteriorLinerMaterial, (r, v) => r.InteriorLinerMaterial = v),
            new ExpectationField<WallSurfaceRow>("Channel gauge", r => r.ChannelSkinGauge, (r, v) => r.ChannelSkinGauge = v),
            new ExpectationField<WallSurfaceRow>("Channel material", r => r.ChannelSkinMaterial, (r, v) => r.ChannelSkinMaterial = v),
            new ExpectationField<WallSurfaceRow>("Insulation", r => r.InsulationThicknessAndMaterial, (r, v) => r.InsulationThicknessAndMaterial = v),
            new ExpectationField<WallSurfaceRow>("Thermal break", r => r.ThermalBreak, (r, v) => r.ThermalBreak = v),
        };

        private void OnApplyWallToAll(object sender, RoutedEventArgs e)
        {
            if (WallSurfaceList.SelectedItem is WallSurfaceRow selected)
                CopyToAll(selected, _data.WallRows, WallCopyFields, r => r.PartNumber, "wall", "walls");
        }

        // ── Base Tab Event Handlers ───────────────────────────────────────────

        private void OnBaseSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = BaseSurfaceList.SelectedItem as BaseSurfaceRow;
            if (selected == null)
            {
                BaseDetailPanel.Visibility = Visibility.Collapsed;
                BasePlaceholderText.Visibility = Visibility.Visible;
                return;
            }

            BasePlaceholderText.Visibility = Visibility.Collapsed;
            BaseDetailPanel.Visibility = Visibility.Visible;

            _isUpdatingUi = true;
            try
            {
                ShowStoredValue(BaseHeightCombo, ExpectationChoices.BaseHeights, selected.BaseHeight);
                ShowStoredValue(BaseMatCombo, ExpectationChoices.BaseMaterials, selected.BaseMaterial);
                ShowStoredValue(FormedChannelGaugeCombo, _gauges, selected.FormedChannelGauge);
                ShowStoredValue(FormedChannelMaterialCombo, _materials, selected.FormedChannelMaterial);
                ShowStoredValue(FloorGaugeCombo, _gauges, selected.FloorGauge);
                ShowStoredValue(FloorMaterialCombo, _materials, selected.FloorMaterial);
                ShowStoredValue(SubFloorGaugeCombo, _gauges, selected.SubFloorGauge);
                ShowStoredValue(SubFloorMaterialCombo, _materials, selected.SubFloorMaterial);
                ShowStoredValue(PerimeterAngleGaugeCombo, _gauges, selected.PerimeterAngleGauge);
                ShowStoredValue(PerimeterAngleMaterialCombo, _materials, selected.PerimeterAngleMaterial);

                ShowStoredValue(FloorThermalCombo, ExpectationChoices.YesNo, selected.FloorThermalBreak);
            }
            finally
            {
                _isUpdatingUi = false;
            }

            CaptureStackStateBeforeInventor();
            RefreshPartsGrid(selected);
            HighlightSurfaceInInventor(selected);
            TryRefreshWireframePreview();
            RestoreStackAfterInventor();
        }

        private void UpdateBaseExpectations()
        {
            var selected = BaseSurfaceList.SelectedItem as BaseSurfaceRow;
            if (selected == null) return;

            selected.BaseHeight = BaseHeightCombo.SelectedItem as string ?? string.Empty;
            selected.BaseMaterial = BaseMatCombo.SelectedItem as string ?? string.Empty;
            selected.FormedChannelGauge = FormedChannelGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.FormedChannelMaterial = FormedChannelMaterialCombo.SelectedItem as string ?? string.Empty;
            selected.FloorGauge = FloorGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.FloorMaterial = FloorMaterialCombo.SelectedItem as string ?? string.Empty;
            selected.SubFloorGauge = SubFloorGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.SubFloorMaterial = SubFloorMaterialCombo.SelectedItem as string ?? string.Empty;
            selected.PerimeterAngleGauge = PerimeterAngleGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.PerimeterAngleMaterial = PerimeterAngleMaterialCombo.SelectedItem as string ?? string.Empty;

            MarkOverridesDirty();
            RefreshPartsGrid(selected);
        }

        private void OnBaseExpectationsChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            UpdateBaseExpectations();
        }

        private void OnBaseExpectationsComboChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            UpdateBaseExpectations();
        }

        private void OnFloorThermalComboChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            var selected = BaseSurfaceList.SelectedItem as BaseSurfaceRow;
            if (selected == null) return;

            selected.FloorThermalBreak = FloorThermalCombo.SelectedItem as string ?? string.Empty;
            MarkOverridesDirty();
            RefreshPartsGrid(selected);
        }

        // ── Parts Grids Rendering & Live Verification ─────────────────────────

        private void RefreshPartsGrid(object surfaceRow)
        {
            if (_iptResult == null) return;

            _isRefreshingPartsGrid = true;
            try
            {
                RefreshPartsGridCore(surfaceRow);
            }
            finally
            {
                _isRefreshingPartsGrid = false;
            }
        }

        private void RefreshPartsGridCore(object surfaceRow)
        {
            if (surfaceRow is RoofSurfaceRow roof)
                ShowPartsRows(RoofPartsGrid, BuildPartsGridRows(roof.SourceSurfaceIam, "Roof", VerificationEngine.BuildRowFields(roof)));
            else if (surfaceRow is WallSurfaceRow wall)
                ShowPartsRows(WallPartsGrid, BuildPartsGridRows(wall.SourceSurfaceIam, "Wall", VerificationEngine.BuildRowFields(wall)));
            else if (surfaceRow is BaseSurfaceRow bs)
                ShowPartsRows(BasePartsGrid, BuildPartsGridRows(bs.SourceSurfaceIam, "Base", VerificationEngine.BuildRowFields(bs)));
        }

        private void ShowPartsRows(DataGrid grid, List<IptVerificationRow> rows)
        {
            grid.ItemsSource = rows;
            ApplyPartsFilter(grid);

            var (all, issues, notChecked) = FilterChipsFor(grid);
            all.Content = $"All {rows.Count}";
            issues.Content = $"Issues {rows.Count(r => r.IsMismatch)}";
            notChecked.Content = $"Not checked {rows.Count(r => r.IsNotChecked)}";
            UpdatePreviewButtons();
        }

        private (RadioButton All, RadioButton Issues, RadioButton NotChecked) FilterChipsFor(DataGrid grid)
        {
            if (ReferenceEquals(grid, WallPartsGrid)) return (WallFilterAll, WallFilterIssues, WallFilterNotChecked);
            if (ReferenceEquals(grid, BasePartsGrid)) return (BaseFilterAll, BaseFilterIssues, BaseFilterNotChecked);
            return (RoofFilterAll, RoofFilterIssues, RoofFilterNotChecked);
        }

        private void ApplyPartsFilter(DataGrid grid)
        {
            if (grid.ItemsSource == null) return;

            string filter = _partsFilters.TryGetValue(grid, out string? f) ? f : "All";
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(grid.ItemsSource);
            view.Filter = filter switch
            {
                "Issues" => item => item is IptVerificationRow r && (r.IsMismatch || r.IsEditPending),
                "NotChecked" => item => item is IptVerificationRow r && r.IsNotChecked,
                _ => null
            };
        }

        private void OnPartsFilterChanged(object sender, RoutedEventArgs e)
        {
            // Checked fires while the XAML loads, before the grids exist.
            if (!IsInitialized || sender is not RadioButton chip || chip.Tag is not string filter) return;

            DataGrid grid = chip.GroupName switch
            {
                "WallFilter" => WallPartsGrid,
                "BaseFilter" => BasePartsGrid,
                _ => RoofPartsGrid
            };

            _partsFilters[grid] = filter;
            ApplyPartsFilter(grid);
        }

        /// <summary>
        /// One grid row per part in the surface, using the same check as Run Verification.
        /// Parts no rule covers are left out; parts a rule covers but that could not be checked are shown.
        /// </summary>
        private List<IptVerificationRow> BuildPartsGridRows(string surfaceIam, string surfaceKind, Dictionary<string, string> rowFields)
        {
            var gridRows = new List<IptVerificationRow>();
            var parts = _iptResult!.Parts
                .Where(p => string.Equals(p.OwnerIamPath, surfaceIam, StringComparison.OrdinalIgnoreCase));

            foreach (var p in parts)
            {
                var check = VerificationEngine.CheckPart(p, rowFields, surfaceKind);
                if (check.Status == PartCheckStatus.NotInConfig) continue;

                string description = MaterialsConfig.GetPartDescription(p.ModelNumber, p.Description);
                AddGridRow(gridRows, p.PartNumber, description, check.Classification, check.FieldName,
                    check.ExpectedValue, check.ActualValue, check.Status, check.Reason, check.IsModified, p.FilePath);
            }

            return gridRows;
        }

        // ── Save & Verify Actions ─────────────────────────────────────────────

        private void OnSave(object sender, RoutedEventArgs e)
        {
            PersistenceManager.SaveOverrides(_iamPath, _data);
            ClearOverridesDirty();
            ShowNotice($"Expectations saved to {Path.GetFileName(PersistenceManager.GetSidecarPath(_iamPath))}.");
        }

        private void OnSaveSnapshot(object sender, RoutedEventArgs e)
        {
            if (_snapshot is null)
            {
                Dialogs.Show(this, "Scan data not available — cannot save a snapshot.",
                    "Save Snapshot", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string defaultPath = SnapshotSerializer.GetDefaultPath(_iamPath);
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title            = "Save Unit Snapshot",
                FileName         = Path.GetFileName(defaultPath),
                InitialDirectory = Path.GetDirectoryName(defaultPath),
                Filter           = "Unit snapshot (*" + SnapshotSerializer.FileSuffix + ")|*" + SnapshotSerializer.FileSuffix,
            };
            if (Dialogs.ShowDialog(this, dialog) != true) return;

            try
            {
                SnapshotSerializer.Save(dialog.FileName, _snapshot);
                Dialogs.Show(this, $"Snapshot saved to:\n{dialog.FileName}\n\nCopy it into UnitConstructionVerifier.Tests\\Snapshots to use it as a regression baseline.",
                    "Save Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DebugLogger.Log(ex, "OnSaveSnapshot");
                Dialogs.Show(this, $"Could not save snapshot:\n{ex.Message}",
                    "Save Snapshot", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnVerify(object sender, RoutedEventArgs e)
        {
            RefreshUnitStatus();
            UnitStatus status = _statusBoard.Status;

            CheckBannerText.Text = status.BannerText();
            CheckBanner.Background = (Brush)FindResource(status.Mismatches > 0 ? "FailTint" : status.Scanned ? "PassTint" : "MutedTint");
            BannerNextIssueButton.Visibility = status.Mismatches > 0 ? Visibility.Visible : Visibility.Collapsed;
            CheckBanner.Visibility = Visibility.Visible;

            if (RoofSurfaceList.SelectedItem != null) RefreshPartsGrid(RoofSurfaceList.SelectedItem);
            if (WallSurfaceList.SelectedItem != null) RefreshPartsGrid(WallSurfaceList.SelectedItem);
            if (BaseSurfaceList.SelectedItem != null) RefreshPartsGrid(BaseSurfaceList.SelectedItem);
        }

        private void OnCloseBannerClick(object sender, RoutedEventArgs e)
        {
            CheckBanner.Visibility = Visibility.Collapsed;
        }

        // ── Unit Status ───────────────────────────────────────────────────────

        private void ScheduleStatusRefresh()
        {
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        /// <summary>Re-runs every check and updates the header pill, tab badges and surface dots.</summary>
        private void RefreshUnitStatus()
        {
            _statusTimer.Stop();
            UnitStatus status = _iptResult is null
                ? UnitStatus.Empty
                : UnitStatus.From(new VerificationEngine(_data, _iptResult).Run());
            _statusBoard.Update(status);

            OverallStatusText.Text = status.HeaderText();
            string tone = !status.Scanned ? "Muted" : status.Mismatches > 0 ? "Fail" : "Pass";
            OverallStatusPill.Background = (Brush)FindResource(tone + "Tint");
            OverallStatusText.Foreground = (Brush)FindResource(tone switch
            {
                "Fail" => "AccentRed",
                "Pass" => "AccentGreen",
                _ => "TextSecondary"
            });

            NotCheckedText.Text = $"{status.NotChecked} not checked";
            NotCheckedPill.Visibility = status.NotChecked > 0 ? Visibility.Visible : Visibility.Collapsed;
            NextIssueButton.Visibility = status.Mismatches > 0 ? Visibility.Visible : Visibility.Collapsed;

            SetTabBadge(RoofTabBadge, RoofTabBadgeText, status, _data.RoofRows.Select(r => r.SourceSurfaceIam));
            SetTabBadge(WallTabBadge, WallTabBadgeText, status, _data.WallRows.Select(r => r.SourceSurfaceIam));
            SetTabBadge(BaseTabBadge, BaseTabBadgeText, status, _data.BaseRows.Select(r => r.SourceSurfaceIam));
        }

        private void SetTabBadge(Border badge, TextBlock text, UnitStatus status, IEnumerable<string> surfaceIams)
        {
            var iams = surfaceIams.ToList();
            if (!status.Scanned || iams.Count == 0)
            {
                badge.Visibility = Visibility.Collapsed;
                return;
            }

            int mismatches = status.MismatchesIn(iams);
            badge.Visibility = Visibility.Visible;
            text.Text = mismatches > 0 ? mismatches.ToString(CultureInfo.InvariantCulture) : "✓";
            badge.Background = (Brush)FindResource(mismatches > 0 ? "FailTint" : "PassTint");
            text.Foreground = (Brush)FindResource(mismatches > 0 ? "AccentRed" : "AccentGreen");
        }

        private void OnNextIssueClick(object sender, RoutedEventArgs e) => GoToNextIssue();

        /// <summary>Selects the next surface, in tab order after the current one, that has a mismatch.</summary>
        private void GoToNextIssue()
        {
            UnitStatus status = _statusBoard.Status;
            var lists = new[] { RoofSurfaceList, WallSurfaceList, BaseSurfaceList };
            var stops = new List<(int Tab, object Row)>();
            for (int tab = 0; tab < lists.Length; tab++)
            {
                foreach (object row in lists[tab].Items)
                {
                    if (status.For(SurfaceStatusBoard.SurfaceIamOf(row)).Mismatches > 0)
                        stops.Add((tab, row));
                }
            }

            if (stops.Count == 0)
            {
                ShowNotice("No mismatches left on this unit.");
                return;
            }

            int currentTab = Math.Max(0, MainTabControl.SelectedIndex);
            object? current = lists[currentTab].SelectedItem;
            int at = stops.FindIndex(s => s.Tab == currentTab && ReferenceEquals(s.Row, current));
            int next = at >= 0 ? (at + 1) % stops.Count : Math.Max(0, stops.FindIndex(s => s.Tab >= currentTab));
            (int nextTab, object nextRow) = stops[next];

            MainTabControl.SelectedIndex = nextTab;
            ListBox list = lists[nextTab];
            list.SelectedItem = nextRow;
            list.ScrollIntoView(nextRow);

            DataGrid grid = new[] { RoofPartsGrid, WallPartsGrid, BasePartsGrid }[nextTab];
            var firstIssue = (grid.ItemsSource as IEnumerable<IptVerificationRow>)?.FirstOrDefault(r => r.IsMismatch);
            if (firstIssue != null)
            {
                grid.SelectedItem = firstIssue;
                grid.ScrollIntoView(firstIssue);
            }
        }

        // ── Notices ───────────────────────────────────────────────────────────

        /// <summary>Shows a short confirmation in the footer instead of a dialog.</summary>
        private void ShowNotice(string message)
        {
            NoticeText.Text = message;
            _noticeTimer.Stop();
            _noticeTimer.Start();
        }

        /// <summary>
        /// Copies one surface's expectations to the others of its kind. The prompt lists exactly what changes;
        /// when every other surface already matches there is nothing to ask.
        /// </summary>
        private void CopyToAll<T>(T source, IList<T> rows, IReadOnlyList<ExpectationField<T>> fields, Func<T, string> name,
            string one, string many) where T : class
        {
            int others = rows.Count - 1;
            if (others <= 0)
            {
                ShowNotice($"There are no other {many} to copy to.");
                return;
            }

            List<SurfaceChange<T>> changes = CopyToAllPreview.Diff(source, rows, fields);
            if (changes.Count == 0)
            {
                ShowNotice($"The other {(others == 1 ? one : many)} already match this {one}.");
                return;
            }

            int matching = others - changes.Count;
            string intro = $"Copy this {one}'s expectations to {changes.Count} other {(changes.Count == 1 ? one : many)}?" +
                (matching > 0 ? $" ({matching} already match.)" : string.Empty);
            if (Dialogs.Show(this, intro + "\n\n" + CopyToAllPreview.Describe(changes, name) + "\n\nNothing is written to part files.",
                    "Copy Expectations", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            CopyToAllPreview.Apply(source, changes, fields);
            MarkOverridesDirty();
            ShowNotice($"Copied expectations from {name(source)} to {changes.Count} other {(changes.Count == 1 ? one : many)}.");
        }

        // ── Edit Mode Actions & Handlers ─────────────────────────────────────

        private void AddGridRow(
            List<IptVerificationRow> gridRows,
            string partNumber,
            string description,
            string partType,
            string parameter,
            string expected,
            string actual,
            PartCheckStatus checkStatus,
            string reason,
            bool isModified,
            string filePath)
        {
            var row = new IptVerificationRow
            {
                PartNumber = partNumber,
                Description = description,
                PartType = partType,
                Parameter = parameter,
                Expected = expected,
                Actual = actual,
                CheckStatus = checkStatus,
                Reason = reason,
                IsModified = isModified,
                FilePath = filePath,
                IsWritable = PartPropertyEditsMapper.CanWrite(parameter)
            };
            row.PropertyChanged += OnRowPropertyChanged;

            if (_pendingEdits.TryGetValue(StagedEdit.KeyFor(row.FilePath, row.Parameter), out StagedEdit pending))
            {
                row.NewValue = pending.NewValue;
            }

            gridRows.Add(row);
        }

        private void OnRowPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is IptVerificationRow row && e.PropertyName == nameof(IptVerificationRow.NewValue))
            {
                string key = StagedEdit.KeyFor(row.FilePath, row.Parameter);
                if (row.IsEditPending && row.IsWritable)
                {
                    _pendingEdits[key] = new StagedEdit(row.FilePath, row.Parameter, row.PartNumber, row.Actual, row.NewValue);
                }
                else
                {
                    _pendingEdits.Remove(key);
                }

                UpdateEditFooter();
            }
        }

        private void OnEditModeChanged(object sender, RoutedEventArgs e)
        {
            bool isEdit = EditModeCheckBox.IsChecked == true;

            RoofActualColumn.IsReadOnly = !isEdit;
            WallActualColumn.IsReadOnly = !isEdit;
            BaseActualColumn.IsReadOnly = !isEdit;

            RoofSyncButtonsPanel.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;
            WallSyncButtonsPanel.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;
            BaseSyncButtonsPanel.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;

            UpdateEditFooter();
        }

        /// <summary>
        /// The staging tray stays visible while Edit parts is on or any edit is staged, so turning the
        /// switch off never hides edits that still need writing or discarding.
        /// </summary>
        private void UpdateEditFooter()
        {
            int staged = _pendingEdits.Count;
            EditModeFooterPanel.Visibility = EditModeCheckBox.IsChecked == true || staged > 0 ? Visibility.Visible : Visibility.Collapsed;
            StagedEditsText.Text = staged == 0
                ? "No part changes staged"
                : staged == 1 ? "1 part change staged" : $"{staged} part changes staged";
        }

        private void OnSyncSelectedRoof(object sender, RoutedEventArgs e) => SyncSelectedRows(RoofPartsGrid);
        private void OnSyncAllMismatchesRoof(object sender, RoutedEventArgs e) => SyncAllMismatches(RoofPartsGrid);
        private void OnSyncSelectedWall(object sender, RoutedEventArgs e) => SyncSelectedRows(WallPartsGrid);
        private void OnSyncAllMismatchesWall(object sender, RoutedEventArgs e) => SyncAllMismatches(WallPartsGrid);
        private void OnSyncSelectedBase(object sender, RoutedEventArgs e) => SyncSelectedRows(BasePartsGrid);
        private void OnSyncAllMismatchesBase(object sender, RoutedEventArgs e) => SyncAllMismatches(BasePartsGrid);

        private void SyncSelectedRows(DataGrid grid)
        {
            if (grid.SelectedItems == null || grid.SelectedItems.Count == 0) return;

            var selectedRows = grid.SelectedItems.Cast<IptVerificationRow>().ToList();
            foreach (var row in selectedRows)
            {
                // Not-checked rows have no expected value to sync to.
                if (row != null && row.IsWritable && !string.IsNullOrWhiteSpace(row.Expected))
                {
                    row.NewValue = row.Expected;
                }
            }
        }

        private void SyncAllMismatches(DataGrid grid)
        {
            if (grid.ItemsSource is IEnumerable<IptVerificationRow> rows)
            {
                foreach (var row in rows)
                {
                    if (row.IsMismatch && row.IsWritable)
                    {
                        row.NewValue = row.Expected;
                    }
                }
            }
        }

        private void OnCancelChanges(object sender, RoutedEventArgs e)
        {
            if (_pendingEdits.Count == 0) return;

            var result = Dialogs.Show(this, $"Discard {_pendingEdits.Count} staged part change(s)? Nothing has been written to the part files.",
                "Discard Changes", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _pendingEdits.Clear();
                if (RoofSurfaceList.SelectedItem != null) RefreshPartsGrid(RoofSurfaceList.SelectedItem);
                if (WallSurfaceList.SelectedItem != null) RefreshPartsGrid(WallSurfaceList.SelectedItem);
                if (BaseSurfaceList.SelectedItem != null) RefreshPartsGrid(BaseSurfaceList.SelectedItem);
                UpdateEditFooter();
                ShowNotice("Staged part changes discarded.");
            }
        }

        private void OnWriteChanges(object sender, RoutedEventArgs e)
        {
            // Force commit any active cell edits in all grids
            try
            {
                RoofPartsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                RoofPartsGrid.CommitEdit(DataGridEditingUnit.Row, true);
                WallPartsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                WallPartsGrid.CommitEdit(DataGridEditingUnit.Row, true);
                BasePartsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                BasePartsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            }
            catch {}

            if (_pendingEdits.Count == 0)
            {
                ShowNotice("No part changes are staged.");
                return;
            }

            _reviewItems = WriteReview.BuildItems(_pendingEdits.Values);
            foreach (WriteReviewItem item in _reviewItems)
            {
                item.PropertyChanged += (_, __) => UpdateWriteReview();
            }

            WriteReviewList.ItemsSource = _reviewItems;
            UpdateWriteReview();
            WriteReviewOverlay.Visibility = Visibility.Visible;
            WriteReviewCancelButton.Focus();
        }

        private void UpdateWriteReview()
        {
            int ticked = _reviewItems.Count(i => i.IsIncluded);
            WriteReviewSummary.Text = WriteReview.Summary(_reviewItems);
            WriteReviewConfirmButton.Content = ticked == 0 ? "Write changes" : $"Write {WriteReview.Plural(ticked, "change")}";
            WriteReviewConfirmButton.IsEnabled = ticked > 0;
        }

        private void CloseWriteReview()
        {
            WriteReviewOverlay.Visibility = Visibility.Collapsed;
            WriteReviewList.ItemsSource = null;
        }

        private void OnWriteReviewCancel(object sender, RoutedEventArgs e) => CloseWriteReview();

        private void OnWriteReviewConfirm(object sender, RoutedEventArgs e)
        {
            var included = _reviewItems.Where(i => i.IsIncluded).ToList();
            CloseWriteReview();
            WriteStagedEdits(included);
        }

        /// <summary>Writes the ticked changes in one Inventor transaction; unticked ones stay staged.</summary>
        private void WriteStagedEdits(List<WriteReviewItem> included)
        {
            Dictionary<string, Operations.PartPropertyEdits> groupedEdits = WriteReview.GroupByFile(included);
            if (groupedEdits.Count == 0)
            {
                ShowNotice("Nothing ticked maps to a writable part property. Nothing was written.");
                return;
            }

            // Pre-flight read-only check
            var lockedFiles = new List<string>();
            foreach (var path in groupedEdits.Keys)
            {
                if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    if (fi.IsReadOnly)
                    {
                        lockedFiles.Add(Path.GetFileName(path));
                    }
                }
            }

            if (lockedFiles.Count > 0)
            {
                var lockSb = new System.Text.StringBuilder();
                lockSb.AppendLine("Cannot proceed. The following files are read-only (possibly checked in to Vault):");
                foreach (var lf in lockedFiles)
                {
                    lockSb.AppendLine($" - {lf}");
                }
                lockSb.AppendLine("\nPlease check them out of Vault first.");
                Dialogs.Show(this, lockSb.ToString(), "Files Locked", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Start Inventor Transaction
            Inventor.Transaction trans = null;
            try
            {
                Inventor._Document? transactionDoc = (Inventor._Document?)FindUnitDocument() ?? _inventorApp.ActiveDocument;
                if (transactionDoc != null)
                {
                    trans = _inventorApp.TransactionManager.StartTransaction(transactionDoc, "UCV Edit Mode Sync");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start transaction: {ex.Message}");
            }

            var writer = new Operations.IptPropertyWriter(_inventorApp);
            int successCount = 0;
            var errorList = new List<string>();
            var partWrites = new List<PartWriteRecord>();

            int fileNumber = 0;
            ShowBusy("Writing part changes…");

            try
            {
                foreach (var kvp in groupedEdits)
                {
                    string filePath = kvp.Key;
                    Operations.PartPropertyEdits edits = kvp.Value;

                    // Show which part is being written before Inventor blocks on it.
                    fileNumber++;
                    UpdateBusy($"{fileNumber} of {groupedEdits.Count}: {Path.GetFileName(filePath)}", (fileNumber - 1.0) / groupedEdits.Count);

                    if (writer.UpdatePartProperties(filePath, edits, out string err))
                    {
                        successCount++;
                        if (writer.LastWrite is PartWriteRecord write) partWrites.Add(write);
                    }
                    else
                    {
                        errorList.Add($"{Path.GetFileName(filePath)}: {err}");
                    }
                }

                if (errorList.Count > 0)
                {
                    if (trans != null)
                    {
                        trans.Abort();
                        trans = null;
                    }

                    var errSb = new System.Text.StringBuilder();
                    errSb.AppendLine("Failed to write changes. Transaction rolled back.");
                    foreach (var err in errorList)
                    {
                        errSb.AppendLine($" - {err}");
                    }
                    Dialogs.Show(this, errSb.ToString(), "Error Writing Changes", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    if (trans != null)
                    {
                        trans.End();
                        trans = null;
                    }

                    // Lets Regenerate changed tell these gauge / material edits from channel reshapes.
                    try { PartWriteStore.Record(_iamPath, partWrites); }
                    catch (Exception ex) { DebugLogger.Log(ex, "VerifierView: recording part writes"); }

                    foreach (WriteReviewItem item in included)
                    {
                        _pendingEdits.Remove(item.Edit.Key);
                    }
                    UpdateEditFooter();

                    string stillStaged = _pendingEdits.Count == 0 ? string.Empty
                        : $" {WriteReview.Plural(_pendingEdits.Count, "change")} still staged.";
                    UpdateBusy($"Re-checking {Path.GetFileName(_iamPath)}…", 1);
                    ReScanAndRefresh();
                    ShowNotice($"Updated {WriteReview.Plural(successCount, "part file")}. Re-checked the unit.{stillStaged}");
                }
            }
            catch (Exception ex)
            {
                if (trans != null)
                {
                    try { trans.Abort(); } catch {}
                }
                Dialogs.Show(this, $"Unexpected error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BusyOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void ReScanAndRefresh()
        {
            if (string.IsNullOrEmpty(_iamPath) || FindUnitDocument() is not Inventor.AssemblyDocument asmDoc)
            {
                ShowNotice($"{Path.GetFileName(_iamPath)} is not open in Inventor, so it was not re-checked.");
                return;
            }

            try
            {
                var iptReader = new Extraction.IptPropertyReader();
                var newIptResult = iptReader.ScanAssembly(asmDoc);
                SetIptScanResult(newIptResult);

                // Refresh the active grid
                if (RoofSurfaceList.SelectedItem != null) RefreshPartsGrid(RoofSurfaceList.SelectedItem);
                if (WallSurfaceList.SelectedItem != null) RefreshPartsGrid(WallSurfaceList.SelectedItem);
                if (BaseSurfaceList.SelectedItem != null) RefreshPartsGrid(BaseSurfaceList.SelectedItem);
            }
            catch (Exception ex)
            {
                Dialogs.Show(this, $"Failed to re-scan assembly after update: {ex.Message}", "Error Refreshing", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// The unit this verifier checks, if it is open in Inventor. Never just the active assembly: the user may have
        /// moved Inventor to another unit, and re-scanning that one would load its parts into this verifier.
        /// </summary>
        private Inventor.AssemblyDocument? FindUnitDocument()
        {
            try
            {
                return _inventorApp.Documents.get_ItemByName(_iamPath) as Inventor.AssemblyDocument;
            }
            catch
            {
                return null; // not open
            }
        }

        private void ShowBusy(string title)
        {
            BusyTitle.Text = title;
            BusyDetail.Text = string.Empty;
            BusyProgress.Value = 0;
            BusyOverlay.Visibility = Visibility.Visible;
        }

        /// <summary>Updates the progress panel and lets WPF draw it before the next blocking Inventor call.</summary>
        private void UpdateBusy(string detail, double fraction)
        {
            BusyDetail.Text = detail;
            BusyProgress.Value = fraction;
            // Render runs above Background priority, so this returns after the panel has been redrawn.
            Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
        }

        // ── Regenerate changed (Docs/batch_regenerate_upload_plan.md) ──────────

        /// <summary>Shows how many surfaces need Regenerate, from the last scan and UCV's own records.</summary>
        private void UpdateRegenerateButton()
        {
            try
            {
                int needed = _iptResult is null || string.IsNullOrEmpty(_iamPath) ? 0
                    : RegenerateReview.CountNeeded(_iptResult.Surfaces, ChannelFingerprintStore.Load(_iamPath), PartWriteStore.Load(_iamPath));
                RegenerateChangedButton.Content = needed > 0 ? $"Regenerate changed ({needed})…" : "Regenerate changed…";
            }
            catch (Exception ex)
            {
                DebugLogger.Log(ex, "VerifierView.UpdateRegenerateButton");
            }
        }

        private void OnRegenerateChanged(object sender, RoutedEventArgs e)
        {
            if (_iptResult is null || _iptResult.Surfaces.Count == 0)
            {
                ShowNotice("Re-check the unit first: there is no surface scan to decide from.");
                return;
            }

            if (_pendingEdits.Count > 0)
            {
                Dialogs.Show(this, "Write or discard the staged part changes first. Regenerate replaces channel parts, so staged edits could land on the wrong files.",
                    "Regenerate changed", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var trace = new List<string>();
            _ycShell = YcShellBridge.Connect(_inventorApp, trace, out string problem, out IReadOnlyList<MemberCheck> checks);
            if (_ycShell is null)
            {
                DebugLogger.Log("Regenerate changed: YCShell not reached. " + problem + Environment.NewLine +
                                string.Join(Environment.NewLine, trace) + Environment.NewLine + string.Join(Environment.NewLine, checks));
                Dialogs.Show(this, $"{problem}\n\nRegenerate changed needs the JCI MOM (YCShell) add-in loaded and logged in. Details are in the UCV debug log.",
                    "Regenerate changed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // The scan can be stale: channels patterned since the last Re-check would be missed (field test, 2026-10-23).
            List<SurfaceChangeState> current = ReadSurfaceChangesNow(_iptResult.Surfaces);

            IReadOnlyList<YcSurface> loaded = _ycShell.GetOpenSurfaces().Where(s => s.IsLoaded && s.IsComposite != true).ToList();
            _regenerateItems = RegenerateReview.BuildItems(
                current,
                ChannelFingerprintStore.Load(_iamPath),
                PartWriteStore.Load(_iamPath),
                path => loaded.Any(s => s.Matches(path)));
            foreach (RegenerateReviewItem item in _regenerateItems)
            {
                item.PropertyChanged += (_, __) => UpdateRegenerateReview();
            }

            RegenerateReviewList.ItemsSource = _regenerateItems;
            UpdateRegenerateReview();
            RegenerateReviewOverlay.Visibility = Visibility.Visible;
            RegenerateReviewCancelButton.Focus();
        }

        /// <summary>
        /// Re-reads YCShell's markers from each surface open in Inventor, keeping the scanned state for any that is not.
        /// Also refreshes the scan result and the button count.
        /// </summary>
        private List<SurfaceChangeState> ReadSurfaceChangesNow(IReadOnlyList<SurfaceChangeState> scanned)
        {
            var current = new List<SurfaceChangeState>(scanned.Count);
            ShowBusy("Checking surfaces for channel changes…");
            try
            {
                for (int i = 0; i < scanned.Count; i++)
                {
                    SurfaceChangeState state = scanned[i];
                    UpdateBusy(Path.GetFileNameWithoutExtension(state.SurfaceIamPath), (double)i / scanned.Count);
                    try
                    {
                        current.Add(OpenAssembly(state.SurfaceIamPath) is Inventor.AssemblyDocument doc
                            ? Extraction.SurfaceChangeReader.Read(doc)
                            : state);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log(ex, $"ReadSurfaceChangesNow: {state.SurfaceIamPath}");
                        current.Add(state);
                    }
                }
            }
            finally
            {
                BusyOverlay.Visibility = Visibility.Collapsed;
            }

            if (_iptResult is not null)
            {
                _iptResult.Surfaces = current;
                UpdateRegenerateButton();
            }

            return current;
        }

        private void UpdateRegenerateReview()
        {
            int ticked = _regenerateItems.Count(i => i.IsIncluded);
            RegenerateReviewSummary.Text = RegenerateReview.Summary(_regenerateItems);
            RegenerateReviewConfirmButton.Content = ticked == 0 ? "Regenerate" : $"Regenerate {WriteReview.Plural(ticked, "surface")}";
            RegenerateReviewConfirmButton.IsEnabled = ticked > 0;
        }

        private void CloseRegenerateReview()
        {
            RegenerateReviewOverlay.Visibility = Visibility.Collapsed;
            RegenerateReviewList.ItemsSource = null;
        }

        private void OnRegenerateReviewCancel(object sender, RoutedEventArgs e) => CloseRegenerateReview();

        private void OnRegenerateReviewConfirm(object sender, RoutedEventArgs e)
        {
            List<string> surfaces = _regenerateItems.Where(i => i.IsIncluded).Select(i => i.SurfaceIamPath).ToList();
            bool autoClose = RegenerateAutoCloseCheck.IsChecked == true;
            CloseRegenerateReview();
            if (surfaces.Count > 0 && _ycShell is not null) RunRegenerateBatch(_ycShell, surfaces, autoClose);
        }

        private void RunRegenerateBatch(YcShellBridge bridge, List<string> surfaces, bool autoClose)
        {
            IReadOnlyList<RegenerateOutcome> outcomes;
            using (var watcher = new YcShellMessageWatcher { AutoClose = autoClose })
            {
                var runner = new RegenerateBatchRunner(
                    new YcShellSurfaceRegenerator(bridge),
                    watcher,
                    SkipReason,
                    RecordChannelFingerprint);

                BatchProgressWindow? progress = null;
                ShowBusy("Regenerating channel features…");
                try
                {
                    progress = BatchProgressWindow.Open("Regenerating channel features");
                    outcomes = runner.Run(surfaces, () => progress.StopRequested, (n, total, surface) =>
                    {
                        string text = $"{n} of {total}: {Path.GetFileNameWithoutExtension(surface)}";
                        progress.Update(text, (n - 1.0) / total);
                        UpdateBusy(text, (n - 1.0) / total);
                    });

                    UpdateBusy($"Re-checking {Path.GetFileName(_iamPath)}…", 1);
                    ReScanAndRefresh();
                }
                catch (Exception ex)
                {
                    DebugLogger.Log(ex, "VerifierView.RunRegenerateBatch");
                    Dialogs.Show(this, $"Regenerate changed stopped: {ex.Message}", "Regenerate changed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                finally
                {
                    progress?.Close();
                    BusyOverlay.Visibility = Visibility.Collapsed;
                }
            }

            string summary = RegenerateBatchSummary.Text(outcomes);
            DebugLogger.Log("Regenerate changed summary:" + Environment.NewLine + summary);
            ShowNotice(RegenerateBatchSummary.Headline(outcomes));
            Dialogs.Show(this, summary, "Regenerate changed", MessageBoxButton.OK,
                outcomes.All(o => o.Succeeded) ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        /// <summary>YCShell refuses surfaces with suppressed parts; skip them here rather than leave the batch waiting on its dialog.</summary>
        private string? SkipReason(string surfaceIamPath)
        {
            Inventor.AssemblyDocument? surface = OpenAssembly(surfaceIamPath);
            if (surface is null) return "not open in Inventor";

            foreach (Inventor.ComponentOccurrence occ in surface.ComponentDefinition.Occurrences)
            {
                if (occ.Suppressed) return "has suppressed parts; unsuppress them and run Regenerate on this surface by hand";
            }

            return null;
        }

        /// <summary>Records the surface's channels so the next run knows it is done until a channel changes again.</summary>
        private void RecordChannelFingerprint(string surfaceIamPath)
        {
            if (OpenAssembly(surfaceIamPath) is not Inventor.AssemblyDocument surface) return;
            SurfaceChangeState state = Extraction.SurfaceChangeReader.Read(surface);
            ChannelFingerprintStore.Record(_iamPath, ChannelFingerprint.From(state, DateTime.UtcNow));
        }

        private Inventor.AssemblyDocument? OpenAssembly(string path)
        {
            try { return _inventorApp.Documents.get_ItemByName(path) as Inventor.AssemblyDocument; }
            catch { return null; }
        }

        // ── Text Normalization & Format Utilities ──────────────────────────────

        private static string Normalize(string s)
        {
            if (s == null) return string.Empty;
            return s.Trim().Replace("\"", "").Replace("'", "").ToUpperInvariant();
        }

        private void PopulateDropdowns(List<string> gauges, List<string> materials)
        {
            _gauges = gauges;
            _materials = materials;

            _isUpdatingUi = true;
            try
            {
                // Roof comboboxes
                RoofExteriorGaugeCombo.ItemsSource = gauges;
                RoofExteriorMaterialCombo.ItemsSource = materials;
                RoofInteriorGaugeCombo.ItemsSource = gauges;
                RoofInteriorMaterialCombo.ItemsSource = materials;
                RoofChannelGaugeCombo.ItemsSource = gauges;
                RoofChannelMaterialCombo.ItemsSource = materials;
                RoofTrimGaugeCombo.ItemsSource = gauges;
                RoofTrimMaterialCombo.ItemsSource = materials;

                // Wall comboboxes
                WallExteriorGaugeCombo.ItemsSource = gauges;
                WallExteriorMaterialCombo.ItemsSource = materials;
                WallInteriorGaugeCombo.ItemsSource = gauges;
                WallInteriorMaterialCombo.ItemsSource = materials;
                WallChannelGaugeCombo.ItemsSource = gauges;
                WallChannelMaterialCombo.ItemsSource = materials;

                // Base comboboxes
                FloorGaugeCombo.ItemsSource = gauges;
                FloorMaterialCombo.ItemsSource = materials;
                SubFloorGaugeCombo.ItemsSource = gauges;
                SubFloorMaterialCombo.ItemsSource = materials;
                FormedChannelGaugeCombo.ItemsSource = gauges;
                FormedChannelMaterialCombo.ItemsSource = materials;
                PerimeterAngleGaugeCombo.ItemsSource = gauges;
                PerimeterAngleMaterialCombo.ItemsSource = materials;

                ApplyExpectationComboWidths(gauges, materials);
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        private void ApplyExpectationComboWidths(List<string> gauges, List<string> materials)
        {
            double gaugeWidth = MeasureItemsTextWidth(gauges) + 28;
            double materialWidth = MeasureItemsTextWidth(materials) + 28;

            foreach (ComboBox combo in new[]
            {
                RoofExteriorGaugeCombo, RoofInteriorGaugeCombo, RoofChannelGaugeCombo, RoofTrimGaugeCombo,
                WallExteriorGaugeCombo, WallInteriorGaugeCombo, WallChannelGaugeCombo,
                FormedChannelGaugeCombo, FloorGaugeCombo, SubFloorGaugeCombo, PerimeterAngleGaugeCombo
            })
            {
                combo.MinWidth = gaugeWidth;
            }

            foreach (ComboBox combo in new[]
            {
                RoofExteriorMaterialCombo, RoofInteriorMaterialCombo, RoofChannelMaterialCombo, RoofTrimMaterialCombo,
                WallExteriorMaterialCombo, WallInteriorMaterialCombo, WallChannelMaterialCombo,
                FormedChannelMaterialCombo, FloorMaterialCombo, SubFloorMaterialCombo, PerimeterAngleMaterialCombo
            })
            {
                combo.MinWidth = materialWidth;
            }

            SizeComboToItems(BaseHeightCombo, "6\"", "8\"", "10\"", "12\"");
            SizeComboToItems(BaseMatCombo, "STL C CHNL", "ALM C CHNL");
            SizeComboToItems(RoofThermalCombo, "Yes", "No");
            SizeComboToItems(WallThermalCombo, "Yes", "No");
            SizeComboToItems(FloorThermalCombo, "Yes", "No");
        }

        private static void SizeComboToItems(ComboBox combo, params string[] items)
        {
            combo.MinWidth = MeasureItemsTextWidth(items) + 28;
        }

        private static double MeasureItemsTextWidth(System.Collections.IEnumerable items)
        {
            var typeface = new Typeface(
                new FontFamily("Segoe UI"),
                FontStyles.Normal,
                FontWeights.Normal,
                FontStretches.Normal);

            double max = 0;
            foreach (object item in items)
            {
                string text = item switch
                {
                    ComboBoxItem comboBoxItem => comboBoxItem.Content?.ToString() ?? string.Empty,
                    _ => item?.ToString() ?? string.Empty
                };

                var formatted = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    12,
                    Brushes.White,
                    1.0);
                max = Math.Max(max, formatted.Width);
            }

            return max;
        }

        private void OnOpenSurfaceInInventor(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                string filePath = null;
                if (menuItem.DataContext is RoofSurfaceRow roof)
                {
                    filePath = roof.SourceSurfaceIam;
                }
                else if (menuItem.DataContext is WallSurfaceRow wall)
                {
                    filePath = wall.SourceSurfaceIam;
                }
                else if (menuItem.DataContext is BaseSurfaceRow baseRow)
                {
                    filePath = baseRow.SourceSurfaceIam;
                }

                OpenDocumentInInventor(filePath);
            }
        }

        private void OnOpenPartInInventor(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is IptVerificationRow row)
            {
                OpenDocumentInInventor(row.FilePath);
            }
        }

        private void OpenDocumentInInventor(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
            {
                try
                {
                    _inventorApp.Documents.Open(filePath, true);
                }
                catch (Exception ex)
                {
                    Dialogs.Show(this, $"Failed to open document in Inventor:\n{ex.Message}", "Error Opening Document", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                Dialogs.Show(this, $"File does not exist or path is invalid:\n{filePath}", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnPartsGridSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshingPartsGrid)
            {
                return;
            }

            if (sender is DataGrid grid)
            {
                UpdatePreviewButtons();
                CaptureStackStateBeforeInventor();
                HighlightSelectedPartInGrid(grid);
                TryRefreshWireframePreview();
                RestoreStackAfterInventor();
            }
        }

        private void UpdatePreviewButtons()
        {
            RoofPreviewButton.IsEnabled = RoofPartsGrid.SelectedItem is IptVerificationRow;
            WallPreviewButton.IsEnabled = WallPartsGrid.SelectedItem is IptVerificationRow;
            BasePreviewButton.IsEnabled = BasePartsGrid.SelectedItem is IptVerificationRow;
        }

        private void HighlightSelectedPartInGrid(DataGrid grid)
        {
            if (_highlightMode == HighlightMode.Off || grid.SelectedItem is not IptVerificationRow row)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(row.FilePath))
            {
                return;
            }

            string? surfaceIamPath = GetSurfaceIamPathForPartsGrid(grid);
            if (string.IsNullOrWhiteSpace(surfaceIamPath))
            {
                InventorSelectionHelper.HighlightByFilePath(_inventorApp, _iamPath, row.FilePath);
                return;
            }

            InventorSelectionHelper.HighlightPartInSurface(
                _inventorApp,
                _iamPath,
                surfaceIamPath,
                row.PartNumber,
                row.FilePath,
                _highlightMode == HighlightMode.XRay);
        }

        private void OnHighlightModeChanged(object sender, RoutedEventArgs e)
        {
            // Checked fires for the default option while the XAML loads.
            if (!IsInitialized)
            {
                return;
            }

            _highlightMode = ReferenceEquals(sender, HighlightOffRadio) ? HighlightMode.Off
                : ReferenceEquals(sender, HighlightXRayRadio) ? HighlightMode.XRay
                : HighlightMode.Box;

            if (_highlightMode == HighlightMode.Off)
            {
                InventorSelectionHelper.RestoreNormal(_inventorApp);
                return;
            }

            CaptureStackStateBeforeInventor();
            DataGrid? activeGrid = GetActivePartsGrid();
            if (activeGrid != null)
            {
                HighlightSelectedPartInGrid(activeGrid);
            }
            else if (GetSelectedSurfaceForCurrentTab() is object surface)
            {
                HighlightSurfaceInInventor(surface);
            }
            else
            {
                InventorSelectionHelper.ClearHighlight(_inventorApp);
            }
            RestoreStackAfterInventor();
        }

        private object? GetSelectedSurfaceForCurrentTab() => MainTabControl.SelectedIndex switch
        {
            0 => RoofSurfaceList.SelectedItem,
            1 => WallSurfaceList.SelectedItem,
            2 => BaseSurfaceList.SelectedItem,
            _ => null
        };

        private void InitializeXRayColorPicker()
        {
            if (XRayColorSettings.TryLoad(out byte r, out byte g, out byte b))
            {
                _xRayColorR = r;
                _xRayColorG = g;
                _xRayColorB = b;
            }

            InventorSelectionHelper.SetXRayOutlineColor(_xRayColorR, _xRayColorG, _xRayColorB);
            UpdateXRayColorSwatch();

            foreach (XRayHighlightColor option in XRayHighlightColors.Options)
            {
                XRayHighlightColor captured = option;
                var row = new Button
                {
                    Style = (Style)FindResource("ColorOptionButton"),
                    Height = 26,
                    Margin = new Thickness(0, 0, 0, 2),
                    Padding = new Thickness(6, 0, 8, 0),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = new SolidColorBrush(Color.FromRgb(captured.R, captured.G, captured.B)),
                    BorderBrush = (Brush)FindResource("BorderColor"),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Tag = captured
                };

                var content = new StackPanel { Orientation = Orientation.Horizontal };
                content.Children.Add(new Border
                {
                    Width = 14,
                    Height = 14,
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = row.Background,
                    BorderBrush = (Brush)FindResource("BorderColor"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2)
                });
                content.Children.Add(new TextBlock
                {
                    Text = captured.Name,
                    Foreground = GetContrastForeground(captured.R, captured.G, captured.B),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Content = content;
                row.Click += OnXRayColorOptionClick;
                XRayColorListPanel.Children.Add(row);
            }
        }

        private void OnXRayColorButtonClick(object sender, RoutedEventArgs e)
        {
            XRayColorPopup.IsOpen = !XRayColorPopup.IsOpen;
        }

        private void OnXRayColorOptionClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not XRayHighlightColor color)
            {
                return;
            }

            SelectXRayColor(color.R, color.G, color.B);
            XRayColorPopup.IsOpen = false;
        }

        private void SelectXRayColor(byte r, byte g, byte b)
        {
            _xRayColorR = r;
            _xRayColorG = g;
            _xRayColorB = b;
            InventorSelectionHelper.SetXRayOutlineColor(r, g, b);
            XRayColorSettings.Save(r, g, b);
            UpdateXRayColorSwatch();

            DataGrid? activeGrid = GetActivePartsGrid();
            if (activeGrid != null)
            {
                HighlightSelectedPartInGrid(activeGrid);
                return;
            }

            object? selectedSurface = MainTabControl.SelectedIndex switch
            {
                0 => RoofSurfaceList.SelectedItem,
                1 => WallSurfaceList.SelectedItem,
                2 => BaseSurfaceList.SelectedItem,
                _ => null
            };

            if (selectedSurface != null)
            {
                CaptureStackStateBeforeInventor();
                HighlightSurfaceInInventor(selectedSurface);
                RestoreStackAfterInventor();
            }
        }

        private void UpdateXRayColorSwatch()
        {
            XRayColorButton.Background = new SolidColorBrush(Color.FromRgb(_xRayColorR, _xRayColorG, _xRayColorB));
        }

        private static Brush GetContrastForeground(byte r, byte g, byte b)
        {
            double luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
            return luminance > 0.62
                ? new SolidColorBrush(Color.FromRgb(30, 30, 46))
                : new SolidColorBrush(Colors.White);
        }

        private void OnMainTabChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, MainTabControl))
            {
                return;
            }

            CaptureStackStateBeforeInventor();
            TryRefreshWireframePreview();
            RestoreStackAfterInventor();
        }

        private void CaptureStackStateBeforeInventor()
        {
            _partPreviewWindow?.CaptureStackState();
            // Only the pop-out window can fall behind Inventor; a docked view is part of Inventor's frame.
            if (Window.GetWindow(this) is Window host)
            {
                InventorWindowStackHelper.Capture(
                    host,
                    ref _keepVerifierAboveInventor,
                    ref _pendingVerifierStackRestore,
                    ref _pendingVerifierFocusRestore);
            }
        }

        private void RestoreStackAfterInventor()
        {
            if (_partPreviewWindow?.IsVisible == true &&
                _partPreviewWindow.TakePendingStackRestore(out bool previewFocus))
            {
                _partPreviewWindow.ScheduleStackRestore(previewFocus);
            }

            if (InventorWindowStackHelper.TakePending(
                    ref _pendingVerifierStackRestore,
                    ref _pendingVerifierFocusRestore,
                    out bool verifierFocus) &&
                Window.GetWindow(this) is Window host)
            {
                InventorWindowStackHelper.ScheduleRestore(host, verifierFocus);
            }
        }

        private void OnPartsGridBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            // Only Edit Mode may stage part edits, and only for properties the writer can set.
            if (EditModeCheckBox.IsChecked != true ||
                e.Row.Item is not IptVerificationRow row ||
                !row.IsWritable)
            {
                e.Cancel = true;
            }
        }

        private void OnPartsGridPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (TreeSearch.FindAncestor<Button>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            CaptureStackStateBeforeInventor();
        }

        private DataGrid? GetActivePartsGridForCurrentTab()
        {
            return MainTabControl.SelectedIndex switch
            {
                0 => RoofPartsGrid,
                1 => WallPartsGrid,
                2 => BasePartsGrid,
                _ => null
            };
        }

        private DataGrid? GetActivePartsGrid()
        {
            DataGrid? grid = GetActivePartsGridForCurrentTab();
            if (grid == null)
            {
                return null;
            }

            bool hasSurface = MainTabControl.SelectedIndex switch
            {
                0 => RoofSurfaceList.SelectedItem != null,
                1 => WallSurfaceList.SelectedItem != null,
                2 => BaseSurfaceList.SelectedItem != null,
                _ => false
            };

            if (!hasSurface || grid.SelectedItem == null)
            {
                return null;
            }

            return grid;
        }

        private bool TryBuildWireframePreview(out string title, out WireframeData? wireframe)
        {
            title = string.Empty;
            wireframe = null;

            DataGrid? grid = GetActivePartsGrid();
            if (grid?.SelectedItem is not IptVerificationRow row || string.IsNullOrWhiteSpace(row.FilePath))
            {
                return false;
            }

            string? surfaceIamPath = GetSurfaceIamPathForPartsGrid(grid);
            if (string.IsNullOrWhiteSpace(surfaceIamPath))
            {
                return false;
            }

            Inventor.ComponentOccurrence? occurrence = InventorSelectionHelper.FindFirstPartOccurrenceInSurface(
                _inventorApp,
                _iamPath,
                surfaceIamPath,
                row.PartNumber,
                row.FilePath);
            if (occurrence == null)
            {
                return false;
            }

            wireframe = PartWireframeExtractor.ExtractFromOccurrence(occurrence);
            if (wireframe == null || wireframe.Segments.Count == 0)
            {
                return false;
            }

            title = row.PartNumber;
            return true;
        }

        private void TryRefreshWireframePreview()
        {
            if (_partPreviewWindow == null || !_partPreviewWindow.IsVisible)
            {
                return;
            }

            if (TryBuildWireframePreview(out string title, out WireframeData? wireframe))
            {
                _partPreviewWindow.ShowWireframe(title, wireframe, resetView: false);
                return;
            }

            _partPreviewWindow.ShowWireframe("Select a part in the active tab", null, resetView: false);
        }

        private void OnWireframePreviewClick(object sender, RoutedEventArgs e)
        {
            if (!TryBuildWireframePreview(out string title, out WireframeData? wireframe))
            {
                ShowNotice("No wireframe for this row. Select a part on the open surface first.");
                return;
            }

            if (_partPreviewWindow == null)
            {
                _partPreviewWindow = new PartPreviewWindow();
                Dialogs.SetOwner(this, _partPreviewWindow);
                Rect bounds = ScreenBounds();
                _partPreviewWindow.Left = WindowLayout.CompanionLeft(bounds.Left, bounds.Width, _partPreviewWindow.Width,
                    SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenWidth);
                _partPreviewWindow.Top = WindowLayout.CompanionTop(bounds.Top, _partPreviewWindow.Height,
                    SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenHeight);
                _partPreviewWindow.Closed += (_, __) => _partPreviewWindow = null;
            }

            _partPreviewWindow.ShowWireframe(title, wireframe, resetView: true);
            _partPreviewWindow.MarkKeepAboveInventor();
            if (!_partPreviewWindow.IsVisible)
            {
                _partPreviewWindow.Show();
            }
            else
            {
                _partPreviewWindow.Activate();
            }
        }

        /// <summary>Where the view is on screen, in WPF units: its window, or the dock it sits in.</summary>
        private Rect ScreenBounds()
        {
            if (Window.GetWindow(this) is Window window)
            {
                return new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight);
            }

            if (PresentationSource.FromVisual(this)?.CompositionTarget is CompositionTarget target)
            {
                Point topLeft = target.TransformFromDevice.Transform(PointToScreen(new Point(0, 0)));
                return new Rect(topLeft, new Size(ActualWidth, ActualHeight));
            }

            return new Rect(0, 0, ActualWidth, ActualHeight);
        }

        private string? GetSurfaceIamPathForPartsGrid(DataGrid grid)
        {
            if (ReferenceEquals(grid, RoofPartsGrid))
            {
                return (RoofSurfaceList.SelectedItem as RoofSurfaceRow)?.SourceSurfaceIam;
            }

            if (ReferenceEquals(grid, WallPartsGrid))
            {
                return (WallSurfaceList.SelectedItem as WallSurfaceRow)?.SourceSurfaceIam;
            }

            if (ReferenceEquals(grid, BasePartsGrid))
            {
                return (BaseSurfaceList.SelectedItem as BaseSurfaceRow)?.SourceSurfaceIam;
            }

            return null;
        }

        private void HighlightSurfaceInInventor(object surfaceRow)
        {
            string? surfaceIamPath = surfaceRow switch
            {
                RoofSurfaceRow roof => roof.SourceSurfaceIam,
                WallSurfaceRow wall => wall.SourceSurfaceIam,
                BaseSurfaceRow baseRow => baseRow.SourceSurfaceIam,
                _ => null
            };

            if (_highlightMode == HighlightMode.Off || string.IsNullOrWhiteSpace(surfaceIamPath))
            {
                return;
            }

            InventorSelectionHelper.HighlightByFilePath(_inventorApp, _iamPath, surfaceIamPath);
        }

        private void OnSurfaceListPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBox listBox)
            {
                return;
            }

            // Ignore copy-button clicks in the row template.
            if (TreeSearch.FindAncestor<Button>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            ListBoxItem? listBoxItem = TreeSearch.FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (listBoxItem?.DataContext == null)
            {
                return;
            }

            // Re-highlight when the row is already selected (SelectionChanged won't fire).
            CaptureStackStateBeforeInventor();
            if (!listBoxItem.IsSelected)
            {
                return;
            }

            HighlightSurfaceInInventor(listBoxItem.DataContext);
            TryRefreshWireframePreview();
            RestoreStackAfterInventor();
        }

        // ── Copy, Search, Keys, Overflow ──────────────────────────────────────

        private void OnCopyPartNumberMenu(object sender, RoutedEventArgs e)
        {
            // The row the menu opened on, plus any other selected rows in that grid.
            if (sender is MenuItem { DataContext: IptVerificationRow row })
            {
                DataGrid? grid = GetActivePartsGridForCurrentTab();
                var selected = grid?.SelectedItems.OfType<IptVerificationRow>().ToList() ?? new List<IptVerificationRow>();
                CopyPartNumbers(selected.Contains(row) ? selected : new List<IptVerificationRow> { row });
            }
        }

        private void OnCopyPartNumbersCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is DataGrid grid)
            {
                CopyPartNumbers(grid.SelectedItems.OfType<IptVerificationRow>().ToList());
                e.Handled = true;
            }
        }

        private void CopyPartNumbers(List<IptVerificationRow> rows)
        {
            var numbers = rows.Select(r => r.PartNumber).Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (numbers.Count == 0) return;

            if (CopyTextToClipboard(string.Join(Environment.NewLine, numbers)))
                ShowNotice(numbers.Count == 1 ? $"Copied {numbers[0]}." : $"Copied {numbers.Count} part numbers.");
        }

        private void OnCopySurfaceNumberMenu(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
                CopySurfaceNumber(menuItem.DataContext);
        }

        private void OnCopySurfaceNumberCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (sender is ListBox list)
            {
                CopySurfaceNumber(list.SelectedItem);
                e.Handled = true;
            }
        }

        private void CopySurfaceNumber(object? surfaceRow)
        {
            string? number = surfaceRow switch
            {
                RoofSurfaceRow roof => roof.PartNumber,
                WallSurfaceRow wall => wall.PartNumber,
                BaseSurfaceRow baseRow => baseRow.PartNumber,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(number) && CopyTextToClipboard(number))
                ShowNotice($"Copied {number!.Trim()}.");
        }

        private bool CopyTextToClipboard(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                Clipboard.SetText(text!.Trim());
                return true;
            }
            catch (Exception ex)
            {
                ShowNotice($"Could not copy to the clipboard: {ex.Message}");
                return false;
            }
        }

        private void OnSurfaceSearchChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized || sender is not TextBox box) return;

            ListBox list = ReferenceEquals(box, WallSearchBox) ? WallSurfaceList
                : ReferenceEquals(box, BaseSearchBox) ? BaseSurfaceList
                : RoofSurfaceList;
            string query = box.Text.Trim();

            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(list.ItemsSource);
            if (view == null) return;
            view.Filter = query.Length == 0 ? null : item => SurfaceMatches(item, query);
        }

        /// <summary>A surface matches on its number, its segments, or the number of any part inside it.</summary>
        private bool SurfaceMatches(object surfaceRow, string query)
        {
            static bool Has(string? text, string q) => text?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

            (string? number, string? segments) = surfaceRow switch
            {
                RoofSurfaceRow roof => (roof.PartNumber, roof.Segments),
                WallSurfaceRow wall => (wall.PartNumber, wall.Segments),
                BaseSurfaceRow baseRow => (baseRow.PartNumber, baseRow.Segments),
                _ => (null, null)
            };
            if (Has(number, query) || Has(segments, query)) return true;

            string? iam = SurfaceStatusBoard.SurfaceIamOf(surfaceRow);
            return _iptResult != null && _iptResult.Parts.Any(p =>
                string.Equals(p.OwnerIamPath, iam, StringComparison.OrdinalIgnoreCase) && Has(p.PartNumber, query));
        }

        /// <summary>
        /// Keys reach the view through <see cref="KeyPassthroughHook"/> in either host. Text fields keep normal copy and
        /// paste. Elsewhere Ctrl+C copies the selected part or surface numbers (not the grid's own row text), and Ctrl+V
        /// does nothing, so Inventor cannot paste an old copied component while the verifier has focus. F8 is Next issue.
        /// </summary>
        private void OnViewPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (BusyOverlay.Visibility == Visibility.Visible)
            {
                e.Handled = true;
                return;
            }

            var focused = Keyboard.FocusedElement as DependencyObject;
            bool inText = focused is System.Windows.Controls.Primitives.TextBoxBase;
            ModifierKeys modifiers = Keyboard.Modifiers;

            if (e.Key == Key.F8 && modifiers == ModifierKeys.None)
            {
                if (WriteReviewOverlay.Visibility != Visibility.Visible && RegenerateReviewOverlay.Visibility != Visibility.Visible) GoToNextIssue();
                e.Handled = true;
            }
            else if (modifiers == ModifierKeys.Control && e.Key == Key.C && !inText)
            {
                if (TreeSearch.FindAncestor<DataGrid>(focused) is DataGrid grid)
                    CopyPartNumbers(grid.SelectedItems.OfType<IptVerificationRow>().ToList());
                else if (TreeSearch.FindAncestor<ListBox>(focused) is ListBox list)
                    CopySurfaceNumber(list.SelectedItem);
                e.Handled = true;
            }
            else if (modifiers == ModifierKeys.Control && e.Key == Key.V && !inText)
            {
                ShowNotice("Paste works in text fields only, so Inventor does not paste a copied part.");
                e.Handled = true;
            }
        }

        private void OnOverflowClick(object sender, RoutedEventArgs e)
        {
            var menu = (ContextMenu)FindResource("OverflowMenu");
            menu.PlacementTarget = OverflowButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            menu.IsOpen = true;
        }
    }

    // ── Grid Row Model ──
    public sealed class IptVerificationRow : System.ComponentModel.INotifyPropertyChanged
    {
        public string PartNumber    { get; set; } = string.Empty;
        public string Description   { get; set; } = string.Empty;
        public string PartType      { get; set; } = string.Empty;
        public string Parameter     { get; set; } = string.Empty;
        public string Expected      { get; set; } = string.Empty;
        public string Actual        { get; set; } = string.Empty;
        public PartCheckStatus CheckStatus { get; set; }
        public string Reason        { get; set; } = string.Empty;
        public bool   IsMismatch    => CheckStatus == PartCheckStatus.Mismatch;
        public bool   IsNotChecked  => CheckStatus == PartCheckStatus.NotChecked;
        public bool   IsDisplayOnly => CheckStatus == PartCheckStatus.DisplayOnly;
        /// <summary>Why a part was not checked, shown after its description.</summary>
        public string ReasonSuffix  => IsNotChecked && !string.IsNullOrWhiteSpace(Reason) ? "  · " + Reason : string.Empty;
        public string DescriptionTooltip => string.IsNullOrWhiteSpace(ReasonSuffix) ? Description : Description + "\n" + Reason;
        public string StatusTooltip => IsEditPending
            ? $"Staged: {Actual} → {NewValue}. Not written yet."
            : !string.IsNullOrWhiteSpace(Reason) ? Reason
            : IsModified ? "Changed after the unit was generated (SQ part or a verifier edit)." : Status;
        /// <summary>SQ part or verifier-edited part; never present on a freshly generated unit.</summary>
        public bool   IsModified    { get; set; }
        /// <summary>True when an edit to <see cref="Parameter"/> can be written back to the part.</summary>
        public bool   IsWritable    { get; set; }
        public string Status        => (CheckStatus switch
        {
            PartCheckStatus.Mismatch    => "✗ Mismatch",
            PartCheckStatus.NotChecked  => "– Not checked",
            PartCheckStatus.DisplayOnly => "Display only",
            _                           => "✓ Pass",
        }) + (IsModified ? " · modified" : string.Empty);
        public string FilePath      { get; set; } = string.Empty;

        private string _newValue = string.Empty;
        public string NewValue
        {
            get => string.IsNullOrEmpty(_newValue) ? Actual : _newValue;
            set
            {
                if (_newValue != value)
                {
                    _newValue = value;
                    
                    // Update pending state first to ensure OnPropertyChanged(NewValue) event handles it correctly
                    bool newPending = !string.Equals(Normalize(_newValue), Normalize(Actual), StringComparison.OrdinalIgnoreCase);
                    if (IsEditPending != newPending)
                    {
                        IsEditPending = newPending;
                    }

                    OnPropertyChanged(nameof(NewValue));
                    OnPropertyChanged(nameof(IsEditPending));
                    OnPropertyChanged(nameof(InPartText));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusTooltip));
                }
            }
        }

        private bool _isEditPending;
        public bool IsEditPending
        {
            get => _isEditPending;
            set
            {
                if (_isEditPending != value)
                {
                    _isEditPending = value;
                    OnPropertyChanged(nameof(IsEditPending));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public string StatusText => IsEditPending ? "● Staged" : Status;

        /// <summary>What the In part cell shows: the part's value, then the staged value while one is staged.</summary>
        public string InPartText => IsEditPending ? $"{Actual} → {NewValue}" : Actual;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        private static string Normalize(string s)
        {
            if (s == null) return string.Empty;
            return s.Trim().Replace("\"", "").Replace("'", "").ToUpperInvariant();
        }
    }
}
