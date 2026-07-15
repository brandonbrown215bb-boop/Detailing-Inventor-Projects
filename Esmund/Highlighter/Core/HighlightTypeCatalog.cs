using System;
using System.Collections.Generic;
using Inventor;

namespace Highlighter.Core
{
    public enum HighlightPartType
    {
        WallSkin,
        WallLiner,
        RoofSkin,
        RoofLiner,
        BaseFloor,
        BaseSubfloor
    }

    /// <summary>
    /// VisTog stock taxonomy for skins/liners/floors.
    /// </summary>
    internal static class HighlightTypeCatalog
    {
        private sealed class TypeRule
        {
            public HighlightPartType Type { get; set; }
            public string Stock { get; set; }
            public string Suffix { get; set; }
            public string[] MomTokens { get; set; }
            public string[] DescriptionTokens { get; set; }
        }

        private static readonly TypeRule[] Rules =
        {
            new TypeRule
            {
                Type = HighlightPartType.WallSkin,
                Stock = "091-30117-081",
                Suffix = "081",
                MomTokens = new[] { "WALL SKIN", "SKIN" },
                DescriptionTokens = new[] { "WALL SKIN", "EXT-WALL SKIN", "WALL SKIN" }
            },
            new TypeRule
            {
                Type = HighlightPartType.WallLiner,
                Stock = "091-30117-082",
                Suffix = "082",
                MomTokens = new[] { "WALL LINER", "LINER" },
                DescriptionTokens = new[] { "WALL LINER", "LINER" }
            },
            new TypeRule
            {
                Type = HighlightPartType.RoofSkin,
                Stock = "091-30117-083",
                Suffix = "083",
                MomTokens = new[] { "ROOF SKIN", "SKIN" },
                DescriptionTokens = new[] { "ROOF SKIN" }
            },
            new TypeRule
            {
                Type = HighlightPartType.RoofLiner,
                Stock = "091-30117-084",
                Suffix = "084",
                MomTokens = new[] { "ROOF LINER", "LINER" },
                DescriptionTokens = new[] { "ROOF LINER" }
            },
            new TypeRule
            {
                Type = HighlightPartType.BaseFloor,
                Stock = "091-30117-056",
                Suffix = "056",
                MomTokens = new[] { "FLOOR" },
                DescriptionTokens = new[] { "FLOOR", "BASE FLOOR" }
            },
            new TypeRule
            {
                Type = HighlightPartType.BaseSubfloor,
                Stock = "091-30117-080",
                Suffix = "080",
                MomTokens = new[] { "SUB FLOOR", "SUBFLOOR" },
                DescriptionTokens = new[] { "SUB FLOOR", "SUBFLOOR", "SUB-FLOOR" }
            },
        };

        public static string DisplayName(HighlightPartType type)
        {
            switch (type)
            {
                case HighlightPartType.WallSkin: return "Wall Skins";
                case HighlightPartType.WallLiner: return "Wall Liners";
                case HighlightPartType.RoofSkin: return "Roof Skins";
                case HighlightPartType.RoofLiner: return "Roof Liners";
                case HighlightPartType.BaseFloor: return "Base Floor";
                case HighlightPartType.BaseSubfloor: return "Base Subfloor";
                default: return type.ToString();
            }
        }

        public static HighlightPartType? Classify(ComponentOccurrence occurrence)
        {
            if (occurrence == null)
            {
                return null;
            }

            try
            {
                Document doc = occurrence.Definition?.Document as Document;
                if (doc == null)
                {
                    return null;
                }

                string name = occurrence.Name ?? string.Empty;
                string display = doc.DisplayName ?? string.Empty;
                string file = doc.FullFileName ?? string.Empty;
                string model = null;
                TryGetMomString(doc, "MODEL_NUMBER", out model);
                string libType = null;
                TryGetMomString(doc, "LIBRARY_FILE_TYPE", out libType);
                string partType = null;
                TryGetMomString(doc, "PART_TYPE", out partType);

                string partNumber = string.Empty;
                string description = string.Empty;
                try
                {
                    PropertySet dts = doc.PropertySets["Design Tracking Properties"];
                    partNumber = dts["Part Number"].Value?.ToString() ?? string.Empty;
                    description = dts["Description"].Value?.ToString() ?? string.Empty;
                }
                catch
                {
                }

                // Stock match first (most specific).
                foreach (TypeRule rule in Rules)
                {
                    if (ContainsStock(name, rule.Stock, rule.Suffix)
                        || ContainsStock(display, rule.Stock, rule.Suffix)
                        || ContainsStock(file, rule.Stock, rule.Suffix)
                        || ContainsStock(model, rule.Stock, rule.Suffix)
                        || ContainsStock(partNumber, rule.Stock, rule.Suffix))
                    {
                        return rule.Type;
                    }
                }

                // Disambiguate skins/liners by zone + description/library.
                string blob = ((libType ?? string.Empty) + " " + (partType ?? string.Empty) + " " + description)
                    .ToUpperInvariant();

                if (blob.Contains("SUB FLOOR") || blob.Contains("SUBFLOOR") || blob.Contains("SUB-FLOOR"))
                {
                    return HighlightPartType.BaseSubfloor;
                }

                if (blob.Contains("FLOOR") && !blob.Contains("ROOF") && !blob.Contains("WALL"))
                {
                    return HighlightPartType.BaseFloor;
                }

                if (blob.Contains("ROOF") && blob.Contains("LINER"))
                {
                    return HighlightPartType.RoofLiner;
                }

                if (blob.Contains("ROOF") && blob.Contains("SKIN"))
                {
                    return HighlightPartType.RoofSkin;
                }

                if (blob.Contains("WALL") && blob.Contains("LINER"))
                {
                    return HighlightPartType.WallLiner;
                }

                if (blob.Contains("WALL") && blob.Contains("SKIN"))
                {
                    return HighlightPartType.WallSkin;
                }
            }
            catch
            {
            }

            return null;
        }

        public static List<string> CollectPathNames(AssemblyDocument assembly, HighlightPartType type)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (assembly == null)
            {
                return names;
            }

            try
            {
                CollectIndexed(assembly.ComponentDefinition.Occurrences, type, names, seen);
            }
            catch
            {
            }

            return names;
        }

        private static void CollectIndexed(
            ComponentOccurrences occurrences,
            HighlightPartType type,
            List<string> names,
            HashSet<string> seen)
        {
            if (occurrences == null)
            {
                return;
            }

            for (int i = 1; i <= occurrences.Count; i++)
            {
                ComponentOccurrence occ;
                try
                {
                    occ = occurrences[i];
                }
                catch
                {
                    continue;
                }

                TryAdd(occ, type, names, seen);

                try
                {
                    if (occ.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject
                        && occ.SubOccurrences != null
                        && occ.SubOccurrences.Count > 0)
                    {
                        CollectIndexedEnum(occ.SubOccurrences, type, names, seen);
                    }
                }
                catch
                {
                }
            }
        }

        private static void CollectIndexedEnum(
            ComponentOccurrencesEnumerator occurrences,
            HighlightPartType type,
            List<string> names,
            HashSet<string> seen)
        {
            if (occurrences == null)
            {
                return;
            }

            for (int i = 1; i <= occurrences.Count; i++)
            {
                ComponentOccurrence occ;
                try
                {
                    occ = occurrences[i];
                }
                catch
                {
                    continue;
                }

                TryAdd(occ, type, names, seen);

                try
                {
                    if (occ.DefinitionDocumentType == DocumentTypeEnum.kAssemblyDocumentObject
                        && occ.SubOccurrences != null
                        && occ.SubOccurrences.Count > 0)
                    {
                        CollectIndexedEnum(occ.SubOccurrences, type, names, seen);
                    }
                }
                catch
                {
                }
            }
        }

        private static void TryAdd(
            ComponentOccurrence occ,
            HighlightPartType type,
            List<string> names,
            HashSet<string> seen)
        {
            if (Classify(occ) != type)
            {
                return;
            }

            string path;
            try
            {
                path = occ.Name;
            }
            catch
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(path) && seen.Add(path))
            {
                names.Add(path);
            }
        }

        private static bool ContainsStock(string text, string stock, string suffix)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string t = text.Replace('_', '-');
            if (t.IndexOf(stock, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return t.IndexOf("30117-" + suffix, StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("30117" + suffix, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryGetMomString(Document doc, string attrName, out string value)
        {
            value = null;
            try
            {
                if (doc?.AttributeSets == null || !doc.AttributeSets.get_NameIsUsed("MOM_DATA"))
                {
                    return false;
                }

                AttributeSet set = doc.AttributeSets["MOM_DATA"];
                if (!set.get_NameIsUsed(attrName))
                {
                    return false;
                }

                value = set[attrName].Value?.ToString()?.Trim();
                return !string.IsNullOrEmpty(value);
            }
            catch
            {
                return false;
            }
        }
    }
}
