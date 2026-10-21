using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FileGrid.Portable.Models;
using FileGrid.Portable.Services;
using FileGrid.Portable.Views;
using Microsoft.Win32;

namespace FileGrid.Portable;

public partial class MainWindow : Window
{
    private readonly LayoutRepository _layouts = new();
    private readonly SettingsRepository _settingsRepo = new();
    private readonly SessionRepository _sessionRepo = new();
    private readonly FileMover _mover = new();
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(800) };

    private List<LayoutState> _layoutList = new();
    private LayoutState _layout = LayoutState.CreateDefault();
    private AppSettings _settings = new();
    private string? _selectedBoxId;
    private string? _selectedLinkId;
    private bool _linking;
    private string? _linkFromId;
    private ConflictChoice? _batchConflict;
    private bool _suppressLayoutComboChange;

    private static readonly string[] BoxColors =
        ["#3d5a80", "#5c4d7d", "#4a6741", "#8b4513", "#2f6f6f", "#6b3a3a", "#555555"];

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsRepo.Load();
        _mover.ResolveConflict = ResolveConflictAsync;
        _saveTimer.Tick += (_, _) => SaveCurrentLayout();

        Grid.LayoutChanged += _ => ScheduleSave();
        Grid.BoxSelected += id => { _selectedBoxId = id; _selectedLinkId = null; RefreshGrid(); };
        Grid.LinkSelected += id => { _selectedLinkId = id; _selectedBoxId = null; RefreshGrid(); };
        Grid.LinkBoxPicked += OnLinkBoxPicked;
        Grid.FilesDroppedOnBox += (boxId, paths) => _ = MoveToBoxById(boxId, paths);

        Loaded += (_, _) => LoadInitial();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void LoadInitial()
    {
        _layoutList = _layouts.List();
        var session = _sessionRepo.Load();
        LayoutState? active = null;
        if (!string.IsNullOrEmpty(session.ActiveLayoutId))
            active = _layouts.Load(session.ActiveLayoutId);
        if (active == null && _layoutList.Count > 0)
            active = _layouts.Load(_layoutList[0].Id);
        if (active == null)
        {
            active = LayoutState.CreateDefault();
            _layouts.Save(active);
            _layoutList = _layouts.List();
        }
        _layout = active;
        _sessionRepo.Save(new SessionMeta { ActiveLayoutId = _layout.Id });
        RefreshLayoutCombo();
        RefreshGrid();
    }

    private void RefreshLayoutCombo()
    {
        _suppressLayoutComboChange = true;
        try
        {
            _layoutList = _layouts.List();
            LayoutCombo.ItemsSource = null;
            LayoutCombo.ItemsSource = _layoutList;
            LayoutCombo.DisplayMemberPath = nameof(LayoutState.Name);
            LayoutCombo.SelectedItem = _layoutList.FirstOrDefault(l => l.Id == _layout.Id);
        }
        finally
        {
            _suppressLayoutComboChange = false;
        }
    }

    private void RefreshGrid() => Grid.Bind(_layout, _settings, _selectedBoxId, _selectedLinkId, _linking, _linkFromId);

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveCurrentLayout()
    {
        _saveTimer.Stop();
        _layouts.Save(_layout);
        _sessionRepo.Save(new SessionMeta { ActiveLayoutId = _layout.Id });
        var idx = _layoutList.FindIndex(l => l.Id == _layout.Id);
        if (idx >= 0) _layoutList[idx] = _layout;
    }

    private async Task<ConflictChoice> ResolveConflictAsync(MoveConflictInfo info)
    {
        if (_batchConflict.HasValue) return _batchConflict.Value;
        var result = await Dispatcher.InvokeAsync(() => ConflictWindow.Show(this, info));
        if (result == null) return ConflictChoice.Cancel;
        if (result.Value.applyToAll) _batchConflict = result.Value.choice;
        return result.Value.choice;
    }

    private async Task MoveToBoxById(string boxId, string[] paths)
    {
        var box = _layout.Boxes.FirstOrDefault(b => b.Id == boxId);
        if (box == null) return;
        _batchConflict = null;
        var results = await _mover.MovePathsAsync(box.FolderPath, paths);
        if (results.Any(r => r.Aborted))
            MessageBox.Show(this, "Move cancelled.", "File Grid", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnLinkBoxPicked(string boxId)
    {
        if (_linkFromId == null)
        {
            _linkFromId = boxId;
            _selectedBoxId = boxId;
            RefreshGrid();
            return;
        }
        if (_linkFromId == boxId)
        {
            _linkFromId = null;
            RefreshGrid();
            return;
        }
        var exists = _layout.Links.Any(l =>
            (l.FromBoxId == _linkFromId && l.ToBoxId == boxId) ||
            (l.FromBoxId == boxId && l.ToBoxId == _linkFromId));
        if (!exists)
        {
            var link = new BoxLink { FromBoxId = _linkFromId, ToBoxId = boxId };
            _layout.Links.Add(link);
            _selectedLinkId = link.Id;
            ScheduleSave();
        }
        _linkFromId = null;
        RefreshGrid();
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape && _linking)
        {
            _linking = false;
            _linkFromId = null;
            ConnectBtn.Content = "Connect boxes";
            RefreshGrid();
        }
        if ((e.Key == System.Windows.Input.Key.Delete || e.Key == System.Windows.Input.Key.Back) && _selectedLinkId != null)
        {
            _layout.Links.RemoveAll(l => l.Id == _selectedLinkId);
            _selectedLinkId = null;
            ScheduleSave();
            RefreshGrid();
            e.Handled = true;
        }
        if ((e.Key == System.Windows.Input.Key.Delete || e.Key == System.Windows.Input.Key.Back) && _selectedBoxId != null)
        {
            RemoveBoxInternal();
            e.Handled = true;
        }
    }

    private void LayoutCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLayoutComboChange) return;
        if (LayoutCombo.SelectedItem is not LayoutState picked || picked.Id == _layout.Id) return;
        SaveCurrentLayout();
        _layout = _layouts.Load(picked.Id) ?? picked;
        _selectedBoxId = null;
        _selectedLinkId = null;
        _sessionRepo.Save(new SessionMeta { ActiveLayoutId = _layout.Id });
        RefreshGrid();
    }

    private void NewLayout_Click(object sender, RoutedEventArgs e)
    {
        var name = NamePromptWindow.Show(this, "New layout", "Name for this sorting job:", $"Job {_layoutList.Count + 1}");
        if (string.IsNullOrWhiteSpace(name)) return;
        SaveCurrentLayout();
        var created = LayoutState.CreateDefault(name.Trim());
        _layouts.Save(created);
        _layout = created;
        _sessionRepo.Save(new SessionMeta { ActiveLayoutId = _layout.Id });
        RefreshLayoutCombo();
        RefreshGrid();
    }

    private void DeleteLayout_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, $"Delete \"{_layout.Name}\"?", "Delete layout", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        var deletedId = _layout.Id;
        _layouts.Delete(deletedId);
        _layoutList = _layouts.List().Where(l => l.Id != deletedId).ToList();
        if (_layoutList.Count == 0)
        {
            _layout = LayoutState.CreateDefault();
            _layouts.Save(_layout);
        }
        else
            _layout = _layouts.Load(_layoutList[0].Id) ?? _layoutList[0];
        _sessionRepo.Save(new SessionMeta { ActiveLayoutId = _layout.Id });
        _selectedBoxId = null;
        RefreshLayoutCombo();
        RefreshGrid();
    }

    private void AddBox_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog();
        if (dlg.ShowDialog() != true) return;
        var folder = dlg.FolderName;
        if (string.IsNullOrWhiteSpace(folder)) return;
        AddBoxesFromPaths([folder]);
    }

    private void DropFolderBtn_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void DropFolderBtn_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        var dirs = paths.Where(Directory.Exists).ToArray();
        if (dirs.Length > 0) AddBoxesFromPaths(dirs);
    }

    private void AddBoxesFromPaths(string[] folders)
    {
        var start = _layout.Boxes.Count;
        for (var i = 0; i < folders.Length; i++)
        {
            var folder = folders[i];
            _layout.Boxes.Add(new GridBox
            {
                Title = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar)),
                Color = BoxColors[(start + i) % BoxColors.Length],
                FolderPath = folder,
                Gx = 2 + (i % 4) * 2,
                Gy = 2 + (i / 4) * 2,
                Gw = 2,
                Gh = 2
            });
        }
        _selectedBoxId = _layout.Boxes.LastOrDefault()?.Id;
        ScheduleSave();
        RefreshGrid();
    }

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        _linking = !_linking;
        _linkFromId = null;
        ConnectBtn.Content = _linking ? "Connecting… (Esc)" : "Connect boxes";
        RefreshGrid();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settings, _layout) { Owner = this };
        if (dlg.ShowDialog() != true) return;
        _settings = dlg.Settings;
        _settingsRepo.Save(_settings);
        if (!string.IsNullOrWhiteSpace(dlg.RenamedLayoutName) && dlg.RenamedLayoutName != _layout.Name)
        {
            _layout.Name = dlg.RenamedLayoutName;
            SaveCurrentLayout();
            RefreshLayoutCombo();
        }
        if (!string.IsNullOrWhiteSpace(dlg.DuplicateLayoutName))
        {
            var idMap = _layout.Boxes.ToDictionary(b => b.Id, _ => Guid.NewGuid().ToString());
            var copy = new LayoutState
            {
                Name = dlg.DuplicateLayoutName,
                CellSize = _layout.CellSize,
                View = new ViewTransform { PanX = _layout.View.PanX, PanY = _layout.View.PanY, Zoom = _layout.View.Zoom },
                Boxes = _layout.Boxes.Select(b => new GridBox
                {
                    Id = idMap[b.Id],
                    Title = b.Title,
                    Color = b.Color,
                    FolderPath = b.FolderPath,
                    Gx = b.Gx, Gy = b.Gy, Gw = b.Gw, Gh = b.Gh
                }).ToList(),
                Links = _layout.Links
                    .Where(l => idMap.ContainsKey(l.FromBoxId) && idMap.ContainsKey(l.ToBoxId))
                    .Select(l => new BoxLink
                    {
                        FromBoxId = idMap[l.FromBoxId],
                        ToBoxId = idMap[l.ToBoxId],
                        Color = l.Color,
                        Thickness = l.Thickness
                    }).ToList()
            };
            _layouts.Save(copy);
            _layout = copy;
            _sessionRepo.Save(new SessionMeta { ActiveLayoutId = _layout.Id });
            RefreshLayoutCombo();
        }
        RefreshGrid();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var box = _layout.Boxes.FirstOrDefault(b => b.Id == _selectedBoxId);
        if (box == null) return;
        try { System.Diagnostics.Process.Start("explorer.exe", box.FolderPath); } catch { /* ignore */ }
    }

    private void RemoveBox_Click(object sender, RoutedEventArgs e) => RemoveBoxInternal();

    private void RemoveBoxInternal()
    {
        if (_selectedBoxId == null) return;
        _layout.Boxes.RemoveAll(b => b.Id == _selectedBoxId);
        _layout.Links.RemoveAll(l => l.FromBoxId == _selectedBoxId || l.ToBoxId == _selectedBoxId);
        _selectedBoxId = null;
        ScheduleSave();
        RefreshGrid();
    }

    private void RemoveLink_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedLinkId == null) return;
        _layout.Links.RemoveAll(l => l.Id == _selectedLinkId);
        _selectedLinkId = null;
        ScheduleSave();
        RefreshGrid();
    }
}
