using System;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;

namespace VisTog.UI
{
    /// <summary>Optional freeform rects kept for JSON compatibility; ignored by standard WinForms layout.</summary>
    internal sealed class VisTogLayoutRect
    {
        public int x { get; set; }
        public int y { get; set; }
        public int w { get; set; }
        public int h { get; set; }
    }

    internal sealed class VisTogFreeformLayout
    {
        public VisTogLayoutRect allOn { get; set; }
        public VisTogLayoutRect sharkCage { get; set; }
        public VisTogLayoutRect simple { get; set; }
        public VisTogLayoutRect advanced { get; set; }
        public VisTogLayoutRect tabs { get; set; }
        public VisTogLayoutRect parts { get; set; }
        public VisTogLayoutRect surface { get; set; }
        public VisTogLayoutRect gear { get; set; }
    }

    internal sealed class VisTogUiSettings
    {
        public int width { get; set; } = 180;
        public int height { get; set; } = 0;
        public int pad { get; set; } = 6;
        public int gap { get; set; } = 4;
        public int btnH { get; set; } = 26;
        public int borderW { get; set; } = 1;
        public string fontFamily { get; set; } = "Segoe UI";
        public float fontSize { get; set; } = 9F;
        public string fontWeight { get; set; } = "600";
        public bool sharkShowIcon { get; set; } = true;
        public bool sharkIconOnly { get; set; } = true;
        public VisTogUiColors colors { get; set; } = new VisTogUiColors();
        public VisTogFreeformLayout layout { get; set; }

        private static VisTogUiSettings _cached;
        private const string FileName = "vistog-ui-settings.json";

        public static VisTogUiSettings Current
        {
            get
            {
                if (_cached == null)
                {
                    _cached = Load();
                }

                return _cached;
            }
        }

        public static void Replace(VisTogUiSettings settings)
        {
            _cached = settings ?? new VisTogUiSettings();
            VisTogUiTheme.Apply(_cached);
        }

        public static string GetSettingsPath()
        {
            string dir = Path.GetDirectoryName(typeof(VisTogUiSettings).Assembly.Location) ?? string.Empty;
            return Path.Combine(dir, FileName);
        }

        public static VisTogUiSettings Load()
        {
            try
            {
                string path = GetSettingsPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var serializer = new JavaScriptSerializer();
                    VisTogUiSettings loaded = serializer.Deserialize<VisTogUiSettings>(json);
                    if (loaded != null)
                    {
                        if (loaded.colors == null)
                        {
                            loaded.colors = new VisTogUiColors();
                        }

                        // Freeform layout shelved — always use standard WinForms flow.
                        loaded.layout = null;
                        return loaded;
                    }
                }
            }
            catch
            {
            }

            return FromLayoutExportDefaults();
        }

        public void Save()
        {
            try
            {
                layout = null;
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(this);
                File.WriteAllText(GetSettingsPath(), json);
            }
            catch
            {
            }
        }

        public static VisTogUiSettings FromLayoutExportDefaults()
        {
            return new VisTogUiSettings
            {
                width = 180,
                pad = 6,
                gap = 4,
                btnH = 26,
                borderW = 1,
                fontFamily = "Segoe UI",
                fontSize = 9F,
                fontWeight = "600",
                sharkShowIcon = true,
                sharkIconOnly = true,
                colors = new VisTogUiColors(),
                layout = null
            };
        }
    }

    internal sealed class VisTogUiColors
    {
        public string back { get; set; } = "#2d2d30";
        public string header { get; set; } = "#3e3e42";
        public string accent { get; set; } = "#2274a5";
        public string text { get; set; } = "#f1f1f1";
        public string muted { get; set; } = "#aaaaaa";
        public string border { get; set; } = "#c8c8c8";
        public string btnBorder { get; set; } = "#5a5a5e";
    }

    internal static class VisTogUiTheme
    {
        public static Color Back { get; private set; } = Color.FromArgb(45, 45, 48);
        public static Color Panel { get; private set; } = Color.FromArgb(37, 37, 40);
        public static Color Header { get; private set; } = Color.FromArgb(62, 62, 66);
        public static Color Accent { get; private set; } = Color.FromArgb(34, 116, 165);
        public static Color Text { get; private set; } = Color.FromArgb(241, 241, 241);
        public static Color Muted { get; private set; } = Color.FromArgb(170, 170, 170);
        public static Color Border { get; private set; } = Color.FromArgb(200, 200, 200);
        public static Color ButtonBorder { get; private set; } = Color.FromArgb(90, 90, 94);
        public static Font UiFont { get; private set; } = new Font("Segoe UI", 9F, FontStyle.Bold);
        public static Font HeaderFont { get; private set; } = new Font("Segoe UI", 9F, FontStyle.Bold);
        public static Font PartFont { get; private set; } = new Font("Segoe UI", 9F, FontStyle.Regular);

        public static void Apply(VisTogUiSettings settings)
        {
            if (settings == null)
            {
                settings = VisTogUiSettings.FromLayoutExportDefaults();
            }

            VisTogUiColors c = settings.colors ?? new VisTogUiColors();
            Back = ParseColor(c.back, Back);
            Header = ParseColor(c.header, Header);
            Accent = ParseColor(c.accent, Accent);
            Text = ParseColor(c.text, Text);
            Muted = ParseColor(c.muted, Muted);
            Border = ParseColor(c.border, Border);
            ButtonBorder = ParseColor(c.btnBorder, ButtonBorder);
            Panel = Color.FromArgb(
                Math.Max(0, Back.R - 8),
                Math.Max(0, Back.G - 8),
                Math.Max(0, Back.B - 8));

            string family = string.IsNullOrWhiteSpace(settings.fontFamily) ? "Segoe UI" : settings.fontFamily;
            float size = settings.fontSize > 0 ? settings.fontSize : 9F;
            FontStyle style = string.Equals(settings.fontWeight, "700", StringComparison.Ordinal)
                || string.Equals(settings.fontWeight, "600", StringComparison.Ordinal)
                ? FontStyle.Bold
                : FontStyle.Regular;

            try
            {
                UiFont = new Font(family, size, style);
                HeaderFont = new Font(family, size, FontStyle.Bold);
                PartFont = new Font(family, size, FontStyle.Regular);
            }
            catch
            {
                UiFont = new Font("Segoe UI", size, style);
                HeaderFont = new Font("Segoe UI", size, FontStyle.Bold);
                PartFont = new Font("Segoe UI", size, FontStyle.Regular);
            }
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return fallback;
            }

            try
            {
                return ColorTranslator.FromHtml(hex.Trim());
            }
            catch
            {
                return fallback;
            }
        }
    }
}
