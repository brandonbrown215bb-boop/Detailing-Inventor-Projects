using System;
using System.Collections.Generic;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class ProjectStateTests
{
    [Fact]
    public void ComputeGeometryFingerprint_CalculatesDeterministicHash()
    {
        var boxA = new GeometryBox(10.1234, 20.5678, 30.0, 100.0, 200.0, 50.0);
        var boxB = new GeometryBox(0.0, 0.0, 0.0, 10.0, 10.0, 10.0);

        var list1 = new List<GeometryBox> { boxA, boxB };
        var list2 = new List<GeometryBox> { boxB, boxA };

        string fp1 = GeometryFingerprinter.CalculateFingerprint(list1);
        string fp2 = GeometryFingerprinter.CalculateFingerprint(list2);

        Assert.NotEmpty(fp1);
        Assert.Equal(fp1, fp2);
        Assert.Equal("0.000,0.000,0.000,10.000,10.000,10.000|10.123,20.568,30.000,100.000,200.000,50.000", fp1);
        Assert.Equal(string.Empty, GeometryFingerprinter.CalculateFingerprint((IEnumerable<GeometryBox>?)null));
    }

    [Fact]
    public void DirtyFlag_Tracking_Lifecycle()
    {
        var vm = new MainViewModel();
        Assert.False(vm.IsDirty);

        var surf = new SurfaceModel
        {
            SurfaceNumber = "SURF-1001",
            StateId = "current"
        };
        vm.Surfaces.Add(surf);
        vm.SelectedSurface = surf;

        vm.UpdateSelectedSurfaceStatus("built");
        Assert.True(vm.IsDirty);

        vm.ClearDirty();
        Assert.False(vm.IsDirty);

        vm.UpdateChecklistItem("Visual Inspection", true);
        Assert.True(vm.IsDirty);

        vm.ClearDirty();
        Assert.False(vm.IsDirty);

        vm.SelectedSurfaceNotes = "New note content";
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void RenumberSurfaceInPlace_PreservesHistoryAndLogsRetiredRecord()
    {
        var project = new ProjectStateModel();
        var record = new SurfaceRecordModel
        {
            StateId = "built",
            Notes = "Original note",
            DisplayNumber = "1001",
            GeometryFingerprint = "0.000,0.000,0.000,10.000,10.000,10.000",
            Checklist = new Dictionary<string, bool> { ["Visual"] = true }
        };
        project.Surfaces["SURF-1001"] = record;

        bool result = ProjectStateService.RenumberSurfaceInPlace(project, "SURF-1001", "1002");

        Assert.True(result);
        Assert.Equal("1002", record.DisplayNumber);
        Assert.Contains("1001", record.PreviousNumbers);

        Assert.True(project.Retired.ContainsKey("1001"));
        var retired = project.Retired["1001"];
        Assert.Equal("1002", retired.SupersededBy);
        Assert.Equal("renumber", retired.TransferType);
        Assert.Equal("SURF-1001", retired.FileKey);
        Assert.NotNull(retired.Snapshot);
        Assert.Equal("1001", retired.Snapshot.DisplayNumber);
        Assert.Equal("built", retired.Snapshot.StateId);
        Assert.Equal("Original note", retired.Snapshot.Notes);
    }

    [Fact]
    public void RenumberSurfaceInPlace_Validation()
    {
        var project = new ProjectStateModel();
        project.Surfaces["SURF-1001"] = new SurfaceRecordModel { DisplayNumber = "1001" };
        project.Surfaces["SURF-1002"] = new SurfaceRecordModel { DisplayNumber = "1002" };

        // Duplicate active display number throws InvalidOperationException
        Assert.Throws<InvalidOperationException>(() =>
            ProjectStateService.RenumberSurfaceInPlace(project, "SURF-1001", "1002"));

        // Empty new number throws ArgumentException
        Assert.Throws<ArgumentException>(() =>
            ProjectStateService.RenumberSurfaceInPlace(project, "SURF-1001", "  "));

        // Non-existent key throws KeyNotFoundException
        Assert.Throws<KeyNotFoundException>(() =>
            ProjectStateService.RenumberSurfaceInPlace(project, "SURF-9999", "1003"));

        // Same display number returns false (no-op)
        bool noop = ProjectStateService.RenumberSurfaceInPlace(project, "SURF-1001", "1001");
        Assert.False(noop);
    }

    [Fact]
    public void FindRenumberCandidates_MatchesByGeometryFingerprint()
    {
        var project = new ProjectStateModel();
        string fpTarget = "0.000,0.000,0.000,10.000,10.000,10.000|10.000,20.000,30.000,100.000,200.000,50.000";

        project.Retired["1001"] = new RetiredSurfaceRecordModel
        {
            GeometryFingerprint = fpTarget,
            SupersededBy = "1002"
        };
        project.Retired["0999"] = new RetiredSurfaceRecordModel
        {
            GeometryFingerprint = "50.000,50.000,50.000,5.000,5.000,5.000"
        };

        var scannedSurface = new SurfaceModel
        {
            SurfaceNumber = "391Z-NEW",
            Boxes = new List<GeometryBox>
            {
                new(10.0, 20.0, 30.0, 100.0, 200.0, 50.0),
                new(0.0, 0.0, 0.0, 10.0, 10.0, 10.0)
            }
        };

        var candidates = ProjectStateService.FindRenumberCandidates(scannedSurface, project);

        Assert.Single(candidates);
        Assert.Equal("1001", candidates[0]);
    }

    [Fact]
    public void LinkPreviousSurface_TransfersStateAndRetiresOldKey()
    {
        var project = new ProjectStateModel();
        var activeRec = new SurfaceRecordModel { DisplayNumber = "1002" };
        project.Surfaces["SURF-1002"] = activeRec;

        var retiredRec = new RetiredSurfaceRecordModel
        {
            GeometryFingerprint = "test-fp",
            Snapshot = new SurfaceRecordModel
            {
                DisplayNumber = "1001",
                StateId = "corrected",
                Notes = "Transferred notes",
                Checklist = new Dictionary<string, bool> { ["Visual"] = true }
            }
        };
        project.Retired["1001"] = retiredRec;

        ProjectStateService.LinkPreviousSurface(project, "1002", "1001", "renumber");

        Assert.Equal("corrected", activeRec.StateId);
        Assert.Equal("Transferred notes", activeRec.Notes);
        Assert.True(activeRec.Checklist["Visual"]);
        Assert.Contains("1001", activeRec.PreviousNumbers);
        Assert.Equal("1002", retiredRec.SupersededBy);
    }

    [Fact]
    public void MergeScan_RetiresMissingSurfaces()
    {
        var project = new ProjectStateModel();
        project.Surfaces["SURF-1001"] = new SurfaceRecordModel { DisplayNumber = "1001", StateId = "done" };
        project.Surfaces["SURF-1002"] = new SurfaceRecordModel { DisplayNumber = "1002", StateId = "built" };

        var activeScannedKeys = new List<string> { "SURF-1001" };
        ProjectStateService.RetireMissingSurfaces(project, activeScannedKeys);

        Assert.True(project.Surfaces.ContainsKey("SURF-1001"));
        Assert.False(project.Surfaces.ContainsKey("SURF-1002"));

        Assert.True(project.Retired.ContainsKey("SURF-1002"));
        var retired = project.Retired["SURF-1002"];
        Assert.Equal("missing", retired.TransferType);
        Assert.Equal("SURF-1002", retired.FileKey);
        Assert.NotNull(retired.Snapshot);
        Assert.Equal("built", retired.Snapshot.StateId);
    }
}
