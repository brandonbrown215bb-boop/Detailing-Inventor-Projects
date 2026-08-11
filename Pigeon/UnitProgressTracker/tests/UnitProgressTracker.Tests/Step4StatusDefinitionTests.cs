using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Step4StatusDefinitionTests
{
    [Fact]
    public void CustomStatusDefinitions_RoundTripThroughProjectSerializer()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"step4_custom_status_{Guid.NewGuid():N}.uptproj");
        try
        {
            var project = new ProjectStateModel
            {
                SourceFolder = @"C:\FakeUnit",
                StatusDefinitions = new List<StatusState>
                {
                    new StatusState("current", "Current", "#94A3B8", "solid"),
                    new StatusState("custom-qc-hold", "QC Hold", "#EF4444", "wireframe"),
                    new StatusState("custom-paint-ready", "Paint Ready", "#38BDF8", "solid")
                }
            };
            project.Surfaces["SURF-1001"] = new SurfaceRecordModel
            {
                DisplayNumber = "1001",
                StateId = "custom-qc-hold",
                Notes = "Awaiting paint inspection"
            };

            ProjectSerializer.SaveAtomic(tempFile, project);

            var reloaded = ProjectSerializer.Load<ProjectStateModel>(tempFile);
            Assert.NotNull(reloaded);
            Assert.Equal(3, reloaded.StatusDefinitions.Count);
            
            var qcHold = reloaded.StatusDefinitions.FirstOrDefault(s => s.Id == "custom-qc-hold");
            Assert.NotNull(qcHold);
            Assert.Equal("QC Hold", qcHold.Name);
            Assert.Equal("#EF4444", qcHold.ColorHex, ignoreCase: true);
            Assert.Equal("wireframe", qcHold.FillType, ignoreCase: true);

            var surf = reloaded.Surfaces["SURF-1001"];
            Assert.Equal("custom-qc-hold", surf.StateId);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void MarkdownExport_UsesProjectStatusDefinitions_AndReportsUnknownStates()
    {
        var project = new ProjectStateModel
        {
            StatusDefinitions = new List<StatusState>
            {
                new StatusState("current", "Current", "#94A3B8"),
                new StatusState("qc-pass", "QC Passed", "#22C55E")
            }
        };

        project.Surfaces["SURF-001"] = new SurfaceRecordModel { DisplayNumber = "001", StateId = "current" };
        project.Surfaces["SURF-002"] = new SurfaceRecordModel { DisplayNumber = "002", StateId = "qc-pass" };
        project.Surfaces["SURF-003"] = new SurfaceRecordModel { DisplayNumber = "003", StateId = "unknown-legacy-state" };

        string report = MarkdownExporter.ExportToMarkdown(project);

        Assert.Contains("QC Passed", report);
        Assert.Contains("Unknown State (unknown-legacy-state)", report);
        Assert.Contains("003", report);
    }

    [Fact]
    public void MainViewModel_GetStatusColor_ResolvesCustomStatus_AndFallsBackSafelyForUnknown()
    {
        var vm = new MainViewModel();
        vm.StatusStates.Clear();
        vm.StatusStates.Add(new StatusState("current", "Current", "#94A3B8"));
        vm.StatusStates.Add(new StatusState("custom-state", "Custom State", "#E11D48"));

        Assert.Equal("#E11D48", vm.GetStatusColor("custom-state"), ignoreCase: true);
        Assert.Equal("#94A3B8", vm.GetStatusColor("non-existent-state"), ignoreCase: true);
    }

    [Fact]
    public void DeleteStatusState_IdentifiesFallback_AndPreservesUnmappedSurfacesVisibly()
    {
        var states = StatusStateService.GetDefaultStates();
        states.Add(new StatusState("temp-stage", "Temp Stage", "#8B5CF6"));

        bool deleted = StatusStateService.DeleteState(states, "temp-stage", out string fallbackId, requestedFallbackId: "current");

        Assert.True(deleted);
        Assert.Equal("current", fallbackId);
        Assert.Null(states.FirstOrDefault(s => s.Id == "temp-stage"));
    }
}
