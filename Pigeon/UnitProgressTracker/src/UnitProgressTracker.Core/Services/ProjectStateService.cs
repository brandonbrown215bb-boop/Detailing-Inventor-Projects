using System;
using System.Collections.Generic;
using System.Linq;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public static class ProjectStateService
{
    public static List<string> FindRenumberCandidates(SurfaceModel surface, ProjectStateModel project)
    {
        if (surface == null || project == null) return new List<string>();
        string fp = GeometryFingerprinter.CalculateFingerprint(surface);
        return FindRenumberCandidates(fp, project);
    }

    public static List<string> FindRenumberCandidates(string geometryFingerprint, ProjectStateModel project)
    {
        if (string.IsNullOrEmpty(geometryFingerprint) || project == null) return new List<string>();

        return project.Retired
            .Where(kv => string.Equals(kv.Value.GeometryFingerprint, geometryFingerprint, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .ToList();
    }

    public static bool RenumberSurfaceInPlace(ProjectStateModel project, string fileKey, string newNumber)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        string trimmedNew = (newNumber ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmedNew))
            throw new ArgumentException("Surface number cannot be empty.", nameof(newNumber));

        if (!project.Surfaces.TryGetValue(fileKey, out var record))
        {
            // Fallback search by DisplayNumber
            var match = project.Surfaces.FirstOrDefault(kv => string.Equals(kv.Value.DisplayNumber, fileKey, StringComparison.OrdinalIgnoreCase));
            if (match.Key != null)
            {
                fileKey = match.Key;
                record = match.Value;
            }
            else
            {
                throw new KeyNotFoundException($"Surface '{fileKey}' not found in active project.");
            }
        }

        string oldDisplay = record.DisplayNumber ?? fileKey;
        if (string.Equals(oldDisplay, trimmedNew, StringComparison.OrdinalIgnoreCase))
            return false; // No change

        foreach (var kv in project.Surfaces)
        {
            if (string.Equals(kv.Key, fileKey, StringComparison.OrdinalIgnoreCase)) continue;
            string existingDisplay = kv.Value.DisplayNumber ?? kv.Key;
            if (string.Equals(existingDisplay, trimmedNew, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kv.Key, trimmedNew, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Surface number '{trimmedNew}' is already in use by surface '{kv.Key}'.");
            }
        }

        // Retire old display number snapshot
        project.Retired[oldDisplay] = new RetiredSurfaceRecordModel
        {
            RetiredAt = DateTime.UtcNow,
            SupersededBy = trimmedNew,
            TransferType = "renumber",
            FileKey = fileKey,
            GeometryFingerprint = record.GeometryFingerprint,
            Snapshot = record.Clone()
        };

        if (!record.PreviousNumbers.Contains(oldDisplay, StringComparer.OrdinalIgnoreCase))
            record.PreviousNumbers.Add(oldDisplay);

        record.DisplayNumber = trimmedNew;
        record.UpdatedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public static void LinkPreviousSurface(ProjectStateModel project, string currentNumber, string previousNumber, string transferType = "renumber")
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        if (string.IsNullOrWhiteSpace(currentNumber) || string.IsNullOrWhiteSpace(previousNumber)) return;

        // Find active surface
        SurfaceRecordModel? activeRecord = null;
        if (project.Surfaces.TryGetValue(currentNumber, out activeRecord) == false)
        {
            var match = project.Surfaces.FirstOrDefault(kv => string.Equals(kv.Value.DisplayNumber, currentNumber, StringComparison.OrdinalIgnoreCase));
            activeRecord = match.Value;
        }

        if (activeRecord == null)
            throw new KeyNotFoundException($"Active surface '{currentNumber}' not found.");

        if (project.Retired.TryGetValue(previousNumber, out var retiredRecord))
        {
            if (string.Equals(transferType, "renumber", StringComparison.OrdinalIgnoreCase) && retiredRecord.Snapshot != null)
            {
                activeRecord.StateId = retiredRecord.Snapshot.StateId;
                activeRecord.Notes = retiredRecord.Snapshot.Notes;
                if (retiredRecord.Snapshot.Checklist != null)
                {
                    foreach (var (k, v) in retiredRecord.Snapshot.Checklist)
                    {
                        activeRecord.Checklist[k] = v;
                    }
                }
            }
            retiredRecord.SupersededBy = currentNumber;
            retiredRecord.TransferType = transferType;
        }

        if (!activeRecord.PreviousNumbers.Contains(previousNumber, StringComparer.OrdinalIgnoreCase))
        {
            activeRecord.PreviousNumbers.Add(previousNumber);
        }

        project.UpdatedAt = DateTime.UtcNow;
    }

    public static void RetireMissingSurfaces(ProjectStateModel project, IEnumerable<string> activeScannedKeys)
    {
        if (project == null || activeScannedKeys == null) return;
        var activeSet = new HashSet<string>(activeScannedKeys, StringComparer.OrdinalIgnoreCase);

        var keysToRetire = project.Surfaces.Keys
            .Where(k => !activeSet.Contains(k))
            .ToList();

        foreach (var key in keysToRetire)
        {
            var record = project.Surfaces[key];
            project.Retired[key] = new RetiredSurfaceRecordModel
            {
                RetiredAt = DateTime.UtcNow,
                TransferType = "missing",
                FileKey = key,
                GeometryFingerprint = record.GeometryFingerprint,
                Snapshot = record.Clone()
            };
            project.Surfaces.Remove(key);
        }

        if (keysToRetire.Count > 0)
        {
            project.UpdatedAt = DateTime.UtcNow;
        }
    }
}
