using System;
using System.Collections.Generic;
using UnitProgressTracker.Core.Services;

namespace UnitProgressTracker.Core.Models;

public class ProjectStateModel
{
    public int Version { get; set; } = 2;
    public string? SourceFolder { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Dictionary<string, SurfaceRecordModel> Surfaces { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, RetiredSurfaceRecordModel> Retired { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public BomImportResult? Bom { get; set; }
    public UnitConfigModel? UnitConfig { get; set; }
    public DisplayPreferences Preferences { get; set; } = new();
}
