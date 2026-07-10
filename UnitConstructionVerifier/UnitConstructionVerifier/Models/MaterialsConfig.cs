using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace UnitConstructionVerifier.Models
{
    public static class MaterialsConfig
    {
        public static List<string> Gauges { get; } = new List<string>();
        public static List<string> Materials { get; } = new List<string>();
        public static Dictionary<string, string> MaterialMappings { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, string> GaugeMappings { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, string> ThicknessMap { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, string> PartClassifications { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public static List<PartRule> PartRules { get; } = new List<PartRule>();

        private static readonly List<string> DefaultGauges = new List<string>
        {
            "0", "0.063", "0.125", "0.188", "0.25", "000", "0000000", "1", "3", "7", "8", "9",
            "10", "11", "12", "14", "16", "18", "19", "20", "22", "24"
        };

        private static readonly List<string> DefaultMaterials = new List<string>
        {
            "ALM DIA", "ALM EMB", "ALM EXP", "ALM GRATE", "ALM PLATE", "ALM PRF", "ALM PRFA", "ALM PRFB", "ALM SHT", "ALMB DIA",
            "GALV CLOTH", "GALV CORA", "GALV EXPA", "GALV EXPB", "GALV EXPBY", "GALV GRATE", "GALV PRFA", "GALV PRFD", "GALV PRFE",
            "GALV PRFF", "GALV PRFG", "GALV PRFH", "SST304", "SST304 CLOTH", "SST304D", "SST304E", "SST304G", "SST304L", "SST304PA",
            "SST304PB", "SST304PC", "SST316L", "SST409", "STL DIA HR", "STL GALV", "STL GALV PPC", "STL GALV PPG", "STL GALV PPW",
            "STL GALV2", "STL GALV3", "STL GALV4", "STL GALVR", "STL GALVS", "STL HOT ROLL", "STL PLATE", "STL STRUCT HR", "STRUCT GALV"
        };

        static MaterialsConfig()
        {
            ResetToDefaults();
        }

        private static void ResetToDefaults()
        {
            Gauges.Clear();
            Gauges.AddRange(DefaultGauges);

            Materials.Clear();
            Materials.AddRange(DefaultMaterials);

            MaterialMappings.Clear();
            MaterialMappings["STEEL, GALVANIZED"] = "STL GALV";
            MaterialMappings["STEEL"] = "STL GALV";
            MaterialMappings["GALVANIZED"] = "STL GALV";
            MaterialMappings["ALUMINUM"] = "ALM SHT";
            MaterialMappings["STAINLESS STEEL"] = "SST304";
            MaterialMappings["Steel, Mild"] = "STL HOT ROLL";
            MaterialMappings["Steel, Galvanized"] = "STL GALV";
            MaterialMappings["Aluminum 6061"] = "ALM SHT";


            GaugeMappings.Clear();
            GaugeMappings["0.0478"] = "18";
            GaugeMappings["0.0598"] = "16";
            GaugeMappings["0.0747"] = "14";
            GaugeMappings["0.1046"] = "12";
            GaugeMappings["0.1345"] = "10";

            ThicknessMap.Clear();

            PartClassifications.Clear();
            PartClassifications["091-30117-082"] = "Liner";
            PartClassifications["091-30117-084"] = "Liner";
            PartClassifications["091-30117-073"] = "Liner";
            PartClassifications["091-30119-006"] = "Liner";
            PartClassifications["091-30119-007"] = "Misc Trim";
            PartClassifications["091-30117-083"] = "Skin";
            PartClassifications["091-30117-081"] = "Skin";
            PartClassifications["091-30117-177"] = "Skin";
            PartClassifications["091-30117-186"] = "Structural Channel";
            PartClassifications["091-30117-187"] = "Structural Channel";
            PartClassifications["091-30117-188"] = "Structural Channel";
            PartClassifications["091-30117-189"] = "Structural Channel";
            PartClassifications["091-30117-066"] = "Base Accessory";
            PartClassifications["091-30117-067"] = "Base Accessory";
            PartClassifications["091-30117-190"] = "Base Accessory";
            PartClassifications["091-30117-001"] = "Trim";
            PartClassifications["091-30117-058"] = "Trim";
            PartClassifications["091-30117-072"] = "Trim";
            PartClassifications["091-30117-074"] = "Trim";
            PartClassifications["091-30117-076"] = "Misc Trim";
            PartClassifications["091-30117-011"] = "Misc Trim";
            PartClassifications["091-30117-057"] = "Misc Trim";
            PartClassifications["091-30117-049"] = "Misc Trim";
            PartClassifications["091-30117-053"] = "Misc Trim";
            PartClassifications["091-30117-022"] = "Misc Trim";
            PartClassifications["091-30117-195"] = "Misc Trim";
            PartClassifications["091-30117-196"] = "Misc Trim";
            PartClassifications["091-30117-064"] = "Channel";
            PartClassifications["091-30117-065"] = "Channel";
            PartClassifications["091-30117-085"] = "Channel";
            PartClassifications["091-30117-086"] = "Channel";
            PartClassifications["091-30117-078"] = "Channel";
            PartClassifications["091-30117-048"] = "Channel";
            PartClassifications["091-30117-046"] = "Channel";
            PartClassifications["091-30117-077"] = "Channel";
            PartClassifications["091-30117-068"] = "Channel";
            PartClassifications["091-30117-069"] = "Channel";
            PartClassifications["091-30117-070"] = "Channel";
            PartClassifications["091-30117-056"] = "Floor Sheet";
            PartClassifications["091-30117-124"] = "Floor Sheet";
            PartClassifications["091-30117-080"] = "Sub-Floor";
            PartClassifications["091-30117-061"] = "Sub-Floor";
            PartClassifications["091-30117-062"] = "Sub-Floor";
            PartClassifications["091-30117-075"] = "Split Cover";
            PartClassifications["091-30117-087"] = "Split Cover";
            PartClassifications["091-30117-089"] = "Split Cover";
            PartClassifications["091-30117-051"] = "Formed Channel";
            PartClassifications["091-30117-054"] = "Perimeter Angle";
            PartClassifications["091-30117-055"] = "Perimeter Angle";
            PartClassifications["091-30117-079"] = "Perimeter Angle";

            PartRules.Clear();
        }

        public static void Initialize()
        {
            try
            {
                string assemblyPath = typeof(MaterialsConfig).Assembly.Location;
                string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? string.Empty;
                string configPath = Path.Combine(assemblyDir, "materials_config.json");

                if (!File.Exists(configPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[UCV] Config file {configPath} not found. Using built-in defaults.");
                    ResetToDefaults();
                }
                else
                {
                    string json = File.ReadAllText(configPath);
                    var data = JsonConvert.DeserializeObject<ConfigDataSchema>(json);
                    if (data != null)
                    {
                        if (data.Gauges != null && data.Gauges.Count > 0)
                        {
                            Gauges.Clear();
                            Gauges.AddRange(data.Gauges);
                        }
                        if (data.Materials != null && data.Materials.Count > 0)
                        {
                            Materials.Clear();
                            Materials.AddRange(data.Materials);
                        }
                        if (data.MaterialMappings != null)
                        {
                            MaterialMappings.Clear();
                            foreach (var kvp in data.MaterialMappings)
                            {
                                MaterialMappings[kvp.Key] = kvp.Value;
                            }
                        }
                        if (data.GaugeMappings != null)
                        {
                            GaugeMappings.Clear();
                            foreach (var kvp in data.GaugeMappings)
                            {
                                GaugeMappings[kvp.Key] = kvp.Value;
                            }
                        }
                        if (data.PartClassifications != null)
                        {
                            PartClassifications.Clear();
                            foreach (var kvp in data.PartClassifications)
                            {
                                PartClassifications[kvp.Key] = kvp.Value;
                            }
                        }
                        if (data.PartRules != null)
                        {
                            PartRules.Clear();
                            PartRules.AddRange(data.PartRules);

                            // Auto-register any stock numbers declared in rules into the
                            // PartClassifications table so stock-number lookup resolves them
                            // before the description-keyword fallback is reached.
                            foreach (var rule in PartRules)
                            {
                                if (rule.StockNumbers == null) continue;
                                foreach (var sn in rule.StockNumbers)
                                {
                                    if (!string.IsNullOrWhiteSpace(sn))
                                        PartClassifications[sn.Trim()] = rule.Classification;
                                }
                            }
                        }
                    }
                }

                // Load materials thickness map database
                string mapPath = Path.Combine(assemblyDir, "materials_thickness_map.json");
                if (File.Exists(mapPath))
                {
                    string mapJson = File.ReadAllText(mapPath);
                    var mapData = JsonConvert.DeserializeObject<Dictionary<string, string>>(mapJson);
                    if (mapData != null)
                    {
                        ThicknessMap.Clear();
                        foreach (var kvp in mapData)
                        {
                            ThicknessMap[kvp.Key] = kvp.Value;
                        }
                        System.Diagnostics.Debug.WriteLine($"[UCV] Loaded {ThicknessMap.Count} entries from thickness map.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UCV] Failed to load materials config: {ex.Message}");
                ResetToDefaults();
            }
        }

        public static string MapMaterial(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string key = raw.Trim();
            if (MaterialMappings.TryGetValue(key, out string mapped))
            {
                return mapped;
            }
            return key;
        }

        public static string MapGauge(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string key = raw.Trim();

            // Try standard string mapping
            if (GaugeMappings.TryGetValue(key, out string mapped))
            {
                return mapped;
            }

            // Try parsing double to handle different decimal representations
            if (double.TryParse(key, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                if (val > 0 && val % 1 == 0)
                {
                    return val.ToString("0");
                }

                string normKey = val.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (GaugeMappings.TryGetValue(normKey, out string mappedVal))
                {
                    return mappedVal;
                }

                // Reverse lookup in JCI ThicknessMap database
                // 1. Precise match (tolerance 0.000009)
                foreach (var kvp in ThicknessMap)
                {
                    if (double.TryParse(kvp.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double mapVal))
                    {
                        if (Math.Abs(val - mapVal) < 0.000009)
                        {
                            string gaugePart = ExtractGaugePart(kvp.Key);
                            if (!string.IsNullOrEmpty(gaugePart))
                            {
                                return gaugePart;
                            }
                        }
                    }
                }

                // 2. Closest match fallback (tolerance 0.00015)
                string bestKey = null;
                double minDiff = double.MaxValue;
                foreach (var kvp in ThicknessMap)
                {
                    if (double.TryParse(kvp.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double mapVal))
                    {
                        double diff = Math.Abs(val - mapVal);
                        if (diff < 0.00015 && diff < minDiff)
                        {
                            minDiff = diff;
                            bestKey = kvp.Key;
                        }
                    }
                }

                if (bestKey != null)
                {
                    string gaugePart = ExtractGaugePart(bestKey);
                    if (!string.IsNullOrEmpty(gaugePart))
                    {
                        return gaugePart;
                    }
                }
            }

            return key;
        }

        private static string ExtractGaugePart(string matchedKey)
        {
            string gaugePart = string.Empty;
            foreach (char c in matchedKey)
            {
                if (char.IsDigit(c) || c == '.')
                {
                    gaugePart += c;
                }
                else
                {
                    break;
                }
            }
            return gaugePart;
        }

        public static bool ResolveFromThickness(string thicknessStr, out string gauge, out string material)
        {
            gauge = string.Empty;
            material = string.Empty;
            if (string.IsNullOrWhiteSpace(thicknessStr)) return false;

            if (double.TryParse(thicknessStr.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                // 1. Precise match (tolerance 0.000009)
                foreach (var kvp in ThicknessMap)
                {
                    if (double.TryParse(kvp.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double mapVal))
                    {
                        if (Math.Abs(val - mapVal) < 0.000009)
                        {
                            return ParseKey(kvp.Key, out gauge, out material);
                        }
                    }
                }

                // 2. Closest match fallback (tolerance 0.00015)
                string bestKey = null;
                double minDiff = double.MaxValue;
                foreach (var kvp in ThicknessMap)
                {
                    if (double.TryParse(kvp.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double mapVal))
                    {
                        double diff = Math.Abs(val - mapVal);
                        if (diff < 0.00015 && diff < minDiff)
                        {
                            minDiff = diff;
                            bestKey = kvp.Key;
                        }
                    }
                }

                if (bestKey != null)
                {
                    return ParseKey(bestKey, out gauge, out material);
                }
            }
            return false;
        }

        private static bool ParseKey(string key, out string gauge, out string material)
        {
            gauge = string.Empty;
            material = string.Empty;
            // Find where the gauge number ends and material code starts
            int i = 0;
            while (i < key.Length && (char.IsDigit(key[i]) || key[i] == '.'))
            {
                i++;
            }
            if (i > 0 && i < key.Length)
            {
                gauge = key.Substring(0, i);
                material = key.Substring(i).Trim();
                return true;
            }
            return false;
        }

        public static string GetPartClassification(string modelOrStockNumber, string description)
        {
            if (!string.IsNullOrWhiteSpace(modelOrStockNumber))
            {
                string key = modelOrStockNumber.Trim();
                if (PartClassifications.TryGetValue(key, out string cls))
                {
                    return cls;
                }
            }

            if (description == null) return string.Empty;

            // Rule-based description-keyword classification.
            // Runs after stock-number lookup but before the legacy hardcoded fallbacks,
            // so config-defined rules take precedence over generic keyword matching.
            foreach (var rule in PartRules)
            {
                if (rule.DescriptionKeywords == null) continue;
                foreach (var keyword in rule.DescriptionKeywords)
                {
                    if (!string.IsNullOrWhiteSpace(keyword) &&
                        description.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return rule.Classification;
                    }
                }
            }

            return "Unknown";
        }

        public static string AdjustExpectedChannel(string expectedChannel, string modelNumber)
        {
            if (string.IsNullOrWhiteSpace(expectedChannel)) return expectedChannel;

            // Check if the part is one of the non-automating wall channel parts/brackets
            if (modelNumber == "091-30117-078" || 
                modelNumber == "091-30117-048" || 
                modelNumber == "091-30117-046" || 
                modelNumber == "091-30117-077")
            {
                // Strip trailing '2', '3', or '4' suffix if present (e.g., "STL GALV3" -> "STL GALV")
                if (expectedChannel.EndsWith("2")) return expectedChannel.Substring(0, expectedChannel.Length - 1);
                if (expectedChannel.EndsWith("3")) return expectedChannel.Substring(0, expectedChannel.Length - 1);
                if (expectedChannel.EndsWith("4")) return expectedChannel.Substring(0, expectedChannel.Length - 1);
            }
            return expectedChannel;
        }

        private class ConfigDataSchema
        {
            public List<string>? Gauges { get; set; }
            public List<string>? Materials { get; set; }
            public Dictionary<string, string>? MaterialMappings { get; set; }
            public Dictionary<string, string>? GaugeMappings { get; set; }
            public Dictionary<string, string>? PartClassifications { get; set; }
            public List<PartRule>? PartRules { get; set; }
        }
    }

    /// <summary>
    /// A config-driven classification and verification rule for a part type.
    /// Defined in the <c>PartRules</c> array of <c>materials_config.json</c>.
    /// </summary>
    public sealed class PartRule
    {
        /// <summary>The classification string returned by <see cref="MaterialsConfig.GetPartClassification"/>.</summary>
        public string Classification { get; set; } = string.Empty;

        /// <summary>Exact model/stock numbers that map to this rule (highest priority).</summary>
        public List<string> StockNumbers { get; set; } = new List<string>();

        /// <summary>Description substrings (case-insensitive) that trigger this rule when no stock number matches.</summary>
        public List<string> DescriptionKeywords { get; set; } = new List<string>();

        /// <summary>
        /// How to resolve the expected gauge. Supported formats:
        /// <list type="bullet">
        ///   <item><c>fixed:&lt;value&gt;</c> — always use the literal gauge, e.g. <c>fixed:16</c></item>
        ///   <item><c>borrow:&lt;FieldName&gt;</c> — copy from a named field on the surface row, e.g. <c>borrow:InteriorLinerGauge</c></item>
        /// </list>
        /// </summary>
        public string GaugeSource { get; set; } = string.Empty;

        /// <summary>
        /// How to resolve the expected material. Supported formats: <c>fixed:&lt;value&gt;</c> or <c>borrow:&lt;FieldName&gt;</c>.
        /// </summary>
        public string MaterialSource { get; set; } = string.Empty;

        /// <summary><c>mismatch</c> — generate a mismatch on failure; <c>display</c> — show in grid only, never flag.</summary>
        public string VerificationMode { get; set; } = "mismatch";

        /// <summary>Grid section label shown in the verification window (e.g. <c>Casing</c>, <c>Base</c>).</summary>
        public string Section { get; set; } = "Casing";

        /// <summary>Column header / field name shown in the mismatch grid.</summary>
        public string FieldName { get; set; } = string.Empty;
    }
}
