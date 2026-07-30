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
        private bool                          _isUpdatingUi;
        private readonly Inventor.Application _inventorApp;
        private readonly Dictionary<string, string> _pendingEdits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private byte _xRayColorR = XRayHighlightColors.Default.R;
        private byte _xRayColorG = XRayHighlightColors.Default.G;
        private byte _xRayColorB = XRayHighlightColors.Default.B;
        private PartPreviewWindow? _partPreviewWindow;
        private bool _isRefreshingPartsGrid;
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
        }

        private void OnLipHeightChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            _data.OtherConstruction.UpturnedLipHeight = LipHeightBox.Text;
        }

        private void OnCurbCheckChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUi) return;
            _data.OtherConstruction.CurbRest = CurbCheckBox.IsChecked == true;
            CurbHeightBox.Visibility = _data.OtherConstruction.CurbRest ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnCurbHeightChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            _data.OtherConstruction.CurbRestHeight = CurbHeightBox.Text;
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
                RoofExteriorGaugeCombo.SelectedItem = selected.ExteriorSkinGauge;
                RoofExteriorMaterialCombo.SelectedItem = selected.ExteriorSkinMaterial;
                RoofInteriorGaugeCombo.SelectedItem = selected.InteriorLinerGauge;
                RoofInteriorMaterialCombo.SelectedItem = selected.InteriorLinerMaterial;
                RoofChannelGaugeCombo.SelectedItem = selected.ChannelSkinGauge;
                RoofChannelMaterialCombo.SelectedItem = selected.ChannelSkinMaterial;
                RoofTrimGaugeCombo.SelectedItem = selected.TrimSkinGauge;
                RoofTrimMaterialCombo.SelectedItem = selected.TrimSkinMaterial;
                RoofInsulationText.Text = selected.InsulationThicknessAndMaterial;

                RoofThermalCombo.SelectedIndex = string.Equals(selected.ThermalBreak, "Yes", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
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

            RefreshPartsGrid(selected);
        }

        private void OnRoofThermalComboChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            var selected = RoofSurfaceList.SelectedItem as RoofSurfaceRow;
            if (selected == null) return;

            selected.ThermalBreak = (RoofThermalCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "No";
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
                WallExteriorGaugeCombo.SelectedItem = selected.ExteriorSkinGauge;
                WallExteriorMaterialCombo.SelectedItem = selected.ExteriorSkinMaterial;
                WallInteriorGaugeCombo.SelectedItem = selected.InteriorLinerGauge;
                WallInteriorMaterialCombo.SelectedItem = selected.InteriorLinerMaterial;
                WallChannelGaugeCombo.SelectedItem = selected.ChannelSkinGauge;
                WallChannelMaterialCombo.SelectedItem = selected.ChannelSkinMaterial;
                WallInsulationText.Text = selected.InsulationThicknessAndMaterial;

                WallThermalCombo.SelectedIndex = string.Equals(selected.ThermalBreak, "Yes", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
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

            RefreshPartsGrid(selected);
        }

        private void OnWallThermalComboChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUi) return;
            var selected = WallSurfaceList.SelectedItem as WallSurfaceRow;
            if (selected == null) return;

            selected.ThermalBreak = (WallThermalCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "No";
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
                SelectComboItemByContent(BaseHeightCombo, selected.BaseHeight);
                SelectComboItemByContent(BaseMatCombo, selected.BaseMaterial);
                FormedChannelGaugeCombo.SelectedItem = selected.FormedChannelGauge;
                FormedChannelMaterialCombo.SelectedItem = selected.FormedChannelMaterial;
                FloorGaugeCombo.SelectedItem = selected.FloorGauge;
                FloorMaterialCombo.SelectedItem = selected.FloorMaterial;
                SubFloorGaugeCombo.SelectedItem = selected.SubFloorGauge;
                SubFloorMaterialCombo.SelectedItem = selected.SubFloorMaterial;
                PerimeterAngleGaugeCombo.SelectedItem = selected.PerimeterAngleGauge;
                PerimeterAngleMaterialCombo.SelectedItem = selected.PerimeterAngleMaterial;

                FloorThermalCombo.SelectedIndex = string.Equals(selected.FloorThermalBreak, "Yes", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
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

            selected.BaseHeight = (BaseHeightCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
            selected.BaseMaterial = (BaseMatCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
            selected.FormedChannelGauge = FormedChannelGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.FormedChannelMaterial = FormedChannelMaterialCombo.SelectedItem as string ?? string.Empty;
            selected.FloorGauge = FloorGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.FloorMaterial = FloorMaterialCombo.SelectedItem as string ?? string.Empty;
            selected.SubFloorGauge = SubFloorGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.SubFloorMaterial = SubFloorMaterialCombo.SelectedItem as string ?? string.Empty;
            selected.PerimeterAngleGauge = PerimeterAngleGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.PerimeterAngleMaterial = PerimeterAngleMaterialCombo.SelectedItem as string ?? string.Empty;

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

            selected.FloorThermalBreak = (FloorThermalCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "No";
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
            var gridRows = new List<IptVerificationRow>();

            if (surfaceRow is RoofSurfaceRow roof)
            {
                var parts = _iptResult.Parts
                    .Where(p => string.Equals(p.OwnerIamPath, roof.SourceSurfaceIam, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var p in parts)
                {
                    var rule = VerificationEngine.FindRule(p);
                    if (rule != null)
                    {
                        var rowFields = VerificationEngine.BuildRowFields(roof);
                        string expectedGauge = VerificationEngine.ResolveRuleField(rule.GaugeSource, rowFields);
                        string expectedMaterial = VerificationEngine.ResolveRuleField(rule.MaterialSource, rowFields);

                        if (string.Equals(rule.Classification, "Channel", StringComparison.OrdinalIgnoreCase))
                        {
                            expectedMaterial = MaterialsConfig.AdjustExpectedChannel(expectedMaterial, p.ModelNumber);
                        }

                        string expected = ConstructionDataHelper.FormatGaugeAndMaterial(expectedGauge, expectedMaterial);
                        string actual = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL, expectedMaterial);
                        bool isMismatch = !string.IsNullOrWhiteSpace(expected) && Normalize(actual) != Normalize(expected);

                        string field = string.IsNullOrWhiteSpace(rule.FieldName) ? rule.Classification : rule.FieldName;

                        if (string.Equals(rule.VerificationMode, "display", StringComparison.OrdinalIgnoreCase))
                        {
                            isMismatch = false;
                        }

                        AddGridRow(gridRows, p.PartNumber, p.Description, rule.Classification, field, expected, actual, isMismatch, p.FilePath);
                    }
                }

                RoofPartsGrid.ItemsSource = gridRows;
            }
            else if (surfaceRow is WallSurfaceRow wall)
            {
                var parts = _iptResult.Parts
                    .Where(p => string.Equals(p.OwnerIamPath, wall.SourceSurfaceIam, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var p in parts)
                {
                    var rule = VerificationEngine.FindRule(p);
                    if (rule != null)
                    {
                        var rowFields = VerificationEngine.BuildRowFields(wall);
                        string expectedGauge = VerificationEngine.ResolveRuleField(rule.GaugeSource, rowFields);
                        string expectedMaterial = VerificationEngine.ResolveRuleField(rule.MaterialSource, rowFields);

                        if (string.Equals(rule.Classification, "Channel", StringComparison.OrdinalIgnoreCase))
                        {
                            expectedMaterial = MaterialsConfig.AdjustExpectedChannel(expectedMaterial, p.ModelNumber);
                        }

                        string expected = ConstructionDataHelper.FormatGaugeAndMaterial(expectedGauge, expectedMaterial);
                        string actual = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL, expectedMaterial);
                        bool isMismatch = !string.IsNullOrWhiteSpace(expected) && Normalize(actual) != Normalize(expected);

                        string field = string.IsNullOrWhiteSpace(rule.FieldName) ? rule.Classification : rule.FieldName;

                        if (string.Equals(rule.VerificationMode, "display", StringComparison.OrdinalIgnoreCase))
                        {
                            isMismatch = false;
                        }

                        AddGridRow(gridRows, p.PartNumber, p.Description, rule.Classification, field, expected, actual, isMismatch, p.FilePath);
                    }
                }

                WallPartsGrid.ItemsSource = gridRows;
            }
            else if (surfaceRow is BaseSurfaceRow bs)
            {
                var parts = _iptResult.Parts
                    .Where(p => string.Equals(p.OwnerIamPath, bs.SourceSurfaceIam, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var p in parts)
                {
                    var rule = VerificationEngine.FindRule(p);
                    if (rule != null)
                    {
                        var rowFields = VerificationEngine.BuildRowFields(bs);
                        string expectedGauge = VerificationEngine.ResolveRuleField(rule.GaugeSource, rowFields);
                        string expectedMaterial = VerificationEngine.ResolveRuleField(rule.MaterialSource, rowFields);

                        string expected = ConstructionDataHelper.FormatGaugeAndMaterial(expectedGauge, expectedMaterial);
                        string actual = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL, expectedMaterial);

                        if (string.Equals(rule.Classification, "Structural Channel", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(rule.Classification, "Structural Angle", StringComparison.OrdinalIgnoreCase))
                        {
                            actual = p.YCMATL;
                        }

                        bool isMismatch = !string.IsNullOrWhiteSpace(expected) && Normalize(actual) != Normalize(expected);

                        string field = string.IsNullOrWhiteSpace(rule.FieldName) ? rule.Classification : rule.FieldName;

                        if (string.Equals(rule.VerificationMode, "display", StringComparison.OrdinalIgnoreCase))
                        {
                            isMismatch = false;
                        }

                        AddGridRow(gridRows, p.PartNumber, p.Description, rule.Classification, field, expected, actual, isMismatch, p.FilePath);
                    }
                }

                BasePartsGrid.ItemsSource = gridRows;
            }
        }

        // ── Save & Verify Actions ─────────────────────────────────────────────

        private void OnSave(object sender, RoutedEventArgs e)
        {
            PersistenceManager.SaveOverrides(_iamPath, _data);
            MessageBox.Show($"Overrides saved to:\n{PersistenceManager.GetSidecarPath(_iamPath)}",
                "Saved Overrides", MessageBoxButton.OK, MessageBoxImage.Information);
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

            if (result.IsPass)
            {
                MessageBox.Show("✓  All checks passed! No mismatches found across any surfaces.",
                    "Verification Passed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"✗  Verification complete. Found {result.Mismatches.Count} mismatches in this assembly. Review individual surface grids to see discrepancies highlighted in red.",
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
            bool isMismatch,
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
                IsMismatch = isMismatch,
                FilePath = filePath
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
                if (row.IsEditPending)
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
                if (row != null)
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
                    if (row.IsMismatch)
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
                    System.Diagnostics.Debug.WriteLine($"[UCV] Ignored pending edit for unrecognized parameter: {parameter}");
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

            if (groupedEdits.Count == 0)
            {
                MessageBox.Show("No pending edits mapped to writable part properties.", "Write Changes", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Confirm modifications
            var sb = new System.Text.StringBuilder();
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

        private static string FormatGaugeAndMaterial(string gauge, string material, string expectedMaterialHint = null)
        {
            gauge    = (gauge    ?? string.Empty).Trim();
            material = (material ?? string.Empty).Trim();

            // If the gauge is a decimal thickness, try to resolve both gauge and material from the database mapping first
            if (double.TryParse(gauge, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                string hint = !string.IsNullOrEmpty(expectedMaterialHint) ? expectedMaterialHint : material;
                if (MaterialsConfig.ResolveFromThickness(gauge, hint, out string resolvedGauge, out string resolvedMaterial))
                {
                    gauge = resolvedGauge;
                    // If no explicit material override is set (e.g. YCMATL is empty or template default), use the resolved material code (e.g. STL GALV PPC)
                    if (string.IsNullOrEmpty(material) || material.Equals("Steel, Galvanized", StringComparison.OrdinalIgnoreCase) || material.Equals("Steel", StringComparison.OrdinalIgnoreCase) || material.Equals("STL GALV", StringComparison.OrdinalIgnoreCase))
                    {
                        material = resolvedMaterial;
                    }
                }
            }

            string mappedGauge = MaterialsConfig.MapGauge(gauge, !string.IsNullOrEmpty(expectedMaterialHint) ? expectedMaterialHint : material);
            string mappedMaterial = MaterialsConfig.MapMaterial(material);

            if (string.IsNullOrEmpty(mappedGauge) && string.IsNullOrEmpty(mappedMaterial)) return string.Empty;
            if (string.IsNullOrEmpty(mappedGauge))    return mappedMaterial;
            if (string.IsNullOrEmpty(mappedMaterial)) return mappedGauge;
            return $"{mappedGauge} GA {mappedMaterial}";
        }

        private void PopulateDropdowns(List<string> gauges, List<string> materials)
        {
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
                combo.Width = gaugeWidth;
            }

            foreach (ComboBox combo in new[]
            {
                RoofExteriorMaterialCombo, RoofInteriorMaterialCombo, RoofChannelMaterialCombo, RoofTrimMaterialCombo,
                WallExteriorMaterialCombo, WallInteriorMaterialCombo, WallChannelMaterialCombo,
                FormedChannelMaterialCombo, FloorMaterialCombo, SubFloorMaterialCombo, PerimeterAngleMaterialCombo
            })
            {
                combo.Width = materialWidth;
            }

            SizeComboToItems(BaseHeightCombo, "6\"", "8\"", "10\"", "12\"");
            SizeComboToItems(BaseMatCombo, "STL C CHNL", "ALM C CHNL");
            SizeComboToItems(RoofThermalCombo, "Yes", "No");
            SizeComboToItems(WallThermalCombo, "Yes", "No");
            SizeComboToItems(FloorThermalCombo, "Yes", "No");
        }

        private static void SizeComboToItems(ComboBox combo, params string[] items)
        {
            combo.Width = MeasureItemsTextWidth(items) + 28;
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

        private void SelectComboItemByContent(ComboBox combo, string content)
        {
            if (content == null) { combo.SelectedIndex = -1; return; }
            string target = content.Trim();
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item)
                {
                    if (string.Equals(item.Content?.ToString()?.Trim(), target, StringComparison.OrdinalIgnoreCase))
                    {
                        combo.SelectedIndex = i;
                        return;
                    }
                }
                else if (string.Equals(combo.Items[i]?.ToString()?.Trim(), target, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            combo.SelectedIndex = -1;
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
            InventorSelectionHelper.RestoreNormal(_inventorApp);
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
        public bool   IsMismatch    { get; set; }
        public string Status        => IsMismatch ? "✗ Mismatch" : "✓ Pass";
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
