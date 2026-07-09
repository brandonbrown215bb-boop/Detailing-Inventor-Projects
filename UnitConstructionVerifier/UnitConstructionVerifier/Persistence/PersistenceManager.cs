using System;
using System.IO;
using Newtonsoft.Json;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Persistence
{
    /// <summary>
    /// Saves and loads user override values as a JSON sidecar file placed next
    /// to the IAM: <c>&lt;IamDirectory&gt;\.construction_verify.json</c>
    /// </summary>
    public static class PersistenceManager
    {
        private const string SidecarFileName = ".construction_verify.json";

        public static string GetSidecarPath(string iamPath)
        {
            string dir = Path.GetDirectoryName(iamPath) ?? string.Empty;
            return Path.Combine(dir, SidecarFileName);
        }

        // ── Save ──────────────────────────────────────────────────────────────

        public static void SaveOverrides(string iamPath, UnitConstructionData data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(GetSidecarPath(iamPath), json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UCV] PersistenceManager.Save: {ex.Message}");
            }
        }

        // ── Load ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Loads saved overrides from the sidecar file and merges them into
        /// <paramref name="data"/>. Extracted (live) values are preserved for
        /// any field that was not overridden.
        /// </summary>
        public static void LoadOverrides(string iamPath, UnitConstructionData data)
        {
            string path = GetSidecarPath(iamPath);
            if (!File.Exists(path)) return;

            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var saved = JsonConvert.DeserializeObject<UnitConstructionData>(json);
                if (saved is null) return;

                MergeRoofRows(data, saved);
                MergeWallRows(data, saved);
                MergeBaseRows(data, saved);
                MergeOtherConstruction(data, saved);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UCV] PersistenceManager.Load: {ex.Message}");
            }
        }

        // ── Merge helpers ─────────────────────────────────────────────────────

        public static void ParseGaugeAndMaterial(string input, out string gauge, out string material)
        {
            gauge = string.Empty;
            material = string.Empty;
            if (string.IsNullOrWhiteSpace(input)) return;

            input = input.Trim();

            // Check if there is "GA" in the string
            int gaIndex = input.IndexOf(" GA ", StringComparison.OrdinalIgnoreCase);
            if (gaIndex >= 0)
            {
                gauge = input.Substring(0, gaIndex).Trim();
                material = input.Substring(gaIndex + 4).Trim();
                return;
            }

            // Fallback: match leading digits/decimals or special strings like 000
            var match = System.Text.RegularExpressions.Regex.Match(input, @"^(\d+(?:\.\d+)?|0000000|000)?\s*(.*)$");
            if (match.Success)
            {
                gauge = match.Groups[1]?.Value?.Trim() ?? string.Empty;
                material = match.Groups[2]?.Value?.Trim() ?? string.Empty;
            }
        }

        private static void MergeRoofRows(UnitConstructionData live, UnitConstructionData saved)
        {
            if (saved.RoofRows == null) return;
            foreach (var savedRow in saved.RoofRows)
            {
                foreach (var liveRow in live.RoofRows)
                {
                    if (!string.Equals(liveRow.PartNumber, savedRow.PartNumber,
                            StringComparison.OrdinalIgnoreCase)) continue;

                    if (!string.IsNullOrWhiteSpace(savedRow.Thickness))
                        liveRow.Thickness = savedRow.Thickness;
                    if (!string.IsNullOrWhiteSpace(savedRow.ExteriorPaint))
                        liveRow.ExteriorPaint = savedRow.ExteriorPaint;
                    
                    if (!string.IsNullOrWhiteSpace(savedRow.ExteriorSkinGauge))
                        liveRow.ExteriorSkinGauge = savedRow.ExteriorSkinGauge;
                    if (!string.IsNullOrWhiteSpace(savedRow.ExteriorSkinMaterial))
                        liveRow.ExteriorSkinMaterial = savedRow.ExteriorSkinMaterial;

                    // Backward compatibility: if split fields are empty but old combined field is present
                    if (string.IsNullOrEmpty(savedRow.ExteriorSkinGauge) && string.IsNullOrEmpty(savedRow.ExteriorSkinMaterial) && !string.IsNullOrWhiteSpace(savedRow.ExteriorGaugeAndMaterial))
                    {
                        ParseGaugeAndMaterial(savedRow.ExteriorGaugeAndMaterial, out string g, out string m);
                        if (!string.IsNullOrEmpty(g)) liveRow.ExteriorSkinGauge = g;
                        if (!string.IsNullOrEmpty(m)) liveRow.ExteriorSkinMaterial = m;
                    }

                    if (!string.IsNullOrWhiteSpace(savedRow.InteriorLinerGauge))
                        liveRow.InteriorLinerGauge = savedRow.InteriorLinerGauge;
                    if (!string.IsNullOrWhiteSpace(savedRow.InteriorLinerMaterial))
                        liveRow.InteriorLinerMaterial = savedRow.InteriorLinerMaterial;

                    // Backward compatibility: if split fields are empty but old combined field is present
                    if (string.IsNullOrEmpty(savedRow.InteriorLinerGauge) && string.IsNullOrEmpty(savedRow.InteriorLinerMaterial) && !string.IsNullOrWhiteSpace(savedRow.InteriorGaugeAndMaterial))
                    {
                        ParseGaugeAndMaterial(savedRow.InteriorGaugeAndMaterial, out string g, out string m);
                        if (!string.IsNullOrEmpty(g)) liveRow.InteriorLinerGauge = g;
                        if (!string.IsNullOrEmpty(m)) liveRow.InteriorLinerMaterial = m;
                    }

                    if (!string.IsNullOrWhiteSpace(savedRow.ChannelSkinGauge))
                        liveRow.ChannelSkinGauge = savedRow.ChannelSkinGauge;
                    if (!string.IsNullOrWhiteSpace(savedRow.ChannelSkinMaterial))
                        liveRow.ChannelSkinMaterial = savedRow.ChannelSkinMaterial;

                    // Backward compatibility: if split fields are empty but old combined field is present
                    if (string.IsNullOrEmpty(savedRow.ChannelSkinGauge) && string.IsNullOrEmpty(savedRow.ChannelSkinMaterial) && !string.IsNullOrWhiteSpace(savedRow.ChannelGaugeAndMaterial))
                    {
                        ParseGaugeAndMaterial(savedRow.ChannelGaugeAndMaterial, out string g, out string m);
                        if (!string.IsNullOrEmpty(g)) liveRow.ChannelSkinGauge = g;
                        if (!string.IsNullOrEmpty(m)) liveRow.ChannelSkinMaterial = m;
                    }

                    if (!string.IsNullOrWhiteSpace(savedRow.TrimSkinGauge))
                        liveRow.TrimSkinGauge = savedRow.TrimSkinGauge;
                    if (!string.IsNullOrWhiteSpace(savedRow.TrimSkinMaterial))
                        liveRow.TrimSkinMaterial = savedRow.TrimSkinMaterial;

                    // Backward compatibility: if split fields are empty but old combined field is present
                    if (string.IsNullOrEmpty(savedRow.TrimSkinGauge) && string.IsNullOrEmpty(savedRow.TrimSkinMaterial) && !string.IsNullOrWhiteSpace(savedRow.TrimGaugeAndMaterial))
                    {
                        ParseGaugeAndMaterial(savedRow.TrimGaugeAndMaterial, out string g, out string m);
                        if (!string.IsNullOrEmpty(g)) liveRow.TrimSkinGauge = g;
                        if (!string.IsNullOrEmpty(m)) liveRow.TrimSkinMaterial = m;
                    }

                    if (!string.IsNullOrWhiteSpace(savedRow.InsulationThicknessAndMaterial))
                        liveRow.InsulationThicknessAndMaterial = savedRow.InsulationThicknessAndMaterial;
                    if (!string.IsNullOrWhiteSpace(savedRow.ThermalBreak))
                        liveRow.ThermalBreak = savedRow.ThermalBreak;
                    break;
                }
            }
        }

        private static void MergeWallRows(UnitConstructionData live, UnitConstructionData saved)
        {
            if (saved.WallRows == null) return;
            foreach (var savedRow in saved.WallRows)
            {
                foreach (var liveRow in live.WallRows)
                {
                    if (!string.Equals(liveRow.PartNumber, savedRow.PartNumber,
                            StringComparison.OrdinalIgnoreCase)) continue;

                    if (!string.IsNullOrWhiteSpace(savedRow.Thickness))
                        liveRow.Thickness = savedRow.Thickness;
                    if (!string.IsNullOrWhiteSpace(savedRow.ExteriorPaint))
                        liveRow.ExteriorPaint = savedRow.ExteriorPaint;
                    
                    if (!string.IsNullOrWhiteSpace(savedRow.ExteriorSkinGauge))
                        liveRow.ExteriorSkinGauge = savedRow.ExteriorSkinGauge;
                    if (!string.IsNullOrWhiteSpace(savedRow.ExteriorSkinMaterial))
                        liveRow.ExteriorSkinMaterial = savedRow.ExteriorSkinMaterial;

                    // Backward compatibility
                    if (string.IsNullOrEmpty(savedRow.ExteriorSkinGauge) && string.IsNullOrEmpty(savedRow.ExteriorSkinMaterial) && !string.IsNullOrWhiteSpace(savedRow.ExteriorGaugeAndMaterial))
                    {
                        ParseGaugeAndMaterial(savedRow.ExteriorGaugeAndMaterial, out string g, out string m);
                        if (!string.IsNullOrEmpty(g)) liveRow.ExteriorSkinGauge = g;
                        if (!string.IsNullOrEmpty(m)) liveRow.ExteriorSkinMaterial = m;
                    }

                    if (!string.IsNullOrWhiteSpace(savedRow.InteriorLinerGauge))
                        liveRow.InteriorLinerGauge = savedRow.InteriorLinerGauge;
                    if (!string.IsNullOrWhiteSpace(savedRow.InteriorLinerMaterial))
                        liveRow.InteriorLinerMaterial = savedRow.InteriorLinerMaterial;

                    // Backward compatibility
                    if (string.IsNullOrEmpty(savedRow.InteriorLinerGauge) && string.IsNullOrEmpty(savedRow.InteriorLinerMaterial) && !string.IsNullOrWhiteSpace(savedRow.InteriorGaugeAndMaterial))
                    {
                        ParseGaugeAndMaterial(savedRow.InteriorGaugeAndMaterial, out string g, out string m);
                        if (!string.IsNullOrEmpty(g)) liveRow.InteriorLinerGauge = g;
                        if (!string.IsNullOrEmpty(m)) liveRow.InteriorLinerMaterial = m;
                    }

                    if (!string.IsNullOrWhiteSpace(savedRow.ChannelSkinGauge))
                        liveRow.ChannelSkinGauge = savedRow.ChannelSkinGauge;
                    if (!string.IsNullOrWhiteSpace(savedRow.ChannelSkinMaterial))
                        liveRow.ChannelSkinMaterial = savedRow.ChannelSkinMaterial;

                    // Backward compatibility
                    if (string.IsNullOrEmpty(savedRow.ChannelSkinGauge) && string.IsNullOrEmpty(savedRow.ChannelSkinMaterial) && !string.IsNullOrWhiteSpace(savedRow.ChannelGaugeAndMaterial))
                    {
                        ParseGaugeAndMaterial(savedRow.ChannelGaugeAndMaterial, out string g, out string m);
                        if (!string.IsNullOrEmpty(g)) liveRow.ChannelSkinGauge = g;
                        if (!string.IsNullOrEmpty(m)) liveRow.ChannelSkinMaterial = m;
                    }

                    if (!string.IsNullOrWhiteSpace(savedRow.InsulationThicknessAndMaterial))
                        liveRow.InsulationThicknessAndMaterial = savedRow.InsulationThicknessAndMaterial;
                    if (!string.IsNullOrWhiteSpace(savedRow.ThermalBreak))
                        liveRow.ThermalBreak = savedRow.ThermalBreak;
                    break;
                }
            }
        }

        private static void MergeBaseRows(UnitConstructionData live, UnitConstructionData saved)
        {
            if (saved.BaseRows == null) return;
            foreach (var savedRow in saved.BaseRows)
            {
                foreach (var liveRow in live.BaseRows)
                {
                    if (!string.Equals(liveRow.PartNumber, savedRow.PartNumber,
                            StringComparison.OrdinalIgnoreCase)) continue;

                    if (!string.IsNullOrWhiteSpace(savedRow.BaseHeight))
                        liveRow.BaseHeight = savedRow.BaseHeight;
                    if (!string.IsNullOrWhiteSpace(savedRow.BaseMaterial))
                        liveRow.BaseMaterial = savedRow.BaseMaterial;
                    if (!string.IsNullOrWhiteSpace(savedRow.BasePaint))
                        liveRow.BasePaint = savedRow.BasePaint;

                    // Formed Channel split merge
                    if (!string.IsNullOrWhiteSpace(savedRow.FormedChannelGauge))
                        liveRow.FormedChannelGauge = savedRow.FormedChannelGauge;
                    if (!string.IsNullOrWhiteSpace(savedRow.FormedChannelMaterialOnly))
                        liveRow.FormedChannelMaterialOnly = savedRow.FormedChannelMaterialOnly;

                    // Backward compatibility for Formed Channel
                    if (string.IsNullOrEmpty(savedRow.FormedChannelGauge) && string.IsNullOrEmpty(savedRow.FormedChannelMaterialOnly) && !string.IsNullOrWhiteSpace(savedRow.FormedChannelMaterial))
                    {
                        ParseGaugeAndMaterial(savedRow.FormedChannelMaterial, out string g, out string m);
                        if (!string.IsNullOrEmpty(g)) liveRow.FormedChannelGauge = g;
                        if (!string.IsNullOrEmpty(m)) liveRow.FormedChannelMaterialOnly = m;
                    }

                    // Floor split merge
                    if (!string.IsNullOrWhiteSpace(savedRow.FloorGauge))
                        liveRow.FloorGauge = savedRow.FloorGauge;
                    if (!string.IsNullOrWhiteSpace(savedRow.FloorMaterial))
                        liveRow.FloorMaterial = savedRow.FloorMaterial;

                    // Backward compatibility for Floor
                    if (string.IsNullOrEmpty(savedRow.FloorGauge) && string.IsNullOrEmpty(savedRow.FloorMaterial) && !string.IsNullOrWhiteSpace(savedRow.FloorGaugeAndMaterial))
                    {
                        ParseGaugeAndMaterial(savedRow.FloorGaugeAndMaterial, out string g, out string m);
                        if (!string.IsNullOrEmpty(g)) liveRow.FloorGauge = g;
                        if (!string.IsNullOrEmpty(m)) liveRow.FloorMaterial = m;
                    }

                    if (!string.IsNullOrWhiteSpace(savedRow.FloorPaint))
                        liveRow.FloorPaint = savedRow.FloorPaint;
                    if (!string.IsNullOrWhiteSpace(savedRow.FloorInsulation))
                        liveRow.FloorInsulation = savedRow.FloorInsulation;
                    if (!string.IsNullOrWhiteSpace(savedRow.FloorThermalBreak))
                        liveRow.FloorThermalBreak = savedRow.FloorThermalBreak;

                    // Sub-Floor split merge
                    if (!string.IsNullOrWhiteSpace(savedRow.SubFloorGauge))
                        liveRow.SubFloorGauge = savedRow.SubFloorGauge;
                    if (!string.IsNullOrWhiteSpace(savedRow.SubFloorMaterial))
                        liveRow.SubFloorMaterial = savedRow.SubFloorMaterial;

                    // Backward compatibility for Sub-Floor
                    if (string.IsNullOrEmpty(savedRow.SubFloorGauge) && string.IsNullOrEmpty(savedRow.SubFloorMaterial) && !string.IsNullOrWhiteSpace(savedRow.SubFloorGaugeAndMaterial))
                    {
                        ParseGaugeAndMaterial(savedRow.SubFloorGaugeAndMaterial, out string g, out string m);
                        if (!string.IsNullOrEmpty(g)) liveRow.SubFloorGauge = g;
                        if (!string.IsNullOrEmpty(m)) liveRow.SubFloorMaterial = m;
                    }

                    // Perimeter Angle split merge
                    if (!string.IsNullOrWhiteSpace(savedRow.PerimeterAngleGauge))
                        liveRow.PerimeterAngleGauge = savedRow.PerimeterAngleGauge;
                    if (!string.IsNullOrWhiteSpace(savedRow.PerimeterAngleMaterial))
                        liveRow.PerimeterAngleMaterial = savedRow.PerimeterAngleMaterial;

                    // Backward compatibility for Perimeter Angle
                    if (string.IsNullOrEmpty(savedRow.PerimeterAngleGauge) && string.IsNullOrEmpty(savedRow.PerimeterAngleMaterial) && !string.IsNullOrWhiteSpace(savedRow.PerimeterAngleGaugeAndMaterial))
                    {
                        ParseGaugeAndMaterial(savedRow.PerimeterAngleGaugeAndMaterial, out string g, out string m);
                        if (!string.IsNullOrEmpty(g)) liveRow.PerimeterAngleGauge = g;
                        if (!string.IsNullOrEmpty(m)) liveRow.PerimeterAngleMaterial = m;
                    }

                    break;
                }
            }
        }

        private static void MergeOtherConstruction(UnitConstructionData live, UnitConstructionData saved)
        {
            if (saved.OtherConstruction == null) return;
            var l = live.OtherConstruction;
            var s = saved.OtherConstruction;

            if (!string.IsNullOrEmpty(s.UpturnedLipHeight))
            {
                l.UpturnedLip       = s.UpturnedLip;
                l.UpturnedLipHeight = s.UpturnedLipHeight;
            }
            if (!string.IsNullOrEmpty(s.CurbRestHeight))
            {
                l.CurbRest       = s.CurbRest;
                l.CurbRestHeight = s.CurbRestHeight;
            }
        }
    }
}
