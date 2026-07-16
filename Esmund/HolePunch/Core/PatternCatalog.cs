using System;
using System.Collections.Generic;

namespace SkinChannelPunch.Core
{
    internal enum PatternSurfaceKind
    {
        Wall,
        Roof,
        Deck
    }

    internal enum PatternCompliance
    {
        IbcBaseline,
        Ibc15,
        Noa,
        Tiered
    }

    internal enum PatternArrangement
    {
        Single,
        Stacked
    }

    internal enum PatternTier
    {
        Tier1,
        Tier2
    }

    internal enum PatternFullFoam
    {
        Yes,
        No
    }

    internal enum PatternFace
    {
        Exterior,
        Interior
    }

    internal enum PatternRun
    {
        InternalChannel,
        Overlap
    }

    internal enum PatternPlacement
    {
        External,
        Corridor
    }

    /// <summary>
    /// Selection state for the column picker (Wall/Roof radios or Deck dropdowns).
    /// </summary>
    internal sealed class PatternSelection
    {
        public PatternSurfaceKind Surface { get; set; } = PatternSurfaceKind.Wall;
        public PatternCompliance Compliance { get; set; } = PatternCompliance.IbcBaseline;
        public PatternArrangement Arrangement { get; set; } = PatternArrangement.Single;
        public PatternTier Tier { get; set; } = PatternTier.Tier1;
        public PatternFullFoam FullFoam { get; set; } = PatternFullFoam.Yes;
        public PatternFace Face { get; set; } = PatternFace.Exterior;
        public PatternPlacement Placement { get; set; } = PatternPlacement.External;
        public PatternRun Run { get; set; } = PatternRun.InternalChannel;
        public string DeckThickness { get; set; } = "4";
        public string DeckChannelSpacing { get; set; } = "24";
        public bool RememberLastUsed { get; set; }

        public PatternSelection Clone()
        {
            return new PatternSelection
            {
                Surface = Surface,
                Compliance = Compliance,
                Arrangement = Arrangement,
                Tier = Tier,
                FullFoam = FullFoam,
                Face = Face,
                Placement = Placement,
                Run = Run,
                DeckThickness = DeckThickness,
                DeckChannelSpacing = DeckChannelSpacing,
                RememberLastUsed = RememberLastUsed,
            };
        }
    }

    internal sealed class PatternResolveResult
    {
        public bool Found { get; set; }
        public double TopInsetIn { get; set; }
        public double BottomInsetIn { get; set; }
        public double MaxCenterToCenterIn { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Built-in values from Obsidian Patterns.md; replaced at load by pattern-presets.json v3 when present.
    /// </summary>
    internal static class PatternCatalog
    {
        private static readonly Dictionary<string, PatternValues> Wall =
            new Dictionary<string, PatternValues>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, PatternValues> Roof =
            new Dictionary<string, PatternValues>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, PatternValues> Deck =
            new Dictionary<string, PatternValues>(StringComparer.OrdinalIgnoreCase);

        private static double _wallExteriorPlacementExternalBottomExtraIn = 0.5;

        static PatternCatalog()
        {
            SeedBuiltIns();
        }

        private static void SeedBuiltIns()
        {
            Wall.Clear();
            Roof.Clear();
            Deck.Clear();
            _wallExteriorPlacementExternalBottomExtraIn = 0.5;

            // IBC Baseline · Single · Full Foam Yes
            AddWall("ibc_baseline|single|yes|exterior|internal", 7, 1.25, 36);
            AddWall("ibc_baseline|single|yes|exterior|overlap", 7, 1.25, 9);
            AddWall("ibc_baseline|single|yes|interior|internal", 0.75, 0.75, 27);
            AddWall("ibc_baseline|single|yes|interior|overlap", 0.75, 0.75, 9);
            // IBC Baseline · Single · Full Foam No (Exterior only in MD)
            AddWall("ibc_baseline|single|no|exterior|internal", 7, 1.25, 27);
            AddWall("ibc_baseline|single|no|exterior|overlap", 7, 1.25, 9);
            // IBC Baseline · Stacked
            AddWall("ibc_baseline|stacked|tier1|exterior|internal", 7, 1.25, 6.5);
            AddWall("ibc_baseline|stacked|tier1|exterior|overlap", 7, 1.25, 6.5);
            AddWall("ibc_baseline|stacked|tier2|exterior|internal", 7, 1.25, 6.5);
            AddWall("ibc_baseline|stacked|tier2|exterior|overlap", 7, 1.25, 6.5);
            // IBC 1.5 · Single
            AddWall("ibc_1_5|single|exterior|internal", 7, 1.25, 36);
            AddWall("ibc_1_5|single|exterior|overlap", 7, 1.25, 9);
            // IBC 1.5 · Stacked
            AddWall("ibc_1_5|stacked|tier1|exterior|internal", 7, 1.25, 5);
            AddWall("ibc_1_5|stacked|tier1|exterior|overlap", 7, 1.25, 5);
            AddWall("ibc_1_5|stacked|tier2|exterior|internal", 7, 1.25, 9);
            AddWall("ibc_1_5|stacked|tier2|exterior|overlap", 7, 1.25, 9);
            // NOA
            AddWall("noa|exterior|internal", 3.5, 1.25, 27);
            AddWall("noa|exterior|overlap", 3.5, 1.25, 2.5);
            // Tiered compliance
            AddWall("tiered|tier1|exterior|internal", 7, 1.25, 36);
            AddWall("tiered|tier1|exterior|overlap", 7, 1.25, 9);
            AddWall("tiered|tier2|exterior|internal", 7, 1.25, 36);
            AddWall("tiered|tier2|exterior|overlap", 7, 1.25, 9);

            string[] spacings = { "24", "23.5", "16", "15.67", "12", "11.75" };
            double[] ctc4 = { 5, 5, 7, 7.5, 10, 10 };
            double[] ctc3 = { 5, 5, 7, 7.5, 10, 10 };
            double[] ctc2 = { 5, 5, 7, 7.5, 10, 10 };
            for (int i = 0; i < spacings.Length; i++)
            {
                AddDeck("4|" + spacings[i], 4.25, 4.25, ctc4[i]);
                AddDeck("3|" + spacings[i], 3.25, 3.25, ctc3[i]);
                AddDeck("2|" + spacings[i], 2.25, 2.25, ctc2[i]);
            }
        }

        /// <summary>
        /// Replace built-in tables from pattern-presets.json v3 catalog arrays (filled rows only).
        /// </summary>
        public static void ApplyFromFile(PatternPresetFile file)
        {
            if (file == null || file.version < 3)
            {
                return;
            }

            if (file.wallExteriorPlacementExternalBottomExtraIn > 0)
            {
                _wallExteriorPlacementExternalBottomExtraIn = file.wallExteriorPlacementExternalBottomExtraIn;
            }

            if (file.wall != null && file.wall.Count > 0)
            {
                Wall.Clear();
                foreach (PatternCatalogEntry e in file.wall)
                {
                    if (IsUsable(e))
                    {
                        AddWall(e.key, e.topInsetIn, e.bottomInsetIn, e.maxCenterToCenterIn);
                    }
                }
            }

            if (file.roof != null && file.roof.Count > 0)
            {
                Roof.Clear();
                foreach (PatternCatalogEntry e in file.roof)
                {
                    if (IsUsable(e))
                    {
                        AddRoof(e.key, e.topInsetIn, e.bottomInsetIn, e.maxCenterToCenterIn);
                    }
                }
            }

            if (file.deck != null && file.deck.Count > 0)
            {
                Deck.Clear();
                foreach (PatternCatalogEntry e in file.deck)
                {
                    if (IsUsable(e))
                    {
                        AddDeck(e.key, e.topInsetIn, e.bottomInsetIn, e.maxCenterToCenterIn);
                    }
                }
            }
        }

        private static bool IsUsable(PatternCatalogEntry e)
        {
            return e != null
                && e.filled
                && !string.IsNullOrWhiteSpace(e.key);
        }

        public static bool ArrangementApplies(PatternSelection s)
        {
            return s.Surface != PatternSurfaceKind.Deck
                && (s.Compliance == PatternCompliance.IbcBaseline
                    || s.Compliance == PatternCompliance.Ibc15);
        }

        public static bool TierApplies(PatternSelection s)
        {
            if (s.Surface == PatternSurfaceKind.Deck)
            {
                return false;
            }

            if (s.Compliance == PatternCompliance.Tiered)
            {
                return true;
            }

            return ArrangementApplies(s) && s.Arrangement == PatternArrangement.Stacked;
        }

        public static bool FullFoamApplies(PatternSelection s)
        {
            return s.Surface != PatternSurfaceKind.Deck
                && s.Compliance == PatternCompliance.IbcBaseline
                && s.Arrangement == PatternArrangement.Single;
        }

        public static bool PlacementApplies(PatternSelection s)
        {
            return s.Surface == PatternSurfaceKind.Wall
                && s.Face == PatternFace.Exterior;
        }

        public static bool InteriorAllowed(PatternSelection s)
        {
            if (s.Surface == PatternSurfaceKind.Deck)
            {
                return false;
            }

            PatternSelection probe = s.Clone();
            probe.Face = PatternFace.Interior;
            string key = BuildWallKey(probe);
            Dictionary<string, PatternValues> dict = s.Surface == PatternSurfaceKind.Roof ? Roof : Wall;
            if (dict.ContainsKey(key))
            {
                return true;
            }

            // Fallback when JSON not loaded yet: historic Patterns.md rules.
            if (s.Compliance == PatternCompliance.Noa || s.Compliance == PatternCompliance.Tiered)
            {
                return false;
            }

            if (FullFoamApplies(s) && s.FullFoam == PatternFullFoam.No)
            {
                return false;
            }

            if (s.Compliance == PatternCompliance.Ibc15 && s.Arrangement == PatternArrangement.Single)
            {
                return false;
            }

            // Stacked interiors are allowed in UI only once filled in the catalog.
            if (ArrangementApplies(s) && s.Arrangement == PatternArrangement.Stacked)
            {
                return false;
            }

            return true;
        }

        public static void Normalize(PatternSelection s)
        {
            if (s.Surface == PatternSurfaceKind.Deck)
            {
                return;
            }

            if (!ArrangementApplies(s))
            {
                s.Arrangement = PatternArrangement.Single;
            }

            if (!InteriorAllowed(s))
            {
                s.Face = PatternFace.Exterior;
            }

            if (!PlacementApplies(s))
            {
                s.Placement = PatternPlacement.External;
            }
        }

        public static PatternResolveResult Resolve(PatternSelection raw)
        {
            PatternSelection s = raw.Clone();
            Normalize(s);

            if (s.Surface == PatternSurfaceKind.Deck)
            {
                string deckKey = (s.DeckThickness ?? "4") + "|" + (s.DeckChannelSpacing ?? "24");
                if (Deck.TryGetValue(deckKey, out PatternValues deck))
                {
                    return Ok(deck);
                }

                return Missing("Unknown deck thickness / channel spacing combo.");
            }

            string key = BuildWallKey(s);
            Dictionary<string, PatternValues> dict = s.Surface == PatternSurfaceKind.Roof ? Roof : Wall;
            if (dict.TryGetValue(key, out PatternValues values))
            {
                PatternResolveResult result = Ok(values);
                // Wall Exterior + Placement External: add configurable bottom inset vs catalog Exterior baseline.
                if (PlacementApplies(s) && s.Placement == PatternPlacement.External)
                {
                    result.BottomInsetIn += _wallExteriorPlacementExternalBottomExtraIn;
                }

                return result;
            }

            if (s.Surface == PatternSurfaceKind.Roof)
            {
                return Missing("This Roof combo is not filled in pattern-presets.json yet.");
            }

            return Missing("This Wall combo is not filled in pattern-presets.json yet.");
        }

        private static string BuildWallKey(PatternSelection s)
        {
            string run = s.Run == PatternRun.InternalChannel ? "internal" : "overlap";
            string face = s.Face == PatternFace.Exterior ? "exterior" : "interior";
            string tier = s.Tier == PatternTier.Tier1 ? "tier1" : "tier2";

            if (s.Compliance == PatternCompliance.Noa)
            {
                return "noa|" + face + "|" + run;
            }

            if (s.Compliance == PatternCompliance.Tiered)
            {
                return "tiered|" + tier + "|" + face + "|" + run;
            }

            string compliance = s.Compliance == PatternCompliance.IbcBaseline ? "ibc_baseline" : "ibc_1_5";
            if (s.Arrangement == PatternArrangement.Stacked)
            {
                return compliance + "|stacked|" + tier + "|" + face + "|" + run;
            }

            if (s.Compliance == PatternCompliance.IbcBaseline)
            {
                string foam = s.FullFoam == PatternFullFoam.Yes ? "yes" : "no";
                return "ibc_baseline|single|" + foam + "|" + face + "|" + run;
            }

            return "ibc_1_5|single|" + face + "|" + run;
        }

        private static PatternResolveResult Ok(PatternValues v)
        {
            return new PatternResolveResult
            {
                Found = true,
                TopInsetIn = v.topInsetIn,
                BottomInsetIn = v.bottomInsetIn,
                MaxCenterToCenterIn = v.maxCenterToCenterIn,
            };
        }

        private static PatternResolveResult Missing(string message)
        {
            return new PatternResolveResult
            {
                Found = false,
                TopInsetIn = PatternConstants.TopInsetIn,
                BottomInsetIn = 1.25,
                MaxCenterToCenterIn = PatternConstants.MaxCenterToCenterIn,
                Message = message,
            };
        }

        private static void AddWall(string key, double top, double bottom, double ctc)
        {
            Wall[key] = new PatternValues
            {
                topInsetIn = top,
                bottomInsetIn = bottom,
                maxCenterToCenterIn = ctc,
            };
        }

        private static void AddRoof(string key, double top, double bottom, double ctc)
        {
            Roof[key] = new PatternValues
            {
                topInsetIn = top,
                bottomInsetIn = bottom,
                maxCenterToCenterIn = ctc,
            };
        }

        private static void AddDeck(string key, double top, double bottom, double ctc)
        {
            Deck[key] = new PatternValues
            {
                topInsetIn = top,
                bottomInsetIn = bottom,
                maxCenterToCenterIn = ctc,
            };
        }
    }
}
