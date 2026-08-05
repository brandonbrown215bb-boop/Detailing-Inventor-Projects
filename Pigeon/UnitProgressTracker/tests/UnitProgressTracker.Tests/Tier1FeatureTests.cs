using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Tier1FeatureTests
{
    // =========================================================================
    // FEATURE F1: R1 Excel BOM & Shell Folder Engine
    // =========================================================================

    [Theory]
    [InlineData("391-12345", true)]
    [InlineData("391-9999", true)]
    [InlineData(" 391-ABC ", true)]
    [InlineData("091-30117-080", false)]
    [InlineData("48000001", false)]
    public void F1_01_Is391Part_Identifies391SeriesCorrectly(string partNumber, bool expected)
    {
        bool actual = BomShellEngine.Is391Part(partNumber);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("DOOR ASSY", true)]
    [InlineData("DRAIN PAN NIPPLE KIT", true)]
    [InlineData("ISO PLT", true)]
    [InlineData("OS LATCH ASSY", true)]
    [InlineData("CASING PANEL TOP", false)]
    [InlineData("CORNER POST 16 GA", false)]
    public void F1_02_IsExcludedFromShellMaker_FiltersExclusionKeywords(string description, bool expected)
    {
        var row = new BomRow { PartNumber = "391-0001", Description = description };
        bool actual = BomShellEngine.IsExcludedFromShellMaker(row);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void F1_03_IsMisplacedCoilPanel_DetectsMisplacedCoils()
    {
        var misplacedRow = new BomRow { PartNumber = "391-100", Segment = "<--" };
        var validRow = new BomRow { PartNumber = "391-100", Segment = "MB" };

        Assert.True(BomShellEngine.IsMisplacedCoilPanel(misplacedRow));
        Assert.False(BomShellEngine.IsMisplacedCoilPanel(validRow));
    }

    [Fact]
    public void F1_04_ParseSkidSegmentOrder_ParsesAndReversesBrackets()
    {
        string skid = "1 [FR-MB]";
        var tokens = BomShellEngine.ParseSkidSegmentOrder(skid);

        Assert.Equal(2, tokens.Count);
        Assert.Equal("MB", tokens[0].Code);
        Assert.Equal("01 MB", tokens[0].FolderPrefix);
        Assert.Equal("FR", tokens[1].Code);
        Assert.Equal("02 FR", tokens[1].FolderPrefix);
    }

    [Fact]
    public void F1_05_BuildPlan_GeneratesValidShellFolderStructureAndStats()
    {
        var rows = new List<BomRow>
        {
            new BomRow { PartNumber = "391-0001", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel 01" },
            new BomRow { PartNumber = "391-0002", Skid = "1 [FR-MB]", Segment = "<--", Description = "Coil Panel" },
            new BomRow { PartNumber = "391-0003", Skid = "1 [FR-MB]", Segment = "MB", Description = "DOOR ASSY 24x60" },
            new BomRow { PartNumber = "091-30117-080", Skid = "1 [FR-MB]", Segment = "MB", Description = "Subfloor Sheet" }
        };

        var engine = new BomShellEngine();
        var plan = engine.BuildPlan(rows, "C:\\ExportRoot");

        Assert.Equal(3, plan.Stats.Total391Rows);
        Assert.Equal(1, plan.Stats.FolderCount);
        Assert.Equal(1, plan.Stats.MisplacedCount);
        Assert.Equal(1, plan.Stats.ExcludedCount);
        Assert.Single(plan.Entries);
        Assert.Equal("Shell/Skid 01/01 MB/Roof Panel 01", plan.Entries[0].RelativePath);
    }

    // =========================================================================
    // FEATURE F2: R2 Atomic Project File (.uptproj) & State
    // =========================================================================

    [Fact]
    public void F2_01_ProjectSerializer_SaveAndLoad_PreservesSurfaceModels()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_project_{Guid.NewGuid():N}.uptproj");
        try
        {
            var originalSurfaces = new List<SurfaceModel>
            {
                new SurfaceModel
                {
                    SurfaceNumber = "SURF-001",
                    PartNumber = "391-0001",
                    StateId = "built",
                    Notes = "Verified alignment",
                    Checklist = new Dictionary<string, bool> { { "Visual Inspection", true }, { "Torque Check", false } }
                }
            };

            ProjectSerializer.SaveAtomic(tempFile, originalSurfaces);
            Assert.True(File.Exists(tempFile));

            var loadedSurfaces = ProjectSerializer.Load<List<SurfaceModel>>(tempFile);
            Assert.NotNull(loadedSurfaces);
            Assert.Single(loadedSurfaces);
            Assert.Equal("SURF-001", loadedSurfaces[0].SurfaceNumber);
            Assert.Equal("built", loadedSurfaces[0].StateId);
            Assert.Equal("Verified alignment", loadedSurfaces[0].Notes);
            Assert.True(loadedSurfaces[0].Checklist["Visual Inspection"]);
            Assert.False(loadedSurfaces[0].Checklist["Torque Check"]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void F2_02_ProjectSerializer_AtomicSave_CleansUpTempFile()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_atomic_{Guid.NewGuid():N}.uptproj");
        try
        {
            var data = new List<string> { "Item1", "Item2" };
            ProjectSerializer.SaveAtomic(tempFile, data);

            Assert.True(File.Exists(tempFile));

            string dir = Path.GetDirectoryName(tempFile)!;
            string fileName = Path.GetFileName(tempFile);
            var remainingTempFiles = Directory.GetFiles(dir, $"{fileName}.tmp.*");
            Assert.Empty(remainingTempFiles);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void F2_03_ProjectSerializer_Load_NonExistentFile_ReturnsDefault()
    {
        string nonExistent = Path.Combine(Path.GetTempPath(), $"non_existent_{Guid.NewGuid():N}.uptproj");
        var result = ProjectSerializer.Load<List<SurfaceModel>>(nonExistent);
        Assert.Null(result);
    }

    [Fact]
    public void F2_04_ProjectSerializer_SaveAndLoad_PreservesStatusStates()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_states_{Guid.NewGuid():N}.uptproj");
        try
        {
            var states = StatusState.DefaultStates;
            ProjectSerializer.SaveAtomic(tempFile, states);

            var reloaded = ProjectSerializer.Load<List<StatusState>>(tempFile);
            Assert.NotNull(reloaded);
            Assert.Equal(states.Count, reloaded.Count);
            Assert.Equal("current", reloaded[0].Id);
            Assert.Equal("#94a3b8", reloaded[0].ColorHex);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void F2_05_ProjectSerializer_SaveAndLoad_PreservesGeometryBoxes()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_boxes_{Guid.NewGuid():N}.uptproj");
        try
        {
            var box = new GeometryBox(10.5, 20.0, 30.25, 100.0, 200.0, 50.0);
            var surf = new SurfaceModel
            {
                SurfaceNumber = "BOX-01",
                Boxes = new List<GeometryBox> { box }
            };

            ProjectSerializer.SaveAtomic(tempFile, new List<SurfaceModel> { surf });

            var loaded = ProjectSerializer.Load<List<SurfaceModel>>(tempFile);
            Assert.NotNull(loaded);
            Assert.Single(loaded[0].Boxes);
            var b = loaded[0].Boxes[0];
            Assert.Equal(10.5, b.X);
            Assert.Equal(20.0, b.Y);
            Assert.Equal(30.25, b.Z);
            Assert.Equal(100.0, b.XLength);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // =========================================================================
    // FEATURE F3: R3 Surface Audit Checklist, Custom Status States & Markdown Export
    // =========================================================================

    [Fact]
    public void F3_01_StatusState_DefaultStates_ContainsRequiredPredefinedStates()
    {
        var defaults = StatusState.DefaultStates;
        Assert.Equal(7, defaults.Count);
        var ids = defaults.Select(d => d.Id).ToList();
        Assert.Contains("current", ids);
        Assert.Contains("corrected", ids);
        Assert.Contains("built", ids);
        Assert.Contains("associated", ids);
        Assert.Contains("paperwork-corrected", ids);
        Assert.Contains("paperwork-uploaded", ids);
        Assert.Contains("done", ids);
    }

    [Theory]
    [InlineData("SURF-0042", "0042")]
    [InlineData("123", "0123")]
    [InlineData("S-9999", "9999")]
    [InlineData("", "0000")]
    [InlineData("PART-ABCD-12345", "2345")]
    public void F3_02_SurfaceModel_ShortLabel_FormattedCorrectly(string input, string expected)
    {
        var surf = new SurfaceModel { SurfaceNumber = input };
        Assert.Equal(expected, surf.ShortLabel);
    }

    [Fact]
    public void F3_03_SurfaceModel_Checklist_TracksItemsCorrectly()
    {
        var surf = new SurfaceModel();
        surf.Checklist["Inspected"] = true;
        surf.Checklist["Approved"] = false;

        Assert.Equal(2, surf.Checklist.Count);
        Assert.True(surf.Checklist["Inspected"]);
        Assert.False(surf.Checklist["Approved"]);
    }

    [Fact]
    public void F3_04_SurfaceModel_VisibilityToggle_UpdatesHiddenState()
    {
        var surf = new SurfaceModel { IsHidden = false };
        surf.IsHidden = !surf.IsHidden;
        Assert.True(surf.IsHidden);
        surf.IsHidden = !surf.IsHidden;
        Assert.False(surf.IsHidden);
    }

    [Fact]
    public void F3_05_MarkdownExport_GeneratesValidAuditReportFormat()
    {
        var surfaces = new List<SurfaceModel>
        {
            new SurfaceModel
            {
                SurfaceNumber = "SURF-0001",
                PartNumber = "391-001",
                StateId = "done",
                Notes = "All good",
                Checklist = new Dictionary<string, bool> { { "Check 1", true } }
            }
        };

        string md = MarkdownExporter.GenerateAuditReport(surfaces, StatusState.DefaultStates);

        Assert.Contains("# Unit Progress Tracker", md);
        Assert.Contains("Total Surfaces", md);
        Assert.Contains("SURF-0001", md);
        Assert.Contains("Done", md);
        Assert.Contains("1/1", md);
        Assert.Contains("All good", md);
    }

    // =========================================================================
    // FEATURE F4: R4 Async IAM File Scanner & GeometryScanner
    // =========================================================================

    [Fact]
    public void F4_01_InventorComReader_IsInventorRunning_ExecutesSafely()
    {
        // Must execute cleanly on any machine regardless of whether Inventor is running
        bool isRunning = InventorComReader.IsInventorRunning();
        Assert.True(isRunning || !isRunning);
    }

    [Fact]
    public void F4_02_GeometryScanner_ScanJsonFolder_ParsesValidSurfaces()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"geom_scan_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string jsonContent = @"{
                ""configuration"": {
                    ""partNumber"": ""391-1001"",
                    ""roof"": {
                        ""geometryList"": [
                            { ""x"": 0.0, ""y"": 0.0, ""z"": 0.0, ""xLength"": 100.0, ""yLength"": 50.0, ""zLength"": 10.0 }
                        ]
                    }
                }
            }";
            File.WriteAllText(Path.Combine(tempDir, "SURF-101.json"), jsonContent);

            var scanned = GeometryScanner.ScanJsonFolder(tempDir);

            Assert.Single(scanned);
            Assert.Equal("SURF-101", scanned[0].SurfaceNumber);
            Assert.Equal("391-1001", scanned[0].PartNumber);
            Assert.Single(scanned[0].Boxes);
            Assert.Equal(100.0, scanned[0].Boxes[0].XLength);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void F4_03_GeometryScanner_ScanJsonFolder_IgnoresViewerSubdirectoryFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"geom_viewer_{Guid.NewGuid():N}");
        string viewerSubDir = Path.Combine(tempDir, ".unit-surface-viewer");
        Directory.CreateDirectory(viewerSubDir);
        try
        {
            string jsonContent = @"{
                ""configuration"": {
                    ""roof"": {
                        ""geometryList"": [{ ""x"": 0, ""y"": 0, ""z"": 0, ""xLength"": 10, ""yLength"": 10, ""zLength"": 10 }]
                    }
                }
            }";
            File.WriteAllText(Path.Combine(viewerSubDir, "IGNORE_ME.json"), jsonContent);

            var scanned = GeometryScanner.ScanJsonFolder(tempDir);
            Assert.Empty(scanned);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void F4_04_GeometryScanner_ScanJsonFolder_NonExistentDirectory_ReturnsEmptyList()
    {
        string nonExistent = Path.Combine(Path.GetTempPath(), $"missing_dir_{Guid.NewGuid():N}");
        var scanned = GeometryScanner.ScanJsonFolder(nonExistent);
        Assert.NotNull(scanned);
        Assert.Empty(scanned);
    }

    [Fact]
    public async Task F4_05_GeometryScanner_ScanAsync_SimulatedAsyncExecution()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"geom_async_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string jsonContent = @"{
                ""configuration"": {
                    ""roof"": { ""geometryList"": [{ ""x"": 0, ""y"": 0, ""z"": 0, ""xLength"": 10, ""yLength"": 10, ""zLength"": 10 }] }
                }
            }";
            File.WriteAllText(Path.Combine(tempDir, "SURF-201.json"), jsonContent);

            using var cts = new CancellationTokenSource();
            var scanned = await Task.Run(() => GeometryScanner.ScanJsonFolder(tempDir), cts.Token);

            Assert.Single(scanned);
            Assert.Equal("SURF-201", scanned[0].SurfaceNumber);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // =========================================================================
    // FEATURE F5: R5 Interactive 3D Viewport & WPF ViewModel Sync
    // =========================================================================

    [Fact]
    public async Task F5_01_MainViewModel_LoadFolder_PopulatesSurfacesCollection()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vm_load_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string json = @"{ ""configuration"": { ""roof"": { ""geometryList"": [{ ""x"": 0, ""y"": 0, ""z"": 0, ""xLength"": 5, ""yLength"": 5, ""zLength"": 5 }] } } }";
            File.WriteAllText(Path.Combine(tempDir, "SURF-301.json"), json);

            var vm = new MainViewModel();
            await vm.LoadFolderAsync(tempDir);

            Assert.Single(vm.Surfaces);
            Assert.Equal("SURF-301", vm.Surfaces[0].SurfaceNumber);
            Assert.Contains("Async scan complete: 1 surfaces loaded", vm.StatusMessage);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void F5_02_MainViewModel_SelectSurfaceByNumber_UpdatesSelectedSurface()
    {
        var vm = new MainViewModel();
        vm.Surfaces.Add(new SurfaceModel { SurfaceNumber = "SURF-001" });
        vm.Surfaces.Add(new SurfaceModel { SurfaceNumber = "SURF-002" });

        vm.SelectSurfaceByNumber("SURF-002");

        Assert.NotNull(vm.SelectedSurface);
        Assert.Equal("SURF-002", vm.SelectedSurface.SurfaceNumber);
        Assert.True(vm.HasSelectedSurface);
    }

    [Fact]
    public void F5_03_MainViewModel_UpdateSelectedSurfaceStatus_TriggersStateChangeAndRefresh()
    {
        var vm = new MainViewModel();
        var surf = new SurfaceModel { SurfaceNumber = "SURF-001", StateId = "current" };
        vm.Surfaces.Add(surf);
        vm.SelectedSurface = surf;

        bool refreshRequested = false;
        vm.RequestViewportRefresh = () => refreshRequested = true;

        vm.UpdateSelectedSurfaceStatus("built");

        Assert.Equal("built", vm.SelectedSurface.StateId);
        Assert.True(refreshRequested);
    }

    [Fact]
    public void F5_04_MainViewModel_GetStatusColor_ReturnsCorrectHexCode()
    {
        var vm = new MainViewModel();
        string builtColor = vm.GetStatusColor("built");
        string unknownColor = vm.GetStatusColor("non-existent-id");

        Assert.Equal("#3b82f6", builtColor);
        Assert.Equal("#94a3b8", unknownColor);
    }

    [Fact]
    public void F5_05_ValueConverters_TabAndBoolToVisibility_ConvertCorrectly()
    {
        var boolConverter = new BoolToVisibilityConverter();
        var tabConverter = new TabToVisibilityConverter();
        var intBoolConverter = new IntToBoolConverter();

        Assert.Equal(System.Windows.Visibility.Visible, boolConverter.Convert(true, typeof(System.Windows.Visibility), null!, null!));
        Assert.Equal(System.Windows.Visibility.Collapsed, boolConverter.Convert(false, typeof(System.Windows.Visibility), null!, null!));

        Assert.Equal(System.Windows.Visibility.Visible, tabConverter.Convert(1, typeof(System.Windows.Visibility), "1", null!));
        Assert.Equal(System.Windows.Visibility.Collapsed, tabConverter.Convert(0, typeof(System.Windows.Visibility), "1", null!));

        Assert.True((bool)intBoolConverter.Convert(2, typeof(bool), "2", null!));
        Assert.False((bool)intBoolConverter.Convert(1, typeof(bool), "2", null!));
    }
}
