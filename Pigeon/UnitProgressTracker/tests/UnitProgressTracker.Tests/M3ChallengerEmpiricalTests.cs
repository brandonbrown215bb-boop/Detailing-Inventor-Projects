using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class M3ChallengerEmpiricalTests
{
    // =========================================================================
    // 1. StatusStateManager & StatusStateService Color Hex Normalization Tests
    // =========================================================================

    [Theory]
    [InlineData("1234", "#94A3B8")]       // 4 chars without # is invalid for RGB/ARGB
    [InlineData("#1234", "#94A3B8")]      // 4 chars with # is invalid
    [InlineData("12345", "#94A3B8")]      // 5 chars invalid
    [InlineData("#GGGGGG", "#94A3B8")]    // Non-hex chars invalid
    [InlineData("red", "#94A3B8")]        // CSS color names invalid
    [InlineData(null, "#94A3B8")]         // Null hex invalid
    [InlineData("", "#94A3B8")]           // Empty string invalid
    [InlineData("   ", "#94A3B8")]        // Whitespace invalid
    [InlineData("#abc", "#AABBCC")]       // 3-digit hex with hash
    [InlineData("abc", "#AABBCC")]        // 3-digit hex without hash
    [InlineData("#123", "#112233")]       // 3-digit hex #123 expands to #112233
    [InlineData("3b82f6", "#3B82F6")]     // 6-digit hex without hash
    [InlineData("#3b82f6", "#3B82F6")]    // 6-digit hex with hash
    [InlineData("  #3b82f6  ", "#3B82F6")]// Hex with whitespace padding
    [InlineData("80FF0000", "#80FF0000")] // 8-digit ARGB hex without hash
    [InlineData("#80FF0000", "#80FF0000")]// 8-digit ARGB hex with hash
    public void StatusStateService_NormalizeHexColor_HandlesAllHexInputsCorrectly(string? inputHex, string expectedHex)
    {
        string result = StatusStateService.NormalizeHexColor(inputHex);
        Assert.Equal(expectedHex, result);
    }

    // =========================================================================
    // 2. StatusStateManager FillType Handling & Normalization
    // =========================================================================

    [Theory]
    [InlineData("solid", "solid")]
    [InlineData("SOLID", "solid")]
    [InlineData(" wireframe ", "wireframe")]
    [InlineData("WIREframe", "wireframe")]
    [InlineData("invalid_fill", "solid")]
    [InlineData(null, "solid")]
    [InlineData("", "solid")]
    public void StatusStateService_NormalizeFillType_NormalizesToSolidOrWireframe(string? inputFill, string expectedFill)
    {
        string result = StatusStateService.NormalizeFillType(inputFill);
        Assert.Equal(expectedFill, result);
    }

    // =========================================================================
    // 3. StatusStateManager State Addition & ID Collision Stress
    // =========================================================================

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void StatusStateManager_AddState_IDCollisionCaseInsensitive_ReturnsFalse()
    {
        var manager = new StatusStateManager();

        bool addLower = manager.AddState(new StatusState("BUILT", "Duplicate Built", "#000000"));
        Assert.False(addLower, "Should reject adding duplicate state ID 'BUILT' when 'built' exists.");

        StatusState? addSpaced = manager.AddState("  built  ", "Spaced Built", "#111111");
        Assert.Null(addSpaced);
    }

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void StatusStateManager_AddState_NullOrEmptyIdOrName_FailsGracefully()
    {
        var manager = new StatusStateManager();

        Assert.False(manager.AddState(new StatusState("", "Valid Name", "#123456")));
        Assert.False(manager.AddState(new StatusState("   ", "Valid Name", "#123456")));
        Assert.False(manager.AddState(new StatusState("valid-id", "", "#123456")));
        Assert.False(manager.AddState(new StatusState("valid-id-2", "   ", "#123456")));
        Assert.Null(manager.AddState("", "Name", "#123456"));
        Assert.Null(manager.AddState("valid-id-3", "", "#123456"));
    }

    // =========================================================================
    // 4. StatusStateManager Deletion Protection & Fallback
    // =========================================================================

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void StatusStateManager_DeleteState_BuiltInProtection_AllDefaultStatesProtected()
    {
        var manager = new StatusStateManager();
        var defaultIds = StatusState.DefaultStates.Select(s => s.Id).ToList();

        foreach (var id in defaultIds)
        {
            bool deleted = manager.DeleteState(id);
            Assert.False(deleted, $"Default state '{id}' should be protected from deletion.");
            Assert.NotNull(manager.GetState(id));
        }

        Assert.False(manager.DeleteState("CURRENT"));
        Assert.False(manager.DeleteState("  DONE  "));
    }

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void StatusStateManager_DeleteCustomState_DeterminesRequestedOrFirstFallback()
    {
        var manager = new StatusStateManager();
        manager.AddState("custom-1", "Custom 1", "#111111");
        manager.AddState("custom-2", "Custom 2", "#222222");

        bool deleted = manager.DeleteState("custom-1", out string fallback, "built");
        Assert.True(deleted);
        Assert.Equal("built", fallback);

        bool deleted2 = manager.DeleteState("custom-2", out string fallback2, "non-existent");
        Assert.True(deleted2);
        Assert.Equal("current", fallback2);
    }

    // =========================================================================
    // 5. Initial States Constructor Edge Cases
    // =========================================================================

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void StatusStateManager_Constructor_InitialStatesWithUnnormalizedValues_NormalizesAll()
    {
        var initial = new List<StatusState>
        {
            new StatusState("s1", "State 1", "ff0000", "WIREframe"),
            new StatusState("s2", "State 2", "#abc", "INVALID_FILL")
        };

        var manager = new StatusStateManager(initial);
        Assert.Equal(2, manager.States.Count);

        var s1 = manager.GetState("s1");
        Assert.NotNull(s1);
        Assert.Equal("#FF0000", s1.ColorHex);
        Assert.Equal("wireframe", s1.FillType);

        var s2 = manager.GetState("s2");
        Assert.NotNull(s2);
        Assert.Equal("#AABBCC", s2.ColorHex);
        Assert.Equal("solid", s2.FillType);
    }

    // =========================================================================
    // 6. MarkdownExporter Table Pipe & Newline Injection (Vulnerability Tests)
    // =========================================================================

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void MarkdownExporter_UnescapedNewlinesInPartNumber_SplitsTableRowIntoMultipleLines()
    {
        var surfaces = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "391-100",
                PartNumber = "391-1001\nINJECTED_LINE",
                SurfaceType = "Roof Panel",
                SurfaceUnitSide = "Top",
                StateId = "built",
                Notes = "Clean Note"
            }
        };

        string md = MarkdownExporter.GenerateAuditReport(surfaces, StatusStateService.GetDefaultStates());

        var lines = md.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        bool lineSplitDetected = lines.Any(l => l.StartsWith("INJECTED_LINE"));
        Assert.True(lineSplitDetected, "Empirical check: Unescaped newline in PartNumber splits Markdown table row.");
    }

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void MarkdownExporter_UnescapedPipesInSurfaceFields_BreaksMarkdownTableColumns()
    {
        var surfaces = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "SURF-01",
                PartNumber = "PART|100",
                SurfaceType = "Roof|Panel",
                SurfaceUnitSide = "Top|Side",
                StateId = "built",
                Notes = "Clean Note"
            }
        };

        string md = MarkdownExporter.GenerateAuditReport(surfaces, StatusStateService.GetDefaultStates());

        var lines = md.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var tableRow = lines.FirstOrDefault(l => l.StartsWith("| SURF-01"));
        Assert.NotNull(tableRow);
        
        // A valid Markdown table row for 8 columns has exactly 9 pipe separators
        int pipeCount = tableRow.Count(c => c == '|');
        Assert.True(pipeCount > 9, $"Empirical check: Pipe count is {pipeCount} (expected 9), proving unescaped pipe injection in surface fields.");
    }

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void MarkdownExporter_UnescapedPipesInRetiredSurfaceFields_BreaksRetiredTableColumns()
    {
        var project = new ProjectStateModel
        {
            Version = 2,
            SourceFolder = @"C:\Units\AHU_01",
            Surfaces = new Dictionary<string, SurfaceRecordModel>
            {
                ["SURF-100"] = new SurfaceRecordModel { DisplayNumber = "100", StateId = "built" }
            },
            Retired = new Dictionary<string, RetiredSurfaceRecordModel>
            {
                ["RET-01"] = new RetiredSurfaceRecordModel
                {
                    RetiredAt = new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc),
                    SupersededBy = "100|A",
                    TransferType = "renumber|split",
                    FileKey = "panel|01.ipt",
                    GeometryFingerprint = "10|20|30"
                }
            }
        };

        string md = MarkdownExporter.ExportToMarkdown(project, StatusStateService.GetDefaultStates());

        var lines = md.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var retiredRow = lines.FirstOrDefault(l => l.StartsWith("| RET-01"));
        Assert.NotNull(retiredRow);

        // A valid Markdown table row for 6 columns has exactly 7 pipe separators
        int pipeCount = retiredRow.Count(c => c == '|');
        Assert.True(pipeCount > 7, $"Empirical check: Retired table pipe count is {pipeCount} (expected 7), proving unescaped pipe injection in retired fields.");
    }

    // =========================================================================
    // 7. MarkdownExporter Notes & Multiline Checklist Formatting
    // =========================================================================

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void MarkdownExporter_NotesWithMarkdownFormattingAndQuotes_EscapesOrFormatsProperly()
    {
        var surfaces = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "391-100",
                Notes = "Line 1\r\n> Sub-quote line\r\nLine 3 with | pipe"
            }
        };

        string md = MarkdownExporter.GenerateAuditReport(surfaces, StatusStateService.GetDefaultStates());

        Assert.Contains(@"Line 1 > Sub-quote line Line 3 with \| pipe", md);
        Assert.Contains("  > Line 1", md);
        Assert.Contains("  > > Sub-quote line", md);
        Assert.Contains("  > Line 3 with | pipe", md);
    }

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void MarkdownExporter_ChecklistKeysWithNewlinesAndSpecialChars_RendersWithoutBreaking()
    {
        var surfaces = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "391-200",
                Checklist = new Dictionary<string, bool>
                {
                    ["Visual Check\n(Passed)"] = true,
                    ["Torque | 50 ft-lbs"] = false,
                    ["[x] Fake Checkbox"] = true
                }
            }
        };

        string md = MarkdownExporter.GenerateAuditReport(surfaces, StatusStateService.GetDefaultStates());

        Assert.Contains("- [x] Visual Check", md);
        Assert.Contains("- [ ] Torque | 50 ft-lbs", md);
        Assert.Contains("- [x] [x] Fake Checkbox", md);
    }

    // =========================================================================
    // 8. MarkdownExporter Status Breakdown with Custom / Orphaned State IDs
    // =========================================================================

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void MarkdownExporter_SurfacesWithCustomStateIdNotInStatesList_StatusBreakdownBehavior()
    {
        var surfaces = new List<SurfaceModel>
        {
            new SurfaceModel { SurfaceNumber = "S1", StateId = "built" },
            new SurfaceModel { SurfaceNumber = "S2", StateId = "custom-unregistered-state" }
        };

        var states = StatusStateService.GetDefaultStates();

        string md = MarkdownExporter.GenerateAuditReport(surfaces, states);

        Assert.NotNull(md);
        Assert.Contains("S2", md);
        Assert.Contains("custom-unregistered-state", md);
    }

    // =========================================================================
    // 9. Null Handling & Edge Cases
    // =========================================================================

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void MarkdownExporter_GenerateAuditReport_NullActiveSurfacesOrStates_HandledGracefully()
    {
        string md = MarkdownExporter.GenerateAuditReport(null, null!, null!);
        Assert.NotNull(md);
        Assert.Contains("Total Surfaces:** 0", md);
        Assert.Contains("Active (Visible):** 0", md);
        Assert.Contains("Hidden:** 0", md);
    }

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void MarkdownExporter_ExportToMarkdown_ProjectWithNullSurfacesOrNullRetired_HandledGracefully()
    {
        var project = new ProjectStateModel
        {
            Version = 2,
            SourceFolder = null,
            Surfaces = null,
            Retired = null
        };

        string md = MarkdownExporter.ExportToMarkdown(project);
        Assert.NotNull(md);
        Assert.Contains("Project Source Folder:** `N/A`", md);
        Assert.Contains("Total Surfaces:** 0", md);
        Assert.Contains("Retired Surfaces Tracked:** `0`", md);
    }

    // =========================================================================
    // 10. Large Scale Performance & Boundary Conditions
    // =========================================================================

    [Fact]
    [Trait("Category", "EmpiricalM3")]
    public void MarkdownExporter_1000Surfaces_GeneratesFastWithoutMemoryException()
    {
        var surfaces = new List<SurfaceModel>();
        for (int i = 0; i < 1000; i++)
        {
            surfaces.Add(new SurfaceModel
            {
                SurfaceNumber = $"391-{i:D4}",
                PartNumber = $"391-{i / 10:D3}",
                SurfaceType = "Casing Panel",
                SurfaceUnitSide = i % 2 == 0 ? "Left" : "Right",
                StateId = i % 3 == 0 ? "done" : (i % 3 == 1 ? "built" : "current"),
                IsHidden = i % 5 == 0,
                Checklist = new Dictionary<string, bool>
                {
                    ["Visual"] = true,
                    ["Seal"] = i % 2 == 0
                },
                Notes = $"Notes for surface {i}\nLine 2"
            });
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        string md = MarkdownExporter.GenerateAuditReport(surfaces, StatusStateService.GetDefaultStates());
        watch.Stop();

        Assert.NotNull(md);
        Assert.True(watch.ElapsedMilliseconds < 2000, $"Markdown generation took {watch.ElapsedMilliseconds}ms for 1000 surfaces (expected < 2000ms).");
        Assert.Contains("Total Surfaces:** 1000", md);
    }
}
