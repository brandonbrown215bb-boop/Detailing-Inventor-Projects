using System;
using System.Collections.Generic;
using System.IO;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Step7ReplaceSurfaceTests
{
    [Fact]
    public void ReplaceSurface_SameIdentity_RefreshesGeometryAndPreservesTracking()
    {
        var project = new ProjectStateModel();
        var originalRecord = new SurfaceRecordModel
        {
            DisplayNumber = "SURF-1001",
            StateId = "built",
            Notes = "Inspected quality",
            Checklist = new Dictionary<string, bool> { { "Visual Inspection", true } },
            GeometryFingerprint = "old-fingerprint"
        };
        project.Surfaces["SURF-1001"] = originalRecord;

        var existingSurface = new SurfaceModel
        {
            SurfaceNumber = "SURF-1001",
            DisplayNumber = "SURF-1001",
            StateId = "built",
            Notes = "Inspected quality",
            Checklist = new Dictionary<string, bool> { { "Visual Inspection", true } },
            Boxes = new List<GeometryBox> { new GeometryBox(0, 0, 0, 10, 10, 10) }
        };

        var replacementCandidate = new SurfaceModel
        {
            SurfaceNumber = "SURF-1001",
            DisplayNumber = "SURF-1001",
            Boxes = new List<GeometryBox> { new GeometryBox(0, 0, 0, 50, 50, 50) }
        };

        var result = ProjectStateService.ReplaceSurfaceInPlace(
            project,
            existingSurface,
            replacementCandidate,
            new[] { existingSurface });

        Assert.True(result.Success);
        Assert.False(result.Renumbered);
        Assert.True(result.TrackingTransferred);
        Assert.Equal(50.0, existingSurface.Boxes[0].XLength);
        Assert.Equal("built", originalRecord.StateId);
        Assert.Equal("Inspected quality", originalRecord.Notes);
        Assert.True(originalRecord.Checklist["Visual Inspection"]);
    }

    [Fact]
    public void ReplaceSurface_ChangedIdentityAndShape_TransfersTrackingAndRecordsLineage()
    {
        var project = new ProjectStateModel();
        var originalRecord = new SurfaceRecordModel
        {
            DisplayNumber = "SURF-1001",
            StateId = "in_progress",
            Notes = "Old notes",
            Checklist = new Dictionary<string, bool> { { "Prep", true } },
            GeometryFingerprint = "old-fp"
        };
        project.Surfaces["SURF-1001"] = originalRecord;

        var existingSurface = new SurfaceModel
        {
            SurfaceNumber = "SURF-1001",
            DisplayNumber = "SURF-1001",
            StateId = "in_progress",
            Notes = "Old notes",
            Checklist = new Dictionary<string, bool> { { "Prep", true } },
            Boxes = new List<GeometryBox> { new GeometryBox(0, 0, 0, 10, 10, 10) }
        };

        var replacementCandidate = new SurfaceModel
        {
            SurfaceNumber = "SURF-1002",
            DisplayNumber = "SURF-1002",
            Boxes = new List<GeometryBox> { new GeometryBox(20, 20, 20, 30, 30, 30) }
        };

        var result = ProjectStateService.ReplaceSurfaceInPlace(
            project,
            existingSurface,
            replacementCandidate,
            new[] { existingSurface },
            confirmRenumberTransfer: true);

        Assert.True(result.Success);
        Assert.True(result.Renumbered);
        Assert.True(result.TrackingTransferred);

        Assert.False(project.Surfaces.ContainsKey("SURF-1001"));
        Assert.True(project.Surfaces.ContainsKey("SURF-1002"));

        Assert.True(project.Retired.ContainsKey("SURF-1001"));
        Assert.Equal("replace", project.Retired["SURF-1001"].TransferType);
        Assert.Equal("SURF-1002", project.Retired["SURF-1001"].SupersededBy);

        Assert.Contains("SURF-1001", replacementCandidate.PreviousNumbers);
        Assert.Equal("in_progress", replacementCandidate.StateId);
        Assert.Equal("Old notes", replacementCandidate.Notes);
        Assert.True(replacementCandidate.Checklist["Prep"]);
    }

    [Fact]
    public void ReplaceSurface_DetectsIntrusion_SetsPersistentIntrusionFlag()
    {
        var project = new ProjectStateModel();
        project.Surfaces["SURF-1001"] = new SurfaceRecordModel { DisplayNumber = "SURF-1001" };
        project.Surfaces["SURF-1002"] = new SurfaceRecordModel { DisplayNumber = "SURF-1002" };

        var existingSurface = new SurfaceModel
        {
            SurfaceNumber = "SURF-1001",
            DisplayNumber = "SURF-1001",
            Boxes = new List<GeometryBox> { new GeometryBox(0, 0, 0, 10, 10, 10) }
        };

        var otherSurface = new SurfaceModel
        {
            SurfaceNumber = "SURF-1002",
            DisplayNumber = "SURF-1002",
            Boxes = new List<GeometryBox> { new GeometryBox(5, 5, 5, 15, 15, 15) }
        };

        var replacementCandidate = new SurfaceModel
        {
            SurfaceNumber = "SURF-1001",
            DisplayNumber = "SURF-1001",
            Boxes = new List<GeometryBox> { new GeometryBox(2, 2, 2, 8, 8, 8) }
        };

        var result = ProjectStateService.ReplaceSurfaceInPlace(
            project,
            existingSurface,
            replacementCandidate,
            new[] { replacementCandidate, otherSurface });

        Assert.True(result.Success);
        Assert.True(result.IntrusionDetected);
        Assert.NotEmpty(project.IntrusionFlags);
        Assert.False(project.IntrusionFlags[0].Resolved);
    }

    [Fact]
    public void ReplaceSurface_TopToBottomReplacement_SetsPersistentIntrusionFlag()
    {
        var project = new ProjectStateModel();
        project.Surfaces["TOP-0001"] = new SurfaceRecordModel { DisplayNumber = "TOP-0001" };
        project.Surfaces["BOT-0001"] = new SurfaceRecordModel { DisplayNumber = "BOT-0001" };

        var topSurface = new SurfaceModel
        {
            SurfaceNumber = "TOP-0001",
            DisplayNumber = "TOP-0001",
            Boxes = new List<GeometryBox> { new GeometryBox(0, 0, 80, 100, 50, 2) }
        };

        var existingBottomSurface = new SurfaceModel
        {
            SurfaceNumber = "BOT-0001",
            DisplayNumber = "BOT-0001",
            Boxes = new List<GeometryBox> { new GeometryBox(0, 0, 0, 100, 50, 2) }
        };

        // Replacement surface has bottom surface geometry (at Z=0..2, overlapping BOT-0001)
        var bottomReplacementCandidate = new SurfaceModel
        {
            SurfaceNumber = "BOT-0002",
            DisplayNumber = "BOT-0002",
            Boxes = new List<GeometryBox> { new GeometryBox(10, 10, 0, 80, 30, 2) }
        };

        var activeSurfaces = new List<SurfaceModel> { topSurface, existingBottomSurface };

        var result = ProjectStateService.ReplaceSurfaceInPlace(
            project,
            topSurface,
            bottomReplacementCandidate,
            activeSurfaces,
            confirmRenumberTransfer: true);

        Assert.True(result.Success);
        Assert.True(result.IntrusionDetected);
        Assert.NotEmpty(project.IntrusionFlags);
        Assert.Contains(project.IntrusionFlags, f => f.SurfaceNumber == "BOT-0002" && f.AffectedSurfaceNumbers.Contains("BOT-0001"));
        Assert.False(project.IntrusionFlags.First(f => f.SurfaceNumber == "BOT-0002").Resolved);
    }

    [Fact]
    public void ReplaceSurface_SaveAndReopen_PersistsReplacedSurfaceAndRetiredLineage()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "ReplaceTest_" + Guid.NewGuid() + ".uptproj");
        try
        {
            var project = new ProjectStateModel();
            project.Surfaces["SURF-1001"] = new SurfaceRecordModel
            {
                DisplayNumber = "SURF-1001",
                StateId = "built",
                Notes = "Original note"
            };

            var existingSurface = new SurfaceModel
            {
                SurfaceNumber = "SURF-1001",
                DisplayNumber = "SURF-1001",
                Boxes = new List<GeometryBox> { new GeometryBox(0, 0, 0, 10, 10, 10) }
            };
            var replacementCandidate = new SurfaceModel
            {
                SurfaceNumber = "SURF-1002",
                DisplayNumber = "SURF-1002",
                Boxes = new List<GeometryBox> { new GeometryBox(20, 0, 0, 10, 10, 10) }
            };

            var result = ProjectStateService.ReplaceSurfaceInPlace(
                project,
                existingSurface,
                replacementCandidate,
                new[] { existingSurface },
                confirmRenumberTransfer: true);
            Assert.True(result.Success);

            ProjectSerializer.SaveAtomic(tempFile, project);
            var reloaded = ProjectSerializer.Load<ProjectStateModel>(tempFile);
            Assert.NotNull(reloaded);

            Assert.True(reloaded.Surfaces.ContainsKey("SURF-1002"));
            Assert.True(reloaded.Retired.ContainsKey("SURF-1001"));
            Assert.Equal("replace", reloaded.Retired["SURF-1001"].TransferType);
            Assert.Equal("SURF-1002", reloaded.Retired["SURF-1001"].SupersededBy);
            Assert.Contains("SURF-1001", reloaded.Surfaces["SURF-1002"].PreviousNumbers);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReplaceSurface_RenumberWithoutConfirmation_PreservesProject()
    {
        var project = new ProjectStateModel();
        project.Surfaces["OLD"] = new SurfaceRecordModel { DisplayNumber = "OLD", StateId = "built" };
        var existing = new SurfaceModel
        {
            SurfaceNumber = "OLD",
            StateId = "built",
            Boxes = new List<GeometryBox> { new GeometryBox(0, 0, 0, 10, 10, 10) }
        };
        var candidate = new SurfaceModel
        {
            SurfaceNumber = "NEW",
            Boxes = new List<GeometryBox> { new GeometryBox(20, 0, 0, 10, 10, 10) }
        };

        var result = ProjectStateService.ReplaceSurfaceInPlace(project, existing, candidate, new[] { existing });

        Assert.False(result.Success);
        Assert.True(result.RequiresRenumberConfirmation);
        Assert.True(project.Surfaces.ContainsKey("OLD"));
        Assert.False(project.Surfaces.ContainsKey("NEW"));
        Assert.Empty(project.Retired);
    }

    [Fact]
    public void ReplaceSurface_InvalidGeometryOrDuplicateIdentity_PreservesProject()
    {
        var project = new ProjectStateModel();
        project.Surfaces["OLD"] = new SurfaceRecordModel { DisplayNumber = "OLD" };
        project.Surfaces["TAKEN"] = new SurfaceRecordModel { DisplayNumber = "TAKEN" };
        var existing = new SurfaceModel { SurfaceNumber = "OLD", Boxes = new List<GeometryBox> { new(0, 0, 0, 10, 10, 10) } };
        var taken = new SurfaceModel { SurfaceNumber = "TAKEN", Boxes = new List<GeometryBox> { new(20, 0, 0, 10, 10, 10) } };

        var invalid = ProjectStateService.ReplaceSurfaceInPlace(
            project,
            existing,
            new SurfaceModel { SurfaceNumber = "NEW" },
            new[] { existing, taken },
            confirmRenumberTransfer: true);
        var duplicate = ProjectStateService.ReplaceSurfaceInPlace(
            project,
            existing,
            new SurfaceModel { SurfaceNumber = "TAKEN", Boxes = new List<GeometryBox> { new(40, 0, 0, 10, 10, 10) } },
            new[] { existing, taken },
            confirmRenumberTransfer: true);

        Assert.False(invalid.Success);
        Assert.False(duplicate.Success);
        Assert.Equal(2, project.Surfaces.Count);
        Assert.Empty(project.Retired);
    }
}
