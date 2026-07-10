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
                        {
                            VerifyWithRule(rule, ipt, BuildRowFields(row), result);
                        }
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
                        {
                            VerifyWithRule(rule, ipt, BuildRowFields(row), result);
                        }
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
                    {
                        var rule = FindRule(ipt);
                        if (rule != null)
                        {
                            VerifyWithRule(rule, ipt, BuildRowFields(row), result);
                        }
                    }
                }
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Per-section comparers

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

        public static Dictionary<string, string> BuildRowFields(BaseSurfaceRow row)
        {
            // Map structural material to "STL C CHNL" or "ALM C CHNL" based on BaseMaterial content
            bool isSteel = (row.BaseMaterial ?? string.Empty).IndexOf("stl", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           (row.BaseMaterial ?? string.Empty).IndexOf("steel", StringComparison.OrdinalIgnoreCase) >= 0;
            string structuralMaterial = isSteel ? "STL C CHNL" : "ALM C CHNL";
            string structuralAngleMaterial = isSteel ? "STL ANGLE" : "ALM ANGLE";

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["BaseStructuralMaterial"] = structuralMaterial,
                ["BaseStructuralAngleMaterial"] = structuralAngleMaterial,
                ["FormedChannelGauge"] = row.FormedChannelGauge,
                ["FormedChannelMaterial"] = row.FormedChannelMaterial,
                ["FormedChannelGaugeAndMaterial"] = row.FormedChannelGaugeAndMaterial,
                ["FloorGauge"] = row.FloorGauge,
                ["FloorMaterial"] = row.FloorMaterial,
                ["FloorGaugeAndMaterial"] = row.FloorGaugeAndMaterial,
                ["SubFloorGauge"] = row.SubFloorGauge,
                ["SubFloorMaterial"] = row.SubFloorMaterial,
                ["SubFloorGaugeAndMaterial"] = row.SubFloorGaugeAndMaterial,
                ["PerimeterAngleGauge"] = row.PerimeterAngleGauge,
                ["PerimeterAngleMaterial"] = row.PerimeterAngleMaterial,
                ["PerimeterAngleGaugeAndMaterial"] = row.PerimeterAngleGaugeAndMaterial,
            };
        }

        public static string ResolveRuleField(string spec, Dictionary<string, string> rowFields)
        {
            if (string.IsNullOrWhiteSpace(spec)) return string.Empty;

            // Handle multiple fallback specs separated by "||"
            if (spec.Contains("||"))
            {
                var parts = spec.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    string resolved = ResolveRuleField(part.Trim(), rowFields);
                    if (!string.IsNullOrWhiteSpace(resolved))
                        return resolved;
                }
                return string.Empty;
            }

            const string fixedPrefix  = "fixed:";
            const string borrowPrefix = "borrow:";
            const string ifPrefix     = "if:";

            if (spec.StartsWith(fixedPrefix, StringComparison.OrdinalIgnoreCase))
                return spec.Substring(fixedPrefix.Length).Trim();

            if (spec.StartsWith(borrowPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string fieldName = spec.Substring(borrowPrefix.Length).Trim();
                return rowFields.TryGetValue(fieldName, out string val) ? (val ?? string.Empty) : string.Empty;
            }

            if (spec.StartsWith(ifPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Format: if:FieldName_contains:Keyword?TrueSpec:FalseSpec
                // Or:     if:FieldName=Value?TrueSpec:FalseSpec
                string body = spec.Substring(ifPrefix.Length);
                int qMark = body.IndexOf('?');
                if (qMark > 0)
                {
                    string condition = body.Substring(0, qMark).Trim();
                    string branches = body.Substring(qMark + 1).Trim();
                    int colon = -1;
                    for (int i = 0; i < branches.Length; i++)
                    {
                        if (branches[i] == ':')
                        {
                            string right = branches.Substring(i + 1).Trim();
                            if (right.StartsWith("fixed:", StringComparison.OrdinalIgnoreCase) ||
                                right.StartsWith("borrow:", StringComparison.OrdinalIgnoreCase) ||
                                right.StartsWith("if:", StringComparison.OrdinalIgnoreCase))
                            {
                                colon = i;
                                break;
                            }
                        }
                    }
                    if (colon > 0)
                    {
                        string trueSpec = branches.Substring(0, colon).Trim();
                        string falseSpec = branches.Substring(colon + 1).Trim();

                        bool condResult = false;
                        if (condition.Contains("_contains:"))
                        {
                            int idx = condition.IndexOf("_contains:", StringComparison.OrdinalIgnoreCase);
                            string fieldName = condition.Substring(0, idx).Trim();
                            string keyword = condition.Substring(idx + "_contains:".Length).Trim();
                            if (rowFields.TryGetValue(fieldName, out string val) && val != null)
                            {
                                condResult = val.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
                            }
                        }
                        else if (condition.Contains("="))
                        {
                            int idx = condition.IndexOf('=');
                            string fieldName = condition.Substring(0, idx).Trim();
                            string expectedVal = condition.Substring(idx + 1).Trim();
                            if (rowFields.TryGetValue(fieldName, out string val) && val != null)
                            {
                                condResult = string.Equals(val.Trim(), expectedVal, StringComparison.OrdinalIgnoreCase);
                            }
                        }

                        string selectedSpec = condResult ? trueSpec : falseSpec;
                        return ResolveRuleField(selectedSpec, rowFields);
                    }
                }
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

            // Special case: Channel expected channel material requires thickness-suffix stripping rules
            if (string.Equals(rule.Classification, "Channel", StringComparison.OrdinalIgnoreCase))
            {
                expectedMaterial = MaterialsConfig.AdjustExpectedChannel(expectedMaterial, ipt.ModelNumber);
            }

            // Build the expected string the same way the UI does: "N GA MATERIAL" (or just material if gauge is empty)
            string expected = ConstructionDataHelper.FormatGaugeAndMaterial(expectedGauge, expectedMaterial);
            if (string.IsNullOrWhiteSpace(expected)) return;

            // Special case: Structural Channel/Angle compares YCMATL directly (no gauge format)
            string actual = FormatGaugeAndMaterial(ipt.MtlGauge, ipt.YCMATL);
            if (string.Equals(rule.Classification, "Structural Channel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rule.Classification, "Structural Angle", StringComparison.OrdinalIgnoreCase))
            {
                actual = ipt.YCMATL;
            }

            string section = string.IsNullOrWhiteSpace(rule.Section)    ? "Casing"           : rule.Section;
            string field   = string.IsNullOrWhiteSpace(rule.FieldName)  ? rule.Classification : rule.FieldName;

            Compare(result, ipt, section, field, expected, actual);
        }
    }
}
