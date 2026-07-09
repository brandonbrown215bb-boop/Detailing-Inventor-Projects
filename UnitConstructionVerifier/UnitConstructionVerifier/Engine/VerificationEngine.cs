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
                    {
                        var rule = FindRule(ipt);
                        if (rule != null)
                            VerifyWithRule(rule, ipt, BuildRowFields(row), result);
                        else
                            VerifyRoofWallIpt(row.Thickness, row.ExteriorGaugeAndMaterial, row.InteriorGaugeAndMaterial,
                                              row.ChannelGaugeAndMaterial, row.TrimGaugeAndMaterial, row.ThermalBreak, ipt, result);
                    }
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
                    {
                        var rule = FindRule(ipt);
                        if (rule != null)
                            VerifyWithRule(rule, ipt, BuildRowFields(row), result);
                        else
                            VerifyRoofWallIpt(row.Thickness, row.ExteriorGaugeAndMaterial, row.InteriorGaugeAndMaterial,
                                              row.ChannelGaugeAndMaterial, row.ExteriorGaugeAndMaterial, row.ThermalBreak, ipt, result);
                    }
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
            // Classify the part using unified classification helper
            string classification = ipt.GetClassification();
            bool isLiner    = classification == "Liner";
            bool isMiscTrim = classification == "Misc Trim" || classification == "Split Cover";
            bool isTrim     = classification == "Trim";
            bool isChannel  = classification == "Channel";

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
                    string finalExpected = MaterialsConfig.AdjustExpectedChannel(expectedChannel, ipt.ModelNumber);
                    Compare(result, ipt, "Casing", "Channel Gauge & Material", finalExpected, actual);
                }
                return;
            }

            // 5. Skins: Gauge & Material only (no thickness)
            bool isSkin = classification == "Skin";

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

        // ─────────────────────────────────────────────────────────────────────
        // Config-driven rule dispatch helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the first <see cref="PartRule"/> whose classification matches the part,
        /// or <c>null</c> if the part should be handled by the fixed dispatch chain.
        /// </summary>
        public static PartRule? FindRule(IptProperties ipt)
        {
            string cls = ipt.GetClassification();
            foreach (var rule in MaterialsConfig.PartRules)
            {
                if (string.Equals(rule.Classification, cls, StringComparison.OrdinalIgnoreCase))
                    return rule;
            }
            return null;
        }

        /// <summary>
        /// Builds a field-name → value map from a <see cref="RoofSurfaceRow"/>,
        /// used to resolve <c>borrow:FieldName</c> sources in <see cref="PartRule"/>.
        /// </summary>
        public static Dictionary<string, string> BuildRowFields(RoofSurfaceRow row)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ExteriorSkinGauge"]     = row.ExteriorSkinGauge,
                ["ExteriorSkinMaterial"]  = row.ExteriorSkinMaterial,
                ["InteriorLinerGauge"]    = row.InteriorLinerGauge,
                ["InteriorLinerMaterial"] = row.InteriorLinerMaterial,
                ["ChannelSkinGauge"]      = row.ChannelSkinGauge,
                ["ChannelSkinMaterial"]   = row.ChannelSkinMaterial,
                ["TrimSkinGauge"]         = row.TrimSkinGauge,
                ["TrimSkinMaterial"]      = row.TrimSkinMaterial,
            };
        }

        /// <summary>
        /// Builds a field-name → value map from a <see cref="WallSurfaceRow"/>.
        /// </summary>
        public static Dictionary<string, string> BuildRowFields(WallSurfaceRow row)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ExteriorSkinGauge"]     = row.ExteriorSkinGauge,
                ["ExteriorSkinMaterial"]  = row.ExteriorSkinMaterial,
                ["InteriorLinerGauge"]    = row.InteriorLinerGauge,
                ["InteriorLinerMaterial"] = row.InteriorLinerMaterial,
                ["ChannelSkinGauge"]      = row.ChannelSkinGauge,
                ["ChannelSkinMaterial"]   = row.ChannelSkinMaterial,
            };
        }

        /// <summary>
        /// Resolves a <c>fixed:value</c> or <c>borrow:FieldName</c> spec against the row's field map.
        /// Returns an empty string if the spec is unrecognised or the borrowed field is empty.
        /// </summary>
        public static string ResolveRuleField(string spec, Dictionary<string, string> rowFields)
        {
            if (string.IsNullOrWhiteSpace(spec)) return string.Empty;

            const string fixedPrefix  = "fixed:";
            const string borrowPrefix = "borrow:";

            if (spec.StartsWith(fixedPrefix, StringComparison.OrdinalIgnoreCase))
                return spec.Substring(fixedPrefix.Length).Trim();

            if (spec.StartsWith(borrowPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string fieldName = spec.Substring(borrowPrefix.Length).Trim();
                return rowFields.TryGetValue(fieldName, out string val) ? (val ?? string.Empty) : string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// Verifies an IPT part according to a config-driven <see cref="PartRule"/>.
        /// Resolves expected gauge and material from the rule's source specs,
        /// then compares against the part's actual properties.
        /// </summary>
        private void VerifyWithRule(
            PartRule rule,
            IptProperties ipt,
            Dictionary<string, string> rowFields,
            VerificationResult result)
        {
            // Display-only rules are never flagged as mismatches.
            if (string.Equals(rule.VerificationMode, "display", StringComparison.OrdinalIgnoreCase))
                return;

            string expectedGauge    = ResolveRuleField(rule.GaugeSource,    rowFields);
            string expectedMaterial = ResolveRuleField(rule.MaterialSource,  rowFields);

            // Build the expected string the same way the UI does: "N GA MATERIAL"
            string expected = ConstructionDataHelper.FormatGaugeAndMaterial(expectedGauge, expectedMaterial);
            if (string.IsNullOrWhiteSpace(expected)) return;

            string actual  = FormatGaugeAndMaterial(ipt.MtlGauge, ipt.YCMATL);
            string section = string.IsNullOrWhiteSpace(rule.Section)    ? "Casing"           : rule.Section;
            string field   = string.IsNullOrWhiteSpace(rule.FieldName)  ? rule.Classification : rule.FieldName;

            Compare(result, ipt, section, field, expected, actual);
        }
    }
}
