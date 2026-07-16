using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Highlighter.Core
{
    public sealed class HighlightRgb
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public string Name { get; set; }

        public HighlightRgb(string name, byte r, byte g, byte b)
        {
            Name = name;
            R = r;
            G = g;
            B = b;
        }
    }

    internal static class HighlightColorPalette
    {
        public static readonly HighlightRgb[] Options =
        {
            new HighlightRgb("Yellow", 255, 220, 0),
            new HighlightRgb("Orange", 255, 140, 0),
            new HighlightRgb("Magenta", 255, 0, 200),
            new HighlightRgb("Cyan", 0, 220, 255),
            new HighlightRgb("Lime", 80, 255, 40),
            new HighlightRgb("Blue", 40, 120, 255),
            new HighlightRgb("White", 255, 255, 255),
            new HighlightRgb("Red", 255, 50, 50),
        };

        public static HighlightRgb DefaultFor(HighlightPartType type)
        {
            switch (type)
            {
                // AutoCAD-familiar scheme: skins red, liners yellow, floor lime, subfloor magenta.
                case HighlightPartType.WallSkin: return Options[7];      // Red
                case HighlightPartType.RoofSkin: return Options[7];      // Red
                case HighlightPartType.WallLiner: return Options[0];     // Yellow
                case HighlightPartType.RoofLiner: return Options[0];     // Yellow
                case HighlightPartType.BaseFloor: return Options[4];     // Lime
                case HighlightPartType.BaseSubfloor: return Options[2];  // Magenta
                default: return Options[0];
            }
        }

        public static string FindName(byte r, byte g, byte b)
        {
            foreach (HighlightRgb opt in Options)
            {
                if (opt.R == r && opt.G == g && opt.B == b)
                {
                    return opt.Name;
                }
            }

            return "Custom";
        }
    }

    /// <summary>
    /// Persists per-type highlight colors under %APPDATA%\Highlighter\colors.txt
    /// </summary>
    internal static class HighlightSettingsStore
    {
        private static string SettingsPath
        {
            get
            {
                string root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Highlighter");
                Directory.CreateDirectory(root);
                return Path.Combine(root, "colors.txt");
            }
        }

        public static Dictionary<HighlightPartType, HighlightRgb> LoadOrDefaults()
        {
            var map = new Dictionary<HighlightPartType, HighlightRgb>();
            foreach (HighlightPartType type in Enum.GetValues(typeof(HighlightPartType)))
            {
                map[type] = HighlightColorPalette.DefaultFor(type);
            }

            try
            {
                string path = SettingsPath;
                if (!File.Exists(path))
                {
                    return map;
                }

                foreach (string line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    {
                        continue;
                    }

                    string[] parts = line.Split('=');
                    if (parts.Length != 2)
                    {
                        continue;
                    }

                    if (!Enum.TryParse(parts[0].Trim(), true, out HighlightPartType type))
                    {
                        continue;
                    }

                    string[] rgb = parts[1].Split(',');
                    if (rgb.Length < 3)
                    {
                        continue;
                    }

                    if (!byte.TryParse(rgb[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r)
                        || !byte.TryParse(rgb[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g)
                        || !byte.TryParse(rgb[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b))
                    {
                        continue;
                    }

                    string name = HighlightColorPalette.FindName(r, g, b);
                    map[type] = new HighlightRgb(name, r, g, b);
                }
            }
            catch
            {
            }

            return map;
        }

        public static void Save(IReadOnlyDictionary<HighlightPartType, HighlightRgb> colors)
        {
            if (colors == null)
            {
                return;
            }

            try
            {
                var lines = new List<string>
                {
                    "# Highlighter per-type colors (R,G,B)"
                };

                foreach (HighlightPartType type in Enum.GetValues(typeof(HighlightPartType)))
                {
                    if (!colors.TryGetValue(type, out HighlightRgb rgb) || rgb == null)
                    {
                        rgb = HighlightColorPalette.DefaultFor(type);
                    }

                    lines.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}={1},{2},{3}",
                        type,
                        rgb.R,
                        rgb.G,
                        rgb.B));
                }

                File.WriteAllLines(SettingsPath, lines);
            }
            catch
            {
            }
        }
    }
}
