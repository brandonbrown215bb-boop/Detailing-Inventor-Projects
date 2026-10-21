using System;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Extraction
{
    /// <summary>
    /// Read access to one part file, implemented over a live Inventor <c>PartDocument</c>
    /// (the add-in) or an Apprentice document (the headless scan tool).
    /// Every member returns an empty string when the value is unavailable.
    /// </summary>
    public interface IPartPropertySource
    {
        string FilePath { get; }
        string ReadProperty(string setName, string propertyName);
        bool HasAttributeSet(string setName);
        string ReadAttribute(string setName, string attributeName);
        string ActiveMaterialName();
        /// <summary>Sheet metal thickness in inches, or empty when not sheet metal.</summary>
        string SheetMetalThicknessInches();
    }

    /// <summary>Maps a part's iProperties and attributes to <see cref="IptProperties"/>.</summary>
    public static class IptPropertyMapper
    {
        private const string UserDefinedSet    = "Inventor User Defined Properties";
        private const string DesignTrackingSet = "Design Tracking Properties";
        private const string MomDataSet        = "MOM_DATA";

        public static IptProperties Map(IPartPropertySource src, string ownerIamPath)
        {
            var props = new IptProperties
            {
                FilePath     = src.FilePath,
                OwnerIamPath = ownerIamPath,
            };

            try
            {
                // ── Design Tracking Properties ────────────────────────────────
                props.PartNumber  = src.ReadProperty(DesignTrackingSet, "Part Number");
                props.Description = src.ReadProperty(DesignTrackingSet, "Description");
                string materialIdentifier = src.ReadProperty(DesignTrackingSet, "Material Identifier");
                string stockNumber = src.ReadProperty(DesignTrackingSet, "Stock Number");

                // ── User Defined iProperties ──────────────────────────────────
                string isHardEditedStr = src.ReadProperty(UserDefinedSet, "IS_HARD_EDITED");
                props.IsHardEdited = string.Equals(isHardEditedStr, "TRUE", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(isHardEditedStr, "1", StringComparison.OrdinalIgnoreCase);

                props.Thickness = src.ReadProperty(UserDefinedSet, "Thickness");
                props.YCMATL    = src.ReadProperty(UserDefinedSet, "YCMATL");
                if (string.IsNullOrWhiteSpace(props.YCMATL))
                    props.YCMATL = src.ReadProperty(UserDefinedSet, "INPUT_PARAMETER_MaterialType");
                if (string.IsNullOrWhiteSpace(props.YCMATL))
                    props.YCMATL = src.ReadProperty(UserDefinedSet, "INPUT_PARAMETER_MaterialStyle");
                props.ModelNumber = src.ReadProperty(UserDefinedSet, "MODEL_NUMBER");
                if (string.IsNullOrWhiteSpace(props.ModelNumber))
                    props.ModelNumber = stockNumber;
                props.MtlGauge      = src.ReadProperty(UserDefinedSet, "INPUT_PARAMETER_Mtl_Gauge");
                props.MaterialStyle = src.ReadProperty(UserDefinedSet, "INPUT_PARAMETER_MaterialStyle");

                // ── MOM_DATA AttributeSet (100% Authoritative Source) ─────────
                bool hasMomData = src.HasAttributeSet(MomDataSet);
                if (hasMomData)
                {
                    string momMat    = src.ReadAttribute(MomDataSet, "MATERIAL_TYPE_CODE");
                    string momThick  = src.ReadAttribute(MomDataSet, "ORIGINAL_SHEET_METAL_THICKNESS");
                    string momModel  = src.ReadAttribute(MomDataSet, "MODEL_NUMBER");
                    string momSource = src.ReadAttribute(MomDataSet, "PART_SOURCE");

                    if (!string.IsNullOrWhiteSpace(momMat))    props.YCMATL = momMat.Trim();
                    if (!string.IsNullOrWhiteSpace(momThick))  props.Thickness = momThick.Trim();
                    if (!string.IsNullOrWhiteSpace(momModel))  props.ModelNumber = momModel.Trim();
                    if (!string.IsNullOrWhiteSpace(momSource)) props.PartSource = momSource.Trim();
                }

                // ── Fallback Material Resolution ──────────────────────────────
                if (string.IsNullOrWhiteSpace(props.YCMATL))
                {
                    string name = src.ActiveMaterialName();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        int colonIdx = name.IndexOf(':');
                        props.YCMATL = (colonIdx >= 0 ? name.Substring(colonIdx + 1) : name).Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(props.YCMATL) && !string.IsNullOrWhiteSpace(materialIdentifier))
                    props.YCMATL = ExtractMaterialFromIdentifier(materialIdentifier);

                if (!hasMomData && props.GetClassification() == "Formed Channel" && props.YCMATL == "STL GALV")
                    props.YCMATL = "STL HOT ROLL";

                // Only use the live sheet metal thickness if the Thickness field is empty.
                if (string.IsNullOrWhiteSpace(props.Thickness))
                    props.Thickness = src.SheetMetalThicknessInches();

                // Only fall back to Thickness for gauge display if the user property is truly absent
                if (string.IsNullOrWhiteSpace(props.MtlGauge))
                    props.MtlGauge = props.Thickness;
            }
            catch { /* return whatever we managed to read */ }

            return props;
        }

        private static string ExtractMaterialFromIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return string.Empty;

            int hashIndex = identifier.IndexOf('#');
            if (hashIndex < 0) return string.Empty;

            string sub = identifier.Substring(hashIndex + 1);
            int colonIndex = sub.IndexOf(':');
            if (colonIndex >= 0) sub = sub.Substring(colonIndex + 1);
            foreach (char stop in new[] { '\n', '\r', '#' })
            {
                int idx = sub.IndexOf(stop);
                if (idx >= 0) sub = sub.Substring(0, idx);
            }
            return sub.Trim();
        }
    }
}
