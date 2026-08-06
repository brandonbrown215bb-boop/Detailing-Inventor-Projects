using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class AdversarialM1Tests : IDisposable
{
    private readonly string _tempFolder;

    public AdversarialM1Tests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "UPT_AdversarialTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempFolder))
        {
            try { Directory.Delete(_tempFolder, recursive: true); } catch { }
        }
    }

    #region Category 1: ExcelBomImporter Adversarial Tests

    [Fact]
    public void Import_HeaderInRow3_ParsesDataCorrectly()
    {
        string csvContent = "JCI AIR HANDLER BOM EXPORT\n" +
                           "Project: Test Unit 123\n" +
                           "Part Number,Quantity,Unit,Skid,Segment,Description,Ext. Description\n" +
                           "391-0001,1,EA,1 [FR-MB],MB,Roof Panel,16 GA STL GALV\n";

        string filePath = Path.Combine(_tempFolder, "header_row3.csv");
        File.WriteAllText(filePath, csvContent, Encoding.UTF8);

        var importer = new ExcelBomImporter();
        var result = importer.ImportBom(filePath);

        Assert.Equal(1, result.KeptCount);
        Assert.Equal("391-0001", result.KeptRows[0].PartNumber);
    }

    [Fact]
    public void Import_NoHeaderRow_ParsesFirstRowAsData()
    {
        string csvContent = "391-0001,1,EA,1 [FR-MB],MB,Roof Panel,16 GA STL GALV\n" +
                           "391-0002,2,EA,1 [FR-MB],MB,Side Panel,16 GA STL GALV\n";

        string filePath = Path.Combine(_tempFolder, "no_header.csv");
        File.WriteAllText(filePath, csvContent, Encoding.UTF8);

        var importer = new ExcelBomImporter();
        var result = importer.ImportBom(filePath);

        Assert.Equal(2, result.TotalRowCount);
        Assert.Equal(2, result.KeptCount);
    }

    [Fact]
    public void Import_CsvWithFewerThan7Columns_PadsMissingColumnsWithEmptyStrings()
    {
        string csvContent = "Part Number,Quantity\n" +
                           "391-0001,5\n";

        string filePath = Path.Combine(_tempFolder, "short_cols.csv");
        File.WriteAllText(filePath, csvContent, Encoding.UTF8);

        var importer = new ExcelBomImporter();
        var result = importer.ImportBom(filePath);

        Assert.Equal(1, result.KeptCount);
        var row = result.KeptRows[0];
        Assert.Equal("391-0001", row.PartNumber);
        Assert.Equal("5", row.Quantity);
        Assert.Equal(string.Empty, row.Unit);
        Assert.Equal(string.Empty, row.Skid);
        Assert.Equal(string.Empty, row.Segment);
    }

    [Fact]
    public void Import_WhitespaceOnlyRows_SkippedWithoutCrashing()
    {
        string csvContent = "Part Number,Quantity,Unit,Skid,Segment,Description,Ext. Description\n" +
                           "   ,   ,  ,  ,  ,  ,  \n" +
                           "391-0001,1,EA,1 [FR-MB],MB,Roof Panel,16 GA STL GALV\n" +
                           "\n" +
                           "    \n";

        string filePath = Path.Combine(_tempFolder, "whitespace_rows.csv");
        File.WriteAllText(filePath, csvContent, Encoding.UTF8);

        var importer = new ExcelBomImporter();
        var result = importer.ImportBom(filePath);

        Assert.Equal(1, result.TotalRowCount);
        Assert.Equal("391-0001", result.KeptRows[0].PartNumber);
    }

    [Fact]
    public void Import_PartNumbersWithVariousPrefixCases_HandledCorrectly()
    {
        Assert.True(ExcelBomImporter.ShouldKeepRow("391-abc", "MB"));
        Assert.True(ExcelBomImporter.ShouldKeepRow("391-123", "MB"));
        Assert.True(ExcelBomImporter.ShouldKeepRow("291-xyz", "MB"));
        Assert.True(ExcelBomImporter.ShouldKeepRow("091Z010136-0993", "<--", "ROOF CAP SPLIT COVER"));
        Assert.False(ExcelBomImporter.ShouldKeepRow("091-123", "MB"));
        Assert.False(ExcelBomImporter.ShouldKeepRow("491-123", "MB"));
    }

    #endregion

    #region Category 2: BomShellEngine Exclusion Pattern & SQ Tests

    [Fact]
    public void IsExcludedFromShellMaker_OutdoorAndIndoorPanels_ExclusionAnalysis()
    {
        var outdoorRow = new BomRow { PartNumber = "391-0001", Description = "OUTDOOR CASING PANEL" };
        var indoorRow = new BomRow { PartNumber = "391-0002", Description = "INDOOR CASING PANEL" };
        var actualDoorRow = new BomRow { PartNumber = "391-0003", Description = "ACCESS DOOR ASSEMBLY" };

        bool outdoorExcluded = BomShellEngine.IsExcludedFromShellMaker(outdoorRow);
        bool indoorExcluded = BomShellEngine.IsExcludedFromShellMaker(indoorRow);
        bool actualDoorExcluded = BomShellEngine.IsExcludedFromShellMaker(actualDoorRow);

        Assert.True(actualDoorExcluded, "ACCESS DOOR ASSEMBLY must be excluded");
        Assert.False(outdoorExcluded, "OUTDOOR CASING PANEL must NOT be excluded");
        Assert.False(indoorExcluded, "INDOOR CASING PANEL must NOT be excluded");
    }

    [Fact]
    public void IsCustomSqAssembly_WordBoundaryMatching_ExcludesSquareAndMosquito()
    {
        var sq1 = new BomRow { PartNumber = "391-0001", Description = "SQ CASING PANEL" };
        var sq2 = new BomRow { PartNumber = "391-0002", Description = "PANEL WITH SQ DOOR" };
        var square = new BomRow { PartNumber = "391-0003", Description = "SQUARE DUCT TRANSITION" };
        var mosquito = new BomRow { PartNumber = "391-0004", Description = "MOSQUITO SCREEN" };
        var sequence = new BomRow { PartNumber = "391-0005", Description = "SEQUENCE CONTROLLER BOX" };

        Assert.True(BomShellEngine.IsCustomSqAssembly(sq1));
        Assert.True(BomShellEngine.IsCustomSqAssembly(sq2));
        Assert.False(BomShellEngine.IsCustomSqAssembly(square));
        Assert.False(BomShellEngine.IsCustomSqAssembly(mosquito));
        Assert.False(BomShellEngine.IsCustomSqAssembly(sequence));
    }

    #endregion

    #region Category 3: Skid Parsing & Segment Ordering Edge Cases

    [Fact]
    public void ParseSkidSegmentOrder_5SegmentSkid_ReversesOrderCorrectly()
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
    public void ParseSkidSegmentOrder_10SegmentSkid_PadsSequencePrefixes()
    {
        string skid = "1 [S1-S2-S3-S4-S5-S6-S7-S8-S9-S10]";
        var tokens = BomShellEngine.ParseSkidSegmentOrder(skid);

        Assert.Equal(10, tokens.Count);
        Assert.Equal("01 S10", tokens[0].FolderPrefix);
        Assert.Equal("02 S9", tokens[1].FolderPrefix);
        Assert.Equal("10 S1", tokens[9].FolderPrefix);
    }

    [Fact]
    public void ParseSkidSegmentOrder_EmptyOrMalformedBracketTokens_HandledGracefully()
    {
        Assert.Empty(BomShellEngine.ParseSkidSegmentOrder("1 []"));
        Assert.Empty(BomShellEngine.ParseSkidSegmentOrder("1 [-]"));
        Assert.Empty(BomShellEngine.ParseSkidSegmentOrder("1 [   ]"));
        Assert.Empty(BomShellEngine.ParseSkidSegmentOrder("Skid 01"));
        Assert.Empty(BomShellEngine.ParseSkidSegmentOrder(""));

        var tokensMalformed = BomShellEngine.ParseSkidSegmentOrder("1 [ FR - - MB ]");
        Assert.Equal(2, tokensMalformed.Count);
        Assert.Equal("01 MB", tokensMalformed[0].FolderPrefix);
        Assert.Equal("02 FR", tokensMalformed[1].FolderPrefix);
    }

    [Fact]
    public void ParseSkidNumber_VariousFormats_PadsToTwoDigits()
    {
        Assert.Equal("01", BomShellEngine.ParseSkidNumber("1 [FR-MB]"));
        Assert.Equal("02", BomShellEngine.ParseSkidNumber("2 [MB]"));
        Assert.Equal("12", BomShellEngine.ParseSkidNumber("12 [FR-MB]"));
        Assert.Equal("05", BomShellEngine.ParseSkidNumber("5"));
        Assert.Null(BomShellEngine.ParseSkidNumber("NoDigits"));
        Assert.Null(BomShellEngine.ParseSkidNumber(""));
        Assert.Null(BomShellEngine.ParseSkidNumber(null!));
    }

    [Fact]
    public void ResolveSegmentFolder_CaseInsensitiveAndSpecialSeparators_Matches()
    {
        string skid = "1 [FR-MB]";
        Assert.Equal("01 MB", BomShellEngine.ResolveSegmentFolder(skid, "mb"));
        Assert.Equal("01 MB", BomShellEngine.ResolveSegmentFolder(skid, "MB - Main Box"));
        Assert.Equal("02 FR", BomShellEngine.ResolveSegmentFolder(skid, "FR - Fan Room"));
        Assert.Null(BomShellEngine.ResolveSegmentFolder(skid, "<--"));
        Assert.Null(BomShellEngine.ResolveSegmentFolder(skid, "NONEXISTENT"));
    }

    #endregion

    #region Category 4: Extreme Folder Names & Disambiguation

    [Fact]
    public void SanitizeAssemblyFolderName_ExtremeLength150Chars_TruncatesTo120()
    {
        string longDesc = new string('A', 150);
        string sanitized = BomShellEngine.SanitizeAssemblyFolderName(longDesc);

        Assert.Equal(120, sanitized.Length);
        Assert.Equal(new string('A', 120), sanitized);
    }

    [Fact]
    public void SanitizeAssemblyFolderName_SpecialCharsAndDots_SanitizesCleanly()
    {
        string raw = "Roof / Wall : Panel <V2> * ? \" | \\ / ... ";
        string sanitized = BomShellEngine.SanitizeAssemblyFolderName(raw);

        Assert.DoesNotContain("/", sanitized);
        Assert.DoesNotContain("\\", sanitized);
        Assert.DoesNotContain(":", sanitized);
        Assert.DoesNotContain("*", sanitized);
        Assert.DoesNotContain("?", sanitized);
        Assert.DoesNotContain("\"", sanitized);
        Assert.DoesNotContain("<", sanitized);
        Assert.DoesNotContain(">", sanitized);
        Assert.DoesNotContain("|", sanitized);
        Assert.False(sanitized.EndsWith("."));
        Assert.False(sanitized.EndsWith(" "));
    }

    [Fact]
    public void SanitizeAssemblyFolderName_OnlySpecialChars_FallsBackToAssembly()
    {
        string raw = " * ? < > / \\ : | . . . ";
        string sanitized = BomShellEngine.SanitizeAssemblyFolderName(raw);

        Assert.Equal("Assembly", sanitized);
    }

    [Fact]
    public void BuildPlan_MultiplePartsWithSameSanitizedFolder_DisambiguatesAll()
    {
        var rows = new List<BomRow>
        {
            new BomRow { PartNumber = "391-0001", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel" },
            new BomRow { PartNumber = "391-0002", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel" },
            new BomRow { PartNumber = "391-0003", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel" },
            new BomRow { PartNumber = "391-0004", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel" }
        };

        var engine = new BomShellEngine();
        var plan = engine.BuildPlan(rows);

        Assert.Equal(4, plan.Entries.Count);
        Assert.Equal("Roof Panel", plan.Entries[0].AssemblyFolder);
        Assert.Equal("Roof Panel [391-0002]", plan.Entries[1].AssemblyFolder);
        Assert.Equal("Roof Panel [391-0003]", plan.Entries[2].AssemblyFolder);
        Assert.Equal("Roof Panel [391-0004]", plan.Entries[3].AssemblyFolder);

        // Verify all 4 relative paths are distinct
        var relativePaths = plan.Entries.Select(e => e.RelativePath).Distinct().ToList();
        Assert.Equal(4, relativePaths.Count);
    }

    #endregion

    #region Category 5: Disk Folder Creation & Path Security

    [Fact]
    public void CreateShellFolders_ValidRelativePaths_CreatesDirectoriesOnDisk()
    {
        string rootPath = Path.Combine(_tempFolder, "ShellExport");
        Directory.CreateDirectory(rootPath);

        var paths = new[]
        {
            "Shell/Skid 01/01 MB/Roof Panel 01",
            "Shell/Skid 01/02 FR/Fan Panel 01",
            "Shell/Skid 02/01 CC1/Coil Assembly"
        };

        int count = BomShellEngine.CreateShellFolders(rootPath, paths);
        Assert.Equal(3, count);

        foreach (var p in paths)
        {
            string expectedDiskPath = Path.Combine(rootPath, p.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(Directory.Exists(expectedDiskPath), $"Expected directory to exist: {expectedDiskPath}");
        }
    }

    [Fact]
    public void CreateShellFolders_NonExistentRoot_ThrowsDirectoryNotFoundException()
    {
        string nonExistentRoot = Path.Combine(_tempFolder, "DoesNotExist_" + Guid.NewGuid().ToString("N"));
        var paths = new[] { "Shell/Skid 01/01 MB/Roof Panel" };

        Assert.Throws<DirectoryNotFoundException>(() => BomShellEngine.CreateShellFolders(nonExistentRoot, paths));
    }

    #endregion
}
