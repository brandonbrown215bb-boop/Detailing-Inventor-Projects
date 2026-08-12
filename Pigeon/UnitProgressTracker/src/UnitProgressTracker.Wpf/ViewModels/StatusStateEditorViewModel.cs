using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;

namespace UnitProgressTracker.Wpf.ViewModels;

public class StatusStateItemViewModel : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _colorHex = "#94a3b8";
    private string _fillType = "solid";

    public string Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string ColorHex
    {
        get => _colorHex;
        set { _colorHex = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsValidColor)); }
    }

    public string FillType
    {
        get => _fillType;
        set { _fillType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsSolid)); OnPropertyChanged(nameof(IsWireframe)); }
    }

    public bool IsSolid
    {
        get => string.Equals(FillType, "solid", StringComparison.OrdinalIgnoreCase);
        set { if (value) FillType = "solid"; }
    }

    public bool IsWireframe
    {
        get => string.Equals(FillType, "wireframe", StringComparison.OrdinalIgnoreCase);
        set { if (value) FillType = "wireframe"; }
    }

    public bool IsValidColor => StatusStateService.IsValidHexColor(ColorHex);

    public StatusStateItemViewModel(StatusState state)
    {
        _id = state.Id;
        _name = state.Name;
        _colorHex = StatusStateService.NormalizeHexColor(state.ColorHex);
        _fillType = StatusStateService.NormalizeFillType(state.FillType);
    }

    public StatusState ToModel()
    {
        return new StatusState(_id, _name, StatusStateService.NormalizeHexColor(_colorHex), StatusStateService.NormalizeFillType(_fillType));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class StatusStateEditorViewModel : INotifyPropertyChanged
{
    private StatusStateItemViewModel? _selectedState;
    private string _validationError = string.Empty;

    public ObservableCollection<StatusStateItemViewModel> States { get; } = new();

    public StatusStateItemViewModel? SelectedState
    {
        get => _selectedState;
        set
        {
            _selectedState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedState));
            Validate();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool HasSelectedState => SelectedState != null;

    public string ValidationError
    {
        get => _validationError;
        set { _validationError = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(ValidationError);

    public List<string> PaletteSwatches { get; } = new()
    {
        "#94a3b8", "#f59e0b", "#3b82f6", "#8b5cf6",
        "#06b6d4", "#10b981", "#22c55e", "#ef4444",
        "#f97316", "#ec4899", "#6366f1", "#64748b"
    };

    public ICommand AddStateCommand { get; }
    public ICommand DeleteStateCommand { get; }
    public ICommand MoveStateUpCommand { get; }
    public ICommand MoveStateDownCommand { get; }
    public ICommand SelectColorSwatchCommand { get; }
    public ICommand ResetDefaultsCommand { get; }
    public ICommand SaveCommand { get; }

    public event Action<bool>? RequestClose;

    public StatusStateEditorViewModel(IEnumerable<StatusState>? currentStates)
    {
        var inputStates = (currentStates != null && currentStates.Any())
            ? currentStates
            : StatusStateService.GetDefaultStates();

        foreach (var st in inputStates)
        {
            States.Add(new StatusStateItemViewModel(st));
        }

        if (States.Count > 0)
            SelectedState = States[0];

        AddStateCommand = new RelayCommand(_ => ExecuteAddState());
        DeleteStateCommand = new RelayCommand(_ => ExecuteDeleteState(), _ => HasSelectedState && States.Count > 1);
        MoveStateUpCommand = new RelayCommand(_ => ExecuteMoveState(-1), _ => SelectedState != null && States.IndexOf(SelectedState) > 0);
        MoveStateDownCommand = new RelayCommand(_ => ExecuteMoveState(1), _ => SelectedState != null && States.IndexOf(SelectedState) < States.Count - 1);
        SelectColorSwatchCommand = new RelayCommand(color => ExecuteSelectColorSwatch(color as string));
        ResetDefaultsCommand = new RelayCommand(_ => ExecuteResetDefaults());
        SaveCommand = new RelayCommand(_ => ExecuteSave(), _ => !HasError);
    }

    private void ExecuteAddState()
    {
        int count = States.Count + 1;
        var newState = new StatusStateItemViewModel(new StatusState($"custom-state-{count}", $"Custom State {count}", "#38bdf8", "solid"));
        States.Add(newState);
        SelectedState = newState;
    }

    private void ExecuteDeleteState()
    {
        if (SelectedState == null || States.Count <= 1) return;
        int idx = States.IndexOf(SelectedState);
        States.Remove(SelectedState);
        SelectedState = States.Count > 0 ? States[Math.Min(idx, States.Count - 1)] : null;
    }

    private void ExecuteMoveState(int offset)
    {
        if (SelectedState == null) return;
        int oldIndex = States.IndexOf(SelectedState);
        int newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= States.Count) return;

        States.Move(oldIndex, newIndex);
        OnPropertyChanged(nameof(SelectedState));
        CommandManager.InvalidateRequerySuggested();
    }

    private void ExecuteSelectColorSwatch(string? colorHex)
    {
        if (SelectedState != null && !string.IsNullOrEmpty(colorHex))
        {
            SelectedState.ColorHex = colorHex;
            Validate();
        }
    }

    private void ExecuteResetDefaults()
    {
        States.Clear();
        foreach (var def in StatusStateService.GetDefaultStates())
        {
            States.Add(new StatusStateItemViewModel(def));
        }
        SelectedState = States.FirstOrDefault();
    }

    private void ExecuteSave()
    {
        if (Validate())
        {
            RequestClose?.Invoke(true);
        }
    }

    public bool Validate()
    {
        if (SelectedState != null)
        {
            if (string.IsNullOrWhiteSpace(SelectedState.Id))
            {
                ValidationError = "State ID cannot be empty.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(SelectedState.Name))
            {
                ValidationError = "State Name cannot be empty.";
                return false;
            }
            if (!SelectedState.IsValidColor)
            {
                ValidationError = "Color Hex must be valid (e.g., #38BDF8 or #FF38BDF8).";
                return false;
            }
        }

        var duplicate = States.GroupBy(s => s.Id.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            ValidationError = $"Duplicate State ID found: '{duplicate.Key}'.";
            return false;
        }

        ValidationError = string.Empty;
        return true;
    }

    public List<StatusState> GetResultStates()
    {
        return States.Select(s => s.ToModel()).ToList();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
