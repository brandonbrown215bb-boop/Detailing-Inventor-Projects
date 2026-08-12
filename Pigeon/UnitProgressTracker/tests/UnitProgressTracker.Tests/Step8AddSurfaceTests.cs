using System;
using System.Collections.Generic;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Step8AddSurfaceTests
{
    [Fact]
    public void UPT_C_016_BuildAddProposal_IsNonMutatingAndReportsDuplicatesAndInvalidGeometry()
    {
        var existing = Surface("SURF-1001", new GeometryBox(0, 0, 0, 10, 10, 10));
        existing.StateId = "done";
        existing.Notes = "Keep me";
        var project = ProjectWith(existing);
        project.Bom = new BomImportResult { KeptRows = new List<BomRow> { new() { PartNumber = "391-KEEP" } } };

        var duplicate = Surface("SURF-1001", new GeometryBox(20, 0, 0, 10, 10, 10));
        var accepted = Surface("SURF-1002", new GeometryBox(30, 0, 0, 10, 10, 10));
        var invalid = new SurfaceModel { SurfaceNumber = "SURF-1003" };

        var proposal = ProjectStateService.BuildAddSurfacesProposal(
            project,
            new[] { existing },
            new[] { duplicate, accepted, invalid });

        Assert.Single(proposal.AcceptedSurfaces);
        Assert.Equal("SURF-1002", proposal.AcceptedSurfaces[0].SurfaceNumber);
        Assert.Equal(2, proposal.Issues.Count);
        Assert.Contains(proposal.Issues, issue => issue.Kind == SurfaceOperationIssueKind.DuplicateIdentity);
        Assert.Contains(proposal.Issues, issue => issue.Kind == SurfaceOperationIssueKind.InvalidGeometry);
        Assert.Single(project.Surfaces);
        Assert.Single(project.Geometry);
        Assert.Equal("Keep me", project.Surfaces["SURF-1001"].Notes);
        Assert.Single(project.Bom.KeptRows);
    }

    [Fact]
    public void UPT_C_016_ApplyAddProposal_PreservesProjectStateAndInitializesAcceptedSurfaces()
    {
        var existing = Surface("SURF-1001", new GeometryBox(0, 0, 0, 10, 10, 10));
        existing.StateId = "done";
        existing.Notes = "Existing tracking";
        existing.Checklist["Existing item"] = true;

        var project = ProjectWith(existing);
        project.SourceFolder = "C:\\unit-source";
        project.Preferences.ChecklistTemplate = new List<string> { "Template A", "Template B" };
        project.Bom = new BomImportResult { KeptRows = new List<BomRow> { new() { PartNumber = "391-KEEP" } } };

        var candidate = Surface("SURF-1002", new GeometryBox(5, 0, 0, 10, 10, 10));
        var proposal = ProjectStateService.BuildAddSurfacesProposal(project, new[] { existing }, new[] { candidate });
        var result = ProjectStateService.ApplyAddSurfacesProposal(project, proposal, new[] { existing });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, project.Surfaces.Count);
        Assert.Equal("Existing tracking", project.Surfaces["SURF-1001"].Notes);
        Assert.True(project.Surfaces["SURF-1001"].Checklist["Existing item"]);
        Assert.Equal("current", project.Surfaces["SURF-1002"].StateId);
        Assert.Equal(new[] { "Template A", "Template B" }, project.Surfaces["SURF-1002"].Checklist.Keys.OrderBy(key => key));
        Assert.All(project.Surfaces["SURF-1002"].Checklist.Values, Assert.False);
        Assert.Equal("C:\\unit-source", project.SourceFolder);
        Assert.Single(project.Bom!.KeptRows);
        Assert.Contains(project.IntrusionFlags, flag =>
            !flag.Resolved &&
            (flag.SurfaceNumber == "SURF-1002" || flag.AffectedSurfaceNumbers.Contains("SURF-1002")));
    }

    private static ProjectStateModel ProjectWith(SurfaceModel surface)
    {
        return new ProjectStateModel
        {
            Surfaces = new Dictionary<string, SurfaceRecordModel>(StringComparer.OrdinalIgnoreCase)
            {
                [surface.SurfaceNumber] = new()
                {
                    DisplayNumber = surface.EffectiveDisplayNumber,
                    StateId = surface.StateId,
                    Notes = surface.Notes,
                    Checklist = new Dictionary<string, bool>(surface.Checklist, StringComparer.OrdinalIgnoreCase),
                    GeometryFingerprint = GeometryFingerprinter.CalculateFingerprint(surface)
                }
            },
            Geometry = new Dictionary<string, SurfaceModel>(StringComparer.OrdinalIgnoreCase)
            {
                [surface.SurfaceNumber] = surface
            }
        };
    }

    private static SurfaceModel Surface(string number, GeometryBox box)
        => new()
        {
            SurfaceNumber = number,
            DisplayNumber = number,
            Boxes = new List<GeometryBox> { box },
            GeometryFingerprint = $"fp-{number}"
        };
}
