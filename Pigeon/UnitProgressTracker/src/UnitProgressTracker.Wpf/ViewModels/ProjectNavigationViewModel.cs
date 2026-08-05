using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UnitProgressTracker.Wpf.ViewModels;

public class ProjectNavigationViewModel : INotifyPropertyChanged
{
    private string? _currentProjectPath;
    private string? _sourceFolder;
    private bool _isProjectLoaded;

    public string? CurrentProjectPath
    {
        get => _currentProjectPath;
        set 
        { 
            _currentProjectPath = value; 
            OnPropertyChanged(); 
            IsProjectLoaded = !string.IsNullOrEmpty(value);
        }
    }

    public string? SourceFolder
    {
        get => _sourceFolder;
        set { _sourceFolder = value; OnPropertyChanged(); }
    }

    public bool IsProjectLoaded
    {
        get => _isProjectLoaded;
        set { _isProjectLoaded = value; OnPropertyChanged(); }
    }

    public ObservableCollection<RecentProjectItemViewModel> RecentProjects { get; } = new();

    public void AddRecentProject(string path, DateTime lastOpened)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        // Remove duplicate if already present
        for (int i = RecentProjects.Count - 1; i >= 0; i--)
        {
            if (string.Equals(RecentProjects[i].FilePath, path, StringComparison.OrdinalIgnoreCase))
            {
                RecentProjects.RemoveAt(i);
            }
        }

        RecentProjects.Insert(0, new RecentProjectItemViewModel(path));

        // Limit list to 10 recent items
        while (RecentProjects.Count > 10)
        {
            RecentProjects.RemoveAt(RecentProjects.Count - 1);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
