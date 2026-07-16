using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace SkinChannelPunch.Core
{
    internal sealed class PatternValues
    {
        public double topInsetIn { get; set; } = PatternConstants.TopInsetIn;
        public double bottomInsetIn { get; set; } = PatternConstants.BottomInsetIn;
        public double maxCenterToCenterIn { get; set; } = PatternConstants.MaxCenterToCenterIn;

        public PatternValues Clone()
        {
            return new PatternValues
            {
                topInsetIn = topInsetIn,
                bottomInsetIn = bottomInsetIn,
                maxCenterToCenterIn = maxCenterToCenterIn,
            };
        }

        public void ApplyOverrides(Dictionary<string, object> overrides)
        {
            if (overrides == null)
            {
                return;
            }

            if (TryGetDouble(overrides, "topInsetIn", out double top))
            {
                topInsetIn = top;
            }

            if (TryGetDouble(overrides, "bottomInsetIn", out double bottom))
            {
                bottomInsetIn = bottom;
            }

            if (TryGetDouble(overrides, "maxCenterToCenterIn", out double ctc))
            {
                maxCenterToCenterIn = ctc;
            }
        }

        private static bool TryGetDouble(Dictionary<string, object> map, string key, out double value)
        {
            value = 0;
            if (!map.TryGetValue(key, out object raw) || raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToDouble(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    internal sealed class PatternModifier
    {
        public string id { get; set; }
        public string label { get; set; }
        public Dictionary<string, object> overrides { get; set; }
    }

    internal sealed class PatternVariant
    {
        public string id { get; set; }
        public string label { get; set; }
        public string exclusiveGroup { get; set; }
        public bool defaultSelected { get; set; }
        public PatternValues values { get; set; }
        public List<PatternModifier> modifiers { get; set; }
    }

    internal sealed class PatternSurfaceType
    {
        public string id { get; set; }
        public string label { get; set; }
        public string exclusiveGroup { get; set; }
        public bool defaultSelected { get; set; }
        public List<PatternVariant> variants { get; set; }
    }

    /// <summary>
    /// One catalog row (Wall/Roof key or Deck thickness|spacing). filled=false means not punchable yet.
    /// </summary>
    internal sealed class PatternCatalogEntry
    {
        public string key { get; set; }
        public string path { get; set; }
        public bool filled { get; set; }
        public double topInsetIn { get; set; }
        public double bottomInsetIn { get; set; }
        public double maxCenterToCenterIn { get; set; }
    }

    internal sealed class PatternPresetFile
    {
        public int version { get; set; } = 1;
        public double holeDiameterIn { get; set; } = PatternConstants.HoleDiameterIn;
        public string notes { get; set; }
        public double wallExteriorPlacementExternalBottomExtraIn { get; set; } = 0.5;
        public PatternValues defaults { get; set; }
        public List<PatternSurfaceType> surfaceTypes { get; set; }

        /// <summary>v3+: Wall picker catalog entries (key matches PatternCatalog.BuildWallKey).</summary>
        public List<PatternCatalogEntry> wall { get; set; }

        /// <summary>v3+: Roof picker catalog (same key shape as wall).</summary>
        public List<PatternCatalogEntry> roof { get; set; }

        /// <summary>v3+: Deck thickness|spacing entries.</summary>
        public List<PatternCatalogEntry> deck { get; set; }
    }

    internal static class PatternPresetConfig
    {
        public const string FileName = "pattern-presets.json";
        public const string EditorFileName = "pattern-presets-editor.html";

        public static string PluginDirectory
        {
            get
            {
                try
                {
                    return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public static string ConfigPath => Path.Combine(PluginDirectory, FileName);

        public static PatternPresetFile LoadOrDefault()
        {
            try
            {
                string path = ConfigPath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var serializer = new JavaScriptSerializer();
                    PatternPresetFile file = serializer.Deserialize<PatternPresetFile>(json);
                    if (file != null)
                    {
                        Normalize(file);
                        PatternCatalog.ApplyFromFile(file);
                        return file;
                    }
                }
            }
            catch
            {
            }

            return CreateBuiltInDefault();
        }

        public static PatternValues Resolve(
            PatternPresetFile file,
            string surfaceId,
            string variantId,
            IEnumerable<string> selectedModifierIds)
        {
            PatternValues result = (file?.defaults ?? new PatternValues()).Clone();

            PatternSurfaceType surface = FindSurface(file, surfaceId);
            PatternVariant variant = FindVariant(surface, variantId);
            if (variant?.values != null)
            {
                result.topInsetIn = variant.values.topInsetIn;
                result.bottomInsetIn = variant.values.bottomInsetIn;
                result.maxCenterToCenterIn = variant.values.maxCenterToCenterIn;
            }

            if (variant?.modifiers == null || selectedModifierIds == null)
            {
                return result;
            }

            var selected = new HashSet<string>(selectedModifierIds, StringComparer.OrdinalIgnoreCase);
            foreach (PatternModifier modifier in variant.modifiers)
            {
                if (modifier == null || string.IsNullOrWhiteSpace(modifier.id))
                {
                    continue;
                }

                if (!selected.Contains(modifier.id))
                {
                    continue;
                }

                result.ApplyOverrides(modifier.overrides);
            }

            return result;
        }

        private static void Normalize(PatternPresetFile file)
        {
            if (file.defaults == null)
            {
                file.defaults = new PatternValues();
            }

            if (file.holeDiameterIn <= 0)
            {
                file.holeDiameterIn = PatternConstants.HoleDiameterIn;
            }

            if (file.wallExteriorPlacementExternalBottomExtraIn <= 0)
            {
                file.wallExteriorPlacementExternalBottomExtraIn = 0.5;
            }

            if (file.wall == null)
            {
                file.wall = new List<PatternCatalogEntry>();
            }

            if (file.roof == null)
            {
                file.roof = new List<PatternCatalogEntry>();
            }

            if (file.deck == null)
            {
                file.deck = new List<PatternCatalogEntry>();
            }

            MarkFilled(file.wall);
            MarkFilled(file.roof);
            MarkFilled(file.deck);

            if (file.surfaceTypes == null)
            {
                file.surfaceTypes = new List<PatternSurfaceType>();
            }

            foreach (PatternSurfaceType surface in file.surfaceTypes)
            {
                if (surface.variants == null)
                {
                    surface.variants = new List<PatternVariant>();
                }

                foreach (PatternVariant variant in surface.variants)
                {
                    if (variant.values == null)
                    {
                        variant.values = file.defaults.Clone();
                    }

                    if (variant.modifiers == null)
                    {
                        variant.modifiers = new List<PatternModifier>();
                    }
                }
            }
        }

        private static void MarkFilled(List<PatternCatalogEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (PatternCatalogEntry e in entries)
            {
                if (e == null || e.filled)
                {
                    continue;
                }

                // JSON null → 0 for doubles; do not invent filled=true from zeros.
                // Promote only when the row clearly has real Patterns.md-style numbers and the flag was omitted.
                if (e.topInsetIn > 0 && e.bottomInsetIn > 0 && e.maxCenterToCenterIn > 0)
                {
                    e.filled = true;
                }
            }
        }

        private static PatternSurfaceType FindSurface(PatternPresetFile file, string surfaceId)
        {
            if (file?.surfaceTypes == null)
            {
                return null;
            }

            foreach (PatternSurfaceType surface in file.surfaceTypes)
            {
                if (string.Equals(surface.id, surfaceId, StringComparison.OrdinalIgnoreCase))
                {
                    return surface;
                }
            }

            return null;
        }

        private static PatternVariant FindVariant(PatternSurfaceType surface, string variantId)
        {
            if (surface?.variants == null)
            {
                return null;
            }

            foreach (PatternVariant variant in surface.variants)
            {
                if (string.Equals(variant.id, variantId, StringComparison.OrdinalIgnoreCase))
                {
                    return variant;
                }
            }

            return null;
        }

        private static PatternPresetFile CreateBuiltInDefault()
        {
            return new PatternPresetFile
            {
                version = 1,
                holeDiameterIn = PatternConstants.HoleDiameterIn,
                defaults = new PatternValues(),
                surfaceTypes = new List<PatternSurfaceType>
                {
                    new PatternSurfaceType
                    {
                        id = "wall",
                        label = "Wall",
                        exclusiveGroup = "surface",
                        defaultSelected = true,
                        variants = new List<PatternVariant>
                        {
                            new PatternVariant
                            {
                                id = "external",
                                label = "External",
                                exclusiveGroup = "wallKind",
                                defaultSelected = true,
                                values = new PatternValues(),
                                modifiers = new List<PatternModifier>(),
                            },
                        },
                    },
                },
            };
        }
    }
}
