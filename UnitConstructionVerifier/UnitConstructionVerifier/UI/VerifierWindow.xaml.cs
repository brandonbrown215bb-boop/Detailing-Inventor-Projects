using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UnitConstructionVerifier.Engine;
using UnitConstructionVerifier.Models;
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

            RefreshPartsGrid(selected);
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

            RefreshPartsGrid(selected);
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
                FormedChannelMaterialCombo.SelectedItem = selected.FormedChannelMaterialOnly;
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

            RefreshPartsGrid(selected);
        }

        private void UpdateBaseExpectations()
        {
            var selected = BaseSurfaceList.SelectedItem as BaseSurfaceRow;
            if (selected == null) return;

            selected.BaseHeight = (BaseHeightCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
            selected.BaseMaterial = (BaseMatCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
            selected.FormedChannelGauge = FormedChannelGaugeCombo.SelectedItem as string ?? string.Empty;
            selected.FormedChannelMaterialOnly = FormedChannelMaterialCombo.SelectedItem as string ?? string.Empty;
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

            var gridRows = new List<IptVerificationRow>();

            if (surfaceRow is RoofSurfaceRow roof)
            {
                var parts = _iptResult.Parts
                    .Where(p => string.Equals(p.OwnerIamPath, roof.SourceSurfaceIam, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var p in parts)
                {
                    // Classify the part using unified classification
                    string classification = p.GetClassification();
                    bool isLiner    = classification == "Liner";
                    bool isMiscTrim = classification == "Misc Trim" || classification == "Split Cover";
                    bool isTrim     = classification == "Trim";
                    bool isChannel  = classification == "Channel";
                    bool isSkin     = classification == "Skin";

                    // Liners: Gauge & Material only
                    if (isLiner)
                    {
                        string expected = roof.InteriorGaugeAndMaterial ?? string.Empty;
                        string actual   = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                        bool isMismatch = !string.IsNullOrWhiteSpace(expected) && Normalize(actual) != Normalize(expected);
                        AddGridRow(gridRows, p.PartNumber, p.Description, "Liner", "Gauge & Material", expected, actual, isMismatch, p.FilePath);
                    }
                    // Trim (Roof Corner Cap / Roof Cap / SQ PART - TRIM): uses dedicated Trim gauge & material
                    else if (isTrim)
                    {
                        string expected = roof.TrimGaugeAndMaterial ?? string.Empty;
                        string actual   = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                        bool isMismatch = !string.IsNullOrWhiteSpace(expected) && Normalize(actual) != Normalize(expected);
                        AddGridRow(gridRows, p.PartNumber, p.Description, "Trim", "Gauge & Material", expected, actual, isMismatch, p.FilePath);
                    }
                    // Misc Trim (PEAKED ROOF SPLIT COVER, ROOF SEAL-OFF ANGLE): shown, no expected spec
                    else if (isMiscTrim)
                    {
                        string actual = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                        AddGridRow(gridRows, p.PartNumber, p.Description, "Misc Trim", "Gauge & Material", string.Empty, actual, false, p.FilePath);
                    }
                    // C:SC Channels: uses dedicated Channel gauge & material
                    else if (isChannel)
                    {
                        string expected = MaterialsConfig.AdjustExpectedChannel(roof.ChannelGaugeAndMaterial, p.ModelNumber) ?? string.Empty;
                        string actual   = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                        bool isMismatch = !string.IsNullOrWhiteSpace(expected) && Normalize(actual) != Normalize(expected);
                        AddGridRow(gridRows, p.PartNumber, p.Description, "Channel", "Gauge & Material", expected, actual, isMismatch, p.FilePath);
                    }
                    // Skins: Gauge & Material only
                    else if (isSkin)
                    {
                        string expected = roof.ExteriorGaugeAndMaterial ?? string.Empty;
                        string actual   = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                        bool isMismatch = !string.IsNullOrWhiteSpace(expected) && Normalize(actual) != Normalize(expected);
                        AddGridRow(gridRows, p.PartNumber, p.Description, "Skin", "Gauge & Material", expected, actual, isMismatch, p.FilePath);
                    }
                    else
                    {
                        var rule = VerificationEngine.FindRule(p);
                        if (rule != null)
                        {
                            var rowFields = VerificationEngine.BuildRowFields(roof);
                            string expectedGauge = VerificationEngine.ResolveRuleField(rule.GaugeSource, rowFields);
                            string expectedMaterial = VerificationEngine.ResolveRuleField(rule.MaterialSource, rowFields);
                            string expected = ConstructionDataHelper.FormatGaugeAndMaterial(expectedGauge, expectedMaterial);
                            string actual = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                            bool isMismatch = !string.IsNullOrWhiteSpace(expected) && Normalize(actual) != Normalize(expected);
                            
                            string section = string.IsNullOrWhiteSpace(rule.Section) ? "Casing" : rule.Section;
                            string field = string.IsNullOrWhiteSpace(rule.FieldName) ? rule.Classification : rule.FieldName;

                            // Custom rules with display verification mode never show as mismatch in grid
                            if (string.Equals(rule.VerificationMode, "display", StringComparison.OrdinalIgnoreCase))
                                isMismatch = false;

                            AddGridRow(gridRows, p.PartNumber, p.Description, rule.Classification, field, expected, actual, isMismatch, p.FilePath);
                        }
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
                    // Classify the part using unified classification
                    string classification = p.GetClassification();
                    bool isLiner    = classification == "Liner";
                    bool isMiscTrim = classification == "Misc Trim" || classification == "Split Cover";
                    bool isTrim     = classification == "Trim";
                    bool isChannel  = classification == "Channel";
                    bool isSkin     = classification == "Skin";

                    // Liners: Gauge & Material only
                    if (isLiner)
                    {
                        string expected = wall.InteriorGaugeAndMaterial ?? string.Empty;
                        string actual   = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                        bool isMismatch = !string.IsNullOrWhiteSpace(expected) && Normalize(actual) != Normalize(expected);
                        AddGridRow(gridRows, p.PartNumber, p.Description, "Liner", "Gauge & Material", expected, actual, isMismatch, p.FilePath);
                    }
                    // Trim (Roof Corner Cap / Roof Cap / SQ PART - TRIM): Gauge & Material only
                    else if (isTrim)
                    {
                        string expected = wall.ExteriorGaugeAndMaterial ?? string.Empty;
                        string actual   = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                        bool isMismatch = !string.IsNullOrWhiteSpace(expected) && Normalize(actual) != Normalize(expected);
                        AddGridRow(gridRows, p.PartNumber, p.Description, "Trim", "Gauge & Material", expected, actual, isMismatch, p.FilePath);
                    }
                    // Misc Trim / Split Cover: shown, no expected spec
                    else if (isMiscTrim)
                    {
                        string actual = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                        AddGridRow(gridRows, p.PartNumber, p.Description, classification, "Gauge & Material", string.Empty, actual, false, p.FilePath);
                    }
                    // C:SC Channels: Gauge & Material only
                    else if (isChannel)
                    {
                        string expected = MaterialsConfig.AdjustExpectedChannel(wall.ChannelGaugeAndMaterial, p.ModelNumber) ?? string.Empty;
                        string actual   = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                        bool isMismatch = !string.IsNullOrWhiteSpace(expected) && Normalize(actual) != Normalize(expected);
                        AddGridRow(gridRows, p.PartNumber, p.Description, "Channel", "Gauge & Material", expected, actual, isMismatch, p.FilePath);
                    }
                    // Skins: Gauge & Material only
                    else if (isSkin)
                    {
                        string expected = wall.ExteriorGaugeAndMaterial ?? string.Empty;
                        string actual   = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                        bool isMismatch = !string.IsNullOrWhiteSpace(expected) && Normalize(actual) != Normalize(expected);
                        AddGridRow(gridRows, p.PartNumber, p.Description, "Skin", "Gauge & Material", expected, actual, isMismatch, p.FilePath);
                    }
                    else
                    {
                        var rule = VerificationEngine.FindRule(p);
                        if (rule != null)
                        {
                            var rowFields = VerificationEngine.BuildRowFields(wall);
                            string expectedGauge = VerificationEngine.ResolveRuleField(rule.GaugeSource, rowFields);
                            string expectedMaterial = VerificationEngine.ResolveRuleField(rule.MaterialSource, rowFields);
                            string expected = ConstructionDataHelper.FormatGaugeAndMaterial(expectedGauge, expectedMaterial);
                            string actual = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                            bool isMismatch = !string.IsNullOrWhiteSpace(expected) && Normalize(actual) != Normalize(expected);
                            
                            string section = string.IsNullOrWhiteSpace(rule.Section) ? "Casing" : rule.Section;
                            string field = string.IsNullOrWhiteSpace(rule.FieldName) ? rule.Classification : rule.FieldName;

                            // Custom rules with display verification mode never show as mismatch in grid
                            if (string.Equals(rule.VerificationMode, "display", StringComparison.OrdinalIgnoreCase))
                                isMismatch = false;

                            AddGridRow(gridRows, p.PartNumber, p.Description, rule.Classification, field, expected, actual, isMismatch, p.FilePath);
                        }
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
                    string classification = p.GetClassification();

                    if (classification == "Sub-Floor")
                    {
                        if (!string.IsNullOrWhiteSpace(bs.SubFloorGaugeAndMaterial))
                        {
                            string actual = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                            bool isMismatch = Normalize(actual) != Normalize(bs.SubFloorGaugeAndMaterial);
                            AddGridRow(gridRows, p.PartNumber, p.Description, "Sub-Floor", "Gauge & Material", bs.SubFloorGaugeAndMaterial, actual, isMismatch, p.FilePath);
                        }
                    }
                    else if (classification == "Floor Sheet")
                    {
                        if (!string.IsNullOrWhiteSpace(bs.FloorGaugeAndMaterial))
                        {
                            string actual = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                            bool isMismatch = Normalize(actual) != Normalize(bs.FloorGaugeAndMaterial);
                            AddGridRow(gridRows, p.PartNumber, p.Description, "Floor Sheet", "Gauge & Material", bs.FloorGaugeAndMaterial, actual, isMismatch, p.FilePath);
                        }
                    }
                    else
                    {
                        bool isStructuralChannel = classification == "Structural Channel";
                        bool isFormedChannel = classification == "Formed Channel";
                        bool isPerimeterAngle = classification == "Perimeter Angle";

                        if (isStructuralChannel || isFormedChannel || isPerimeterAngle)
                        {
                            if (isStructuralChannel)
                            {
                                if (!string.IsNullOrWhiteSpace(bs.BaseMaterial))
                                {
                                    bool isSteel = bs.BaseMaterial.IndexOf("stl", StringComparison.OrdinalIgnoreCase) >= 0 || 
                                                   bs.BaseMaterial.IndexOf("steel", StringComparison.OrdinalIgnoreCase) >= 0;
                                    string expected = isSteel ? "STL C CHNL" : "ALM C CHNL";
                                    bool isMismatch = Normalize(p.YCMATL) != Normalize(expected);
                                    AddGridRow(gridRows, p.PartNumber, p.Description, "Structural Channel", "Base Material", expected, p.YCMATL, isMismatch, p.FilePath);
                                }
                            }
                            else if (isFormedChannel)
                            {
                                if (!string.IsNullOrWhiteSpace(bs.FormedChannelMaterial))
                                {
                                    string actual = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                                    bool isMismatch = Normalize(actual) != Normalize(bs.FormedChannelMaterial);
                                    AddGridRow(gridRows, p.PartNumber, p.Description, "Formed Channel", "Gauge & Material", bs.FormedChannelMaterial, actual, isMismatch, p.FilePath);
                                }
                            }
                            else if (isPerimeterAngle)
                            {
                                if (!string.IsNullOrWhiteSpace(bs.PerimeterAngleGaugeAndMaterial))
                                {
                                    string actual = FormatGaugeAndMaterial(p.MtlGauge, p.YCMATL);
                                    bool isMismatch = Normalize(actual) != Normalize(bs.PerimeterAngleGaugeAndMaterial);
                                    AddGridRow(gridRows, p.PartNumber, p.Description, "Perimeter Angle", "Gauge & Material", bs.PerimeterAngleGaugeAndMaterial, actual, isMismatch, p.FilePath);
                                }
                            }
                        }
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
            if (RoofPartsGrid.Columns.Count > 5) RoofPartsGrid.Columns[5].IsReadOnly = !isEdit;
            if (WallPartsGrid.Columns.Count > 5) WallPartsGrid.Columns[5].IsReadOnly = !isEdit;
            if (BasePartsGrid.Columns.Count > 5) BasePartsGrid.Columns[5].IsReadOnly = !isEdit;

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

                if (parameter == "Thickness")
                {
                    edits.Thickness = value;
                }
                else if (parameter == "Gauge & Material" || parameter == "Formed Channel Material")
                {
                    PersistenceManager.ParseGaugeAndMaterial(value, out string g, out string m);
                    edits.MtlGauge = g;
                    edits.YCMATL = m;
                }
                else if (parameter == "Base Material")
                {
                    edits.YCMATL = value;
                }
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

        private static string FormatGaugeAndMaterial(string gauge, string material)
        {
            gauge    = (gauge    ?? string.Empty).Trim();
            material = (material ?? string.Empty).Trim();

            // If the gauge is a decimal thickness, try to resolve both gauge and material from the database mapping first
            if (double.TryParse(gauge, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                if (MaterialsConfig.ResolveFromThickness(gauge, out string resolvedGauge, out string resolvedMaterial))
                {
                    gauge = resolvedGauge;
                    // If no explicit material override is set (e.g. YCMATL is empty or template default), use the resolved material code (e.g. STL GALV PPC)
                    if (string.IsNullOrEmpty(material) || material.Equals("Steel, Galvanized", StringComparison.OrdinalIgnoreCase) || material.Equals("Steel", StringComparison.OrdinalIgnoreCase) || material.Equals("STL GALV", StringComparison.OrdinalIgnoreCase))
                    {
                        material = resolvedMaterial;
                    }
                }
            }

            string mappedGauge = MaterialsConfig.MapGauge(gauge);
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
            }
            finally
            {
                _isUpdatingUi = false;
            }
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
