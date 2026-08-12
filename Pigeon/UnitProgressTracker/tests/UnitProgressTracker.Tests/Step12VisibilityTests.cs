using System.ComponentModel;
using System.IO;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Wpf.ViewModels;

namespace UnitProgressTracker.Tests;

public class Step12VisibilityTests
{
    [Fact]
    public void SurfaceVisibility_NotifiesBoundConsumers()
    {
        var surface = new SurfaceModel();
        string? changedProperty = null;
        surface.PropertyChanged += (_, args) => changedProperty = args.PropertyName;

        surface.IsHidden = true;

        Assert.Equal(nameof(SurfaceModel.IsHidden), changedProperty);
    }

    [Fact]
    public void MixedGroup_HasDefinedState_AndToggleShowsAll()
    {
        var visible = new SurfaceModel { SurfaceNumber = "A" };
        var hidden = new SurfaceModel { SurfaceNumber = "B", IsHidden = true };
        var group = new SurfaceGroupViewModel();
        group.Surfaces.Add(visible);
        group.Surfaces.Add(hidden);

        Assert.Null(group.IsGroupVisible);
        Assert.Equal("Mixed - Show All", group.ToggleButtonText);

        group.ToggleGroupVisibilityCommand.Execute(null);

        Assert.True(group.IsGroupVisible);
        Assert.All(group.Surfaces, surface => Assert.False(surface.IsHidden));
    }

    [Fact]
    public void IndividualGroupAndShowAll_UpdateCountsAndDirtyStateImmediately()
    {
        var vm = new MainViewModel();
        var first = new SurfaceModel { SurfaceNumber = "SURF-A" };
        var second = new SurfaceModel { SurfaceNumber = "SURF-B" };
        vm.Surfaces.Add(first);
        vm.Surfaces.Add(second);
        vm.GroupMode = "flat";
        vm.ClearDirty();

        vm.ToggleSurfaceVisibilityCommand.Execute(first);

        Assert.True(first.IsHidden);
        Assert.Equal(1, vm.ActiveSurfacesCount);
        Assert.Equal(1, vm.HiddenSurfacesCount);
        Assert.True(vm.IsDirty);
        Assert.True(vm.ProjectState.Surfaces["SURF-A"].Hidden);

        vm.ClearDirty();
        vm.GroupedSurfaces.Single().ToggleGroupVisibilityCommand.Execute(null);

        Assert.All(vm.Surfaces, surface => Assert.False(surface.IsHidden));
        Assert.Equal(2, vm.ActiveSurfacesCount);
        Assert.True(vm.IsDirty);

        first.IsHidden = true;
        vm.ClearDirty();
        vm.ShowAllSurfacesCommand.Execute(null);
        Assert.False(first.IsHidden);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void Visibility_SaveAndReopen_RestoresHiddenState()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"upt-step12-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string projectPath = Path.Combine(directory, "visibility.uptproj");
        try
        {
            var vm = new MainViewModel();
            var surface = new SurfaceModel { SurfaceNumber = "SURF-HIDDEN", IsHidden = true };
            surface.Boxes.Add(new GeometryBox(0, 0, 0, 10, 10, 1));
            vm.Surfaces.Add(surface);

            Assert.True(vm.SaveProjectInternal(projectPath));

            var reopened = new MainViewModel();
            reopened.LoadProjectFromFile(projectPath);
            Assert.True(Assert.Single(reopened.Surfaces).IsHidden);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
