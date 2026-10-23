using System;

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
    }
}
