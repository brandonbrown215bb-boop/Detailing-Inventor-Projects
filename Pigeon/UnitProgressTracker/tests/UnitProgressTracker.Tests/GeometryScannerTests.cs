using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class GeometryScannerTests
{
    private static string CreateValidJsonContent(string partNumber, string surfaceType = "Roof", string side = "Top")
    {
        return $$"""
        {
          "configuration": {
            "partNumber": "{{partNumber}}",
            "surfaceType": "{{surfaceType}}",
            "surfaceUnitSide": "{{side}}",
            "roof": {
              "geometryList": [
                {
                  "x": 0.0,
                  "y": 10.0,
                  "z": 20.0,
                  "xLength": 100.0,
                  "yLength": 50.0,
                  "zLength": 2.0
                }
              ]
            }
          }
        }
        """;
    }

    [Fact]
    public async Task ScanIamFolderAsync_NonExistentDirectory_ReturnsEmptyList()
    {
        string fakePath = Path.Combine(Path.GetTempPath(), "NonExistentFolder_" + Guid.NewGuid());
        var result = await GeometryScanner.ScanIamFolderAsync(fakePath);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ScanIamFolderAsync_ValidJsonDirectory_ScansAndReportsProgress()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ScannerTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Surf001.json"), CreateValidJsonContent("391-1001"));
            File.WriteAllText(Path.Combine(tempDir, "Surf002.json"), CreateValidJsonContent("391-1002"));

            var progressReports = new List<ProgressReport>();
            var progress = new Progress<ProgressReport>(p => progressReports.Add(p));

            var results = await GeometryScanner.ScanIamFolderAsync(tempDir, progress);

            Assert.Equal(2, results.Count);
            Assert.Equal("Surf001", results[0].SurfaceNumber);
            Assert.Equal("Surf002", results[1].SurfaceNumber);

            Assert.NotEmpty(progressReports);
            var finalReport = progressReports.Last();
            Assert.Equal(2, finalReport.Scanned);
            Assert.Equal(2, finalReport.Total);
            Assert.Equal(100.0, finalReport.Percent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScanIamFolderAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "CancelTest_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            for (int i = 0; i < 5; i++)
            {
                File.WriteAllText(Path.Combine(tempDir, $"Surf{i:D3}.json"), CreateValidJsonContent($"391-{i:D4}"));
            }

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await GeometryScanner.ScanIamFolderAsync(tempDir, cancellationToken: cts.Token);
            });
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScanIamFolderAsync_DuplicateSurfaces_DeduplicatesBySurfaceNumber()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "DupTest_" + Guid.NewGuid());
        string subDir = Path.Combine(tempDir, "SubFolder");
        Directory.CreateDirectory(subDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Surf001.json"), CreateValidJsonContent("391-1001"));
            File.WriteAllText(Path.Combine(subDir, "Surf001.json"), CreateValidJsonContent("391-1001-Dup"));

            var results = await GeometryScanner.ScanIamFolderAsync(tempDir);

            Assert.Single(results);
            Assert.Equal("Surf001", results[0].SurfaceNumber);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScanIamFolderAsync_ViewerSubdirectory_IgnoresViewerFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ViewerTest_" + Guid.NewGuid());
        string viewerDir = Path.Combine(tempDir, ".unit-surface-viewer");
        Directory.CreateDirectory(viewerDir);

        try
        {
            File.WriteAllText(Path.Combine(tempDir, "Surf001.json"), CreateValidJsonContent("391-1001"));
            File.WriteAllText(Path.Combine(viewerDir, "ViewerConfig.json"), CreateValidJsonContent("391-VIEWER"));

            var results = await GeometryScanner.ScanIamFolderAsync(tempDir);

            Assert.Single(results);
            Assert.Equal("Surf001", results[0].SurfaceNumber);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScanIamFileAsync_ValidFile_ParsesSurfaceModel()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), "TestFile_" + Guid.NewGuid() + ".json");

        try
        {
            File.WriteAllText(tempFile, CreateValidJsonContent("391-9999"));

            var model = await GeometryScanner.ScanIamFileAsync(tempFile, Path.GetDirectoryName(tempFile)!);

            Assert.NotNull(model);
            Assert.Equal("391-9999", model.PartNumber);
            Assert.Single(model.Boxes);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
