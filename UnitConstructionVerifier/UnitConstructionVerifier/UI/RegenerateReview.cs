using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.UI
{
    /// <summary>One surface in the Regenerate changed review. Surfaces that need it are ticked.</summary>
    public sealed class RegenerateReviewItem : INotifyPropertyChanged
    {
        private bool _isIncluded;

        public RegenerateReviewItem(string surfaceIamPath, SurfaceDecision decision, bool canRun, string cannotRunReason)
        {
            SurfaceIamPath = surfaceIamPath;
            Decision = decision;
            CanRun = canRun;
            CannotRunReason = cannotRunReason;
            _isIncluded = canRun && decision.Needed;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string SurfaceIamPath { get; }
        public SurfaceDecision Decision { get; }
        public bool CanRun { get; }
        public string CannotRunReason { get; }

        public string SurfaceName => Path.GetFileNameWithoutExtension(SurfaceIamPath);
        public bool Needed => Decision.Needed;

        /// <summary>Why: the rule's reasons, or why it can't run.</summary>
        public string ReasonText => !CanRun ? CannotRunReason : Decision.ToString();

        public string Tooltip => SurfaceIamPath + Environment.NewLine + string.Join(Environment.NewLine, Decision.Reasons);

        /// <summary>Ticked only because of surface-wide or can't-tell flags; may not be channels.</summary>
        public bool IsUncertain => Decision.Needed && !Decision.ChannelsCertain;

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
        public static List<RegenerateReviewItem> BuildItems(
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
                    return new RegenerateReviewItem(s.SurfaceIamPath, decision, canRun, "not loaded by YCShell");
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

        public static string Summary(IReadOnlyList<RegenerateReviewItem> items)
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
}
