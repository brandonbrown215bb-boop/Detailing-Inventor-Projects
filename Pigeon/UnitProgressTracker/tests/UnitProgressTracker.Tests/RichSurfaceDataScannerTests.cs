using System.IO;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class RichSurfaceDataScannerTests
{
    [Fact]
    public void ParseConfigJson_ExtractsJobContext_CasingSpecs_Openings_And_Bulkheads()
    {
        string sampleJsonPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "document_config_sample.json");
        Assert.True(File.Exists(sampleJsonPath));

        string jsonText = File.ReadAllText(sampleJsonPath);
        var surface = GeometryScanner.ParseConfigJson(jsonText, "391Z010115-0001.json", "", "json");

        Assert.NotNull(surface);
        Assert.NotNull(surface.JobContext);
        Assert.Equal("6E-110085-03", surface.JobContext.SalesOrderNumber);
        Assert.Equal("Temple Animal Research", surface.JobContext.JobName);
        Assert.Equal("20138", surface.JobContext.ComNumber);
        Assert.Equal("AHU-3-1", surface.JobContext.UnitTag);

        Assert.NotNull(surface.CasingSpec);
        Assert.Equal(2.0, surface.CasingSpec.WallThicknessTop);
        Assert.Equal("18 GA STL GALV", surface.CasingSpec.SkinTop);
        Assert.Equal("20 GA STL GALV", surface.CasingSpec.LinerTop);
        Assert.Equal(16, surface.CasingSpec.FloorMaterialGauge);

        Assert.NotEmpty(surface.Openings);
        Assert.Contains(surface.Openings, o => o.OpeningType == "Door" && o.DoorPartNumber == "391-60231-920");

        Assert.NotEmpty(surface.BulkheadHolePatterns);
        Assert.Contains(surface.BulkheadHolePatterns, b => b.BulkheadPartNumber == "391-60231-913");

        Assert.NotEmpty(surface.BulkheadChannels);
    }
}
