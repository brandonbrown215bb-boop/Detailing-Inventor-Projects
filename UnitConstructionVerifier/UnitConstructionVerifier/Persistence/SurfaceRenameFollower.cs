using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Persistence
{
    /// <summary>
    /// Moves UCV's per-surface records (saved expectations, channel fingerprints) to surfaces MOM renamed on upload,
    /// using the surface paths the records themselves hold. Runs when the verifier opens, before saved expectations
    /// are merged into freshly built rows, and on Re-check.
    /// </summary>
    public static class SurfaceRenameFollower
    {
        /// <param name="knownPaths">Surface paths the caller holds too (the open verifier's rows).</param>
        /// <param name="unitReferences">Assembly paths the unit references now.</param>
        public static Dictionary<string, string> Follow(string unitIamPath, IEnumerable<string> knownPaths, IEnumerable<string> unitReferences)
        {
            List<string> paths = knownPaths
                .Concat(SavedSurfacePaths(unitIamPath))
                .Concat(ChannelFingerprintStore.Load(unitIamPath).Keys)
                .ToList();

            Dictionary<string, string> renames = SurfaceRenames.Find(paths, File.Exists, unitReferences);
            if (renames.Count == 0) return renames;

            PersistenceManager.RenameSurfaces(unitIamPath, renames);
            ChannelFingerprintStore.RenameSurfaces(unitIamPath, renames);
            DebugLogger.Log("Followed renamed surfaces: " + string.Join(", ", renames.Select(r => $"{Path.GetFileName(r.Key)} -> {Path.GetFileName(r.Value)}")));
            return renames;
        }

        private static IEnumerable<string> SavedSurfacePaths(string unitIamPath)
        {
            string path = PersistenceManager.GetSidecarPath(unitIamPath);
            if (!File.Exists(path)) return Array.Empty<string>();

            try
            {
                var saved = JsonConvert.DeserializeObject<UnitConstructionData>(File.ReadAllText(path, System.Text.Encoding.UTF8));
                if (saved is null) return Array.Empty<string>();
                return saved.RoofRows.Select(r => r.SourceSurfaceIam)
                    .Concat(saved.WallRows.Select(r => r.SourceSurfaceIam))
                    .Concat(saved.BaseRows.Select(r => r.SourceSurfaceIam))
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();
            }
            catch (Exception ex)
            {
                DebugLogger.Log(ex, $"SurfaceRenameFollower: {path}");
                return Array.Empty<string>();
            }
        }
    }
}
