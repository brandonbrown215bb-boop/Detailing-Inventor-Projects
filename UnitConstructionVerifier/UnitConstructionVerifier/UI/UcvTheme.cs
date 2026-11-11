using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace UnitConstructionVerifier.UI
{
    public enum ThemeChoice { System, Light, Dark }

    /// <summary>
    /// UCV's light and dark palettes. The choice is saved per Windows user; System follows the Windows app theme
    /// (Settings › Personalization › Colors) and repaints when it changes. Views reference the palette keys as
    /// DynamicResource so a switch repaints them in place.
    /// </summary>
    public static class UcvTheme
    {
        private static readonly (object Key, string Dark, string Light)[] Palette =
        {
            ("BgPrimary",     "#191B22", "#EEF0F4"),
            ("BgSurface",     "#20232B", "#F7F8FA"),
            ("BgCard",        "#262A33", "#FFFFFF"),
            ("BgInput",       "#15171D", "#FFFFFF"),
            ("BgHover",       "#2A2F3B", "#E4E7EE"),
            ("BgSelected",    "#2D3550", "#D9E2FA"),
            ("BorderColor",   "#343946", "#D5D9E2"),
            ("BorderStrong",  "#4A5163", "#AEB4C2"),
            ("TextPrimary",   "#E7E9EF", "#1B1E26"),
            ("TextSecondary", "#98A0B1", "#5B6272"),
            ("TextOnAccent",  "#12141B", "#FFFFFF"),
            ("AccentBlue",    "#7C9EFF", "#2F5BD3"),
            ("AccentGreen",   "#46C083", "#1E8A55"),
            ("AccentRed",     "#F06872", "#C93441"),
            ("AccentOrange",  "#E8A33D", "#B86E00"),
            ("AccentPending", "#D9B44A", "#9A7400"),
            ("FailTint",      "#3A2228", "#FBE3E5"),
            ("PassTint",      "#1E3529", "#DDF2E6"),
            ("PendingTint",   "#3A3218", "#FBF0D2"),
            ("MutedTint",     "#2B2F39", "#E6E9EF"),
            // Default control templates that fall back to system colours. No InactiveSelectionHighlight*BrushKey
            // entries: Inventor runs WPF in .NET 4.0 compatibility, where those keys equal ControlBrushKey/ControlTextBrushKey.
            (SystemColors.ControlBrushKey,       "#20232B", "#F7F8FA"),
            (SystemColors.ControlTextBrushKey,   "#E7E9EF", "#1B1E26"),
            (SystemColors.HighlightBrushKey,     "#2D3550", "#D9E2FA"),
            (SystemColors.HighlightTextBrushKey, "#E7E9EF", "#1B1E26"),
        };

        private const string AppliedKey = "UcvTheme.IsDark";

        private static string SettingsPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UnitConstructionVerifier", "theme.txt");

        private static ThemeChoice? _choice;

        /// <summary>Raised on the setting thread when <see cref="Choice"/> changes.</summary>
        public static event Action? Changed;

        /// <summary>The user's choice, loaded once from AppData and saved on every change.</summary>
        public static ThemeChoice Choice
        {
            get => _choice ??= Load();
            set
            {
                if (_choice == value) return;
                _choice = value;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                    File.WriteAllText(SettingsPath, value.ToString());
                }
                catch { /* not remembered next time; the switch still applies now */ }
                Changed?.Invoke();
            }
        }

        public static bool IsDark => Resolve(Choice, SystemUsesLightTheme());

        public static bool Resolve(ThemeChoice choice, bool systemUsesLight)
            => choice == ThemeChoice.Dark || (choice == ThemeChoice.System && !systemUsesLight);

        public static ThemeChoice Next(ThemeChoice choice) => (ThemeChoice)(((int)choice + 1) % 3);

        public static ThemeChoice Parse(string? text)
            => Enum.TryParse(text?.Trim(), true, out ThemeChoice c) && Enum.IsDefined(typeof(ThemeChoice), c) ? c : ThemeChoice.System;

        private static ThemeChoice Load()
        {
            try { return File.Exists(SettingsPath) ? Parse(File.ReadAllText(SettingsPath)) : ThemeChoice.System; }
            catch { return ThemeChoice.System; }
        }

        private static bool SystemUsesLightTheme()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
            }
            catch { return false; }
        }

        /// <summary>Puts the current palette into <paramref name="resources"/>; does nothing if it is already there.</summary>
        public static void Apply(ResourceDictionary resources)
        {
            bool dark = IsDark;
            if (resources[AppliedKey] is bool applied && applied == dark) return;

            foreach (var (key, darkHex, lightHex) in Palette)
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? darkHex : lightHex));
                brush.Freeze();
                resources[key] = brush;
            }
            resources[AppliedKey] = dark;
        }

        /// <summary>Paints <paramref name="element"/> now and whenever the choice or the Windows theme changes. Dispose to stop.</summary>
        public static IDisposable Track(FrameworkElement element)
        {
            Action repaint = () => element.Dispatcher.BeginInvoke((Action)(() => Apply(element.Resources)));
            UserPreferenceChangedEventHandler onSystem = (_, e) =>
            {
                if (e.Category == UserPreferenceCategory.General) repaint();
            };

            Apply(element.Resources);
            Changed += repaint;
            SystemEvents.UserPreferenceChanged += onSystem;
            return new Subscription(() =>
            {
                Changed -= repaint;
                SystemEvents.UserPreferenceChanged -= onSystem;
            });
        }

        private sealed class Subscription : IDisposable
        {
            private Action? _undo;
            public Subscription(Action undo) => _undo = undo;
            public void Dispose() { _undo?.Invoke(); _undo = null; }
        }
    }
}
