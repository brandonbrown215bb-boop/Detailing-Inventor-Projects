using System;
using System.Collections.Generic;
using System.IO;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Step9RetireRestoreTests
{
    [Fact]
    public void UPT_C_017_RetireSurface_PreservesGeometryTrackingAndAuditReason()
    {
        var surface = TrackedSurface();
        var project = ProjectWith(surface);

        var result = ProjectStateService.RetireSurface(project, surface, new[] { surface }, "removed");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Empty(project.Surfaces);
        Assert.Empty(project.Geometry);
        var retired = Assert.Single(project.Retired).Value;
        Assert.Equal("removed", retired.TransferType);
        Assert.Equal("SURF-2001", retired.FileKey);
        Assert.NotNull(retired.GeometrySnapshot);
        Assert.Single(retired.GeometrySnapshot!.Boxes);
        Assert.Equal("Detailed note", retired.Snapshot!.Notes);
        Assert.True(retired.Snapshot.Checklist["Audit item"]);
        Assert.Null(retired.RestoredAt);
    }

    [Fact]
    public void UPT_C_017_RetireSaveReopenRestore_RoundTripsCompleteSurfaceAndKeepsRetirementHistory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"upt-step9-{Guid.NewGuid():N}.uptproj");
        try
        {
            var surface = TrackedSurface();
            var project = ProjectWith(surface);
            var retire = ProjectStateService.RetireSurface(project, surface, new[] { surface }, "missing-unnecessary");
            Assert.True(retire.Success, retire.ErrorMessage);
            ProjectSerializer.SaveAtomic(path, project);

            var reopened = Assert.IsType<ProjectStateModel>(ProjectSerializer.Load<ProjectStateModel>(path));
            var restore = ProjectStateService.RestoreSurface(reopened, retire.RetiredKey, Array.Empty<SurfaceModel>());

            Assert.True(restore.Success, restore.ErrorMessage);
            Assert.Equal("SURF-2001", restore.RestoredSurface!.SurfaceNumber);
            Assert.Equal("Detailed note", restore.RestoredSurface.Notes);
            Assert.True(restore.RestoredSurface.Checklist["Audit item"]);
            Assert.Single(restore.RestoredSurface.Boxes);
            Assert.True(reopened.Surfaces.ContainsKey("SURF-2001"));
            Assert.True(reopened.Geometry.ContainsKey("SURF-2001"));
            Assert.NotNull(reopened.Retired[retire.RetiredKey].RestoredAt);
            Assert.Equal("SURF-2001", reopened.Retired[retire.RetiredKey].RestoredAs);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void RestoreSurface_WithoutCachedGeometry_PreservesRetiredState()
    {
        var project = new ProjectStateModel();
        project.Retired["SURF-LOST"] = new RetiredSurfaceRecordModel
        {
            FileKey = "SURF-LOST",
            TransferType = "removed",
            Snapshot = new SurfaceRecordModel { DisplayNumber = "SURF-LOST" }
        };

        var result = ProjectStateService.RestoreSurface(project, "SURF-LOST", Array.Empty<SurfaceModel>());

        Assert.False(result.Success);
        Assert.Contains("geometry", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(project.Surfaces);
        Assert.True(project.Retired.ContainsKey("SURF-LOST"));
        Assert.Null(project.Retired["SURF-LOST"].RestoredAt);
    }

    [Fact]
    public void MainViewModel_LoadsRemovedSectionAndRestoreCommandMarksProjectDirty()
    {
        string path = Path.Combine(Path.GetTempPath(), $"upt-step9-vm-{Guid.NewGuid():N}.uptproj");
        try
        {
            var surface = TrackedSurface();
            var project = ProjectWith(surface);
            var retire = ProjectStateService.RetireSurface(project, surface, new[] { surface }, "removed");
            Assert.True(retire.Success, retire.ErrorMessage);
            ProjectSerializer.SaveAtomic(path, project);

            var vm = new MainViewModel();
            vm.LoadProjectFromFile(path);
            var removed = Assert.Single(vm.RemovedSurfaces);

            vm.RestoreSurfaceCommand.Execute(removed);

            Assert.Single(vm.Surfaces);
            Assert.Empty(vm.RemovedSurfaces);
            Assert.True(vm.IsDirty);
            Assert.Contains("Restored surface", vm.StatusMessage);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void MissingSurface_MarkUnnecessary_RetiresWithRestorableGeometryAndReason()
    {
        var surface = TrackedSurface();
        var project = ProjectWith(surface);
        var proposal = RescanReconciler.Reconcile(new[] { surface }, Array.Empty<SurfaceModel>());
        var decisions = new RescanReviewDecisions();
        decisions.MissingSurfaceResolutions[surface.SurfaceNumber] = MissingSurfaceResolution.MarkUnnecessary;

        var result = ProjectStateService.ApplyRescanProposal(project, proposal, decisions);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Empty(project.Surfaces);
        Assert.Empty(project.Geometry);
        var retired = Assert.Single(project.Retired).Value;
        Assert.Equal("missing-unnecessary", retired.TransferType);
        Assert.NotNull(retired.GeometrySnapshot);
        Assert.Equal("Detailed note", retired.Snapshot!.Notes);
    }

    private static SurfaceModel TrackedSurface()
        => new()
        {
            SurfaceNumber = "SURF-2001",
            DisplayNumber = "2001",
            StateId = "checked",
            Notes = "Detailed note",
            IsHidden = true,
            PreviousNumbers = new List<string> { "1999" },
            Checklist = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) { ["Audit item"] = true },
            Boxes = new List<GeometryBox> { new(0, 0, 0, 12, 3, 8) },
            GeometryFingerprint = "fp-2001"
        };

    private static ProjectStateModel ProjectWith(SurfaceModel surface)
        => new()
        {
            Surfaces = new Dictionary<string, SurfaceRecordModel>(StringComparer.OrdinalIgnoreCase)
            {
                [surface.SurfaceNumber] = new()
                {
                    DisplayNumber = surface.DisplayNumber,
                    StateId = surface.StateId,
                    Notes = surface.Notes,
                    Hidden = surface.IsHidden,
                    PreviousNumbers = new List<string>(surface.PreviousNumbers),
                    Checklist = new Dictionary<string, bool>(surface.Checklist, StringComparer.OrdinalIgnoreCase),
                    GeometryFingerprint = surface.GeometryFingerprint
                }
            },
            Geometry = new Dictionary<string, SurfaceModel>(StringComparer.OrdinalIgnoreCase)
            {
                [surface.SurfaceNumber] = surface
            }
        };
}
