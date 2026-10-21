using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace UnitConstructionVerifier.Models
{
    // ─────────────────────────────────────────────────────────────────────────
    // Deserialization model for DOCUMENT_CONFIG_JSON
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class ConfigJson
    {
        [JsonProperty("configuration")]
        public ConfigData Configuration { get; set; } = new();

        [JsonProperty("documentInfo")]
        public DocumentInfo? DocumentInfo { get; set; }
    }

    public sealed class DocumentInfo
    {
        [JsonProperty("assemblyNumber")]
        public string? AssemblyNumber { get; set; }
    }

    public sealed class ConfigData
    {
        public string SourceIamPath { get; set; } = string.Empty;

        [JsonProperty("surfaceType")]
        public string SurfaceType { get; set; } = string.Empty;          // YC SURF WALL / BASE / ROOF

        [JsonProperty("surfaceID")]
        public string? SurfaceId { get; set; }

        public bool IsSharedWall()
        {
            if (string.IsNullOrEmpty(SurfaceId)) return false;
            foreach (var surface in UnitSurfaceList)
            {
                if (string.Equals(surface.Id, SurfaceId, StringComparison.OrdinalIgnoreCase))
                {
                    return surface.IsInteriorWall;
                }
            }
            return false;
        }

        /// <summary>The unit side this surface sits on (Top, Left, Right, Front, Rear, Bottom), or null.</summary>
        public string? GetUnitSide()
        {
            if (string.IsNullOrEmpty(SurfaceId)) return null;
            foreach (var surface in UnitSurfaceList)
            {
                if (string.Equals(surface.Id, SurfaceId, StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(surface.UnitSide) ? null : surface.UnitSide;
            }
            return null;
        }

        /// <summary>
        /// True when this base has an upturned lip (UTL): its own unitBase geometry, or its skid's entry in
        /// unitBaseList, carries an upturnedLipHeight. Matched the perimeter angle type on 89 of 90 ISG bases.
        /// </summary>
        public bool HasUpturnedLip()
        {
            if (UnitBase?.UnitBaseGeometryList.Any(g => g.UpturnedLipHeight > 0) == true) return true;
            return UnitBaseList.Any(b => b.UpturnedLipHeight > 0 &&
                                         !string.IsNullOrEmpty(SkidId) &&
                                         string.Equals(b.SkidId, SkidId, StringComparison.OrdinalIgnoreCase));
        }

        [JsonProperty("unitType")]
        public string? UnitType { get; set; }                            // Indoor / Outdoor

        [JsonProperty("unitConstructionType")]
        public string? UnitConstructionType { get; set; }                // Standard / IBC

        [JsonProperty("unitBase")]
        public UnitBaseConfig? UnitBase { get; set; }

        [JsonProperty("skidID")]
        public string? SkidId { get; set; }

        [JsonProperty("skidNumber")]
        public int? SkidNumber { get; set; }

        [JsonProperty("skidSegmentSequence")]
        public string? SkidSegmentSequence { get; set; }

        [JsonProperty("housingStyle")]
        public string HousingStyle { get; set; } = string.Empty;         // ThermalBreak, Standard, etc.

        [JsonProperty("roof")]
        public SurfaceGeometryContainer? Roof { get; set; }

        [JsonProperty("base")]
        public SurfaceGeometryContainer? Base { get; set; }

        [JsonProperty("wall")]
        public SurfaceGeometryContainer? Wall { get; set; }

        [JsonProperty("nominalSurfaceThickness")]
        public double? NominalSurfaceThickness { get; set; }

        [JsonProperty("structuralMaterialType")]
        public string? StructuralMaterialType { get; set; }

        [JsonProperty("formedChannelMaterialType")]
        public string? FormedChannelMaterialType { get; set; }

        [JsonProperty("formedChannelMaterialGauge")]
        public double? FormedChannelMaterialGauge { get; set; }

        [JsonProperty("surfaceSegmentList")]
        public List<SurfaceSegment> SurfaceSegmentList { get; set; } = new();

        [JsonProperty("unitSurfaceList")]
        public List<UnitSurface> UnitSurfaceList { get; set; } = new();

        [JsonProperty("unitBaseList")]
        public List<UnitBase> UnitBaseList { get; set; } = new();

        [JsonProperty("insulationList")]
        public List<InsulationEntry> InsulationList { get; set; } = new();

        [JsonProperty("defaultConstructionOptions_FloorMaterialType")]
        public string? DefaultFloorMaterialType { get; set; }

        [JsonProperty("defaultConstructionOptions_FloorMaterialGauge")]
        public double? DefaultFloorMaterialGauge { get; set; }

        [JsonProperty("defaultConstructionOptions_FloorPaintType")]
        public string? DefaultFloorPaintType { get; set; }
    }

    public sealed class SurfaceSegment
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("skidID")]
        public string? SkidId { get; set; }

        [JsonProperty("segmentType")]
        public string SegmentType { get; set; } = string.Empty;          // CC, MB, XA-1, etc.

        [JsonProperty("housingStyle")]
        public string? HousingStyle { get; set; }

        [JsonProperty("bulkheadList")]
        public List<Bulkhead> BulkheadList { get; set; } = new();

        [JsonProperty("insulationType")]
        public string? InsulationType { get; set; }

        // ── Skin (exterior) ──────────────────────────────────────────────────
        [JsonProperty("skinMaterialType_Top")]    public string? SkinMaterialType_Top    { get; set; }
        [JsonProperty("skinMaterialGauge_Top")]   public double? SkinMaterialGauge_Top   { get; set; }
        [JsonProperty("skinMaterialType_Left")]   public string? SkinMaterialType_Left   { get; set; }
        [JsonProperty("skinMaterialGauge_Left")]  public double? SkinMaterialGauge_Left  { get; set; }
        [JsonProperty("skinMaterialType_Right")]  public string? SkinMaterialType_Right  { get; set; }
        [JsonProperty("skinMaterialGauge_Right")] public double? SkinMaterialGauge_Right { get; set; }
        [JsonProperty("skinMaterialType_Front")]  public string? SkinMaterialType_Front  { get; set; }
        [JsonProperty("skinMaterialGauge_Front")] public double? SkinMaterialGauge_Front { get; set; }
        [JsonProperty("skinMaterialType_Rear")]   public string? SkinMaterialType_Rear   { get; set; }
        [JsonProperty("skinMaterialGauge_Rear")]  public double? SkinMaterialGauge_Rear  { get; set; }

        // ── Liner (interior) ─────────────────────────────────────────────────
        [JsonProperty("linerMaterialType_Top")]    public string? LinerMaterialType_Top    { get; set; }
        [JsonProperty("linerMaterialGauge_Top")]   public double? LinerMaterialGauge_Top   { get; set; }
        [JsonProperty("linerMaterialType_Left")]   public string? LinerMaterialType_Left   { get; set; }
        [JsonProperty("linerMaterialGauge_Left")]  public double? LinerMaterialGauge_Left  { get; set; }
        [JsonProperty("linerMaterialType_Right")]  public string? LinerMaterialType_Right  { get; set; }
        [JsonProperty("linerMaterialGauge_Right")] public double? LinerMaterialGauge_Right { get; set; }
        [JsonProperty("linerMaterialType_Front")]  public string? LinerMaterialType_Front  { get; set; }
        [JsonProperty("linerMaterialGauge_Front")] public double? LinerMaterialGauge_Front { get; set; }
        [JsonProperty("linerMaterialType_Rear")]   public string? LinerMaterialType_Rear   { get; set; }
        [JsonProperty("linerMaterialGauge_Rear")]  public double? LinerMaterialGauge_Rear  { get; set; }

        // ── Floor (for BASE surfaces) ─────────────────────────────────────────
        [JsonProperty("floorMaterialType")]  public string? FloorMaterialType  { get; set; }
        [JsonProperty("floorMaterialGauge")] public double? FloorMaterialGauge { get; set; }
        [JsonProperty("floorPaintType")]     public string? FloorPaintType     { get; set; }

        // ── Wall thickness indicators (presence = has that wall) ─────────────
        [JsonProperty("wallThickness_Top")]   public double? WallThickness_Top   { get; set; }
        [JsonProperty("wallThickness_Left")]  public double? WallThickness_Left  { get; set; }
        [JsonProperty("wallThickness_Right")] public double? WallThickness_Right { get; set; }
        [JsonProperty("wallThickness_Front")] public double? WallThickness_Front { get; set; }
        [JsonProperty("wallThickness_Rear")]  public double? WallThickness_Rear  { get; set; }

        [JsonProperty("segmentTypeSuffix")]
        public object? SegmentTypeSuffix { get; set; }

        [JsonProperty("segmentRelativePosition_Front")]
        public double? SegmentRelativePositionFront { get; set; }

        [JsonProperty("segmentRelativePosition_Rear")]
        public double? SegmentRelativePositionRear { get; set; }

        public string FullSegmentName
        {
            get
            {
                if (SegmentTypeSuffix != null)
                {
                    string suffix = SegmentTypeSuffix.ToString().Trim();
                    if (!string.IsNullOrEmpty(suffix) && suffix != "0")
                    {
                        return $"{SegmentType}-{suffix}";
                    }
                }
                return SegmentType;
            }
        }
    }

    public sealed class UnitSurface
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("unitSide")]
        public string UnitSide { get; set; } = string.Empty;             // Left, Right, Top, Front, Rear, Bottom

        [JsonProperty("nominalSurfaceThickness")]
        public double? NominalSurfaceThickness { get; set; }

        [JsonProperty("isInteriorWall")]
        public bool IsInteriorWall { get; set; }
    }

    public sealed class UnitBase
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("skidID")]
        public string? SkidId { get; set; }

        [JsonProperty("upturnedLipHeight")]
        public double? UpturnedLipHeight { get; set; }

        [JsonProperty("geometry")]
        public GeometryItem? Geometry { get; set; }
    }

    /// <summary>The <c>unitBase</c> object of a UnitBase surface configuration.</summary>
    public sealed class UnitBaseConfig
    {
        [JsonProperty("unitBaseType")]
        public string? UnitBaseType { get; set; }                        // A36

        [JsonProperty("unitBaseMaterialType")]
        public string? UnitBaseMaterialType { get; set; }                // StructuralSteel / StructuralAluminum

        [JsonProperty("unitBaseGeometryList")]
        public List<UnitBaseGeometry> UnitBaseGeometryList { get; set; } = new();
    }

    public sealed class UnitBaseGeometry
    {
        [JsonProperty("hasSubFloor")]
        public bool? HasSubFloor { get; set; }

        [JsonProperty("subFloorMaterialType")]
        public string? SubFloorMaterialType { get; set; }

        [JsonProperty("subFloorMaterialGauge")]
        public double? SubFloorMaterialGauge { get; set; }

        /// <summary>Upturned lip (UTL) height; present (e.g. 2.0) only when this base has an upturned lip.</summary>
        [JsonProperty("upturnedLipHeight")]
        public double? UpturnedLipHeight { get; set; }
    }

    public sealed class Bulkhead
    {
        [JsonProperty("partInfo")]
        public BulkheadPartInfo? PartInfo { get; set; }
    }

    public sealed class BulkheadPartInfo
    {
        [JsonProperty("mapicsItemData")]
        public MapicsItemData? MapicsItemData { get; set; }
    }

    public sealed class MapicsItemData
    {
        [JsonProperty("extendedDescription1")]
        public string? ExtendedDescription1 { get; set; }
    }

    public sealed class InsulationEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("unitSide")]
        public string UnitSide { get; set; } = string.Empty;

        [JsonProperty("insulationType")]
        public string? InsulationType { get; set; }

        [JsonProperty("nominalThickness")]
        public double? NominalThickness { get; set; }

        [JsonProperty("volume")]
        public double? Volume { get; set; }
    }

    public sealed class SurfaceGeometryContainer
    {
        [JsonProperty("geometryList")]
        public List<GeometryItem> GeometryList { get; set; } = new();
    }

    public sealed class GeometryItem
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }

        [JsonProperty("xLength")]
        public double XLength { get; set; }

        [JsonProperty("yLength")]
        public double? YLength { get; set; }

        [JsonProperty("zLength")]
        public double? ZLength { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Deserialization model for DOCUMENT_ENGINEERING_JSON
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class EngineeringJson
    {
        [JsonProperty("rules")]
        public EngineeringRules? Rules { get; set; }
    }

    public sealed class EngineeringRules
    {
        [JsonProperty("engineeringConstantList")]
        public List<EngineeringConstant> Constants { get; set; } = new();
    }

    public sealed class EngineeringConstant
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("value")]
        public object? Value { get; set; }
    }

    /// <summary>Parsed / resolved engineering data relevant to verification.</summary>
    public sealed class EngineeringData
    {
        public double? CurbSupportAngleHeight    { get; set; }
        public double? CurbSupportAngleFlange    { get; set; }
        public double? CurbSupportAngleThickness { get; set; }

        /// <summary>
        /// Every engineering constant by type name (e.g. <c>UnitBase_PerimeterAngle_MaterialType</c>),
        /// as invariant-culture strings. Supplies part material standards.
        /// </summary>
        public Dictionary<string, string> Constants { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool HasCurbRest => CurbSupportAngleHeight.HasValue && CurbSupportAngleHeight > 0;

        /// <summary>Returns the constant formatted for gauge/material fields ("16", "STL GALV"), or empty.</summary>
        public string Constant(string type)
        {
            if (!Constants.TryGetValue(type, out string value) || string.IsNullOrWhiteSpace(value)) return string.Empty;
            return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d)
                ? d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                : value.Trim();
        }
    }
}
