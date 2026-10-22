using System;
using System.Collections.Generic;

namespace UnitConstructionVerifier.Operations
{
    /// <summary>
    /// Reads MOM_DATA.DOCUMENT_STATUS, the change marker UCV's write-back and YCShell's Mark As Changed both set to
    /// "Changed_SQ". A successful YCShell upload resets it to "NoChange".
    /// </summary>
    public static class PartChangeStatus
    {
        public const string ChangedSq = "Changed_SQ";

        public static bool IsChangedSq(string? status) =>
            string.Equals(status?.Trim(), ChangedSq, StringComparison.OrdinalIgnoreCase);

        /// <summary>DOCUMENT_STATUS of a part, or null when it has none.</summary>
        public static string? Read(Inventor.Document doc)
        {
            try
            {
                foreach (Inventor.AttributeSet set in doc.AttributeSets)
                {
                    if (set.Name != "MOM_DATA") continue;
                    foreach (Inventor.Attribute attribute in set)
                    {
                        if (attribute.Name == "DOCUMENT_STATUS") return attribute.Value?.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log(ex, "PartChangeStatus.Read");
            }

            return null;
        }

        /// <summary>Count of each DOCUMENT_STATUS value over the parts an assembly references, at any depth.</summary>
        public static IDictionary<string, int> CountByStatus(Inventor.AssemblyDocument assembly)
        {
            var counts = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Inventor.Document doc in assembly.AllReferencedDocuments)
            {
                if (doc.DocumentType != Inventor.DocumentTypeEnum.kPartDocumentObject) continue;
                string status = Read(doc) ?? "(none)";
                counts[status] = counts.TryGetValue(status, out int n) ? n + 1 : 1;
            }

            return counts;
        }
    }
}
