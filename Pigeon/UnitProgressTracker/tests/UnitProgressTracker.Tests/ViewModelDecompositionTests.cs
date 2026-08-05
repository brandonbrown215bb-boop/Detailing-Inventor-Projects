using System;
using System.Collections.Generic;
using System.Threading;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class ViewModelDecompositionTests
{
    [Fact]
    public void ScanProgressViewModel_InitialState_IsReady()
    {
        var vm = new ScanProgressViewModel();
        Assert.False(vm.IsScanRunning);
        Assert.Equal(0, vm.ScanProgress);
        Assert.Equal("Ready", vm.StatusText);
    }

    [Fact]
    public void ScanProgressViewModel_StartNewScan_ResetsProgressAndReturnsToken()
    {
        var vm = new ScanProgressViewModel();
        var token = vm.StartNewScan();

        Assert.True(vm.IsScanRunning);
        Assert.Equal(0, vm.ScanProgress);
        Assert.False(token.IsCancellationRequested);

        vm.ReportProgress(50.0, "Halfway done");
        Assert.Equal(50.0, vm.ScanProgress);
        Assert.Equal("Halfway done", vm.StatusText);

        vm.CompleteScan(12);
        Assert.False(vm.IsScanRunning);
        Assert.Equal(100, vm.ScanProgress);
        Assert.Contains("12", vm.StatusText);
    }

    [Fact]
    public void BomFilterViewModel_Matches_FiltersBySearchAndVisibility()
    {
        var filter = new BomFilterViewModel
        {
            SearchQuery = "ROOF",
            ShowHiddenSurfaces = false
        };

        var visibleRoof = new SurfaceModel { SurfaceNumber = "SURF-101", SurfaceType = "Roof Panel", IsHidden = false };
        var hiddenRoof = new SurfaceModel { SurfaceNumber = "SURF-102", SurfaceType = "Roof Panel", IsHidden = true };
        var visibleWall = new SurfaceModel { SurfaceNumber = "SURF-103", SurfaceType = "Wall Panel", IsHidden = false };

        Assert.True(filter.Matches(visibleRoof));
        Assert.False(filter.Matches(hiddenRoof));
        Assert.False(filter.Matches(visibleWall));
    }

    [Fact]
    public void ProjectNavigationViewModel_AddRecentProject_DeduplicatesAndLimitsCount()
    {
        var nav = new ProjectNavigationViewModel();
        
        for (int i = 0; i < 15; i++)
        {
            nav.AddRecentProject($@"C:\Projects\Project{i}.uptproj", DateTime.UtcNow);
        }

        Assert.Equal(10, nav.RecentProjects.Count);
        Assert.Equal($@"C:\Projects\Project14.uptproj", nav.RecentProjects[0].FilePath);

        // Add duplicate
        nav.AddRecentProject($@"C:\Projects\Project14.uptproj", DateTime.UtcNow);
        Assert.Equal(10, nav.RecentProjects.Count);
    }
}
