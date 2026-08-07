using System;
using System.Collections.Generic;
using System.IO;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class BomShellEngineTests
{
    [Theory]
    [InlineData("391-12345", true)]
    [InlineData(" 391-9999 ", true)]
    [InlineData("091-30117-080", false)]
    [InlineData("48000001", false)]
    public void Is391Part_Identifies391Series(string partNumber, bool expected)
    {
        Assert.Equal(expected, BomShellEngine.Is391Part(partNumber));
    }

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
    [InlineData("CASING PANEL TOP", false)]
    [InlineData("ROOF PANEL 01", false)]
    [InlineData("INDOOR CASING PANEL", false)]
    [InlineData("OUTDOOR CASING PANEL", false)]
    [InlineData("ACCESS DOOR", true)]
    [InlineData("STAINLESS DOORS", true)]
    public void IsExcludedFromShellMaker_Filters9ExclusionPatterns(string description, bool expected)
    {
        var row = new BomRow { PartNumber = "391-0001", Description = description };
        Assert.Equal(expected, BomShellEngine.IsExcludedFromShellMaker(row));
    }

    [Theory]
    [InlineData("Roof / Wall : Panel <V2>", "", "Roof Wall Panel V2")]
    [InlineData("Panel Description.", "", "Panel Description")]
    [InlineData("   Trailing Space   ", "", "Trailing Space")]
    [InlineData("", "", "Assembly")]
    [InlineData("CON", "", "CON_")]
    [InlineData("PRN", "", "PRN_")]
    [InlineData("AUX", "", "AUX_")]
    [InlineData("NUL", "", "NUL_")]
    [InlineData("COM1", "", "COM1_")]
    [InlineData("LPT9", "", "LPT9_")]
    [InlineData("Panel\0With\tControl\nChars", "", "Panel With Control Chars")]
    public void SanitizeAssemblyFolderName_CleansIllegalCharsAndWhitespace(string rawDesc, string rawExt, string expected)
    {
        Assert.Equal(expected, BomShellEngine.SanitizeAssemblyFolderName(rawDesc, rawExt));
    }

    [Fact]
    public void CreateShellFolders_PathTraversalAttempt_ThrowsArgumentException()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "UPT_ShellTest_" + Guid.NewGuid().ToString("N"));
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

    [Theory]
    [InlineData(@"\\NetworkShare\Folder")]
    [InlineData(@"\\127.0.0.1\c$\Secret")]
    [InlineData("//Server/Share/Folder")]
    [InlineData(@"\\?\C:\Windows\System32")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"\System32\Drivers")]
    public void CreateShellFolders_AbsoluteAndUncPaths_ThrowsArgumentException(string absoluteOrUncPath)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "UPT_UncTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var relativePaths = new[] { absoluteOrUncPath };
            var ex = Assert.Throws<ArgumentException>(() => BomShellEngine.CreateShellFolders(tempRoot, relativePaths));
            Assert.Contains("Path traversal attempt rejected", ex.Message);
            Assert.Contains("is an absolute path", ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }


    [Fact]
    public void IsMisplacedCoilPanel_DetectsArrowSegment()
    {
        var misplaced = new BomRow { PartNumber = "391-100", Segment = "<--" };
        var valid = new BomRow { PartNumber = "391-100", Segment = "MB" };
        var non391 = new BomRow { PartNumber = "291-100", Segment = "<--" };

        Assert.True(BomShellEngine.IsMisplacedCoilPanel(misplaced));
        Assert.False(BomShellEngine.IsMisplacedCoilPanel(valid));
        Assert.False(BomShellEngine.IsMisplacedCoilPanel(non391));
    }

    [Fact]
    public void ParseSkidSegmentOrder_ReversesMultiSegmentBracketTokens()
    {
        string skid = "3 [XA2-FF2-RF-XA1-CC1]";
        var tokens = BomShellEngine.ParseSkidSegmentOrder(skid);

        Assert.Equal(5, tokens.Count);
        Assert.Equal("01 CC1", tokens[0].FolderPrefix);
        Assert.Equal("02 XA1", tokens[1].FolderPrefix);
        Assert.Equal("03 RF", tokens[2].FolderPrefix);
        Assert.Equal("04 FF2", tokens[3].FolderPrefix);
        Assert.Equal("05 XA2", tokens[4].FolderPrefix);
    }

    [Fact]
    public void ResolveSegmentFolder_MatchesSegmentCode()
    {
        string skid = "1 [FR-MB]";
        string? result = BomShellEngine.ResolveSegmentFolder(skid, "MB - Main Box");

        Assert.Equal("01 MB", result);
    }

    [Fact]
    public void ResolveSegmentFolder_HW1_Matches_HW_OnSameSkid()
    {
        string skid = "01 - [(RF2-XA4-XA3-HW-RF1-XA2-IP-XA1-FE)]";
        string? result = BomShellEngine.ResolveSegmentFolder(skid, "HW-1 - Heat Wheel");

        Assert.Equal("06 HW", result);
    }

    [Fact]
    public void ResolveSegmentFolder_HW3_DoesNotMatch_HW_OnSameSkid()
    {
        string skid = "01 - [(RF2-XA4-XA3-HW-RF1-XA2-IP-XA1-FE)]";
        string? result = BomShellEngine.ResolveSegmentFolder(skid, "HW-3 - Heat Wheel");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("PANEL SQ TYPE A", true)]
    [InlineData("SQ ACCESS DOOR", true)]
    [InlineData("SQ-FRAME ASSEMBLY", true)]
    [InlineData("SQUARE PANEL", false)]
    [InlineData("STANDARD CASING", false)]
    public void IsCustomSqAssembly_IdentifiesSqTag(string description, bool expected)
    {
        var row = new BomRow { PartNumber = "391-0001", Description = description };
        Assert.Equal(expected, BomShellEngine.IsCustomSqAssembly(row));
    }



    [Fact]
    public void BuildPlan_DisambiguatesDuplicateFolderNamesInSameSegment()
    {
        var rows = new List<BomRow>
        {
            new BomRow { PartNumber = "391-0001", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel" },
            new BomRow { PartNumber = "391-0002", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel" }
        };

        var engine = new BomShellEngine();
        var plan = engine.BuildPlan(rows);

        Assert.Equal(2, plan.Entries.Count);
        Assert.Equal("Roof Panel", plan.Entries[0].AssemblyFolder);
        Assert.Equal("Roof Panel [391-0002]", plan.Entries[1].AssemblyFolder);
    }

    [Fact]
    public void BuildPlan_GeneratesValidShellFolderStructure()
    {
        var rows = new List<BomRow>
        {
            new BomRow
            {
                PartNumber = "391-0001",
                Quantity = "1",
                Unit = "EA",
                Skid = "1 [FR-MB]",
                Segment = "MB",
                Description = "Roof Panel 01",
                ExtDescription = "Standard Galvanized"
            },
            new BomRow
            {
                PartNumber = "391-0002",
                Quantity = "1",
                Unit = "EA",
                Skid = "1 [FR-MB]",
                Segment = "<--",
                Description = "Coil Panel"
            },
            new BomRow
            {
                PartNumber = "391-0003",
                Quantity = "1",
                Unit = "EA",
                Skid = "1 [FR-MB]",
                Segment = "MB",
                Description = "DOOR ASSY 24x60"
            }
        };

        var engine = new BomShellEngine();
        var plan = engine.BuildPlan(rows, "C:\\ExportRoot");

        Assert.Single(plan.Entries);
        Assert.Single(plan.Misplaced);
        Assert.Single(plan.Excluded);

        var entry = plan.Entries[0];
        Assert.Equal("391-0001", entry.PartNumber);
        Assert.Equal("01 MB", entry.SegmentFolder);
        Assert.Equal("Shell/Skid 01/01 MB/Roof Panel 01 Standard Galvanized", entry.RelativePath);
    }

    [Fact]
    public void CreateShellFolders_CreatesPhysicalDirectoryStructureOnDisk()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "UPT_ShellTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var relativePaths = new[]
            {
                "Shell/Skid 01/01 MB/Roof Panel 01",
                "Shell/Skid 01/02 FR/Fan Panel 01"
            };

            int created = BomShellEngine.CreateShellFolders(tempRoot, relativePaths);
            Assert.Equal(2, created);

            Assert.True(Directory.Exists(Path.Combine(tempRoot, "Shell", "Skid 01", "01 MB", "Roof Panel 01")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "Shell", "Skid 01", "02 FR", "Fan Panel 01")));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CreateShellFolders_InvalidRoot_ThrowsDirectoryNotFoundException()
    {
        string invalidPath = Path.Combine(Path.GetTempPath(), "UPT_NonExistentFolder_" + Guid.NewGuid().ToString("N"));
        Assert.Throws<DirectoryNotFoundException>(() => BomShellEngine.CreateShellFolders(invalidPath, new[] { "Shell/Skid 01/01 MB/Panel" }));
    }
}
