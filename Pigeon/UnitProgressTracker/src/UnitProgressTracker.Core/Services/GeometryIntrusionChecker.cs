using System;
using System.Collections.Generic;
using System.Linq;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public static class GeometryIntrusionChecker
{
    private const double Tolerance = 0.001;

    public static List<GeometryIntrusionFlagModel> CheckIntrusions(IEnumerable<SurfaceModel> surfaces)
    {
        var flags = new List<GeometryIntrusionFlagModel>();
        var surfaceList = surfaces.Where(s => s != null && s.Boxes.Count > 0).ToList();

        for (int i = 0; i < surfaceList.Count; i++)
        {
            var s1 = surfaceList[i];
            var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int j = 0; j < surfaceList.Count; j++)
            {
                if (i == j) continue;
                var s2 = surfaceList[j];

                if (SurfacesOverlapVolumetrically(s1, s2))
                {
                    affected.Add(s2.SurfaceNumber);
                }
            }

            if (affected.Count > 0)
            {
                flags.Add(new GeometryIntrusionFlagModel
                {
                    SurfaceNumber = s1.SurfaceNumber,
                    AffectedSurfaceNumbers = affected.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList(),
                    Message = $"Geometry intrusion detected between {s1.SurfaceNumber} and {string.Join(", ", affected)}.",
                    Resolved = false
                });
            }
        }

        return flags;
    }

    public static List<GeometryIntrusionFlagModel> ReconcileFlags(
        IEnumerable<GeometryIntrusionFlagModel>? existingFlags,
        IEnumerable<GeometryIntrusionFlagModel>? detectedFlags)
    {
        var detectedBySurface = (detectedFlags ?? Enumerable.Empty<GeometryIntrusionFlagModel>())
            .GroupBy(flag => flag.SurfaceNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var reconciled = new List<GeometryIntrusionFlagModel>();
        foreach (var existing in existingFlags ?? Enumerable.Empty<GeometryIntrusionFlagModel>())
        {
            if (detectedBySurface.Remove(existing.SurfaceNumber, out var detected))
            {
                reconciled.Add(CloneFlag(detected, resolved: false));
            }
            else
            {
                reconciled.Add(CloneFlag(existing, resolved: true));
            }
        }

        reconciled.AddRange(detectedBySurface.Values.Select(flag => CloneFlag(flag, resolved: false)));
        return reconciled;
    }

    private static GeometryIntrusionFlagModel CloneFlag(GeometryIntrusionFlagModel source, bool resolved)
    {
        return new GeometryIntrusionFlagModel
        {
            SurfaceNumber = source.SurfaceNumber,
            AffectedSurfaceNumbers = new List<string>(source.AffectedSurfaceNumbers ?? new List<string>()),
            Message = source.Message,
            Resolved = resolved
        };
    }

    private static bool SurfacesOverlapVolumetrically(SurfaceModel s1, SurfaceModel s2)
    {
        foreach (var b1 in s1.Boxes)
        {
            foreach (var b2 in s2.Boxes)
            {
                if (BoxesOverlapVolumetrically(b1, b2))
                    return true;
            }
        }
        return false;
    }

    private static bool BoxesOverlapVolumetrically(GeometryBox b1, GeometryBox b2)
    {
        double minX1 = Math.Min(b1.X, b1.X + b1.XLength);
        double maxX1 = Math.Max(b1.X, b1.X + b1.XLength);
        double minY1 = Math.Min(b1.Y, b1.Y + b1.YLength);
        double maxY1 = Math.Max(b1.Y, b1.Y + b1.YLength);
        double minZ1 = Math.Min(b1.Z, b1.Z + b1.ZLength);
        double maxZ1 = Math.Max(b1.Z, b1.Z + b1.ZLength);

        double minX2 = Math.Min(b2.X, b2.X + b2.XLength);
        double maxX2 = Math.Max(b2.X, b2.X + b2.XLength);
        double minY2 = Math.Min(b2.Y, b2.Y + b2.YLength);
        double maxY2 = Math.Max(b2.Y, b2.Y + b2.YLength);
        double minZ2 = Math.Min(b2.Z, b2.Z + b2.ZLength);
        double maxZ2 = Math.Max(b2.Z, b2.Z + b2.ZLength);

        bool overlapX = (maxX1 - Tolerance > minX2) && (minX1 + Tolerance < maxX2);
        bool overlapY = (maxY1 - Tolerance > minY2) && (minY1 + Tolerance < maxY2);
        bool overlapZ = (maxZ1 - Tolerance > minZ2) && (minZ1 + Tolerance < maxZ2);

        return overlapX && overlapY && overlapZ;
    }
}
