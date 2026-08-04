using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

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

        string dir = Path.GetDirectoryName(Path.GetFullPath(filePath))
            ?? throw new InvalidOperationException($"Invalid directory for path: {filePath}");
        Directory.CreateDirectory(dir);

        string tempPath = $"{filePath}.tmp.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}";

        try
        {
            string json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(tempPath, json);

            int retries = 5;
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
                    Thread.Sleep(50);
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
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
        {
            return default;
        }
    }
}
