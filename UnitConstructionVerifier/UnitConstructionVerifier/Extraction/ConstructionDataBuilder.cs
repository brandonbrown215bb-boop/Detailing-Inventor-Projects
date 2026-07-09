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
                    skinMat = DominantSkinMaterialForConfig(config);
                    skinGauge = DominantSkinGaugeForConfig(config);
                    linerMat = DominantLinerMaterialForConfig(config);
                    linerGauge = DominantLinerGaugeForConfig(config);

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

                if (isRoof)
                {
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
                        ChannelSkinGauge = skinGauge,
                        ChannelSkinMaterial = skinMat,
                        TrimSkinGauge = skinGauge,
                        TrimSkinMaterial = skinMat,
                        InsulationThicknessAndMaterial = insulStr,
                        ThermalBreak = isThermalBreak ? "Yes" : "No",
                        SourceSurfaceIam = config.SourceIamPath
                    };
                    data.RoofRows.Add(row);
                }
                else
                {
                    string paint = config.SurfaceSegmentList.FirstOrDefault()?.FloorPaintType ?? string.Empty;

                    string wallChannelMat = "STL GALV?";
                    if (!string.IsNullOrEmpty(thickness))
                    {
                        string clean = thickness.Replace("\"", "").Trim();
                        if (double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                        {
                            int rounded = (int)Math.Round(val);
                            if (rounded == 2) wallChannelMat = "STL GALV2";
                            else if (rounded == 3) wallChannelMat = "STL GALV3";
                            else if (rounded == 4) wallChannelMat = "STL GALV4";
                        }
                        else
                        {
                            if (clean.Contains("2")) wallChannelMat = "STL GALV2";
                            else if (clean.Contains("3")) wallChannelMat = "STL GALV3";
                            else if (clean.Contains("4")) wallChannelMat = "STL GALV4";
                        }
                    }

                    var row = new WallSurfaceRow
                    {
                        PartNumber = partNum,
                        Segments = segsStr,
                        Thickness = thickness,
                        ExteriorPaint = paint,
                        ExteriorSkinGauge = skinGauge,
                        ExteriorSkinMaterial = skinMat,
                        InteriorLinerGauge = linerGauge,
                        InteriorLinerMaterial = linerMat,
                        ChannelSkinGauge = "16",
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

                bool isSteel = config.StructuralMaterialType == null || 
                               config.StructuralMaterialType.IndexOf("steel", StringComparison.OrdinalIgnoreCase) >= 0;
                string baseMat = isSteel ? "STL C CHNL" : "ALM C CHNL";
                string formedGauge = "10";
                string formedMatOnly = isSteel ? "STL HOT ROLL" : "ALM HOT ROLL";
                string basePaint = string.Empty;

                if (baseIpts.Count > 0)
                {
                    var structuralParts = baseIpts
                        .Where(p => p.Description.StartsWith("CHN:STRUCT", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (structuralParts.Count > 0)
                    {
                        var dominantStructural = structuralParts
                            .GroupBy(p => p.YCMATL)
                            .OrderByDescending(g => g.Count())
                            .First().Key;
                        if (!string.IsNullOrWhiteSpace(dominantStructural))
                        {
                            baseMat = dominantStructural;
                        }
                    }

                    var formedParts = baseIpts
                        .Where(p => p.Description.IndexOf("Channel, Formed", StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();

                    if (formedParts.Count > 0)
                    {
                        var dominantFormed = formedParts
                            .GroupBy(p => new { p.MtlGauge, p.YCMATL })
                            .OrderByDescending(g => g.Count())
                            .First().Key;
                        formedGauge = MaterialsConfig.MapGauge(dominantFormed.MtlGauge);
                        formedMatOnly = MaterialsConfig.MapMaterial(dominantFormed.YCMATL);
                    }

                    var nonFloorParts = baseIpts
                        .Where(p => p.Description.IndexOf("Channel, Formed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    p.Description.StartsWith("CHN:STRUCT", StringComparison.OrdinalIgnoreCase))
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

                floorGauge = MaterialsConfig.MapGauge(floorGauge);
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

                var subFloorParts = _ipt.Parts
                    .Where(p => string.Equals(p.OwnerIamPath, config.SourceIamPath, StringComparison.OrdinalIgnoreCase) &&
                                p.IsSubFloor)
                    .ToList();
                string subFloorGauge = string.Empty;
                string subFloorMat = string.Empty;
                if (subFloorParts.Count > 0)
                {
                    var first = subFloorParts[0];
                    subFloorGauge = MaterialsConfig.MapGauge(first.MtlGauge);
                    subFloorMat = MaterialsConfig.MapMaterial(first.YCMATL);
                }

                // Extract Perimeter Angle
                string perimeterGauge = string.Empty;
                string perimeterMat = string.Empty;
                var perimeterParts = baseIpts
                    .Where(p => p.Description.IndexOf("perimeter angle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                p.Description.IndexOf("angle, perimeter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                (p.Description.IndexOf("perimeter", StringComparison.OrdinalIgnoreCase) >= 0 && p.Description.IndexOf("angle", StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();
                if (perimeterParts.Count > 0)
                {
                    var dominantPerimeter = perimeterParts
                        .GroupBy(p => new { p.MtlGauge, p.YCMATL })
                        .OrderByDescending(g => g.Count())
                        .First().Key;
                    perimeterGauge = MaterialsConfig.MapGauge(dominantPerimeter.MtlGauge);
                    perimeterMat = MaterialsConfig.MapMaterial(dominantPerimeter.YCMATL);
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
                    FormedChannelMaterialOnly = formedMatOnly,
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
