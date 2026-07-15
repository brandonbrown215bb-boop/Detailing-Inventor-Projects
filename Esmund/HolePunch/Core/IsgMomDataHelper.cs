using System;
using Inventor;

namespace SkinChannelPunch.Core
{
    /// <summary>
    /// Reads ISG MOM_DATA attributes on a part document (see Document → AttributeSets → MOM_DATA).
    /// </summary>
    internal static class IsgMomDataHelper
    {
        public const string AttributeSetName = "MOM_DATA";
        public const string DocumentStatusName = "DOCUMENT_STATUS";
        public const string StatusNoChange = "NoChange";

        /// <summary>
        /// True when MOM_DATA.DOCUMENT_STATUS is NoChange (typical unconverted Derived part).
        /// Missing MOM_DATA / attribute → false (non-ISG parts are not warned).
        /// </summary>
        public static bool IsDocumentStatusNoChange(Document document)
        {
            return TryGetDocumentStatus(document, out string status)
                && string.Equals(status, StatusNoChange, StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryGetDocumentStatus(Document document, out string status)
        {
            status = null;
            if (document?.AttributeSets == null)
            {
                return false;
            }

            try
            {
                if (!document.AttributeSets.get_NameIsUsed(AttributeSetName))
                {
                    return false;
                }

                AttributeSet set = document.AttributeSets[AttributeSetName];
                if (!set.get_NameIsUsed(DocumentStatusName))
                {
                    return false;
                }

                object value = set[DocumentStatusName].Value;
                status = value?.ToString()?.Trim();
                return !string.IsNullOrEmpty(status);
            }
            catch
            {
                return false;
            }
        }

        public static Document GetOccurrenceDocument(ComponentOccurrence occurrence)
        {
            try
            {
                return occurrence?.Definition?.Document as Document;
            }
            catch
            {
                return null;
            }
        }
    }
}
