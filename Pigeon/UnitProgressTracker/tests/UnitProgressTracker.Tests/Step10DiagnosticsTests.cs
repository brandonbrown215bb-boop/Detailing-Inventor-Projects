using System;
using System.IO;
using System.Threading.Tasks;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.ViewModels;
using Xunit;

namespace UnitProgressTracker.Tests;

public class Step10DiagnosticsTests
{
    [Theory]
    [InlineData("{ not-json", ProjectLoadFailureKind.CorruptJson)]
    [InlineData("{\"format\":\"Pigeon.UnitProgressTracker.Project\",\"version\":2}", ProjectLoadFailureKind.LegacyPigeonVersion)]
    [InlineData("{\"version\":3,\"surfaces\":[]}", ProjectLoadFailureKind.LegacyEsmundVersion)]
    [InlineData("{\"format\":\"Pigeon.UnitProgressTracker.Project\",\"version\":5}", ProjectLoadFailureKind.NewerVersion)]
    [InlineData("{\"format\":\"Pigeon.UnitProgressTracker.Project\",\"version\":4}", ProjectLoadFailureKind.IncompleteProject)]
    [InlineData("{\"format\":\"Pigeon.UnitProgressTracker.Project\",\"version\":4,\"geometry\":{},\"surfaces\":{\"SURF-1\":{\"checklist\":{},\"previousNumbers\":[]}},\"retired\":{},\"statusDefinitions\":[],\"intrusionFlags\":[],\"camera\":{},\"preferences\":{\"checklistTemplate\":[]}}", ProjectLoadFailureKind.MissingRequiredGeometry)]
    public void UPT_C_005_LoadProject_DistinguishesFailureWithoutReturningPartialState(string json, ProjectLoadFailureKind expected)
    {
        string path = Path.Combine(Path.GetTempPath(), $"upt-step10-{Guid.NewGuid():N}.uptproj");
        try
        {
            File.WriteAllText(path, json);

            var result = ProjectSerializer.LoadProject(path);

            Assert.False(result.Success);
            Assert.Equal(expected, result.FailureKind);
            Assert.Null(result.Project);
            Assert.NotEmpty(result.ActionableMessage);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void MainViewModel_FailedProjectLoad_PreservesLastUsableProjectAndReportsSpecificOutcome()
    {
        string path = Path.Combine(Path.GetTempPath(), $"upt-step10-{Guid.NewGuid():N}.uptproj");
        try
        {
            File.WriteAllText(path, "{\"format\":\"Pigeon.UnitProgressTracker.Project\",\"version\":5}");
            var vm = new MainViewModel();
            vm.Surfaces.Add(new SurfaceModel { SurfaceNumber = "KEEP", Boxes = { new GeometryBox(0, 0, 0, 1, 1, 1) } });

            vm.LoadProjectFromFile(path);

            Assert.Single(vm.Surfaces);
            Assert.Equal("KEEP", vm.Surfaces[0].SurfaceNumber);
            Assert.Contains("newer", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("preserved", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task UPT_C_011_ScanDiagnostics_ReportAcceptedDuplicateAndFailedFilesBySafeIdentifier()
    {
        string root = Path.Combine(Path.GetTempPath(), $"upt-step10-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "SURF-1.json"), ValidConfigJson());
            Directory.CreateDirectory(Path.Combine(root, "nested"));
            File.WriteAllText(Path.Combine(root, "nested", "SURF-1.json"), ValidConfigJson());
            File.WriteAllText(Path.Combine(root, "BROKEN.json"), "{broken");

            var result = await GeometryScanner.ScanIamFolderWithDiagnosticsAsync(root);

            Assert.False(result.HasFatalFailure);
            Assert.Equal(3, result.DiscoveredFileCount);
            Assert.Single(result.AcceptedSurfaces);
            Assert.Single(result.SkippedFiles);
            Assert.Single(result.FailedFiles);
            Assert.Equal("SURF-1.json", result.SkippedFiles[0].FileIdentifier);
            Assert.Equal("BROKEN.json", result.FailedFiles[0].FileIdentifier);
            Assert.DoesNotContain(root, result.FailedFiles[0].Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScanDiagnostics_InaccessibleFolder_IsFatalAndActionable()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"upt-missing-{Guid.NewGuid():N}");

        var result = await GeometryScanner.ScanIamFolderWithDiagnosticsAsync(missing);

        Assert.True(result.HasFatalFailure);
        Assert.Equal(ScanFailureKind.InaccessibleFolder, result.FatalFailureKind);
        Assert.Empty(result.AcceptedSurfaces);
        Assert.Contains("folder", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private static string ValidConfigJson() => """
        {
          "configuration": {
            "partNumber": "391-TEST",
            "roof": {
              "geometryList": [
                { "geometry": { "x": 0, "y": 0, "z": 0, "xLength": 10, "yLength": 2, "zLength": 8 } }
              ]
            }
          }
        }
        """;
}
