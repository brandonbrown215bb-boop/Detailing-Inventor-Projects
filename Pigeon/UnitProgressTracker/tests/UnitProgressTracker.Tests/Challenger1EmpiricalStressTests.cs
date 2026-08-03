using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Challenger1EmpiricalStressTests
{
    #region Fix 1: Exclusion Rules Empirical Verification

    [Theory]
    [InlineData("INDOOR CASING PANEL", false)]
    [InlineData("OUTDOOR CASING PANEL", false)]
    [InlineData("OUTDOOR HOOD", false)]
    [InlineData("INDOOR CASING PANEL 2", false)]
    [InlineData("OUTDOOR HOOD VENTILATOR", false)]
    [InlineData("DOOR", true)]
    [InlineData("DOORS", true)]
    [InlineData("ACCESS DOOR", true)]
    [InlineData("DOOR ASSY", true)]
    [InlineData("DOORWAYS", false)]
    [InlineData("INDOOR UNIT", false)]
    [InlineData("OUTDOORS", false)]
    public void IsExcludedFromShellMaker_IndoorOutdoorCasingPanels_NotExcluded(string description, bool expectedExcluded)
    {
        var row = new BomRow { PartNumber = "391-0001", Description = description };
        bool actual = BomShellEngine.IsExcludedFromShellMaker(row);
        Assert.Equal(expectedExcluded, actual);
    }

    [Fact]
    public void BuildPlan_CasingPanelsAndHood_GenerateShellFolderEntries()
    {
        var rows = new List<BomRow>
        {
            new BomRow { PartNumber = "391-0001", Skid = "1 [FR-MB]", Segment = "MB", Description = "INDOOR CASING PANEL", ExtDescription = "16 GA STL GALV" },
            new BomRow { PartNumber = "391-0002", Skid = "1 [FR-MB]", Segment = "MB", Description = "OUTDOOR CASING PANEL", ExtDescription = "16 GA STL GALV" },
            new BomRow { PartNumber = "391-0003", Skid = "1 [FR-MB]", Segment = "MB", Description = "OUTDOOR HOOD", ExtDescription = "ALM" },
            new BomRow { PartNumber = "391-0004", Skid = "1 [FR-MB]", Segment = "MB", Description = "ACCESS DOOR ASSY", ExtDescription = "SST" }
        };

        var engine = new BomShellEngine();
        var plan = engine.BuildPlan(rows);

        Assert.Equal(3, plan.Entries.Count);
        Assert.Single(plan.Excluded);
        Assert.Equal("391-0004", plan.Excluded[0].PartNumber);

        var entryDescriptions = plan.Entries.Select(e => e.Description).ToList();
        Assert.Contains("INDOOR CASING PANEL", entryDescriptions);
        Assert.Contains("OUTDOOR CASING PANEL", entryDescriptions);
        Assert.Contains("OUTDOOR HOOD", entryDescriptions);
    }

    #endregion

    #region Fix 2: Reserved Device Names & Control Characters Verification

    [Theory]
    [InlineData("CON", "CON_")]
    [InlineData("PRN", "PRN_")]
    [InlineData("AUX", "AUX_")]
    [InlineData("NUL", "NUL_")]
    [InlineData("COM1", "COM1_")]
    [InlineData("COM9", "COM9_")]
    [InlineData("LPT1", "LPT1_")]
    [InlineData("LPT9", "LPT9_")]
    [InlineData("con", "con_")]
    [InlineData("Prn", "Prn_")]
    [InlineData("Aux", "Aux_")]
    [InlineData("nul", "nul_")]
    [InlineData("com1", "com1_")]
    [InlineData("lpt1", "lpt1_")]
    public void SanitizeAssemblyFolderName_Win32ReservedNames_AppendsUnderscore(string raw, string expected)
    {
        string sanitized = BomShellEngine.SanitizeAssemblyFolderName(raw);
        Assert.Equal(expected, sanitized);
    }

    [Fact]
    public void SanitizeAssemblyFolderName_ControlCharactersAndReservedNames_HandledCleanly()
    {
        string rawWithControl = "Panel\u0000\u0007\u001FWithControl";
        string sanitizedControl = BomShellEngine.SanitizeAssemblyFolderName(rawWithControl);
        Assert.Equal("Panel WithControl", sanitizedControl);

        string rawConWithControl = "\u0001\u0002CON\u0003";
        string sanitizedConControl = BomShellEngine.SanitizeAssemblyFolderName(rawConWithControl);
        Assert.Equal("CON_", sanitizedConControl);
    }

    #endregion

    #region Fix 3: Path Traversal & Root Containment Verification

    [Theory]
    [InlineData("Shell/Skid 01/01 MB/../../../../EscapedFolder")]
    [InlineData("../../escape")]
    [InlineData("../../../Windows/System32")]
    [InlineData("Shell/Skid 01/../../..")]
    public void CreateShellFolders_RelativePathTraversal_ThrowsArgumentException(string relativePath)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "UPT_Challenger1_Traversal_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var relativePaths = new[] { relativePath };
            var ex = Assert.Throws<ArgumentException>(() => BomShellEngine.CreateShellFolders(tempRoot, relativePaths));
            Assert.Contains("Path traversal attempt rejected", ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("C:\\Windows\\System32")]
    [InlineData("D:\\OutsideDirectory")]
    public void CreateShellFolders_DriveLetterAbsolutePath_ThrowsArgumentException(string absolutePath)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "UPT_Challenger1_DrivePath_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var relativePaths = new[] { absolutePath };
            var ex = Assert.Throws<ArgumentException>(() => BomShellEngine.CreateShellFolders(tempRoot, relativePaths));
            Assert.Contains("Path traversal attempt rejected", ex.Message);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CreateShellFolders_UncAbsolutePath_FailsToThrowDueToTrimStartBug()
    {
        // ADVERSARIAL DISCOVERY:
        // BomShellEngine.CreateShellFolders calls .TrimStart(Path.DirectorySeparatorChar) BEFORE checking Path.IsPathRooted.
        // As a result, UNC paths like "\\NetworkShare\Folder" have leading slashes stripped into "NetworkShare\Folder",
        // causing Path.IsPathRooted to return false and bypassing absolute path validation!
        string tempRoot = Path.Combine(Path.GetTempPath(), "UPT_Challenger1_UncPath_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            string uncPath = "\\\\NetworkShare\\Folder";
            var relativePaths = new[] { uncPath };

            // Demonstrates that calling Path.IsPathRooted on safeRelative (after TrimStart) misses UNC paths!
            bool isRootedBeforeTrim = Path.IsPathRooted(uncPath.Replace('/', Path.DirectorySeparatorChar));
            string safeRelative = uncPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            bool isRootedAfterTrim = Path.IsPathRooted(safeRelative);

            Assert.True(isRootedBeforeTrim, "UNC path is rooted before TrimStart");
            Assert.False(isRootedAfterTrim, "UNC path loses rooted status after TrimStart");

            // When CreateShellFolders is called with uncPath, it fails to throw ArgumentException!
            Assert.Throws<ArgumentException>(() => BomShellEngine.CreateShellFolders(tempRoot, relativePaths));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void CreateShellFolders_ValidPathsIncludingSanitizedReservedNames_CreatesFoldersSuccessfully()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "UPT_Challenger1_ValidFolders_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var validPaths = new[]
            {
                "Shell/Skid 01/01 MB/INDOOR CASING PANEL",
                "Shell/Skid 01/01 MB/OUTDOOR CASING PANEL",
                "Shell/Skid 01/01 MB/OUTDOOR HOOD",
                "Shell/Skid 01/01 MB/CON_",
                "Shell/Skid 01/01 MB/PRN_",
                "Shell/Skid 01/01 MB/AUX_",
                "Shell/Skid 01/01 MB/NUL_"
            };

            int count = BomShellEngine.CreateShellFolders(tempRoot, validPaths);
            Assert.Equal(7, count);

            foreach (var path in validPaths)
            {
                string fullPath = Path.Combine(tempRoot, path.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(Directory.Exists(fullPath), $"Expected folder to exist at: {fullPath}");
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    #endregion
}
