using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Inventor;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Extraction
{
    /// <summary>
    /// Reads YCShell's change markers from a live surface IAM: the assembly's deleted/transformed flags and, per
    /// top-level occurrence, OCCURRENCE_IS_NEW, PART_TYPE, the part's DOCUMENT_STATUS and the occurrence position.
    /// </summary>
    public static class SurfaceChangeReader
    {
        private const string MomData = "MOM_DATA";

        public static SurfaceChangeState Read(AssemblyDocument surface)
        {
            var state = new SurfaceChangeState
            {
                SurfaceIamPath = surface.FullFileName,
                HasDeletedComponents = IsOne(ReadMomAttribute(surface.AttributeSets, "HAS_DELETED_COMPONENTS")),
                HasTransformedComponents = IsOne(ReadMomAttribute(surface.AttributeSets, "HAS_TRANSFORMED_COMPONENTS")),
            };

            foreach (ComponentOccurrence occ in surface.ComponentDefinition.Occurrences)
            {
                try
                {
                    state.Occurrences.Add(ReadOccurrence(occ));
                }
                catch (Exception ex)
                {
                    DebugLogger.Log(ex, $"SurfaceChangeReader: {surface.FullFileName}");
                }
            }

            return state;
        }

        private static SurfaceOccurrence ReadOccurrence(ComponentOccurrence occ)
        {
            string name = occ.Name;
            string partFile = string.Empty;
            string partStatus = string.Empty;
            bool hardEdited = false;

            if (occ.Definition?.Document is Document doc)
            {
                partFile = doc.FullFileName;
                partStatus = ReadMomAttribute(doc.AttributeSets, "DOCUMENT_STATUS");
                hardEdited = IsHardEdited(doc);
            }

            string id = ReadMomAttribute(occ.AttributeSets, "OCCURRENCE_ID");

            return new SurfaceOccurrence
            {
                Id = string.IsNullOrWhiteSpace(id) ? name : id,
                Name = name,
                PartFile = partFile,
                PartType = ReadMomAttribute(occ.AttributeSets, "PART_TYPE"),
                IsNew = IsOne(ReadMomAttribute(occ.AttributeSets, "OCCURRENCE_IS_NEW")),
                PartStatus = partStatus,
                Position = Position(occ),
                PartVersion = PartVersion(partFile),
                PartHardEdited = hardEdited,
            };
        }

        /// <summary>The IS_HARD_EDITED user property UCV's write-back sets.</summary>
        private static bool IsHardEdited(Document doc)
        {
            try
            {
                string value = doc.PropertySets["Inventor User Defined Properties"]["IS_HARD_EDITED"]?.Value?.ToString() ?? string.Empty;
                return string.Equals(value.Trim(), "TRUE", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;   // property absent
            }
        }

        /// <summary>The file version <see cref="SurfaceOccurrence.PartVersion"/> uses: last write time, UTC ticks.</summary>
        public static string PartVersion(string partFile)
        {
            try
            {
                return partFile.Length > 0 && System.IO.File.Exists(partFile)
                    ? System.IO.File.GetLastWriteTimeUtc(partFile).Ticks.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;
            }
            catch (IOException)
            {
                return string.Empty;
            }
        }

        /// <summary>The occurrence's transformation matrix, rounded to 0.001 (cm / unit vectors).</summary>
        internal static string Position(ComponentOccurrence occ)
        {
            try
            {
                double[] cells = new double[16];
                occ.Transformation.GetMatrixData(ref cells);
                return FormatPosition(cells);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string FormatPosition(double[] cells) =>
            string.Join(",", cells.Select(v => (Math.Round(v, 3) + 0.0).ToString("0.###", CultureInfo.InvariantCulture)));

        internal static string ReadMomAttribute(AttributeSets sets, string attributeName)
        {
            try
            {
                foreach (AttributeSet set in sets)
                {
                    if (!string.Equals(set.Name, MomData, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (Inventor.Attribute attribute in set)
                    {
                        if (string.Equals(attribute.Name, attributeName, StringComparison.OrdinalIgnoreCase))
                            return attribute.Value?.ToString() ?? string.Empty;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        private static bool IsOne(string value) => value.Trim() == "1";
    }
}
