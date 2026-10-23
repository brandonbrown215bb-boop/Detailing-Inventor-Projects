using System.Collections.Generic;
using System.Linq;

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
        public string PartSource        { get; set; } = string.Empty;  // MOM_DATA PART_SOURCE
        public bool IsHardEdited        { get; set; }                  // IS_HARD_EDITED User Defined property flag

        // Design Tracking Properties
        public string Description       { get; set; } = string.Empty;  // part description / title

        public string GetClassification()
        {
            return MaterialsConfig.GetPartClassification(ModelNumber, Description);
        }

        /// <summary>
        /// True when the part was modified after the unit was generated: an SQ part
        /// (description "SQ PART - ..." or MOM_DATA PART_SOURCE = SQPart) or a part edited
        /// through the verifier (IS_HARD_EDITED). Fresh units have none.
        /// </summary>
        public bool IsModified =>
            IsHardEdited ||
            string.Equals(PartSource, "SQPart", System.StringComparison.OrdinalIgnoreCase) ||
            (Description ?? string.Empty).TrimStart().StartsWith("SQ PART", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>True when the part classification is "Sub-Floor".</summary>
        public bool IsSubFloor => GetClassification() == "Sub-Floor";
    }

    /// <summary>All IPT parts found under an assembly, keyed by part number.</summary>
    public sealed class IptScanResult
    {
        public List<IptProperties> Parts { get; set; } = new();

        /// <summary>Change markers per surface IAM, for Regenerate / Upload changed. Empty in older snapshots.</summary>
        public List<SurfaceChangeState> Surfaces { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Verification result
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class VerificationResult
    {
        public bool IsPass => Mismatches.Count == 0;
        public List<IptMismatch> Mismatches { get; set; } = new();

        /// <summary>One outcome per part, including parts that could not be checked.</summary>
        public List<PartCheck> Checks { get; set; } = new();

        public int CountOf(PartCheckStatus status) => Checks.Count(c => c.Status == status);
    }

    public enum PartCheckStatus
    {
        Pass,
        Mismatch,
        /// <summary>Rule is display-only; values are shown but never flagged.</summary>
        DisplayOnly,
        /// <summary>A rule applies but the part could not be compared; <see cref="PartCheck.Reason"/> says why.</summary>
        NotChecked,
        /// <summary>No rule covers this part, so it is intentionally not verified.</summary>
        NotInConfig,
    }

    /// <summary>The verification outcome for one IPT part.</summary>
    public sealed class PartCheck
    {
        public string SurfaceIamPath  { get; set; } = string.Empty;
        public string IptPartNumber   { get; set; } = string.Empty;
        public string IptFilePath     { get; set; } = string.Empty;
        public string Classification  { get; set; } = string.Empty;
        public string Section         { get; set; } = string.Empty;
        public string FieldName       { get; set; } = string.Empty;
        public string ExpectedValue   { get; set; } = string.Empty;
        public string ActualValue     { get; set; } = string.Empty;
        public PartCheckStatus Status { get; set; }
        public string Reason          { get; set; } = string.Empty;

        /// <summary>The part was modified after generation (SQ part or verifier edit); see <see cref="IptProperties.IsModified"/>.</summary>
        public bool IsModified        { get; set; }
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
