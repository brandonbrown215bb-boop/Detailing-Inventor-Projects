using System;
using System.Collections.Generic;
using Inventor;
using UnitConstructionVerifier.Extraction;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Scan
{
    /// <summary>One surface IAM read through Apprentice: its config, engineering data and parts.</summary>
    internal sealed class SurfaceScan
    {
        public ConfigData Config { get; set; } = null!;
        public EngineeringData? Engineering { get; set; }
        public List<IptProperties> Parts { get; } = new();
    }

    /// <summary>
    /// Read-only access to Inventor files through Apprentice (no running Inventor needed).
    /// Mirrors the add-in's <c>IptPropertyReader</c> walk and uses the same shared parsers.
    /// </summary>
    internal sealed class ApprenticeReader : IDisposable
    {
        private const string ConfigAttribute = "DOCUMENT_CONFIG_JSON";
        private const string EngineeringAttribute = "DOCUMENT_ENGINEERING_JSON";

        private readonly ApprenticeServerComponent _apprentice = new ApprenticeServerComponent();

        /// <summary>Returns the surface scan, or null when the IAM has no DOCUMENT_CONFIG_JSON.</summary>
        public SurfaceScan? ReadSurface(string iamPath)
        {
            ApprenticeServerDocument? doc = null;
            try
            {
                doc = _apprentice.Open(iamPath);
                var config = ConfigJsonExtractor.Parse(ReadAttributeText(doc, ConfigAttribute), doc.FullFileName);
                if (config == null) return null;

                var scan = new SurfaceScan
                {
                    Config = config,
                    Engineering = EngineeringJsonExtractor.Parse(ReadAttributeText(doc, EngineeringAttribute)),
                };
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                ScanOccurrences(((AssemblyComponentDefinition)doc.ComponentDefinition).Occurrences, doc.FullFileName, scan.Parts, visited);
                return scan;
            }
            finally
            {
                try { doc?.Close(); } catch { }
            }
        }

        private static void ScanOccurrences(ComponentOccurrences occurrences, string ownerIamPath, List<IptProperties> parts, HashSet<string> visited)
        {
            foreach (ComponentOccurrence occ in occurrences)
            {
                try
                {
                    var refDoc = (ApprenticeServerDocument)occ.Definition.Document;
                    if (!visited.Add(refDoc.FullFileName)) continue;

                    if (refDoc.DocumentType == DocumentTypeEnum.kPartDocumentObject)
                    {
                        parts.Add(IptPropertyMapper.Map(new ApprenticePartSource(refDoc), ownerIamPath));
                    }
                    else if (refDoc.DocumentType == DocumentTypeEnum.kAssemblyDocumentObject)
                    {
                        // A sub-assembly with its own CONFIG_JSON is its own surface boundary.
                        string nextOwner = string.IsNullOrWhiteSpace(ReadAttributeText(refDoc, ConfigAttribute)) ? ownerIamPath : refDoc.FullFileName;
                        ScanOccurrences(((AssemblyComponentDefinition)refDoc.ComponentDefinition).Occurrences, nextOwner, parts, visited);
                    }
                }
                catch { /* skip inaccessible references, as the add-in does */ }
            }
        }

        private static string ReadAttributeText(ApprenticeServerDocument doc, string attributeName)
        {
            try
            {
                foreach (AttributeSet set in doc.AttributeSets)
                    foreach (Inventor.Attribute attr in set)
                        if (string.Equals(attr.Name, attributeName, StringComparison.OrdinalIgnoreCase))
                            return AttributeText.Decode(attr.Value);
            }
            catch { }
            return string.Empty;
        }

        public void Dispose()
        {
            try { _apprentice.Close(); } catch { }
        }

        /// <summary><see cref="IPartPropertySource"/> over an Apprentice part document.</summary>
        private sealed class ApprenticePartSource : IPartPropertySource
        {
            private readonly ApprenticeServerDocument _doc;

            public ApprenticePartSource(ApprenticeServerDocument doc) => _doc = doc;

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

            // Apprentice exposes neither the active material nor live sheet metal parameters.
            public string ActiveMaterialName() => string.Empty;

            public string SheetMetalThicknessInches() => string.Empty;

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
    }
}
