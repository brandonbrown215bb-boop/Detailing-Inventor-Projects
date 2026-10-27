using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Persistence
{
    /// <summary>
    /// Every number each surface has had, in a sidecar next to the unit IAM: <c>&lt;IamDirectory&gt;\.ucv_surface_history.json</c>.
    /// </summary>
    public static class SurfaceHistoryStore
    {
        public const string SidecarFileName = ".ucv_surface_history.json";

        public static string GetSidecarPath(string unitIamPath) =>
            Path.Combine(Path.GetDirectoryName(unitIamPath) ?? string.Empty, SidecarFileName);

        public static List<SurfaceHistory> Load(string unitIamPath)
        {
            string path = GetSidecarPath(unitIamPath);
            if (!File.Exists(path)) return new List<SurfaceHistory>();

            try
            {
                return JsonConvert.DeserializeObject<List<SurfaceHistory>>(File.ReadAllText(path)) ?? new List<SurfaceHistory>();
            }
            catch (Exception ex)
            {
                // Never overwrite a history UCV cannot read: keep a copy before a later save replaces it.
                DebugLogger.Log(ex, $"SurfaceHistoryStore.Load: {path}");
                try { File.Copy(path, path + ".unreadable", overwrite: true); } catch { }
                return new List<SurfaceHistory>();
            }
        }

        public static void Save(string unitIamPath, List<SurfaceHistory> histories) =>
            File.WriteAllText(GetSidecarPath(unitIamPath), JsonConvert.SerializeObject(histories, Formatting.Indented));

        public static void RecordRenames(string unitIamPath, IReadOnlyDictionary<string, string> renames)
        {
            if (renames.Count == 0) return;
            List<SurfaceHistory> all = Load(unitIamPath);
            SurfaceHistoryRules.AddRenames(all, renames, RenamedAt, DateTime.UtcNow);
            Save(unitIamPath, all);
        }

        public static void RecordUploads(string unitIamPath, IEnumerable<string> uploadedPaths)
        {
            List<SurfaceHistory> all = Load(unitIamPath);
            SurfaceHistoryRules.AddUploads(all, uploadedPaths, DateTime.UtcNow);
            Save(unitIamPath, all);
        }

        /// <summary>YCShell writes the renamed surface during the upload, so its file's creation time is when it happened.</summary>
        private static DateTime RenamedAt(string newIamPath)
        {
            try { return File.Exists(newIamPath) ? File.GetCreationTimeUtc(newIamPath) : DateTime.UtcNow; }
            catch { return DateTime.UtcNow; }
        }
    }
}
