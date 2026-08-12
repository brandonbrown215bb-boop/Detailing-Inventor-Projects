using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UnitProgressTracker.Core.Models;

public class ListDisplayOptions
{
    public string GroupMode { get; set; } = "skid"; // "skid", "type", "flat"
    public string NameMode { get; set; } = "both";  // "both", "long", "short"
    public string SortMode { get; set; } = "default"; // "default", "skid", "type", "skid-type"
    public bool ShowTypeTag { get; set; } = true;
    public bool ShowSkidTag { get; set; } = true;
    public bool ShowSideTag { get; set; } = true;
}

public class ViewerOptions
{
    public bool ShowGrid { get; set; } = true;
    public bool ShowSkidLabels { get; set; } = true;
    public bool ShowLegend { get; set; } = true;
    public bool ShowHoverTooltip { get; set; } = true;
    public bool FpsControlsEnabled { get; set; } = true;
    public double SurfaceOpacity { get; set; } = 0.9;
    public bool WireframeVisible { get; set; } = true;
}

public class StickerOptions
{
    public string FontFamily { get; set; } = "Segoe UI";
    public string TextColorHex { get; set; } = "#F8FAFC";
    public string BackgroundColorHex { get; set; } = "#0F172A";
    public string BorderColorHex { get; set; } = "#94A3B8";
}

public class ThemeOptions
{
    public string ThemeName { get; set; } = "Dark";
    public string AccentColorHex { get; set; } = "#38BDF8";
    public string PanelBackgroundHex { get; set; } = "#1E293B";
    public bool AutoSyncWithSystemTheme { get; set; } = true;
    public double UiFontScale { get; set; } = 1.0;
    public bool HighVisibilityFocus { get; set; } = true;
    public bool HighContrastOverride { get; set; } = false;
}

public class DisplayPreferences
{
    public ListDisplayOptions ListDisplay { get; set; } = new();
    public ViewerOptions ViewerOptions { get; set; } = new();
    public StickerOptions StickerOptions { get; set; } = new();
    // Theme/accessibility belongs to AppSettings. Keep this compatibility shim out
    // of the portable project payload so older callers do not leak user settings.
    [JsonIgnore]
    public ThemeOptions ThemeOptions { get; set; } = new();
    public List<string> ChecklistTemplate { get; set; } = new()
    {
        "Verified dimensions",
        "Verified material",
        "Verified openings",
        "Paperwork complete"
    };

    public DisplayPreferences Clone() => new()
    {
        ListDisplay = new ListDisplayOptions
        {
            GroupMode = ListDisplay.GroupMode,
            NameMode = ListDisplay.NameMode,
            SortMode = ListDisplay.SortMode,
            ShowTypeTag = ListDisplay.ShowTypeTag,
            ShowSkidTag = ListDisplay.ShowSkidTag,
            ShowSideTag = ListDisplay.ShowSideTag
        },
        ViewerOptions = new ViewerOptions
        {
            ShowGrid = ViewerOptions.ShowGrid,
            ShowSkidLabels = ViewerOptions.ShowSkidLabels,
            ShowLegend = ViewerOptions.ShowLegend,
            ShowHoverTooltip = ViewerOptions.ShowHoverTooltip,
            FpsControlsEnabled = ViewerOptions.FpsControlsEnabled,
            SurfaceOpacity = ViewerOptions.SurfaceOpacity,
            WireframeVisible = ViewerOptions.WireframeVisible
        },
        StickerOptions = new StickerOptions
        {
            FontFamily = StickerOptions.FontFamily,
            TextColorHex = StickerOptions.TextColorHex,
            BackgroundColorHex = StickerOptions.BackgroundColorHex,
            BorderColorHex = StickerOptions.BorderColorHex
        },
        ChecklistTemplate = new List<string>(ChecklistTemplate)
    };
}

public static class ThemeOptionsExtensions
{
    public static ThemeOptions Clone(this ThemeOptions options) => new()
    {
        ThemeName = options.ThemeName,
        AccentColorHex = options.AccentColorHex,
        PanelBackgroundHex = options.PanelBackgroundHex,
        AutoSyncWithSystemTheme = options.AutoSyncWithSystemTheme,
        UiFontScale = options.UiFontScale,
        HighVisibilityFocus = options.HighVisibilityFocus,
        HighContrastOverride = options.HighContrastOverride
    };
}
