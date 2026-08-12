using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using Xunit;

namespace UnitProgressTracker.Tests;

public class ProjectSerializerTests : IDisposable
{
    private readonly string _tempDirectory;

    public ProjectSerializerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "UPT_Serializer_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, true); } catch { }
        }
    }

    [Fact]
    public void SaveAtomic_CreatesFileSuccessfully()
    {
        string filePath = Path.Combine(_tempDirectory, "test-project.uptproj");
        var model = new ProjectStateModel
        {
            Version = ProjectStateModel.CurrentVersion,
            SourceFolder = @"C:\Units\AHU_01",
            UpdatedAt = DateTime.UtcNow
        };
        model.Surfaces["SURF-1001"] = new SurfaceRecordModel
        {
            DisplayNumber = "1001",
            StateId = "built",
            Notes = "Test note"
        };

        ProjectSerializer.SaveAtomic(filePath, model);

        Assert.True(File.Exists(filePath));
        var tempFiles = Directory.GetFiles(_tempDirectory, "*.tmp.*");
        Assert.Empty(tempFiles);
    }

    [Fact]
    public void SaveAtomic_OverwritesExistingFileSafely()
    {
        string filePath = Path.Combine(_tempDirectory, "overwrite-project.uptproj");
        var initialModel = new ProjectStateModel { Version = ProjectStateModel.CurrentVersion, SourceFolder = "Initial" };
        ProjectSerializer.SaveAtomic(filePath, initialModel);

        var updatedModel = new ProjectStateModel { Version = ProjectStateModel.CurrentVersion, SourceFolder = "Updated" };
        updatedModel.Surfaces["SURF-2002"] = new SurfaceRecordModel { DisplayNumber = "2002", StateId = "done" };
        updatedModel.Geometry["SURF-2002"] = new SurfaceModel
        {
            SurfaceNumber = "SURF-2002",
            Boxes = new List<GeometryBox> { new(0, 0, 0, 10, 2, 8) }
        };

        ProjectSerializer.SaveAtomic(filePath, updatedModel);

        var loaded = ProjectSerializer.Load<ProjectStateModel>(filePath);
        Assert.NotNull(loaded);
        Assert.Equal("Updated", loaded.SourceFolder);
        Assert.True(loaded.Surfaces.ContainsKey("SURF-2002"));
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsNullOrDefaultWithoutCrashing()
    {
        string filePath = Path.Combine(_tempDirectory, "corrupt.uptproj");
        File.WriteAllText(filePath, "{ \"version\": 2, \"surfaces\": { corrupt json string... ");

        var result = ProjectSerializer.Load<ProjectStateModel>(filePath);

        Assert.Null(result);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesFullStateGraph()
    {
        string filePath = Path.Combine(_tempDirectory, "roundtrip.uptproj");
        var original = new ProjectStateModel
        {
            Version = ProjectStateModel.CurrentVersion,
            SourceFolder = @"C:\Units\AHU_1001",
            UpdatedAt = DateTime.UtcNow
        };

        var surf = new SurfaceRecordModel
        {
            StateId = "built",
            Notes = "Line 1\nLine 2",
            Hidden = true,
            DisplayNumber = "1001",
            PreviousNumbers = new List<string> { "0999" },
            GeometryFingerprint = "10.000,20.000,30.000,100.000,200.000,50.000",
            Checklist = new Dictionary<string, bool>
            {
                ["Visual Inspection"] = true,
                ["Torque Check"] = false
            }
        };
        original.Surfaces["SURF-1001"] = surf;
        original.Geometry["SURF-1001"] = new SurfaceModel
        {
            SurfaceNumber = "SURF-1001",
            Boxes = new List<GeometryBox> { new(10, 20, 30, 100, 200, 50) }
        };

        var retired = new RetiredSurfaceRecordModel
        {
            RetiredAt = DateTime.UtcNow,
            SupersededBy = "1001",
            TransferType = "renumber",
            FileKey = "SURF-1001",
            GeometryFingerprint = "10.000,20.000,30.000,100.000,200.000,50.000",
            Snapshot = surf.Clone()
        };
        original.Retired["0999"] = retired;

        original.Bom = new BomImportResult
        {
            SourceFilePath = @"C:\BOMs\unit-bom.xlsx",
            ImportedAt = DateTime.UtcNow,
            AllRows = new List<BomRow>
            {
                new BomRow { PartNumber = "391-101", Quantity = "1", Unit = "EA", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel" }
            },
            KeptRows = new List<BomRow>
            {
                new BomRow { PartNumber = "391-101", Quantity = "1", Unit = "EA", Skid = "1 [FR-MB]", Segment = "MB", Description = "Roof Panel" }
            }
        };

        ProjectSerializer.SaveAtomic(filePath, original);
        var loaded = ProjectSerializer.Load<ProjectStateModel>(filePath);

        Assert.NotNull(loaded);
        Assert.Equal(ProjectStateModel.CurrentVersion, loaded.Version);
        Assert.Equal(@"C:\Units\AHU_1001", loaded.SourceFolder);

        Assert.True(loaded.Surfaces.ContainsKey("SURF-1001"));
        var loadedSurf = loaded.Surfaces["SURF-1001"];
        Assert.Equal("built", loadedSurf.StateId);
        Assert.Equal("Line 1\nLine 2", loadedSurf.Notes);
        Assert.True(loadedSurf.Hidden);
        Assert.Equal("1001", loadedSurf.DisplayNumber);
        Assert.Contains("0999", loadedSurf.PreviousNumbers);
        Assert.True(loadedSurf.Checklist["Visual Inspection"]);
        Assert.False(loadedSurf.Checklist["Torque Check"]);

        Assert.True(loaded.Retired.ContainsKey("0999"));
        var loadedRetired = loaded.Retired["0999"];
        Assert.Equal("1001", loadedRetired.SupersededBy);
        Assert.Equal("renumber", loadedRetired.TransferType);
        Assert.NotNull(loadedRetired.Snapshot);
        Assert.Equal("1001", loadedRetired.Snapshot.DisplayNumber);

        Assert.NotNull(loaded.Bom);
        Assert.Equal(1, loaded.Bom.KeptCount);
        Assert.Equal("391-101", loaded.Bom.KeptRows[0].PartNumber);
    }

    [Fact]
    public void Load_RejectsPreProductionVersion2()
    {
        string filePath = Path.Combine(_tempDirectory, "v2.uptproj");
        string jsonV2 = "{ \"version\": 2, \"sourceFolder\": \"C:\\\\Test\", \"surfaces\": {} }";
        File.WriteAllText(filePath, jsonV2);

        var loaded = ProjectSerializer.Load<ProjectStateModel>(filePath);
        Assert.Null(loaded);
    }

    [Fact]
    public void SaveAtomic_InvalidPath_ThrowsException()
    {
        string invalidPath = @"Q:\NonExistentDriveDirectory12345\file.uptproj";
        Assert.Throws<DirectoryNotFoundException>(() => ProjectSerializer.SaveAtomic(invalidPath, "test payload"));
    }

    [Fact]
    public void SaveAtomic_ConcurrentWrites_HandlesUniqueTempFiles()
    {
        string filePath = Path.Combine(_tempDirectory, "concurrent.uptproj");

        Parallel.For(0, 10, i =>
        {
            var model = new ProjectStateModel
            {
                Version = ProjectStateModel.CurrentVersion,
                SourceFolder = $"Folder_{i}"
            };
            ProjectSerializer.SaveAtomic(filePath, model);
        });

        Assert.True(File.Exists(filePath));
        var loaded = ProjectSerializer.Load<ProjectStateModel>(filePath);
        Assert.NotNull(loaded);
        Assert.Equal(ProjectStateModel.CurrentVersion, loaded.Version);
    }

    [Fact]
    public void Load_Version4Fixture_PreservesGeometryTrackingAndProjectState()
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "v4-complete-project.uptproj");

        var loaded = ProjectSerializer.Load<ProjectStateModel>(fixturePath);

        Assert.NotNull(loaded);
        Assert.Equal(ProjectStateModel.FormatId, loaded.Format);
        Assert.Equal(ProjectStateModel.CurrentVersion, loaded.Version);
        Assert.Contains("SURF-1001", loaded.Geometry.Keys);
        Assert.NotEmpty(loaded.Geometry["SURF-1001"].Boxes);
        Assert.Equal("built", loaded.Surfaces["SURF-1001"].StateId);
        Assert.Equal(1, loaded.Bom?.KeptRows.Count);
        Assert.Contains(loaded.StatusDefinitions, state => state.Id == "built");
        Assert.Contains("0988", loaded.Retired["0999"].Snapshot!.PreviousNumbers);
        Assert.False(loaded.IntrusionFlags[0].Resolved);
        Assert.Equal(42, loaded.Camera.PositionX);
        Assert.Contains("Verified dimensions", loaded.Preferences.ChecklistTemplate);
    }

    [Theory]
    [InlineData("v2", "{\"version\":2,\"surfaces\":{}}")]
    [InlineData("v3", "{\"version\":3,\"surfaces\":{}}")]
    [InlineData("newer", "{\"format\":\"Pigeon.UnitProgressTracker.Project\",\"version\":5,\"geometry\":{},\"surfaces\":{},\"retired\":{},\"statusDefinitions\":[],\"intrusionFlags\":[],\"camera\":{},\"preferences\":{}}")]
    [InlineData("incomplete", "{\"format\":\"Pigeon.UnitProgressTracker.Project\",\"version\":4,\"surfaces\":{}}")]
    public void Load_RejectsUnsupportedOrIncompleteProjectShapes(string name, string json)
    {
        string filePath = Path.Combine(_tempDirectory, name + ".uptproj");
        File.WriteAllText(filePath, json);

        Assert.Null(ProjectSerializer.Load<ProjectStateModel>(filePath));
    }

    [Fact]
    public void MainViewModel_RejectingProjectFile_DoesNotReplaceCurrentProject()
    {
        string filePath = Path.Combine(_tempDirectory, "unsupported.uptproj");
        File.WriteAllText(filePath, "{\"version\":2,\"surfaces\":{}}");

        var vm = new UnitProgressTracker.Wpf.ViewModels.MainViewModel();
        vm.Surfaces.Add(new SurfaceModel { SurfaceNumber = "CURRENT" });
        var originalProject = vm.ProjectState;

        vm.LoadProjectFromFile(filePath);

        Assert.Same(originalProject, vm.ProjectState);
        Assert.Single(vm.Surfaces);
        Assert.Equal("CURRENT", vm.Surfaces[0].SurfaceNumber);
        Assert.Contains("unsupported project format", vm.StatusMessage);
    }

    [Fact]
    public void MainViewModel_NullRequiredProjectGraph_DoesNotReplaceCurrentProject()
    {
        string filePath = Path.Combine(_tempDirectory, "null-graph.uptproj");
        File.WriteAllText(filePath,
            "{\"format\":\"Pigeon.UnitProgressTracker.Project\",\"version\":4,\"geometry\":null,\"surfaces\":{},\"retired\":{},\"statusDefinitions\":[],\"intrusionFlags\":[],\"camera\":{},\"preferences\":{\"checklistTemplate\":[]}}");
        var vm = new UnitProgressTracker.Wpf.ViewModels.MainViewModel();
        vm.Surfaces.Add(new SurfaceModel { SurfaceNumber = "CURRENT" });
        var originalProject = vm.ProjectState;

        vm.LoadProjectFromFile(filePath);

        Assert.Same(originalProject, vm.ProjectState);
        Assert.Single(vm.Surfaces);
        Assert.Equal("CURRENT", vm.Surfaces[0].SurfaceNumber);
    }

    [Fact]
    public void MainViewModel_LoadProject_OfflineMode_RestoresGeometryAndSetsOfflineFlag()
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "v4-complete-project.uptproj");
        var vm = new UnitProgressTracker.Wpf.ViewModels.MainViewModel();

        vm.LoadProjectFromFile(fixturePath);

        Assert.True(vm.IsOfflineMode);
        Assert.Contains("Offline Mode", vm.StatusMessage);
        Assert.Contains("Offline Mode", vm.WindowTitle);
        Assert.NotEmpty(vm.Surfaces);
        Assert.Equal("SURF-1001", vm.Surfaces[0].SurfaceNumber);
        Assert.NotEmpty(vm.Surfaces[0].Boxes);
    }

    [Fact]
    public void MainViewModel_LoadProject_RebuildsViewportBeforeRestoringCamera()
    {
        string fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "v4-complete-project.uptproj");
        var calls = new List<string>();
        var vm = new UnitProgressTracker.Wpf.ViewModels.MainViewModel
        {
            RequestViewportRefresh = () => calls.Add("viewport"),
            RequestSetCameraState = _ => calls.Add("camera")
        };

        vm.LoadProjectFromFile(fixturePath);

        Assert.Equal(new[] { "viewport", "camera" }, calls);
    }

    [Fact]
    public void MainViewModel_SaveAndLoad_PreservesCameraState()
    {
        string filePath = Path.Combine(_tempDirectory, "camera_test.uptproj");
        var vm = new UnitProgressTracker.Wpf.ViewModels.MainViewModel();

        var expectedCamera = new CameraStateModel
        {
            PositionX = 10.5,
            PositionY = 20.5,
            PositionZ = 30.5,
            TargetX = 1.0,
            TargetY = 2.0,
            TargetZ = 3.0,
            UpX = 0,
            UpY = 1,
            UpZ = 0
        };

        vm.RequestGetCameraState = () => expectedCamera;

        vm.Surfaces.Add(new SurfaceModel
        {
            SurfaceNumber = "SURF-1",
            Boxes = new List<GeometryBox> { new(0, 0, 0, 10, 2, 8) }
        });
        bool saved = vm.SaveProjectInternal(filePath);
        Assert.True(saved);

        CameraStateModel? restoredCamera = null;
        var vm2 = new UnitProgressTracker.Wpf.ViewModels.MainViewModel();
        vm2.RequestSetCameraState = state => restoredCamera = state;

        vm2.LoadProjectFromFile(filePath);

        Assert.NotNull(restoredCamera);
        Assert.Equal(10.5, restoredCamera.PositionX);
        Assert.Equal(20.5, restoredCamera.PositionY);
        Assert.Equal(30.5, restoredCamera.PositionZ);
    }
}
