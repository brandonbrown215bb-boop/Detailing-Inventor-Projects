using System.Collections.Generic;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public enum ScanFailureKind
{
    None,
    InaccessibleFolder,
    JsonParseFailure,
    MissingGeometry,
    InventorComFailure,
    DuplicateIdentity,
    FileReadFailure
}

public sealed class ScanFileDiagnostic
{
    public string FileIdentifier { get; init; } = string.Empty;
    public ScanFailureKind Kind { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class GeometryScanResult
{
    public List<SurfaceModel> AcceptedSurfaces { get; init; } = new();
    public List<ScanFileDiagnostic> SkippedFiles { get; init; } = new();
    public List<ScanFileDiagnostic> FailedFiles { get; init; } = new();
    public int DiscoveredFileCount { get; init; }
    public ScanFailureKind FatalFailureKind { get; init; }
    public bool HasFatalFailure => FatalFailureKind != ScanFailureKind.None;
    public string Summary { get; init; } = string.Empty;
}
