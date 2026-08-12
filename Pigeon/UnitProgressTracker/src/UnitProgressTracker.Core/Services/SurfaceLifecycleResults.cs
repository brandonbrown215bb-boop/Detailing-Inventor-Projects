using System;
using System.Collections.Generic;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public enum SurfaceOperationIssueKind
{
    DuplicateIdentity,
    InvalidGeometry
}

public sealed class SurfaceOperationIssue
{
    public SurfaceOperationIssueKind Kind { get; init; }
    public string SurfaceIdentifier { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class AddSurfacesProposal
{
    public List<SurfaceModel> AcceptedSurfaces { get; init; } = new();
    public List<SurfaceOperationIssue> Issues { get; init; } = new();
    public List<GeometryIntrusionFlagModel> IntrusionFlags { get; init; } = new();
}

public sealed class AddSurfacesApplyResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<SurfaceModel> AddedSurfaces { get; init; } = new();
    public List<GeometryIntrusionFlagModel> IntrusionFlags { get; init; } = new();
}

public sealed class RetireSurfaceResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string RetiredKey { get; init; } = string.Empty;
    public SurfaceModel? RetiredSurface { get; init; }
}

public sealed class RestoreSurfaceResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string RetiredKey { get; init; } = string.Empty;
    public SurfaceModel? RestoredSurface { get; init; }
}
