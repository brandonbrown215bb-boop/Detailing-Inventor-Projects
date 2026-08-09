using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public static class AppSettingsService
{
    public const string DataRootEnvironmentVariable = "UNIT_PROGRESS_TRACKER_DATA_ROOT";
    private static readonly AsyncLocal<string?> ScopedDataRoot = new();

    public static IDisposable UseDataRoot(string dataRoot)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
            throw new ArgumentException("Data root cannot be null or empty.", nameof(dataRoot));

        string? previous = ScopedDataRoot.Value;
        ScopedDataRoot.Value = Path.GetFullPath(dataRoot);
        return new DataRootScope(previous);
    }

    public static string GetSettingsFilePath()
    {
        string? configuredRoot = ScopedDataRoot.Value
            ?? Environment.GetEnvironmentVariable(DataRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.Combine(Path.GetFullPath(configuredRoot), "UnitProgressTracker", "settings.json");
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "UnitProgressTracker", "settings.json");
    }

    public static AppSettings LoadSettings()
    {
        string filePath = GetSettingsFilePath();
        try
        {
            if (File.Exists(filePath))
            {
                var settings = ProjectSerializer.Load<AppSettings>(filePath);
                if (settings != null)
                {
                    int max = settings.MaxRecentProjects > 0 ? settings.MaxRecentProjects : 10;
                    settings.MaxRecentProjects = max;
                    settings.RecentProjects = settings.RecentProjects
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Select(p => Path.GetFullPath(p))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(max)
                        .ToList();
                    return settings;
                }
            }
        }
        catch
        {
            // Fallback to defaults on read/deserialize error
        }
        return new AppSettings();
    }

    public static void SaveSettings(AppSettings settings)
    {
        try
        {
            string filePath = GetSettingsFilePath();
            ProjectSerializer.SaveAtomic(filePath, settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    public static void AddRecentProject(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath)) return;

        var settings = LoadSettings();
        string fullPath = Path.GetFullPath(projectPath);

        settings.RecentProjects.RemoveAll(p => string.Equals(Path.GetFullPath(p), fullPath, StringComparison.OrdinalIgnoreCase));
        settings.RecentProjects.Insert(0, fullPath);

        if (settings.RecentProjects.Count > settings.MaxRecentProjects)
        {
            settings.RecentProjects = settings.RecentProjects.Take(settings.MaxRecentProjects).ToList();
        }

        settings.LastOpenedProject = fullPath;
        SaveSettings(settings);
    }

    public static void ClearRecentProjects()
    {
        var settings = LoadSettings();
        settings.RecentProjects.Clear();
        settings.LastOpenedProject = null;
        SaveSettings(settings);
    }

    private sealed class DataRootScope : IDisposable
    {
        private readonly string? _previous;
        private bool _disposed;

        public DataRootScope(string? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed) return;
            ScopedDataRoot.Value = _previous;
            _disposed = true;
        }
    }
}
