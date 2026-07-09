using System.Collections.Generic;

namespace UnitConstructionVerifier.Models
{
    // ── Top-level container ───────────────────────────────────────────────────

    /// <summary>All extracted and user-editable construction data for one IAM.</summary>
    public sealed class UnitConstructionData
    {
        public string                  IamPath            { get; set; } = string.Empty;
        public string                  SurfaceType        { get; set; } = string.Empty; // YC SURF WALL / ROOF / BASE
        public List<RoofSurfaceRow>    RoofRows           { get; set; } = new();
        public List<WallSurfaceRow>    WallRows           { get; set; } = new();
        public List<BaseSurfaceRow>    BaseRows           { get; set; } = new();
        public OtherConstruction       OtherConstruction  { get; set; } = new();
    }

    // ── Casing Details (Roof / Wall) ──────────────────────────────────────────
    
    public static class ConstructionDataHelper
    {
        public static string FormatGaugeAndMaterial(string gauge, string material)
        {
            gauge    = (gauge    ?? string.Empty).Trim();
            material = (material ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(gauge) && string.IsNullOrEmpty(material)) return string.Empty;
            if (string.IsNullOrEmpty(gauge))    return material;
            if (string.IsNullOrEmpty(material)) return gauge;
            return $"{gauge} GA {material}";
        }
    }

    public sealed class RoofSurfaceRow
    {
        public string PartNumber                { get; set; } = string.Empty; // e.g. 391Z010111-0151
        public string Segments                  { get; set; } = string.Empty; // e.g. "FS, DP"
        public string Thickness                 { get; set; } = string.Empty;
        public string ExteriorPaint             { get; set; } = string.Empty;
        public string ExteriorSkinGauge         { get; set; } = string.Empty;
        public string ExteriorSkinMaterial      { get; set; } = string.Empty;
        public string InteriorLinerGauge        { get; set; } = string.Empty;
        public string InteriorLinerMaterial     { get; set; } = string.Empty;
        public string ChannelSkinGauge          { get; set; } = string.Empty;
        public string ChannelSkinMaterial       { get; set; } = string.Empty;
        public string TrimSkinGauge             { get; set; } = string.Empty;
        public string TrimSkinMaterial          { get; set; } = string.Empty;
        public string InsulationThicknessAndMaterial { get; set; } = string.Empty;
        public string ThermalBreak              { get; set; } = string.Empty;

        public string SourceSurfaceIam          { get; set; } = string.Empty;

        // Computed for backward compatibility / simplicity in verification
        public string ExteriorGaugeAndMaterial =>
            ConstructionDataHelper.FormatGaugeAndMaterial(ExteriorSkinGauge, ExteriorSkinMaterial);
        public string InteriorGaugeAndMaterial =>
            ConstructionDataHelper.FormatGaugeAndMaterial(InteriorLinerGauge, InteriorLinerMaterial);
        public string ChannelGaugeAndMaterial =>
            ConstructionDataHelper.FormatGaugeAndMaterial(ChannelSkinGauge, ChannelSkinMaterial);
        public string TrimGaugeAndMaterial =>
            ConstructionDataHelper.FormatGaugeAndMaterial(TrimSkinGauge, TrimSkinMaterial);
    }

    public sealed class WallSurfaceRow
    {
        public string PartNumber                { get; set; } = string.Empty; // e.g. 391Z010111-0151
        public string Segments                  { get; set; } = string.Empty; // e.g. "CC, XA-1"
        public string Thickness                 { get; set; } = string.Empty;
        public string ExteriorPaint             { get; set; } = string.Empty;
        public string ExteriorSkinGauge         { get; set; } = string.Empty;
        public string ExteriorSkinMaterial      { get; set; } = string.Empty;
        public string InteriorLinerGauge        { get; set; } = string.Empty;
        public string InteriorLinerMaterial     { get; set; } = string.Empty;
        public string ChannelSkinGauge          { get; set; } = string.Empty;
        public string ChannelSkinMaterial       { get; set; } = string.Empty;
        public string InsulationThicknessAndMaterial { get; set; } = string.Empty;
        public string ThermalBreak              { get; set; } = string.Empty;

        public string SourceSurfaceIam          { get; set; } = string.Empty;

        // Computed for backward compatibility / simplicity in verification
        public string ExteriorGaugeAndMaterial => ConstructionDataHelper.FormatGaugeAndMaterial(ExteriorSkinGauge, ExteriorSkinMaterial);
        public string InteriorGaugeAndMaterial => ConstructionDataHelper.FormatGaugeAndMaterial(InteriorLinerGauge, InteriorLinerMaterial);
        public string ChannelGaugeAndMaterial => ConstructionDataHelper.FormatGaugeAndMaterial(ChannelSkinGauge, ChannelSkinMaterial);
    }

    // ── Base Details ──────────────────────────────────────────────────────────

    public sealed class BaseSurfaceRow
    {
        public string PartNumber                { get; set; } = string.Empty;
        public string Segments                  { get; set; } = string.Empty;

        public string BaseHeight                { get; set; } = string.Empty;

        // Base structural frame details
        public string BaseMaterial              { get; set; } = string.Empty;
        public string FormedChannelGauge        { get; set; } = string.Empty;
        public string FormedChannelMaterialOnly { get; set; } = string.Empty;
        public string BasePaint                 { get; set; } = string.Empty;

        // Floor casing details
        public string FloorGauge                { get; set; } = string.Empty;
        public string FloorMaterial             { get; set; } = string.Empty;
        public string FloorPaint                { get; set; } = string.Empty;
        public string FloorInsulation           { get; set; } = string.Empty;
        public string FloorThermalBreak         { get; set; } = string.Empty;

        // Sub-Floor details
        public string SubFloorGauge             { get; set; } = string.Empty;
        public string SubFloorMaterial          { get; set; } = string.Empty;

        // Perimeter Angle details
        public string PerimeterAngleGauge       { get; set; } = string.Empty;
        public string PerimeterAngleMaterial    { get; set; } = string.Empty;

        public string SourceSurfaceIam          { get; set; } = string.Empty;

        // Computed properties for compatibility and verification
        public string FormedChannelMaterial => ConstructionDataHelper.FormatGaugeAndMaterial(FormedChannelGauge, FormedChannelMaterialOnly);
        public string FloorGaugeAndMaterial => ConstructionDataHelper.FormatGaugeAndMaterial(FloorGauge, FloorMaterial);
        public string SubFloorGaugeAndMaterial => ConstructionDataHelper.FormatGaugeAndMaterial(SubFloorGauge, SubFloorMaterial);
        public string PerimeterAngleGaugeAndMaterial => ConstructionDataHelper.FormatGaugeAndMaterial(PerimeterAngleGauge, PerimeterAngleMaterial);
    }

    // ── Other Construction ────────────────────────────────────────────────────

    public sealed class OtherConstruction
    {
        public bool   UpturnedLip       { get; set; }
        public string UpturnedLipHeight { get; set; } = string.Empty;   // e.g. "2.0\""

        public bool   CurbRest          { get; set; }
        public string CurbRestHeight    { get; set; } = string.Empty;   // from UnitBase_CurbSupportAngleHeight
    }
}
