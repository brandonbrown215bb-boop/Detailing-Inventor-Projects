using System;
using System.Collections.Generic;
using System.Linq;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public static class BulkheadChannelCalculator
{
    public const double ChannelWidth = 1.5; // Standard 1.5" channel width in JCI AHU construction

    public static List<GeometryBox> CalculateChannels(
        IEnumerable<BulkheadHolePatternModel> patterns,
        List<GeometryBox>? surfaceBoxes = null,
        string surfaceUnitSide = "")
    {
        var result = new List<GeometryBox>();
        if (patterns == null || !patterns.Any()) return result;

        string normSurfSide = NormalizeSide(surfaceUnitSide);

        // Base surface bounding dimensions
        double surfX = 0, surfY = 0, surfZ = 0;
        double surfXLength = 30, surfYLength = 75, surfZLength = 2;

        if (surfaceBoxes != null && surfaceBoxes.Count > 0)
        {
            var first = surfaceBoxes[0];
            surfX = first.X;
            surfY = first.Y;
            surfZ = first.Z;
            surfXLength = Math.Max(0.1, first.XLength);
            surfYLength = Math.Max(0.1, first.YLength);
            surfZLength = Math.Max(0.1, first.ZLength);
        }

        // Group hole patterns by BulkheadPart & Side
        var groups = patterns
            .Where(p => p.WidthQty > 0)
            .GroupBy(p => new { Part = p.BulkheadPartNumber, Side = NormalizeSide(p.UnitSide) });

        foreach (var group in groups)
        {
            string patSide = group.Key.Side;

            // 1. Surface Side Matching: Only generate channels for the matching surface side
            if (!IsSideMatching(normSurfSide, patSide))
            {
                continue;
            }

            // 2. Bottom Side Filter: Bulkhead channels only present on Bottom surfaces for FAN bulkheads/segments
            if (patSide == "bottom")
            {
                bool isFan = group.Any(p =>
                    (p.BulkheadDescription ?? "").Contains("FAN", StringComparison.OrdinalIgnoreCase) ||
                    (p.BulkheadPartNumber ?? "").Contains("FAN", StringComparison.OrdinalIgnoreCase) ||
                    (p.SegmentType ?? "").Contains("FN", StringComparison.OrdinalIgnoreCase)
                );
                if (!isFan) continue;
            }

            // 3. Find overall min start offset and max end offset across all hole pattern entries in line
            double minDoa = group.Min(p => p.DoaOffset);
            double minWidthOffset = group.Min(p => p.WidthOffset);
            double maxWidthEnd = group.Max(p =>
                p.WidthOffset + (p.WidthQty > 1 ? (p.WidthQty - 1) * p.WidthSpacing : 0.0)
            );

            // Channel starts at minWidthOffset and spans (maxWidthEnd - minWidthOffset) + 1.5"
            double patternSpan = Math.Max(ChannelWidth, (maxWidthEnd - minWidthOffset) + ChannelWidth);

            double x, y, z;
            double xl, yl, zl;

            // X position along airflow (minDoa is distance along X from front edge)
            double xCenter = minDoa > 0 ? (surfX + minDoa) : (surfX + surfXLength / 2.0);
            x = Math.Max(surfX, xCenter - ChannelWidth / 2.0);
            xl = Math.Min(ChannelWidth, surfX + surfXLength - x);

            switch (patSide)
            {
                case "top":
                case "bottom":
                    y = (patSide == "top") ? Math.Max(surfY, surfY + surfYLength - ChannelWidth) : surfY;
                    yl = Math.Min(ChannelWidth, surfYLength);

                    // Bulkhead channel starts at minWidthOffset on the surface
                    z = Math.Max(surfZ, surfZ + minWidthOffset);
                    zl = Math.Min(patternSpan, surfZ + surfZLength - z);
                    break;

                case "left":
                case "right":
                    z = (patSide == "right") ? Math.Max(surfZ, surfZ + surfZLength - ChannelWidth) : surfZ;
                    zl = Math.Min(ChannelWidth, surfZLength);

                    // Bulkhead channel starts at minWidthOffset on the surface
                    y = Math.Max(surfY, surfY + minWidthOffset);
                    yl = Math.Min(patternSpan, surfY + surfYLength - y);
                    break;

                case "front":
                case "rear":
                    x = surfX;
                    xl = Math.Min(ChannelWidth, surfXLength);

                    y = Math.Max(surfY, surfY + minWidthOffset);
                    yl = Math.Min(ChannelWidth, surfYLength);

                    z = Math.Max(surfZ, surfZ + minDoa);
                    zl = Math.Min(patternSpan, surfZ + surfZLength - z);
                    break;

                default:
                    y = Math.Max(surfY, surfY + minWidthOffset);
                    yl = Math.Min(ChannelWidth, surfYLength);
                    z = surfZ;
                    zl = Math.Min(patternSpan, surfZLength);
                    break;
            }

            if (xl > 0 && yl > 0 && zl > 0)
            {
                result.Add(new GeometryBox(x, y, z, xl, yl, zl));
            }
        }

        return result;
    }

    private static string NormalizeSide(string side)
    {
        if (string.IsNullOrWhiteSpace(side)) return "";
        string s = side.Trim().ToLowerInvariant();
        if (s.Contains("roof") || s.Contains("top")) return "top";
        if (s.Contains("floor") || s.Contains("bottom")) return "bottom";
        if (s.Contains("left")) return "left";
        if (s.Contains("right")) return "right";
        if (s.Contains("front")) return "front";
        if (s.Contains("rear") || s.Contains("back")) return "rear";
        return s;
    }

    private static bool IsSideMatching(string surfaceSide, string patternSide)
    {
        if (string.IsNullOrEmpty(surfaceSide)) return true;
        return string.Equals(surfaceSide, patternSide, StringComparison.OrdinalIgnoreCase);
    }
}
