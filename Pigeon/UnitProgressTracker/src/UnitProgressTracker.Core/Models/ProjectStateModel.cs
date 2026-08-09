using System;
using System.Collections.Generic;
using UnitProgressTracker.Core.Services;

namespace UnitProgressTracker.Core.Models;

public class ProjectStateModel
{
    public const string FormatId = "Pigeon.UnitProgressTracker.Project";
    public const int CurrentVersion = 4;

    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = CurrentVersion;
    public string? SourceFolder { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Dictionary<string, SurfaceModel> Geometry { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, SurfaceRecordModel> Surfaces { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, RetiredSurfaceRecordModel> Retired { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<StatusState> StatusDefinitions { get; set; } = new();
    public List<GeometryIntrusionFlagModel> IntrusionFlags { get; set; } = new();
    public CameraStateModel Camera { get; set; } = new();
    public BomImportResult? Bom { get; set; }
    public UnitConfigModel? UnitConfig { get; set; }
    public DisplayPreferences Preferences { get; set; } = new();
}
