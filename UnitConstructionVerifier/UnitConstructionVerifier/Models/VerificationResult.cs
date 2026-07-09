using System.Collections.Generic;

namespace UnitConstructionVerifier.Models
{
    // ─────────────────────────────────────────────────────────────────────────
    // IPT iProperty scan results
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Properties extracted from a single IPT part file.</summary>
    public sealed class IptProperties
    {
        public string PartNumber        { get; set; } = string.Empty;  // e.g. 091Z009287-0001
        public string FilePath          { get; set; } = string.Empty;
        public string OwnerIamPath      { get; set; } = string.Empty;  // parent surface IAM

        // User Defined iProperties
        public string Thickness         { get; set; } = string.Empty;  // "0.056"
        public string YCMATL            { get; set; } = string.Empty;  // "STL GALV"
        public string ModelNumber       { get; set; } = string.Empty;  // "091-30117-073"
        public string MtlGauge          { get; set; } = string.Empty;  // INPUT_PARAMETER_Mtl_Gauge
        public string MaterialStyle     { get; set; } = string.Empty;  // INPUT_PARAMETER_MaterialStyle

        // Design Tracking Properties
        public string Description       { get; set; } = string.Empty;  // part description / title

        /// <summary>True when MODEL_NUMBER == "091-30117-080" or Description contains "SUBFLOOR".</summary>
        public bool IsSubFloor => ModelNumber.Trim() == "091-30117-080" || 
                                  (!string.IsNullOrEmpty(Description) && Description.IndexOf("SUBFLOOR", System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>All IPT parts found under an assembly, keyed by part number.</summary>
    public sealed class IptScanResult
    {
        public List<IptProperties> Parts { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Verification result
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class VerificationResult
    {
        public bool IsPass => Mismatches.Count == 0;
        public List<IptMismatch> Mismatches { get; set; } = new();
    }

    /// <summary>
    /// A single field mismatch for one IPT, grouped under its surface IAM.
    /// </summary>
    public sealed class IptMismatch
    {
        public string SurfaceIamPath  { get; set; } = string.Empty;
        public string SurfaceIamName  { get; set; } = string.Empty;  // basename without extension
        public string IptPartNumber   { get; set; } = string.Empty;
        public string IptFilePath     { get; set; } = string.Empty;
        public string FieldName       { get; set; } = string.Empty;
        public string ExpectedValue   { get; set; } = string.Empty;
        public string ActualValue     { get; set; } = string.Empty;
        public string Section         { get; set; } = string.Empty;  // Casing / Base / Floor
    }
}
