using System;
using System.IO;
using System.Text;
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
                           "491-0001,1,EA,1 [FR-MB],MB,MAPICS Multiplier Factor,,40,ADD,OK,0.0\n";

        File.WriteAllText(csvPath, csvContent, Encoding.UTF8);

        var importer = new ExcelBomImporter();
        var result = importer.ImportBom(csvPath);

        Assert.Equal(4, result.TotalRowCount);
        Assert.Equal(2, result.KeptCount);
        Assert.Equal(2, result.DroppedCount);

        Assert.Contains(result.KeptRows, r => r.PartNumber == "391-1001");
        Assert.Contains(result.KeptRows, r => r.PartNumber == "291-2001");
        Assert.Contains(result.DroppedRows, r => r.PartNumber == "091-30117-080");
        Assert.Contains(result.DroppedRows, r => r.PartNumber == "491-0001");
    }

    [Theory]
    [InlineData("391-1001", "MB", true)]
    [InlineData("291-2001", "MB", true)]
    [InlineData("386-3001", "MB", true)]
    [InlineData("486-4001", "MB", true)]
    [InlineData("251-5001", "MB", true)]
    [InlineData("5E0302690501000", "<--", true)]
    [InlineData("091-30117-080", "MB", false)]
    [InlineData("025-0001", "MB", false)]
    [InlineData("007-0001", "MB", false)]
    [InlineData("026-0001", "MB", false)]
    [InlineData("028-0001", "MB", false)]
    [InlineData("035-0001", "MB", false)]
    [InlineData("491-0001", "MB", false)]
    [InlineData("291-2001", "<--", false)]
    [InlineData("391-1001", "<--", true)]
    public void PrefixTierFilter_FiltersRowsCorrectly(string partNumber, string segment, bool shouldKeep)
    {
        bool result = ExcelBomImporter.ShouldKeepRow(partNumber, segment);
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
    public void Import_EmptyFile_ReturnsEmptyResult()
    {
        using var emptyStream = new MemoryStream(Array.Empty<byte>());
        var importer = new ExcelBomImporter();
        var result = importer.ImportBom(emptyStream, "empty.csv");

        Assert.Equal(0, result.TotalRowCount);
        Assert.Equal(0, result.KeptCount);
        Assert.Equal(0, result.DroppedCount);
    }
}
