using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public class IamScanProgress
{
    public int Scanned { get; init; }
    public int Total { get; init; }
    public string CurrentFile { get; init; } = string.Empty;
    public double Percent => Total > 0 ? (double)Scanned / Total * 100 : 0;
}

public class AsyncGeometryScanner
{
    /// <summary>
    /// Scans a folder for .iam files via active Inventor COM, falling back to
    /// adjacent .json sidecar files when Inventor is not running.
    /// Progress is reported after each file; the scan is fully cancellable.
    /// </summary>
    public static async Task<List<SurfaceModel>> ScanFolderAsync(
        string folderPath,
        IProgress<IamScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return new List<SurfaceModel>();

        // Collect candidate files — prefer .iam, fall back to .json sidecars
        bool inventorRunning = InventorComReader.IsInventorRunning();
        string[] files = inventorRunning
            ? Directory.GetFiles(folderPath, "*.iam", SearchOption.AllDirectories)
            : Directory.GetFiles(folderPath, "*.json", SearchOption.AllDirectories)
                       .Where(f => !f.Contains(".unit-surface-viewer", StringComparison.OrdinalIgnoreCase))
                       .ToArray();

        var result = new List<SurfaceModel>();
        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int total = files.Length;

        for (int i = 0; i < files.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string file = files[i];
            progress?.Report(new IamScanProgress
            {
                Scanned = i,
                Total = total,
                CurrentFile = Path.GetFileName(file)
            });

            SurfaceModel? model = inventorRunning
                ? await Task.Run(() => ScanIamFile(file, folderPath), cancellationToken)
                : await Task.Run(() => ScanJsonFile(file, folderPath), cancellationToken);

            if (model != null && seenNumbers.Add(model.SurfaceNumber))
            {
                result.Add(model);
            }
        }

        progress?.Report(new IamScanProgress { Scanned = total, Total = total, CurrentFile = string.Empty });

        result.Sort((a, b) => string.Compare(a.SurfaceNumber, b.SurfaceNumber, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private static SurfaceModel? ScanIamFile(string iamPath, string rootFolder)
    {
        string? json = InventorComReader.TryReadConfigJsonAttribute(iamPath);
        if (string.IsNullOrWhiteSpace(json)) return null;

        return ParseConfigJson(json, iamPath, rootFolder, "iam");
    }

    private static SurfaceModel? ScanJsonFile(string jsonPath, string rootFolder)
    {
        try
        {
            string text = File.ReadAllText(jsonPath);
            return ParseConfigJson(text, jsonPath, rootFolder, "json");
        }
        catch
        {
            return null;
        }
    }

    private static SurfaceModel? ParseConfigJson(string jsonText, string filePath, string rootFolder, string sourceType)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            if (!doc.RootElement.TryGetProperty("configuration", out var conf)) return null;

            var boxes = GeometryScanner.ExtractBoxesFromConfig(conf);
            if (boxes.Count == 0) return null;

            string surfaceNumber = Path.GetFileNameWithoutExtension(filePath);
            string partNumber = conf.TryGetProperty("partNumber", out var pn) ? pn.GetString() ?? surfaceNumber : surfaceNumber;
            string surfaceType = conf.TryGetProperty("surfaceType", out var st) ? st.GetString() ?? "" : "";
            string side = conf.TryGetProperty("surfaceUnitSide", out var sus) ? sus.GetString() ?? "" : "";

            return new SurfaceModel
            {
                SurfaceNumber = surfaceNumber,
                FilePath = filePath,
                RelativePath = Path.GetRelativePath(rootFolder, filePath),
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
}
