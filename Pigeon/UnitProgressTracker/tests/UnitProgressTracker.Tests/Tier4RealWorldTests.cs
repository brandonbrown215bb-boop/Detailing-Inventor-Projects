using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Tier4RealWorldTests
{
    [Fact]
    public void E2E_Scenario_1_CompleteAHUProjectLifecycle()
    {
        string workDir = Path.Combine(Path.GetTempPath(), $"e2e_ahu_lifecycle_{Guid.NewGuid():N}");
        string shellRoot = Path.Combine(workDir, "ShellExport");
        string jsonDir = Path.Combine(workDir, "Surfaces");
        string projectPath = Path.Combine(workDir, "unit_project.uptproj");

        Directory.CreateDirectory(shellRoot);
        Directory.CreateDirectory(jsonDir);

        try
        {
            // 1. Raw BOM Data (Combination of 391 parts, exclusions, misplaced coils, SQ assemblies, and non-391 items)
            var bomRows = new List<BomRow>
            {
                new BomRow { PartNumber = "391-1001", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel Top" },
                new BomRow { PartNumber = "391-1002", Skid = "1 [FR-MB]", Segment = "MB", Description = "SQ ACCESS HATCH 24x60" },
                new BomRow { PartNumber = "391-1003", Skid = "1 [FR-MB]", Segment = "FR", Description = "Fan Deck Channel" },
                new BomRow { PartNumber = "391-1004", Skid = "1 [FR-MB]", Segment = "<--", Description = "Misplaced Coil Panel" },
                new BomRow { PartNumber = "391-1005", Skid = "1 [FR-MB]", Segment = "MB", Description = "DOOR ASSY 30x72" },
                new BomRow { PartNumber = "091-30117-080", Skid = "1 [FR-MB]", Segment = "MB", Description = "Subfloor Sheet Metal" }
            };

            // 2. Build Plan & Create Shell Directories
            var engine = new BomShellEngine();
            var plan = engine.BuildPlan(bomRows, shellRoot);

            Assert.Equal(5, plan.Stats.Total391Rows);
            Assert.Equal(3, plan.Stats.FolderCount);
            Assert.Equal(1, plan.Stats.MisplacedCount);
            Assert.Equal(1, plan.Stats.ExcludedCount);
            Assert.Equal(1, plan.Stats.CustomSqCount);

            int created = BomShellEngine.CreateShellFolders(shellRoot, plan.Entries.Select(e => e.RelativePath));
            Assert.Equal(3, created);

            // 3. Generate Scanned Surface Geometry Files
            string surfaceJson1 = @"{ ""configuration"": { ""partNumber"": ""391-1001"", ""roof"": { ""geometryList"": [{ ""x"": 0, ""y"": 0, ""z"": 0, ""xLength"": 120, ""yLength"": 60, ""zLength"": 2 }] } } }";
            string surfaceJson2 = @"{ ""configuration"": { ""partNumber"": ""391-1002"", ""wall"": { ""geometryList"": [{ ""x"": 0, ""y"": 60, ""z"": 0, ""xLength"": 24, ""yLength"": 60, ""zLength"": 2 }] } } }";

            File.WriteAllText(Path.Combine(jsonDir, "SURF-0001.json"), surfaceJson1);
            File.WriteAllText(Path.Combine(jsonDir, "SURF-0002.json"), surfaceJson2);

            var scannedSurfaces = GeometryScanner.ScanJsonFolder(jsonDir);
            Assert.Equal(2, scannedSurfaces.Count);

            // 4. Perform Audit Workflow (Checklists, Notes, Status Updates)
            scannedSurfaces[0].StateId = "built";
            scannedSurfaces[0].Notes = "Roof panel installed and sealed.";
            scannedSurfaces[0].Checklist["Torque Checked"] = true;
            scannedSurfaces[0].Checklist["Visual Quality Pass"] = true;

            scannedSurfaces[1].StateId = "paperwork-uploaded";
            scannedSurfaces[1].Notes = "SQ Door mounted.";
            scannedSurfaces[1].Checklist["Latch Test"] = true;

            // 5. Atomic Save Project
            ProjectSerializer.SaveAtomic(projectPath, scannedSurfaces);
            Assert.True(File.Exists(projectPath));

            // 6. Reload Project & Export Markdown Audit Report
            var reloadedSurfaces = ProjectSerializer.Load<List<SurfaceModel>>(projectPath);
            Assert.NotNull(reloadedSurfaces);

            var vm = new MainViewModel();
            foreach (var s in reloadedSurfaces) vm.Surfaces.Add(s);

            string reportMd = MarkdownAuditExporter.GenerateAuditReport(vm.Surfaces, vm.StatusStates);

            Assert.Contains("Total Surfaces: 2", reportMd);
            Assert.Contains("SURF-0001", reportMd);
            Assert.Contains("Roof panel installed and sealed.", reportMd);
            Assert.Contains("Built", reportMd);
            Assert.Contains("Paperwork Uploaded", reportMd);
        }
        finally
        {
            if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
        }
    }

    [Fact]
    public void E2E_Scenario_2_MultiSkidSegmentDeduplicationAndFolderNaming()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"e2e_multiskid_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            var bomRows = new List<BomRow>
            {
                // Skid 1: [FR-MB] -> Reverse token order: 01 MB, 02 FR
                new BomRow { PartNumber = "391-2001", Skid = "1 [FR-MB]", Segment = "MB", Description = "Side Wall Panel" },
                new BomRow { PartNumber = "391-2002", Skid = "1 [FR-MB]", Segment = "MB", Description = "Side Wall Panel" }, // Duplicate folder name in same segment!
                new BomRow { PartNumber = "391-2003", Skid = "1 [FR-MB]", Segment = "FR", Description = "Fan Inlet Cone" },
                // Skid 2: [CO-AHU] -> Reverse token order: 01 AHU, 02 CO
                new BomRow { PartNumber = "391-3001", Skid = "2 [CO-AHU]", Segment = "AHU", Description = "Filter Rack Assembly" },
                new BomRow { PartNumber = "391-3002", Skid = "2 [CO-AHU]", Segment = "CO", Description = "Coil Header Cover" }
            };

            var engine = new BomShellEngine();
            var plan = engine.BuildPlan(bomRows, tempRoot);

            Assert.Equal(5, plan.Entries.Count);

            // Check relative path ordering & deduplication suffixes
            var entries = plan.Entries;
            Assert.Equal("Shell/Skid 01/01 MB/Side Wall Panel", entries[0].RelativePath);
            Assert.Equal("Shell/Skid 01/01 MB/Side Wall Panel [391-2002]", entries[1].RelativePath);
            Assert.Equal("Shell/Skid 01/02 FR/Fan Inlet Cone", entries[2].RelativePath);
            Assert.Equal("Shell/Skid 02/01 AHU/Filter Rack Assembly", entries[3].RelativePath);
            Assert.Equal("Shell/Skid 02/02 CO/Coil Header Cover", entries[4].RelativePath);

            // Create physical directories
            int created = BomShellEngine.CreateShellFolders(tempRoot, plan.Entries.Select(e => e.RelativePath));
            Assert.Equal(5, created);

            Assert.True(Directory.Exists(Path.Combine(tempRoot, "Shell", "Skid 01", "01 MB", "Side Wall Panel")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "Shell", "Skid 01", "01 MB", "Side Wall Panel [391-2002]")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "Shell", "Skid 02", "01 AHU", "Filter Rack Assembly")));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void E2E_Scenario_3_ProjectPersistenceAndCrashRecovery()
    {
        string projectPath = Path.Combine(Path.GetTempPath(), $"crash_recovery_{Guid.NewGuid():N}.uptproj");
        string tempDir = Path.GetDirectoryName(projectPath)!;

        try
        {
            var surfaces = new List<SurfaceModel>();
            for (int i = 1; i <= 50; i++)
            {
                surfaces.Add(new SurfaceModel
                {
                    SurfaceNumber = $"SURF-{i:D4}",
                    PartNumber = $"391-{i:D4}",
                    StateId = "current",
                    Notes = $"State version {i}"
                });
            }

            // Rapid atomic updates simulating continuous project auto-saves
            for (int step = 1; step <= 20; step++)
            {
                foreach (var s in surfaces)
                {
                    s.StateId = step % 2 == 0 ? "built" : "corrected";
                    s.Notes = $"Update pass {step}";
                }

                ProjectSerializer.SaveAtomic(projectPath, surfaces);

                // Verify target file exists and no leftover .tmp files exist
                Assert.True(File.Exists(projectPath));
                var leftoverTemps = Directory.GetFiles(tempDir, $"{Path.GetFileName(projectPath)}.tmp.*");
                Assert.Empty(leftoverTemps);
            }

            // Final state verification after crash/reload simulation
            var reloaded = ProjectSerializer.Load<List<SurfaceModel>>(projectPath);
            Assert.NotNull(reloaded);
            Assert.Equal(50, reloaded.Count);
            Assert.Equal("built", reloaded[0].StateId);
            Assert.Equal("Update pass 20", reloaded[0].Notes);
        }
        finally
        {
            if (File.Exists(projectPath)) File.Delete(projectPath);
        }
    }

    [Fact]
    public void E2E_Scenario_4_SurfaceAuditFilteringAndVisibilityWorkflow()
    {
        var surfaces = new List<SurfaceModel>();

        // Create 70 surfaces with distributed status states
        for (int i = 1; i <= 70; i++)
        {
            string state = (i % 7) switch
            {
                0 => "current",
                1 => "corrected",
                2 => "built",
                3 => "associated",
                4 => "paperwork-corrected",
                5 => "paperwork-uploaded",
                _ => "done"
            };

            surfaces.Add(new SurfaceModel
            {
                SurfaceNumber = $"SURF-{i:D4}",
                PartNumber = $"391-{(i % 10):D4}",
                StateId = state,
                IsHidden = i > 60, // Hide last 10
                Checklist = new Dictionary<string, bool> { { "QACheck", i % 2 == 0 } }
            });
        }

        var vm = new MainViewModel();
        foreach (var s in surfaces) vm.Surfaces.Add(s);

        // Verify loaded collection statistics
        Assert.Equal(70, vm.Surfaces.Count);
        Assert.Equal(10, vm.Surfaces.Count(s => s.IsHidden));
        Assert.Equal(60, vm.Surfaces.Count(s => !s.IsHidden));

        // Generate Markdown audit report
        string md = MarkdownAuditExporter.GenerateAuditReport(vm.Surfaces, vm.StatusStates);

        Assert.Contains("Total Surfaces: 70", md);
        Assert.Contains("Active (Visible): 60", md);
        Assert.Contains("Hidden: 10", md);
        Assert.Contains("Done | 10 | 14.3%", md);
    }

    [Fact]
    public void E2E_Scenario_5_CustomSqAssemblyAndMisplacedCoilSegregation()
    {
        var bomRows = new List<BomRow>
        {
            // Misplaced coil lines with "<--" segment
            new BomRow { PartNumber = "391-901", Skid = "1 [FR-MB]", Segment = "<--", Description = "Coil Section Left Panel" },
            new BomRow { PartNumber = "391-902", Skid = "1 [FR-MB]", Segment = "<--", Description = "Coil Header Access" },

            // Custom SQ door assemblies (containing SQ in description or starting with SQ)
            new BomRow { PartNumber = "391-903", Skid = "1 [FR-MB]", Segment = "MB", Description = "SQ ACCESS HATCH 24x60" },
            new BomRow { PartNumber = "391-904", Skid = "1 [FR-MB]", Segment = "MB", Description = "SQ HATCH 18x18" },

            // Excluded items (doors, drain pans, test covers)
            new BomRow { PartNumber = "391-905", Skid = "1 [FR-MB]", Segment = "MB", Description = "DRAIN PAN NIPPLE KIT" },
            new BomRow { PartNumber = "391-906", Skid = "1 [FR-MB]", Segment = "MB", Description = "DOOR ASSY 30x72" },

            // Standard valid 391 casing parts
            new BomRow { PartNumber = "391-907", Skid = "1 [FR-MB]", Segment = "MB", Description = "Corner Post Left" },
            new BomRow { PartNumber = "391-908", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel Top" }
        };

        var engine = new BomShellEngine();
        var plan = engine.BuildPlan(bomRows);

        // Validate plan breakdown & statistics
        Assert.Equal(8, plan.Stats.Total391Rows);
        Assert.Equal(4, plan.Stats.FolderCount);
        Assert.Equal(2, plan.Stats.MisplacedCount);
        Assert.Equal(2, plan.Stats.ExcludedCount);
        Assert.Equal(2, plan.Stats.CustomSqCount);

        Assert.All(plan.Misplaced, r => Assert.Equal("<--", r.Segment));
        Assert.All(plan.Excluded, r => Assert.True(BomShellEngine.IsExcludedFromShellMaker(r)));

        var sqEntries = plan.Entries.Where(e => e.IsCustomSq).ToList();
        Assert.Equal(2, sqEntries.Count);
        Assert.Contains(sqEntries, e => e.PartNumber == "391-903");
        Assert.Contains(sqEntries, e => e.PartNumber == "391-904");
    }
}
