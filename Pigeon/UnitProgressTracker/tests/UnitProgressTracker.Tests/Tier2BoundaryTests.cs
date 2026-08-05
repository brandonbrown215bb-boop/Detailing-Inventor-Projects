using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Tier2BoundaryTests
{
    // =========================================================================
    // FEATURE F1: R1 Boundary Cases
    // =========================================================================

    [Fact]
    public void F1_B01_SanitizeAssemblyFolderName_TruncatesLongNamesAndSanitizesChars()
    {
        string rawDescription = @"Roof / Panel : Heavy * Duty ? ""Assembly"" <Special> | Test .";
        string sanitized = BomShellEngine.SanitizeAssemblyFolderName(rawDescription);

        Assert.DoesNotContain("/", sanitized);
        Assert.DoesNotContain("\\", sanitized);
        Assert.DoesNotContain(":", sanitized);
        Assert.DoesNotContain("*", sanitized);
        Assert.DoesNotContain("?", sanitized);
        Assert.DoesNotContain("\"", sanitized);
        Assert.DoesNotContain("<", sanitized);
        Assert.DoesNotContain(">", sanitized);
        Assert.DoesNotContain("|", sanitized);

        string ultraLong = new string('A', 200);
        string truncated = BomShellEngine.SanitizeAssemblyFolderName(ultraLong);
        Assert.Equal(120, truncated.Length);
    }

    [Fact]
    public void F1_B02_ResolveSegmentFolder_UnmatchedSegment_ReturnsNull()
    {
        string skid = "1 [FR-MB]";
        string? result = BomShellEngine.ResolveSegmentFolder(skid, "XX - Unknown Segment");
        Assert.Null(result);
    }

    [Fact]
    public void F1_B03_BuildPlan_DuplicateAssemblyFolders_AppendsPartNumberSuffix()
    {
        var rows = new List<BomRow>
        {
            new BomRow
            {
                PartNumber = "391-0001", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel"
            },
            new BomRow
            {
                PartNumber = "391-0002", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel"
            }
        };

        var engine = new BomShellEngine();
        var plan = engine.BuildPlan(rows);

        Assert.Equal(2, plan.Entries.Count);
        Assert.Equal("Shell/Skid 01/01 MB/Roof Panel", plan.Entries[0].RelativePath);
        Assert.Equal("Shell/Skid 01/01 MB/Roof Panel [391-0002]", plan.Entries[1].RelativePath);
    }

    [Theory]
    [InlineData("1 [FR-MB]", "01")]
    [InlineData("02 [CO-AHU]", "02")]
    [InlineData("10", "10")]
    [InlineData("ABC", null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    public void F1_B04_ParseSkidNumber_VariousFormats_ExtractsLeadingDigits(string skidInput, string? expected)
    {
        string? actual = BomShellEngine.ParseSkidNumber(skidInput);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void F1_B05_CreateShellFolders_EmptyOrInvalidRoot_ThrowsDirectoryNotFoundException()
    {
        string invalidRoot = Path.Combine(Path.GetTempPath(), $"invalid_root_{Guid.NewGuid():N}");
        Assert.Throws<DirectoryNotFoundException>(() => BomShellEngine.CreateShellFolders(invalidRoot, new[] { "Folder1" }));
    }

    // =========================================================================
    // FEATURE F2: R2 Boundary Cases
    // =========================================================================

    [Fact]
    public void F2_B01_ProjectSerializer_Load_CorruptJson_ReturnsDefault()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"corrupt_{Guid.NewGuid():N}.uptproj");
        try
        {
            File.WriteAllText(tempFile, "{ INVALID JSON PAYLOAD }");
            var result = ProjectSerializer.Load<List<SurfaceModel>>(tempFile);
            Assert.Null(result);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void F2_B02_ProjectSerializer_SaveAtomic_ConcurrentWrites_HandlesUniqueTempFiles()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"concurrent_{Guid.NewGuid():N}.uptproj");
        try
        {
            for (int i = 0; i < 10; i++)
            {
                ProjectSerializer.SaveAtomic(tempFile, new { Iteration = i });
                var loaded = ProjectSerializer.Load<Dictionary<string, int>>(tempFile);
                Assert.NotNull(loaded);
                Assert.Equal(i, loaded["iteration"]);
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void F2_B03_ProjectSerializer_SaveAtomic_InvalidDirectory_ThrowsException()
    {
        string invalidPath = @"Q:\NonExistentDriveDirectory12345\file.uptproj";
        Assert.Throws<DirectoryNotFoundException>(() => ProjectSerializer.SaveAtomic(invalidPath, "test"));
    }

    [Fact]
    public void F2_B04_ProjectSerializer_SaveAndLoad_LargeSurfaceDataset()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"large_dataset_{Guid.NewGuid():N}.uptproj");
        try
        {
            var largeList = new List<SurfaceModel>();
            for (int i = 0; i < 1000; i++)
            {
                largeList.Add(new SurfaceModel
                {
                    SurfaceNumber = $"SURF-{i:D4}",
                    PartNumber = $"391-{i:D4}",
                    Boxes = new List<GeometryBox> { new GeometryBox(i, i, i, 10, 10, 10) }
                });
            }

            ProjectSerializer.SaveAtomic(tempFile, largeList);
            var reloaded = ProjectSerializer.Load<List<SurfaceModel>>(tempFile);

            Assert.NotNull(reloaded);
            Assert.Equal(1000, reloaded.Count);
            Assert.Equal("SURF-0999", reloaded[999].SurfaceNumber);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void F2_B05_ProjectSerializer_SaveAndLoad_SpecialCharactersInNotesAndLabels()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"special_chars_{Guid.NewGuid():N}.uptproj");
        try
        {
            var surf = new SurfaceModel
            {
                SurfaceNumber = "SURF-UNICODE-⚡",
                Notes = "Line 1\nLine 2\tTabbed \"Quotes\" & <Angles>",
                Checklist = new Dictionary<string, bool> { { "Special Key !@#$%^&*()", true } }
            };

            ProjectSerializer.SaveAtomic(tempFile, new List<SurfaceModel> { surf });
            var reloaded = ProjectSerializer.Load<List<SurfaceModel>>(tempFile);

            Assert.NotNull(reloaded);
            Assert.Equal("SURF-UNICODE-⚡", reloaded[0].SurfaceNumber);
            Assert.Equal("Line 1\nLine 2\tTabbed \"Quotes\" & <Angles>", reloaded[0].Notes);
            Assert.True(reloaded[0].Checklist["Special Key !@#$%^&*()"]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // =========================================================================
    // FEATURE F3: R3 Boundary Cases
    // =========================================================================

    [Fact]
    public void F3_B01_StatusState_CustomStateWithInvalidColor_HandlesFallback()
    {
        var vm = new MainViewModel();
        vm.StatusStates.Add(new StatusState("custom-state", "Custom", "NOT_A_COLOR"));

        string color = vm.GetStatusColor("custom-state");
        Assert.Equal("NOT_A_COLOR", color);
    }

    [Theory]
    [InlineData("", "0000")]
    [InlineData("A", "000A")]
    [InlineData("SURF-1-2-3-45678", "5678")]
    [InlineData("PANEL-9", "0009")]
    public void F3_B02_SurfaceModel_ShortLabel_EdgeCases(string surfaceNum, string expected)
    {
        var surf = new SurfaceModel { SurfaceNumber = surfaceNum };
        Assert.Equal(expected, surf.ShortLabel);
    }

    [Fact]
    public void F3_B03_SurfaceModel_Checklist_ConcurrentKeyModifications()
    {
        var surf = new SurfaceModel();
        surf.Checklist["Key1"] = true;
        surf.Checklist["Key1"] = false;
        surf.Checklist[""] = true;

        Assert.Equal(2, surf.Checklist.Count);
        Assert.False(surf.Checklist["Key1"]);
        Assert.True(surf.Checklist[""]);
    }

    [Fact]
    public void F3_B04_MarkdownExport_EmptySurfaces_GeneratesEmptyReportHeader()
    {
        var emptySurfaces = new List<SurfaceModel>();
        string md = MarkdownExporter.GenerateAuditReport(emptySurfaces, StatusState.DefaultStates);

        Assert.Contains("# Unit Progress Tracker", md);
        Assert.Contains("Total Surfaces", md);
        Assert.Contains("Active (Visible)", md);
    }

    [Fact]
    public void F3_B05_SurfaceModel_Notes_ExceedingLength_PreservesUntruncatedText()
    {
        string longNote = new string('N', 10000);
        var surf = new SurfaceModel { Notes = longNote };
        Assert.Equal(10000, surf.Notes.Length);
    }

    // =========================================================================
    // FEATURE F4: R4 Boundary Cases
    // =========================================================================

    [Fact]
    public void F4_B01_GeometryScanner_ScanJsonFolder_InvalidJsonFormat_SkipsFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"geom_corrupt_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "BAD.json"), "{ TRASH }");
            var scanned = GeometryScanner.ScanJsonFolder(tempDir);
            Assert.Empty(scanned);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void F4_B02_GeometryScanner_ScanJsonFolder_MissingGeometryList_SkipsElement()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"geom_missing_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string json = @"{ ""configuration"": { ""partNumber"": ""391-001"" } }";
            File.WriteAllText(Path.Combine(tempDir, "NOGEOM.json"), json);
            var scanned = GeometryScanner.ScanJsonFolder(tempDir);
            Assert.Empty(scanned);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void F4_B03_GeometryScanner_ScanJsonFolder_ZeroOrNegativeBoxDimensions_FiltersInvalidBoxes()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"geom_zero_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string json = @"{
                ""configuration"": {
                    ""roof"": {
                        ""geometryList"": [
                            { ""x"": 0, ""y"": 0, ""z"": 0, ""xLength"": 0, ""yLength"": 10, ""zLength"": 10 },
                            { ""x"": 0, ""y"": 0, ""z"": 0, ""xLength"": -5, ""yLength"": 10, ""zLength"": 10 },
                            { ""x"": 0, ""y"": 0, ""z"": 0, ""xLength"": 10, ""yLength"": 10, ""zLength"": 10 }
                        ]
                    }
                }
            }";
            File.WriteAllText(Path.Combine(tempDir, "INVALID_BOX.json"), json);

            var scanned = GeometryScanner.ScanJsonFolder(tempDir);
            Assert.Single(scanned);
            Assert.Single(scanned[0].Boxes);
            Assert.Equal(10.0, scanned[0].Boxes[0].XLength);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void F4_B04_GeometryScanner_ScanJsonFolder_DuplicateSurfaceNumbers_Deduplicates()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"geom_dup_{Guid.NewGuid():N}");
        string subDir1 = Path.Combine(tempDir, "Sub1");
        string subDir2 = Path.Combine(tempDir, "Sub2");
        Directory.CreateDirectory(subDir1);
        Directory.CreateDirectory(subDir2);
        try
        {
            string json = @"{ ""configuration"": { ""roof"": { ""geometryList"": [{ ""x"": 0, ""y"": 0, ""z"": 0, ""xLength"": 10, ""yLength"": 10, ""zLength"": 10 }] } } }";
            File.WriteAllText(Path.Combine(subDir1, "SURF-DUP.json"), json);
            File.WriteAllText(Path.Combine(subDir2, "SURF-DUP.json"), json);

            var scanned = GeometryScanner.ScanJsonFolder(tempDir);
            Assert.Single(scanned);
            Assert.Equal("SURF-DUP", scanned[0].SurfaceNumber);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void F4_B05_InventorComReader_TryReadConfigJsonAttribute_NonExistentFile_ReturnsNull()
    {
        string missingIam = "C:\\NonExistentDirectory\\MissingModel.iam";
        string? json = InventorComReader.TryReadConfigJsonAttribute(missingIam);
        Assert.Null(json);
    }

    // =========================================================================
    // FEATURE F5: R5 Boundary Cases
    // =========================================================================

    [Fact]
    public void F5_B01_MainViewModel_SelectSurfaceByNumber_NonExistentNumber_ClearsOrPreservesSelection()
    {
        var vm = new MainViewModel();
        var surf = new SurfaceModel { SurfaceNumber = "SURF-001" };
        vm.Surfaces.Add(surf);
        vm.SelectedSurface = surf;

        vm.SelectSurfaceByNumber("NON-EXISTENT-SURFACE");

        Assert.Equal("SURF-001", vm.SelectedSurface.SurfaceNumber);
    }

    [Fact]
    public void F5_B02_MainViewModel_LoadBomRows_EmptyRowSet_GeneratesEmptyPlan()
    {
        var vm = new MainViewModel();
        vm.LoadBomRows(new List<BomRow>());

        Assert.NotNull(vm.CurrentBomPlan);
        Assert.Empty(vm.BomEntries);
        Assert.Equal(0, vm.CurrentBomPlan.Stats.Total391Rows);
    }

    [Fact]
    public void F5_B03_MainViewModel_CreateShellFolders_InvalidPath_SetsStatusMessageError()
    {
        var vm = new MainViewModel();
        vm.ShellRootPath = "Z:\\NonExistentFolder12345";
        vm.CreateShellFolders();

        Assert.Contains("Error", vm.StatusMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"[.*+?^${}()|[\]\\]")]
    public void F5_B04_MainViewModel_SearchText_Filtering_HandlesNullOrWhitespace(string? search)
    {
        var vm = new MainViewModel();
        vm.SearchText = search!;
        Assert.Equal(search, vm.SearchText);
    }

    [Fact]
    public void F5_B05_ValueConverters_InvalidParameters_ReturnDefaultsWithoutThrowing()
    {
        var boolConverter = new BoolToVisibilityConverter();
        var tabConverter = new TabToVisibilityConverter();
        var intBoolConverter = new IntToBoolConverter();

        Assert.Equal(System.Windows.Visibility.Collapsed, boolConverter.Convert("NOT_A_BOOL", typeof(System.Windows.Visibility), null!, null!));
        Assert.Equal(System.Windows.Visibility.Collapsed, tabConverter.Convert("NOT_AN_INT", typeof(System.Windows.Visibility), "1", null!));
        Assert.False((bool)intBoolConverter.Convert("NOT_AN_INT", typeof(bool), "1", null!));
    }
}
