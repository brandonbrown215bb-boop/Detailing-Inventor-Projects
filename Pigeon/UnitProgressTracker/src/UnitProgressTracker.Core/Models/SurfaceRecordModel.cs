using System;
using System.Collections.Generic;

namespace UnitProgressTracker.Core.Models;

public class SurfaceRecordModel
{
    public string? StateId { get; set; } = "current";
    public Dictionary<string, bool> Checklist { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Notes { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public bool Hidden { get; set; }
    public string? DisplayNumber { get; set; }
    public List<string> PreviousNumbers { get; set; } = new();
    public string? GeometryFingerprint { get; set; }

    public SurfaceRecordModel Clone()
    {
        return new SurfaceRecordModel
        {
            StateId = StateId,
            Checklist = new Dictionary<string, bool>(Checklist, StringComparer.OrdinalIgnoreCase),
            Notes = Notes,
            UpdatedAt = UpdatedAt,
            Hidden = Hidden,
            DisplayNumber = DisplayNumber,
            PreviousNumbers = new List<string>(PreviousNumbers),
            GeometryFingerprint = GeometryFingerprint
        };
    }
}
