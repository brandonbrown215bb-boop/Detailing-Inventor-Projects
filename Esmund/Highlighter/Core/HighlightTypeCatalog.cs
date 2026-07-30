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
    /// Classify by Design Tracking Stock Number (VisTog-style).
    /// From a skid: only descend into 391Z surface subassemblies (liners live there).
    /// From an open 391Z surface: walk the whole surface assembly tree.
    /// Corner stock 091-30117-073 is excluded.
    /// </summary>
    internal static class HighlightTypeCatalog
    {
        private sealed class TypeRule
        {
            public HighlightPartType Type { get; set; }
            public string Stock { get; set; }
        }

        private static readonly TypeRule[] Rules =
        {
            new TypeRule { Type = HighlightPartType.WallSkin, Stock = "091-30117-081" },
            new TypeRule { Type = HighlightPartType.WallLiner, Stock = "091-30117-082" },
            new TypeRule { Type = HighlightPartType.RoofSkin, Stock = "091-30117-083" },
            new TypeRule { Type = HighlightPartType.RoofLiner, Stock = "091-30117-084" },
            new TypeRule { Type = HighlightPartType.BaseFloor, Stock = "091-30117-056" },
            new TypeRule { Type = HighlightPartType.BaseSubfloor, Stock = "091-30117-080" },
        };

        private static readonly Dictionary<string, HighlightPartType> StockLookup = BuildStockLookup();

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

        /// <summary>
        /// Collect matching part occurrences without path round-trip (VisTog-style walk).
        /// </summary>
        public static List<ComponentOccurrence> CollectOccurrences(AssemblyDocument assembly, HighlightPartType type)
        {
            var list = new List<ComponentOccurrence>();
            if (assembly == null) return list;
            bool inside391Z = Is391ZDocument((Document)(object)assembly);
            try
            {
                WalkOccurrences(assembly.ComponentDefinition.Occurrences, type, list, inside391Z);
            }
            catch { }
            return list;
        }

        public static HighlightPartType? Classify(ComponentOccurrence occurrence)
        {
            if (occurrence == null) return null;
            try
            {
                if (occurrence.DefinitionDocumentType != DocumentTypeEnum.kPartDocumentObject) return null;
                Document doc = GetReferencedDocument(occurrence);
                if (doc == null) return null;
                string stock = GetDesignTrackingStock(doc);
                if (string.IsNullOrWhiteSpace(stock)) return null;
                if (StockLookup.TryGetValue(NormalizeStock(stock), out HighlightPartType type)) return type;
            }
            catch { }
            return null;
        }

        private static void WalkOccurrences(
            ComponentOccurrences occurrences,
            HighlightPartType type,
            List<ComponentOccurrence> matches,
            bool inside391Z)
        {
            if (occurrences == null) return;
            for (int i = 1; i <= occurrences.Count; i++)
            {
                ComponentOccurrence occ;
                try { occ = occurrences[i]; } catch { continue; }
                WalkOne(occ, type, matches, inside391Z);
            }
        }

        private static void WalkEnum(
            ComponentOccurrencesEnumerator occurrences,
            HighlightPartType type,
            List<ComponentOccurrence> matches,
            bool inside391Z)
        {
            if (occurrences == null) return;
            for (int i = 1; i <= occurrences.Count; i++)
            {
                ComponentOccurrence occ;
                try { occ = occurrences[i]; } catch { continue; }
                WalkOne(occ, type, matches, inside391Z);
            }
        }

        private static void WalkOne(
            ComponentOccurrence occ,
            HighlightPartType type,
            List<ComponentOccurrence> matches,
            bool inside391Z)
        {
            if (Classify(occ) == type) matches.Add(occ);

            if (occ.DefinitionDocumentType != DocumentTypeEnum.kAssemblyDocumentObject) return;

            bool descend = inside391Z || Is391ZOccurrence(occ);
            if (!descend) return;

            try
            {
                if (occ.SubOccurrences != null && occ.SubOccurrences.Count > 0)
                {
                    WalkEnum(occ.SubOccurrences, type, matches, inside391Z: true);
                }
            }
            catch { }
        }

        private static bool Is391ZDocument(Document document)
        {
            try
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(document?.FullFileName ?? string.Empty);
                return fileName.StartsWith("391Z", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool Is391ZOccurrence(ComponentOccurrence occurrence)
        {
            Document doc = GetReferencedDocument(occurrence);
            return doc != null && Is391ZDocument(doc);
        }

        private static Dictionary<string, HighlightPartType> BuildStockLookup()
        {
            var map = new Dictionary<string, HighlightPartType>(StringComparer.OrdinalIgnoreCase);
            foreach (TypeRule rule in Rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Stock)) continue;
                map[NormalizeStock(rule.Stock)] = rule.Type;
            }
            return map;
        }

        private static string NormalizeStock(string stock)
        {
            return string.IsNullOrWhiteSpace(stock) ? string.Empty : stock.Trim().Replace('_', '-');
        }

        private static string GetDesignTrackingStock(Document doc)
        {
            try
            {
                object raw = doc.PropertySets["Design Tracking Properties"]["Stock Number"].Value;
                if (raw == null) return string.Empty;
                return Convert.ToString(raw)?.Trim() ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        internal static Document GetReferencedDocument(ComponentOccurrence occurrence)
        {
            if (occurrence == null) return null;
            try
            {
                var document = occurrence.ReferencedDocumentDescriptor.ReferencedDocument as Document;
                if (document != null) return document;
            }
            catch { }
            try
            {
                ComponentDefinition definition = occurrence.Definition;
                if (definition is PartComponentDefinition partDefinition) return partDefinition.Document as Document;
                if (definition is AssemblyComponentDefinition assemblyDefinition) return assemblyDefinition.Document as Document;
            }
            catch { }
            return null;
        }
    }
}
