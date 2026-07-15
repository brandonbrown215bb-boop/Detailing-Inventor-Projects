using System;
using System.IO;
using System.Web.Script.Serialization;

namespace SkinChannelPunch.Core
{
    internal static class PatternLastUsedStore
    {
        public const string FileName = "pattern-last-used.json";

        private sealed class Dto
        {
            public bool rememberLastUsed { get; set; }
            public string surface { get; set; }
            public string compliance { get; set; }
            public string arrangement { get; set; }
            public string tier { get; set; }
            public string fullFoam { get; set; }
            public string face { get; set; }
            public string placement { get; set; }
            public string run { get; set; }
            public string deckThickness { get; set; }
            public string deckChannelSpacing { get; set; }
        }

        public static string Path =>
            System.IO.Path.Combine(PatternPresetConfig.PluginDirectory, FileName);

        public static PatternSelection LoadOrNull()
        {
            try
            {
                string path = Path;
                if (!File.Exists(path))
                {
                    return null;
                }

                var dto = new JavaScriptSerializer().Deserialize<Dto>(File.ReadAllText(path));
                if (dto == null)
                {
                    return null;
                }

                return FromDto(dto);
            }
            catch
            {
                return null;
            }
        }

        public static void Save(PatternSelection selection)
        {
            try
            {
                string dir = PatternPresetConfig.PluginDirectory;
                if (string.IsNullOrWhiteSpace(dir))
                {
                    return;
                }

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var dto = ToDto(selection);
                string json = new JavaScriptSerializer().Serialize(dto);
                File.WriteAllText(Path, json);
            }
            catch
            {
            }
        }

        private static PatternSelection FromDto(Dto dto)
        {
            var s = new PatternSelection
            {
                RememberLastUsed = dto.rememberLastUsed,
                DeckThickness = string.IsNullOrWhiteSpace(dto.deckThickness) ? "4" : dto.deckThickness,
                DeckChannelSpacing = string.IsNullOrWhiteSpace(dto.deckChannelSpacing)
                    ? "24"
                    : dto.deckChannelSpacing,
            };

            s.Surface = ParseEnum(dto.surface, PatternSurfaceKind.Wall);
            s.Compliance = ParseEnum(dto.compliance, PatternCompliance.IbcBaseline);
            s.Arrangement = ParseEnum(dto.arrangement, PatternArrangement.Single);
            s.Tier = ParseEnum(dto.tier, PatternTier.Tier1);
            s.FullFoam = ParseEnum(dto.fullFoam, PatternFullFoam.Yes);
            s.Face = ParseEnum(dto.face, PatternFace.Exterior);
            s.Placement = ParseEnum(dto.placement, PatternPlacement.External);
            s.Run = ParseEnum(dto.run, PatternRun.InternalChannel);
            PatternCatalog.Normalize(s);
            return s;
        }

        private static Dto ToDto(PatternSelection s)
        {
            return new Dto
            {
                rememberLastUsed = s.RememberLastUsed,
                surface = s.Surface.ToString(),
                compliance = s.Compliance.ToString(),
                arrangement = s.Arrangement.ToString(),
                tier = s.Tier.ToString(),
                fullFoam = s.FullFoam.ToString(),
                face = s.Face.ToString(),
                placement = s.Placement.ToString(),
                run = s.Run.ToString(),
                deckThickness = s.DeckThickness,
                deckChannelSpacing = s.DeckChannelSpacing,
            };
        }

        private static T ParseEnum<T>(string raw, T fallback) where T : struct
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            return Enum.TryParse(raw, true, out T value) ? value : fallback;
        }
    }
}
