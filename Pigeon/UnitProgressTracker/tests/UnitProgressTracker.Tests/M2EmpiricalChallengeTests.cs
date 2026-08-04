using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class M2EmpiricalChallengeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsBackupPath;
    private readonly string _realSettingsPath;
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public M2EmpiricalChallengeTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), "UPT_Empirical_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _realSettingsPath = AppSettingsService.GetSettingsFilePath();
        _settingsBackupPath = Path.Combine(Path.GetTempPath(), "UPT_settings_backup_" + Guid.NewGuid().ToString("N") + ".json");

        if (File.Exists(_realSettingsPath))
        {
            try
            {
                File.Copy(_realSettingsPath, _settingsBackupPath, true);
            }
            catch { }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        // Restore original settings if backed up
        if (File.Exists(_settingsBackupPath))
        {
            try
            {
                string dir = Path.GetDirectoryName(_realSettingsPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.Copy(_settingsBackupPath, _realSettingsPath, true);
                File.Delete(_settingsBackupPath);
            }
            catch { }
        }
    }

    // =========================================================================
    // SECTION 1: IsDirty Tracking Lifecycle across ALL UI Actions
    // =========================================================================

    [Fact]
    public void IsDirty_Lifecycle_InitialState_IsFalse()
    {
        var vm = new MainViewModel();
        Assert.False(vm.IsDirty);
        Assert.DoesNotContain("*", vm.WindowTitle);
    }

    [Fact]
    public void IsDirty_Lifecycle_StatusEdits_SetsDirty()
    {
        var vm = new MainViewModel();
        var surf = new SurfaceModel { SurfaceNumber = "SURF-1001", StateId = "current" };
        vm.Surfaces.Add(surf);
        vm.SelectedSurface = surf;
        Assert.False(vm.IsDirty);

        vm.UpdateSelectedSurfaceStatus("built");

        Assert.True(vm.IsDirty);
        Assert.Contains("*", vm.WindowTitle);
    }

    [Fact]
    public void IsDirty_Lifecycle_ChecklistModifications_SetsDirty()
    {
        var vm = new MainViewModel();
        var surf = new SurfaceModel { SurfaceNumber = "SURF-1001" };
        vm.Surfaces.Add(surf);
        vm.SelectedSurface = surf;
        Assert.False(vm.IsDirty);

        vm.UpdateChecklistItem("Visual Inspection", true);

        Assert.True(vm.IsDirty);
        Assert.True(surf.Checklist["Visual Inspection"]);
    }

    [Fact]
    public void IsDirty_Lifecycle_ChecklistItemViewModel_Toggle_SetsDirty()
    {
        var vm = new MainViewModel();
        var surf = new SurfaceModel { SurfaceNumber = "SURF-1001" };
        surf.Checklist["Torque Check"] = false;
        vm.Surfaces.Add(surf);
        vm.SelectedSurface = surf;
        Assert.False(vm.IsDirty);

        var itemVm = vm.ChecklistItems.First(i => i.Key == "Torque Check");
        itemVm.IsChecked = true;

        Assert.True(vm.IsDirty);
        Assert.True(surf.Checklist["Torque Check"]);
    }

    [Fact]
    public void IsDirty_Lifecycle_NotesTextChanges_SetsDirtyOnlyOnActualChange()
    {
        var vm = new MainViewModel();
        var surf = new SurfaceModel { SurfaceNumber = "SURF-1001", Notes = "Original" };
        vm.Surfaces.Add(surf);
        vm.SelectedSurface = surf;
        Assert.False(vm.IsDirty);

        // Setting same notes should not trigger dirty
        vm.SelectedSurfaceNotes = "Original";
        Assert.False(vm.IsDirty);

        // Setting new notes triggers dirty
        vm.SelectedSurfaceNotes = "Modified note";
        Assert.True(vm.IsDirty);
        Assert.Equal("Modified note", surf.Notes);
    }

    [Fact]
    public void IsDirty_Lifecycle_SurfaceVisibilityToggling_SetsDirty()
    {
        var vm = new MainViewModel();
        var surf = new SurfaceModel { SurfaceNumber = "SURF-1001", IsHidden = false };
        vm.Surfaces.Add(surf);
        Assert.False(vm.IsDirty);

        vm.ToggleSurfaceVisibilityCommand.Execute(surf);

        Assert.True(vm.IsDirty);
        Assert.True(surf.IsHidden);
    }

    [Fact]
    public void IsDirty_Lifecycle_BOMRowEdits_AddAndDelete_SetsDirty()
    {
        var vm = new MainViewModel();
        Assert.False(vm.IsDirty);

        vm.AddBomRowCommand.Execute(null);
        Assert.True(vm.IsDirty);
        Assert.Single(vm.BomEntries);

        vm.ClearDirty();
        Assert.False(vm.IsDirty);

        vm.SelectedBomEntry = vm.BomEntries.First();
        vm.DeleteBomRowCommand.Execute(null);
        Assert.True(vm.IsDirty);
        Assert.Empty(vm.BomEntries);
    }

    [Fact]
    public void IsDirty_Lifecycle_SaveAndLoad_ClearsDirty()
    {
        string projectPath = Path.Combine(_tempDir, "test_dirty.uptproj");
        var vm = new MainViewModel();
        var surf = new SurfaceModel { SurfaceNumber = "SURF-1001", StateId = "current" };
        vm.Surfaces.Add(surf);
        vm.SelectedSurface = surf;
        vm.UpdateSelectedSurfaceStatus("built");
        Assert.True(vm.IsDirty);

        bool saved = vm.SaveProjectInternal(projectPath);
        Assert.True(saved);
        Assert.False(vm.IsDirty);
        Assert.DoesNotContain("*", vm.WindowTitle);

        // Mutate again
        vm.UpdateSelectedSurfaceStatus("corrected");
        Assert.True(vm.IsDirty);

        // Load project clears dirty
        vm.LoadProjectFromFile(projectPath);
        Assert.False(vm.IsDirty);
    }

    // =========================================================================
    // SECTION 2: Auto-Save Background Timer Behavior
    // =========================================================================

    [Fact]
    public void AutoSave_Tick_SavesDirtyProjectWithValidPath()
    {
        string projectPath = Path.Combine(_tempDir, "autosave_test.uptproj");
        var vm = new MainViewModel();
        vm.CurrentProjectPath = projectPath;
        var surf = new SurfaceModel { SurfaceNumber = "SURF-1001" };
        vm.Surfaces.Add(surf);
        vm.SelectedSurface = surf;
        vm.UpdateSelectedSurfaceStatus("done");
        Assert.True(vm.IsDirty);

        // Invoke private handler via reflection
        var tickMethod = typeof(MainViewModel).GetMethod("OnAutoSaveTimerTick", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(tickMethod);
        tickMethod.Invoke(vm, new object?[] { null, EventArgs.Empty });

        _output.WriteLine($"StatusMessage: {vm.StatusMessage}");
        Assert.False(vm.IsDirty);
        Assert.True(File.Exists(projectPath));
        Assert.Contains("[Auto-Save]", vm.StatusMessage);
    }

    [Fact]
    public void AutoSave_Tick_DoesNotSaveWhenClean()
    {
        string projectPath = Path.Combine(_tempDir, "autosave_clean.uptproj");
        var vm = new MainViewModel();
        vm.CurrentProjectPath = projectPath;
        Assert.False(vm.IsDirty);

        var tickMethod = typeof(MainViewModel).GetMethod("OnAutoSaveTimerTick", BindingFlags.NonPublic | BindingFlags.Instance);
        tickMethod!.Invoke(vm, new object?[] { null, EventArgs.Empty });

        Assert.False(File.Exists(projectPath));
    }

    [Fact]
    public void AutoSave_Tick_DoesNotSaveWhenProjectPathIsNull()
    {
        var vm = new MainViewModel();
        vm.CurrentProjectPath = null;
        var surf = new SurfaceModel { SurfaceNumber = "SURF-1001" };
        vm.Surfaces.Add(surf);
        vm.SelectedSurface = surf;
        vm.UpdateSelectedSurfaceStatus("done");
        Assert.True(vm.IsDirty);

        var tickMethod = typeof(MainViewModel).GetMethod("OnAutoSaveTimerTick", BindingFlags.NonPublic | BindingFlags.Instance);
        tickMethod!.Invoke(vm, new object?[] { null, EventArgs.Empty });

        // Remains dirty because no path was set to save to
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void AutoSave_Tick_DoesNotSaveDuringActiveScanning()
    {
        string projectPath = Path.Combine(_tempDir, "autosave_scanning.uptproj");
        var vm = new MainViewModel();
        vm.CurrentProjectPath = projectPath;
        vm.IsScanning = true;
        vm.MarkDirty();

        var tickMethod = typeof(MainViewModel).GetMethod("OnAutoSaveTimerTick", BindingFlags.NonPublic | BindingFlags.Instance);
        tickMethod!.Invoke(vm, new object?[] { null, EventArgs.Empty });

        Assert.True(vm.IsDirty);
        Assert.False(File.Exists(projectPath));
    }

    // =========================================================================
    // SECTION 3: MRU Settings Persistence (%APPDATA%\UnitProgressTracker\settings.json)
    // =========================================================================

    [Fact]
    public void MRU_Settings_PathNormalization_And_CaseInsensitiveDeduplication()
    {
        AppSettingsService.ClearRecentProjects();

        string pathRaw = Path.Combine(_tempDir, "subdir", "..", "mru1.uptproj");
        string pathNormalized = Path.GetFullPath(pathRaw);
        string pathUpper = pathNormalized.ToUpperInvariant();

        AppSettingsService.AddRecentProject(pathRaw);
        AppSettingsService.AddRecentProject(pathUpper);

        var settings = AppSettingsService.LoadSettings();
        Assert.Single(settings.RecentProjects);
        Assert.Equal(pathNormalized, settings.RecentProjects[0], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void MRU_Settings_CapsAtMax10Items()
    {
        AppSettingsService.ClearRecentProjects();

        for (int i = 1; i <= 15; i++)
        {
            string p = Path.Combine(_tempDir, $"project_{i:D2}.uptproj");
            AppSettingsService.AddRecentProject(p);
        }

        var settings = AppSettingsService.LoadSettings();
        Assert.Equal(10, settings.RecentProjects.Count);
        // Latest added item should be at top (index 0)
        string expectedTop = Path.GetFullPath(Path.Combine(_tempDir, "project_15.uptproj"));
        Assert.Equal(expectedTop, settings.RecentProjects[0], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void MRU_Settings_ClearRecentProjects_EmptiesList()
    {
        AppSettingsService.AddRecentProject(Path.Combine(_tempDir, "p1.uptproj"));
        var vm = new MainViewModel();
        Assert.True(vm.HasRecentProjects);

        vm.ClearRecentProjectsCommand.Execute(null);

        Assert.False(vm.HasRecentProjects);
        Assert.Empty(vm.RecentProjects);
        var settings = AppSettingsService.LoadSettings();
        Assert.Empty(settings.RecentProjects);
        Assert.Null(settings.LastOpenedProject);
    }

    // =========================================================================
    // SECTION 4: ConfirmUnsavedChanges & WPF Menu Commands
    // =========================================================================

    [Fact]
    public void ConfirmUnsavedChanges_WhenClean_ReturnsTrueImmediately()
    {
        var vm = new MainViewModel();
        Assert.False(vm.IsDirty);

        bool confirmed = vm.ConfirmUnsavedChanges();
        Assert.True(confirmed);
    }

    [Fact]
    public void FileMenuCommands_ExecutionAndCanExecuteState()
    {
        AppSettingsService.ClearRecentProjects();
        var vm = new MainViewModel();

        Assert.NotNull(vm.NewProjectCommand);
        Assert.NotNull(vm.OpenProjectCommand);
        Assert.NotNull(vm.SaveProjectCommand);
        Assert.NotNull(vm.SaveProjectAsCommand);
        Assert.NotNull(vm.ClearRecentProjectsCommand);
        Assert.NotNull(vm.ExitCommand);

        Assert.True(vm.NewProjectCommand.CanExecute(null));
        Assert.True(vm.OpenProjectCommand.CanExecute(null));
        Assert.True(vm.SaveProjectCommand.CanExecute(null));
        Assert.True(vm.SaveProjectAsCommand.CanExecute(null));
        Assert.True(vm.ExitCommand.CanExecute(null));

        // Clear recent projects command disabled when list is empty
        Assert.False(vm.ClearRecentProjectsCommand.CanExecute(null));

        // Add recent project
        AppSettingsService.AddRecentProject(Path.Combine(_tempDir, "recent.uptproj"));
        vm.RefreshRecentProjects();
        Assert.True(vm.ClearRecentProjectsCommand.CanExecute(null));
    }
}
