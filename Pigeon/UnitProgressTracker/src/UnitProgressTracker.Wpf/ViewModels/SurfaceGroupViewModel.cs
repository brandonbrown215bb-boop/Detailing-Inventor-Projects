using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Wpf.ViewModels;

public class SurfaceGroupViewModel : INotifyPropertyChanged
{
    private string _groupKey = string.Empty;
    private string _displayName = string.Empty;
    private bool _isExpanded = true;
    private bool _isGroupVisible = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string GroupKey
    {
        get => _groupKey;
        set { _groupKey = value; OnPropertyChanged(); }
    }

    public string DisplayName
    {
        get => _displayName;
        set { _displayName = value; OnPropertyChanged(); }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public bool IsGroupVisible
    {
        get => _isGroupVisible;
        set { _isGroupVisible = value; OnPropertyChanged(); }
    }

    public ObservableCollection<SurfaceModel> Surfaces { get; } = new();

    public int SurfaceCount => Surfaces.Count;

    public string ToggleButtonText => IsGroupVisible ? "👁 Hide All" : "👁 Show All";

    public ICommand ToggleGroupVisibilityCommand { get; }

    public event Action<SurfaceGroupViewModel>? GroupVisibilityToggled;

    public SurfaceGroupViewModel()
    {
        ToggleGroupVisibilityCommand = new RelayCommand(_ =>
        {
            IsGroupVisible = !IsGroupVisible;
            bool hideTarget = !IsGroupVisible;

            foreach (var surface in Surfaces)
            {
                surface.IsHidden = hideTarget;
            }

            OnPropertyChanged(nameof(ToggleButtonText));
            GroupVisibilityToggled?.Invoke(this);
        });
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
