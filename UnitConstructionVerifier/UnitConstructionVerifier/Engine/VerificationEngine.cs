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
            var coveredIams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in _userData.RoofRows ?? new List<RoofSurfaceRow>())
                CheckSurface(row.SourceSurfaceIam, "Roof", BuildRowFields(row), coveredIams, result);
            foreach (var row in _userData.WallRows ?? new List<WallSurfaceRow>())
                CheckSurface(row.SourceSurfaceIam, "Wall", BuildRowFields(row), coveredIams, result);
            foreach (var row in _userData.BaseRows ?? new List<BaseSurfaceRow>())
                CheckSurface(row.SourceSurfaceIam, "Base", BuildRowFields(row), coveredIams, result);

            // Parts whose surface IAM produced no construction row still get an outcome.
            foreach (var ipt in _iptData.Parts.Where(p => !coveredIams.Contains(p.OwnerIamPath ?? string.Empty)))
                Record(CheckPart(ipt, null, null), result);

            return result;
        }

        private void CheckSurface(
            string surfaceIam,
            string surfaceKind,
            Dictionary<string, string> rowFields,
            HashSet<string> coveredIams,
            VerificationResult result)
        {
            coveredIams.Add(surfaceIam ?? string.Empty);
            foreach (var ipt in _iptData.Parts.Where(p => string.Equals(p.OwnerIamPath, surfaceIam, StringComparison.OrdinalIgnoreCase)))
                Record(CheckPart(ipt, rowFields, surfaceKind), result);
        }

        private static void Record(PartCheck check, VerificationResult result)
        {
            result.Checks.Add(check);
            if (check.Status != PartCheckStatus.Mismatch) return;

            result.Mismatches.Add(new IptMismatch
            {
                SurfaceIamPath  = check.SurfaceIamPath,
                SurfaceIamName  = System.IO.Path.GetFileNameWithoutExtension(check.SurfaceIamPath),
                IptPartNumber   = check.IptPartNumber,
                IptFilePath     = check.IptFilePath,
                FieldName       = check.FieldName,
                ExpectedValue   = Normalize(check.ExpectedValue),
                ActualValue     = Normalize(check.ActualValue),
                Section         = check.Section,
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // Per-section comparers

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        public static string Normalize(string s)
        {
            if (s == null) return string.Empty;
            return s.Trim().Replace("\"", "").Replace("'", "").ToUpperInvariant();
        }

        public static string FormatGaugeAndMaterial(string gauge, string material, string expectedMaterialHint = null)
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
                    if (string.IsNullOrEmpty(material) || 
                        material.Equals("Steel, Galvanized", StringComparison.OrdinalIgnoreCase) || 
                        material.Equals("Steel", StringComparison.OrdinalIgnoreCase) || 
                        material.Equals("STL GALV", StringComparison.OrdinalIgnoreCase) ||
                        (material.Equals("STL HOT ROLL", StringComparison.OrdinalIgnoreCase) && 
                         !string.IsNullOrEmpty(expectedMaterialHint) && 
                         (expectedMaterialHint.ToUpperInvariant().Contains("ALM") || expectedMaterialHint.ToUpperInvariant().Contains("ALUMINUM"))))
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
                ["RoofCapGauge"]          = row.RoofCapGauge,
                ["RoofCapMaterial"]       = row.RoofCapMaterial,
                ["RoofAccessoryGauge"]    = row.RoofAccessoryGauge,
                ["RoofAccessoryMaterial"] = row.RoofAccessoryMaterial,
            };
        }

        public static Dictionary<string, string> BuildRowFields(WallSurfaceRow row)
        {
            string skinGauge = row.IsSharedWall ? row.InteriorLinerGauge : row.ExteriorSkinGauge;
            string skinMat = row.IsSharedWall ? row.InteriorLinerMaterial : row.ExteriorSkinMaterial;

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ExteriorSkinGauge"]     = skinGauge,
                ["ExteriorSkinMaterial"]  = skinMat,
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
                ["DrainSupportAngleGauge"] = row.DrainSupportAngleGauge,
                ["DrainSupportAngleMaterial"] = row.DrainSupportAngleMaterial,
                ["FloorAttachmentAngleGauge"] = row.FloorAttachmentAngleGauge,
                ["FloorAttachmentAngleMaterial"] = row.FloorAttachmentAngleMaterial,
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
        /// Verifies one IPT part against its config-driven <see cref="PartRule"/>. This is the single
        /// check used by both <see cref="Run"/> and the verifier window's parts grids.
        /// </summary>
        /// <param name="rowFields">Fields of the part's surface row, or <c>null</c> when its IAM has no row.</param>
        /// <param name="surfaceKind">"Roof", "Wall" or "Base"; used in Not checked reasons.</param>
        public static PartCheck CheckPart(IptProperties ipt, Dictionary<string, string>? rowFields, string? surfaceKind)
        {
            var check = new PartCheck
            {
                SurfaceIamPath = ipt.OwnerIamPath ?? string.Empty,
                IptPartNumber  = ipt.PartNumber,
                IptFilePath    = ipt.FilePath,
                Classification = ipt.GetClassification(),
                IsModified     = ipt.IsModified,
            };

            var rule = FindRule(ipt);
            if (rule == null)
            {
                check.Status = PartCheckStatus.NotInConfig;
                check.Reason = "No rule in materials_config.json covers this part.";
                return check;
            }

            check.Section   = string.IsNullOrWhiteSpace(rule.Section)   ? "Casing"            : rule.Section;
            check.FieldName = string.IsNullOrWhiteSpace(rule.FieldName) ? rule.Classification : rule.FieldName;

            if (rowFields == null)
            {
                check.Status = PartCheckStatus.NotChecked;
                check.Reason = "Its surface IAM has no construction row, so there is nothing to compare against.";
                return check;
            }

            string expectedGauge    = ResolveRuleField(rule.GaugeSource,   rowFields);
            string expectedMaterial = ResolveRuleField(rule.MaterialSource, rowFields);

            // Special case: Channel expected channel material requires thickness-suffix stripping rules
            if (string.Equals(rule.Classification, "Channel", StringComparison.OrdinalIgnoreCase))
            {
                expectedMaterial = MaterialsConfig.AdjustExpectedChannel(expectedMaterial, ipt.ModelNumber);
            }

            // "N GA MATERIAL" (or just material if gauge is empty)
            check.ExpectedValue = ConstructionDataHelper.FormatGaugeAndMaterial(expectedGauge, expectedMaterial);

            // Special case: Structural Channel/Angle compares YCMATL directly (no gauge format)
            check.ActualValue = IsStructural(rule) ? ipt.YCMATL : FormatGaugeAndMaterial(ipt.MtlGauge, ipt.YCMATL, expectedMaterial);

            if (string.Equals(rule.VerificationMode, "display", StringComparison.OrdinalIgnoreCase))
            {
                check.Status = PartCheckStatus.DisplayOnly;
                check.Reason = "Display-only rule; never flagged.";
                return check;
            }

            if (string.IsNullOrWhiteSpace(check.ExpectedValue))
            {
                var referenced = ReferencedFields(rule.GaugeSource).Concat(ReferencedFields(rule.MaterialSource))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var missing = referenced.Where(f => !rowFields.ContainsKey(f)).ToList();

                check.Status = PartCheckStatus.NotChecked;
                check.Reason = missing.Count > 0
                    ? $"'{rule.Classification}' expects {string.Join(", ", missing)}, which {surfaceKind?.ToLowerInvariant() ?? "this"} surfaces don't have. Is the part in the right surface?"
                    : $"No expected value: {string.Join(", ", referenced)} is blank for this surface (inactive side, or not in the config; set it in the surface row and Save Overrides to check).";
                return check;
            }

            check.Status = string.Equals(Normalize(check.ExpectedValue), Normalize(check.ActualValue), StringComparison.OrdinalIgnoreCase)
                ? PartCheckStatus.Pass
                : PartCheckStatus.Mismatch;
            return check;
        }

        private static bool IsStructural(PartRule rule) =>
            string.Equals(rule.Classification, "Structural Channel", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rule.Classification, "Structural Angle", StringComparison.OrdinalIgnoreCase);

        /// <summary>Row field names a source spec reads (<c>borrow:</c> targets and <c>if:</c> conditions).</summary>
        private static IEnumerable<string> ReferencedFields(string spec)
        {
            if (string.IsNullOrWhiteSpace(spec)) yield break;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(spec, @"borrow:\s*([A-Za-z]+)"))
                yield return m.Groups[1].Value;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(spec, @"if:\s*([A-Za-z]+?)(?:_contains:|=)"))
                yield return m.Groups[1].Value;
        }
    }
}
