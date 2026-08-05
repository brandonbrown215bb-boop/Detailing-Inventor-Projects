using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Wpf.ViewModels;

namespace UnitProgressTracker.Wpf;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = ViewModel;

        // Wire viewport callbacks
        ViewModel.RequestViewportRefresh = Refresh3DViewport;
        ViewModel.RequestHighlightSurface = sn => Viewport3D.HighlightSurface(sn);
        ViewModel.RequestSetWireframe = v => Viewport3D.SetWireframeVisible(v);
        ViewModel.RequestSetOpacity = o => Viewport3D.SetGlobalOpacity(o);
        ViewModel.RequestSetSurfaceVisibility = (hidden, sn) => Viewport3D.SetSurfaceVisibility(hidden, sn);

        Viewport3D.SurfacePicked += OnSurfacePickedIn3D;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.ConfirmUnsavedChanges())
        {
            e.Cancel = true;
        }
    }

    private void OnSurfacePickedIn3D(string surfaceNumber)
    {
        ViewModel.SelectSurfaceByNumber(surfaceNumber);
    }

    private void Refresh3DViewport()
    {
        Viewport3D.LoadSurfaces(ViewModel.Surfaces, ViewModel.GetStatusColor);
    }

    private async void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose folder containing unit 391Z surfaces"
        };
        if (dialog.ShowDialog() == true)
        {
            await ViewModel.LoadFolderAsync(dialog.FolderName);
        }
    }

    private async void OnRescanClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.HasFolder)
        {
            await ViewModel.LoadFolderAsync(ViewModel.CurrentFolderPath!);
        }
    }

    private void OnLoadDemoDataClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Surfaces.Clear();

        var demoSurfaces = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "391Z010142-0001",
                SurfaceUnitSide = "Roof",
                StateId = "done",
                Checklist = new System.Collections.Generic.Dictionary<string, bool>
                {
                    ["Verified dimensions"] = true,
                    ["Verified material"] = false,
                    ["Verified openings"] = false,
                    ["Paperwork complete"] = false
                },
                Boxes = new List<GeometryBox> { new(0, 120, 0, 140, 4, 80) }
            },
            new SurfaceModel
            {
                SurfaceNumber = "391Z010142-0002",
                SurfaceUnitSide = "Left Wall",
                StateId = "built",
                Checklist = new System.Collections.Generic.Dictionary<string, bool>
                {
                    ["Verified dimensions"] = true,
                    ["Verified material"] = true,
                    ["Verified openings"] = false,
                    ["Paperwork complete"] = false
                },
                Boxes = new List<GeometryBox> { new(0, 0, 0, 140, 120, 2) }
            },
            new SurfaceModel
            {
                SurfaceNumber = "391Z010142-0003",
                SurfaceUnitSide = "Right Wall",
                StateId = "corrected",
                Checklist = new System.Collections.Generic.Dictionary<string, bool>
                {
                    ["Verified dimensions"] = false,
                    ["Verified material"] = false,
                    ["Verified openings"] = false,
                    ["Paperwork complete"] = false
                },
                Boxes = new List<GeometryBox> { new(0, 0, 78, 140, 120, 2) }
            },
            new SurfaceModel
            {
                SurfaceNumber = "391Z010142-0004",
                SurfaceUnitSide = "Unit Base",
                StateId = "associated",
                Checklist = new System.Collections.Generic.Dictionary<string, bool>
                {
                    ["Verified dimensions"] = true,
                    ["Verified material"] = true,
                    ["Verified openings"] = true,
                    ["Paperwork complete"] = false
                },
                Boxes = new List<GeometryBox> { new(0, 0, 0, 140, 10, 80) }
            }
        };

        foreach (var surf in demoSurfaces) ViewModel.Surfaces.Add(surf);

        var demoBom = new List<BomRow>
        {
            new BomRow { PartNumber = "391-0101", Quantity = "1", Unit = "EA", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel Assembly", ExtDescription = "16 GA STL GALV" },
            new BomRow { PartNumber = "391-0102", Quantity = "1", Unit = "EA", Skid = "1 [FR-MB]", Segment = "FR", Description = "Filter Rack Casing", ExtDescription = "2" },
            new BomRow { PartNumber = "391-0103", Quantity = "1", Unit = "EA", Skid = "1 [FR-MB]", Segment = "<--", Description = "Coil Panel Assembly" },
            new BomRow { PartNumber = "391-0104", Quantity = "2", Unit = "EA", Skid = "1 [FR-MB]", Segment = "MB", Description = "SQ Custom Door Assembly" }
        };

        ViewModel.LoadBomRows(demoBom);
        ViewModel.StatusMessage = "Loaded demo 3D surfaces and BOM shell data.";
        ViewModel.MarkDirty();
        Refresh3DViewport();
    }

    private void OnSurfacesTabClick(object sender, RoutedEventArgs e) => ViewModel.SelectedTabIndex = 0;

    private void OnBomTabClick(object sender, RoutedEventArgs e) => ViewModel.SelectedTabIndex = 1;

    private void OnSetShellRootClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose shell export root folder" };
        if (dialog.ShowDialog() == true)
        {
            ViewModel.ShellRootPath = dialog.FolderName;
            ViewModel.MarkDirty();
            if (ViewModel.CurrentBomPlan != null)
            {
                ViewModel.LoadBomRows(ViewModel.CurrentBomPlan.Entries.ConvertAll(e => new BomRow
                {
                    PartNumber = e.PartNumber,
                    Quantity = e.Quantity,
                    Unit = e.Unit,
                    Skid = e.Skid,
                    Segment = e.Segment,
                    Description = e.Description,
                    ExtDescription = e.ExtDescription
                }));
            }
        }
    }

    private void OnCreateShellFoldersClick(object sender, RoutedEventArgs e) => ViewModel.CreateShellFolders();

    private void OnFitViewClick(object sender, RoutedEventArgs e) => Refresh3DViewport();

    private void OnResetCameraClick(object sender, RoutedEventArgs e) => Refresh3DViewport();

    private void OnStatusSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StatusComboBox.SelectedValue is string stateId)
        {
            ViewModel.UpdateSelectedSurfaceStatus(stateId);
        }
    }

    private void OnOpacitySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Viewport3D.SetGlobalOpacity(e.NewValue);
    }

    private void OnAddChecklistItemClick(object sender, RoutedEventArgs e)
    {
        string label = NewChecklistItemBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(label) || ViewModel.SelectedSurface == null) return;

        ViewModel.AddChecklistItemCommand.Execute(label);
        NewChecklistItemBox.Clear();
    }

    private void OnNewChecklistItemKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnAddChecklistItemClick(sender, e);
            e.Handled = true;
        }
    }
}