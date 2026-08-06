using System.Collections.Generic;

namespace UnitProgressTracker.Core.Models;

public class SurfaceModel
{
    public string SurfaceNumber { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string SourceType { get; set; } = "json";
    public string PartNumber { get; set; } = string.Empty;
    public string SurfaceType { get; set; } = string.Empty;
    public string SurfaceUnitSide { get; set; } = string.Empty;
    public string ConfigurationKind { get; set; } = string.Empty;
    public string SkidNumber { get; set; } = string.Empty;
    private int _skidId = 1;
    public int SkidId
    {
        get => _skidId <= 0 ? 1 : _skidId;
        set => _skidId = value <= 0 ? 1 : value;
    }
    public string StateId { get; set; } = "current";
    public string Notes { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public Dictionary<string, bool> Checklist { get; set; } = new();
    public List<GeometryBox> Boxes { get; set; } = new();
    public string? DisplayNumber { get; set; }
    public List<string> PreviousNumbers { get; set; } = new();
    public string? GeometryFingerprint { get; set; }

    public string EffectiveDisplayNumber => string.IsNullOrWhiteSpace(DisplayNumber) ? SurfaceNumber : DisplayNumber;

    public string SkidTag => $"S{SkidId}";

    public string TypeTag => string.IsNullOrWhiteSpace(ConfigurationKind)
        ? (string.IsNullOrWhiteSpace(SurfaceType) ? "Surface" : SurfaceType)
        : (ConfigurationKind == "UnitBase" ? "Base" : ConfigurationKind);

    public string SideTag => string.IsNullOrWhiteSpace(SurfaceUnitSide) ? string.Empty : SurfaceUnitSide.Trim();

    public string ShortLabel
    {
        get
        {
            string num = EffectiveDisplayNumber;
            if (string.IsNullOrEmpty(num)) return "0000";
            int dashIndex = num.LastIndexOf('-');
            string suffix = dashIndex >= 0 ? num[(dashIndex + 1)..] : num;
            return suffix.Length <= 4 ? suffix.PadLeft(4, '0') : suffix[^4..];
        }
    }
}
