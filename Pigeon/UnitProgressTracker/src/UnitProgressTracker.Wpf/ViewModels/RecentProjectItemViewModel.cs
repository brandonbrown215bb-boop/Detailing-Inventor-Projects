using System.IO;

namespace UnitProgressTracker.Wpf.ViewModels;

public class RecentProjectItemViewModel
{
    public string FilePath { get; }
    public string DisplayName => Path.GetFileName(FilePath);
    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;

    public RecentProjectItemViewModel(string filePath)
    {
        FilePath = filePath;
    }
}
