using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.UI
{
    /// <summary>One surface in the Regenerate / Upload changed review. Surfaces that need the batch are ticked.</summary>
    public sealed class SurfaceReviewItem : INotifyPropertyChanged
    {
        private bool _isIncluded;

        public SurfaceReviewItem(string surfaceIamPath, SurfaceDecision decision, bool canRun, string cannotRunReason, string warning = "")
        {
            SurfaceIamPath = surfaceIamPath;
            Decision = decision;
            CanRun = canRun;
            CannotRunReason = cannotRunReason;
            Warning = warning;
            _isIncluded = canRun && decision.Needed;
        }

        /// <summary>Something to look at before running, such as "still needs Regenerate" on an upload.</summary>
        public string Warning { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string SurfaceIamPath { get; }
        public SurfaceDecision Decision { get; }
        public bool CanRun { get; }
        public string CannotRunReason { get; }

        public string SurfaceName => Path.GetFileNameWithoutExtension(SurfaceIamPath);
        public bool Needed => Decision.Needed;

        /// <summary>Why: the rule's reasons, or why it can't run.</summary>
        public string ReasonText => !CanRun ? CannotRunReason
            : Warning.Length > 0 && Decision.Needed ? $"{Warning}  ·  {Decision}"
            : Decision.ToString();

        public string Tooltip => SurfaceIamPath + Environment.NewLine + string.Join(Environment.NewLine, Decision.Reasons);

        /// <summary>Ticked only because of surface-wide or can't-tell flags; may not be channels.</summary>
        public bool IsUncertain => Decision.Needed && !Decision.ChannelsCertain;

        /// <summary>Shown in orange: uncertain, or carrying a warning.</summary>
        public bool NeedsAttention => IsUncertain || (Decision.Needed && Warning.Length > 0);

        public bool IsIncluded
        {
            get => _isIncluded;
            set
            {
                bool next = value && CanRun;
                if (next == _isIncluded) return;
                _isIncluded = next;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIncluded)));
            }
        }
    }

    public static class RegenerateReview
    {
        /// <summary>
        /// Every scanned surface with its Regenerate decision, needed ones first. <paramref name="loadedInYcShell"/> says
        /// whether YCShell can run a surface; null when YCShell has not been reached yet.
        /// </summary>
        public static List<SurfaceReviewItem> BuildItems(
            IEnumerable<SurfaceChangeState> surfaces,
            IReadOnlyDictionary<string, ChannelFingerprint> fingerprints,
            IReadOnlyDictionary<string, PartWriteRecord> partWrites,
            Func<string, bool>? loadedInYcShell)
        {
            return surfaces
                .GroupBy(s => s.SurfaceIamPath.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Select(s =>
                {
                    fingerprints.TryGetValue(s.SurfaceIamPath.Trim(), out ChannelFingerprint? fingerprint);
                    SurfaceDecision decision = SurfaceChangeRules.Regenerate(s, fingerprint, partWrites);
                    bool canRun = loadedInYcShell?.Invoke(s.SurfaceIamPath) ?? true;
                    return new SurfaceReviewItem(s.SurfaceIamPath, decision, canRun, "not loaded by YCShell");
                })
                .OrderByDescending(i => i.Needed)
                .ThenBy(i => i.SurfaceName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static int CountNeeded(
            IEnumerable<SurfaceChangeState> surfaces,
            IReadOnlyDictionary<string, ChannelFingerprint> fingerprints,
            IReadOnlyDictionary<string, PartWriteRecord> partWrites) =>
            BuildItems(surfaces, fingerprints, partWrites, null).Count(i => i.Needed);

        public static string Summary(IReadOnlyList<SurfaceReviewItem> items)
        {
            int ticked = items.Count(i => i.IsIncluded);
            int needed = items.Count(i => i.Needed);
            int uncertain = items.Count(i => i.IsIncluded && i.IsUncertain);
            int unavailable = items.Count(i => i.Needed && !i.CanRun);
            var parts = new List<string> { $"{needed} of {items.Count} surfaces have channel changes; {ticked} ticked." };
            if (uncertain > 0) parts.Add($"{uncertain} ticked only because YCShell flagged moved, deleted or UCV-edited parts (may not be channels).");
            if (unavailable > 0) parts.Add($"{unavailable} can't run: not loaded by YCShell.");
            return string.Join(" ", parts);
        }
    }

    public static class UploadReview
    {
        public const string NeedsRegenerate = "still needs Regenerate";

        /// <summary>
        /// Every scanned surface with its Upload decision, needed ones first. Surfaces whose channels still need
        /// Regenerate carry a warning; they stay ticked, as uploading is the detailer's call.
        /// </summary>
        public static List<SurfaceReviewItem> BuildItems(
            IEnumerable<SurfaceChangeState> surfaces,
            IReadOnlyDictionary<string, ChannelFingerprint> fingerprints,
            IReadOnlyDictionary<string, PartWriteRecord> partWrites,
            Func<string, bool>? loadedInYcShell)
        {
            return surfaces
                .GroupBy(s => s.SurfaceIamPath.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Select(s =>
                {
                    fingerprints.TryGetValue(s.SurfaceIamPath.Trim(), out ChannelFingerprint? fingerprint);
                    bool needsRegenerate = SurfaceChangeRules.Regenerate(s, fingerprint, partWrites).Needed;
                    return new SurfaceReviewItem(s.SurfaceIamPath, SurfaceChangeRules.Upload(s),
                        loadedInYcShell?.Invoke(s.SurfaceIamPath) ?? true, "not loaded by YCShell",
                        needsRegenerate ? NeedsRegenerate : string.Empty);
                })
                .OrderByDescending(i => i.Needed)
                .ThenBy(i => i.SurfaceName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static int CountNeeded(IEnumerable<SurfaceChangeState> surfaces) =>
            surfaces.GroupBy(s => s.SurfaceIamPath.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Count(g => SurfaceChangeRules.Upload(g.First()).Needed);

        public static string Summary(IReadOnlyList<SurfaceReviewItem> items, string? unitName)
        {
            int ticked = items.Count(i => i.IsIncluded);
            int needed = items.Count(i => i.Needed);
            int regenerate = items.Count(i => i.IsIncluded && i.Warning == NeedsRegenerate);
            int unavailable = items.Count(i => i.Needed && !i.CanRun);
            var parts = new List<string> { $"{needed} of {items.Count} surfaces have changes since their last upload; {ticked} ticked." };
            if (regenerate > 0) parts.Add($"{regenerate} ticked {(regenerate == 1 ? "surface still has" : "surfaces still have")} channel changes: run Regenerate changed first.");
            if (unavailable > 0) parts.Add($"{unavailable} can't run: not loaded by YCShell.");
            if (unitName is not null) parts.Add($"{unitName} goes along, so a surface MOM renames is replaced in the unit.");
            return string.Join(" ", parts);
        }

        /// <summary>What happened to each surface sent to the upload window, read after it closed.</summary>
        public static string Result(IReadOnlyList<string> sent, Func<string, bool> fileExists, Func<string, SurfaceDecision?> uploadDecisionNow)
        {
            var uploaded = new List<string>();
            var stillChanged = new List<string>();
            var renamed = new List<string>();
            foreach (string path in sent)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (!fileExists(path)) renamed.Add(name);
                else if (uploadDecisionNow(path)?.Needed == true) stillChanged.Add($"{name}: {uploadDecisionNow(path)}");
                else uploaded.Add(name);
            }

            var lines = new List<string> { $"{uploaded.Count + renamed.Count} of {sent.Count} surfaces uploaded." };
            if (stillChanged.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add($"Still changed ({stillChanged.Count}): not uploaded, or the upload failed. Check YCShell's upload log.");
                lines.AddRange(stillChanged.Select(s => "   " + s));
            }
            if (renamed.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add($"Renamed by MOM ({renamed.Count}): the old file is gone. Check that the unit references the new surface.");
                lines.AddRange(renamed.Select(s => "   " + s));
            }
            if (uploaded.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add($"Uploaded ({uploaded.Count})");
                lines.Add("   " + string.Join(", ", uploaded));
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
