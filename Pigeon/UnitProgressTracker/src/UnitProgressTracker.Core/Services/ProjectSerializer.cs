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

    public static T? Load<T>(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return default;

        try
        {
            string json = File.ReadAllText(filePath);
            if (typeof(T) == typeof(ProjectStateModel) && !IsSupportedProjectDocument(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
        {
            return default;
        }
    }

    private static bool IsSupportedProjectDocument(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("format", out var format) ||
                format.ValueKind != JsonValueKind.String ||
                !string.Equals(format.GetString(), ProjectStateModel.FormatId, StringComparison.Ordinal) ||
                !root.TryGetProperty("version", out var version) ||
                version.ValueKind != JsonValueKind.Number ||
                !version.TryGetInt32(out int versionValue) ||
                versionValue != ProjectStateModel.CurrentVersion)
            {
                return false;
            }

            string[] requiredProperties =
            {
                "geometry", "surfaces", "retired", "statusDefinitions",
                "intrusionFlags", "camera", "preferences"
            };

            return requiredProperties.All(property => root.TryGetProperty(property, out _));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
