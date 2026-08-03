using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Tier3CrossFeatureTests
{
    [Fact]
    public void CrossFeature_1_ExcelBomToProjectState()
    {
        // 1. Build BOM Shell Plan
        var bomRows = new List<BomRow>
        {
            new BomRow
            {
                PartNumber = "391-5001",
                Skid = "1 [FR-MB]",
                Segment = "MB",
                Description = "Wall Panel Left"
            }
        };

        var engine = new BomShellEngine();
        var plan = engine.BuildPlan(bomRows, "C:\\ExportRoot");
        Assert.Single(plan.Entries);

        // 2. Map BOM entry to SurfaceModel
        var entry = plan.Entries[0];
        var surface = new SurfaceModel
        {
            SurfaceNumber = "SURF-5001",
            PartNumber = entry.PartNumber,
            RelativePath = entry.RelativePath,
            StateId = "built",
            Notes = $"Generated from {entry.AssemblyFolder}"
        };

        // 3. Serialize to .uptproj
        string tempFile = Path.Combine(Path.GetTempPath(), $"cross1_{Guid.NewGuid():N}.uptproj");
        try
        {
            ProjectSerializer.SaveAtomic(tempFile, new List<SurfaceModel> { surface });

            // 4. Reload and verify alignment
            var reloaded = ProjectSerializer.Load<List<SurfaceModel>>(tempFile);
            Assert.NotNull(reloaded);
            Assert.Single(reloaded);
            Assert.Equal("391-5001", reloaded[0].PartNumber);
            Assert.Equal("Shell/Skid 01/01 MB/Wall Panel Left", reloaded[0].RelativePath);
            Assert.Equal("built", reloaded[0].StateId);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void CrossFeature_2_ScannedSurfacesToAuditChecklistToMarkdown()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"cross2_scan_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // 1. Create Surface JSON
            string json = @"{
                ""configuration"": {
                    ""partNumber"": ""391-6001"",
                    ""wall"": {
                        ""geometryList"": [{ ""x"": 10, ""y"": 20, ""z"": 30, ""xLength"": 100, ""yLength"": 50, ""zLength"": 5 }]
                    }
                }
            }";
            File.WriteAllText(Path.Combine(tempDir, "SURF-6001.json"), json);

            // 2. Scan folder
            var surfaces = GeometryScanner.ScanJsonFolder(tempDir);
            Assert.Single(surfaces);

            // 3. Update Audit checklist & notes
            var surf = surfaces[0];
            surf.StateId = "paperwork-uploaded";
            surf.Notes = "Surface inspection complete.";
            surf.Checklist["Weld Check"] = true;
            surf.Checklist["Paint Gauge Check"] = true;

            // 4. Export Markdown Audit Report
            string md = MarkdownAuditExporter.GenerateAuditReport(surfaces, StatusState.DefaultStates);

            // 5. Verify Markdown contains scanned data + checklist audit state
            Assert.Contains("SURF-6001", md);
            Assert.Contains("391-6001", md);
            Assert.Contains("Paperwork Uploaded", md);
            Assert.Contains("2/2", md);
            Assert.Contains("Surface inspection complete.", md);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CrossFeature_3_ViewModelStateSyncWithProjectSaveLoad()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"cross3_vm_{Guid.NewGuid():N}.uptproj");
        try
        {
            // 1. Setup Initial ViewModel
            var vm1 = new MainViewModel();
            var surf1 = new SurfaceModel
            {
                SurfaceNumber = "SURF-7001",
                StateId = "current",
                Notes = "Initial state"
            };
            vm1.Surfaces.Add(surf1);

            // 2. Update Surface Status
            vm1.SelectSurfaceByNumber("SURF-7001");
            vm1.UpdateSelectedSurfaceStatus("corrected");

            // 3. Save Project State
            ProjectSerializer.SaveAtomic(tempFile, vm1.Surfaces.ToList());

            // 4. Load into fresh ViewModel
            var reloadedSurfaces = ProjectSerializer.Load<List<SurfaceModel>>(tempFile);
            var vm2 = new MainViewModel();
            foreach (var s in reloadedSurfaces!)
            {
                vm2.Surfaces.Add(s);
            }

            // 5. Verify VM state synchronization
            vm2.SelectSurfaceByNumber("SURF-7001");
            Assert.NotNull(vm2.SelectedSurface);
            Assert.Equal("corrected", vm2.SelectedSurface.StateId);
            Assert.Equal("#f59e0b", vm2.GetStatusColor(vm2.SelectedSurface.StateId));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void CrossFeature_4_AsyncScanToViewportRenderData()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"cross4_viewport_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string json1 = @"{ ""configuration"": { ""partNumber"": ""391-8001"", ""roof"": { ""geometryList"": [{ ""x"": 0, ""y"": 0, ""z"": 0, ""xLength"": 10, ""yLength"": 10, ""zLength"": 10 }] } } }";
            string json2 = @"{ ""configuration"": { ""partNumber"": ""391-8002"", ""wall"": { ""geometryList"": [{ ""x"": 20, ""y"": 0, ""z"": 0, ""xLength"": 15, ""yLength"": 15, ""zLength"": 5 }] } } }";

            File.WriteAllText(Path.Combine(tempDir, "SURF-8001.json"), json1);
            File.WriteAllText(Path.Combine(tempDir, "SURF-8002.json"), json2);

            // 1. Scan folder
            var surfaces = GeometryScanner.ScanJsonFolder(tempDir);
            Assert.Equal(2, surfaces.Count);

            // 2. Set statuses
            surfaces[0].StateId = "built";
            surfaces[1].StateId = "done";

            // 3. Load into ViewModel & check status colors
            var vm = new MainViewModel();
            foreach (var s in surfaces) vm.Surfaces.Add(s);

            string color1 = vm.GetStatusColor(vm.Surfaces[0].StateId);
            string color2 = vm.GetStatusColor(vm.Surfaces[1].StateId);

            Assert.Equal("#3b82f6", color1);
            Assert.Equal("#22c55e", color2);
            Assert.Equal(10.0, vm.Surfaces[0].Boxes[0].XLength);
            Assert.Equal(15.0, vm.Surfaces[1].Boxes[0].XLength);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CrossFeature_5_BomPlanToShellFolderCreationToProjectExport()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"cross5_shell_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            // 1. Generate BOM Plan
            var rows = new List<BomRow>
            {
                new BomRow { PartNumber = "391-9001", Skid = "1 [FR-MB]", Segment = "MB", Description = "Main Frame" },
                new BomRow { PartNumber = "391-9002", Skid = "1 [FR-MB]", Segment = "FR", Description = "Fan Deck" }
            };

            var engine = new BomShellEngine();
            var plan = engine.BuildPlan(rows, tempRoot);

            // 2. Create physical folders on disk
            int createdCount = BomShellEngine.CreateShellFolders(tempRoot, plan.Entries.Select(e => e.RelativePath));
            Assert.Equal(2, createdCount);

            // 3. Verify folders exist physically
            string path1 = Path.Combine(tempRoot, "Shell", "Skid 01", "01 MB", "Main Frame");
            string path2 = Path.Combine(tempRoot, "Shell", "Skid 01", "02 FR", "Fan Deck");

            Assert.True(Directory.Exists(path1));
            Assert.True(Directory.Exists(path2));

            // 4. Save Project File in shell root
            string projectFile = Path.Combine(tempRoot, "project.uptproj");
            ProjectSerializer.SaveAtomic(projectFile, plan);

            Assert.True(File.Exists(projectFile));
            var reloadedPlan = ProjectSerializer.Load<ShellFolderPlan>(projectFile);
            Assert.NotNull(reloadedPlan);
            Assert.Equal(2, reloadedPlan.Entries.Count);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }
}
