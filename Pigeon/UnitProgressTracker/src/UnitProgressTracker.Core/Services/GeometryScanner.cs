using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

[SupportedOSPlatform("windows")]
public class GeometryScanner
{
    private static readonly HashSet<string> SkipFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "oldversions", "archive", "archived", "backup", "backups", "temp", "tmp", "_restore", ".unit-surface-viewer"
    };

    public static string[] GetScannableFiles(string rootFolder)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            return Array.Empty<string>();

        void WalkDirectory(string currentDir, int depth)
        {
            if (depth > 12) return;

            string dirName = Path.GetFileName(currentDir);
            if (dirName.StartsWith(".") || SkipFolderNames.Contains(dirName))
                return;

            try
            {
                var files = Directory.GetFiles(currentDir);
                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file);
                    string name = Path.GetFileName(file);

                    if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!file.EndsWith(".uptproj", StringComparison.OrdinalIgnoreCase) &&
                            !file.Contains(".unit-surface-viewer", StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(file);
                        }
                    }
                    else if (ext.Equals(".iam", StringComparison.OrdinalIgnoreCase))
                    {
                        // Filter for surface assemblies (e.g. 391Z*.iam) and ignore 391-*.iam
                        if (name.StartsWith("391Z", StringComparison.OrdinalIgnoreCase) &&
                            !name.StartsWith("391-", StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(file);
                        }
                    }
                }

                var subDirs = Directory.GetDirectories(currentDir);
                foreach (var subDir in subDirs)
                {
                    WalkDirectory(subDir, depth + 1);
                }
            }
            catch
            {
                // Ignore inaccessible directories
            }
        }

        WalkDirectory(rootFolder, 0);
        return results.Distinct().ToArray();
    }

    /// <summary>
    /// Asynchronously scans a folder for surface assembly files (.iam / .json) via headless COM / Apprentice,
    /// without attaching to active Inventor GUI processes. Skipping OldVersions and backup folders.
    /// Progress reports and cancellation tokens are fully supported.
    /// </summary>
    public static async Task<List<SurfaceModel>> ScanIamFolderAsync(
        string folderPath,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return new List<SurfaceModel>();

        var allFilePaths = GetScannableFiles(folderPath);

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
            string configKind = conf.TryGetProperty("configurationKind", out var ck) ? ck.GetString() ?? "" : "";

            int rawSkid = 0;
            if (conf.TryGetProperty("skidId", out var skElem))
            {
                if (skElem.ValueKind == JsonValueKind.Number && skElem.TryGetInt32(out int idVal))
                    rawSkid = idVal;
                else if (skElem.ValueKind == JsonValueKind.String && int.TryParse(skElem.GetString(), out int parsedVal))
                    rawSkid = parsedVal;
            }
            else if (conf.TryGetProperty("skidNumber", out var skNumElem))
            {
                if (skNumElem.ValueKind == JsonValueKind.Number && skNumElem.TryGetInt32(out int numVal))
                    rawSkid = numVal;
                else if (skNumElem.ValueKind == JsonValueKind.String && int.TryParse(skNumElem.GetString(), out int parsedVal2))
                    rawSkid = parsedVal2;
            }

            // Skids start at 0 in config data: 0 or null -> 1, 1 -> 2, 2 -> 3...
            int displaySkidId = rawSkid >= 0 ? rawSkid + 1 : 1;

            return new SurfaceModel
            {
                SurfaceNumber = surfaceNumber,
                FilePath = filePath,
                RelativePath = !string.IsNullOrEmpty(rootFolder) ? Path.GetRelativePath(rootFolder, filePath) : filePath,
                SourceType = sourceType,
                PartNumber = partNumber,
                SurfaceType = surfaceType,
                SurfaceUnitSide = side,
                ConfigurationKind = configKind,
                SkidId = displaySkidId,
                SkidNumber = $"Skid {displaySkidId}",
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

        void ProcessGeometryList(JsonElement parent, string listPropName)
        {
            if (parent.TryGetProperty(listPropName, out var listElem) && listElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in listElem.EnumerateArray())
                {
                    if (item.TryGetProperty("geometry", out var geom))
                    {
                        var box = ParseGeometryBox(geom);
                        if (box != null) boxes.Add(box);
                    }
                    else
                    {
                        var box = ParseGeometryBox(item);
                        if (box != null) boxes.Add(box);
                    }
                }
            }
        }

        string[] parentKeys = { "roof", "wall", "unitBase", "floor", "endPanel", "door" };
        string[] listKeys = { "geometryList", "unitBaseGeometryList", "floorGeometryList" };

        foreach (var parentKey in parentKeys)
        {
            if (conf.TryGetProperty(parentKey, out var parent))
            {
                foreach (var listKey in listKeys)
                {
                    ProcessGeometryList(parent, listKey);
                }
            }
        }

        return boxes;
    }

    private static GeometryBox? ParseGeometryBox(JsonElement elem)
    {
        double GetCoordDouble(string prop)
        {
            if (elem.TryGetProperty(prop, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number) return val.GetDouble();
                if (val.ValueKind == JsonValueKind.String && double.TryParse(val.GetString(), out double d)) return d;
            }
            return 0.0; // Position coordinates default to 0.0 if omitted
        }

        double GetLengthDouble(string prop)
        {
            if (elem.TryGetProperty(prop, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number) return val.GetDouble();
                if (val.ValueKind == JsonValueKind.String && double.TryParse(val.GetString(), out double d)) return d;
            }
            return double.NaN; // Length dimensions are required
        }

        double x = GetCoordDouble("x");
        double y = GetCoordDouble("y");
        double z = GetCoordDouble("z");

        double xl = GetLengthDouble("xLength");
        double yl = GetLengthDouble("yLength");
        double zl = GetLengthDouble("zLength");

        if (double.IsNaN(xl) || double.IsNaN(yl) || double.IsNaN(zl))
            return null;

        if (xl <= 0 || yl <= 0 || zl <= 0) return null;

        return new GeometryBox(x, y, z, xl, yl, zl);
    }
}

