using System;
using System.Collections.Generic;
using System.Globalization;
using Inventor;
using Newtonsoft.Json;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Extraction
{
    /// <summary>
    /// Reads the DOCUMENT_ENGINEERING_JSON attribute from an assembly's
    /// AttributeSets and extracts curb-rest and other engineering constants.
    /// </summary>
    public sealed class EngineeringJsonExtractor
    {
        private const string AttrSetSearchName = "DOCUMENT_ENGINEERING_JSON";

        public EngineeringData? Extract(AssemblyDocument doc)
        {
            try
            {
                AttributeSets attrSets = doc.AttributeSets;
                foreach (AttributeSet set in attrSets)
                {
                    foreach (Inventor.Attribute attr in set)
                    {
                        try
                        {
                            if (!string.Equals(attr.Name, AttrSetSearchName,
                                    StringComparison.OrdinalIgnoreCase)) continue;

                            var result = Parse(AttributeText.Decode(attr.Value));
                            if (result is null) continue;
                            return result;
                        }
                        catch { /* skip malformed */ }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UCV] EngineeringJsonExtractor: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Parses a DOCUMENT_ENGINEERING_JSON payload. Shared with the headless scan tool.
        /// </summary>
        public static EngineeringData? Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var root = JsonConvert.DeserializeObject<EngineeringJson>(json);
            if (root?.Rules is null) return null;

            var result = new EngineeringData();
            foreach (var constant in root.Rules.Constants)
            {
                if (string.IsNullOrWhiteSpace(constant.Type)) continue;
                double? v = TryParseDouble(constant.Value);
                if (constant.Value != null)
                    result.Constants[constant.Type] = v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : constant.Value.ToString() ?? string.Empty;

                switch (constant.Type)
                {
                    case "UnitBase_CurbSupportAngleHeight":
                        result.CurbSupportAngleHeight = v; break;
                    case "UnitBase_CurbSupportAngleFlangeLength":
                        result.CurbSupportAngleFlange = v; break;
                    case "UnitBase_CurbSupportAngleThickness":
                        result.CurbSupportAngleThickness = v; break;
                }
            }
            return result;
        }

        /// <summary>
        /// Extracts and merges engineering constants from the root document
        /// and all referenced sub-assemblies.
        /// </summary>
        public EngineeringData ExtractAll(AssemblyDocument rootDoc)
        {
            var result = new EngineeringData();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rootEng = Extract(rootDoc);
            if (rootEng != null)
            {
                Merge(result, rootEng);
                visited.Add(rootDoc.FullFileName);
            }

            foreach (Document refDoc in rootDoc.AllReferencedDocuments)
            {
                try
                {
                    string path = refDoc.FullFileName;
                    if (visited.Contains(path)) continue;
                    visited.Add(path);

                    if (refDoc is AssemblyDocument asmSubDoc)
                    {
                        var subEng = Extract(asmSubDoc);
                        if (subEng != null)
                        {
                            Merge(result, subEng);
                        }
                    }
                }
                catch { /* skip reference errors */ }
            }

            return result;
        }

        public static void Merge(EngineeringData target, EngineeringData source)
        {
            // Constants are per-unit standards; the first surface that defines one wins.
            foreach (var kvp in source.Constants)
                if (!target.Constants.ContainsKey(kvp.Key))
                    target.Constants[kvp.Key] = kvp.Value;

            if (source.CurbSupportAngleHeight.HasValue && 
                (!target.CurbSupportAngleHeight.HasValue || source.CurbSupportAngleHeight > target.CurbSupportAngleHeight))
                target.CurbSupportAngleHeight = source.CurbSupportAngleHeight;

            if (source.CurbSupportAngleFlange.HasValue && 
                (!target.CurbSupportAngleFlange.HasValue || source.CurbSupportAngleFlange > target.CurbSupportAngleFlange))
                target.CurbSupportAngleFlange = source.CurbSupportAngleFlange;

            if (source.CurbSupportAngleThickness.HasValue && 
                (!target.CurbSupportAngleThickness.HasValue || source.CurbSupportAngleThickness > target.CurbSupportAngleThickness))
                target.CurbSupportAngleThickness = source.CurbSupportAngleThickness;
        }

        private static double? TryParseDouble(object? value)
        {
            if (value is null) return null;
            if (value is double d) return d;
            if (value is long l) return l;
            return double.TryParse(value.ToString(),
                NumberStyles.Any, CultureInfo.InvariantCulture, out double r) ? r : null;
        }
    }
}
