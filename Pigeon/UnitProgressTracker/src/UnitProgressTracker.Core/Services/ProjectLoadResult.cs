using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public enum ProjectLoadFailureKind
{
    None,
    MissingOrInaccessibleFile,
    CorruptJson,
    LegacyPigeonVersion,
    LegacyEsmundVersion,
    NewerVersion,
    UnsupportedFormat,
    IncompleteProject,
    MissingRequiredGeometry
}

public sealed class ProjectLoadResult
{
    public bool Success => Project != null && FailureKind == ProjectLoadFailureKind.None;
    public ProjectStateModel? Project { get; init; }
    public ProjectLoadFailureKind FailureKind { get; init; }
    public string ActionableMessage { get; init; } = string.Empty;

    public static ProjectLoadResult Loaded(ProjectStateModel project)
        => new() { Project = project };

    public static ProjectLoadResult Failed(ProjectLoadFailureKind kind, string message)
        => new() { FailureKind = kind, ActionableMessage = message };
}
