using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnitProgressTracker.Core.Services;

public class ProjectSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void SaveAtomic<T>(string filePath, T data)
    {
        string dir = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("Invalid path");
        Directory.CreateDirectory(dir);

        string tempPath = $"{filePath}.tmp.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}";
        string json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(tempPath, json);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        File.Move(tempPath, filePath);
    }

    public static T? Load<T>(string filePath)
    {
        if (!File.Exists(filePath)) return default;
        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return default;
        }
    }
}
