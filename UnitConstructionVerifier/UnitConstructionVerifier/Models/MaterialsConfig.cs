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

                // Reverse lookup in JCI ThicknessMap database (handles 5-digit precision formats e.g. 0.05604 -> "16")
                foreach (var kvp in ThicknessMap)
                {
                    if (double.TryParse(kvp.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double mapVal))
                    {
                        if (Math.Abs(val - mapVal) < 0.000009)
                        {
                            string matchedKey = kvp.Key;
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
                            if (!string.IsNullOrEmpty(gaugePart))
                            {
                                return gaugePart;
                            }
                        }
                    }
                }
            }

            return key;
        }

        public static bool ResolveFromThickness(string thicknessStr, out string gauge, out string material)
        {
            gauge = string.Empty;
            material = string.Empty;
            if (string.IsNullOrWhiteSpace(thicknessStr)) return false;

            if (double.TryParse(thicknessStr.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                foreach (var kvp in ThicknessMap)
                {
                    if (double.TryParse(kvp.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double mapVal))
                    {
                        if (Math.Abs(val - mapVal) < 0.000009)
                        {
                            string key = kvp.Key; // e.g. "16STL GALV PPC" or "10STL HOT ROLL"
                            
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
                        }
                    }
                }
            }
            return false;
        }

        private class ConfigDataSchema
        {
            public List<string>? Gauges { get; set; }
            public List<string>? Materials { get; set; }
            public Dictionary<string, string>? MaterialMappings { get; set; }
            public Dictionary<string, string>? GaugeMappings { get; set; }
        }
    }
}
