using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Wpf.ViewModels;

public class OptionsViewModel : INotifyPropertyChanged
{
    private DisplayPreferences _preferences;
    private ThemeOptions _applicationThemeOptions;
    private int _selectedTabIndex;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DisplayPreferences Preferences
    {
        get => _preferences;
        set { _preferences = value; OnPropertyChanged(); }
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); }
    }

    public ThemeOptions ApplicationThemeOptions
    {
        get => _applicationThemeOptions;
        set { _applicationThemeOptions = value; OnPropertyChanged(); }
    }

    public ObservableCollection<StatusState> StatusStates { get; } = new();
    public ObservableCollection<string> ChecklistTemplate { get; } = new();

    public ICommand AddStatusStateCommand { get; }
    public ICommand DeleteStatusStateCommand { get; }
    public ICommand AddChecklistTemplateItemCommand { get; }
    public ICommand DeleteChecklistTemplateItemCommand { get; }
    public ICommand ResetDefaultsCommand { get; }

    public OptionsViewModel(
        DisplayPreferences preferences,
        IEnumerable<StatusState> statusStates,
        ThemeOptions? applicationThemeOptions = null)
    {
        _preferences = preferences.Clone();
        _applicationThemeOptions = (applicationThemeOptions ?? new ThemeOptions()).Clone();
        foreach (var s in statusStates)
            StatusStates.Add(new StatusState(s.Id, s.Name, s.ColorHex, s.FillType));
        foreach (var c in _preferences.ChecklistTemplate) ChecklistTemplate.Add(c);

        AddStatusStateCommand = new RelayCommand(_ =>
        {
            StatusStates.Add(new StatusState
            {
                Id = $"state_{StatusStates.Count + 1}",
                Name = "New State",
                ColorHex = "#64748B",
                FillType = "solid"
            });
        });

        DeleteStatusStateCommand = new RelayCommand(param =>
        {
            if (param is StatusState state && StatusStates.Count > 1)
            {
                StatusStates.Remove(state);
            }
        });

        AddChecklistTemplateItemCommand = new RelayCommand(param =>
        {
            if (param is string itemText && !string.IsNullOrWhiteSpace(itemText))
            {
                string cleanText = itemText.Trim();
                if (!ChecklistTemplate.Any(item => string.Equals(item, cleanText, StringComparison.OrdinalIgnoreCase)))
                    ChecklistTemplate.Add(cleanText);
            }
        });

        DeleteChecklistTemplateItemCommand = new RelayCommand(param =>
        {
            if (param is string itemText)
            {
                ChecklistTemplate.Remove(itemText);
            }
        });

        ResetDefaultsCommand = new RelayCommand(_ =>
        {
            Preferences = new DisplayPreferences();
            ApplicationThemeOptions = new ThemeOptions();
            ChecklistTemplate.Clear();
            foreach (var c in Preferences.ChecklistTemplate) ChecklistTemplate.Add(c);
        });
    }

    public void PrepareForSave()
    {
        Preferences.ChecklistTemplate = ChecklistTemplate.ToList();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
