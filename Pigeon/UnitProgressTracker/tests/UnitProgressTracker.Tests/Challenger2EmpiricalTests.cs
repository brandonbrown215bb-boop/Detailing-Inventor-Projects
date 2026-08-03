using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Challenger2EmpiricalTests
{
    #region Test Suite 1: Path Traversal & Disk Folder Creation Security

    [Fact]
    public void CreateShellFolders_PathTraversalInRelativePath_ThrowsArgumentException()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "UPT_TraversalRoot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var relativePaths = new[]
            {
                "Shell/Skid 01/01 MB/../../../../EscapedFolder"
            };

            Assert.Throws<ArgumentException>(() => BomShellEngine.CreateShellFolders(tempRoot, relativePaths));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CreateShellFolders_AbsolutePathInRelativePath_ThrowsArgumentException()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "UPT_AbsRoot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string outsideTarget = Path.Combine(Path.GetTempPath(), "UPT_OutsideTarget_" + Guid.NewGuid().ToString("N"));

        try
        {
            var relativePaths = new[] { outsideTarget };
            Assert.Throws<ArgumentException>(() => BomShellEngine.CreateShellFolders(tempRoot, relativePaths));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            if (Directory.Exists(outsideTarget)) Directory.Delete(outsideTarget, recursive: true);
        }
    }

    [Fact]
    public void BuildPlan_SkidWithDirectoryTraversal_GeneratesTraversingRelativePath()
    {
        var rows = new List<BomRow>
        {
            new BomRow
            {
                PartNumber = "391-0001",
                Skid = "1 [FR-MB]",
                Segment = "MB",
                Description = "../../Roof Panel"
            }
        };

        var engine = new BomShellEngine();
        var plan = engine.BuildPlan(rows);

        Assert.Single(plan.Entries);
        var entry = plan.Entries[0];
        // Relative path tokens are sanitized
        Assert.DoesNotContain("..", entry.RelativePath);
    }

    [Fact]
    public void SanitizeAssemblyFolderName_Win32ReservedNames_Handling()
    {
        string sanitizedCon = BomShellEngine.SanitizeAssemblyFolderName("CON");
        string sanitizedAux = BomShellEngine.SanitizeAssemblyFolderName("AUX");
        string sanitizedPrn = BomShellEngine.SanitizeAssemblyFolderName("PRN");

        Assert.Equal("CON_", sanitizedCon);
        Assert.Equal("AUX_", sanitizedAux);
        Assert.Equal("PRN_", sanitizedPrn);
    }

    [Fact]
    public void SanitizeAssemblyFolderName_ControlCharacters_Handling()
    {
        string raw = "Panel\0With\tControl\nChars";
        string sanitized = BomShellEngine.SanitizeAssemblyFolderName(raw);
        Assert.DoesNotContain('\0', sanitized);
        Assert.Equal("Panel With Control Chars", sanitized);
    }

    #endregion

    #region Test Suite 2: WPF DataGrid Filtering Pipeline

    [Fact]
    public void MainViewModel_FilteringPipeline_MultiCriteriaExecution()
    {
        var vm = new MainViewModel();
        var rows = new List<BomRow>
        {
            new BomRow { PartNumber = "391-0001", Skid = "1 [FR-MB]", Segment = "MB", Description = "ROOF PANEL SQ TYPE A", ExtDescription = "Galv" },
            new BomRow { PartNumber = "391-0002", Skid = "1 [FR-MB]", Segment = "MB", Description = "CASING PANEL", ExtDescription = "Painted" },
            new BomRow { PartNumber = "391-0003", Skid = "2 [FR-FF]", Segment = "FF", Description = "SQ ACCESS PANEL", ExtDescription = "SST" },
            new BomRow { PartNumber = "391-0004", Skid = "1 [FR-MB]", Segment = "FR", Description = "FRONT PANEL", ExtDescription = "Galv" },
        };

        vm.LoadBomRows(rows);
        Assert.Equal(4, vm.BomEntries.Count);
        Assert.Equal(4, vm.FilteredBomEntries.Count);

        // 1. Skid Filter
        vm.SelectedSkidFilter = "1 [FR-MB]";
        Assert.Equal(3, vm.FilteredBomEntries.Count);
        Assert.All(vm.FilteredBomEntries, e => Assert.Equal("1 [FR-MB]", e.Skid));

        // 2. Segment Filter on top of Skid Filter
        vm.SelectedSegmentFilter = "MB";
        Assert.Equal(2, vm.FilteredBomEntries.Count);
        Assert.All(vm.FilteredBomEntries, e => Assert.Equal("MB", e.Segment));

        // 3. Custom SQ Only toggle
        vm.IsCustomSqOnly = true;
        Assert.Single(vm.FilteredBomEntries);
        Assert.Equal("391-0001", vm.FilteredBomEntries[0].PartNumber);

        // 4. Reset SQ toggle and apply SearchText
        vm.IsCustomSqOnly = false;
        vm.SearchText = "CASING";
        Assert.Single(vm.FilteredBomEntries);
        Assert.Equal("391-0002", vm.FilteredBomEntries[0].PartNumber);

        // 5. Clear filters
        vm.SelectedSkidFilter = "All Skids";
        vm.SelectedSegmentFilter = "All Segments";
        vm.SearchText = "";
        Assert.Equal(4, vm.FilteredBomEntries.Count);
    }

    [Fact]
    public void MainViewModel_SearchText_SearchesExtDescriptionAndRelativePath()
    {
        var vm = new MainViewModel();
        var rows = new List<BomRow>
        {
            new BomRow { PartNumber = "391-0001", Skid = "1 [FR-MB]", Segment = "MB", Description = "Panel A", ExtDescription = "SpecialCoating123" },
            new BomRow { PartNumber = "391-0002", Skid = "1 [FR-MB]", Segment = "MB", Description = "Panel B", ExtDescription = "Standard" }
        };

        vm.LoadBomRows(rows);

        vm.SearchText = "SpecialCoating123";
        Assert.Single(vm.FilteredBomEntries);
        Assert.Equal("391-0001", vm.FilteredBomEntries[0].PartNumber);

        vm.SearchText = "01 MB";
        Assert.Equal(2, vm.FilteredBomEntries.Count);
    }

    #endregion

    #region Test Suite 3: Custom SQ Tagging Logic

    [Theory]
    [InlineData("SQ ASSY", true)]
    [InlineData("PANEL SQ TYPE A", true)]
    [InlineData("SQ-FRAME ASSEMBLY", true)]
    [InlineData("DOOR SQ", true)]
    [InlineData("sq panel", true)]
    [InlineData("SQUARE PANEL", false)]
    [InlineData("MOSQUITO", false)]
    [InlineData("DESQ", false)]
    [InlineData("CASING PANEL", false)]
    public void IsCustomSqAssembly_RegexWordBoundaryMatching(string description, bool expected)
    {
        var row = new BomRow { Description = description };
        Assert.Equal(expected, BomShellEngine.IsCustomSqAssembly(row));
    }

    [Fact]
    public void IsCustomSqAssembly_MatchesInExtDescription()
    {
        var row = new BomRow { Description = "Standard Panel", ExtDescription = "Requires SQ Door Cutout" };
        Assert.True(BomShellEngine.IsCustomSqAssembly(row));
    }

    #endregion

    #region Test Suite 4: Misplaced Coil Panel Count Tracking

    [Fact]
    public void MisplacedCoilPanel_TrackingAndAlertState()
    {
        var vm = new MainViewModel();
        var rows = new List<BomRow>
        {
            new BomRow { PartNumber = "391-0001", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel" },
            new BomRow { PartNumber = "391-0002", Skid = "1 [FR-MB]", Segment = "<--", Description = "Misplaced Coil Panel 1" },
            new BomRow { PartNumber = "391-0003", Skid = "1 [FR-MB]", Segment = " <-- ", Description = "Misplaced Coil Panel 2" },
            new BomRow { PartNumber = "291-0004", Skid = "1 [FR-MB]", Segment = "<--", Description = "Non-391 Arrow Segment (Ignored)" }
        };

        vm.LoadBomRows(rows);

        // 391-0002 and 391-0003 (with spaces) are misplaced coil panels
        Assert.True(vm.HasMisplacedCoilPanels);
        Assert.Equal(2, vm.MisplacedCoilPanelsCount);
        Assert.Equal(2, vm.MisplacedRows.Count);

        // Shell entries should only contain valid segment rows (391-0001)
        Assert.Single(vm.BomEntries);
        Assert.Equal("391-0001", vm.BomEntries[0].PartNumber);
    }

    [Fact]
    public void MisplacedCoilPanels_NotIncludedInCreateShellFolders()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "UPT_MisplacedTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var rows = new List<BomRow>
            {
                new BomRow { PartNumber = "391-0001", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel" },
                new BomRow { PartNumber = "391-0002", Skid = "1 [FR-MB]", Segment = "<--", Description = "Misplaced Coil Panel" }
            };

            var vm = new MainViewModel();
            vm.ShellRootPath = tempRoot;
            vm.LoadBomRows(rows);

            Assert.Single(vm.BomEntries);
            Assert.Single(vm.MisplacedRows);

            vm.CreateShellFolders();

            // Verify only 1 shell folder was created
            string[] createdFolders = Directory.GetDirectories(Path.Combine(tempRoot, "Shell", "Skid 01", "01 MB"));
            Assert.Single(createdFolders);
            Assert.EndsWith("Roof Panel", createdFolders[0]);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    #endregion

    #region Test Suite 5: Exclusion Pattern Over-Exclusion Check

    [Theory]
    [InlineData("DOOR ASSY 24x60", true)]
    [InlineData("DRAIN PAN NIPPLE KIT", true)]
    [InlineData("ASY F GA-SPC GLV", true)]
    [InlineData("ISO PLT SST", true)]
    [InlineData("2 INCH OS LATCH ASSY SS", true)]
    [InlineData("3 INCH IS LATCH ASSY SS", true)]
    [InlineData("TEST COVER PLATE", true)]
    [InlineData("SUMP DRAIN PLUG", true)]
    [InlineData("STAINLESS FLOOR DRAIN", true)]
    public void IsExcludedFromShellMaker_All9ExclusionPatterns(string description, bool expected)
    {
        var row = new BomRow { PartNumber = "391-0001", Description = description };
        Assert.Equal(expected, BomShellEngine.IsExcludedFromShellMaker(row));
    }

    [Fact]
    public void ExclusionPattern_CheckSubstringsLikeIndoorAndOutdoor()
    {
        var indoorRow = new BomRow { PartNumber = "391-0001", Description = "INDOOR CASING PANEL" };
        var outdoorRow = new BomRow { PartNumber = "391-0002", Description = "OUTDOOR CASING PANEL" };

        bool indoorExcluded = BomShellEngine.IsExcludedFromShellMaker(indoorRow);
        bool outdoorExcluded = BomShellEngine.IsExcludedFromShellMaker(outdoorRow);

        Assert.False(indoorExcluded, "'INDOOR CASING PANEL' must NOT be excluded");
        Assert.False(outdoorExcluded, "'OUTDOOR CASING PANEL' must NOT be excluded");
    }

    #endregion
}
