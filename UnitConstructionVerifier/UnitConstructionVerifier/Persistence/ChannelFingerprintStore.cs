using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Persistence
{
    /// <summary>
    /// UCV's record of each surface's channels after UCV last regenerated it, in a sidecar next to the unit IAM:
    /// <c>&lt;IamDirectory&gt;\.ucv_channel_fingerprints.json</c>. Keyed by surface IAM path (case-insensitive).
    /// </summary>
    public static class ChannelFingerprintStore
    {
        public const string SidecarFileName = ".ucv_channel_fingerprints.json";

        public static string GetSidecarPath(string unitIamPath) =>
            Path.Combine(Path.GetDirectoryName(unitIamPath) ?? string.Empty, SidecarFileName);

        public static Dictionary<string, ChannelFingerprint> Load(string unitIamPath)
        {
            var result = new Dictionary<string, ChannelFingerprint>(StringComparer.OrdinalIgnoreCase);
            string path = GetSidecarPath(unitIamPath);
            if (!File.Exists(path)) return result;

            try
            {
                var saved = JsonConvert.DeserializeObject<List<ChannelFingerprint>>(File.ReadAllText(path));
                foreach (ChannelFingerprint fingerprint in saved ?? new List<ChannelFingerprint>())
                {
                    if (!string.IsNullOrWhiteSpace(fingerprint.SurfaceIamPath)) result[fingerprint.SurfaceIamPath.Trim()] = fingerprint;
                }
            }
            catch (Exception ex)
            {
                // A damaged record only means UCV falls back to YCShell's markers.
                DebugLogger.Log(ex, $"ChannelFingerprintStore.Load: {path}");
            }

            return result;
        }

        public static ChannelFingerprint? Find(string unitIamPath, string surfaceIamPath) =>
            Load(unitIamPath).TryGetValue(surfaceIamPath.Trim(), out ChannelFingerprint? fingerprint) ? fingerprint : null;

        /// <summary>Replaces the record for one surface; other surfaces are kept.</summary>
        public static void Record(string unitIamPath, ChannelFingerprint fingerprint)
        {
            Dictionary<string, ChannelFingerprint> all = Load(unitIamPath);
            all[fingerprint.SurfaceIamPath.Trim()] = fingerprint;

            var ordered = new List<ChannelFingerprint>(all.Values);
            ordered.Sort((a, b) => string.Compare(a.SurfaceIamPath, b.SurfaceIamPath, StringComparison.OrdinalIgnoreCase));
            File.WriteAllText(GetSidecarPath(unitIamPath), JsonConvert.SerializeObject(ordered, Formatting.Indented));
        }
    }
}
