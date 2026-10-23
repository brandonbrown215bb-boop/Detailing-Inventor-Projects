using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnitConstructionVerifier.Models
{
    /// <summary>
    /// Follows surfaces MOM renamed on upload. YCShell saves the renamed surface as a new IAM in the same folder, deletes
    /// the old one and replaces it in the unit, so a surface row's IAM path (and its part number, the file name) go
    /// stale. Field test, 2026-10-23: after an 8-surface upload, Re-check unit no longer matched those surfaces.
    /// </summary>
    public static class SurfaceRenames
    {
        /// <summary>
        /// Old surface IAM path to new, for each surface whose file is gone and whose folder now holds exactly one other
        /// IAM the unit references (and no surface row claims).
        /// </summary>
        public static Dictionary<string, string> Find(IEnumerable<string> surfacePaths, Func<string, bool> fileExists,
            IEnumerable<string> unitReferences)
        {
            List<string> known = surfacePaths.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            List<string> references = unitReferences.Select(r => r.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string old in known.Where(p => !fileExists(p)))
            {
                string folder = Path.GetDirectoryName(old) ?? string.Empty;
                List<string> candidates = references
                    .Where(r => string.Equals(Path.GetDirectoryName(r), folder, StringComparison.OrdinalIgnoreCase))
                    .Where(r => !known.Contains(r, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (candidates.Count == 1) renames[old] = candidates[0];
            }

            return renames;
        }

        /// <summary>Points surface rows at their renamed IAMs. Returns how many rows changed.</summary>
        public static int Apply(UnitConstructionData data, IReadOnlyDictionary<string, string> renames)
        {
            int changed = 0;
            foreach (RoofSurfaceRow row in data.RoofRows)
                changed += Rename(row.SourceSurfaceIam, row.PartNumber, renames, (iam, pn) => { row.SourceSurfaceIam = iam; row.PartNumber = pn; });
            foreach (WallSurfaceRow row in data.WallRows)
                changed += Rename(row.SourceSurfaceIam, row.PartNumber, renames, (iam, pn) => { row.SourceSurfaceIam = iam; row.PartNumber = pn; });
            foreach (BaseSurfaceRow row in data.BaseRows)
                changed += Rename(row.SourceSurfaceIam, row.PartNumber, renames, (iam, pn) => { row.SourceSurfaceIam = iam; row.PartNumber = pn; });
            return changed;
        }

        /// <summary>Renames one row; its part number follows only when it was the old file name, as the builder sets it.</summary>
        private static int Rename(string? sourceIam, string partNumber, IReadOnlyDictionary<string, string> renames, Action<string, string> set)
        {
            string source = (sourceIam ?? string.Empty).Trim();
            if (!renames.TryGetValue(source, out string? renamed)) return 0;

            bool partNumberIsFileName = string.Equals(partNumber, Path.GetFileNameWithoutExtension(source), StringComparison.OrdinalIgnoreCase);
            set(renamed, partNumberIsFileName ? Path.GetFileNameWithoutExtension(renamed) : partNumber);
            return 1;
        }
    }
}
