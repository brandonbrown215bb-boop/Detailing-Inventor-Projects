using System;
using System.IO;
using System.Text;
using ExcelDataReader;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class ExcelBomImporterTests : IDisposable
{
    private readonly string _tempFolder;

    public ExcelBomImporterTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "UPT_ImporterTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempFolder))
        {
            Directory.Delete(_tempFolder, recursive: true);
        }
    }

    [Fact]
    public void Read11Columns_Keeps7AndDrops4Columns()
    {
        string csvContent = "Part Number,Quantity,Unit,Skid,Segment,Description,Ext. Description,MAPICS Seqc,MAPICS Action,MAPICS Response,Labor Hours\n" +
                           "391-0001,1,EA,1 [FR-MB],MB,Roof Panel,16 GA STL GALV,100,ADD,OK,2.5\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var importer = new ExcelBomImporter();
        var result = importer.ImportBom(stream, "test_bom.csv");

        Assert.Equal(1, result.TotalRowCount);
        Assert.Equal(1, result.KeptCount);
        Assert.Equal(0, result.DroppedCount);

        var row = result.KeptRows[0];
        Assert.Equal("391-0001", row.PartNumber);
        Assert.Equal("1", row.Quantity);
        Assert.Equal("EA", row.Unit);
        Assert.Equal("1 [FR-MB]", row.Skid);
        Assert.Equal("MB", row.Segment);
        Assert.Equal("Roof Panel", row.Description);
        Assert.Equal("16 GA STL GALV", row.ExtDescription);
    }

    [Fact]
    public void Import_ValidCsvFile_ParsesBomRowsCorrectly()
    {
        string csvPath = Path.Combine(_tempFolder, "sample_unit_bom.csv");
        string csvContent = "Part Number,Quantity,Unit,Skid,Segment,Description,Ext. Description,MAPICS Seqc,MAPICS Action,MAPICS Response,Labor Hours\n" +
                           "391-1001,1,EA,1 [FR-MB],MB,Casing Roof Panel,16 GA STL GALV,10,ADD,OK,1.0\n" +
                           "291-2001,1,EA,1 [FR-MB],MB,Cooling Coil,Copper Tubes,20,ADD,OK,3.0\n" +
                           "091-30117-080,50,EA,1 [FR-MB],MB,Subfloor Screw,Zinc,30,ADD,OK,0.1\n" +
                           "024-41723-011,1,EA,1 [FR-MB],MB,VFD DRIVE,N3R,35,ADD,OK,0.0\n" +
                           "491-0001,1,EA,1 [FR-MB],MB,MAPICS Multiplier Factor,,40,ADD,OK,0.0\n";

        File.WriteAllText(csvPath, csvContent, Encoding.UTF8);

        var importer = new ExcelBomImporter();
        var result = importer.ImportBom(csvPath);

        Assert.Equal(5, result.TotalRowCount);
        Assert.Equal(2, result.KeptCount);
        Assert.Equal(3, result.DroppedCount);

        Assert.Contains(result.KeptRows, r => r.PartNumber == "391-1001");
        Assert.Contains(result.KeptRows, r => r.PartNumber == "291-2001");
        Assert.Contains(result.DroppedRows, r => r.PartNumber == "091-30117-080");
        Assert.Contains(result.DroppedRows, r => r.PartNumber == "024-41723-011");
        Assert.Contains(result.DroppedRows, r => r.PartNumber == "491-0001");
    }

    [Theory]
    [InlineData("391-1001", "MB", "ROOF PANEL", true)]
    [InlineData("291-2001", "MB", "COOLING COIL", true)]
    [InlineData("386-3001", "MB", "LABEL KIT", true)]
    [InlineData("486-4001", "MB", "FRAME ASSY", true)]
    [InlineData("251-5001", "MB", "SUBASSEMBLY", true)]
    [InlineData("091Z010136-0993", "<--", "ROOF CAP SPLIT COVER", true)]
    [InlineData("391-60125-617", "HW-1", "DOOR 24 X 72 STD", true)]
    [InlineData("091-30117-080", "MB", "SUBFLOOR SCREW", false)]
    [InlineData("024-41723-011", "HW-1", "VFD DRIVE N3R", false)]
    [InlineData("290-010136-701", "IC", "STEAM COIL", false)]
    [InlineData("025-0001", "MB", "CONDUIT", false)]
    [InlineData("007-0001", "MB", "COPPER TUBE", false)]
    [InlineData("026-0001", "MB", "SCREW", false)]
    [InlineData("028-0001", "MB", "GASKET", false)]
    [InlineData("035-0001", "MB", "HINGE", false)]
    [InlineData("491-0001", "MB", "FACTOR", false)]
    [InlineData("291-2001", "<--", "COOLING COIL", true)]
    [InlineData("391-1001", "<--", "ROOF PANEL", true)]
    public void ContextAwareFilter_FiltersRowsCorrectly(string partNumber, string segment, string description, bool shouldKeep)
    {
        bool result = ExcelBomImporter.ShouldKeepRow(partNumber, segment, description);
        Assert.Equal(shouldKeep, result);
    }

    [Fact]
    public void Import_NonExistentFile_ThrowsFileNotFoundException()
    {
        var importer = new ExcelBomImporter();
        string nonExistentPath = Path.Combine(_tempFolder, "missing_bom_file.xlsx");

        Assert.Throws<FileNotFoundException>(() => importer.ImportBom(nonExistentPath));
    }

    [Fact]
    public void Import_RealJob20170Files_ImportsFlatAndGroupedCorrectly()
    {
        string flatPath = @"C:\Users\jbrow263\ISG\Jobs Checked\20170\BOM_FLAT_6E-330066-03_20260807_0653.xlsx";
        string grpdPath = @"C:\Users\jbrow263\ISG\Jobs Checked\20170\BOM_GRPD_6E-330066-03_20260807_0652.xlsx";

        if (File.Exists(flatPath))
        {
            var importer = new ExcelBomImporter();
            var flatResult = importer.ImportBom(flatPath);

            Assert.True(flatResult.KeptCount > 100);
            Assert.Contains(flatResult.KeptRows, r => r.PartNumber.StartsWith("091Z"));
            Assert.Contains(flatResult.KeptRows, r => r.PartNumber == "391-60233-529");
            Assert.Contains(flatResult.KeptRows, r => r.PartNumber == "391-60234-081");

            Assert.DoesNotContain(flatResult.KeptRows, r => r.PartNumber.StartsWith("024-"));
            Assert.DoesNotContain(flatResult.KeptRows, r => r.PartNumber.StartsWith("290-"));

            var shellEngine = new BomShellEngine();
            var plan = shellEngine.BuildPlan(flatResult.KeptRows);
            Assert.Contains(plan.Entries, e => e.PartNumber == "391-60233-529" && e.SegmentFolder == "06 HW");
        }

        if (File.Exists(grpdPath))
        {
            var importer = new ExcelBomImporter();
            var grpdResult = importer.ImportBom(grpdPath);

            Assert.True(grpdResult.KeptCount > 0, $"Actual KeptCount = {grpdResult.KeptCount}");
            Assert.Contains(grpdResult.KeptRows, r => r.PartNumber == "391-60232-609");
            Assert.Contains(grpdResult.KeptRows, r => r.PartNumber == "391-60232-611");

            var shellEngine = new BomShellEngine();
            var plan = shellEngine.BuildPlan(grpdResult.KeptRows);
            Assert.True(plan.Entries.Count > 0, $"Grouped BOM entries should populate shell plan, but got {plan.Entries.Count}");
        }
    }
}
