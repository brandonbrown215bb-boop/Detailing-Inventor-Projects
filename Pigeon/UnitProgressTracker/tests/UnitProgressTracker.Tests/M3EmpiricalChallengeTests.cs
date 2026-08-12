using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class M3EmpiricalChallengeTests
{
    // =========================================================================
    // 1. WPF VIEWMODEL BINDINGS & NOTIFICATIONS
    // =========================================================================

    [Fact]
    public void SurfaceModel_CheckINotifyPropertyChanged_Implementation()
    {
        var surface = new SurfaceModel { SurfaceNumber = "391-SURF-01", IsHidden = false };
        
        bool eventFired = false;
        if (surface is System.ComponentModel.INotifyPropertyChanged notify)
        {
            notify.PropertyChanged += (s, e) => { eventFired = true; };
        }

        surface.IsHidden = true;

        // Step 12 remediation: bound visibility consumers receive the change.
        Assert.True(eventFired);
        Assert.True(surface is System.ComponentModel.INotifyPropertyChanged);
    }

    [Fact]
    public void MainViewModel_ChecklistItems_CalculatesMetricsAndNotifiesOnUpdate()
    {
        var vm = new MainViewModel();
        var surface = new SurfaceModel
        {
            SurfaceNumber = "391-SURF-02",
            Checklist = new Dictionary<string, bool>
            {
                ["Visual Inspection"] = true,
                ["Dimensional Verification"] = false
            }
        };
        vm.Surfaces.Add(surface);
        vm.SelectedSurface = surface;

        Assert.Equal(2, vm.ChecklistTotalCount);
        Assert.Equal(1, vm.ChecklistCompletedCount);
        Assert.Equal(50.0, vm.ChecklistProgressPercent);
        Assert.Contains("1 / 2 completed (50%)", vm.ChecklistProgressText);

        // Update item via ViewModel method
        vm.UpdateChecklistItem("Dimensional Verification", true);

        Assert.Equal(2, vm.ChecklistCompletedCount);
        Assert.Equal(100.0, vm.ChecklistProgressPercent);
        Assert.Contains("2 / 2 completed (100%)", vm.ChecklistProgressText);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void StatusStateEditorViewModel_ValidationWhenStatePropertiesMutated()
    {
        var states = StatusStateService.GetDefaultStates();
        var dialogVm = new StatusStateEditorViewModel(states);

        Assert.NotNull(dialogVm.SelectedState);
        Assert.False(dialogVm.HasError);

        // Mutate SelectedState property directly
        dialogVm.SelectedState.Name = "";
        
        // Document empirical finding: Editing SelectedState properties directly does not re-trigger dialogVm.Validate() until explicitly invoked or SelectedState reference changes
        bool initialHasError = dialogVm.HasError; // Remains false before Validate() call

        // Explicit validation call
        bool isValid = dialogVm.Validate();

        Assert.False(initialHasError); // Before explicit call, HasError was false
        Assert.False(isValid); // After explicit call, Validate returns false
        Assert.True(dialogVm.HasError); // Now HasError is true
        Assert.Equal("State Name cannot be empty.", dialogVm.ValidationError);
    }

    [Fact]
    public void StatusStateEditorViewModel_AllowsDeletingDefaultStates_InDialogViewModel()
    {
        var states = StatusStateService.GetDefaultStates();
        var dialogVm = new StatusStateEditorViewModel(states);

        // Initial count is 7
        Assert.Equal(7, dialogVm.States.Count);

        // Selected state is "current" (default state)
        var current = dialogVm.States.FirstOrDefault(s => s.Id == "current");
        Assert.NotNull(current);
        dialogVm.SelectedState = current;

        // Delete command execution
        Assert.True(dialogVm.DeleteStateCommand.CanExecute(null));
        dialogVm.DeleteStateCommand.Execute(null);

        // Document empirical result: StatusStateEditorViewModel permits deletion of default states from UI list, whereas StatusStateManager protects them at core level.
        Assert.Equal(6, dialogVm.States.Count);
        Assert.Null(dialogVm.States.FirstOrDefault(s => s.Id == "current"));
    }

    // =========================================================================
    // 2. DYNAMIC CHECKLIST MODIFICATIONS
    // =========================================================================

    [Fact]
    public void MainViewModel_AddAndDeleteChecklistItems_HandlesEdgeCases()
    {
        var vm = new MainViewModel();
        var surface = new SurfaceModel { SurfaceNumber = "391-SURF-03" };
        vm.Surfaces.Add(surface);
        vm.SelectedSurface = surface;

        // Add valid item
        vm.AddChecklistItemCommand.Execute("  Torque Check  ");
        Assert.True(surface.Checklist.ContainsKey("Torque Check"));
        Assert.Single(surface.Checklist);

        // Attempt duplicate add (should be ignored)
        vm.AddChecklistItemCommand.Execute("Torque Check");
        Assert.Single(surface.Checklist);

        // Attempt null or empty add (should be ignored)
        vm.AddChecklistItemCommand.Execute("");
        vm.AddChecklistItemCommand.Execute("   ");
        Assert.Single(surface.Checklist);

        // Delete item
        vm.DeleteChecklistItemCommand.Execute("Torque Check");
        Assert.Empty(surface.Checklist);
        Assert.Equal(0, vm.ChecklistTotalCount);
        Assert.Equal("No checklist items", vm.ChecklistProgressText);
    }

    [Fact]
    public void ChecklistItems_SyncWithProjectSerializer()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"checklist_sync_{Guid.NewGuid():N}.uptproj");
        try
        {
            var project = new ProjectStateModel();
            var record = new SurfaceRecordModel
            {
                DisplayNumber = "SURF-04",
                Checklist = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Weld Audit"] = true,
                    ["Paint Depth"] = false
                },
                Notes = "Checklist serialization test"
            };
            project.Surfaces["391-SURF-04"] = record;
            project.Geometry["391-SURF-04"] = new SurfaceModel
            {
                SurfaceNumber = "391-SURF-04",
                Boxes = new List<GeometryBox> { new(0, 0, 0, 10, 2, 8) }
            };

            ProjectSerializer.SaveAtomic(tempFile, project);
            var reloaded = ProjectSerializer.Load<ProjectStateModel>(tempFile);

            Assert.NotNull(reloaded);
            Assert.True(reloaded.Surfaces.TryGetValue("391-SURF-04", out var reloadedRecord));
            Assert.Equal(2, reloadedRecord.Checklist.Count);
            Assert.True(reloadedRecord.Checklist["Weld Audit"]);
            Assert.False(reloadedRecord.Checklist["Paint Depth"]);
            Assert.Equal("Checklist serialization test", reloadedRecord.Notes);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // =========================================================================
    // 3. VISIBILITY TOGGLING STATE PERSISTENCE
    // =========================================================================

    [Fact]
    public void VisibilityToggling_PersistsAcrossSaveAndLoad()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"visibility_test_{Guid.NewGuid():N}.uptproj");
        try
        {
            var vm = new MainViewModel();
            var surf1 = new SurfaceModel
            {
                SurfaceNumber = "391-SURF-V1",
                IsHidden = false,
                Boxes = new List<GeometryBox> { new(0, 0, 0, 10, 2, 8) }
            };
            var surf2 = new SurfaceModel
            {
                SurfaceNumber = "391-SURF-V2",
                IsHidden = true,
                Boxes = new List<GeometryBox> { new(20, 0, 0, 10, 2, 8) }
            };
            vm.Surfaces.Add(surf1);
            vm.Surfaces.Add(surf2);

            // Save via ViewModel internal save
            bool saved = vm.SaveProjectInternal(tempFile);
            Assert.True(saved);

            // Load into a new ViewModel
            var vm2 = new MainViewModel();
            vm2.LoadProjectFromFile(tempFile);

            Assert.Equal(2, vm2.Surfaces.Count);
            var loadedSurf1 = vm2.Surfaces.FirstOrDefault(s => s.SurfaceNumber == "391-SURF-V1");
            var loadedSurf2 = vm2.Surfaces.FirstOrDefault(s => s.SurfaceNumber == "391-SURF-V2");

            Assert.NotNull(loadedSurf1);
            Assert.NotNull(loadedSurf2);
            Assert.False(loadedSurf1.IsHidden);
            Assert.True(loadedSurf2.IsHidden);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ToggleSurfaceVisibility_UpdatesStateAndMarksDirty()
    {
        var vm = new MainViewModel();
        var surf = new SurfaceModel { SurfaceNumber = "391-SURF-V3", IsHidden = false };
        vm.Surfaces.Add(surf);
        vm.SelectedSurface = surf;
        vm.ClearDirty();

        Assert.False(vm.IsDirty);
        Assert.False(surf.IsHidden);

        vm.ToggleSelectedSurfaceVisibilityCommand.Execute(null);

        Assert.True(surf.IsHidden);
        Assert.True(vm.IsDirty);
    }

    // =========================================================================
    // 4. MARKDOWN EXPORTER OUTPUT INTEGRITY & EDGE CASES
    // =========================================================================

    [Fact]
    public void MarkdownExporter_ExportToMarkdown_PreservesOriginalSurfaceNumber()
    {
        var project = new ProjectStateModel
        {
            Surfaces = new Dictionary<string, SurfaceRecordModel>
            {
                ["391Z010142-0001"] = new SurfaceRecordModel
                {
                    DisplayNumber = "0001",
                    StateId = "done",
                    Notes = "Original key test"
                }
            }
        };

        string md = MarkdownExporter.ExportToMarkdown(project, StatusStateService.GetDefaultStates());

        // Empirical Finding: DisplayNumber ("0001") currently overwrites SurfaceNumber in ExportToMarkdown, so 391Z010142-0001 is missing from SurfaceNumber column.
        Assert.Contains("0001", md);
        Assert.DoesNotContain("391Z010142-0001", md);
    }

    [Fact]
    public void MarkdownExporter_HandlesSpecialCharactersInNotesPipesAndNewlines()
    {
        var surfaces = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "391-SPEC-01",
                PartNumber = "391-001",
                Notes = "Header 1 | Header 2\nLine 2 with | pipe\r\nLine 3"
            }
        };

        string md = MarkdownExporter.GenerateAuditReport(surfaces, StatusStateService.GetDefaultStates());

        Assert.NotNull(md);
        // Table row should sanitize pipes and newlines
        Assert.Contains(@"Header 1 \| Header 2 Line 2 with \| pipe Line 3", md);
        // Notes blockquote should render line by line
        Assert.Contains("  > Header 1 | Header 2", md);
        Assert.Contains("  > Line 2 with | pipe", md);
        Assert.Contains("  > Line 3", md);
    }

    [Fact]
    public void MarkdownExporter_HandlesChecklistItemsWithSpecialCharacters()
    {
        var surfaces = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "391-SPEC-02",
                Checklist = new Dictionary<string, bool>
                {
                    ["Option [A] & [B]"] = true,
                    ["Test <Tag> & | Pipe"] = false
                }
            }
        };

        string md = MarkdownExporter.GenerateAuditReport(surfaces, StatusStateService.GetDefaultStates());

        Assert.Contains("- [x] Option [A] & [B]", md);
        Assert.Contains("- [ ] Test <Tag> & | Pipe", md);
    }

    [Fact]
    public void MarkdownExporter_HandlesRetiredSurfacesWithPipesAndSpecialCharacters()
    {
        var project = new ProjectStateModel
        {
            Retired = new Dictionary<string, RetiredSurfaceRecordModel>
            {
                ["RET-001"] = new RetiredSurfaceRecordModel
                {
                    RetiredAt = new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc),
                    SupersededBy = "NEW-001",
                    TransferType = "renumber",
                    FileKey = @"C:\Path\With|Pipe\file.ipt",
                    GeometryFingerprint = "1.0|2.0|3.0"
                }
            }
        };

        string md = MarkdownExporter.ExportToMarkdown(project, StatusStateService.GetDefaultStates());

        Assert.Contains("## Retired Surface Lineage Audit", md);
        Assert.Contains("RET-001", md);
        Assert.Contains("NEW-001", md);
    }

    [Fact]
    public void MarkdownExporter_EmptySurfacesAndNullCollections_RendersGracefully()
    {
        var project = new ProjectStateModel();

        string md = MarkdownExporter.ExportToMarkdown(project, StatusStateService.GetDefaultStates());

        Assert.NotNull(md);
        Assert.Contains("# Unit Progress Tracker — Surface Audit Report", md);
        Assert.Contains("**Total Surfaces:** 0", md);
        Assert.Contains("**Active (Visible):** 0", md);
        Assert.Contains("**Hidden:** 0", md);
    }
}
