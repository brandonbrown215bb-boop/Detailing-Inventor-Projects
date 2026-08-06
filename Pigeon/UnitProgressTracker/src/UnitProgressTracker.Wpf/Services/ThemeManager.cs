using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Wpf.Services
{
    public class ThemeManager
    {
        private static readonly Lazy<ThemeManager> _instance = new Lazy<ThemeManager>(() => new ThemeManager());
        public static ThemeManager Instance => _instance.Value;

        private ThemeOptions _currentOptions = new ThemeOptions();
        private bool _isSubscribed = false;

        private ThemeManager() { }

        public void Initialize(ThemeOptions options)
        {
            _currentOptions = options ?? new ThemeOptions();

            if (!_isSubscribed)
            {
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
                SystemParameters.StaticPropertyChanged += OnSystemParametersPropertyChanged;
                _isSubscribed = true;
            }

            ApplyTheme(_currentOptions);
        }

        public void ApplyTheme(ThemeOptions options)
        {
            if (options != null)
            {
                _currentOptions = options;
            }

            string themeFile = "Themes/DarkTheme.xaml";

            if (_currentOptions.HighContrastOverride || SystemParameters.HighContrast)
            {
                themeFile = "Themes/HighContrastTheme.xaml";
            }
            else if (_currentOptions.AutoSyncWithSystemTheme)
            {
                bool isSystemLight = IsSystemLightMode();
                themeFile = isSystemLight ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml";
            }
            else
            {
                switch (_currentOptions.ThemeName?.ToLowerInvariant())
                {
                    case "light":
                        themeFile = "Themes/LightTheme.xaml";
                        break;
                    case "highcontrast":
                        themeFile = "Themes/HighContrastTheme.xaml";
                        break;
                    case "dark":
                    default:
                        themeFile = "Themes/DarkTheme.xaml";
                        break;
                }
            }

            SwapThemeDictionary(themeFile);
        }

        private void SwapThemeDictionary(string relativeUri)
        {
            var appResources = Application.Current?.Resources;
            if (appResources == null) return;

            var merged = appResources.MergedDictionaries;
            var oldTheme = merged.FirstOrDefault(d => d.Source != null && (
                d.Source.OriginalString.Contains("DarkTheme.xaml") ||
                d.Source.OriginalString.Contains("LightTheme.xaml") ||
                d.Source.OriginalString.Contains("HighContrastTheme.xaml")));

            var newThemeDict = new ResourceDictionary
            {
                Source = new Uri(relativeUri, UriKind.Relative)
            };

            if (oldTheme != null)
            {
                int index = merged.IndexOf(oldTheme);
                merged.RemoveAt(index);
                merged.Insert(index, newThemeDict);
            }
            else
            {
                merged.Add(newThemeDict);
            }
        }

        private bool IsSystemLightMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("AppsUseLightTheme");
                        if (val is int intVal)
                        {
                            return intVal != 0;
                        }
                    }
                }
            }
            catch
            {
                // Fallback to dark if registry fails
            }
            return false;
        }

        private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (_currentOptions.AutoSyncWithSystemTheme && Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() => ApplyTheme(_currentOptions));
            }
        }

        private void OnSystemParametersPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast) && Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() => ApplyTheme(_currentOptions));
            }
        }
    }
}
