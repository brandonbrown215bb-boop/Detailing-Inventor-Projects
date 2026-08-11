using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Step6BomIsolationTests
{
    [Fact]
    public void ManualBomEdits_UpdateProjectStateBom_AndSurviveSaveAndReopen()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"step6_bom_{Guid.NewGuid():N}.uptproj");
        try
        {
            var vm = new MainViewModel();
            var initialRows = new List<BomRow>
            {
                new BomRow { PartNumber = "391-1001", Skid = "1 [FR-MB]", Segment = "MB", Description = "Panel A" },
                new BomRow { PartNumber = "391-1002", Skid = "1 [FR-MB]", Segment = "MB", Description = "Panel B" }
            };

            vm.LoadBomRows(initialRows);
            Assert.NotNull(vm.ProjectState.Bom);
            Assert.Equal(2, vm.ProjectState.Bom.KeptCount);

            // Simulate inline addition
            vm.ExecuteAddBomRow();
            Assert.Equal(3, vm.BomEntries.Count);
            Assert.NotNull(vm.ProjectState.Bom);
            Assert.Equal(3, vm.ProjectState.Bom.KeptCount);

            // Simulate inline deletion
            var entryToDelete = vm.BomEntries.FirstOrDefault(e => e.PartNumber == "391-1002");
            Assert.NotNull(entryToDelete);
            vm.SelectedBomEntry = entryToDelete;
            vm.ExecuteDeleteBomRow();

            Assert.Equal(2, vm.BomEntries.Count);
            Assert.NotNull(vm.ProjectState.Bom);
            Assert.Equal(2, vm.ProjectState.Bom.KeptCount);

            // Save project
            bool saved = vm.SaveProjectInternal(tempFile);
            Assert.True(saved);

            // Load into a fresh VM
            var vm2 = new MainViewModel();
            vm2.LoadProjectFromFile(tempFile);

            Assert.NotNull(vm2.ProjectState.Bom);
            Assert.Equal(2, vm2.BomEntries.Count);
            Assert.Contains(vm2.BomEntries, e => e.PartNumber == "391-1001");
            Assert.Contains(vm2.BomEntries, e => e.PartNumber == "391-NEW");
            Assert.DoesNotContain(vm2.BomEntries, e => e.PartNumber == "391-1002");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ProjectSwitching_ClearsBOMState_WhenLoadingProjectWithNoBOM()
    {
        string projectWithBom = Path.Combine(Path.GetTempPath(), $"proj_bom_{Guid.NewGuid():N}.uptproj");
        string projectNoBom = Path.Combine(Path.GetTempPath(), $"proj_nobom_{Guid.NewGuid():N}.uptproj");

        try
        {
            // Project 1: Has BOM
            var vm = new MainViewModel();
            vm.LoadBomRows(new List<BomRow>
            {
                new BomRow { PartNumber = "391-8888", Skid = "1 [FF-MB]", Segment = "FF", Description = "Coil Shield" }
            });
            vm.SaveProjectInternal(projectWithBom);

            // Project 2: No BOM
            var project2State = new ProjectStateModel { SourceFolder = @"C:\NoBomUnit" };
            ProjectSerializer.SaveAtomic(projectNoBom, project2State);

            // Open Project 1
            var vmSession = new MainViewModel();
            vmSession.LoadProjectFromFile(projectWithBom);
            Assert.Single(vmSession.BomEntries);
            Assert.Equal("391-8888", vmSession.BomEntries[0].PartNumber);

            // Switch to Project 2 (No BOM)
            vmSession.LoadProjectFromFile(projectNoBom);

            Assert.Empty(vmSession.BomEntries);
            Assert.Empty(vmSession.MisplacedRows);
            Assert.Null(vmSession.ProjectState.Bom);
            Assert.Equal("All Skids", vmSession.SelectedSkidFilter);
            Assert.Equal("All Segments", vmSession.SelectedSegmentFilter);
        }
        finally
        {
            if (File.Exists(projectWithBom)) File.Delete(projectWithBom);
            if (File.Exists(projectNoBom)) File.Delete(projectNoBom);
        }
    }

    [Fact]
    public void ShellFolderCreation_IncludesManualEntries_AndExcludesDeletedEntries()
    {
        string tempShellRoot = Path.Combine(Path.GetTempPath(), $"shell_export_{Guid.NewGuid():N}");
        try
        {
            var vm = new MainViewModel();
            vm.ShellRootPath = tempShellRoot;
            Directory.CreateDirectory(tempShellRoot);

            var rows = new List<BomRow>
            {
                new BomRow { PartNumber = "391-KEEP", Skid = "1 [FR-MB]", Segment = "MB", Description = "Keep Component" },
                new BomRow { PartNumber = "391-DELETE", Skid = "1 [FR-MB]", Segment = "MB", Description = "Delete Component" }
            };
            vm.LoadBomRows(rows);

            // Delete 391-DELETE
            var toDelete = vm.BomEntries.First(e => e.PartNumber == "391-DELETE");
            vm.SelectedBomEntry = toDelete;
            vm.ExecuteDeleteBomRow();

            vm.CreateShellFolders();

            var createdDirs = Directory.GetDirectories(tempShellRoot, "*", SearchOption.AllDirectories);
            Assert.Contains(createdDirs, d => d.Contains("Keep Component"));
            Assert.DoesNotContain(createdDirs, d => d.Contains("Delete Component"));
        }
        finally
        {
            if (Directory.Exists(tempShellRoot)) Directory.Delete(tempShellRoot, recursive: true);
        }
    }

    [Fact]
    public void ManualBomEntry_WithCustomSkidAndSegment_IsPreservedAndAddedToBomEntries()
    {
        var vm = new MainViewModel();
        var customRows = new List<BomRow>
        {
            new BomRow { PartNumber = "391-CUSTOM1", Skid = "Skid 1", Segment = "FF-1", Description = "Custom Front Panel" },
            new BomRow { PartNumber = "391-CUSTOM2", Skid = "CustomSkid", Segment = "CustomSeg", Description = "Custom Roof Cap" }
        };

        vm.LoadBomRows(customRows);

        Assert.Equal(2, vm.BomEntries.Count);
        Assert.NotNull(vm.ProjectState.Bom);
        Assert.Equal(2, vm.ProjectState.Bom.KeptCount);
        Assert.Contains(vm.BomEntries, e => e.PartNumber == "391-CUSTOM1" && e.RelativePath.Contains("FF-1"));
        Assert.Contains(vm.BomEntries, e => e.PartNumber == "391-CUSTOM2" && e.RelativePath.Contains("CustomSeg"));
    }
}
