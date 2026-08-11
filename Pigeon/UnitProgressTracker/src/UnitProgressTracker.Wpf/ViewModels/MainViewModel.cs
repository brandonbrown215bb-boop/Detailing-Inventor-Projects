using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;

namespace UnitProgressTracker.Wpf.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        _isExecuting = true;
        CommandManager.InvalidateRequerySuggested();
        try { await _execute(parameter); }
        finally
        {
            _isExecuting = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

public record CombinedBulkheadChannelViewModel(
    string BulkheadPartNumber,
    string BulkheadDesc,
    string UnitSide,
    double DoaOffset,
    double StartOffset,
    double ChannelSpan,
    int TotalHoles
);

public class MainViewModel : INotifyPropertyChanged
{
    private string? _currentFolderPath;
    private string? _currentProjectPath;
    private bool _isDirty;
    private SurfaceModel? _selectedSurface;
    private ShellFolderEntry? _selectedBomEntry;
    private string _searchText = string.Empty;
    private string _shellRootPath = string.Empty;
    private int _selectedTabIndex = 0;
    private string _statusMessage = "Ready. Open a unit folder or project to begin.";
    private string _selectedSkidFilter = "All Skids";
    private string _selectedSegmentFilter = "All Segments";
    private bool _isCustomSqOnly;
    private bool _showMisplacedDetails;
    private bool _wireframeVisible = true;
    private double _globalOpacity = 1.0;
    private bool _isScanning;
    private double _scanProgress;
    private string _scanProgressLabel = string.Empty;
    private readonly DispatcherTimer _autoSaveTimer;

    public ProjectStateModel ProjectState { get; private set; } = new();
    public ObservableCollection<SurfaceModel> Surfaces { get; } = new();
    public ObservableCollection<SurfaceGroupViewModel> GroupedSurfaces { get; } = new();
    public ObservableCollection<SurfaceModel> RemovedSurfaces { get; } = new();
    public ObservableCollection<StatusState> StatusStates { get; } = new();
    public ObservableCollection<ShellFolderEntry> BomEntries { get; } = new();
    public ObservableCollection<ShellFolderEntry> FilteredBomEntries { get; } = new();
    public ObservableCollection<BomRow> MisplacedRows { get; } = new();
    public ObservableCollection<string> AvailableSkids { get; } = new();
    public ObservableCollection<string> AvailableSegments { get; } = new();
    public ShellFolderPlan? CurrentBomPlan { get; private set; }

    // Decomposed Child ViewModels
    public ScanProgressViewModel ScanProgressVM { get; } = new();
    public BomFilterViewModel BomFilterVM { get; } = new();
    public ProjectNavigationViewModel ProjectNavVM { get; } = new();
    public ObservableCollection<RecentProjectItemViewModel> RecentProjects => ProjectNavVM.RecentProjects;

    // Callbacks wired by MainWindow
    public Action? RequestViewportRefresh { get; set; }
    public Action<string>? RequestHighlightSurface { get; set; }
    public Action<bool>? RequestSetWireframe { get; set; }
    public Action<bool>? RequestSetSkidGrid { get; set; }
    public Action<double>? RequestSetOpacity { get; set; }
    public Action<bool, string>? RequestSetSurfaceVisibility { get; set; }
    public Func<CameraStateModel>? RequestGetCameraState { get; set; }
    public Action<CameraStateModel>? RequestSetCameraState { get; set; }
    public Func<string?>? RequestBrowseShellRootFolder { get; set; }

    private bool _isOfflineMode;
    public bool IsOfflineMode
    {
        get => _isOfflineMode;
        set
        {
            if (_isOfflineMode != value)
            {
                _isOfflineMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    // -----------------------------------------------------------------------
    // Properties
    // -----------------------------------------------------------------------

    public string? CurrentFolderPath
    {
        get => _currentFolderPath;
        set { _currentFolderPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasFolder)); OnPropertyChanged(nameof(WindowTitle)); }
    }

    public bool HasFolder => !string.IsNullOrWhiteSpace(CurrentFolderPath);

    public string? CurrentProjectPath
    {
        get => _currentProjectPath;
        set
        {
            _currentProjectPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasProjectPath));
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public bool HasProjectPath => !string.IsNullOrWhiteSpace(CurrentProjectPath);

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public string WindowTitle
    {
        get
        {
            string name = string.IsNullOrEmpty(CurrentProjectPath)
                ? (HasFolder ? Path.GetFileName(CurrentFolderPath)! : "Untitled Project")
                : Path.GetFileName(CurrentProjectPath);
            string offline = IsOfflineMode ? " [Offline Mode]" : "";
            string marker = IsDirty ? "*" : "";
            return $"Unit Progress Tracker — {name}{offline}{marker}";
        }
    }

    public bool HasRecentProjects => RecentProjects.Count > 0;

    public static string NormalizeSide(string? side)
    {
        if (string.IsNullOrWhiteSpace(side)) return "";
        string s = side.Trim().ToLowerInvariant();
        if (s.Contains("roof") || s.Contains("top")) return "top";
        if (s.Contains("floor") || s.Contains("bottom")) return "bottom";
        if (s.Contains("left")) return "left";
        if (s.Contains("right")) return "right";
        if (s.Contains("front")) return "front";
        if (s.Contains("rear") || s.Contains("back")) return "rear";
        return s;
    }

    public SurfaceModel? SelectedSurface
    {
        get => _selectedSurface;
        set
        {
            _selectedSurface = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedSurface));
            OnPropertyChanged(nameof(SelectedSurfaceNotes));
            OnPropertyChanged(nameof(SelectedSurfaceCasingSpec));
            OnPropertyChanged(nameof(SelectedSurfaceSideName));
            OnPropertyChanged(nameof(SelectedSurfaceSkin));
            OnPropertyChanged(nameof(SelectedSurfaceLiner));
            OnPropertyChanged(nameof(SelectedSurfaceWallThickness));
            OnPropertyChanged(nameof(IsSelectedSurfaceFloor));
            OnPropertyChanged(nameof(SelectedSurfaceOpenings));
            OnPropertyChanged(nameof(SelectedSurfaceBulkheadPatterns));
            OnPropertyChanged(nameof(SelectedSurfaceBulkheadChannels));
            OnPropertyChanged(nameof(SelectedSurfaceCombinedChannels));
            OnPropertyChanged(nameof(CurrentJobContext));
            OnPropertyChanged(nameof(HasJobContext));
            NotifyChecklistChanged();
            if (value != null)
                RequestHighlightSurface?.Invoke(value.SurfaceNumber);
        }
    }

    public bool HasSelectedSurface => SelectedSurface != null;

    public string SelectedSurfaceNotes
    {
        get => SelectedSurface?.Notes ?? string.Empty;
        set
        {
            if (SelectedSurface != null && SelectedSurface.Notes != value)
            {
                SelectedSurface.Notes = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    // Expose checklist as a bindable list for the right-panel ItemsControl
    public IEnumerable<ChecklistItemViewModel> ChecklistItems
    {
        get
        {
            if (SelectedSurface == null) return Enumerable.Empty<ChecklistItemViewModel>();
            return SelectedSurface.Checklist.Select(kv => new ChecklistItemViewModel(kv.Key, kv.Value, this));
        }
    }

    public int ChecklistCompletedCount => SelectedSurface?.Checklist.Values.Count(v => v) ?? 0;
    public int ChecklistTotalCount => SelectedSurface?.Checklist.Count ?? 0;
    public double ChecklistProgressPercent => ChecklistTotalCount > 0 ? (double)ChecklistCompletedCount / ChecklistTotalCount * 100.0 : 0.0;
    public string ChecklistProgressText => ChecklistTotalCount > 0 
        ? $"{ChecklistCompletedCount} / {ChecklistTotalCount} completed ({ChecklistProgressPercent:F0}%)"
        : "No checklist items";

    public void NotifyChecklistChanged()
    {
        OnPropertyChanged(nameof(ChecklistItems));
        OnPropertyChanged(nameof(ChecklistCompletedCount));
        OnPropertyChanged(nameof(ChecklistTotalCount));
        OnPropertyChanged(nameof(ChecklistProgressPercent));
        OnPropertyChanged(nameof(ChecklistProgressText));
    }

    private bool _showBulkheadChannels = true;
    public bool ShowBulkheadChannels
    {
        get => _showBulkheadChannels;
        set
        {
            if (_showBulkheadChannels != value)
            {
                _showBulkheadChannels = value;
                OnPropertyChanged();
                RequestViewportRefresh?.Invoke();
            }
        }
    }

    public JobContextModel? CurrentJobContext => Surfaces.FirstOrDefault(s => s.JobContext != null && !string.IsNullOrEmpty(s.JobContext.SalesOrderNumber))?.JobContext;
    public bool HasJobContext => CurrentJobContext != null;

    public CasingSpecModel? SelectedSurfaceCasingSpec => SelectedSurface?.CasingSpec;

    public string SelectedSurfaceSideName
    {
        get
        {
            if (SelectedSurface == null) return "";
            string side = NormalizeSide(SelectedSurface.SideTag);
            return side switch
            {
                "top" => "Top / Roof Surface",
                "bottom" => "Bottom / Floor Surface",
                "left" => "Left Wall Surface",
                "right" => "Right Wall Surface",
                "front" => "Front Wall Surface",
                "rear" => "Rear Wall Surface",
                _ => SelectedSurface.SideTag
            };
        }
    }

    public string SelectedSurfaceSkin
    {
        get
        {
            if (SelectedSurface?.CasingSpec == null) return "N/A";
            string side = NormalizeSide(SelectedSurface.SideTag);
            var spec = SelectedSurface.CasingSpec;
            return side switch
            {
                "top" => !string.IsNullOrEmpty(spec.SkinTop) ? spec.SkinTop : "N/A",
                "bottom" => !string.IsNullOrEmpty(spec.SkinBottom) ? spec.SkinBottom : "N/A",
                "left" => !string.IsNullOrEmpty(spec.SkinLeft) ? spec.SkinLeft : "N/A",
                "right" => !string.IsNullOrEmpty(spec.SkinRight) ? spec.SkinRight : "N/A",
                "front" => !string.IsNullOrEmpty(spec.SkinFront) ? spec.SkinFront : "N/A",
                "rear" => !string.IsNullOrEmpty(spec.SkinRear) ? spec.SkinRear : "N/A",
                _ => !string.IsNullOrEmpty(spec.SkinTop) ? spec.SkinTop : "N/A"
            };
        }
    }

    public string SelectedSurfaceLiner
    {
        get
        {
            if (SelectedSurface?.CasingSpec == null) return "N/A";
            string side = NormalizeSide(SelectedSurface.SideTag);
            var spec = SelectedSurface.CasingSpec;
            return side switch
            {
                "top" => !string.IsNullOrEmpty(spec.LinerTop) ? spec.LinerTop : "N/A",
                "bottom" => !string.IsNullOrEmpty(spec.LinerBottom) ? spec.LinerBottom : "N/A",
                "left" => !string.IsNullOrEmpty(spec.LinerLeft) ? spec.LinerLeft : "N/A",
                "right" => !string.IsNullOrEmpty(spec.LinerRight) ? spec.LinerRight : "N/A",
                "front" => !string.IsNullOrEmpty(spec.LinerFront) ? spec.LinerFront : "N/A",
                "rear" => !string.IsNullOrEmpty(spec.LinerRear) ? spec.LinerRear : "N/A",
                _ => !string.IsNullOrEmpty(spec.LinerTop) ? spec.LinerTop : "N/A"
            };
        }
    }

    public string SelectedSurfaceWallThickness
    {
        get
        {
            if (SelectedSurface?.CasingSpec == null) return "N/A";
            string side = NormalizeSide(SelectedSurface.SideTag);
            var spec = SelectedSurface.CasingSpec;
            double? th = side switch
            {
                "top" => spec.WallThicknessTop,
                "bottom" => spec.WallThicknessBottom,
                "left" => spec.WallThicknessLeft,
                "right" => spec.WallThicknessRight,
                "front" => spec.WallThicknessFront,
                "rear" => spec.WallThicknessRear,
                _ => spec.WallThicknessTop
            };
            return (th.HasValue && th.Value > 0) ? $"{th.Value:0.0}\"" : "N/A";
        }
    }

    public bool IsSelectedSurfaceFloor => NormalizeSide(SelectedSurface?.SideTag) == "bottom";

    public IEnumerable<OpeningModel> SelectedSurfaceOpenings
    {
        get
        {
            if (SelectedSurface?.Openings == null) return Enumerable.Empty<OpeningModel>();
            string surfSide = NormalizeSide(SelectedSurface.SideTag);
            if (string.IsNullOrEmpty(surfSide)) return SelectedSurface.Openings;
            return SelectedSurface.Openings.Where(o => NormalizeSide(o.UnitSide) == surfSide);
        }
    }

    public IEnumerable<BulkheadHolePatternModel> SelectedSurfaceBulkheadPatterns => SelectedSurface?.BulkheadHolePatterns ?? Enumerable.Empty<BulkheadHolePatternModel>();

    public IEnumerable<CombinedBulkheadChannelViewModel> SelectedSurfaceCombinedChannels
    {
        get
        {
            if (SelectedSurface?.BulkheadHolePatterns == null) return Enumerable.Empty<CombinedBulkheadChannelViewModel>();
            string surfSide = NormalizeSide(SelectedSurface.SideTag);

            var matching = SelectedSurface.BulkheadHolePatterns.Where(p => p.WidthQty > 0);
            if (!string.IsNullOrEmpty(surfSide))
            {
                matching = matching.Where(p => NormalizeSide(p.UnitSide) == surfSide);
            }

            var result = new List<CombinedBulkheadChannelViewModel>();
            foreach (var group in matching.GroupBy(p => new { Part = p.BulkheadPartNumber, Side = p.UnitSide }))
            {
                var first = group.First();
                double minDoa = group.Min(p => p.DoaOffset);
                double minOffset = group.Min(p => p.WidthOffset);
                double maxEnd = group.Max(p => p.WidthOffset + (p.WidthQty > 1 ? (p.WidthQty - 1) * p.WidthSpacing : 0.0));
                double span = Math.Max(1.5, (maxEnd - minOffset) + 1.5);
                int totalHoles = (int)group.Sum(p => p.WidthQty);

                result.Add(new CombinedBulkheadChannelViewModel(
                    BulkheadPartNumber: first.BulkheadPartNumber,
                    BulkheadDesc: first.BulkheadDescription,
                    UnitSide: first.UnitSide,
                    DoaOffset: minDoa,
                    StartOffset: minOffset,
                    ChannelSpan: span,
                    TotalHoles: totalHoles
                ));
            }

            return result;
        }
    }

    public IEnumerable<GeometryBox> SelectedSurfaceBulkheadChannels => SelectedSurface?.BulkheadChannels ?? Enumerable.Empty<GeometryBox>();

    public ShellFolderEntry? SelectedBomEntry
    {
        get => _selectedBomEntry;
        set { _selectedBomEntry = value; OnPropertyChanged(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); FilterBomEntries(); }
    }

    public string ShellRootPath
    {
        get => _shellRootPath;
        set { _shellRootPath = value; OnPropertyChanged(); RecalculateEntryAbsolutePaths(); }
    }

    public DisplayPreferences Preferences => ProjectState.Preferences;

    public bool HasUnitConfig => ProjectState.UnitConfig != null;
    public string UnitConfigSummaryText => ProjectState.UnitConfig != null
        ? $"Config: {ProjectState.UnitConfig.ProjectId ?? "Loaded"} ({ProjectState.UnitConfig.Skids.Count} Skids)"
        : "No Config.xml loaded";

    public string GroupMode
    {
        get => Preferences.ListDisplay.GroupMode;
        set
        {
            Preferences.ListDisplay.GroupMode = value;
            OnPropertyChanged();
            RebuildGroupedSurfaces();
            MarkDirty();
        }
    }

    public string NameMode
    {
        get => Preferences.ListDisplay.NameMode;
        set
        {
            Preferences.ListDisplay.NameMode = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public string SortMode
    {
        get => Preferences.ListDisplay.SortMode;
        set
        {
            Preferences.ListDisplay.SortMode = value;
            OnPropertyChanged();
            RebuildGroupedSurfaces();
            MarkDirty();
        }
    }

    public bool ShowTypeTag
    {
        get => Preferences.ListDisplay.ShowTypeTag;
        set
        {
            Preferences.ListDisplay.ShowTypeTag = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public bool ShowSkidTag
    {
        get => Preferences.ListDisplay.ShowSkidTag;
        set
        {
            Preferences.ListDisplay.ShowSkidTag = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public bool ShowSideTag
    {
        get => Preferences.ListDisplay.ShowSideTag;
        set
        {
            Preferences.ListDisplay.ShowSideTag = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public bool ShowSkidGrid
    {
        get => Preferences.ViewerOptions.ShowGrid;
        set
        {
            Preferences.ViewerOptions.ShowGrid = value;
            OnPropertyChanged();
            RequestSetSkidGrid?.Invoke(value);
            MarkDirty();
        }
    }

    public bool ShowLegend
    {
        get => Preferences.ViewerOptions.ShowLegend;
        set
        {
            Preferences.ViewerOptions.ShowLegend = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public bool ShowHoverTooltip
    {
        get => Preferences.ViewerOptions.ShowHoverTooltip;
        set
        {
            Preferences.ViewerOptions.ShowHoverTooltip = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public int ActiveSurfacesCount => Surfaces.Count(s => !s.IsHidden);
    public int HiddenSurfacesCount => Surfaces.Count(s => s.IsHidden);
    public int RemovedSurfacesCount => RemovedSurfaces.Count;

    public void RebuildGroupedSurfaces()
    {
        GroupedSurfaces.Clear();

        IEnumerable<SurfaceModel> query = Surfaces;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(s => s.SurfaceNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                                  || s.PartNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                                  || s.SurfaceUnitSide.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        if (GroupMode == "skid")
        {
            var groups = query.GroupBy(s => s.SkidId).OrderBy(g => g.Key);
            foreach (var g in groups)
            {
                var grpVM = new SurfaceGroupViewModel
                {
                    GroupKey = $"Skid {g.Key}",
                    DisplayName = $"Skid {g.Key} ({g.Count()} surfaces)"
                };
                grpVM.GroupVisibilityToggled += _ => RequestViewportRefresh?.Invoke();

                foreach (var surf in g)
                    grpVM.Surfaces.Add(surf);

                GroupedSurfaces.Add(grpVM);
            }
        }
        else if (GroupMode == "type")
        {
            var groups = query.GroupBy(s => s.TypeTag).OrderBy(g => g.Key);
            foreach (var g in groups)
            {
                var grpVM = new SurfaceGroupViewModel
                {
                    GroupKey = g.Key,
                    DisplayName = $"{g.Key} ({g.Count()} surfaces)"
                };
                grpVM.GroupVisibilityToggled += _ => RequestViewportRefresh?.Invoke();

                foreach (var surf in g)
                    grpVM.Surfaces.Add(surf);

                GroupedSurfaces.Add(grpVM);
            }
        }
        else
        {
            var grpVM = new SurfaceGroupViewModel
            {
                GroupKey = "All Surfaces",
                DisplayName = $"All Surfaces ({query.Count()})"
            };
            grpVM.GroupVisibilityToggled += _ => RequestViewportRefresh?.Invoke();

            foreach (var surf in query)
                grpVM.Surfaces.Add(surf);

            GroupedSurfaces.Add(grpVM);
        }

        OnPropertyChanged(nameof(ActiveSurfacesCount));
        OnPropertyChanged(nameof(HiddenSurfacesCount));
        OnPropertyChanged(nameof(RemovedSurfacesCount));
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string SelectedSkidFilter
    {
        get => _selectedSkidFilter;
        set { _selectedSkidFilter = value; OnPropertyChanged(); FilterBomEntries(); }
    }

    public string SelectedSegmentFilter
    {
        get => _selectedSegmentFilter;
        set { _selectedSegmentFilter = value; OnPropertyChanged(); FilterBomEntries(); }
    }

    public bool IsCustomSqOnly
    {
        get => _isCustomSqOnly;
        set { _isCustomSqOnly = value; OnPropertyChanged(); FilterBomEntries(); }
    }

    public bool HasMisplacedCoilPanels => MisplacedRows.Count > 0;
    public int MisplacedCoilPanelsCount => MisplacedRows.Count;
    public string MisplacedCoilPanelMessage => $"{MisplacedCoilPanelsCount} row(s) have segment '<--'. These lines do not belong to a skid sequence and will be skipped in folder creation.";

    public bool ShowMisplacedDetails
    {
        get => _showMisplacedDetails;
        set { _showMisplacedDetails = value; OnPropertyChanged(); OnPropertyChanged(nameof(MisplacedDetailsToggleText)); }
    }

    public string MisplacedDetailsToggleText => ShowMisplacedDetails ? "Hide Details" : "View Details";

    // R5 — Wireframe toggle
    public bool WireframeVisible
    {
        get => _wireframeVisible;
        set
        {
            _wireframeVisible = value;
            OnPropertyChanged();
            RequestSetWireframe?.Invoke(value);
        }
    }

    // R5 — Global opacity
    public double GlobalOpacity
    {
        get => _globalOpacity;
        set
        {
            _globalOpacity = Math.Clamp(value, 0.1, 1.0);
            OnPropertyChanged();
            RequestSetOpacity?.Invoke(_globalOpacity);
        }
    }

    // R4 — Async scan state
    public bool IsScanning
    {
        get => _isScanning;
        set { _isScanning = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotScanning)); }
    }

    public bool IsNotScanning => !_isScanning;

    public double ScanProgress
    {
        get => _scanProgress;
        set { _scanProgress = value; OnPropertyChanged(); }
    }

    public string ScanProgressLabel
    {
        get => _scanProgressLabel;
        set { _scanProgressLabel = value; OnPropertyChanged(); }
    }

    // -----------------------------------------------------------------------
    // Commands
    // -----------------------------------------------------------------------

    public ICommand NewProjectCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand SaveProjectCommand { get; }
    public ICommand SaveProjectAsCommand { get; }
    public ICommand OpenRecentProjectCommand { get; }
    public ICommand ClearRecentProjectsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand ImportExcelBomCommand { get; }
    public ICommand ImportUnitConfigCommand { get; }
    public ICommand SetShellRootFolderCommand { get; }
    public ICommand CreateShellFoldersCommand { get; }
    public ICommand OpenShellFolderCommand { get; }
    public ICommand AddBomRowCommand { get; }
    public ICommand DeleteBomRowCommand { get; }
    public ICommand ToggleMisplacedDetailsCommand { get; }
    public ICommand ExportMarkdownCommand { get; }
    public ICommand ToggleWireframeCommand { get; }
    public ICommand ToggleSurfaceVisibilityCommand { get; }
    public ICommand CancelScanCommand { get; }
    public ICommand AsyncScanFolderCommand { get; }

    // M3 Commands
    public ICommand ManageStatusStatesCommand { get; private set; } = null!;
    public ICommand AddChecklistItemCommand { get; private set; } = null!;
    public ICommand DeleteChecklistItemCommand { get; private set; } = null!;
    public ICommand ClearNotesCommand { get; private set; } = null!;
    public ICommand ToggleSelectedSurfaceVisibilityCommand { get; private set; } = null!;

    // Esmund Parity Commands
    public ICommand ShowAllSurfacesCommand { get; }
    public ICommand RenumberSurfaceCommand { get; }
    public ICommand LinkPreviousSurfaceCommand { get; }
    public ICommand ReplaceFromIamCommand { get; }
    public ICommand RemoveSurfaceCommand { get; }
    public ICommand OpenOptionsDialogCommand { get; }
    public ICommand OpenRecentProjectsDialogCommand { get; }
    public ICommand OpenBomAddDialogCommand { get; }
    public ICommand ImportJsonCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand AddSurfacesFromFolderCommand { get; }

    public MainViewModel()
    {
        foreach (var state in StatusStateService.GetDefaultStates())
            StatusStates.Add(state);

        AvailableSkids.Add("All Skids");
        AvailableSegments.Add("All Segments");

        NewProjectCommand = new RelayCommand(_ => ExecuteNewProject());
        OpenProjectCommand = new RelayCommand(_ => ExecuteOpenProject());
        SaveProjectCommand = new RelayCommand(_ => ExecuteSaveProject());
        SaveProjectAsCommand = new RelayCommand(_ => ExecuteSaveProjectAs());
        OpenRecentProjectCommand = new RelayCommand(p => ExecuteOpenRecentProject(p as string));
        ClearRecentProjectsCommand = new RelayCommand(_ => ExecuteClearRecentProjects(), _ => HasRecentProjects);
        ExitCommand = new RelayCommand(_ => ExecuteExit());
        ImportExcelBomCommand = new RelayCommand(_ => ExecuteImportExcelBom());
        ImportUnitConfigCommand = new RelayCommand(_ => ExecuteImportUnitConfig());
        SetShellRootFolderCommand = new RelayCommand(_ => ExecuteSetShellRootFolder());
        CreateShellFoldersCommand = new RelayCommand(_ => CreateShellFolders(), _ => BomEntries.Count > 0);
        OpenShellFolderCommand = new RelayCommand(_ => ExecuteOpenShellFolder(), _ => !string.IsNullOrWhiteSpace(ShellRootPath) && Directory.Exists(ShellRootPath));
        AddBomRowCommand = new RelayCommand(_ => ExecuteAddBomRow());
        DeleteBomRowCommand = new RelayCommand(_ => ExecuteDeleteBomRow(), _ => SelectedBomEntry != null);
        ToggleMisplacedDetailsCommand = new RelayCommand(_ => ShowMisplacedDetails = !ShowMisplacedDetails);
        ExportMarkdownCommand = new RelayCommand(_ => ExecuteExportMarkdown(), _ => Surfaces.Count > 0);
        ToggleWireframeCommand = new RelayCommand(_ => WireframeVisible = !WireframeVisible);
        ToggleSurfaceVisibilityCommand = new RelayCommand(p => ExecuteToggleSurfaceVisibility(p as SurfaceModel));
        CancelScanCommand = new RelayCommand(_ => ScanProgressVM.CancelScan(), _ => IsScanning);
        AsyncScanFolderCommand = new AsyncRelayCommand(_ => ExecuteAsyncScanAsync());

        // M3 Command Initializations
        ManageStatusStatesCommand = new RelayCommand(_ => ExecuteManageStatusStates());
        AddChecklistItemCommand = new RelayCommand(p => ExecuteAddChecklistItem(p as string), _ => HasSelectedSurface);
        DeleteChecklistItemCommand = new RelayCommand(p => ExecuteDeleteChecklistItem(p as string), _ => HasSelectedSurface);
        ClearNotesCommand = new RelayCommand(_ => SelectedSurfaceNotes = string.Empty, _ => HasSelectedSurface && !string.IsNullOrEmpty(SelectedSurfaceNotes));
        ToggleSelectedSurfaceVisibilityCommand = new RelayCommand(_ => ExecuteToggleSurfaceVisibility(SelectedSurface), _ => HasSelectedSurface);

        // Esmund Parity Commands Initializations
        ShowAllSurfacesCommand = new RelayCommand(_ => ExecuteShowAllSurfaces());
        RenumberSurfaceCommand = new RelayCommand(p => ExecuteRenumberSurface(p as string), _ => HasSelectedSurface);
        LinkPreviousSurfaceCommand = new RelayCommand(p => ExecuteLinkPreviousSurface(p as string), _ => HasSelectedSurface);
        ReplaceFromIamCommand = new RelayCommand(_ => ExecuteReplaceFromIam(), _ => HasSelectedSurface);
        RemoveSurfaceCommand = new RelayCommand(_ => ExecuteRemoveSurface(), _ => HasSelectedSurface);
        OpenOptionsDialogCommand = new RelayCommand(_ => ExecuteOpenOptionsDialog());
        OpenRecentProjectsDialogCommand = new RelayCommand(_ => ExecuteOpenRecentProjectsDialog());
        OpenBomAddDialogCommand = new RelayCommand(_ => ExecuteOpenBomAddDialog());
        ImportJsonCommand = new RelayCommand(_ => ExecuteImportJson());
        ExportJsonCommand = new RelayCommand(_ => ExecuteExportJson(), _ => Surfaces.Count > 0);
        AddSurfacesFromFolderCommand = new RelayCommand(_ => ExecuteAddSurfacesFromFolder());

        // Setup 5-minute auto-save background timer
        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5.0)
        };
        _autoSaveTimer.Tick += OnAutoSaveTimerTick;
        _autoSaveTimer.Start();

        RefreshRecentProjects();
    }

    public void MarkDirty() => IsDirty = true;
    public void ClearDirty() => IsDirty = false;

    public void RefreshRecentProjects()
    {
        var settings = AppSettingsService.LoadSettings();
        RecentProjects.Clear();
        foreach (var path in settings.RecentProjects)
        {
            RecentProjects.Add(new RecentProjectItemViewModel(path));
        }
        OnPropertyChanged(nameof(HasRecentProjects));
    }

    private void OnAutoSaveTimerTick(object? sender, EventArgs e)
    {
        if (IsDirty && !string.IsNullOrWhiteSpace(CurrentProjectPath) && !IsScanning)
        {
            SaveProjectInternal(CurrentProjectPath);
            StatusMessage = $"[Auto-Save] Project auto-saved to {Path.GetFileName(CurrentProjectPath)} at {DateTime.Now:HH:mm:ss}.";
        }
    }

    public bool SaveProjectInternal(string filePath)
    {
        try
        {
            ProjectState.Format = ProjectStateModel.FormatId;
            ProjectState.Version = ProjectStateModel.CurrentVersion;
            ProjectState.SourceFolder = CurrentFolderPath;
            ProjectState.UpdatedAt = DateTime.UtcNow;

            ProjectState.StatusDefinitions = StatusStates
                .Select(state => new StatusState(state.Id, state.Name, state.ColorHex, state.FillType))
                .ToList();

            foreach (var surf in Surfaces)
            {
                string key = surf.SurfaceNumber;
                if (string.IsNullOrWhiteSpace(key)) continue;

                if (!ProjectState.Surfaces.TryGetValue(key, out var record))
                {
                    record = new SurfaceRecordModel();
                    ProjectState.Surfaces[key] = record;
                }

                record.StateId = surf.StateId;
                record.Checklist = new Dictionary<string, bool>(surf.Checklist, StringComparer.OrdinalIgnoreCase);
                record.Notes = surf.Notes;
                record.UpdatedAt = DateTime.UtcNow;
                record.Hidden = surf.IsHidden;
                record.DisplayNumber = surf.DisplayNumber ?? key;
                record.PreviousNumbers = surf.PreviousNumbers ?? new List<string>();
                record.GeometryFingerprint = surf.GeometryFingerprint ?? GeometryFingerprinter.CalculateFingerprint(surf);
                ProjectState.Geometry[key] = surf;
            }

            var cameraState = RequestGetCameraState?.Invoke();
            if (cameraState != null)
            {
                ProjectState.Camera = cameraState;
            }

            var currentBomRows = GetCurrentBomRows();
            if (currentBomRows.Count > 0)
            {
                ProjectState.Bom = new BomImportResult
                {
                    SourceFilePath = ProjectState.Bom?.SourceFilePath ?? string.Empty,
                    ImportedAt = ProjectState.Bom?.ImportedAt ?? DateTime.UtcNow,
                    KeptRows = currentBomRows,
                    AllRows = currentBomRows
                };
            }
            else
            {
                ProjectState.Bom = null;
            }

            ProjectSerializer.SaveAtomic(filePath, ProjectState);
            AppSettingsService.AddRecentProject(filePath);
            RefreshRecentProjects();
            CurrentProjectPath = filePath;
            ClearDirty();
            StatusMessage = $"Project saved to {Path.GetFileName(filePath)}.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving project: {ex.Message}";
            return false;
        }
    }

    public void LoadProjectFromFile(string filePath)
    {
        try
        {
            var project = ProjectSerializer.Load<ProjectStateModel>(filePath);
            if (project == null)
            {
                StatusMessage = "Error loading project: File is invalid or uses an unsupported project format (only Version 4 .uptproj files are supported).";
                return;
            }

            ProjectState = project;
            CurrentProjectPath = filePath;
            if (!string.IsNullOrEmpty(project.SourceFolder))
                CurrentFolderPath = project.SourceFolder;

            IsOfflineMode = !string.IsNullOrWhiteSpace(project.SourceFolder) && !Directory.Exists(project.SourceFolder);

            Surfaces.Clear();
            StatusStates.Clear();
            foreach (var state in project.StatusDefinitions.Count > 0
                ? project.StatusDefinitions
                : StatusStateService.GetDefaultStates())
            {
                StatusStates.Add(state);
            }

            foreach (var (key, rec) in project.Surfaces)
            {
                var surf = project.Geometry.TryGetValue(key, out var savedGeometry)
                    ? savedGeometry
                    : new SurfaceModel { SurfaceNumber = key };

                surf.SurfaceNumber = key;
                surf.DisplayNumber = rec.DisplayNumber ?? surf.DisplayNumber ?? key;
                surf.StateId = rec.StateId ?? surf.StateId ?? "current";
                surf.Notes = rec.Notes ?? string.Empty;
                surf.IsHidden = rec.Hidden;
                surf.Checklist = rec.Checklist != null
                    ? new Dictionary<string, bool>(rec.Checklist, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, bool>();
                surf.PreviousNumbers = rec.PreviousNumbers != null
                    ? new List<string>(rec.PreviousNumbers)
                    : new List<string>();
                surf.GeometryFingerprint = rec.GeometryFingerprint ?? surf.GeometryFingerprint;
                Surfaces.Add(surf);
            }

            if (project.Bom != null && project.Bom.KeptRows != null && project.Bom.KeptRows.Count > 0)
            {
                LoadBomRows(project.Bom.KeptRows);
            }
            else
            {
                ClearBomState();
            }

            AppSettingsService.AddRecentProject(filePath);
            RefreshRecentProjects();
            ClearDirty();

            string modeText = IsOfflineMode ? " (Offline Mode)" : string.Empty;
            StatusMessage = $"Loaded project from {Path.GetFileName(filePath)}{modeText} ({Surfaces.Count} surfaces).";

            if (project.Camera != null)
            {
                RequestSetCameraState?.Invoke(project.Camera);
            }

            RequestViewportRefresh?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading project: {ex.Message}";
        }
    }

    public bool ConfirmUnsavedChanges()
    {
        if (!IsDirty) return true;

        var result = System.Windows.MessageBox.Show(
            "You have unsaved changes in the current project.\nDo you want to save before proceeding?",
            "Unsaved Changes",
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            if (string.IsNullOrWhiteSpace(CurrentProjectPath))
            {
                return ExecuteSaveProjectAs();
            }
            return SaveProjectInternal(CurrentProjectPath);
        }
        if (result == System.Windows.MessageBoxResult.No)
        {
            return true;
        }
        return false; // Cancel
    }

    private void ExecuteNewProject()
    {
        if (!ConfirmUnsavedChanges()) return;

        Surfaces.Clear();
        ProjectState = new ProjectStateModel();
        ClearBomState();
        CurrentFolderPath = null;
        CurrentProjectPath = null;
        ClearDirty();
        StatusMessage = "Created new empty project.";
    }

    private void ExecuteOpenProject()
    {
        if (!ConfirmUnsavedChanges()) return;

        var dialog = new OpenFileDialog
        {
            Title = "Open Unit Progress Tracker Project",
            Filter = "Unit Progress Tracker Project (*.uptproj)|*.uptproj|JSON Files (*.json)|*.json|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadProjectFromFile(dialog.FileName);
        }
    }

    private void ExecuteOpenRecentProject(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        if (!File.Exists(filePath))
        {
            System.Windows.MessageBox.Show($"The selected project file does not exist:\n{filePath}", "File Not Found", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }

        if (!ConfirmUnsavedChanges()) return;

        LoadProjectFromFile(filePath);
    }

    private void ExecuteSaveProject()
    {
        if (string.IsNullOrWhiteSpace(CurrentProjectPath))
        {
            ExecuteSaveProjectAs();
        }
        else
        {
            SaveProjectInternal(CurrentProjectPath);
        }
    }

    private bool ExecuteSaveProjectAs()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Project As",
            Filter = "Unit Progress Tracker Project (*.uptproj)|*.uptproj|All Files (*.*)|*.*",
            DefaultExt = "uptproj",
            FileName = !string.IsNullOrEmpty(CurrentProjectPath) ? Path.GetFileName(CurrentProjectPath) : "unit-project.uptproj"
        };

        if (dialog.ShowDialog() == true)
        {
            return SaveProjectInternal(dialog.FileName);
        }
        return false;
    }

    private void ExecuteClearRecentProjects()
    {
        AppSettingsService.ClearRecentProjects();
        RefreshRecentProjects();
        StatusMessage = "Cleared recent projects history.";
    }

    private void ExecuteExit()
    {
        if (ConfirmUnsavedChanges())
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

    // -----------------------------------------------------------------------
    // Surface loading & M3 dynamic features
    // -----------------------------------------------------------------------

    public async Task LoadFolderAsync(string folderPath)
    {
        CurrentFolderPath = folderPath;
        await ExecuteAsyncScanAsync();
    }

    public void LoadFolder(string folderPath)
    {
        LoadFolderAsync(folderPath).GetAwaiter().GetResult();
    }

    public async Task ExecuteAsyncScanAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentFolderPath)) return;

        var token = ScanProgressVM.StartNewScan();
        IsScanning = true;
        ScanProgress = 0;
        ScanProgressLabel = "Starting scan...";

        try
        {
            var progress = new Progress<ProgressReport>(p =>
            {
                ScanProgress = p.Percent;
                ScanProgressLabel = p.Total > 0
                    ? $"Scanning {p.Scanned}/{p.Total} — {p.CurrentFile}"
                    : (string.IsNullOrEmpty(p.StatusMessage) ? "Done." : p.StatusMessage);

                ScanProgressVM.ReportProgress(p.Percent, ScanProgressLabel);
            });

            var candidateSurfaces = await GeometryScanner.ScanIamFolderAsync(
                CurrentFolderPath,
                progress,
                token);

            var reconcileResult = RescanReconciler.Reconcile(
                Surfaces,
                candidateSurfaces,
                ProjectState.Preferences?.ChecklistTemplate);

            Surfaces.Clear();
            foreach (var surf in reconcileResult.ReconciledSurfaces)
            {
                Surfaces.Add(surf);
            }

            ProjectState.IntrusionFlags = reconcileResult.IntrusionFlags;

            IsOfflineMode = false;
            StatusMessage = $"Async scan complete: {Surfaces.Count} surfaces loaded ({reconcileResult.ExactMatches.Count} matched, {reconcileResult.NewSurfaces.Count} new, {reconcileResult.MissingSurfaces.Count} missing, {reconcileResult.IntrusionFlags.Count} intrusion flags).";
            ScanProgressVM.CompleteScan(Surfaces.Count);
            MarkDirty();
            RequestViewportRefresh?.Invoke();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "IAM scan cancelled by user. Active project state preserved.";
            ScanProgressVM.FailScan("Cancelled by user");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}. Active project state preserved.";
            ScanProgressVM.FailScan(ex.Message);
        }
        finally
        {
            IsScanning = false;
            ScanProgressLabel = string.Empty;
        }
    }

    public void SelectSurfaceByNumber(string surfaceNumber)
    {
        var found = Surfaces.FirstOrDefault(s => string.Equals(s.SurfaceNumber, surfaceNumber, StringComparison.OrdinalIgnoreCase));
        if (found != null) SelectedSurface = found;
    }

    public string GetStatusColor(string stateId)
    {
        var match = StatusStates.FirstOrDefault(s => string.Equals(s.Id, stateId, StringComparison.OrdinalIgnoreCase));
        return match?.ColorHex ?? "#94a3b8";
    }

    public void UpdateSelectedSurfaceStatus(string stateId)
    {
        if (SelectedSurface != null && !string.Equals(SelectedSurface.StateId, stateId, StringComparison.OrdinalIgnoreCase))
        {
            SelectedSurface.StateId = stateId;
            MarkDirty();
            RequestViewportRefresh?.Invoke();
            OnPropertyChanged(nameof(SelectedSurface));
        }
    }

    public void UpdateChecklistItem(string key, bool value)
    {
        if (SelectedSurface != null)
        {
            SelectedSurface.Checklist[key] = value;
            MarkDirty();
            NotifyChecklistChanged();
        }
    }

    private void ExecuteManageStatusStates()
    {
        var dialogVm = new StatusStateEditorViewModel(StatusStates);
        var dialog = new StatusStateEditorDialog(dialogVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            var newStates = dialogVm.GetResultStates();
            StatusStates.Clear();
            foreach (var st in newStates) StatusStates.Add(st);
            
            MarkDirty();
            RequestViewportRefresh?.Invoke();
            StatusMessage = $"Updated status states configuration ({StatusStates.Count} states defined).";
        }
    }

    private void ExecuteAddChecklistItem(string? key)
    {
        if (SelectedSurface == null || string.IsNullOrWhiteSpace(key)) return;

        string cleanKey = key.Trim();
        if (!SelectedSurface.Checklist.ContainsKey(cleanKey))
        {
            SelectedSurface.Checklist[cleanKey] = false;
            MarkDirty();
            NotifyChecklistChanged();
        }
    }

    private void ExecuteDeleteChecklistItem(string? key)
    {
        if (SelectedSurface == null || string.IsNullOrWhiteSpace(key)) return;

        if (SelectedSurface.Checklist.Remove(key))
        {
            MarkDirty();
            NotifyChecklistChanged();
        }
    }

    private void ExecuteToggleSurfaceVisibility(SurfaceModel? surface)
    {
        if (surface == null) return;
        surface.IsHidden = !surface.IsHidden;
        MarkDirty();
        RequestSetSurfaceVisibility?.Invoke(surface.IsHidden, surface.SurfaceNumber);
        OnPropertyChanged(nameof(SelectedSurface));
    }

    // -----------------------------------------------------------------------
    // Markdown export (R3)
    // -----------------------------------------------------------------------

    private void ExecuteExportMarkdown()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Audit Report",
            Filter = "Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
            DefaultExt = "md",
            FileName = $"audit-report-{DateTime.Now:yyyy-MM-dd}"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                MarkdownExporter.SaveAuditReport(dialog.FileName, ProjectState, Surfaces, StatusStates);
                StatusMessage = $"Audit report exported to {Path.GetFileName(dialog.FileName)}.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export error: {ex.Message}";
            }
        }
    }

    // -----------------------------------------------------------------------
    // BOM commands
    // -----------------------------------------------------------------------

    public void ExecuteImportExcelBom()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Excel or CSV BOM File",
            Filter = "BOM Files (*.xlsx;*.csv)|*.xlsx;*.csv|Excel Files (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var importer = new ExcelBomImporter();
                var result = importer.ImportBom(dialog.FileName);
                ProjectState.Bom = result;
                LoadBomRows(result.KeptRows);
                MarkDirty();
                StatusMessage = $"Imported {result.KeptCount} kept BOM rows from {Path.GetFileName(dialog.FileName)} ({result.DroppedCount} hardware/factor rows dropped).";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error importing BOM: {ex.Message}";
            }
        }
    }

    public void ExecuteImportUnitConfig()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Ce3 Unit Config XML File",
            Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string content = File.ReadAllText(dialog.FileName);
                var config = UnitConfigParser.ParseUnitConfigXml(content, dialog.FileName);
                ProjectState.UnitConfig = config;
                MarkDirty();

                if (ProjectState.Bom != null)
                {
                    LoadBomRows(ProjectState.Bom.KeptRows);
                }
                else if (BomEntries.Count > 0)
                {
                    LoadBomRows(GetCurrentBomRows());
                }

                StatusMessage = $"Imported unit Config.xml ({config.Skids.Count} skids, Project: {config.ProjectId ?? "N/A"}).";
                OnPropertyChanged(nameof(UnitConfigSummaryText));
                OnPropertyChanged(nameof(HasUnitConfig));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error importing unit Config.xml: {ex.Message}";
            }
        }
    }

    public void ExecuteSetShellRootFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select Shell Root Export Folder" };
        if (dialog.ShowDialog() == true)
        {
            ShellRootPath = dialog.FolderName;
            MarkDirty();
            StatusMessage = $"Shell root path set to: {ShellRootPath}";
        }
    }

    public void ExecuteOpenShellFolder()
    {
        if (!string.IsNullOrWhiteSpace(ShellRootPath) && Directory.Exists(ShellRootPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", ShellRootPath) { UseShellExecute = true });
        }
    }

    public void ExecuteAddBomRow()
    {
        var newRow = new BomRow
        {
            PartNumber = "391-NEW",
            Quantity = "1",
            Unit = "EA",
            Skid = "1 [FR-MB]",
            Segment = "MB",
            Description = "New Component Assembly"
        };

        List<BomRow> currentRows = GetCurrentBomRows();
        currentRows.Add(newRow);
        LoadBomRows(currentRows);
        MarkDirty();

        var addedEntry = BomEntries.FirstOrDefault(e => e.PartNumber == "391-NEW");
        if (addedEntry != null) SelectedBomEntry = addedEntry;
    }

    public void ExecuteDeleteBomRow()
    {
        if (SelectedBomEntry == null) return;

        string targetKey = SelectedBomEntry.EntryKey;
        List<BomRow> currentRows = GetCurrentBomRows();
        currentRows.RemoveAll(r => BomShellEngine.BuildEntryKey(r.PartNumber, r.Skid, r.Segment, r.Description, r.ExtDescription) == targetKey);
        LoadBomRows(currentRows);
        MarkDirty();
    }

    public List<BomRow> GetCurrentBomRows()
    {
        var list = BomEntries.Select(entry => new BomRow
        {
            PartNumber = entry.PartNumber,
            Quantity = entry.Quantity,
            Unit = entry.Unit,
            Skid = entry.Skid,
            Segment = entry.Segment,
            Description = entry.Description,
            ExtDescription = entry.ExtDescription
        }).ToList();

        foreach (var m in MisplacedRows) list.Add(m);
        return list;
    }

    public void ClearBomState()
    {
        ProjectState.Bom = null;
        CurrentBomPlan = new ShellFolderPlan();
        BomEntries.Clear();
        FilteredBomEntries.Clear();
        MisplacedRows.Clear();
        SelectedBomEntry = null;
        OnPropertyChanged(nameof(HasMisplacedCoilPanels));
        OnPropertyChanged(nameof(MisplacedCoilPanelsCount));
        UpdateDropdownFilters();
        FilterBomEntries();
    }

    public void LoadBomRows(IEnumerable<BomRow> rows)
    {
        var rowList = (rows ?? Enumerable.Empty<BomRow>()).ToList();
        var engine = new BomShellEngine();
        CurrentBomPlan = engine.BuildPlan(rowList, ShellRootPath, ProjectState.UnitConfig);

        BomEntries.Clear();
        foreach (var entry in CurrentBomPlan.Entries) BomEntries.Add(entry);

        MisplacedRows.Clear();
        foreach (var m in CurrentBomPlan.Misplaced) MisplacedRows.Add(m);

        OnPropertyChanged(nameof(HasMisplacedCoilPanels));
        OnPropertyChanged(nameof(MisplacedCoilPanelsCount));

        UpdateDropdownFilters();
        FilterBomEntries();

        ProjectState.Bom = new BomImportResult
        {
            KeptRows = rowList,
            AllRows = rowList,
            ImportedAt = DateTime.UtcNow
        };

        StatusMessage = $"Loaded BOM: {CurrentBomPlan.Entries.Count} shell folders planned, {CurrentBomPlan.Misplaced.Count} misplaced coil lines.";
    }

    public void CreateShellFolders()
    {
        if (string.IsNullOrWhiteSpace(ShellRootPath) || !Directory.Exists(ShellRootPath))
        {
            string? chosen = RequestBrowseShellRootFolder?.Invoke();
            if (!string.IsNullOrWhiteSpace(chosen) && Directory.Exists(chosen))
            {
                ShellRootPath = chosen;
                MarkDirty();
                var currentRows = GetCurrentBomRows();
                if (currentRows.Count > 0)
                {
                    LoadBomRows(currentRows);
                }
            }
            else
            {
                StatusMessage = "Error: Shell folder export cancelled. No valid export root folder selected.";
                return;
            }
        }

        var targetEntries = BomEntries.Count > 0 ? BomEntries.ToList() : (CurrentBomPlan?.Entries ?? new List<ShellFolderEntry>());
        if (targetEntries.Count > 0)
        {
            int created = BomShellEngine.CreateShellFolders(ShellRootPath, targetEntries);
            StatusMessage = $"Successfully created {created} shell export folders in {ShellRootPath}.";
        }
        else
        {
            StatusMessage = "No valid 391- BOM shell entries available to export.";
        }
    }

    private void UpdateDropdownFilters()
    {
        string currentSkid = SelectedSkidFilter;
        string currentSeg = SelectedSegmentFilter;

        AvailableSkids.Clear();
        AvailableSkids.Add("All Skids");
        foreach (var sk in BomEntries.Select(e => e.Skid).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s))
            AvailableSkids.Add(sk);

        AvailableSegments.Clear();
        AvailableSegments.Add("All Segments");
        foreach (var sg in BomEntries.Select(e => e.Segment).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s))
            AvailableSegments.Add(sg);

        _selectedSkidFilter = AvailableSkids.Contains(currentSkid) ? currentSkid : "All Skids";
        _selectedSegmentFilter = AvailableSegments.Contains(currentSeg) ? currentSeg : "All Segments";
        OnPropertyChanged(nameof(SelectedSkidFilter));
        OnPropertyChanged(nameof(SelectedSegmentFilter));
    }

    private void FilterBomEntries()
    {
        var query = BomEntries.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedSkidFilter) && SelectedSkidFilter != "All Skids")
            query = query.Where(e => string.Equals(e.Skid, SelectedSkidFilter, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(SelectedSegmentFilter) && SelectedSegmentFilter != "All Segments")
            query = query.Where(e => string.Equals(e.Segment, SelectedSegmentFilter, StringComparison.OrdinalIgnoreCase));

        if (IsCustomSqOnly)
            query = query.Where(e => e.IsCustomSq);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string term = SearchText.Trim();
            query = query.Where(e =>
                (!string.IsNullOrEmpty(e.PartNumber) && e.PartNumber.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.Description) && e.Description.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.ExtDescription) && e.ExtDescription.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.Skid) && e.Skid.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.Segment) && e.Segment.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(e.RelativePath) && e.RelativePath.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        FilteredBomEntries.Clear();
        foreach (var entry in query) FilteredBomEntries.Add(entry);
    }

    private void RecalculateEntryAbsolutePaths()
    {
        foreach (var entry in BomEntries)
        {
            entry.AbsolutePath = !string.IsNullOrWhiteSpace(ShellRootPath)
                ? Path.Combine(ShellRootPath, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar))
                : null;
        }
    }

    private void ExecuteShowAllSurfaces()
    {
        foreach (var surf in Surfaces)
        {
            surf.IsHidden = false;
        }
        RebuildGroupedSurfaces();
        RequestViewportRefresh?.Invoke();
        StatusMessage = "Restored all hidden surfaces.";
    }

    private void ExecuteRenumberSurface(string? newNum)
    {
        if (SelectedSurface == null || string.IsNullOrWhiteSpace(newNum)) return;
        string oldNum = SelectedSurface.EffectiveDisplayNumber;
        if (!SelectedSurface.PreviousNumbers.Contains(oldNum))
            SelectedSurface.PreviousNumbers.Add(oldNum);

        SelectedSurface.DisplayNumber = newNum.Trim();
        MarkDirty();
        RebuildGroupedSurfaces();
        StatusMessage = $"Renumbered surface {oldNum} -> {newNum.Trim()}.";
    }

    private void ExecuteLinkPreviousSurface(string? prevNum)
    {
        if (SelectedSurface == null || string.IsNullOrWhiteSpace(prevNum)) return;
        if (!SelectedSurface.PreviousNumbers.Contains(prevNum.Trim()))
        {
            SelectedSurface.PreviousNumbers.Add(prevNum.Trim());
            MarkDirty();
            StatusMessage = $"Linked previous surface history {prevNum.Trim()} -> {SelectedSurface.SurfaceNumber}.";
        }
    }

    private async void ExecuteReplaceFromIam()
    {
        if (SelectedSurface == null) return;

        var targetSurface = SelectedSurface;
        var dlg = new OpenFileDialog
        {
            Title = $"Select Inventor Assembly (.iam) to replace geometry for {targetSurface.SurfaceNumber}",
            Filter = "Inventor Assembly or JSON (*.iam;*.json)|*.iam;*.json|Inventor Assembly (*.iam)|*.iam|JSON Files (*.json)|*.json|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog() != true) return;

        string pickedFile = dlg.FileName;
        string rootFolder = Path.GetDirectoryName(pickedFile) ?? string.Empty;

        SurfaceModel? candidate = null;
        if (pickedFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            candidate = GeometryScanner.ParseConfigJson(File.ReadAllText(pickedFile), pickedFile, rootFolder, "json");
        }
        else
        {
            candidate = await GeometryScanner.ScanIamFileAsync(pickedFile, rootFolder);
            if (candidate == null)
            {
                // Fallback for IAM files without sidecar JSON or embedded COM metadata
                candidate = new SurfaceModel
                {
                    SurfaceNumber = Path.GetFileNameWithoutExtension(pickedFile),
                    FilePath = pickedFile,
                    RelativePath = !string.IsNullOrEmpty(rootFolder) ? Path.GetRelativePath(rootFolder, pickedFile) : pickedFile,
                    SourceType = "iam",
                    PartNumber = Path.GetFileNameWithoutExtension(pickedFile)
                };
            }
        }

        if (candidate == null)
        {
            StatusMessage = $"Replace failed: Could not read file {Path.GetFileName(pickedFile)}.";
            return;
        }

        var result = ProjectStateService.ReplaceSurfaceInPlace(
            ProjectState,
            targetSurface,
            candidate,
            Surfaces);

        if (!result.Success)
        {
            StatusMessage = $"Replace failed: {result.ErrorMessage}";
            return;
        }

        if (result.Renumbered)
        {
            int idx = Surfaces.IndexOf(targetSurface);
            if (idx >= 0)
            {
                Surfaces[idx] = candidate;
            }
            else
            {
                Surfaces.Remove(targetSurface);
                Surfaces.Add(candidate);
            }
            SelectedSurface = candidate;
        }
        else
        {
            OnPropertyChanged(nameof(SelectedSurface));
        }

        RebuildGroupedSurfaces();
        RequestViewportRefresh?.Invoke();
        MarkDirty();

        if (result.IntrusionDetected)
        {
            StatusMessage = $"Replaced {result.OldSurfaceNumber} -> {result.NewSurfaceNumber} (Warning: Geometry intrusion detected).";
        }
        else
        {
            StatusMessage = $"Successfully replaced surface {result.OldSurfaceNumber} -> {result.NewSurfaceNumber}.";
        }
    }

    private void ExecuteRemoveSurface()
    {
        if (SelectedSurface == null) return;
        var target = SelectedSurface;
        SelectedSurface = null;

        Surfaces.Remove(target);
        if (!RemovedSurfaces.Contains(target))
            RemovedSurfaces.Add(target);

        RebuildGroupedSurfaces();
        RequestViewportRefresh?.Invoke();
        MarkDirty();
        StatusMessage = $"Removed surface {target.SurfaceNumber} to Retired section.";
    }

    private void ExecuteOpenOptionsDialog()
    {
        var vm = new OptionsViewModel(Preferences, StatusStates);
        var dlg = new OptionsDialog(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dlg.ShowDialog() == true)
        {
            StatusStates.Clear();
            foreach (var s in vm.StatusStates) StatusStates.Add(s);

            RebuildGroupedSurfaces();
            RequestSetSkidGrid?.Invoke(ShowSkidGrid);
            RequestViewportRefresh?.Invoke();
            MarkDirty();
            StatusMessage = "Saved options and display preferences.";
        }
    }

    private void ExecuteOpenRecentProjectsDialog()
    {
        var dlg = new RecentProjectsDialog(RecentProjects)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dlg.ShowDialog() == true)
        {
            if (dlg.ClearRequested)
            {
                ExecuteClearRecentProjects();
            }
            else if (dlg.SelectedProject != null)
            {
                ExecuteOpenRecentProject(dlg.SelectedProject.FilePath);
            }
        }
    }

    private void ExecuteOpenBomAddDialog()
    {
        var vm = new BomAddEntryViewModel(ProjectState.Bom);
        if (AvailableSkids.Count > 1)
        {
            vm.AvailableSkids.Clear();
            foreach (var sk in AvailableSkids)
            {
                if (sk != "All Skids") vm.AvailableSkids.Add(sk);
            }
            if (vm.AvailableSkids.Count > 0)
                vm.SelectedSkid = vm.AvailableSkids[0];
        }

        if (AvailableSegments.Count > 1)
        {
            vm.AvailableSegments.Clear();
            foreach (var sg in AvailableSegments)
            {
                if (sg != "All Segments") vm.AvailableSegments.Add(sg);
            }
            if (vm.AvailableSegments.Count > 0)
                vm.SelectedSegment = vm.AvailableSegments[0];
        }

        var dlg = new BomAddEntryDialog(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (dlg.ShowDialog() == true)
        {
            string partNum = string.IsNullOrWhiteSpace(vm.PartNumber) ? "391-NEW" : vm.PartNumber.Trim();
            if (!partNum.StartsWith("391-", StringComparison.OrdinalIgnoreCase))
            {
                partNum = "391-" + partNum;
            }

            var newRow = new BomRow
            {
                PartNumber = partNum,
                Quantity = vm.Quantity > 0 ? vm.Quantity.ToString() : "1",
                Unit = "EA",
                Skid = vm.SelectedSkid ?? "1 [FR-MB]",
                Segment = vm.SelectedSegment ?? "MB",
                Description = string.IsNullOrWhiteSpace(vm.Description) ? "Custom Assembly" : vm.Description.Trim(),
                ExtDescription = vm.ExtDescription?.Trim() ?? string.Empty
            };

            var currentRows = GetCurrentBomRows();
            currentRows.Add(newRow);
            LoadBomRows(currentRows);
            MarkDirty();
            StatusMessage = $"Added 391- entry {newRow.PartNumber} ({newRow.Skid}).";
        }
    }

    private void ExecuteImportJson()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Import Project JSON",
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            LoadProjectFromFile(dlg.FileName);
        }
    }

    private void ExecuteExportJson()
    {
        ExecuteSaveProjectAs();
    }

    private async void ExecuteAddSurfacesFromFolder()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Add surface(s) from folder..."
        };
        if (dlg.ShowDialog() == true)
        {
            await LoadFolderAsync(dlg.FolderName);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // Public surface so MainWindow can notify after directly mutating a model property
    public void OnPropertyChangedPublic(string propertyName) => OnPropertyChanged(propertyName);
}

// Helper VM for the checklist ItemsControl binding
public class ChecklistItemViewModel : INotifyPropertyChanged
{
    private readonly MainViewModel _parent;
    public string Key { get; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            _isChecked = value;
            OnPropertyChanged();
            _parent.UpdateChecklistItem(Key, value);
        }
    }

    public ChecklistItemViewModel(string key, bool isChecked, MainViewModel parent)
    {
        Key = key;
        _isChecked = isChecked;
        _parent = parent;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
