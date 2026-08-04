using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public static class GeometryFingerprinter
{
    public static string CalculateFingerprint(IEnumerable<GeometryBox>? boxes)
    {
        if (boxes == null) return string.Empty;
        var list = boxes.ToList();
        if (list.Count == 0) return string.Empty;

        var sortedBoxStrings = list
            .Select(b => string.Format(
                CultureInfo.InvariantCulture,
                "{0:F3},{1:F3},{2:F3},{3:F3},{4:F3},{5:F3}",
                b.X, b.Y, b.Z, b.XLength, b.YLength, b.ZLength))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        return string.Join("|", sortedBoxStrings);
    }

    public static string CalculateFingerprint(SurfaceModel? surface)
    {
        if (surface == null) return string.Empty;
        if (surface.Boxes != null && surface.Boxes.Count > 0)
        {
            return CalculateFingerprint(surface.Boxes);
        }
        return surface.GeometryFingerprint ?? string.Empty;
    }
}
