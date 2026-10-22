using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Extraction
{
    /// <summary>
    /// Converts raw extracted <see cref="ConfigData"/> lists, <see cref="EngineeringData"/>,
    /// and <see cref="IptScanResult"/> into the user-facing <see cref="UnitConstructionData"/>
    /// that pre-populates the WPF form.
    /// </summary>
    public sealed class ConstructionDataBuilder
    {
        private readonly List<ConfigData> _configs;
        private readonly EngineeringData? _eng;
        private readonly IptScanResult    _ipt;

        public ConstructionDataBuilder(List<ConfigData> configs, EngineeringData? eng, IptScanResult ipt)
        {
            _configs = configs ?? new List<ConfigData>();
            _eng     = eng;
            _ipt     = ipt;
        }

        public UnitConstructionData Build()
        {
            var data = new UnitConstructionData();

            var surfaceTypes = _configs
                .Select(c => c.SurfaceType)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToList();
            data.SurfaceType = string.Join(", ", surfaceTypes);

            // Build Roof and Wall rows
            BuildRoofAndWallRows(data, _configs);

            // Build Base rows
            BuildBaseRows(data, _configs);

            // Build Other Construction specs
            BuildOtherConstruction(data);

            return data;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Casing (Roof & Wall)
        // ─────────────────────────────────────────────────────────────────────

        private void BuildRoofAndWallRows(UnitConstructionData data, List<ConfigData> configs)
        {
            var sortedConfigs = configs
                .OrderByDescending(GetConfigXCoordinate)
                .ToList();

            foreach (var config in sortedConfigs)
            {
                string type = (config.SurfaceType ?? string.Empty).ToUpperInvariant();
                bool isRoof = type.Contains("ROOF");
                bool isWall = type.Contains("WALL");

                if (!isRoof && !isWall) continue;

                string skinMat = string.Empty;
                string skinGauge = string.Empty;
                string linerMat = string.Empty;
                string linerGauge = string.Empty;
                string thickness = string.Empty;
                string insulStr = string.Empty;
                bool isThermalBreak = false;

                var segNames = config.SurfaceSegmentList.Select(s => s.FullSegmentName).ToList();
                string segsStr = string.Join(", ", segNames);

                if (config.SurfaceSegmentList.Count > 0)
                {
                    // Each surface uses its own side of the unit (roofs: Top). The majority vote across
                    // all sides is only a fallback when the surface's side is unknown.
                    string? side = isRoof ? "Top" : config.GetUnitSide();
                    skinMat = SideValue(config, side, s => SkinOf(s, side!).Material) ?? DominantSkinMaterialForConfig(config);
                    skinGauge = SideValue(config, side, s => SkinOf(s, side!).Gauge) ?? DominantSkinGaugeForConfig(config);
                    linerMat = SideValue(config, side, s => LinerOf(s, side!).Material) ?? DominantLinerMaterialForConfig(config);
                    linerGauge = SideValue(config, side, s => LinerOf(s, side!).Gauge) ?? DominantLinerGaugeForConfig(config);

                    var firstSeg = config.SurfaceSegmentList.First();
                    thickness = GetWallThickness(config, firstSeg);
                    isThermalBreak = string.Equals(
                        firstSeg.HousingStyle ?? config.HousingStyle,
                        "ThermalBreak",
                        StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    if (config.NominalSurfaceThickness.HasValue && config.NominalSurfaceThickness > 0)
                    {
                        thickness = $"{config.NominalSurfaceThickness.Value:0.##}\"";
                    }
                    isThermalBreak = string.Equals(config.HousingStyle, "ThermalBreak", StringComparison.OrdinalIgnoreCase);
                }

                var insul = config.InsulationList
                    .Where(i => !string.IsNullOrEmpty(i.InsulationType))
                    .FirstOrDefault();

                if (insul is not null)
                {
                    string thick = insul.NominalThickness.HasValue
                        ? $"{insul.NominalThickness:0.##}\""
                        : string.Empty;
                    insulStr = string.IsNullOrEmpty(thick)
                        ? insul.InsulationType ?? string.Empty
                        : $"{thick} {insul.InsulationType}".Trim();
                }

                string partNum = System.IO.Path.GetFileNameWithoutExtension(config.SourceIamPath);

                // Standard casing channels (064/065/085/086) are always 16 GA STL GALV + panel thickness (2/3/4).
                // Sloped outdoor roof channels (068/069/070) and brackets drop the suffix
                // (MaterialsConfig.AdjustExpectedChannel). formedChannelMaterialType is deliberately not used:
                // a unit whose config says Outdoor but has standard roof channels (Tiger Team FINAL) must be flagged.
                string channelMat = ChannelMaterialFor(thickness);
                string channelGauge = "16";

                if (isRoof)
                {
                    // Roof trim (roof caps) starts from the wall exterior (the roof's side skins), not the roof top skin.
                    // Roofs without segments (sequence-fallback surfaces) borrow the unit's wall exterior.
                    string wallExteriorMat = SideSkinMaterial(config) ?? UnitWallExteriorMaterial() ?? string.Empty;
                    string capGauge = wallExteriorMat.Length == 0 ? string.Empty
                        : _eng?.Constant("Roof_RoofCap_MaterialGauge_Default") is { Length: > 0 } g ? g : "16";
                    string accessoryGauge = _eng?.Constant("Roof_RoofSealOffAngle_SealOffAngle_MaterialGauge_Default") is { Length: > 0 } ag ? ag : "16";
                    string accessoryMat = _eng?.Constant("Roof_RoofSealOffAngle_SealOffAngle_MaterialType_Default") is { Length: > 0 } am ? am : "STL GALV";

                    var row = new RoofSurfaceRow
                    {
                        PartNumber = partNum,
                        Segments = segsStr,
                        Thickness = thickness,
                        ExteriorPaint = string.Empty, // Roof has no exterior paint column
                        ExteriorSkinGauge = skinGauge,
                        ExteriorSkinMaterial = skinMat,
                        InteriorLinerGauge = linerGauge,
                        InteriorLinerMaterial = linerMat,
                        ChannelSkinGauge = channelGauge,
                        ChannelSkinMaterial = channelMat,
                        TrimSkinGauge = capGauge,
                        TrimSkinMaterial = wallExteriorMat,
                        RoofAccessoryGauge = accessoryGauge,
                        RoofAccessoryMaterial = accessoryMat,
                        InsulationThicknessAndMaterial = insulStr,
                        ThermalBreak = isThermalBreak ? "Yes" : "No",
                        SourceSurfaceIam = config.SourceIamPath
                    };
                    data.RoofRows.Add(row);
                }
                else
                {
                    string paint = config.SurfaceSegmentList.FirstOrDefault()?.FloorPaintType ?? string.Empty;
                    string wallChannelMat = channelMat;

                    bool isShared = config.IsSharedWall();
                    if (isShared)
                    {
                        skinGauge = linerGauge;
                        skinMat = linerMat;
                    }

                    var row = new WallSurfaceRow
                    {
                        PartNumber = partNum,
                        Segments = segsStr,
                        IsSharedWall = isShared,
                        Thickness = thickness,
                        ExteriorPaint = paint,
                        ExteriorSkinGauge = skinGauge,
                        ExteriorSkinMaterial = skinMat,
                        InteriorLinerGauge = linerGauge,
                        InteriorLinerMaterial = linerMat,
                        ChannelSkinGauge = channelGauge,
                        ChannelSkinMaterial = wallChannelMat,
                        InsulationThicknessAndMaterial = insulStr,
                        ThermalBreak = isThermalBreak ? "Yes" : "No",
                        SourceSurfaceIam = config.SourceIamPath
                    };
                    data.WallRows.Add(row);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Base & Floor
        // ─────────────────────────────────────────────────────────────────────

        private void BuildBaseRows(UnitConstructionData data, List<ConfigData> configs)
        {
            var baseConfigs = configs
                .Where(c => (c.SurfaceType ?? string.Empty).ToUpperInvariant().Contains("BASE"))
                .OrderByDescending(GetConfigXCoordinate)
                .ToList();

            foreach (var config in baseConfigs)
            {
                var baseIpts = _ipt.Parts
                    .Where(p => string.Equals(p.OwnerIamPath, config.SourceIamPath, StringComparison.OrdinalIgnoreCase) &&
                                !p.IsSubFloor && !string.IsNullOrEmpty(p.YCMATL))
                    .ToList();

                string segsStr = string.Join(", ", config.SurfaceSegmentList.Select(s => s.FullSegmentName));
                string partNum = System.IO.Path.GetFileNameWithoutExtension(config.SourceIamPath);

                // Expectations come from the engineering config only, never from the scanned parts,
                // so a base whose parts are consistently wrong is still flagged.
                bool isSteel = config.StructuralMaterialType == null ||
                               config.StructuralMaterialType.IndexOf("steel", StringComparison.OrdinalIgnoreCase) >= 0;
                string baseMat = isSteel ? "STL C CHNL" : "ALM C CHNL";
                string formedMatOnly = !string.IsNullOrWhiteSpace(config.FormedChannelMaterialType)
                    ? MaterialsConfig.MapMaterial(config.FormedChannelMaterialType)
                    : (isSteel ? "STL HOT ROLL" : "ALM HOT ROLL");
                string formedGauge = config.FormedChannelMaterialGauge.HasValue
                    ? MaterialsConfig.MapGauge(config.FormedChannelMaterialGauge.Value.ToString(CultureInfo.InvariantCulture), formedMatOnly)
                    : "10";
                string basePaint = string.Empty;

                if (baseIpts.Count > 0)
                {
                    // Base paint is display-only, so the dominant scanned value is fine here.
                    var nonFloorParts = baseIpts
                        .Where(p => p.GetClassification() == "Formed Channel" ||
                                    p.GetClassification() == "Structural Channel" ||
                                    p.GetClassification() == "Structural Angle")
                        .ToList();

                    if (nonFloorParts.Count > 0)
                    {
                        basePaint = nonFloorParts
                            .GroupBy(p => p.MaterialStyle)
                            .OrderByDescending(g => g.Count())
                            .First().Key;
                    }
                }

                string floorGauge = config.DefaultFloorMaterialGauge?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                string floorMat = config.DefaultFloorMaterialType ?? string.Empty;
                string floorPaint = config.DefaultFloorPaintType ?? string.Empty;
                string floorInsul = string.Empty;
                bool isThermalBreak = string.Equals(config.HousingStyle, "ThermalBreak", StringComparison.OrdinalIgnoreCase);

                if (config.SurfaceSegmentList.Count > 0)
                {
                    var firstSeg = config.SurfaceSegmentList.First();
                    if (string.IsNullOrEmpty(floorMat))
                        floorMat = firstSeg.FloorMaterialType ?? string.Empty;
                    if (string.IsNullOrEmpty(floorGauge))
                        floorGauge = firstSeg.FloorMaterialGauge?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                    if (string.IsNullOrEmpty(floorPaint))
                        floorPaint = firstSeg.FloorPaintType ?? string.Empty;

                    isThermalBreak = string.Equals(
                        firstSeg.HousingStyle ?? config.HousingStyle,
                        "ThermalBreak",
                        StringComparison.OrdinalIgnoreCase);
                }

                floorGauge = MaterialsConfig.MapGauge(floorGauge, floorMat);
                floorMat = MaterialsConfig.MapMaterial(floorMat);

                var insul = config.InsulationList
                    .Where(i => i.UnitSide == "Bottom" && !string.IsNullOrEmpty(i.InsulationType))
                    .FirstOrDefault();
                if (insul is not null)
                {
                    string thick = insul.NominalThickness.HasValue
                        ? $"{insul.NominalThickness:0.##}\""
                        : string.Empty;
                    floorInsul = string.IsNullOrEmpty(thick)
                        ? insul.InsulationType ?? string.Empty
                        : $"{thick} {insul.InsulationType}".Trim();
                }

                // Sub-floor: unitBase.unitBaseGeometryList[].subFloorMaterialType / subFloorMaterialGauge.
                string subFloorGauge = string.Empty;
                string subFloorMat = string.Empty;
                var subFloorGeom = config.UnitBase?.UnitBaseGeometryList
                    .FirstOrDefault(g => g.HasSubFloor != false && !string.IsNullOrWhiteSpace(g.SubFloorMaterialType));
                if (subFloorGeom != null)
                {
                    subFloorMat = MaterialsConfig.MapMaterial(subFloorGeom.SubFloorMaterialType!);
                    subFloorGauge = subFloorGeom.SubFloorMaterialGauge.HasValue
                        ? MaterialsConfig.MapGauge(subFloorGeom.SubFloorMaterialGauge.Value.ToString(CultureInfo.InvariantCulture), subFloorMat)
                        : string.Empty;
                }

                // Perimeter angle: bases with an upturned lip (UTL) use the UTL angle for their floor;
                // everything else uses the engineering standard (UnitBase_PerimeterAngle_*, normally 16 GA STL GALV).
                string perimeterGauge;
                string perimeterMat;
                if (config.HasUpturnedLip())
                {
                    (perimeterGauge, perimeterMat) = UtlPerimeterAngle(floorMat);
                }
                else
                {
                    perimeterGauge = _eng?.Constant("UnitBase_PerimeterAngle_MaterialGauge") is { Length: > 0 } pg ? pg : "16";
                    perimeterMat = _eng?.Constant("UnitBase_PerimeterAngle_MaterialType") is { Length: > 0 } pm ? pm : "STL GALV";
                }

                var baseObj = config.UnitBaseList
                    .Where(b => string.Equals(b.SkidId, config.SkidId, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
                if (baseObj == null && config.UnitBaseList.Count > 0)
                {
                    baseObj = config.UnitBaseList.First();
                }

                string baseHeightStr = string.Empty;
                if (baseObj?.Geometry?.YLength != null)
                {
                    baseHeightStr = $"{baseObj.Geometry.YLength.Value:0.##}\"";
                }

                var row = new BaseSurfaceRow
                {
                    PartNumber = partNum,
                    Segments = segsStr,
                    BaseHeight = baseHeightStr,
                    BaseMaterial = baseMat,
                    FormedChannelGauge = formedGauge,
                    FormedChannelMaterial = formedMatOnly,
                    BasePaint = basePaint,
                    FloorGauge = floorGauge,
                    FloorMaterial = floorMat,
                    FloorPaint = floorPaint,
                    FloorInsulation = floorInsul,
                    FloorThermalBreak = isThermalBreak ? "Yes" : "No",
                    SubFloorGauge = subFloorGauge,
                    SubFloorMaterial = subFloorMat,
                    PerimeterAngleGauge = perimeterGauge,
                    PerimeterAngleMaterial = perimeterMat,
                    // Drain support angles are 10 GA in the base alloy; the engineering
                    // UnitBase_FloorDrainSupportMaterial constant (16 GA STL GALV) matched no scanned part.
                    DrainSupportAngleGauge = "10",
                    DrainSupportAngleMaterial = isSteel ? "STL HOT ROLL" : "ALM SHT",
                    FloorAttachmentAngleGauge = _eng?.Constant("UnitBase_AluminumFloorAttachmentAngle_MaterialGauge") is { Length: > 0 } fg ? fg : "14",
                    FloorAttachmentAngleMaterial = _eng?.Constant("UnitBase_AluminumFloorAttachmentAngle_MaterialType") is { Length: > 0 } fm ? fm : "ALM SHT",
                    SourceSurfaceIam = config.SourceIamPath
                };
                data.BaseRows.Add(row);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static double GetConfigXCoordinate(ConfigData config)
        {
            var geom = config.Roof?.GeometryList?.FirstOrDefault()
                       ?? config.Base?.GeometryList?.FirstOrDefault()
                       ?? config.Wall?.GeometryList?.FirstOrDefault();
            return geom?.X ?? 0.0;
        }

        private static string GetWallThickness(ConfigData config, SurfaceSegment seg)
        {
            if (config.NominalSurfaceThickness.HasValue && config.NominalSurfaceThickness > 0)
                return $"{config.NominalSurfaceThickness.Value:0.##}\"";

            var vals = new double?[] {
                seg.WallThickness_Top, seg.WallThickness_Left, seg.WallThickness_Right,
                seg.WallThickness_Front, seg.WallThickness_Rear
            }.Where(v => v.HasValue && v.Value > 0).ToList();

            if (vals.Any())
                return $"{vals.Max():0.##}\"";

            return string.Empty;
        }

        private void BuildOtherConstruction(UnitConstructionData data)
        {
            var other = data.OtherConstruction;

            foreach (var config in _configs)
            {
                var lipEntry = config.UnitBaseList
                    .Where(b => b.UpturnedLipHeight.HasValue && b.UpturnedLipHeight > 0)
                    .FirstOrDefault();

                if (lipEntry is not null)
                {
                    other.UpturnedLip       = true;
                    other.UpturnedLipHeight = $"{lipEntry.UpturnedLipHeight:0.##}\"";
                    break;
                }
            }

            if (_eng is not null && _eng.HasCurbRest)
            {
                other.CurbRest       = true;
                other.CurbRestHeight = _eng.CurbSupportAngleHeight.HasValue
                    ? $"{_eng.CurbSupportAngleHeight:0.##}\""
                    : string.Empty;
            }
        }

        private static string FormatGaugeAndMaterial(string gauge, string material)
        {
            gauge    = (gauge ?? string.Empty).Trim();
            material = (material ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(gauge) && string.IsNullOrEmpty(material)) return string.Empty;
            if (string.IsNullOrEmpty(gauge))    return material;
            if (string.IsNullOrEmpty(material)) return gauge;
            return $"{gauge} GA {material}";
        }

        private static string DominantSkinMaterialForConfig(ConfigData config)
        {
            var list = new List<string>();
            foreach (var seg in config.SurfaceSegmentList)
            {
                string mat = DominantSkinMaterial(seg);
                if (!string.IsNullOrEmpty(mat)) list.Add(mat);
            }
            return list.GroupBy(v => v).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? string.Empty;
        }

        /// <summary>
        /// UTL perimeter angles follow the floor: 10 GA ALM SHT on aluminum floors, 10 GA in the floor's own stainless
        /// code (e.g. SST304D; the "D" is kept and normalized downstream) on stainless floors, 12 GA STL GALV on galvanized floors
        /// (seen on job 20129 and the UTL Notch test case). Unknown floors give no material, so the part is Not checked.
        /// </summary>
        private static (string Gauge, string Material) UtlPerimeterAngle(string floorMat)
        {
            string floor = (floorMat ?? string.Empty).Trim().ToUpperInvariant();
            if (floor.StartsWith("ALM")) return ("10", "ALM SHT");
            if (floor.StartsWith("SST")) return ("10", floorMat!.Trim());
            if (floor.StartsWith("STL")) return ("12", "STL GALV");
            return (string.Empty, string.Empty);
        }

        private static (string Gauge, string Material) SkinOf(SurfaceSegment s, string side) => side switch
        {
            "Top"   => (FormatGauge(s.SkinMaterialGauge_Top),   s.SkinMaterialType_Top   ?? string.Empty),
            "Left"  => (FormatGauge(s.SkinMaterialGauge_Left),  s.SkinMaterialType_Left  ?? string.Empty),
            "Right" => (FormatGauge(s.SkinMaterialGauge_Right), s.SkinMaterialType_Right ?? string.Empty),
            "Front" => (FormatGauge(s.SkinMaterialGauge_Front), s.SkinMaterialType_Front ?? string.Empty),
            "Rear"  => (FormatGauge(s.SkinMaterialGauge_Rear),  s.SkinMaterialType_Rear  ?? string.Empty),
            _       => (string.Empty, string.Empty),
        };

        private static (string Gauge, string Material) LinerOf(SurfaceSegment s, string side) => side switch
        {
            "Top"   => (FormatGauge(s.LinerMaterialGauge_Top),   s.LinerMaterialType_Top   ?? string.Empty),
            "Left"  => (FormatGauge(s.LinerMaterialGauge_Left),  s.LinerMaterialType_Left  ?? string.Empty),
            "Right" => (FormatGauge(s.LinerMaterialGauge_Right), s.LinerMaterialType_Right ?? string.Empty),
            "Front" => (FormatGauge(s.LinerMaterialGauge_Front), s.LinerMaterialType_Front ?? string.Empty),
            "Rear"  => (FormatGauge(s.LinerMaterialGauge_Rear),  s.LinerMaterialType_Rear  ?? string.Empty),
            _       => (string.Empty, string.Empty),
        };

        private static string FormatGauge(double? gauge) =>
            gauge.HasValue ? gauge.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;

        /// <summary>Most common non-empty value of <paramref name="select"/> across the config's segments for one side, or null.</summary>
        private static string? SideValue(ConfigData config, string? side, Func<SurfaceSegment, string> select)
        {
            if (string.IsNullOrEmpty(side)) return null;
            return config.SurfaceSegmentList
                .Select(select)
                .Where(v => !string.IsNullOrEmpty(v))
                .GroupBy(v => v)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;
        }

        /// <summary>The wall exterior material: most common skin material on the four vertical sides, or null.</summary>
        private static string? SideSkinMaterial(ConfigData config)
        {
            return new[] { "Left", "Right", "Front", "Rear" }
                .SelectMany(side => config.SurfaceSegmentList.Select(s => SkinOf(s, side).Material))
                .Where(v => !string.IsNullOrEmpty(v))
                .GroupBy(v => v)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;
        }

        /// <summary>Most common own-side exterior skin material across the unit's non-interior wall surfaces, or null.</summary>
        private string? UnitWallExteriorMaterial()
        {
            return _configs
                .Where(c => (c.SurfaceType ?? string.Empty).IndexOf("WALL", StringComparison.OrdinalIgnoreCase) >= 0 && !c.IsSharedWall())
                .Select(c => SideValue(c, c.GetUnitSide(), s => SkinOf(s, c.GetUnitSide()!).Material))
                .Where(v => !string.IsNullOrEmpty(v))
                .GroupBy(v => v)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;
        }

        /// <summary>Standard channel material for a panel thickness: STL GALV2/3/4, or "STL GALV?" when unknown.</summary>
        private static string ChannelMaterialFor(string thickness)
        {
            string clean = (thickness ?? string.Empty).Replace("\"", "").Trim();
            if (double.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            {
                int rounded = (int)Math.Round(val);
                if (rounded >= 2 && rounded <= 4) return "STL GALV" + rounded;
            }
            else
            {
                if (clean.Contains("2")) return "STL GALV2";
                if (clean.Contains("3")) return "STL GALV3";
                if (clean.Contains("4")) return "STL GALV4";
            }
            return "STL GALV?";
        }

        private static string DominantSkinGaugeForConfig(ConfigData config)
        {
            var list = new List<string>();
            foreach (var seg in config.SurfaceSegmentList)
            {
                string gauge = DominantSkinGauge(seg);
                if (!string.IsNullOrEmpty(gauge)) list.Add(gauge);
            }
            return list.GroupBy(v => v).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? string.Empty;
        }

        private static string DominantLinerMaterialForConfig(ConfigData config)
        {
            var list = new List<string>();
            foreach (var seg in config.SurfaceSegmentList)
            {
                string mat = DominantLinerMaterial(seg);
                if (!string.IsNullOrEmpty(mat)) list.Add(mat);
            }
            return list.GroupBy(v => v).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? string.Empty;
        }

        private static string DominantLinerGaugeForConfig(ConfigData config)
        {
            var list = new List<string>();
            foreach (var seg in config.SurfaceSegmentList)
            {
                string gauge = DominantLinerGauge(seg);
                if (!string.IsNullOrEmpty(gauge)) list.Add(gauge);
            }
            return list.GroupBy(v => v).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? string.Empty;
        }

        private static string DominantSkinMaterial(SurfaceSegment seg)
        {
            var list = new List<string>();
            if (seg.WallThickness_Top.HasValue && seg.WallThickness_Top > 0 && !string.IsNullOrEmpty(seg.SkinMaterialType_Top))
                list.Add(seg.SkinMaterialType_Top!);
            if (seg.WallThickness_Left.HasValue && seg.WallThickness_Left > 0 && !string.IsNullOrEmpty(seg.SkinMaterialType_Left))
                list.Add(seg.SkinMaterialType_Left!);
            if (seg.WallThickness_Right.HasValue && seg.WallThickness_Right > 0 && !string.IsNullOrEmpty(seg.SkinMaterialType_Right))
                list.Add(seg.SkinMaterialType_Right!);
            if (seg.WallThickness_Front.HasValue && seg.WallThickness_Front > 0 && !string.IsNullOrEmpty(seg.SkinMaterialType_Front))
                list.Add(seg.SkinMaterialType_Front!);
            if (seg.WallThickness_Rear.HasValue && seg.WallThickness_Rear > 0 && !string.IsNullOrEmpty(seg.SkinMaterialType_Rear))
                list.Add(seg.SkinMaterialType_Rear!);

            return list.GroupBy(v => v).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? string.Empty;
        }

        private static string DominantSkinGauge(SurfaceSegment seg)
        {
            var list = new List<double>();
            if (seg.WallThickness_Top.HasValue && seg.WallThickness_Top > 0 && seg.SkinMaterialGauge_Top.HasValue)
                list.Add(seg.SkinMaterialGauge_Top.Value);
            if (seg.WallThickness_Left.HasValue && seg.WallThickness_Left > 0 && seg.SkinMaterialGauge_Left.HasValue)
                list.Add(seg.SkinMaterialGauge_Left.Value);
            if (seg.WallThickness_Right.HasValue && seg.WallThickness_Right > 0 && seg.SkinMaterialGauge_Right.HasValue)
                list.Add(seg.SkinMaterialGauge_Right.Value);
            if (seg.WallThickness_Front.HasValue && seg.WallThickness_Front > 0 && seg.SkinMaterialGauge_Front.HasValue)
                list.Add(seg.SkinMaterialGauge_Front.Value);
            if (seg.WallThickness_Rear.HasValue && seg.WallThickness_Rear > 0 && seg.SkinMaterialGauge_Rear.HasValue)
                list.Add(seg.SkinMaterialGauge_Rear.Value);

            if (!list.Any()) return string.Empty;
            return list.GroupBy(v => v).OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string DominantLinerMaterial(SurfaceSegment seg)
        {
            var list = new List<string>();
            if (seg.WallThickness_Top.HasValue && seg.WallThickness_Top > 0 && !string.IsNullOrEmpty(seg.LinerMaterialType_Top))
                list.Add(seg.LinerMaterialType_Top!);
            if (seg.WallThickness_Left.HasValue && seg.WallThickness_Left > 0 && !string.IsNullOrEmpty(seg.LinerMaterialType_Left))
                list.Add(seg.LinerMaterialType_Left!);
            if (seg.WallThickness_Right.HasValue && seg.WallThickness_Right > 0 && !string.IsNullOrEmpty(seg.LinerMaterialType_Right))
                list.Add(seg.LinerMaterialType_Right!);
            if (seg.WallThickness_Front.HasValue && seg.WallThickness_Front > 0 && !string.IsNullOrEmpty(seg.LinerMaterialType_Front))
                list.Add(seg.LinerMaterialType_Front!);
            if (seg.WallThickness_Rear.HasValue && seg.WallThickness_Rear > 0 && !string.IsNullOrEmpty(seg.LinerMaterialType_Rear))
                list.Add(seg.LinerMaterialType_Rear!);

            return list.GroupBy(v => v).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? string.Empty;
        }

        private static string DominantLinerGauge(SurfaceSegment seg)
        {
            var list = new List<double>();
            if (seg.WallThickness_Top.HasValue && seg.WallThickness_Top > 0 && seg.LinerMaterialGauge_Top.HasValue)
                list.Add(seg.LinerMaterialGauge_Top.Value);
            if (seg.WallThickness_Left.HasValue && seg.WallThickness_Left > 0 && seg.LinerMaterialGauge_Left.HasValue)
                list.Add(seg.LinerMaterialGauge_Left.Value);
            if (seg.WallThickness_Right.HasValue && seg.WallThickness_Right > 0 && seg.LinerMaterialGauge_Right.HasValue)
                list.Add(seg.LinerMaterialGauge_Right.Value);
            if (seg.WallThickness_Front.HasValue && seg.WallThickness_Front > 0 && seg.LinerMaterialGauge_Front.HasValue)
                list.Add(seg.LinerMaterialGauge_Front.Value);
            if (seg.WallThickness_Rear.HasValue && seg.WallThickness_Rear > 0 && seg.LinerMaterialGauge_Rear.HasValue)
                list.Add(seg.LinerMaterialGauge_Rear.Value);

            if (!list.Any()) return string.Empty;
            return list.GroupBy(v => v).OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }
}
