using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public class GeometryScanner
{
    public static List<SurfaceModel> ScanJsonFolder(string folderPath)
    {
        var result = new List<SurfaceModel>();
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return result;

        var files = Directory.GetFiles(folderPath, "*.json", SearchOption.AllDirectories);
        var seenNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (file.Contains(".unit-surface-viewer", StringComparison.OrdinalIgnoreCase)) continue;

            string surfaceNumber = Path.GetFileNameWithoutExtension(file);
            if (seenNumbers.Contains(surfaceNumber)) continue;

            try
            {
                string jsonText = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(jsonText);

                if (!doc.RootElement.TryGetProperty("configuration", out var conf)) continue;

                var boxes = ExtractBoxesFromConfig(conf);
                if (boxes.Count == 0) continue;

                seenNumbers.Add(surfaceNumber);

                string partNumber = conf.TryGetProperty("partNumber", out var pn) ? pn.GetString() ?? surfaceNumber : surfaceNumber;
                string surfaceType = conf.TryGetProperty("surfaceType", out var st) ? st.GetString() ?? "" : "";
                string side = conf.TryGetProperty("surfaceUnitSide", out var sus) ? sus.GetString() ?? "" : "";

                result.Add(new SurfaceModel
                {
                    SurfaceNumber = surfaceNumber,
                    FilePath = file,
                    RelativePath = Path.GetRelativePath(folderPath, file),
                    SourceType = "json",
                    PartNumber = partNumber,
                    SurfaceType = surfaceType,
                    SurfaceUnitSide = side,
                    Boxes = boxes
                });
            }
            catch
            {
                // Skip invalid JSON files
            }
        }

        result.Sort((a, b) => string.Compare(a.SurfaceNumber, b.SurfaceNumber, StringComparison.OrdinalIgnoreCase));
        return result;
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
