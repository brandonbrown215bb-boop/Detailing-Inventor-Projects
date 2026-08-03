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
    public int SkidId { get; set; } = 1;
    public string StateId { get; set; } = "current";
    public string Notes { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public Dictionary<string, bool> Checklist { get; set; } = new();
    public List<GeometryBox> Boxes { get; set; } = new();

    public string ShortLabel
    {
        get
        {
            if (string.IsNullOrEmpty(SurfaceNumber)) return "0000";
            int dashIndex = SurfaceNumber.LastIndexOf('-');
            string suffix = dashIndex >= 0 ? SurfaceNumber[(dashIndex + 1)..] : SurfaceNumber;
            return suffix.Length <= 4 ? suffix.PadLeft(4, '0') : suffix[^4..];
        }
    }
}
