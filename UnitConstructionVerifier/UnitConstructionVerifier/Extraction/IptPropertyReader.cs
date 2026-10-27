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
            if (initialOwner.Length > 0) ReadSurfaceChanges(rootDoc, result);

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
                        bool isSurface = HasConfigJson(subAsm);
                        string nextOwner = isSurface ? filePath : ownerIamPath;
                        if (isSurface) ReadSurfaceChanges(subAsm, result);

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

        private static void ReadSurfaceChanges(AssemblyDocument surface, IptScanResult result)
        {
            try { result.Surfaces.Add(SurfaceChangeReader.Read(surface)); }
            catch (Exception ex) { DebugLogger.Log(ex, $"IptPropertyReader.ReadSurfaceChanges: {surface.FullFileName}"); }
        }

        private static IptProperties ReadPartProperties(PartDocument doc, string ownerIamPath)
            => IptPropertyMapper.Map(new PartDocumentSource(doc), ownerIamPath);

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
