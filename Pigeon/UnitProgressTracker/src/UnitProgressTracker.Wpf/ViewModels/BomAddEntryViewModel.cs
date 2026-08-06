using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;

namespace UnitProgressTracker.Wpf.ViewModels;

public class BomAddEntryViewModel : INotifyPropertyChanged
{
    private string _partNumber = string.Empty;
    private string _selectedSkid = string.Empty;
    private string _selectedSegment = string.Empty;
    private string _description = string.Empty;
    private string _extDescription = string.Empty;
    private int _quantity = 1;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string PartNumber
    {
        get => _partNumber;
        set { _partNumber = value; OnPropertyChanged(); }
    }

    public string SelectedSkid
    {
        get => _selectedSkid;
        set
        {
            _selectedSkid = value;
            OnPropertyChanged();
            UpdateAvailableSegments();
        }
    }

    public string SelectedSegment
    {
        get => _selectedSegment;
        set { _selectedSegment = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public string ExtDescription
    {
        get => _extDescription;
        set { _extDescription = value; OnPropertyChanged(); }
    }

    public int Quantity
    {
        get => _quantity;
        set { _quantity = Math.Max(1, value); OnPropertyChanged(); }
    }

    public ObservableCollection<string> AvailableSkids { get; } = new();
    public ObservableCollection<string> AvailableSegments { get; } = new();

    public BomAddEntryViewModel(BomImportResult? bomState)
    {
        for (int i = 1; i <= 8; i++) AvailableSkids.Add($"Skid {i}");
        SelectedSkid = "Skid 1";
    }

    private void UpdateAvailableSegments()
    {
        AvailableSegments.Clear();
        AvailableSegments.Add("FF-1");
        AvailableSegments.Add("FF-2");
        AvailableSegments.Add("FF-3");
        AvailableSegments.Add("AT");
        AvailableSegments.Add("DP");
        AvailableSegments.Add("FS");
        AvailableSegments.Add("MB");
        SelectedSegment = AvailableSegments.FirstOrDefault() ?? string.Empty;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
