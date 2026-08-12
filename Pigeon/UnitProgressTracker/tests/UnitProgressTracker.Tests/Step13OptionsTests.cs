using System.IO;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.ViewModels;

namespace UnitProgressTracker.Tests;

public class Step13OptionsTests
{
    [Fact]
    public void OptionsEditingAndReset_AreDetachedUntilApply()
    {
        var original = new DisplayPreferences();
        original.ListDisplay.SortMode = "skid";
        var status = new StatusState("built", "Built", "#123456");
        var options = new OptionsViewModel(original, new[] { status }, new ThemeOptions { ThemeName = "Dark" });

        options.Preferences.ListDisplay.SortMode = "type";
        options.StatusStates[0].Name = "Changed";
        options.ApplicationThemeOptions.ThemeName = "Light";

        Assert.Equal("skid", original.ListDisplay.SortMode);
        Assert.Equal("Built", status.Name);

        options.ResetDefaultsCommand.Execute(null);

        Assert.Equal("skid", original.ListDisplay.SortMode);
        Assert.Equal("Dark", options.ApplicationThemeOptions.ThemeName);
    }

    [Fact]
    public void ApplyOptions_UpdatesProjectRuntimeAndApplicationScopesSeparately()
    {
        string dataRoot = Path.Combine(Path.GetTempPath(), $"upt-step13-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataRoot);
        try
        {
            using var scope = AppSettingsService.UseDataRoot(dataRoot);
            var vm = new MainViewModel();
            bool? grid = null;
            bool? labels = null;
            bool? wireframe = null;
            double? opacity = null;
            vm.RequestSetSkidGrid = value => grid = value;
            vm.RequestSetSkidLabels = value => labels = value;
            vm.RequestSetWireframe = value => wireframe = value;
            vm.RequestSetOpacity = value => opacity = value;

            var options = new OptionsViewModel(vm.Preferences, vm.StatusStates, new ThemeOptions());
            options.Preferences.ListDisplay.SortMode = "type";
            options.Preferences.ViewerOptions.ShowGrid = false;
            options.Preferences.ViewerOptions.ShowSkidLabels = false;
            options.Preferences.ViewerOptions.WireframeVisible = false;
            options.Preferences.ViewerOptions.SurfaceOpacity = 0.55;
            options.ApplicationThemeOptions.AutoSyncWithSystemTheme = false;
            options.ApplicationThemeOptions.ThemeName = "Light";

            vm.ApplyOptions(options);

            Assert.Equal("type", vm.Preferences.ListDisplay.SortMode);
            Assert.False(grid);
            Assert.False(labels);
            Assert.False(wireframe);
            Assert.Equal(0.55, opacity);
            Assert.True(vm.IsDirty);
            Assert.Equal("Light", AppSettingsService.LoadSettings().ThemeOptions.ThemeName);
            Assert.Equal("Dark", vm.Preferences.ThemeOptions.ThemeName);
        }
        finally
        {
            if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, recursive: true);
        }
    }

    [Fact]
    public void CameraChange_IsProjectOwnedDirtyTrackedAndRoundTrips()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"upt-step13-camera-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string projectPath = Path.Combine(directory, "camera.uptproj");
        try
        {
            var vm = new MainViewModel();
            var camera = new CameraStateModel
            {
                PositionX = 10,
                PositionY = 20,
                PositionZ = 30,
                TargetX = 1,
                TargetY = 2,
                TargetZ = 3,
                UpY = 1
            };

            vm.UpdateCameraState(camera);
            Assert.True(vm.IsDirty);
            Assert.Equal(10, vm.ProjectState.Camera.PositionX);

            Assert.True(vm.SaveProjectInternal(projectPath));
            Assert.False(vm.IsDirty);

            var reopened = new MainViewModel();
            reopened.LoadProjectFromFile(projectPath);
            Assert.Equal(10, reopened.ProjectState.Camera.PositionX);
            Assert.Equal(3, reopened.ProjectState.Camera.TargetZ);
            Assert.False(reopened.IsDirty);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ProjectSerialization_DoesNotContainApplicationThemePreferences()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"upt-step13-project-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string projectPath = Path.Combine(directory, "scope.uptproj");
        try
        {
            var project = new ProjectStateModel();
            project.Preferences.ThemeOptions.ThemeName = "Light";
            ProjectSerializer.SaveAtomic(projectPath, project);

            string json = File.ReadAllText(projectPath);
            Assert.DoesNotContain("themeOptions", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
