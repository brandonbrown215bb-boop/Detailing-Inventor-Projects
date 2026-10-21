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
    public partial class VerifierWindow : Window
    {
        private readonly UnitConstructionData _data;
        private readonly string               _iamPath;
        private IptScanResult?                _iptResult;
        private UnitSnapshot?                 _snapshot;
        private bool                          _isUpdatingUi;
        private readonly Inventor.Application _inventorApp;
        private readonly Dictionary<string, string> _pendingEdits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

        public VerifierWindow(UnitConstructionData data, string iamPath, Inventor.Application inventorApp)
        {
            InitializeComponent();

            _data    = data;
            _iamPath = iamPath;
            _inventorApp = inventorApp;

            IamPathLabel.Text = Path.GetFileName(iamPath);

            // Load config from materials_config.json
            MaterialsConfig.Initialize();

            // Populate dropdowns with standard config options
            PopulateDropdowns(MaterialsConfig.Gauges, MaterialsConfig.Materials);

            // Populate list boxes
            RoofSurfaceList.ItemsSource = _data.RoofRows;
            WallSurfaceList.ItemsSource = _data.WallRows;
            BaseSurfaceList.ItemsSource = _data.BaseRows;

            // Load global other specs
            PopulateGlobalSpecs();
            InitializeXRayColorPicker();

            Activated += (_, __) => _keepVerifierAboveInventor = true;
            Closing += OnVerifierWindowClosing;
        }

        public void SetIptScanResult(IptScanResult iptResult)
        {
            _iptResult = iptResult;
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

        private void OnApplyRoofToAll(object sender, RoutedEventArgs e)
        {
            var selected = RoofSurfaceList.SelectedItem as RoofSurfaceRow;
            if (selected == null) return;

            foreach (var row in _data.RoofRows)
            {
                if (row == selected) continue;
                row.Thickness = selected.Thickness;
                row.ExteriorSkinGauge = selected.ExteriorSkinGauge;
                row.ExteriorSkinMaterial = selected.ExteriorSkinMaterial;
                row.InteriorLinerGauge = selected.InteriorLinerGauge;
                row.InteriorLinerMaterial = selected.InteriorLinerMaterial;
                row.ChannelSkinGauge = selected.ChannelSkinGauge;
                row.ChannelSkinMaterial = selected.ChannelSkinMaterial;
                row.TrimSkinGauge = selected.TrimSkinGauge;
                row.TrimSkinMaterial = selected.TrimSkinMaterial;
                row.InsulationThicknessAndMaterial = selected.InsulationThicknessAndMaterial;
                row.ThermalBreak = selected.ThermalBreak;
            }

            MarkOverridesDirty();
            MessageBox.Show("Casing specifications copied to all Roof surfaces.", "Apply Casing Specs", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void OnApplyWallToAll(object sender, RoutedEventArgs e)
        {
            var selected = WallSurfaceList.SelectedItem as WallSurfaceRow;
            if (selected == null) return;

            foreach (var row in _data.WallRows)
            {
                if (row == selected) continue;
                row.Thickness = selected.Thickness;
                row.ExteriorPaint = selected.ExteriorPaint;
                row.ExteriorSkinGauge = selected.ExteriorSkinGauge;
                row.ExteriorSkinMaterial = selected.ExteriorSkinMaterial;
                row.InteriorLinerGauge = selected.InteriorLinerGauge;
                row.InteriorLinerMaterial = selected.InteriorLinerMaterial;
                row.ChannelSkinGauge = selected.ChannelSkinGauge;
                row.ChannelSkinMaterial = selected.ChannelSkinMaterial;
                row.InsulationThicknessAndMaterial = selected.InsulationThicknessAndMaterial;
                row.ThermalBreak = selected.ThermalBreak;
            }

            MarkOverridesDirty();
            MessageBox.Show("Casing specifications copied to all Wall surfaces.", "Apply Casing Specs", MessageBoxButton.OK, MessageBoxImage.Information);
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
                RoofPartsGrid.ItemsSource = BuildPartsGridRows(roof.SourceSurfaceIam, "Roof", VerificationEngine.BuildRowFields(roof));
            else if (surfaceRow is WallSurfaceRow wall)
                WallPartsGrid.ItemsSource = BuildPartsGridRows(wall.SourceSurfaceIam, "Wall", VerificationEngine.BuildRowFields(wall));
            else if (surfaceRow is BaseSurfaceRow bs)
                BasePartsGrid.ItemsSource = BuildPartsGridRows(bs.SourceSurfaceIam, "Base", VerificationEngine.BuildRowFields(bs));
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
            MessageBox.Show($"Overrides saved to:\n{PersistenceManager.GetSidecarPath(_iamPath)}",
                "Saved Overrides", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnSaveSnapshot(object sender, RoutedEventArgs e)
        {
            if (_snapshot is null)
            {
                MessageBox.Show("Scan data not available — cannot save a snapshot.",
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
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                SnapshotSerializer.Save(dialog.FileName, _snapshot);
                MessageBox.Show($"Snapshot saved to:\n{dialog.FileName}\n\nCopy it into UnitConstructionVerifier.Tests\\Snapshots to use it as a regression baseline.",
                    "Save Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DebugLogger.Log(ex, "OnSaveSnapshot");
                MessageBox.Show($"Could not save snapshot:\n{ex.Message}",
                    "Save Snapshot", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnVerify(object sender, RoutedEventArgs e)
        {
            if (_iptResult is null)
            {
                MessageBox.Show("IPT scan data not available — cannot verify.",
                    "Verify", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var engine = new VerificationEngine(_data, _iptResult);
            VerificationResult result = engine.Run();

            int passed     = result.CountOf(PartCheckStatus.Pass);
            int notChecked = result.CountOf(PartCheckStatus.NotChecked);
            int modified   = result.Checks.Count(c => c.IsModified);
            string coverage = $"\n\nChecked: {passed + result.Mismatches.Count} parts" +
                              (notChecked > 0 ? $"\nNot checked: {notChecked} parts (shown in grey in the surface grids; hover for the reason)" : string.Empty) +
                              (modified > 0 ? $"\nModified after generation (SQ or edited): {modified} parts, marked \"modified\" in the grids" : string.Empty);

            if (result.IsPass && notChecked == 0)
            {
                MessageBox.Show("✓  All checks passed! No mismatches found across any surfaces." + coverage,
                    "Verification Passed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (result.IsPass)
            {
                MessageBox.Show("✓  No mismatches found, but some parts could not be checked." + coverage,
                    "Verification Incomplete", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show($"✗  Verification complete. Found {result.Mismatches.Count} mismatches in this assembly. Review individual surface grids to see discrepancies highlighted in red." + coverage,
                    "Verification Mismatches", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // Force refresh current selection parts grid
            if (RoofSurfaceList.SelectedItem != null) RefreshPartsGrid(RoofSurfaceList.SelectedItem);
            if (WallSurfaceList.SelectedItem != null) RefreshPartsGrid(WallSurfaceList.SelectedItem);
            if (BaseSurfaceList.SelectedItem != null) RefreshPartsGrid(BaseSurfaceList.SelectedItem);
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

            string key = $"{row.FilePath}|{row.Parameter}";
            if (_pendingEdits.TryGetValue(key, out string pendingVal))
            {
                row.NewValue = pendingVal;
            }

            gridRows.Add(row);
        }

        private void OnRowPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is IptVerificationRow row && e.PropertyName == nameof(IptVerificationRow.NewValue))
            {
                string key = $"{row.FilePath}|{row.Parameter}";
                if (row.IsEditPending && row.IsWritable)
                {
                    _pendingEdits[key] = row.NewValue;
                }
                else
                {
                    _pendingEdits.Remove(key);
                }
            }
        }

        private void OnEditModeChanged(object sender, RoutedEventArgs e)
        {
            bool isEdit = EditModeCheckBox.IsChecked == true;

            // Enable/disable column editing
            if (RoofPartsGrid.Columns.Count > 6) RoofPartsGrid.Columns[6].IsReadOnly = !isEdit;
            if (WallPartsGrid.Columns.Count > 6) WallPartsGrid.Columns[6].IsReadOnly = !isEdit;
            if (BasePartsGrid.Columns.Count > 6) BasePartsGrid.Columns[6].IsReadOnly = !isEdit;

            // Show/hide sync panels
            RoofSyncButtonsPanel.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;
            WallSyncButtonsPanel.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;
            BaseSyncButtonsPanel.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;

            // Show/hide footers
            EditModeFooterPanel.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;
            NormalModeFooterPanel.Visibility = isEdit ? Visibility.Collapsed : Visibility.Visible;
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
            var result = MessageBox.Show("Are you sure you want to discard all unsaved edits?", "Discard Changes", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _pendingEdits.Clear();
                if (RoofSurfaceList.SelectedItem != null) RefreshPartsGrid(RoofSurfaceList.SelectedItem);
                if (WallSurfaceList.SelectedItem != null) RefreshPartsGrid(WallSurfaceList.SelectedItem);
                if (BaseSurfaceList.SelectedItem != null) RefreshPartsGrid(BaseSurfaceList.SelectedItem);
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
                MessageBox.Show("No pending edits to write.", "Write Changes", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Group edits by file
            var groupedEdits = new Dictionary<string, Operations.PartPropertyEdits>(StringComparer.OrdinalIgnoreCase);
            var unwritable = new List<string>();
            foreach (var kvp in _pendingEdits)
            {
                string[] parts = kvp.Key.Split('|');
                if (parts.Length < 2) continue;
                string filePath = parts[0];
                string parameter = parts[1];
                string value = kvp.Value;

                if (!groupedEdits.TryGetValue(filePath, out var edits))
                {
                    edits = new Operations.PartPropertyEdits();
                    groupedEdits[filePath] = edits;
                }

                if (!Operations.PartPropertyEditsMapper.TryApply(edits, parameter, value))
                {
                    unwritable.Add($"{Path.GetFileName(filePath)}: {parameter} = {value}");
                }
            }

            var emptyFiles = groupedEdits
                .Where(kvp => !Operations.PartPropertyEditsMapper.HasAnyValue(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (string filePath in emptyFiles)
            {
                groupedEdits.Remove(filePath);
            }

            string unwritableNote = unwritable.Count == 0
                ? string.Empty
                : "These edits cannot be written to part properties and are skipped:\n" +
                  string.Join("\n", unwritable.Select(u => $" - {u}")) + "\n\n";

            if (groupedEdits.Count == 0)
            {
                MessageBox.Show(unwritableNote + "No staged edits map to writable part properties. Nothing was written.",
                    "Write Changes", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Confirm modifications
            var sb = new System.Text.StringBuilder();
            sb.Append(unwritableNote);
            sb.AppendLine("The following part files will be modified in Inventor:");
            foreach (var path in groupedEdits.Keys)
            {
                sb.AppendLine($" - {Path.GetFileName(path)}");
            }
            sb.AppendLine("\nDo you want to proceed?");

            var confirm = MessageBox.Show(sb.ToString(), "Confirm Write Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

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
                MessageBox.Show(lockSb.ToString(), "Files Locked", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Start Inventor Transaction
            Inventor.Transaction trans = null;
            try
            {
                if (_inventorApp.ActiveDocument != null)
                {
                    trans = _inventorApp.TransactionManager.StartTransaction(_inventorApp.ActiveDocument, "UCV Edit Mode Sync");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start transaction: {ex.Message}");
            }

            var writer = new Operations.IptPropertyWriter(_inventorApp);
            int successCount = 0;
            var errorList = new List<string>();

            this.IsEnabled = false;

            try
            {
                foreach (var kvp in groupedEdits)
                {
                    string filePath = kvp.Key;
                    Operations.PartPropertyEdits edits = kvp.Value;

                    if (writer.UpdatePartProperties(filePath, edits, out string err))
                    {
                        successCount++;
                    }
                    else
                    {
                        errorList.Add($"{Path.GetFileName(filePath)}: {err}");
                    }

                    // Keep UI responsive (STA constraint friendly)
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new Action(() => { }));
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
                    MessageBox.Show(errSb.ToString(), "Error Writing Changes", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    if (trans != null)
                    {
                        trans.End();
                        trans = null;
                    }

                    _pendingEdits.Clear();

                    MessageBox.Show($"Successfully updated {successCount} parts.", "Write Changes Completed", MessageBoxButton.OK, MessageBoxImage.Information);

                    ReScanAndRefresh();
                }
            }
            catch (Exception ex)
            {
                if (trans != null)
                {
                    try { trans.Abort(); } catch {}
                }
                MessageBox.Show($"Unexpected error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.IsEnabled = true;
            }
        }

        private void ReScanAndRefresh()
        {
            if (string.IsNullOrEmpty(_iamPath) || _inventorApp.ActiveDocument is not Inventor.AssemblyDocument asmDoc) return;

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
                MessageBox.Show($"Failed to re-scan assembly after update: {ex.Message}", "Error Refreshing", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
                    MessageBox.Show($"Failed to open document in Inventor:\n{ex.Message}", "Error Opening Document", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show($"File does not exist or path is invalid:\n{filePath}", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                CaptureStackStateBeforeInventor();
                HighlightSelectedPartInGrid(grid);
                TryRefreshWireframePreview();
                RestoreStackAfterInventor();
            }
        }

        private void HighlightSelectedPartInGrid(DataGrid grid)
        {
            if (grid.SelectedItem is not IptVerificationRow row)
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
                XRayCheckBox.IsChecked == true);
        }

        private void OnXRayChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi)
            {
                return;
            }

            DataGrid? activeGrid = GetActivePartsGrid();
            if (activeGrid != null)
            {
                HighlightSelectedPartInGrid(activeGrid);
                return;
            }

            InventorSelectionHelper.ClearHighlight(_inventorApp);
        }

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
            InventorWindowStackHelper.Capture(
                this,
                ref _keepVerifierAboveInventor,
                ref _pendingVerifierStackRestore,
                ref _pendingVerifierFocusRestore);
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
                    out bool verifierFocus))
            {
                InventorWindowStackHelper.ScheduleRestore(this, verifierFocus);
            }
        }

        private void OnNormalSelectionClick(object sender, RoutedEventArgs e)
        {
            _isUpdatingUi = true;
            try
            {
                XRayCheckBox.IsChecked = false;
            }
            finally
            {
                _isUpdatingUi = false;
            }

            InventorSelectionHelper.RestoreNormal(_inventorApp);
        }

        private void OnVerifierWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            string? message = UnsavedWorkPrompt.BuildMessage(_overridesDirty, _pendingEdits.Count);
            if (message != null)
            {
                MessageBoxResult answer = MessageBox.Show(
                    this,
                    message,
                    "Unsaved Changes",
                    _overridesDirty ? MessageBoxButton.YesNoCancel : MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);

                if (answer == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }

                if (answer == MessageBoxResult.Yes)
                {
                    PersistenceManager.SaveOverrides(_iamPath, _data);
                    ClearOverridesDirty();
                }
            }

            InventorSelectionHelper.RestoreNormal(_inventorApp);
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
            if (FindAncestor<Button>(e.OriginalSource as DependencyObject) != null)
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
                MessageBox.Show(
                    "Select a part row in the active tab and surface first.",
                    "Wireframe Preview",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (_partPreviewWindow == null)
            {
                _partPreviewWindow = new PartPreviewWindow
                {
                    Owner = this,
                    Left = Left + Width + 12,
                    Top = Top
                };
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

            if (string.IsNullOrWhiteSpace(surfaceIamPath))
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
            if (FindAncestor<Button>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            ListBoxItem? listBoxItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
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

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void OnCopyPartNumberClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string? text = button.Tag as string;
                if (string.IsNullOrWhiteSpace(text) && button.DataContext is IptVerificationRow row)
                {
                    text = row.PartNumber;
                }

                CopyTextToClipboard(text);
            }

            e.Handled = true;
        }

        private void OnCopySurfaceNumberClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                CopyTextToClipboard(button.Tag as string);
            }

            e.Handled = true;
        }

        private static void CopyTextToClipboard(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            try
            {
                Clipboard.SetText(text.Trim());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not copy to clipboard:\n{ex.Message}", "Copy Failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
                    OnPropertyChanged(nameof(StatusText));
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

        public string StatusText => IsEditPending ? "* Unsaved" : Status;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        private static string Normalize(string s)
        {
            if (s == null) return string.Empty;
            return s.Trim().Replace("\"", "").Replace("'", "").ToUpperInvariant();
        }
    }
}
