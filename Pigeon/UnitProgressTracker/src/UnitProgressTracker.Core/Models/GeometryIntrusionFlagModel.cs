using System.Collections.Generic;

namespace UnitProgressTracker.Core.Models;

public class GeometryIntrusionFlagModel
{
    public string SurfaceNumber { get; set; } = string.Empty;
    public List<string> AffectedSurfaceNumbers { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public bool Resolved { get; set; }
}
