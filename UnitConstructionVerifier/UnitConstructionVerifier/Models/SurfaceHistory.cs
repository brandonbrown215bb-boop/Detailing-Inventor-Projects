using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace UnitConstructionVerifier.Models
{
    // ─────────────────────────────────────────────────────────────────────────
    // Surface number history: every number a surface has had, so a detailer can
    // follow 0021 -> 0048 -> 0049 -> 0122 across uploads (product owner, 2026-10-23).
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class SurfaceHistoryEntry
    {
        public string PartNumber { get; set; } = string.Empty;
        public string IamPath    { get; set; } = string.Empty;
        public DateTime AtUtc    { get; set; }
        public string Event      { get; set; } = string.Empty;   // "first seen", "renamed by MOM", "uploaded"
    }

    public sealed class SurfaceHistory
    {
        public const string FirstSeen = "first seen";
        public const string Renamed = "renamed by MOM";
        public const string Uploaded = "uploaded";

        public List<SurfaceHistoryEntry> Entries { get; set; } = new();

        public SurfaceHistoryEntry? Current => Entries.LastOrDefault();

        public bool WasRenamed => Entries.Any(e => e.Event == Renamed);

        /// <summary>The numbers in order, e.g. "391Z010216-0021 → 0048 → 0049".</summary>
        public string NumberChain()
        {
            List<string> numbers = Entries.Select(e => e.PartNumber).Where(n => n.Length > 0).ToList();
            var chain = new List<string>();
            foreach (string n in numbers)
            {
                if (chain.Count > 0 && string.Equals(chain[chain.Count - 1], n, StringComparison.OrdinalIgnoreCase)) continue;
                chain.Add(n);
            }

            return chain.Count == 0 ? string.Empty
                : chain[0] + string.Concat(chain.Skip(1).Select(n => " → " + ShortAfter(chain[0], n)));
        }

        /// <summary>One line per event, oldest first.</summary>
        public IEnumerable<string> Lines() =>
            Entries.Select(e => $"{e.AtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}  {e.PartNumber,-18} {e.Event}");

        /// <summary>"391Z010216-0048" after "391Z010216-0021" shows as "0048" when only the sequence changed.</summary>
        private static string ShortAfter(string first, string next)
        {
            int dash = first.LastIndexOf('-');
            return dash > 0 && next.Length > dash && string.Equals(first.Substring(0, dash + 1), next.Substring(0, dash + 1), StringComparison.OrdinalIgnoreCase)
                ? next.Substring(dash + 1)
                : next;
        }
    }

    public static class SurfaceHistoryRules
    {
        private static string Number(string iamPath) => Path.GetFileNameWithoutExtension(iamPath);

        private static SurfaceHistory? Find(List<SurfaceHistory> all, string iamPath) =>
            all.LastOrDefault(h => string.Equals(h.Current?.IamPath, iamPath.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>Appends each rename to the surface's chain, starting a chain for a surface seen for the first time.</summary>
        public static void AddRenames(List<SurfaceHistory> all, IReadOnlyDictionary<string, string> renames, Func<string, DateTime> renamedAtUtc,
            DateTime nowUtc)
        {
            foreach (KeyValuePair<string, string> rename in renames)
            {
                SurfaceHistory? history = Find(all, rename.Key);
                if (history is null)
                {
                    history = new SurfaceHistory();
                    history.Entries.Add(new SurfaceHistoryEntry { PartNumber = Number(rename.Key), IamPath = rename.Key, AtUtc = nowUtc, Event = SurfaceHistory.FirstSeen });
                    all.Add(history);
                }

                history.Entries.Add(new SurfaceHistoryEntry
                {
                    PartNumber = Number(rename.Value), IamPath = rename.Value, AtUtc = renamedAtUtc(rename.Value), Event = SurfaceHistory.Renamed,
                });
            }
        }

        /// <summary>
        /// Notes an upload of each surface (by its path after any rename). A surface just renamed by this upload already
        /// has its entry, so it is not noted twice.
        /// </summary>
        public static void AddUploads(List<SurfaceHistory> all, IEnumerable<string> uploadedPaths, DateTime atUtc)
        {
            foreach (string path in uploadedPaths)
            {
                SurfaceHistory? history = Find(all, path);
                if (history is null)
                {
                    history = new SurfaceHistory();
                    all.Add(history);
                }
                else if (history.Current is { Event: SurfaceHistory.Renamed } last && Math.Abs((atUtc - last.AtUtc).TotalMinutes) < 30)
                {
                    continue;
                }

                history.Entries.Add(new SurfaceHistoryEntry { PartNumber = Number(path), IamPath = path.Trim(), AtUtc = atUtc, Event = SurfaceHistory.Uploaded });
            }
        }

        /// <summary>The history list as text, renamed surfaces first.</summary>
        public static string Report(IReadOnlyList<SurfaceHistory> all)
        {
            if (all.Count == 0) return "No surface history yet. It starts with the first upload or renamed surface UCV sees.";

            var lines = new List<string>();
            foreach (SurfaceHistory history in all.OrderByDescending(h => h.WasRenamed).ThenBy(h => h.Current?.PartNumber, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add(history.NumberChain());
                lines.AddRange(history.Lines().Select(l => "   " + l));
                lines.Add(string.Empty);
            }

            return string.Join(Environment.NewLine, lines).TrimEnd();
        }
    }
}
