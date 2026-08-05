using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Wpf.ViewModels;

public class BomFilterViewModel : INotifyPropertyChanged
{
    private string _searchQuery = string.Empty;
    private string? _selectedStatusStateId;
    private bool _showHiddenSurfaces = true;
    private bool _showVisibleSurfaces = true;

    public string SearchQuery
    {
        get => _searchQuery;
        set { _searchQuery = value ?? string.Empty; OnPropertyChanged(); OnFilterChanged(); }
    }

    public string? SelectedStatusStateId
    {
        get => _selectedStatusStateId;
        set { _selectedStatusStateId = value; OnPropertyChanged(); OnFilterChanged(); }
    }

    public bool ShowHiddenSurfaces
    {
        get => _showHiddenSurfaces;
        set { _showHiddenSurfaces = value; OnPropertyChanged(); OnFilterChanged(); }
    }

    public bool ShowVisibleSurfaces
    {
        get => _showVisibleSurfaces;
        set { _showVisibleSurfaces = value; OnPropertyChanged(); OnFilterChanged(); }
    }

    public bool Matches(SurfaceModel surface)
    {
        if (surface == null) return false;

        // Visibility filter
        if (surface.IsHidden && !ShowHiddenSurfaces) return false;
        if (!surface.IsHidden && !ShowVisibleSurfaces) return false;

        // Status State filter
        if (!string.IsNullOrEmpty(SelectedStatusStateId) &&
            !string.Equals(surface.StateId, SelectedStatusStateId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Text Search filter across surface number, part number, type, side, and notes
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string query = SearchQuery.Trim();
            bool matchesText = (surface.SurfaceNumber?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                               (surface.PartNumber?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                               (surface.SurfaceType?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                               (surface.SurfaceUnitSide?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                               (surface.Notes?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);

            if (!matchesText) return false;
        }

        return true;
    }

    public event EventHandler? FilterChanged;
    private void OnFilterChanged() => FilterChanged?.Invoke(this, EventArgs.Empty);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
