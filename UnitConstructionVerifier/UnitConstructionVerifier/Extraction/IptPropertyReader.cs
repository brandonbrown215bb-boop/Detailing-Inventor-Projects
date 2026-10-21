using System;
using System.Collections.Generic;
using Inventor;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Extraction
{
    /// <summary>
    /// Recursively walks all component occurrences in an assembly and reads
    /// the User Defined iProperties from each IPT part document.
    /// </summary>
    public sealed class IptPropertyReader
    {
        public IptScanResult ScanAssembly(AssemblyDocument rootDoc)
        {
            var result  = new IptScanResult();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Determine if the root document itself is a Surface IAM
            string initialOwner = HasConfigJson(rootDoc) ? rootDoc.FullFileName : string.Empty;

            ScanOccurrences(rootDoc.ComponentDefinition.Occurrences, initialOwner, result, visited);
            return result;
        }

        private void ScanOccurrences(
            ComponentOccurrences occurrences,
            string ownerIamPath,
            IptScanResult result,
            HashSet<string> visited)
        {
            foreach (ComponentOccurrence occ in occurrences)
            {
                try
                {
                    Inventor.Document refDoc = (Inventor.Document)occ.Definition.Document;
                    string filePath = refDoc.FullFileName;

                    if (visited.Contains(filePath)) continue;
                    visited.Add(filePath);

                    if (refDoc is PartDocument partDoc)
                    {
                        var props = ReadPartProperties(partDoc, ownerIamPath);
                        result.Parts.Add(props);
                    }
                    else if (refDoc is AssemblyDocument subAsm)
                    {
                        // If the subassembly has a CONFIG_JSON, it defines its own surface boundary.
                        // Otherwise, it inherits the parent surface owner.
                        string nextOwner = HasConfigJson(subAsm) ? filePath : ownerIamPath;

                        ScanOccurrences(
                            subAsm.ComponentDefinition.Occurrences,
                            nextOwner,
                            result,
                            visited);
                    }
                }
                catch { /* skip inaccessible references */ }
            }
        }

        private static IptProperties ReadPartProperties(PartDocument doc, string ownerIamPath)
            => IptPropertyMapper.Map(new PartDocumentSource(doc), ownerIamPath);

        /// <summary><see cref="IPartPropertySource"/> over a live Inventor part document.</summary>
        private sealed class PartDocumentSource : IPartPropertySource
        {
            private readonly PartDocument _doc;

            public PartDocumentSource(PartDocument doc) => _doc = doc;

            public string FilePath => _doc.FullFileName;

            public string ReadProperty(string setName, string propertyName)
            {
                try { return _doc.PropertySets[setName][propertyName]?.Value?.ToString() ?? string.Empty; }
                catch { return string.Empty; }
            }

            public bool HasAttributeSet(string setName) => FindSet(setName) != null;

            public string ReadAttribute(string setName, string attributeName)
            {
                try
                {
                    var set = FindSet(setName);
                    if (set == null) return string.Empty;
                    foreach (Inventor.Attribute attr in set)
                        if (string.Equals(attr.Name, attributeName, StringComparison.OrdinalIgnoreCase))
                            return attr.Value?.ToString() ?? string.Empty;
                }
                catch { }
                return string.Empty;
            }

            public string ActiveMaterialName()
            {
                try { return _doc.ActiveMaterial?.Name ?? string.Empty; }
                catch { return string.Empty; }
            }

            public string SheetMetalThicknessInches()
            {
                try
                {
                    if (_doc.ComponentDefinition is SheetMetalComponentDefinition smDef)
                    {
                        double inches = Convert.ToDouble(smDef.Thickness.Value) / 2.54;
                        return inches.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
                catch { }
                return string.Empty;
            }

            private AttributeSet? FindSet(string setName)
            {
                try
                {
                    foreach (AttributeSet set in _doc.AttributeSets)
                        if (string.Equals(set.Name, setName, StringComparison.OrdinalIgnoreCase))
                            return set;
                }
                catch { }
                return null;
            }
        }

        private static bool HasConfigJson(AssemblyDocument doc)
        {
            try
            {
                foreach (AttributeSet set in doc.AttributeSets)
                {
                    foreach (Inventor.Attribute attr in set)
                    {
                        if (string.Equals(attr.Name, "DOCUMENT_CONFIG_JSON", StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch {}
            return false;
        }
    }
}
