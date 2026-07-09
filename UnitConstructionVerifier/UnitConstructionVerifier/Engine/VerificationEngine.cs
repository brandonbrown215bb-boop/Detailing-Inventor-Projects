using System;
using System.Collections.Generic;
using System.Linq;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Engine
{
    /// <summary>
    /// Compares user-edited <see cref="UnitConstructionData"/> values against
    /// the IPT iProperties extracted from the assembly.
    /// Returns a <see cref="VerificationResult"/> listing every field mismatch,
    /// grouped by surface IAM → IPT part.
    /// </summary>
    public sealed class VerificationEngine
    {
        private readonly UnitConstructionData _userData;
        private readonly IptScanResult        _iptData;

        public VerificationEngine(UnitConstructionData userData, IptScanResult iptData)
        {
            _userData = userData;
            _iptData  = iptData;
        }

        public VerificationResult Run()
        {
            var result = new VerificationResult();

            // ── Roof Casing verification ─────────────────────────────────────
            if (_userData.RoofRows != null)
            {
                foreach (var row in _userData.RoofRows)
                {
                    var candidates = _iptData.Parts
                        .Where(p => string.Equals(p.OwnerIamPath, row.SourceSurfaceIam, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var ipt in candidates)
                        VerifyRoofWallIpt(row.Thickness, row.ExteriorGaugeAndMaterial, row.InteriorGaugeAndMaterial,
                                          row.ChannelGaugeAndMaterial, row.TrimGaugeAndMaterial, row.ThermalBreak, ipt, result);
                }
            }

            // ── Wall Casing verification ─────────────────────────────────────
            if (_userData.WallRows != null)
            {
                foreach (var row in _userData.WallRows)
                {
                    var candidates = _iptData.Parts
                        .Where(p => string.Equals(p.OwnerIamPath, row.SourceSurfaceIam, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var ipt in candidates)
                        VerifyRoofWallIpt(row.Thickness, row.ExteriorGaugeAndMaterial, row.InteriorGaugeAndMaterial,
                                          row.ExteriorGaugeAndMaterial, row.ExteriorGaugeAndMaterial, row.ThermalBreak, ipt, result);
                }
            }

            // ── Base & Floor verification ────────────────────────────────────
            if (_userData.BaseRows != null)
            {
                foreach (var row in _userData.BaseRows)
                {
                    var candidates = _iptData.Parts
                        .Where(p => string.Equals(p.OwnerIamPath, row.SourceSurfaceIam, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var ipt in candidates)
                        VerifyBaseIpt(row, ipt, result);
                }
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Per-section comparers
        // ─────────────────────────────────────────────────────────────────────

        private void VerifyRoofWallIpt(
            string expectedThickness,
            string expectedExterior,
            string expectedInterior,
            string expectedChannel,
            string expectedTrim,
            string expectedThermalBreak,
            IptProperties ipt,
            VerificationResult result)
        {
            // Classify the part
            bool isLiner = ipt.Description.IndexOf("liner", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isMiscTrim = ipt.Description.IndexOf("peaked roof split cover", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              ipt.Description.IndexOf("roof seal-off angle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              string.Equals(ipt.ModelNumber, "091-30119-007", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(ipt.ModelNumber, "091-30117-076", StringComparison.OrdinalIgnoreCase);
            bool isTrim  = !isMiscTrim &&
                           (ipt.Description.IndexOf("roof corner cap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ipt.Description.IndexOf("roof cap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ipt.Description.IndexOf("sq part - trim", StringComparison.OrdinalIgnoreCase) >= 0);
            bool isChannel = ipt.Description.StartsWith("C:SC", StringComparison.OrdinalIgnoreCase);

            // 1. Liners: Gauge & Material only (no thickness)
            if (isLiner)
            {
                if (!string.IsNullOrWhiteSpace(expectedInterior))
                {
                    string actual = FormatGaugeAndMaterial(ipt.MtlGauge, ipt.YCMATL);
                    Compare(result, ipt, "Casing", "Interior Gauge & Material", expectedInterior, actual);
                }
                return;
            }

            // 2. Misc Trim (PEAKED ROOF SPLIT COVER, ROOF SEAL-OFF ANGLE): display only, no mismatch
            if (isMiscTrim) return;

            // 3. Trim (Roof Corner Cap / Roof Cap / SQ PART - TRIM): dedicated trim gauge & material
            if (isTrim)
            {
                if (!string.IsNullOrWhiteSpace(expectedTrim))
                {
                    string actual = FormatGaugeAndMaterial(ipt.MtlGauge, ipt.YCMATL);
                    Compare(result, ipt, "Casing", "Trim Gauge & Material", expectedTrim, actual);
                }
                return;
            }

            // 4. C:SC Channels: dedicated channel gauge & material
            if (isChannel)
            {
                if (!string.IsNullOrWhiteSpace(expectedChannel))
                {
                    string actual = FormatGaugeAndMaterial(ipt.MtlGauge, ipt.YCMATL);
                    Compare(result, ipt, "Casing", "Channel Gauge & Material", expectedChannel, actual);
                }
                return;
            }

            // 5. Skins: Gauge & Material only (no thickness)
            bool isSkin = ipt.Description.IndexOf("skin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         ipt.Description.IndexOf("panel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         ipt.Description.IndexOf("post", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isSkin && !string.IsNullOrWhiteSpace(expectedExterior))
            {
                string actual = FormatGaugeAndMaterial(ipt.MtlGauge, ipt.YCMATL);
                Compare(result, ipt, "Casing", "Exterior Gauge & Material", expectedExterior, actual);
            }
        }

        private void VerifyBaseIpt(BaseSurfaceRow row, IptProperties ipt, VerificationResult result)
        {
            // 1. Sub-floor sheet
            if (ipt.IsSubFloor)
            {
                if (!string.IsNullOrWhiteSpace(row.SubFloorGaugeAndMaterial))
                {
                    string actual = FormatGaugeAndMaterial(ipt.MtlGauge, ipt.YCMATL);
                    Compare(result, ipt, "Base", "Sub-Floor Gauge & Material", row.SubFloorGaugeAndMaterial, actual);
                }
            }
            // 2. Main Floor sheet
            else if (ipt.Description.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     ipt.Description.IndexOf("deck", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!string.IsNullOrWhiteSpace(row.FloorGaugeAndMaterial))
                {
                    string actual = FormatGaugeAndMaterial(ipt.MtlGauge, ipt.YCMATL);
                    Compare(result, ipt, "Base", "Floor Gauge & Material", row.FloorGaugeAndMaterial, actual);
                }
            }
            // 3. Base structural steel
            else
            {
                bool isStructuralChannel = ipt.Description.StartsWith("CHN:STRUCT", StringComparison.OrdinalIgnoreCase);
                bool isFormedChannel = ipt.Description.IndexOf("Channel, Formed", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isPerimeterAngle = ipt.Description.IndexOf("perimeter angle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        ipt.Description.IndexOf("angle, perimeter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        (ipt.Description.IndexOf("perimeter", StringComparison.OrdinalIgnoreCase) >= 0 && ipt.Description.IndexOf("angle", StringComparison.OrdinalIgnoreCase) >= 0);

                if (isStructuralChannel || isFormedChannel || isPerimeterAngle)
                {
                    if (isStructuralChannel)
                    {
                        if (!string.IsNullOrWhiteSpace(row.BaseMaterial))
                        {
                            bool isSteel = row.BaseMaterial.IndexOf("stl", StringComparison.OrdinalIgnoreCase) >= 0 || 
                                           row.BaseMaterial.IndexOf("steel", StringComparison.OrdinalIgnoreCase) >= 0;
                            string expected = isSteel ? "STL C CHNL" : "ALM C CHNL";
                            Compare(result, ipt, "Base", "Base Structural Material", expected, ipt.YCMATL);
                        }
                    }
                    else if (isFormedChannel)
                    {
                        if (!string.IsNullOrWhiteSpace(row.FormedChannelMaterial))
                        {
                            string actual = FormatGaugeAndMaterial(ipt.MtlGauge, ipt.YCMATL);
                            Compare(result, ipt, "Base", "Formed Channel Material", row.FormedChannelMaterial, actual);
                        }
                    }
                    else if (isPerimeterAngle)
                    {
                        if (!string.IsNullOrWhiteSpace(row.PerimeterAngleGaugeAndMaterial))
                        {
                            string actual = FormatGaugeAndMaterial(ipt.MtlGauge, ipt.YCMATL);
                            Compare(result, ipt, "Base", "Perimeter Angle Gauge & Material", row.PerimeterAngleGaugeAndMaterial, actual);
                        }
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static void Compare(
            VerificationResult result,
            IptProperties ipt,
            string section,
            string fieldName,
            string expected,
            string actual)
        {
            expected = Normalize(expected);
            actual   = Normalize(actual);
            if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)) return;

            result.Mismatches.Add(new IptMismatch
            {
                SurfaceIamPath  = ipt.OwnerIamPath,
                SurfaceIamName  = System.IO.Path.GetFileNameWithoutExtension(ipt.OwnerIamPath),
                IptPartNumber   = ipt.PartNumber,
                IptFilePath     = ipt.FilePath,
                FieldName       = fieldName,
                ExpectedValue   = expected,
                ActualValue     = actual,
                Section         = section,
            });
        }

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
    }
}
