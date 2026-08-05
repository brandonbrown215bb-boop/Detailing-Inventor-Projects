using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public class GeometryScanner
{
    /// <summary>
    /// Asynchronously scans a folder for .iam assembly files via active Inventor COM,
    /// falling back to .json geometry sidecar files when Inventor is not running.
    /// Progress reports and cancellation tokens are fully supported.
    /// </summary>
    public static async Task<List<SurfaceModel>> ScanIamFolderAsync(
        string folderPath,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return new List<SurfaceModel>();

        var jsonFiles = Directory.GetFiles(folderPath, "*.json", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".uptproj", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains(".unit-surface-viewer", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var iamFiles = Directory.GetFiles(folderPath, "*.iam", SearchOption.AllDirectories);

        var allFilePaths = jsonFiles.Concat(iamFiles).Distinct().ToArray();

        var result = new List<SurfaceModel>();
        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int total = allFilePaths.Length;

        for (int i = 0; i < allFilePaths.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string file = allFilePaths[i];
            string fileName = Path.GetFileName(file);

            progress?.Report(new ProgressReport(
                Scanned: i,
                Total: total,
                CurrentFile: fileName,
                StatusMessage: $"Scanning {i + 1} of {total}: {fileName}"
            ));

            SurfaceModel? model = file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? await Task.Run(() => ScanJsonFile(file, folderPath), cancellationToken)
                : await ScanIamFileAsync(file, folderPath, cancellationToken);

            if (model != null && seenNumbers.Add(model.SurfaceNumber))
            {
                result.Add(model);
            }
        }

        progress?.Report(new ProgressReport(
            Scanned: total,
            Total: total,
            CurrentFile: string.Empty,
            StatusMessage: "Scan complete."
        ));

        result.Sort((a, b) => string.Compare(a.SurfaceNumber, b.SurfaceNumber, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    public static Task<SurfaceModel?> ScanIamFileAsync(
        string iamPath,
        string rootFolder = "",
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Check for adjacent .json sidecar for instant lightweight scanning
            string jsonPath = Path.ChangeExtension(iamPath, ".json");
            if (File.Exists(jsonPath))
            {
                var jsonModel = ScanJsonFile(jsonPath, rootFolder);
                if (jsonModel != null) return jsonModel;
            }

            // 2. Fallback to background/invisible COM attribute reading
            string? json = InventorComReader.TryReadConfigJsonAttribute(iamPath);
            if (!string.IsNullOrWhiteSpace(json))
            {
                return ParseConfigJson(json, iamPath, rootFolder, "iam");
            }

            return null;
        }, cancellationToken);
    }

    public static List<SurfaceModel> ScanJsonFolder(string folderPath)
    {
        return ScanIamFolderAsync(folderPath).GetAwaiter().GetResult();
    }

    private static SurfaceModel? ScanJsonFile(string jsonPath, string rootFolder)
    {
        try
        {
            if (!File.Exists(jsonPath)) return null;
            string text = File.ReadAllText(jsonPath);
            return ParseConfigJson(text, jsonPath, rootFolder, "json");
        }
        catch
        {
            return null;
        }
    }

    public static SurfaceModel? ParseConfigJson(string jsonText, string filePath, string rootFolder, string sourceType)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            if (!doc.RootElement.TryGetProperty("configuration", out var conf)) return null;

            var boxes = ExtractBoxesFromConfig(conf);
            if (boxes.Count == 0) return null;

            string surfaceNumber = Path.GetFileNameWithoutExtension(filePath);
            string partNumber = conf.TryGetProperty("partNumber", out var pn) ? pn.GetString() ?? surfaceNumber : surfaceNumber;
            string surfaceType = conf.TryGetProperty("surfaceType", out var st) ? st.GetString() ?? "" : "";
            string side = conf.TryGetProperty("surfaceUnitSide", out var sus) ? sus.GetString() ?? "" : "";

            return new SurfaceModel
            {
                SurfaceNumber = surfaceNumber,
                FilePath = filePath,
                RelativePath = !string.IsNullOrEmpty(rootFolder) ? Path.GetRelativePath(rootFolder, filePath) : filePath,
                SourceType = sourceType,
                PartNumber = partNumber,
                SurfaceType = surfaceType,
                SurfaceUnitSide = side,
                Boxes = boxes
            };
        }
        catch
        {
            return null;
        }
    }

    public static List<GeometryBox> ExtractBoxesFromConfig(JsonElement conf)
    {
        var boxes = new List<GeometryBox>();

        if (conf.TryGetProperty("roof", out var roof) && roof.TryGetProperty("geometryList", out var roofList) && roofList.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in roofList.EnumerateArray())
            {
                var box = ParseGeometryBox(item);
                if (box != null) boxes.Add(box);
            }
        }

        if (conf.TryGetProperty("wall", out var wall) && wall.TryGetProperty("geometryList", out var wallList) && wallList.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in wallList.EnumerateArray())
            {
                var box = ParseGeometryBox(item);
                if (box != null) boxes.Add(box);
            }
        }

        if (conf.TryGetProperty("unitBase", out var ub) && ub.TryGetProperty("unitBaseGeometryList", out var ubList) && ubList.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in ubList.EnumerateArray())
            {
                if (entry.TryGetProperty("geometry", out var geom))
                {
                    var box = ParseGeometryBox(geom);
                    if (box != null) boxes.Add(box);
                }
            }
        }

        return boxes;
    }

    private static GeometryBox? ParseGeometryBox(JsonElement elem)
    {
        double GetDouble(string prop)
        {
            if (elem.TryGetProperty(prop, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number) return val.GetDouble();
                if (val.ValueKind == JsonValueKind.String && double.TryParse(val.GetString(), out double d)) return d;
            }
            return double.NaN;
        }

        double x = GetDouble("x");
        double y = GetDouble("y");
        double z = GetDouble("z");
        double xl = GetDouble("xLength");
        double yl = GetDouble("yLength");
        double zl = GetDouble("zLength");

        if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z) || double.IsNaN(xl) || double.IsNaN(yl) || double.IsNaN(zl))
            return null;

        if (xl <= 0 || yl <= 0 || zl <= 0) return null;

        return new GeometryBox(x, y, z, xl, yl, zl);
    }
}
