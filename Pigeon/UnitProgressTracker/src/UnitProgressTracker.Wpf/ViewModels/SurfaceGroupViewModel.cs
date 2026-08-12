using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    private bool? _isGroupVisible = true;

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

    public bool? IsGroupVisible
    {
        get => _isGroupVisible;
        set { _isGroupVisible = value; OnPropertyChanged(); }
    }

    public ObservableCollection<SurfaceModel> Surfaces { get; } = new();

    public int SurfaceCount => Surfaces.Count;

    public string ToggleButtonText => IsGroupVisible switch
    {
        true => "Hide All",
        false => "Show All",
        null => "Mixed - Show All"
    };

    public ICommand ToggleGroupVisibilityCommand { get; }

    public event Action<SurfaceGroupViewModel>? GroupVisibilityToggled;

    public SurfaceGroupViewModel()
    {
        Surfaces.CollectionChanged += OnSurfacesCollectionChanged;
        ToggleGroupVisibilityCommand = new RelayCommand(_ =>
        {
            bool hideTarget = IsGroupVisible == true;

            foreach (var surface in Surfaces)
            {
                surface.IsHidden = hideTarget;
            }

            RefreshVisibilityState();
            GroupVisibilityToggled?.Invoke(this);
        });
    }

    private void OnSurfacesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (SurfaceModel surface in e.OldItems)
                surface.PropertyChanged -= OnSurfacePropertyChanged;
        }

        if (e.NewItems != null)
        {
            foreach (SurfaceModel surface in e.NewItems)
                surface.PropertyChanged += OnSurfacePropertyChanged;
        }

        OnPropertyChanged(nameof(SurfaceCount));
        RefreshVisibilityState();
    }

    private void OnSurfacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SurfaceModel.IsHidden))
            RefreshVisibilityState();
    }

    private void RefreshVisibilityState()
    {
        bool? state = Surfaces.Count == 0
            ? true
            : Surfaces.All(surface => !surface.IsHidden)
                ? true
                : Surfaces.All(surface => surface.IsHidden)
                    ? false
                    : null;

        if (_isGroupVisible != state)
        {
            _isGroupVisible = state;
            OnPropertyChanged(nameof(IsGroupVisible));
        }

        OnPropertyChanged(nameof(ToggleButtonText));
    }

    public void Detach()
    {
        Surfaces.CollectionChanged -= OnSurfacesCollectionChanged;
        foreach (var surface in Surfaces)
            surface.PropertyChanged -= OnSurfacePropertyChanged;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
