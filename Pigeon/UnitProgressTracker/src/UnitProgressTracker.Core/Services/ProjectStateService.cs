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
            Snapshot = record.Clone(),
            GeometrySnapshot = project.Geometry.TryGetValue(fileKey, out var renumberGeometry)
                ? CloneSurface(renumberGeometry)
                : null
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
                Snapshot = record.Clone(),
                GeometrySnapshot = project.Geometry.TryGetValue(key, out var missingGeometry)
                    ? CloneSurface(missingGeometry)
                    : null
            };
            project.Surfaces.Remove(key);
            project.Geometry.Remove(key);
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

    public static AddSurfacesProposal BuildAddSurfacesProposal(
        ProjectStateModel project,
        IEnumerable<SurfaceModel> activeSurfaces,
        IEnumerable<SurfaceModel> candidates)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));

        var activeList = (activeSurfaces ?? Enumerable.Empty<SurfaceModel>()).ToList();
        var accepted = new List<SurfaceModel>();
        var issues = new List<SurfaceOperationIssue>();
        var occupiedIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var surface in activeList)
        {
            if (!string.IsNullOrWhiteSpace(surface.SurfaceNumber)) occupiedIdentities.Add(surface.SurfaceNumber);
            if (!string.IsNullOrWhiteSpace(surface.EffectiveDisplayNumber)) occupiedIdentities.Add(surface.EffectiveDisplayNumber);
        }

        foreach (var source in candidates ?? Enumerable.Empty<SurfaceModel>())
        {
            string identifier = string.IsNullOrWhiteSpace(source?.SurfaceNumber) ? "(unknown surface)" : source.SurfaceNumber;
            if (source == null || !HasValidGeometry(source))
            {
                issues.Add(new SurfaceOperationIssue
                {
                    Kind = SurfaceOperationIssueKind.InvalidGeometry,
                    SurfaceIdentifier = identifier,
                    Message = $"'{identifier}' did not produce renderable geometry."
                });
                continue;
            }

            if (occupiedIdentities.Contains(source.SurfaceNumber) || occupiedIdentities.Contains(source.EffectiveDisplayNumber))
            {
                issues.Add(new SurfaceOperationIssue
                {
                    Kind = SurfaceOperationIssueKind.DuplicateIdentity,
                    SurfaceIdentifier = identifier,
                    Message = $"'{identifier}' duplicates an active or already accepted surface identity."
                });
                continue;
            }

            var candidate = CloneSurface(source);
            candidate.StateId = "current";
            candidate.Notes = string.Empty;
            candidate.IsHidden = false;
            candidate.PreviousNumbers = new List<string>();
            candidate.Checklist = (project.Preferences?.ChecklistTemplate ?? new List<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(item => item, _ => false, StringComparer.OrdinalIgnoreCase);
            candidate.GeometryFingerprint = GeometryFingerprinter.CalculateFingerprint(candidate);

            accepted.Add(candidate);
            occupiedIdentities.Add(candidate.SurfaceNumber);
            occupiedIdentities.Add(candidate.EffectiveDisplayNumber);
        }

        var combined = activeList.Concat(accepted).ToList();
        return new AddSurfacesProposal
        {
            AcceptedSurfaces = accepted,
            Issues = issues,
            IntrusionFlags = GeometryIntrusionChecker.CheckIntrusions(combined)
        };
    }

    public static AddSurfacesApplyResult ApplyAddSurfacesProposal(
        ProjectStateModel project,
        AddSurfacesProposal proposal,
        IEnumerable<SurfaceModel> activeSurfaces)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        if (proposal == null) throw new ArgumentNullException(nameof(proposal));

        if (proposal.AcceptedSurfaces.Count == 0)
        {
            return new AddSurfacesApplyResult
            {
                Success = false,
                ErrorMessage = "The reviewed add proposal contains no accepted surfaces."
            };
        }

        var activeList = (activeSurfaces ?? Enumerable.Empty<SurfaceModel>()).ToList();
        var occupied = new HashSet<string>(project.Surfaces.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var record in project.Surfaces.Values)
        {
            if (!string.IsNullOrWhiteSpace(record.DisplayNumber)) occupied.Add(record.DisplayNumber);
        }

        foreach (var candidate in proposal.AcceptedSurfaces)
        {
            if (!HasValidGeometry(candidate))
            {
                return new AddSurfacesApplyResult
                {
                    Success = false,
                    ErrorMessage = $"Accepted surface '{candidate.SurfaceNumber}' no longer has valid geometry."
                };
            }

            var candidateIdentities = new[] { candidate.SurfaceNumber, candidate.EffectiveDisplayNumber }
                .Where(identity => !string.IsNullOrWhiteSpace(identity))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (candidateIdentities.Any(occupied.Contains))
            {
                return new AddSurfacesApplyResult
                {
                    Success = false,
                    ErrorMessage = $"Accepted surface '{candidate.SurfaceNumber}' now conflicts with active project state."
                };
            }

            foreach (var identity in candidateIdentities) occupied.Add(identity);
        }

        var newRecords = new Dictionary<string, SurfaceRecordModel>(project.Surfaces, StringComparer.OrdinalIgnoreCase);
        var newGeometry = new Dictionary<string, SurfaceModel>(project.Geometry, StringComparer.OrdinalIgnoreCase);
        var added = proposal.AcceptedSurfaces.Select(CloneSurface).ToList();
        foreach (var surface in added)
        {
            newRecords[surface.SurfaceNumber] = CreateRecordFromSurface(surface);
            newGeometry[surface.SurfaceNumber] = surface;
        }

        var detected = GeometryIntrusionChecker.CheckIntrusions(activeList.Concat(added));
        var reconciledFlags = GeometryIntrusionChecker.ReconcileFlags(project.IntrusionFlags, detected);

        project.Surfaces = newRecords;
        project.Geometry = newGeometry;
        project.IntrusionFlags = reconciledFlags;
        project.UpdatedAt = DateTime.UtcNow;

        return new AddSurfacesApplyResult
        {
            Success = true,
            AddedSurfaces = added,
            IntrusionFlags = reconciledFlags
        };
    }

    public static RetireSurfaceResult RetireSurface(
        ProjectStateModel project,
        SurfaceModel surface,
        IEnumerable<SurfaceModel> activeSurfaces,
        string transferType = "removed")
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        if (surface == null) throw new ArgumentNullException(nameof(surface));

        var activeList = (activeSurfaces ?? Enumerable.Empty<SurfaceModel>()).ToList();
        if (activeList.Count(candidate => ReferenceEquals(candidate, surface) ||
                                          string.Equals(candidate.SurfaceNumber, surface.SurfaceNumber, StringComparison.OrdinalIgnoreCase)) != 1)
        {
            return new RetireSurfaceResult { ErrorMessage = "Retire requires exactly one active surface." };
        }

        string fileKey = surface.SurfaceNumber;
        if (!project.Surfaces.TryGetValue(fileKey, out var record))
        {
            var match = project.Surfaces.FirstOrDefault(entry =>
                string.Equals(entry.Value.DisplayNumber, surface.EffectiveDisplayNumber, StringComparison.OrdinalIgnoreCase));
            if (match.Key == null)
            {
                return new RetireSurfaceResult { ErrorMessage = $"Surface '{surface.EffectiveDisplayNumber}' is not active in project state." };
            }

            fileKey = match.Key;
            record = match.Value;
        }

        var geometry = project.Geometry.TryGetValue(fileKey, out var savedGeometry) ? savedGeometry : surface;
        if (!HasValidGeometry(geometry))
        {
            return new RetireSurfaceResult { ErrorMessage = $"Surface '{surface.EffectiveDisplayNumber}' cannot be retired without a valid geometry snapshot." };
        }

        string retiredKey = GetAvailableRetiredKey(project, surface.EffectiveDisplayNumber);
        var retiredGeometry = CloneSurface(geometry);
        var retiredRecords = new Dictionary<string, RetiredSurfaceRecordModel>(project.Retired, StringComparer.OrdinalIgnoreCase)
        {
            [retiredKey] = new RetiredSurfaceRecordModel
            {
                RetiredAt = DateTime.UtcNow,
                TransferType = string.IsNullOrWhiteSpace(transferType) ? "removed" : transferType,
                FileKey = fileKey,
                GeometryFingerprint = record.GeometryFingerprint ?? retiredGeometry.GeometryFingerprint,
                Snapshot = record.Clone(),
                GeometrySnapshot = retiredGeometry
            }
        };

        var newRecords = new Dictionary<string, SurfaceRecordModel>(project.Surfaces, StringComparer.OrdinalIgnoreCase);
        var newGeometry = new Dictionary<string, SurfaceModel>(project.Geometry, StringComparer.OrdinalIgnoreCase);
        newRecords.Remove(fileKey);
        newGeometry.Remove(fileKey);

        var remaining = activeList.Where(candidate => !ReferenceEquals(candidate, surface) &&
            !string.Equals(candidate.SurfaceNumber, surface.SurfaceNumber, StringComparison.OrdinalIgnoreCase)).ToList();
        project.Surfaces = newRecords;
        project.Geometry = newGeometry;
        project.Retired = retiredRecords;
        project.IntrusionFlags = GeometryIntrusionChecker.ReconcileFlags(project.IntrusionFlags, GeometryIntrusionChecker.CheckIntrusions(remaining));
        project.UpdatedAt = DateTime.UtcNow;

        return new RetireSurfaceResult
        {
            Success = true,
            RetiredKey = retiredKey,
            RetiredSurface = retiredGeometry
        };
    }

    public static RestoreSurfaceResult RestoreSurface(
        ProjectStateModel project,
        string retiredKey,
        IEnumerable<SurfaceModel> activeSurfaces)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        if (string.IsNullOrWhiteSpace(retiredKey) || !project.Retired.TryGetValue(retiredKey, out var retired))
        {
            return new RestoreSurfaceResult { ErrorMessage = "The selected retired surface no longer exists." };
        }

        if (retired.RestoredAt.HasValue)
        {
            return new RestoreSurfaceResult { ErrorMessage = $"Retirement record '{retiredKey}' has already been restored." };
        }

        string fileKey = string.IsNullOrWhiteSpace(retired.FileKey) ? retiredKey : retired.FileKey;
        SurfaceModel? savedGeometry = retired.GeometrySnapshot;
        if (savedGeometry == null && project.Geometry.TryGetValue(fileKey, out var legacyGeometry))
        {
            savedGeometry = legacyGeometry;
        }

        if (!HasValidGeometry(savedGeometry))
        {
            return new RestoreSurfaceResult
            {
                ErrorMessage = $"Retired surface '{retiredKey}' has no cached geometry. Reacquire its source before restoring."
            };
        }

        var restored = CloneSurface(savedGeometry!);
        restored.SurfaceNumber = fileKey;
        if (retired.Snapshot != null)
        {
            ApplyRecordToSurface(retired.Snapshot, restored);
        }

        bool identityConflict = project.Surfaces.ContainsKey(fileKey) ||
            project.Surfaces.Values.Any(record => string.Equals(record.DisplayNumber, restored.EffectiveDisplayNumber, StringComparison.OrdinalIgnoreCase)) ||
            (activeSurfaces ?? Enumerable.Empty<SurfaceModel>()).Any(surface =>
                string.Equals(surface.SurfaceNumber, fileKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(surface.EffectiveDisplayNumber, restored.EffectiveDisplayNumber, StringComparison.OrdinalIgnoreCase));
        if (identityConflict)
        {
            return new RestoreSurfaceResult
            {
                ErrorMessage = $"Cannot restore '{retiredKey}' because identity '{restored.EffectiveDisplayNumber}' is already active."
            };
        }

        restored.GeometryFingerprint ??= GeometryFingerprinter.CalculateFingerprint(restored);
        var newRecords = new Dictionary<string, SurfaceRecordModel>(project.Surfaces, StringComparer.OrdinalIgnoreCase)
        {
            [fileKey] = CreateRecordFromSurface(restored)
        };
        var newGeometry = new Dictionary<string, SurfaceModel>(project.Geometry, StringComparer.OrdinalIgnoreCase)
        {
            [fileKey] = restored
        };

        var combined = (activeSurfaces ?? Enumerable.Empty<SurfaceModel>()).Append(restored).ToList();
        project.Surfaces = newRecords;
        project.Geometry = newGeometry;
        project.IntrusionFlags = GeometryIntrusionChecker.ReconcileFlags(project.IntrusionFlags, GeometryIntrusionChecker.CheckIntrusions(combined));
        retired.RestoredAt = DateTime.UtcNow;
        retired.RestoredAs = fileKey;
        project.UpdatedAt = DateTime.UtcNow;

        return new RestoreSurfaceResult
        {
            Success = true,
            RetiredKey = retiredKey,
            RestoredSurface = restored
        };
    }

    public static ReplaceSurfaceResult ReplaceSurfaceInPlace(
        ProjectStateModel project,
        SurfaceModel existingSurface,
        SurfaceModel replacementCandidate,
        IEnumerable<SurfaceModel> activeSurfaces,
        bool confirmRenumberTransfer = false)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        if (existingSurface == null) throw new ArgumentNullException(nameof(existingSurface));
        if (replacementCandidate == null) throw new ArgumentNullException(nameof(replacementCandidate));

        var activeList = (activeSurfaces ?? Enumerable.Empty<SurfaceModel>()).ToList();
        if (activeList.Count(surface => ReferenceEquals(surface, existingSurface) ||
                                        string.Equals(surface.SurfaceNumber, existingSurface.SurfaceNumber, StringComparison.OrdinalIgnoreCase)) != 1)
        {
            return ReplaceFailure("Replace requires exactly one selected active surface.");
        }

        if (string.IsNullOrWhiteSpace(replacementCandidate.SurfaceNumber) ||
            replacementCandidate.Boxes == null ||
            replacementCandidate.Boxes.Count == 0 ||
            replacementCandidate.Boxes.Any(box => box.XLength <= 0 || box.YLength <= 0 || box.ZLength <= 0))
        {
            return ReplaceFailure("Replacement scan did not produce one valid surface with renderable geometry.");
        }

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

        var duplicateIdentity = activeList.FirstOrDefault(surface =>
            !ReferenceEquals(surface, existingSurface) &&
            !string.Equals(surface.SurfaceNumber, existingSurface.SurfaceNumber, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(surface.SurfaceNumber, replacementCandidate.SurfaceNumber, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(surface.EffectiveDisplayNumber, replacementCandidate.SurfaceNumber, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(surface.SurfaceNumber, newDisplayNumber, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(surface.EffectiveDisplayNumber, newDisplayNumber, StringComparison.OrdinalIgnoreCase)));
        if (duplicateIdentity != null)
        {
            return ReplaceFailure($"Replacement identity '{newDisplayNumber}' is already active as '{duplicateIdentity.SurfaceNumber}'.");
        }

        if (!isSameIdentity && !confirmRenumberTransfer)
        {
            return new ReplaceSurfaceResult
            {
                Success = false,
                RequiresRenumberConfirmation = true,
                ErrorMessage = $"Replacing '{oldDisplayNumber}' with '{newDisplayNumber}' requires explicit tracking-transfer confirmation.",
                OldSurfaceNumber = oldDisplayNumber,
                NewSurfaceNumber = newDisplayNumber
            };
        }

        if (isSameIdentity)
        {
            targetRecord.GeometryFingerprint = newFingerprint;
            targetRecord.UpdatedAt = DateTime.UtcNow;
            CopyScannedGeometry(replacementCandidate, existingSurface, newFingerprint);
            project.Geometry[targetKey] = existingSurface;
        }
        else
        {
            // Retire old display number snapshot for audit lineage
            string retiredKey = GetAvailableRetiredKey(project, oldDisplayNumber);
            project.Retired[retiredKey] = new RetiredSurfaceRecordModel
            {
                RetiredAt = DateTime.UtcNow,
                SupersededBy = newDisplayNumber,
                TransferType = "replace",
                FileKey = targetKey,
                GeometryFingerprint = targetRecord.GeometryFingerprint,
                Snapshot = targetRecord.Clone(),
                GeometrySnapshot = project.Geometry.TryGetValue(targetKey, out var replacedGeometry)
                    ? CloneSurface(replacedGeometry)
                    : CloneSurface(existingSurface)
            };

            var newRecord = new SurfaceRecordModel
            {
                DisplayNumber = newDisplayNumber,
                GeometryFingerprint = newFingerprint,
                StateId = targetRecord.StateId,
                Notes = targetRecord.Notes,
                Checklist = new Dictionary<string, bool>(targetRecord.Checklist, StringComparer.OrdinalIgnoreCase),
                PreviousNumbers = new List<string>(targetRecord.PreviousNumbers),
                Hidden = targetRecord.Hidden,
                UpdatedAt = DateTime.UtcNow
            };

            if (!newRecord.PreviousNumbers.Contains(oldDisplayNumber, StringComparer.OrdinalIgnoreCase))
            {
                newRecord.PreviousNumbers.Add(oldDisplayNumber);
            }

            project.Surfaces.Remove(targetKey);
            project.Geometry.Remove(targetKey);
            project.Surfaces[replacementCandidate.SurfaceNumber] = newRecord;

            replacementCandidate.StateId = newRecord.StateId ?? "current";
            replacementCandidate.Notes = newRecord.Notes;
            replacementCandidate.Checklist = newRecord.Checklist;
            replacementCandidate.PreviousNumbers = newRecord.PreviousNumbers;
            replacementCandidate.DisplayNumber = newRecord.DisplayNumber;
            replacementCandidate.GeometryFingerprint = newFingerprint;
            replacementCandidate.IsHidden = newRecord.Hidden;

            project.Geometry[replacementCandidate.SurfaceNumber] = replacementCandidate;
        }

        // Recalculate intrusion flags across active surfaces
        var allSurfaces = activeList
            .Where(s => s != existingSurface &&
                        !string.Equals(s.SurfaceNumber, existingSurface.SurfaceNumber, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(s.SurfaceNumber, replacementCandidate.SurfaceNumber, StringComparison.OrdinalIgnoreCase))
            .ToList();
        allSurfaces.Add(isSameIdentity ? existingSurface : replacementCandidate);

        var newlyDetectedIntrusions = GeometryIntrusionChecker.CheckIntrusions(allSurfaces);
        project.IntrusionFlags = GeometryIntrusionChecker.ReconcileFlags(project.IntrusionFlags, newlyDetectedIntrusions);

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

    public static RescanApplyResult ApplyRescanProposal(
        ProjectStateModel project,
        RescanReconcileResult proposal,
        RescanReviewDecisions decisions)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        if (proposal == null) throw new ArgumentNullException(nameof(proposal));
        if (decisions == null) throw new ArgumentNullException(nameof(decisions));

        if (proposal.Conflicts.Count > 0)
        {
            return RescanFailure("Rescan contains unresolved identity conflicts.");
        }

        foreach (var renumber in proposal.RenumberCandidates)
        {
            if (!decisions.RenumberTransfers.ContainsKey(renumber.ScannedCandidate.SurfaceNumber))
            {
                return RescanFailure($"Renumber candidate '{renumber.ScannedCandidate.SurfaceNumber}' has not been reviewed.");
            }
        }

        var confirmedSources = proposal.RenumberCandidates
            .Where(candidate => decisions.RenumberTransfers[candidate.ScannedCandidate.SurfaceNumber])
            .Select(candidate => candidate.ExistingSurface.SurfaceNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var missing in proposal.MissingSurfaces.Where(surface => !confirmedSources.Contains(surface.SurfaceNumber)))
        {
            if (!decisions.MissingSurfaceResolutions.ContainsKey(missing.SurfaceNumber))
            {
                return RescanFailure($"Missing surface '{missing.SurfaceNumber}' has not been reviewed.");
            }
        }

        var applied = proposal.ExactMatches.ToList();
        applied.AddRange(proposal.NewSurfaces);
        applied.AddRange(proposal.RenumberCandidates.Select(candidate => candidate.ScannedCandidate));
        int confirmedRenumbers = 0;

        foreach (var renumber in proposal.RenumberCandidates)
        {
            if (!decisions.RenumberTransfers[renumber.ScannedCandidate.SurfaceNumber]) continue;

            TransferTracking(renumber.ExistingSurface, renumber.ScannedCandidate);
            confirmedRenumbers++;
        }

        var retirements = new List<(SurfaceModel Surface, string? SupersededBy, string TransferType)>();
        foreach (var missing in proposal.MissingSurfaces)
        {
            if (confirmedSources.Contains(missing.SurfaceNumber))
            {
                var replacement = proposal.RenumberCandidates.Single(candidate =>
                    string.Equals(candidate.ExistingSurface.SurfaceNumber, missing.SurfaceNumber, StringComparison.OrdinalIgnoreCase));
                retirements.Add((missing, replacement.ScannedCandidate.EffectiveDisplayNumber, "renumber"));
                continue;
            }

            var resolution = decisions.MissingSurfaceResolutions[missing.SurfaceNumber];
            if (resolution == MissingSurfaceResolution.MarkUnnecessary)
            {
                retirements.Add((missing, null, "missing-unnecessary"));
            }
            else
            {
                applied.Add(missing);
            }
        }

        var duplicate = applied
            .Where(surface => !string.IsNullOrWhiteSpace(surface.SurfaceNumber))
            .GroupBy(surface => surface.SurfaceNumber, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            return RescanFailure($"Reviewed rescan would create duplicate active identity '{duplicate.Key}'.");
        }

        var newSurfaceRecords = applied.ToDictionary(
            surface => surface.SurfaceNumber,
            CreateRecordFromSurface,
            StringComparer.OrdinalIgnoreCase);
        var newGeometry = new Dictionary<string, SurfaceModel>(project.Geometry, StringComparer.OrdinalIgnoreCase);
        foreach (var surface in applied)
        {
            newGeometry[surface.SurfaceNumber] = surface;
        }

        var detected = GeometryIntrusionChecker.CheckIntrusions(applied);
        var reconciledFlags = GeometryIntrusionChecker.ReconcileFlags(project.IntrusionFlags, detected);
        var newRetired = new Dictionary<string, RetiredSurfaceRecordModel>(project.Retired, StringComparer.OrdinalIgnoreCase);
        foreach (var retirement in retirements)
        {
            RetireForTransfer(newRetired, retirement.Surface, retirement.SupersededBy, retirement.TransferType);
            if (!applied.Any(surface => string.Equals(surface.SurfaceNumber, retirement.Surface.SurfaceNumber, StringComparison.OrdinalIgnoreCase)))
            {
                newGeometry.Remove(retirement.Surface.SurfaceNumber);
            }
        }

        project.Surfaces = newSurfaceRecords;
        project.Geometry = newGeometry;
        project.Retired = newRetired;
        project.IntrusionFlags = reconciledFlags;
        project.UpdatedAt = DateTime.UtcNow;

        return new RescanApplyResult
        {
            Success = true,
            AppliedSurfaces = applied,
            IntrusionFlags = reconciledFlags,
            ConfirmedRenumberCount = confirmedRenumbers,
            RetiredMissingCount = retirements.Count(retirement => retirement.TransferType == "missing-unnecessary")
        };
    }

    private static ReplaceSurfaceResult ReplaceFailure(string message)
        => new() { Success = false, ErrorMessage = message };

    private static RescanApplyResult RescanFailure(string message)
        => new() { Success = false, ErrorMessage = message };

    private static void CopyScannedGeometry(SurfaceModel source, SurfaceModel target, string fingerprint)
    {
        target.Boxes = new List<GeometryBox>(source.Boxes);
        target.PartNumber = source.PartNumber;
        target.SurfaceType = source.SurfaceType;
        target.SurfaceUnitSide = source.SurfaceUnitSide;
        target.ConfigurationKind = source.ConfigurationKind;
        target.SkidNumber = source.SkidNumber;
        target.SkidId = source.SkidId;
        target.JobContext = source.JobContext;
        target.CasingSpec = source.CasingSpec;
        target.Openings = new List<OpeningModel>(source.Openings ?? new List<OpeningModel>());
        target.BulkheadChannels = new List<GeometryBox>(source.BulkheadChannels ?? new List<GeometryBox>());
        target.BulkheadHolePatterns = new List<BulkheadHolePatternModel>(source.BulkheadHolePatterns ?? new List<BulkheadHolePatternModel>());
        target.GeometryFingerprint = fingerprint;
        target.FilePath = source.FilePath;
        target.RelativePath = source.RelativePath;
        target.SourceType = source.SourceType;
    }

    private static bool HasValidGeometry(SurfaceModel? surface)
        => surface != null &&
           !string.IsNullOrWhiteSpace(surface.SurfaceNumber) &&
           surface.Boxes != null &&
           surface.Boxes.Count > 0 &&
           surface.Boxes.All(box => box != null && box.XLength > 0 && box.YLength > 0 && box.ZLength > 0);

    private static SurfaceModel CloneSurface(SurfaceModel source)
    {
        return new SurfaceModel
        {
            SurfaceNumber = source.SurfaceNumber,
            FilePath = source.FilePath,
            RelativePath = source.RelativePath,
            SourceType = source.SourceType,
            PartNumber = source.PartNumber,
            SurfaceType = source.SurfaceType,
            SurfaceUnitSide = source.SurfaceUnitSide,
            ConfigurationKind = source.ConfigurationKind,
            SkidNumber = source.SkidNumber,
            SkidId = source.SkidId,
            StateId = source.StateId,
            Notes = source.Notes,
            IsHidden = source.IsHidden,
            Checklist = new Dictionary<string, bool>(source.Checklist ?? new Dictionary<string, bool>(), StringComparer.OrdinalIgnoreCase),
            Boxes = new List<GeometryBox>(source.Boxes ?? new List<GeometryBox>()),
            JobContext = source.JobContext,
            CasingSpec = source.CasingSpec,
            Openings = new List<OpeningModel>(source.Openings ?? new List<OpeningModel>()),
            BulkheadHolePatterns = new List<BulkheadHolePatternModel>(source.BulkheadHolePatterns ?? new List<BulkheadHolePatternModel>()),
            BulkheadChannels = new List<GeometryBox>(source.BulkheadChannels ?? new List<GeometryBox>()),
            DisplayNumber = source.DisplayNumber,
            PreviousNumbers = new List<string>(source.PreviousNumbers ?? new List<string>()),
            GeometryFingerprint = source.GeometryFingerprint
        };
    }

    private static void ApplyRecordToSurface(SurfaceRecordModel record, SurfaceModel surface)
    {
        surface.StateId = record.StateId ?? "current";
        surface.Notes = record.Notes ?? string.Empty;
        surface.IsHidden = record.Hidden;
        surface.DisplayNumber = record.DisplayNumber ?? surface.SurfaceNumber;
        surface.Checklist = new Dictionary<string, bool>(record.Checklist ?? new Dictionary<string, bool>(), StringComparer.OrdinalIgnoreCase);
        surface.PreviousNumbers = new List<string>(record.PreviousNumbers ?? new List<string>());
        surface.GeometryFingerprint = record.GeometryFingerprint ?? surface.GeometryFingerprint;
    }

    private static void TransferTracking(SurfaceModel source, SurfaceModel target)
    {
        target.StateId = source.StateId;
        target.Notes = source.Notes;
        target.IsHidden = source.IsHidden;
        target.Checklist = new Dictionary<string, bool>(source.Checklist, StringComparer.OrdinalIgnoreCase);
        target.PreviousNumbers = new List<string>(source.PreviousNumbers);
        if (!target.PreviousNumbers.Contains(source.EffectiveDisplayNumber, StringComparer.OrdinalIgnoreCase))
        {
            target.PreviousNumbers.Add(source.EffectiveDisplayNumber);
        }
    }

    private static SurfaceRecordModel CreateRecordFromSurface(SurfaceModel surface)
    {
        return new SurfaceRecordModel
        {
            StateId = surface.StateId,
            Checklist = new Dictionary<string, bool>(surface.Checklist, StringComparer.OrdinalIgnoreCase),
            Notes = surface.Notes,
            UpdatedAt = DateTime.UtcNow,
            Hidden = surface.IsHidden,
            DisplayNumber = surface.DisplayNumber ?? surface.SurfaceNumber,
            PreviousNumbers = new List<string>(surface.PreviousNumbers),
            GeometryFingerprint = surface.GeometryFingerprint ?? GeometryFingerprinter.CalculateFingerprint(surface)
        };
    }

    private static void RetireForTransfer(
        IDictionary<string, RetiredSurfaceRecordModel> retired,
        SurfaceModel surface,
        string? supersededBy,
        string transferType)
    {
        string key = GetAvailableRetiredKey(retired, surface.EffectiveDisplayNumber);
        retired[key] = new RetiredSurfaceRecordModel
        {
            RetiredAt = DateTime.UtcNow,
            SupersededBy = supersededBy,
            TransferType = transferType,
            FileKey = surface.SurfaceNumber,
            GeometryFingerprint = surface.GeometryFingerprint,
            Snapshot = CreateRecordFromSurface(surface),
            GeometrySnapshot = CloneSurface(surface)
        };
    }

    private static string GetAvailableRetiredKey(ProjectStateModel project, string preferredKey)
        => GetAvailableRetiredKey(project.Retired, preferredKey);

    private static string GetAvailableRetiredKey(
        IDictionary<string, RetiredSurfaceRecordModel> retired,
        string preferredKey)
    {
        if (!retired.ContainsKey(preferredKey)) return preferredKey;

        int suffix = 2;
        string candidate;
        do
        {
            candidate = $"{preferredKey} ({suffix++})";
        }
        while (retired.ContainsKey(candidate));

        return candidate;
    }
}

public class ReplaceSurfaceResult
{
    public bool Success { get; set; }
    public bool Renumbered { get; set; }
    public bool TrackingTransferred { get; set; }
    public bool RequiresRenumberConfirmation { get; set; }
    public bool IntrusionDetected { get; set; }
    public string? ErrorMessage { get; set; }
    public string OldSurfaceNumber { get; set; } = string.Empty;
    public string NewSurfaceNumber { get; set; } = string.Empty;
    public List<GeometryIntrusionFlagModel> IntrusionFlags { get; set; } = new();
}

