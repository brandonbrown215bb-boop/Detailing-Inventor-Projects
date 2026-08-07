using System;
using System.IO;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class UnitConfigParserTests
{
    private const string SampleXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<root:AHU xmlns:root=""http://schemas.airside.be.jci.com/AHU/Configuration"">
  <projectID>6E-630042-10</projectID>
  <segmentList>
    <segment_IP>
      <segmentID>{2C0A813A-A7FB-4C0C-875B-543811FA78FD}</segmentID>
      <segmentType>Intake Plenum</segmentType>
    </segment_IP>
    <segment_MB>
      <segmentID>{3D1B924B-B8FC-5D1D-986C-654922FA89FE}</segmentID>
      <segmentType>Mixing Box</segmentType>
    </segment_MB>
    <segment_FF>
      <segmentID>{4E2C035C-C90D-6E2E-097D-765A33FA90FF}</segmentID>
      <segmentType>Flat Filter</segmentType>
    </segment_FF>
  </segmentList>
  <shippingSkidList>
    <shippingSkid>
      <segmentReference>
        <sequence>1</sequence>
        <segmentID>{2C0A813A-A7FB-4C0C-875B-543811FA78FD}</segmentID>
      </segmentReference>
      <segmentReference>
        <sequence>2</sequence>
        <segmentID>{3D1B924B-B8FC-5D1D-986C-654922FA89FE}</segmentID>
      </segmentReference>
    </shippingSkid>
    <shippingSkid>
      <segmentReference>
        <sequence>1</sequence>
        <segmentID>{4E2C035C-C90D-6E2E-097D-765A33FA90FF}</segmentID>
      </segmentReference>
    </shippingSkid>
  </shippingSkidList>
</root:AHU>";

    [Fact]
    public void ParseUnitConfigXml_ParsesSampleXmlCorrectly()
    {
        var config = UnitConfigParser.ParseUnitConfigXml(SampleXml, "sample.xml");

        Assert.NotNull(config);
        Assert.Equal("6E-630042-10", config.ProjectId);
        Assert.Equal(2, config.Skids.Count);

        // Skid 1: IP, MB
        var skid1 = config.Skids[0];
        Assert.Equal(1, skid1.Id);
        Assert.Equal("IP-MB", skid1.Bracket);
        Assert.Equal(2, skid1.Segments.Count);
        Assert.Equal("01 IP", skid1.Segments[0].FolderPrefix);
        Assert.Equal("02 MB", skid1.Segments[1].FolderPrefix);

        // Skid 2: FF
        var skid2 = config.Skids[1];
        Assert.Equal(2, skid2.Id);
        Assert.Equal("FF", skid2.Bracket);
        Assert.Single(skid2.Segments);
        Assert.Equal("01 FF", skid2.Segments[0].FolderPrefix);
    }

    [Fact]
    public void ResolveSegmentFolderFromConfig_ResolvesConfigFolders()
    {
        var config = UnitConfigParser.ParseUnitConfigXml(SampleXml);

        string? folder1 = UnitConfigParser.ResolveSegmentFolderFromConfig("01", "IP - Intake Plenum", config);
        Assert.Equal("01 IP", folder1);

        string? folder2 = UnitConfigParser.ResolveSegmentFolderFromConfig("01", "MB - Mixing Box", config);
        Assert.Equal("02 MB", folder2);

        string? folder3 = UnitConfigParser.ResolveSegmentFolderFromConfig("02", "FF - Flat Filter", config);
        Assert.Equal("01 FF", folder3);
    }

    [Fact]
    public void BomShellEngine_UsesUnitConfig_WhenProvided()
    {
        var config = UnitConfigParser.ParseUnitConfigXml(SampleXml);

        string? resolved = BomShellEngine.ResolveSegmentFolder("Skid 1 [IP-MB]", "IP - Intake", config);
        Assert.Equal("01 IP", resolved);
    }

    [Fact]
    public void ParseUnitConfigXml_CanParseActual20131TestUnitFile_IfExists()
    {
        string testFilePath = @"C:\Users\jbrow263\ISG\ISG Test Units\20131 PC Shenanigans\UE\6E-630042-10\Config.xml";
        if (File.Exists(testFilePath))
        {
            string content = File.ReadAllText(testFilePath);
            var config = UnitConfigParser.ParseUnitConfigXml(content, testFilePath);

            Assert.NotNull(config);
            Assert.NotEmpty(config.Skids);
            Assert.True(config.Skids.Count > 0);
            foreach (var skid in config.Skids)
            {
                Assert.NotEmpty(skid.Segments);
            }
        }
    }
}
