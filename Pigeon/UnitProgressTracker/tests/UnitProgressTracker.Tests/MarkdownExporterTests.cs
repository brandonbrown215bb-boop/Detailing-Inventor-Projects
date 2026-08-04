using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class MarkdownExporterTests
{
    [Fact]
    public void GenerateAuditReport_FullProjectStateModel_ProducesCompleteMarkdownReport()
    {
        var surfaces = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "391-1001-01",
                PartNumber = "391-1001",
                SurfaceType = "Roof Panel",
                SurfaceUnitSide = "Top",
                StateId = "built",
                IsHidden = false,
                Checklist = new Dictionary<string, bool> { ["Visual"] = true, ["Torque"] = true },
                Notes = "Inspected and approved"
            },
            new SurfaceModel
            {
                SurfaceNumber = "391-1002-01",
                PartNumber = "391-1002",
                SurfaceType = "Floor Panel",
                SurfaceUnitSide = "Bottom",
                StateId = "corrected",
                IsHidden = true,
                Checklist = new Dictionary<string, bool> { ["Visual"] = true, ["Seal"] = false },
                Notes = "Pending seal re-check"
            }
        };

        var states = StatusStateService.GetDefaultStates();
        string markdown = MarkdownExporter.GenerateAuditReport(surfaces, states);

        Assert.NotNull(markdown);
        Assert.Contains("# Unit Progress Tracker — Surface Audit Report", markdown);
        Assert.Contains("**Total Surfaces:** 2", markdown);
        Assert.Contains("**Active (Visible):** 1", markdown);
        Assert.Contains("**Hidden:** 1", markdown);
        Assert.Contains("## Status Breakdown", markdown);
        Assert.Contains("## Surface Details", markdown);
        Assert.Contains("391-1001-01", markdown);
        Assert.Contains("391-1002-01", markdown);
        Assert.Contains("Inspected and approved", markdown);
    }

    [Fact]
    public void GenerateAuditReport_FormatsChecklistItems_WithCheckedAndUncheckedVerification()
    {
        var surfaces = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "SURF-A",
                PartNumber = "391-001",
                Checklist = new Dictionary<string, bool> { ["Visual"] = true, ["Dimension"] = true }
            },
            new SurfaceModel
            {
                SurfaceNumber = "SURF-B",
                PartNumber = "391-002",
                Checklist = new Dictionary<string, bool> { ["Visual"] = true, ["Leak Test"] = false }
            },
            new SurfaceModel
            {
                SurfaceNumber = "SURF-C",
                PartNumber = "391-003",
                Checklist = new Dictionary<string, bool>()
            }
        };

        string md = MarkdownExporter.GenerateAuditReport(surfaces, StatusStateService.GetDefaultStates());

        Assert.Contains("2/2", md);
        Assert.Contains("1/2", md);
        Assert.Contains("N/A", md);
        Assert.Contains("- [x] Visual", md);
        Assert.Contains("- [ ] Leak Test", md);
    }

    [Fact]
    public void GenerateAuditReport_FormatsNotes_SanitizesNewlinesAndPipes()
    {
        var surfaces = new List<SurfaceModel>
        {
            new SurfaceModel { SurfaceNumber = "SURF-1", Notes = "Line 1\nLine 2\r\nLine 3" },
            new SurfaceModel { SurfaceNumber = "SURF-2", Notes = "Value | Spec" },
            new SurfaceModel { SurfaceNumber = "SURF-3", Notes = "" }
        };

        string md = MarkdownExporter.GenerateAuditReport(surfaces, StatusStateService.GetDefaultStates());

        var lines = md.Split('\n').Select(l => l.TrimEnd()).Where(l => l.StartsWith("| SURF-")).ToList();
        Assert.Equal(3, lines.Count);

        Assert.Contains("Line 1 Line 2 Line 3", lines[0]);
        Assert.Contains(@"Value \| Spec", lines[1]);
        Assert.Contains("—", lines[2]);
    }

    [Fact]
    public void GenerateAuditReport_WithRetiredSurfaces_RendersRetiredSurfaceLineageTable()
    {
        var project = new ProjectStateModel
        {
            Version = 2,
            SourceFolder = @"C:\Units\AHU_01",
            Surfaces = new Dictionary<string, SurfaceRecordModel>
            {
                ["SURF-1001"] = new SurfaceRecordModel { DisplayNumber = "1001", StateId = "built" }
            },
            Retired = new Dictionary<string, RetiredSurfaceRecordModel>
            {
                ["SURF-0999"] = new RetiredSurfaceRecordModel
                {
                    RetiredAt = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc),
                    SupersededBy = "1001",
                    TransferType = "renumber",
                    FileKey = "casing_panel_01.ipt",
                    GeometryFingerprint = "10.0,20.0,30.0"
                }
            }
        };

        string md = MarkdownExporter.ExportToMarkdown(project, StatusStateService.GetDefaultStates());

        Assert.Contains("## Retired Surface Lineage Audit", md);
        Assert.Contains("| Surface Number | Superseded By | Transfer Type | Retired At | File Key | Fingerprint |", md);
        Assert.Contains("SURF-0999", md);
        Assert.Contains("1001", md);
        Assert.Contains("renumber", md);
        Assert.Contains("casing_panel_01.ipt", md);
    }

    [Fact]
    public void ExportToMarkdown_NullProject_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => MarkdownExporter.ExportToMarkdown(null!, StatusStateService.GetDefaultStates()));
    }

    [Fact]
    public void SaveAuditReport_WritesToFileSuccessfully()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"audit_export_{Guid.NewGuid():N}.md");
        try
        {
            var surfaces = new List<SurfaceModel>
            {
                new SurfaceModel { SurfaceNumber = "391-100", PartNumber = "391-100", StateId = "done" }
            };

            MarkdownExporter.SaveAuditReport(tempFile, surfaces, StatusStateService.GetDefaultStates());

            Assert.True(File.Exists(tempFile));
            string content = File.ReadAllText(tempFile);
            Assert.Contains("# Unit Progress Tracker — Surface Audit Report", content);
            Assert.Contains("391-100", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
