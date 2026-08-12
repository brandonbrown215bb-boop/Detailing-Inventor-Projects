using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Step3RescanTrackingTests : IDisposable
{
    private readonly string _tempDirectory;

    public Step3RescanTrackingTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UPT_Step3_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, true); } catch { }
        }
    }

    [Fact]
    public void RescanReconciler_ExactMatches_PreserveTrackingAndUpdateGeometry()
    {
        var existing = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "SURF-1001",
                StateId = "done",
                Notes = "Detailer note for 1001",
                DisplayNumber = "1001-ALT",
                PreviousNumbers = new List<string> { "0999" },
                IsHidden = true,
                Checklist = new Dictionary<string, bool> { ["Visual Inspection"] = true },
                Boxes = new List<GeometryBox> { new(0, 0, 0, 10, 10, 10) }
            }
        };

        var scannedCandidates = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "SURF-1001",
                PartNumber = "391-1001",
                Boxes = new List<GeometryBox> { new(0, 0, 0, 20, 20, 20) } // Updated geometry
            }
        };

        var result = RescanReconciler.Reconcile(existing, scannedCandidates);

        Assert.Single(result.ReconciledSurfaces);
        var reconciled = result.ReconciledSurfaces[0];
        Assert.Equal("SURF-1001", reconciled.SurfaceNumber);
        Assert.Equal("done", reconciled.StateId);
        Assert.Equal("Detailer note for 1001", reconciled.Notes);
        Assert.Equal("1001-ALT", reconciled.DisplayNumber);
        Assert.Contains("0999", reconciled.PreviousNumbers);
        Assert.True(reconciled.IsHidden);
        Assert.True(reconciled.Checklist["Visual Inspection"]);
        Assert.Equal(20, reconciled.Boxes[0].XLength);
    }

    [Fact]
    public void RescanReconciler_MissingSurfaces_ArePreservedInReconciledSet()
    {
        var existing = new List<SurfaceModel>
        {
            new SurfaceModel { SurfaceNumber = "SURF-1001", StateId = "built", Notes = "Existing 1001" },
            new SurfaceModel { SurfaceNumber = "SURF-1002", StateId = "done", Notes = "Missing from scan" }
        };

        var scannedCandidates = new List<SurfaceModel>
        {
            new SurfaceModel { SurfaceNumber = "SURF-1001" }
        };

        var result = RescanReconciler.Reconcile(existing, scannedCandidates);

        Assert.Equal(2, result.ReconciledSurfaces.Count);
        Assert.Single(result.MissingSurfaces);
        Assert.Equal("SURF-1002", result.MissingSurfaces[0].SurfaceNumber);
        Assert.Equal("Missing from scan", result.MissingSurfaces[0].Notes);
    }

    [Fact]
    public void RescanReconciler_NewSurfaces_InitializedWithChecklistTemplate()
    {
        var existing = new List<SurfaceModel>();
        var scannedCandidates = new List<SurfaceModel>
        {
            new SurfaceModel { SurfaceNumber = "SURF-NEW-1" }
        };

        string customTemplate = "Dimension Check; Material Check; Paint Inspection";

        var result = RescanReconciler.Reconcile(existing, scannedCandidates, customTemplate);

        Assert.Single(result.NewSurfaces);
        var newSurf = result.NewSurfaces[0];
        Assert.Equal("current", newSurf.StateId);
        Assert.Equal(3, newSurf.Checklist.Count);
        Assert.False(newSurf.Checklist["Dimension Check"]);
        Assert.False(newSurf.Checklist["Material Check"]);
        Assert.False(newSurf.Checklist["Paint Inspection"]);
    }

    [Fact]
    public void GeometryIntrusionChecker_DetectsProtrusionOverlap_GeneratesFlag()
    {
        var surfaces = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "SURF-A",
                Boxes = new List<GeometryBox> { new(0, 0, 0, 100, 100, 100) }
            },
            new SurfaceModel
            {
                SurfaceNumber = "SURF-B",
                // Overlaps volumetrically into SURF-A
                Boxes = new List<GeometryBox> { new(50, 50, 50, 100, 100, 100) }
            },
            new SurfaceModel
            {
                SurfaceNumber = "SURF-C",
                // Separate box with zero overlap
                Boxes = new List<GeometryBox> { new(500, 500, 500, 50, 50, 50) }
            }
        };

        var flags = GeometryIntrusionChecker.CheckIntrusions(surfaces);

        Assert.True(flags.Count >= 2);
        var flagA = flags.First(f => f.SurfaceNumber == "SURF-A");
        Assert.Contains("SURF-B", flagA.AffectedSurfaceNumbers);
        Assert.False(flagA.Resolved);
    }

    [Fact]
    public void GeometryIntrusionChecker_HiddenSurfacesStillParticipate_AndCleanScanResolvesFlag()
    {
        var visible = new SurfaceModel
        {
            SurfaceNumber = "VISIBLE",
            Boxes = new List<GeometryBox> { new(0, 0, 0, 10, 10, 10) }
        };
        var hidden = new SurfaceModel
        {
            SurfaceNumber = "HIDDEN",
            IsHidden = true,
            Boxes = new List<GeometryBox> { new(5, 5, 5, 10, 10, 10) }
        };

        var detected = GeometryIntrusionChecker.CheckIntrusions(new[] { visible, hidden });
        var resolved = GeometryIntrusionChecker.ReconcileFlags(detected, Array.Empty<GeometryIntrusionFlagModel>());

        Assert.Contains(detected, flag => flag.SurfaceNumber == "VISIBLE" && flag.AffectedSurfaceNumbers.Contains("HIDDEN"));
        Assert.All(resolved, flag => Assert.True(flag.Resolved));
    }

    [Fact]
    public void ApplyRescanProposal_RequiresEveryDecision_ThenTransfersOnlyConfirmedTracking()
    {
        var existing = new SurfaceModel
        {
            SurfaceNumber = "OLD",
            StateId = "built",
            Notes = "keep me",
            Checklist = new Dictionary<string, bool> { ["Checked"] = true },
            Boxes = new List<GeometryBox> { new(0, 0, 0, 10, 10, 10) }
        };
        existing.GeometryFingerprint = GeometryFingerprinter.CalculateFingerprint(existing);
        var candidate = new SurfaceModel
        {
            SurfaceNumber = "NEW",
            Boxes = new List<GeometryBox> { new(0, 0, 0, 10, 10, 10) }
        };
        var project = new ProjectStateModel();
        project.Surfaces["OLD"] = new SurfaceRecordModel { DisplayNumber = "OLD", StateId = "built" };
        project.Geometry["OLD"] = existing;
        var proposal = RescanReconciler.Reconcile(new[] { existing }, new[] { candidate });

        var rejected = ProjectStateService.ApplyRescanProposal(project, proposal, new RescanReviewDecisions());
        Assert.False(rejected.Success);
        Assert.True(project.Surfaces.ContainsKey("OLD"));
        Assert.Empty(project.Retired);

        var decisions = new RescanReviewDecisions();
        decisions.RenumberTransfers["NEW"] = true;
        var applied = ProjectStateService.ApplyRescanProposal(project, proposal, decisions);

        Assert.True(applied.Success);
        Assert.Equal("built", candidate.StateId);
        Assert.Equal("keep me", candidate.Notes);
        Assert.True(candidate.Checklist["Checked"]);
        Assert.True(project.Surfaces.ContainsKey("NEW"));
        Assert.False(project.Surfaces.ContainsKey("OLD"));
        Assert.Equal("NEW", project.Retired["OLD"].SupersededBy);
    }

    [Fact]
    public void ApplyRescanProposal_MissingSurfaceMustBeReviewedBeforeRetirement()
    {
        var missing = new SurfaceModel
        {
            SurfaceNumber = "MISSING",
            Boxes = new List<GeometryBox> { new(0, 0, 0, 10, 10, 10) }
        };
        var project = new ProjectStateModel();
        project.Surfaces["MISSING"] = new SurfaceRecordModel { DisplayNumber = "MISSING" };
        var proposal = RescanReconciler.Reconcile(new[] { missing }, Array.Empty<SurfaceModel>());

        var decisions = new RescanReviewDecisions();
        decisions.MissingSurfaceResolutions["MISSING"] = MissingSurfaceResolution.MarkUnnecessary;
        var applied = ProjectStateService.ApplyRescanProposal(project, proposal, decisions);

        Assert.True(applied.Success);
        Assert.Empty(project.Surfaces);
        Assert.Equal("missing-unnecessary", project.Retired["MISSING"].TransferType);
    }

    [Fact]
    public void RescanReconciler_DuplicateScannedIdentity_IsAConflict()
    {
        var scanned = new[]
        {
            new SurfaceModel { SurfaceNumber = "DUP" },
            new SurfaceModel { SurfaceNumber = "dup" }
        };

        var proposal = RescanReconciler.Reconcile(Array.Empty<SurfaceModel>(), scanned);

        Assert.Single(proposal.Conflicts);
        Assert.Empty(proposal.ReconciledSurfaces);
    }

    [Fact]
    public void FullIntegration_Scan_Edit_Rescan_Save_Reopen_PreservesTracking()
    {
        string projectPath = Path.Combine(_tempDirectory, "integration_rescan.uptproj");

        // 1. Initial ViewModel setup
        var vm1 = new MainViewModel();
        var initialSurfaces = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "SURF-1001",
                StateId = "current",
                Boxes = new List<GeometryBox> { new(0, 0, 0, 100, 100, 10) }
            },
            new SurfaceModel
            {
                SurfaceNumber = "SURF-1002",
                StateId = "current",
                Boxes = new List<GeometryBox> { new(110, 0, 0, 100, 100, 10) }
            }
        };

        foreach (var s in initialSurfaces) vm1.Surfaces.Add(s);

        // 2. Detailer edits tracking
        var surf1 = vm1.Surfaces.First(s => s.SurfaceNumber == "SURF-1001");
        vm1.SelectedSurface = surf1;
        vm1.UpdateSelectedSurfaceStatus("built");
        vm1.UpdateChecklistItem("Visual Inspection", true);
        vm1.SelectedSurfaceNotes = "Verified torque and dimensions.";
        vm1.RenumberSurfaceCommand.Execute("1001-FINAL");

        Assert.Equal("1001-FINAL", surf1.DisplayNumber);
        Assert.Equal("built", surf1.StateId);
        Assert.True(surf1.Checklist["Visual Inspection"]);
        Assert.Equal("Verified torque and dimensions.", surf1.Notes);

        // 3. Rescan simulation with candidate surfaces (including one updated geometry and one new surface)
        var scannedCandidates = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "SURF-1001",
                Boxes = new List<GeometryBox> { new(0, 0, 0, 100, 100, 15) } // Geometry updated
            },
            new SurfaceModel
            {
                SurfaceNumber = "SURF-1002",
                Boxes = new List<GeometryBox> { new(110, 0, 0, 100, 100, 10) }
            },
            new SurfaceModel
            {
                SurfaceNumber = "SURF-1003",
                Boxes = new List<GeometryBox> { new(220, 0, 0, 100, 100, 10) }
            }
        };

        var reconcileResult = RescanReconciler.Reconcile(vm1.Surfaces, scannedCandidates);
        vm1.Surfaces.Clear();
        foreach (var s in reconcileResult.ReconciledSurfaces) vm1.Surfaces.Add(s);

        // 4. Save project
        bool saved = vm1.SaveProjectInternal(projectPath);
        Assert.True(saved);

        // 5. Reopen in fresh ViewModel instance
        var vm2 = new MainViewModel();
        vm2.LoadProjectFromFile(projectPath);

        Assert.Equal(3, vm2.Surfaces.Count);
        var loaded1001 = vm2.Surfaces.First(s => s.SurfaceNumber == "SURF-1001");

        Assert.Equal("built", loaded1001.StateId);
        Assert.Equal("Verified torque and dimensions.", loaded1001.Notes);
        Assert.Equal("1001-FINAL", loaded1001.DisplayNumber);
        Assert.True(loaded1001.Checklist["Visual Inspection"]);
        Assert.Equal(15, loaded1001.Boxes[0].ZLength);
    }
}
