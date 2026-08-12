using System;
using System.Collections.Generic;
using System.Linq;
using UnitProgressTracker.Core.Models;

namespace UnitProgressTracker.Core.Services;

public class RescanReconcileResult
{
    public List<SurfaceModel> ReconciledSurfaces { get; set; } = new();
    public List<SurfaceModel> ExactMatches { get; set; } = new();
    public List<SurfaceModel> NewSurfaces { get; set; } = new();
    public List<SurfaceModel> MissingSurfaces { get; set; } = new();
    public List<RenumberCandidate> RenumberCandidates { get; set; } = new();
    public List<RescanConflict> Conflicts { get; set; } = new();
    public List<GeometryIntrusionFlagModel> IntrusionFlags { get; set; } = new();
}

public class RenumberCandidate
{
    public SurfaceModel ScannedCandidate { get; set; } = new();
    public SurfaceModel ExistingSurface { get; set; } = new();
}

public class RescanConflict
{
    public string SurfaceNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public enum MissingSurfaceResolution
{
    OverrideMissing,
    MarkUnnecessary,
    KeepPendingForReplacement
}

public class RescanReviewDecisions
{
    public Dictionary<string, bool> RenumberTransfers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, MissingSurfaceResolution> MissingSurfaceResolutions { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public class RescanApplyResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SurfaceModel> AppliedSurfaces { get; set; } = new();
    public List<GeometryIntrusionFlagModel> IntrusionFlags { get; set; } = new();
    public int ConfirmedRenumberCount { get; set; }
    public int RetiredMissingCount { get; set; }
}

public static class RescanReconciler
{
    public static RescanReconcileResult Reconcile(
        IEnumerable<SurfaceModel> existingSurfaces,
        IEnumerable<SurfaceModel> scannedCandidates,
        string? checklistTemplate)
    {
        return Reconcile(existingSurfaces, scannedCandidates, ParseTemplateString(checklistTemplate));
    }

    public static RescanReconcileResult Reconcile(
        IEnumerable<SurfaceModel> existingSurfaces,
        IEnumerable<SurfaceModel> scannedCandidates,
        IEnumerable<string>? checklistTemplate = null)
    {
        var result = new RescanReconcileResult();
        var existingList = existingSurfaces?.ToList() ?? new List<SurfaceModel>();
        var scannedList = scannedCandidates?.ToList() ?? new List<SurfaceModel>();

        var existingGroups = existingList
            .Where(s => !string.IsNullOrWhiteSpace(s.SurfaceNumber))
            .GroupBy(s => s.SurfaceNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var existingMap = existingGroups
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);

        foreach (var duplicate in existingGroups.Where(group => group.Count() > 1))
        {
            result.Conflicts.Add(new RescanConflict
            {
                SurfaceNumber = duplicate.Key,
                Message = $"Active project contains duplicate surface identity '{duplicate.Key}'."
            });
        }

        var duplicateScannedKeys = scannedList
            .Where(s => !string.IsNullOrWhiteSpace(s.SurfaceNumber))
            .GroupBy(s => s.SurfaceNumber, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var duplicateKey in duplicateScannedKeys)
        {
            result.Conflicts.Add(new RescanConflict
            {
                SurfaceNumber = duplicateKey,
                Message = $"Scan produced duplicate surface identity '{duplicateKey}'."
            });
        }

        var matchedExistingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reconciledSurfaces = new List<SurfaceModel>();

        // Phase 1: Exact SurfaceNumber Matching
        foreach (var candidate in scannedList)
        {
            string key = candidate.SurfaceNumber;
            if (duplicateScannedKeys.Contains(key)) continue;
            if (!string.IsNullOrWhiteSpace(key) && existingMap.TryGetValue(key, out var existing))
            {
                matchedExistingKeys.Add(key);
                candidate.StateId = existing.StateId ?? "current";
                candidate.Notes = existing.Notes ?? string.Empty;
                candidate.IsHidden = existing.IsHidden;
                candidate.DisplayNumber = existing.DisplayNumber ?? key;
                candidate.PreviousNumbers = existing.PreviousNumbers != null
                    ? new List<string>(existing.PreviousNumbers)
                    : new List<string>();
                candidate.Checklist = existing.Checklist != null
                    ? new Dictionary<string, bool>(existing.Checklist, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, bool>();
                candidate.GeometryFingerprint = GeometryFingerprinter.CalculateFingerprint(candidate);

                reconciledSurfaces.Add(candidate);
                result.ExactMatches.Add(candidate);
            }
        }

        // Phase 2: Renumber Detection & New Surface Initialization
        var defaultChecklistKeys = ParseChecklistTemplate(checklistTemplate);
        var reservedRenumberSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in scannedList)
        {
            string key = candidate.SurfaceNumber;
            if (string.IsNullOrWhiteSpace(key) || duplicateScannedKeys.Contains(key) || matchedExistingKeys.Contains(key)) continue;

            candidate.GeometryFingerprint = GeometryFingerprinter.CalculateFingerprint(candidate);

            // Check for renumber candidate by fingerprint match
            var fingerprintMatches = existingList.Where(e =>
                !matchedExistingKeys.Contains(e.SurfaceNumber) &&
                !reservedRenumberSources.Contains(e.SurfaceNumber) &&
                !string.IsNullOrWhiteSpace(e.GeometryFingerprint) &&
                string.Equals(e.GeometryFingerprint, candidate.GeometryFingerprint, StringComparison.OrdinalIgnoreCase))
                .ToList();

            bool isRenumberCandidate = fingerprintMatches.Count == 1;
            if (isRenumberCandidate)
            {
                var fingerprintMatch = fingerprintMatches[0];
                reservedRenumberSources.Add(fingerprintMatch.SurfaceNumber);
                result.RenumberCandidates.Add(new RenumberCandidate
                {
                    ScannedCandidate = candidate,
                    ExistingSurface = fingerprintMatch
                });
            }
            else if (fingerprintMatches.Count > 1)
            {
                result.Conflicts.Add(new RescanConflict
                {
                    SurfaceNumber = candidate.SurfaceNumber,
                    Message = $"Surface '{candidate.SurfaceNumber}' matches multiple existing geometry fingerprints."
                });
            }

            // Setup new surface state
            candidate.StateId = "current";
            candidate.Checklist = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var itemKey in defaultChecklistKeys)
            {
                candidate.Checklist[itemKey] = false;
            }

            reconciledSurfaces.Add(candidate);
            if (!isRenumberCandidate)
            {
                result.NewSurfaces.Add(candidate);
            }
        }

        // Phase 3: Missing Surface Preservation
        foreach (var existing in existingList)
        {
            if (!matchedExistingKeys.Contains(existing.SurfaceNumber))
            {
                result.MissingSurfaces.Add(existing);
                // Retain missing surface in the project so visible detailer work is never lost
                if (!reconciledSurfaces.Any(s => string.Equals(s.SurfaceNumber, existing.SurfaceNumber, StringComparison.OrdinalIgnoreCase)))
                {
                    reconciledSurfaces.Add(existing);
                }
            }
        }

        result.ReconciledSurfaces = reconciledSurfaces;
        result.IntrusionFlags = GeometryIntrusionChecker.CheckIntrusions(reconciledSurfaces);

        return result;
    }

    private static List<string> ParseChecklistTemplate(IEnumerable<string>? template)
    {
        if (template != null && template.Any())
        {
            return template
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return new List<string>
        {
            "Verified dimensions",
            "Verified material",
            "Verified openings",
            "Paperwork complete"
        };
    }

    private static List<string>? ParseTemplateString(string? template)
    {
        if (string.IsNullOrWhiteSpace(template)) return null;
        return template.Split(new[] { '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
