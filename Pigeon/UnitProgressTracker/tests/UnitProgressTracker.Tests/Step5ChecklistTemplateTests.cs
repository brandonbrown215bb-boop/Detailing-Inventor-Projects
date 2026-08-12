using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Step5ChecklistTemplateTests
{
    [Fact]
    public void Rescan_InitializesNewSurfacesFromProjectChecklistTemplate()
    {
        var customTemplate = new List<string> { "Custom Item 1", "Custom Item 2" };
        var scannedCandidates = new List<SurfaceModel>
        {
            new SurfaceModel { SurfaceNumber = "SURF-NEW-100" }
        };

        var result = RescanReconciler.Reconcile(
            existingSurfaces: Enumerable.Empty<SurfaceModel>(),
            scannedCandidates: scannedCandidates,
            checklistTemplate: customTemplate);

        Assert.Single(result.NewSurfaces);
        var newSurf = result.NewSurfaces[0];
        Assert.Equal(2, newSurf.Checklist.Count);
        Assert.True(newSurf.Checklist.ContainsKey("Custom Item 1"));
        Assert.False(newSurf.Checklist["Custom Item 1"]);
        Assert.True(newSurf.Checklist.ContainsKey("Custom Item 2"));
        Assert.False(newSurf.Checklist["Custom Item 2"]);
    }

    [Fact]
    public void Rescan_PreservesExistingChecklistWork_OnExactMatches()
    {
        var existing = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "SURF-101",
                Checklist = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Verified dimensions"] = true,
                    ["Custom Item 1"] = true
                }
            }
        };

        var scanned = new List<SurfaceModel>
        {
            new SurfaceModel { SurfaceNumber = "SURF-101" }
        };

        var customTemplate = new List<string> { "Verified dimensions", "Verified material", "New Item" };

        var result = RescanReconciler.Reconcile(existing, scanned, customTemplate);

        Assert.Single(result.ExactMatches);
        var matched = result.ExactMatches[0];
        Assert.True(matched.Checklist["Verified dimensions"]);
        Assert.True(matched.Checklist["Custom Item 1"]);
    }

    [Fact]
    public void SyncChecklistTemplateToSurfaces_AppendsNewItems_WithoutOverwritingExistingWork()
    {
        var project = new ProjectStateModel();
        project.Preferences.ChecklistTemplate = new List<string> { "Item A", "Item B" };

        var rec = new SurfaceRecordModel
        {
            DisplayNumber = "101",
            Checklist = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["Item A"] = true
            }
        };
        project.Surfaces["SURF-101"] = rec;

        var newTemplate = new List<string> { "Item A", "Item B", "Item C" };
        ProjectStateService.SyncChecklistTemplateToSurfaces(project, newTemplate);

        Assert.Equal(3, project.Preferences.ChecklistTemplate.Count);
        Assert.True(rec.Checklist["Item A"]); // Existing true value preserved
        Assert.False(rec.Checklist["Item B"]); // Added from template, defaulted false
        Assert.False(rec.Checklist["Item C"]); // Added from template, defaulted false
    }

    [Fact]
    public void SaveAndReopen_PreservesProjectChecklistTemplate_AndSurfaceWork()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"step5_checklist_{Guid.NewGuid():N}.uptproj");
        try
        {
            var project = new ProjectStateModel
            {
                SourceFolder = @"C:\TestFolder",
                Preferences = new DisplayPreferences
                {
                    ChecklistTemplate = new List<string> { "QA Check", "Safety Seal" }
                }
            };
            project.Surfaces["SURF-201"] = new SurfaceRecordModel
            {
                DisplayNumber = "201",
                Checklist = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["QA Check"] = true,
                    ["Safety Seal"] = false
                }
            };
            project.Geometry["SURF-201"] = new SurfaceModel
            {
                SurfaceNumber = "SURF-201",
                Boxes = new List<GeometryBox> { new(0, 0, 0, 10, 2, 8) }
            };

            ProjectSerializer.SaveAtomic(tempFile, project);
            var reloaded = ProjectSerializer.Load<ProjectStateModel>(tempFile);

            Assert.NotNull(reloaded);
            Assert.Equal(2, reloaded.Preferences.ChecklistTemplate.Count);
            Assert.Contains("QA Check", reloaded.Preferences.ChecklistTemplate);
            Assert.Contains("Safety Seal", reloaded.Preferences.ChecklistTemplate);

            var surfRec = reloaded.Surfaces["SURF-201"];
            Assert.True(surfRec.Checklist["QA Check"]);
            Assert.False(surfRec.Checklist["Safety Seal"]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void SyncChecklistTemplate_HandlesCaseInsensitiveDuplicatesDeterministically()
    {
        var project = new ProjectStateModel();
        var rawTemplate = new List<string> { "item a", "Item A", "ITEM A  ", "Item B" };

        ProjectStateService.SyncChecklistTemplateToSurfaces(project, rawTemplate);

        Assert.Equal(2, project.Preferences.ChecklistTemplate.Count);
        Assert.Equal("item a", project.Preferences.ChecklistTemplate[0]);
        Assert.Equal("Item B", project.Preferences.ChecklistTemplate[1]);
    }

    [Fact]
    public void OptionsChecklistTemplate_RejectsCaseOnlyDuplicates()
    {
        var preferences = new DisplayPreferences { ChecklistTemplate = new List<string> { "QA Check" } };
        var vm = new OptionsViewModel(preferences, StatusStateService.GetDefaultStates());

        vm.AddChecklistTemplateItemCommand.Execute("  qa check  ");

        Assert.Single(vm.ChecklistTemplate);
        Assert.Equal("QA Check", vm.ChecklistTemplate[0]);
    }
}
