using System;
using System.Collections.Generic;
using Inventor;

namespace VisTog.Core
{
    public static class StockPropertyHelper
    {
        public static string GetStockNumber(ComponentOccurrence occurrence)
        {
            Document document = GetReferencedDocument(occurrence);
            if (document == null)
            {
                return string.Empty;
            }

            return GetDesignTrackingValue(document, "Stock Number");
        }

        /// <summary>
        /// Project-tab Description (Design Tracking Properties).
        /// </summary>
        public static string GetDescription(ComponentOccurrence occurrence)
        {
            Document document = GetReferencedDocument(occurrence);
            if (document == null)
            {
                return string.Empty;
            }

            return GetDesignTrackingValue(document, "Description");
        }

        private static Document GetReferencedDocument(ComponentOccurrence occurrence)
        {
            if (occurrence == null)
            {
                return null;
            }

            try
            {
                var document = occurrence.ReferencedDocumentDescriptor.ReferencedDocument as Document;
                if (document != null)
                {
                    return document;
                }
            }
            catch
            {
            }

            try
            {
                ComponentDefinition definition = occurrence.Definition;
                if (definition is PartComponentDefinition partDefinition)
                {
                    return partDefinition.Document as Document;
                }

                if (definition is AssemblyComponentDefinition assemblyDefinition)
                {
                    return assemblyDefinition.Document as Document;
                }
            }
            catch
            {
            }

            return null;
        }

        private static string GetDesignTrackingValue(Document document, string propertyName)
        {
            try
            {
                object raw = document.PropertySets["Design Tracking Properties"][propertyName].Value;
                if (raw == null)
                {
                    return string.Empty;
                }

                return Convert.ToString(raw)?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public static class AssemblyOccurrenceWalker
    {
        public static void CollectOccurrences(ComponentOccurrences occurrences, ICollection<ComponentOccurrence> list)
        {
            if (occurrences == null || list == null)
            {
                return;
            }

            foreach (ComponentOccurrence occurrence in occurrences)
            {
                list.Add(occurrence);

                if (occurrence.DefinitionDocumentType != DocumentTypeEnum.kAssemblyDocumentObject)
                {
                    continue;
                }

                try
                {
                    var referencedDocument = occurrence.ReferencedDocumentDescriptor.ReferencedDocument as Document;
                    if (referencedDocument == null)
                    {
                        continue;
                    }

                    string fileName = System.IO.Path.GetFileNameWithoutExtension(referencedDocument.FullFileName);
                    if (!fileName.StartsWith("391Z", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (ComponentOccurrence subOccurrence in occurrence.SubOccurrences)
                    {
                        list.Add(subOccurrence);
                        if (subOccurrence.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
                        {
                            CollectNestedAssembly(subOccurrence, list);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Full recursive walk (no 391Z filter) for surface Description matching.
        /// </summary>
        public static void CollectAllOccurrences(ComponentOccurrences occurrences, ICollection<ComponentOccurrence> list)
        {
            if (occurrences == null || list == null)
            {
                return;
            }

            foreach (ComponentOccurrence occurrence in occurrences)
            {
                list.Add(occurrence);
                CollectAllDescendants(occurrence, list);
            }
        }

        private static void CollectAllDescendants(ComponentOccurrence occurrence, ICollection<ComponentOccurrence> list)
        {
            if (occurrence.DefinitionDocumentType != DocumentTypeEnum.kAssemblyDocumentObject)
            {
                return;
            }

            try
            {
                foreach (ComponentOccurrence subOccurrence in occurrence.SubOccurrences)
                {
                    list.Add(subOccurrence);
                    CollectAllDescendants(subOccurrence, list);
                }
            }
            catch
            {
            }
        }

        private static void CollectNestedAssembly(ComponentOccurrence assemblyOccurrence, ICollection<ComponentOccurrence> list)
        {
            try
            {
                var referencedDocument = assemblyOccurrence.ReferencedDocumentDescriptor.ReferencedDocument as Document;
                if (referencedDocument == null)
                {
                    return;
                }

                string fileName = System.IO.Path.GetFileNameWithoutExtension(referencedDocument.FullFileName);
                if (!fileName.StartsWith("391Z", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                foreach (ComponentOccurrence subOccurrence in assemblyOccurrence.SubOccurrences)
                {
                    list.Add(subOccurrence);
                    if (subOccurrence.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
                    {
                        CollectNestedAssembly(subOccurrence, list);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
