using System;

namespace UnitProgressTracker.Core.Models;

public class RetiredSurfaceRecordModel
{
    public DateTime RetiredAt { get; set; } = DateTime.UtcNow;
    public string? SupersededBy { get; set; }
    public string TransferType { get; set; } = "renumber"; // "renumber", "missing", "replaced", "removed"
    public string? FileKey { get; set; }
    public string? GeometryFingerprint { get; set; }
    public SurfaceRecordModel? Snapshot { get; set; }
}
