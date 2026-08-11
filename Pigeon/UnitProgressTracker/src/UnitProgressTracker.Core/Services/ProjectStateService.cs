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

    public static void SyncChecklistTemplateToSurfaces(
        ProjectStateModel project,
        IEnumerable<string>? newTemplateItems,
        bool resetExistingWork = false,
        IEnumerable<SurfaceModel>? inMemorySurfaces = null)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));

        var cleanTemplate = (newTemplateItems ?? Enumerable.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (project.Preferences == null)
            project.Preferences = new DisplayPreferences();

        project.Preferences.ChecklistTemplate = cleanTemplate;

        if (project.Surfaces != null)
        {
            foreach (var record in project.Surfaces.Values)
            {
                if (record.Checklist == null || resetExistingWork)
                {
                    record.Checklist = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                }

                foreach (var itemKey in cleanTemplate)
                {
                    if (!record.Checklist.ContainsKey(itemKey))
                    {
                        record.Checklist[itemKey] = false;
                    }
                }
            }
        }

        if (inMemorySurfaces != null)
        {
            foreach (var surf in inMemorySurfaces)
            {
                if (surf.Checklist == null || resetExistingWork)
                {
                    surf.Checklist = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                }

                foreach (var itemKey in cleanTemplate)
                {
                    if (!surf.Checklist.ContainsKey(itemKey))
                    {
                        surf.Checklist[itemKey] = false;
                    }
                }
            }
        }

        project.UpdatedAt = DateTime.UtcNow;
    }

    public static ReplaceSurfaceResult ReplaceSurfaceInPlace(
        ProjectStateModel project,
        SurfaceModel existingSurface,
        SurfaceModel replacementCandidate,
        IEnumerable<SurfaceModel> activeSurfaces,
        bool confirmRenumberTransfer = true)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        if (existingSurface == null) throw new ArgumentNullException(nameof(existingSurface));
        if (replacementCandidate == null) throw new ArgumentNullException(nameof(replacementCandidate));

        string targetKey = existingSurface.SurfaceNumber;
        if (!project.Surfaces.TryGetValue(targetKey, out var targetRecord))
        {
            var match = project.Surfaces.FirstOrDefault(kv => string.Equals(kv.Value.DisplayNumber, targetKey, StringComparison.OrdinalIgnoreCase));
            if (match.Key != null)
            {
                targetKey = match.Key;
                targetRecord = match.Value;
            }
            else
            {
                return new ReplaceSurfaceResult
                {
                    Success = false,
                    ErrorMessage = $"Target surface '{targetKey}' not found in project state."
                };
            }
        }

        string oldDisplayNumber = targetRecord.DisplayNumber ?? existingSurface.EffectiveDisplayNumber;
        string newDisplayNumber = replacementCandidate.EffectiveDisplayNumber;
        string newFingerprint = GeometryFingerprinter.CalculateFingerprint(replacementCandidate);

        bool isSameIdentity = string.Equals(oldDisplayNumber, newDisplayNumber, StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(targetKey, replacementCandidate.SurfaceNumber, StringComparison.OrdinalIgnoreCase);

        if (isSameIdentity)
        {
            targetRecord.GeometryFingerprint = newFingerprint;
            targetRecord.UpdatedAt = DateTime.UtcNow;

            existingSurface.Boxes = replacementCandidate.Boxes ?? new List<GeometryBox>();
            existingSurface.PartNumber = replacementCandidate.PartNumber;
            existingSurface.SurfaceType = replacementCandidate.SurfaceType;
            existingSurface.SurfaceUnitSide = replacementCandidate.SurfaceUnitSide;
            existingSurface.ConfigurationKind = replacementCandidate.ConfigurationKind;
            existingSurface.SkidNumber = replacementCandidate.SkidNumber;
            existingSurface.SkidId = replacementCandidate.SkidId;
            existingSurface.Openings = replacementCandidate.Openings ?? new List<OpeningModel>();
            existingSurface.BulkheadChannels = replacementCandidate.BulkheadChannels ?? new List<GeometryBox>();
            existingSurface.BulkheadHolePatterns = replacementCandidate.BulkheadHolePatterns ?? new List<BulkheadHolePatternModel>();
            existingSurface.GeometryFingerprint = newFingerprint;
            existingSurface.FilePath = replacementCandidate.FilePath;
            existingSurface.RelativePath = replacementCandidate.RelativePath;
            existingSurface.SourceType = replacementCandidate.SourceType;
        }
        else
        {
            // Retire old display number snapshot for audit lineage
            project.Retired[oldDisplayNumber] = new RetiredSurfaceRecordModel
            {
                RetiredAt = DateTime.UtcNow,
                SupersededBy = newDisplayNumber,
                TransferType = "replace",
                FileKey = targetKey,
                GeometryFingerprint = targetRecord.GeometryFingerprint,
                Snapshot = targetRecord.Clone()
            };

            var newRecord = new SurfaceRecordModel
            {
                DisplayNumber = newDisplayNumber,
                GeometryFingerprint = newFingerprint,
                StateId = targetRecord.StateId,
                Notes = targetRecord.Notes,
                Checklist = new Dictionary<string, bool>(targetRecord.Checklist, StringComparer.OrdinalIgnoreCase),
                PreviousNumbers = new List<string>(targetRecord.PreviousNumbers),
                UpdatedAt = DateTime.UtcNow
            };

            if (!newRecord.PreviousNumbers.Contains(oldDisplayNumber, StringComparer.OrdinalIgnoreCase))
            {
                newRecord.PreviousNumbers.Add(oldDisplayNumber);
            }

            project.Surfaces.Remove(targetKey);
            project.Surfaces[replacementCandidate.SurfaceNumber] = newRecord;

            replacementCandidate.StateId = newRecord.StateId ?? "current";
            replacementCandidate.Notes = newRecord.Notes;
            replacementCandidate.Checklist = newRecord.Checklist;
            replacementCandidate.PreviousNumbers = newRecord.PreviousNumbers;
            replacementCandidate.DisplayNumber = newRecord.DisplayNumber;
            replacementCandidate.GeometryFingerprint = newFingerprint;
        }

        // Recalculate intrusion flags across active surfaces
        var allSurfaces = activeSurfaces
            .Where(s => s != existingSurface &&
                        !string.Equals(s.SurfaceNumber, existingSurface.SurfaceNumber, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(s.SurfaceNumber, replacementCandidate.SurfaceNumber, StringComparison.OrdinalIgnoreCase))
            .ToList();
        allSurfaces.Add(isSameIdentity ? existingSurface : replacementCandidate);

        var newlyDetectedIntrusions = GeometryIntrusionChecker.CheckIntrusions(allSurfaces);

        if (project.IntrusionFlags == null)
            project.IntrusionFlags = new List<GeometryIntrusionFlagModel>();

        // Persist newly detected intrusion flags into project state until manually cleaned/overwritten
        foreach (var newFlag in newlyDetectedIntrusions)
        {
            var existingFlag = project.IntrusionFlags.FirstOrDefault(f =>
                string.Equals(f.SurfaceNumber, newFlag.SurfaceNumber, StringComparison.OrdinalIgnoreCase));

            if (existingFlag != null)
            {
                existingFlag.AffectedSurfaceNumbers = newFlag.AffectedSurfaceNumbers;
                existingFlag.Message = newFlag.Message;
                existingFlag.Resolved = false;
            }
            else
            {
                project.IntrusionFlags.Add(newFlag);
            }
        }

        bool intrusionDetected = newlyDetectedIntrusions.Any(f =>
            string.Equals(f.SurfaceNumber, replacementCandidate.SurfaceNumber, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f.SurfaceNumber, existingSurface.SurfaceNumber, StringComparison.OrdinalIgnoreCase) ||
            f.AffectedSurfaceNumbers.Contains(replacementCandidate.SurfaceNumber, StringComparer.OrdinalIgnoreCase) ||
            f.AffectedSurfaceNumbers.Contains(existingSurface.SurfaceNumber, StringComparer.OrdinalIgnoreCase));

        project.UpdatedAt = DateTime.UtcNow;

        return new ReplaceSurfaceResult
        {
            Success = true,
            Renumbered = !isSameIdentity,
            TrackingTransferred = true,
            IntrusionDetected = intrusionDetected,
            OldSurfaceNumber = oldDisplayNumber,
            NewSurfaceNumber = newDisplayNumber,
            IntrusionFlags = project.IntrusionFlags
        };
    }
}

public class ReplaceSurfaceResult
{
    public bool Success { get; set; }
    public bool Renumbered { get; set; }
    public bool TrackingTransferred { get; set; }
    public bool IntrusionDetected { get; set; }
    public string? ErrorMessage { get; set; }
    public string OldSurfaceNumber { get; set; } = string.Empty;
    public string NewSurfaceNumber { get; set; } = string.Empty;
    public List<GeometryIntrusionFlagModel> IntrusionFlags { get; set; } = new();
}

