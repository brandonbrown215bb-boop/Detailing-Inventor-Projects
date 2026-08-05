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
            Version = 2,
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
        var initialModel = new ProjectStateModel { Version = 2, SourceFolder = "Initial" };
        ProjectSerializer.SaveAtomic(filePath, initialModel);

        var updatedModel = new ProjectStateModel { Version = 2, SourceFolder = "Updated" };
        updatedModel.Surfaces["SURF-2002"] = new SurfaceRecordModel { DisplayNumber = "2002", StateId = "done" };

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
            Version = 2,
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
        Assert.Equal(2, loaded.Version);
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
    public void Load_ValidatesSchemaVersionAndHandlesVersion2()
    {
        string filePath = Path.Combine(_tempDirectory, "v2.uptproj");
        string jsonV2 = "{ \"version\": 2, \"sourceFolder\": \"C:\\\\Test\", \"surfaces\": {} }";
        File.WriteAllText(filePath, jsonV2);

        var loaded = ProjectSerializer.Load<ProjectStateModel>(filePath);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Version);
        Assert.Equal(@"C:\Test", loaded.SourceFolder);
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
                Version = 2,
                SourceFolder = $"Folder_{i}"
            };
            ProjectSerializer.SaveAtomic(filePath, model);
        });

        Assert.True(File.Exists(filePath));
        var loaded = ProjectSerializer.Load<ProjectStateModel>(filePath);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Version);
    }
}
