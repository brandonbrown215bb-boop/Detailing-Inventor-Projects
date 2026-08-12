using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public static class ProjectSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void SaveAtomic<T>(string filePath, T data)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (data is ProjectStateModel project &&
            (project.Format != ProjectStateModel.FormatId || project.Version != ProjectStateModel.CurrentVersion))
        {
            throw new InvalidOperationException(
                $"Project files must use format '{ProjectStateModel.FormatId}' version {ProjectStateModel.CurrentVersion}.");
        }

        string dir = Path.GetDirectoryName(Path.GetFullPath(filePath))
            ?? throw new InvalidOperationException($"Invalid directory for path: {filePath}");
        Directory.CreateDirectory(dir);

        string tempPath = $"{filePath}.tmp.{Environment.ProcessId}.{Guid.NewGuid():N}";

        try
        {
            string json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(tempPath, json);

            int retries = 20;
            while (retries > 0)
            {
                try
                {
                    File.Move(tempPath, filePath, overwrite: true);
                    break;
                }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && retries > 1)
                {
                    retries--;
                    Thread.Sleep(Random.Shared.Next(10, 50));
                }
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* Ignore cleanup errors */ }
            }
        }
    }

    public static ProjectLoadResult LoadProject(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return ProjectLoadResult.Failed(
                ProjectLoadFailureKind.MissingOrInaccessibleFile,
                "The project file is missing or inaccessible. Choose an existing version 4 .uptproj file.");
        }

        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return ProjectLoadResult.Failed(
                ProjectLoadFailureKind.MissingOrInaccessibleFile,
                "The project file could not be read. Check file access and try again.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return ProjectLoadResult.Failed(
                ProjectLoadFailureKind.CorruptJson,
                "The project contains invalid JSON. Restore a known-good version 4 .uptproj file or rescan the source.");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ProjectLoadResult.Failed(
                    ProjectLoadFailureKind.UnsupportedFormat,
                    "The file is not a Unit Progress Tracker project document.");
            }

            int? versionValue = root.TryGetProperty("version", out var version) &&
                                version.ValueKind == JsonValueKind.Number &&
                                version.TryGetInt32(out int parsedVersion)
                ? parsedVersion
                : null;

            if (versionValue == 2)
            {
                return ProjectLoadResult.Failed(
                    ProjectLoadFailureKind.LegacyPigeonVersion,
                    "This is a pre-production Pigeon version 2 file. Open or create a production version 4 project instead; no automatic conversion is supported.");
            }

            if (versionValue == 3)
            {
                return ProjectLoadResult.Failed(
                    ProjectLoadFailureKind.LegacyEsmundVersion,
                    "This is a pre-production Esmund version 3 file. Open or create a production Pigeon version 4 project instead; no automatic conversion is supported.");
            }

            if (versionValue > ProjectStateModel.CurrentVersion)
            {
                return ProjectLoadResult.Failed(
                    ProjectLoadFailureKind.NewerVersion,
                    $"This project uses newer version {versionValue}. Update Unit Progress Tracker before opening it.");
            }

            if (versionValue != ProjectStateModel.CurrentVersion ||
                !root.TryGetProperty("format", out var format) ||
                format.ValueKind != JsonValueKind.String ||
                !string.Equals(format.GetString(), ProjectStateModel.FormatId, StringComparison.Ordinal))
            {
                return ProjectLoadResult.Failed(
                    ProjectLoadFailureKind.UnsupportedFormat,
                    "The file is not a supported Pigeon Unit Progress Tracker version 4 project.");
            }

            string[] requiredProperties =
            {
                "geometry", "surfaces", "retired", "statusDefinitions",
                "intrusionFlags", "camera", "preferences"
            };
            if (requiredProperties.Any(property => !root.TryGetProperty(property, out _)))
            {
                return ProjectLoadResult.Failed(
                    ProjectLoadFailureKind.IncompleteProject,
                    "The version 4 project is incomplete and is missing required project-state sections. The active project was not changed.");
            }
        }

        try
        {
            var project = JsonSerializer.Deserialize<ProjectStateModel>(json, JsonOptions);
            if (project == null || !IsValidProjectState(project))
            {
                return ProjectLoadResult.Failed(
                    ProjectLoadFailureKind.IncompleteProject,
                    "The version 4 project contains incomplete or invalid geometry/tracking state. The active project was not changed.");
            }

            if (project.Surfaces.Any(entry =>
                    !project.Geometry.TryGetValue(entry.Key, out var geometry) ||
                    geometry.Boxes == null ||
                    geometry.Boxes.Count == 0))
            {
                return ProjectLoadResult.Failed(
                    ProjectLoadFailureKind.MissingRequiredGeometry,
                    "The version 4 project is missing required renderable geometry for one or more active surfaces. Reopen a complete project or rescan the source; the active project was not changed.");
            }

            return ProjectLoadResult.Loaded(project);
        }
        catch (Exception ex) when (ex is JsonException || ex is NotSupportedException)
        {
            return ProjectLoadResult.Failed(
                ProjectLoadFailureKind.IncompleteProject,
                "The version 4 project contains values this application cannot read. The active project was not changed.");
        }
    }

    public static T? Load<T>(string filePath)
    {
        if (typeof(T) == typeof(ProjectStateModel))
        {
            return (T?)(object?)LoadProject(filePath).Project;
        }

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return default;

        try
        {
            string json = File.ReadAllText(filePath);
            var loaded = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return loaded;
        }
        catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
        {
            return default;
        }
    }

    private static bool IsValidProjectState(ProjectStateModel project)
    {
        if (project.Geometry == null || project.Surfaces == null || project.Retired == null ||
            project.StatusDefinitions == null || project.IntrusionFlags == null ||
            project.Camera == null || project.Preferences == null ||
            project.Preferences.ChecklistTemplate == null)
        {
            return false;
        }

        if (project.Surfaces.Any(entry =>
                string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null ||
                entry.Value.Checklist == null || entry.Value.PreviousNumbers == null))
        {
            return false;
        }

        if (project.Geometry.Any(entry =>
                string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null ||
                entry.Value.Boxes == null || entry.Value.Checklist == null ||
                entry.Value.PreviousNumbers == null ||
                entry.Value.Boxes.Any(box => box == null || box.XLength <= 0 || box.YLength <= 0 || box.ZLength <= 0)))
        {
            return false;
        }

        if (project.Retired.Values.Any(record => record == null ||
                (record.GeometrySnapshot != null &&
                 (record.GeometrySnapshot.Boxes == null ||
                  record.GeometrySnapshot.Boxes.Any(box => box == null || box.XLength <= 0 || box.YLength <= 0 || box.ZLength <= 0)))))
        {
            return false;
        }

        return project.Bom == null ||
               (project.Bom.AllRows != null && project.Bom.KeptRows != null &&
                project.Bom.DroppedRows != null && project.Bom.KeptCountByPrefix != null);
    }
}
