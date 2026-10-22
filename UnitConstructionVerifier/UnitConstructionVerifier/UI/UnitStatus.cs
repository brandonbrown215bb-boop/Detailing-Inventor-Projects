using System;
using System.Collections.Generic;
using System.Linq;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.UI
{
    /// <summary>Check outcome counts for one surface IAM.</summary>
    public sealed class SurfaceStatus
    {
        public static readonly SurfaceStatus Unknown = new SurfaceStatus(0, 0, 0, scanned: false);

        public SurfaceStatus(int passed, int mismatches, int notChecked, bool scanned = true)
        {
            Passed = passed;
            Mismatches = mismatches;
            NotChecked = notChecked;
            Scanned = scanned;
        }

        public int Passed { get; }
        public int Mismatches { get; }
        public int NotChecked { get; }
        public bool Scanned { get; }

        public bool IsFail => Mismatches > 0;
        public bool IsPass => Scanned && Mismatches == 0 && Passed > 0;
        public bool ShowBadge => Mismatches > 0;
    }

    /// <summary>
    /// Whole-unit summary built from one <see cref="VerificationResult"/>: what the header pill, tab badges,
    /// surface dots and the re-check banner show.
    /// </summary>
    public sealed class UnitStatus
    {
        public static readonly UnitStatus Empty = new UnitStatus(new Dictionary<string, SurfaceStatus>(StringComparer.OrdinalIgnoreCase), 0, 0, 0, 0, scanned: false);

        private readonly Dictionary<string, SurfaceStatus> _bySurface;

        private UnitStatus(Dictionary<string, SurfaceStatus> bySurface, int passed, int mismatches, int notChecked, int modified, bool scanned)
        {
            _bySurface = bySurface;
            Passed = passed;
            Mismatches = mismatches;
            NotChecked = notChecked;
            Modified = modified;
            Scanned = scanned;
        }

        public int Passed { get; }
        public int Mismatches { get; }
        public int NotChecked { get; }
        public int Modified { get; }
        public bool Scanned { get; }
        public int Checked => Passed + Mismatches;
        public int SurfacesWithMismatches => _bySurface.Values.Count(s => s.Mismatches > 0);

        public static UnitStatus From(VerificationResult result)
        {
            var bySurface = new Dictionary<string, SurfaceStatus>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in result.Checks.GroupBy(c => c.SurfaceIamPath ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                bySurface[group.Key] = new SurfaceStatus(
                    group.Count(c => c.Status == PartCheckStatus.Pass),
                    group.Count(c => c.Status == PartCheckStatus.Mismatch),
                    group.Count(c => c.Status == PartCheckStatus.NotChecked));
            }

            return new UnitStatus(
                bySurface,
                result.CountOf(PartCheckStatus.Pass),
                result.CountOf(PartCheckStatus.Mismatch),
                result.CountOf(PartCheckStatus.NotChecked),
                result.Checks.Count(c => c.IsModified),
                scanned: true);
        }

        /// <summary>Counts for a surface IAM. A scanned surface with no covered parts reports zeros.</summary>
        public SurfaceStatus For(string? surfaceIamPath)
        {
            if (!Scanned) return SurfaceStatus.Unknown;
            return _bySurface.TryGetValue(surfaceIamPath ?? string.Empty, out var status)
                ? status
                : new SurfaceStatus(0, 0, 0);
        }

        /// <summary>Mismatch total across the given surfaces (for a tab badge).</summary>
        public int MismatchesIn(IEnumerable<string> surfaceIamPaths) => surfaceIamPaths.Sum(p => For(p).Mismatches);

        /// <summary>Short text for the header pill.</summary>
        public string HeaderText()
        {
            if (!Scanned) return "Not scanned";
            if (Mismatches == 0) return Checked == 0 ? "No parts checked" : $"✓ All {Checked} checked parts match";
            int surfaces = SurfacesWithMismatches;
            return $"✗ {Plural(Mismatches, "mismatch", "mismatches")} on {Plural(surfaces, "surface", "surfaces")}";
        }

        /// <summary>One-line summary shown after Re-check unit.</summary>
        public string BannerText()
        {
            if (!Scanned) return "The assembly scan is not available, so nothing could be checked.";

            var parts = new List<string> { $"Re-checked {Plural(Checked, "part", "parts")}" };
            parts.Add(Mismatches == 0
                ? "no mismatches"
                : $"{Plural(Mismatches, "mismatch", "mismatches")} on {Plural(SurfacesWithMismatches, "surface", "surfaces")}");
            if (NotChecked > 0) parts.Add($"{NotChecked} not checked");
            if (Modified > 0) parts.Add($"{Modified} modified after generation");
            return string.Join(" · ", parts) + ".";
        }

        private static string Plural(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";
    }
}
