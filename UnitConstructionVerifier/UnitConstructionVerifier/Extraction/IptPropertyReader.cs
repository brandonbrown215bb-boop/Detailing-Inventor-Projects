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
        // iProperty set names
        private const string UserDefinedSet     = "Inventor User Defined Properties";
        private const string DesignTrackingSet  = "Design Tracking Properties";

        // iProperty names
        private const string PropThickness      = "Thickness";
        private const string PropYCMATL         = "YCMATL";
        private const string PropModelNumber    = "MODEL_NUMBER";
        private const string PropMtlGauge       = "INPUT_PARAMETER_Mtl_Gauge";
        private const string PropMaterialStyle  = "INPUT_PARAMETER_MaterialStyle";
        private const string PropPartNumber     = "Part Number";
        private const string PropDescription    = "Description";

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

        private IptProperties ReadPartProperties(PartDocument doc, string ownerIamPath)
        {
            var props = new IptProperties
            {
                FilePath      = doc.FullFileName,
                OwnerIamPath  = ownerIamPath,
            };

            try
            {
                PropertySets sets = doc.PropertySets;
                string materialIdentifier = string.Empty;

                // ── Design Tracking Properties ────────────────────────────────
                PropertySet? tracking = TryGetSet(sets, DesignTrackingSet);
                string stockNumber = string.Empty;
                if (tracking is not null)
                {
                    props.PartNumber  = ReadProp(tracking, PropPartNumber);
                    props.Description = ReadProp(tracking, PropDescription);
                    materialIdentifier = ReadProp(tracking, "Material Identifier");
                    stockNumber = ReadProp(tracking, "Stock Number");
                }

                // ── User Defined iProperties ──────────────────────────────────
                PropertySet? userDefined = TryGetSet(sets, UserDefinedSet);
                if (userDefined is not null)
                {
                    props.Thickness     = ReadProp(userDefined, PropThickness);
                    props.YCMATL        = ReadProp(userDefined, PropYCMATL);
                    if (string.IsNullOrWhiteSpace(props.YCMATL))
                    {
                        props.YCMATL    = ReadProp(userDefined, "INPUT_PARAMETER_MaterialType");
                    }
                    if (string.IsNullOrWhiteSpace(props.YCMATL))
                    {
                        props.YCMATL    = ReadProp(userDefined, PropMaterialStyle);
                    }
                    if (string.IsNullOrWhiteSpace(props.YCMATL))
                    {
                        try
                        {
                            string name = doc.ActiveMaterial?.Name;
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                int colonIdx = name.IndexOf(':');
                                if (colonIdx >= 0)
                                {
                                    name = name.Substring(colonIdx + 1);
                                }
                                props.YCMATL = name.Trim();
                            }
                        }
                        catch { }
                    }
                    if (string.IsNullOrWhiteSpace(props.YCMATL) && !string.IsNullOrWhiteSpace(materialIdentifier))
                    {
                        props.YCMATL    = ExtractMaterialFromIdentifier(materialIdentifier);
                    }
                    props.ModelNumber   = ReadProp(userDefined, PropModelNumber);
                    if (string.IsNullOrWhiteSpace(props.ModelNumber))
                    {
                        props.ModelNumber = stockNumber;
                    }
                    props.MtlGauge      = ReadProp(userDefined, PropMtlGauge);
                    props.MaterialStyle = ReadProp(userDefined, PropMaterialStyle);

                    if (props.GetClassification() == "Formed Channel" && props.YCMATL == "STL GALV")
                    {
                        props.YCMATL = "STL HOT ROLL";
                    }
                }

                // ── Sheet Metal Component Definition ──────────────────────────
                // Only use the live sheet metal thickness to update the Thickness field.
                // Do NOT let it overwrite MtlGauge — the user-defined INPUT_PARAMETER_Mtl_Gauge
                // property is the authoritative gauge source and may have just been written.
                try
                {
                    if (doc.ComponentDefinition is SheetMetalComponentDefinition smDef)
                    {
                        double actualThick = Convert.ToDouble(smDef.Thickness.Value) / 2.54;
                        props.Thickness = actualThick.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
                catch { }

                // Only fall back to Thickness for gauge display if the user property is truly absent
                if (string.IsNullOrWhiteSpace(props.MtlGauge))
                {
                    props.MtlGauge = props.Thickness;
                }
            }
            catch { /* return whatever we managed to read */ }

            return props;
        }

        private static string ExtractMaterialFromIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return string.Empty;

            int hashIndex = identifier.IndexOf('#');
            if (hashIndex >= 0)
            {
                string sub = identifier.Substring(hashIndex + 1);
                int colonIndex = sub.IndexOf(':');
                if (colonIndex >= 0)
                {
                    sub = sub.Substring(colonIndex + 1);
                }
                int newlineIndex = sub.IndexOf('\n');
                if (newlineIndex >= 0)
                {
                    sub = sub.Substring(0, newlineIndex);
                }
                int returnIndex = sub.IndexOf('\r');
                if (returnIndex >= 0)
                {
                    sub = sub.Substring(0, returnIndex);
                }
                int secondHash = sub.IndexOf('#');
                if (secondHash >= 0)
                {
                    sub = sub.Substring(0, secondHash);
                }
                return sub.Trim();
            }

            return string.Empty;
        }

        private static PropertySet? TryGetSet(PropertySets sets, string name)
        {
            try { return sets[name]; }
            catch { return null; }
        }

        private static string ReadProp(PropertySet set, string name)
        {
            try { return set[name]?.Value?.ToString() ?? string.Empty; }
            catch { return string.Empty; }
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
