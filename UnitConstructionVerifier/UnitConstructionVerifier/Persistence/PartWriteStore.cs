using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnitConstructionVerifier.Models;

namespace UnitConstructionVerifier.Persistence
{
    /// <summary>
    /// UCV's record of the parts its gauge / material write-back saved, in a sidecar next to the unit IAM:
    /// <c>&lt;IamDirectory&gt;\.ucv_part_writes.json</c>. Keyed by part file path (case-insensitive).
    /// </summary>
    public static class PartWriteStore
    {
        public const string SidecarFileName = ".ucv_part_writes.json";

        public static string GetSidecarPath(string unitIamPath) =>
            Path.Combine(Path.GetDirectoryName(unitIamPath) ?? string.Empty, SidecarFileName);

        public static Dictionary<string, PartWriteRecord> Load(string unitIamPath)
        {
            var result = new Dictionary<string, PartWriteRecord>(StringComparer.OrdinalIgnoreCase);
            string path = GetSidecarPath(unitIamPath);
            if (!File.Exists(path)) return result;

            try
            {
                foreach (PartWriteRecord write in JsonConvert.DeserializeObject<List<PartWriteRecord>>(File.ReadAllText(path)) ?? new List<PartWriteRecord>())
                {
                    if (!string.IsNullOrWhiteSpace(write.PartFile)) result[write.PartFile.Trim()] = write;
                }
            }
            catch (Exception ex)
            {
                // A damaged record only means UCV-written channels read as "can't tell", which is ticked anyway.
                DebugLogger.Log(ex, $"PartWriteStore.Load: {path}");
            }

            return result;
        }

        /// <summary>Adds writes from one write-back run, folding each into the part's earlier record when it follows on.</summary>
        public static void Record(string unitIamPath, IEnumerable<PartWriteRecord> writes)
        {
            Dictionary<string, PartWriteRecord> all = Load(unitIamPath);
            foreach (PartWriteRecord write in writes)
            {
                string key = write.PartFile.Trim();
                all[key] = all.TryGetValue(key, out PartWriteRecord? earlier) ? earlier.FollowedBy(write) : write;
            }

            List<PartWriteRecord> ordered = all.Values.OrderBy(w => w.PartFile, StringComparer.OrdinalIgnoreCase).ToList();
            File.WriteAllText(GetSidecarPath(unitIamPath), JsonConvert.SerializeObject(ordered, Formatting.Indented));
        }
    }
}
