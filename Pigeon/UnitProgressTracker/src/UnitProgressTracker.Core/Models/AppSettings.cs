using System.Collections.Generic;

namespace UnitProgressTracker.Core.Models;

public class AppSettings
{
    public List<string> RecentProjects { get; set; } = new();
    public int MaxRecentProjects { get; set; } = 10;
    public string? LastOpenedProject { get; set; }
    public double AutoSaveIntervalMinutes { get; set; } = 5.0;
    public bool AutoSaveEnabled { get; set; } = true;
}
