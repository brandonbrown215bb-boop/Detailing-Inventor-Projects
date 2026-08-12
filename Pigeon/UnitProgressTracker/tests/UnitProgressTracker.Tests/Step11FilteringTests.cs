using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Wpf.ViewModels;

namespace UnitProgressTracker.Tests;

public class Step11FilteringTests
{
    [Fact]
    public void StatusFilter_TogglesById_ComposesWithSearchVisibilityAndGrouping_WithoutDirtying()
    {
        var vm = new MainViewModel();
        var builtVisible = Surface("SURF-1002", "built", false, 2, "Roof");
        var builtHidden = Surface("SURF-1001", "built", true, 1, "Roof");
        var current = Surface("SURF-1003", "current", false, 1, "Floor");
        vm.Surfaces.Add(builtVisible);
        vm.Surfaces.Add(builtHidden);
        vm.Surfaces.Add(current);
        vm.SelectedSurface = current;
        vm.ClearDirty();

        vm.ToggleStatusFilter("built");

        Assert.Equal("built", vm.SelectedStatusFilterId);
        Assert.Null(vm.SelectedSurface);
        Assert.Equal(2, vm.FilteredSurfacesCount);
        Assert.All(vm.GroupedSurfaces.SelectMany(group => group.Surfaces), surface => Assert.Equal("built", surface.StateId));
        Assert.False(vm.IsDirty);

        vm.SearchText = "1001";
        vm.SurfaceVisibilityFilter = "hidden";
        vm.GroupMode = "flat";
        vm.ClearDirty(); // GroupMode is project-owned; filtering itself remains session-only.

        Assert.Single(vm.GetFilteredSurfaces());
        Assert.Same(builtHidden, vm.GetFilteredSurfaces()[0]);
        Assert.False(vm.IsDirty);

        vm.ToggleStatusFilter("built");
        Assert.Null(vm.SelectedStatusFilterId);
        Assert.Single(vm.GetFilteredSurfaces());
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void Filtering_UpdatesCountEmptyStateAndViewportProjection()
    {
        var vm = new MainViewModel();
        vm.Surfaces.Add(Surface("SURF-2001", "current", false, 1, "Roof"));
        vm.RebuildGroupedSurfaces();
        vm.ClearDirty();

        vm.SearchText = "does-not-exist";

        Assert.Equal(0, vm.FilteredSurfacesCount);
        Assert.Equal("0 shown", vm.FilteredSurfaceCountText);
        Assert.Equal("No surfaces match the current filters.", vm.FilteredEmptyStateMessage);
        Assert.Empty(vm.GroupedSurfaces);
        Assert.Empty(vm.GetFilteredSurfaces());
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void SortMode_OrdersTheFilteredSurfaceSequence()
    {
        var vm = new MainViewModel();
        vm.Surfaces.Add(Surface("SURF-3003", "built", false, 2, "Roof"));
        vm.Surfaces.Add(Surface("SURF-3001", "built", false, 1, "Wall"));
        vm.Surfaces.Add(Surface("SURF-3002", "built", false, 1, "Roof"));

        vm.SortMode = "skid-type";

        Assert.Equal(
            new[] { "SURF-3002", "SURF-3001", "SURF-3003" },
            vm.GetFilteredSurfaces().Select(surface => surface.SurfaceNumber));
    }

    private static SurfaceModel Surface(string number, string status, bool hidden, int skid, string type) => new()
    {
        SurfaceNumber = number,
        DisplayNumber = number,
        StateId = status,
        IsHidden = hidden,
        SkidId = skid,
        SurfaceType = type,
        PartNumber = $"PN-{number}"
    };
}
