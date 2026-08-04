namespace UnitProgressTracker.Core.Models;

public record ProgressReport(
    int Scanned = 0,
    int Total = 0,
    string CurrentFile = "",
    string StatusMessage = ""
)
{
    public double Percent => Total > 0 ? (double)Scanned / Total * 100.0 : 0.0;
}
