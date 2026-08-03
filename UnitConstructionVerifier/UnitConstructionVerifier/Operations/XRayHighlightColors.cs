using System;
using System.Globalization;
using System.IO;

namespace UnitConstructionVerifier.Operations
{
    public sealed class XRayHighlightColor
    {
        public XRayHighlightColor(string name, byte r, byte g, byte b)
        {
            Name = name;
            R = r;
            G = g;
            B = b;
        }

        public string Name { get; }
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
    }

    /// <summary>
    /// Wireframe color options aligned with the Highlighter add-in palette.
    /// </summary>
    internal static class XRayHighlightColors
    {
        public static readonly XRayHighlightColor[] Options =
        {
            new XRayHighlightColor("Yellow", 255, 220, 0),
            new XRayHighlightColor("Orange", 255, 140, 0),
            new XRayHighlightColor("Magenta", 255, 0, 200),
            new XRayHighlightColor("Cyan", 0, 220, 255),
            new XRayHighlightColor("Lime", 80, 255, 40),
            new XRayHighlightColor("Blue", 40, 120, 255),
            new XRayHighlightColor("White", 255, 255, 255),
            new XRayHighlightColor("Red", 255, 50, 50),
        };

        public static XRayHighlightColor Default => Options[1];

        public static XRayHighlightColor Find(byte r, byte g, byte b)
        {
            foreach (XRayHighlightColor option in Options)
            {
                if (option.R == r && option.G == g && option.B == b)
                {
                    return option;
                }
            }

            return new XRayHighlightColor("Custom", r, g, b);
        }
    }

    internal static class XRayColorSettings
    {
        private static string SettingsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UnitConstructionVerifier",
                "xray_color.txt");

        public static bool TryLoad(out byte r, out byte g, out byte b)
        {
            r = XRayHighlightColors.Default.R;
            g = XRayHighlightColors.Default.G;
            b = XRayHighlightColors.Default.B;

            try
            {
                string path = SettingsPath;
                if (!File.Exists(path))
                {
                    return false;
                }

                string line = File.ReadAllText(path).Trim();
                string[] rgb = line.Split(',');
                if (rgb.Length < 3)
                {
                    return false;
                }

                if (!byte.TryParse(rgb[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out r)
                    || !byte.TryParse(rgb[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out g)
                    || !byte.TryParse(rgb[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out b))
                {
                    r = XRayHighlightColors.Default.R;
                    g = XRayHighlightColors.Default.G;
                    b = XRayHighlightColors.Default.B;
                    return false;
                }

                return true;
            }
            catch
            {
                r = XRayHighlightColors.Default.R;
                g = XRayHighlightColors.Default.G;
                b = XRayHighlightColors.Default.B;
                return false;
            }
        }

        public static void Save(byte r, byte g, byte b)
        {
            try
            {
                string path = SettingsPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(
                    path,
                    string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", r, g, b));
            }
            catch
            {
            }
        }
    }

    internal static class PreviewWireframeColorSettings
    {
        private static string SettingsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UnitConstructionVerifier",
                "preview_wireframe_color.txt");

        public static bool TryLoad(out byte r, out byte g, out byte b)
        {
            r = XRayHighlightColors.Default.R;
            g = XRayHighlightColors.Default.G;
            b = XRayHighlightColors.Default.B;

            try
            {
                string path = SettingsPath;
                if (!File.Exists(path))
                {
                    return false;
                }

                string line = File.ReadAllText(path).Trim();
                string[] rgb = line.Split(',');
                if (rgb.Length < 3)
                {
                    return false;
                }

                if (!byte.TryParse(rgb[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out r)
                    || !byte.TryParse(rgb[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out g)
                    || !byte.TryParse(rgb[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out b))
                {
                    r = XRayHighlightColors.Default.R;
                    g = XRayHighlightColors.Default.G;
                    b = XRayHighlightColors.Default.B;
                    return false;
                }

                return true;
            }
            catch
            {
                r = XRayHighlightColors.Default.R;
                g = XRayHighlightColors.Default.G;
                b = XRayHighlightColors.Default.B;
                return false;
            }
        }

        public static void Save(byte r, byte g, byte b)
        {
            try
            {
                string path = SettingsPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(
                    path,
                    string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", r, g, b));
            }
            catch
            {
            }
        }
    }
}
