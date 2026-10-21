using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnitConstructionVerifier.Extraction;
using UnitConstructionVerifier.Models;
using UnitConstructionVerifier.Persistence;

namespace UnitConstructionVerifier.Scan
{
    /// <summary>
    /// ucv-scan: reads unit folders headlessly (read-only, through Apprentice), writes a
    /// <c>.unit_snapshot.json</c> and <c>.outcome.json</c> per unit plus <c>summary.csv</c>.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var roots = new List<string>();
            string outDir = Path.Combine(Environment.CurrentDirectory, "ucv-scan-out");
            string? replayDir = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--out" && i + 1 < args.Length) outDir = args[++i];
                else if (args[i] == "--replay" && i + 1 < args.Length) replayDir = args[++i];
                else if (args[i] == "-h" || args[i] == "--help") return Usage();
                else roots.Add(args[i]);
            }
            if (roots.Count == 0 && replayDir == null) return Usage();

            MaterialsConfig.Initialize();
            foreach (var problem in MaterialsConfig.RuleProblems)
                Console.Error.WriteLine($"[config] {problem}");
            Directory.CreateDirectory(outDir);

            if (replayDir != null) return Replay(replayDir, outDir);

            var iams = roots
                .SelectMany(root => Directory.EnumerateFiles(root, "*.iam", SearchOption.AllDirectories))
                .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(seg => seg.Equals("OldVersions", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Console.Error.WriteLine($"{iams.Count} IAMs under {string.Join(", ", roots)}");

            // A unit is the folder that holds the surface folders (a skid or unit folder).
            var units = new SortedDictionary<string, UnitSnapshot>(StringComparer.OrdinalIgnoreCase);
            int done = 0, failed = 0;
            using (var reader = new ApprenticeReader())
            {
                foreach (var iam in iams)
                {
                    if (++done % 50 == 0) Console.Error.WriteLine($"  {done}/{iams.Count}");
                    try
                    {
                        var surface = reader.ReadSurface(iam);
                        if (surface == null) continue;

                        string unitKey = Path.GetDirectoryName(Path.GetDirectoryName(iam)) ?? iam;
                        if (!units.TryGetValue(unitKey, out var snapshot))
                        {
                            snapshot = new UnitSnapshot
                            {
                                SourceIamPath = unitKey,
                                CapturedAtUtc = DateTime.UtcNow,
                                AddinVersion = "ucv-scan " + typeof(MaterialsConfig).Assembly.GetName().Version,
                                Engineering = new EngineeringData(),
                            };
                            units[unitKey] = snapshot;
                        }
                        snapshot.Configs.Add(surface.Config);
                        snapshot.Ipt.Parts.AddRange(surface.Parts);
                        if (surface.Engineering != null) EngineeringJsonExtractor.Merge(snapshot.Engineering!, surface.Engineering);
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Console.Error.WriteLine($"FAIL {iam}: {ex.Message}");
                    }
                }
            }

            var summary = new StringBuilder(SummaryHeader);
            foreach (var kv in units)
            {
                string name = FileStem(kv.Key, roots);
                try
                {
                    SnapshotSerializer.Save(Path.Combine(outDir, name + SnapshotSerializer.FileSuffix), kv.Value);
                    WriteOutcome(kv.Value, Path.Combine(outDir, name), summary);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.Error.WriteLine($"FAIL writing {kv.Key}: {ex.Message}");
                }
            }
            File.WriteAllText(Path.Combine(outDir, "summary.csv"), summary.ToString());

            Console.Error.WriteLine($"{units.Count} units written to {outDir}; {failed} failures");
            return failed == 0 ? 0 : 2;
        }

        private const string SummaryHeader = "unit,surfaces,parts,pass,mismatch,not_checked,not_in_config,display_only,modified\n";

        /// <summary>Re-runs the current builder and rules over saved snapshots (no Inventor files are opened).</summary>
        private static int Replay(string snapshotDir, string outDir)
        {
            var summary = new StringBuilder(SummaryHeader);
            int count = 0, failed = 0;
            foreach (var file in Directory.GetFiles(snapshotDir, "*" + SnapshotSerializer.FileSuffix).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string stem = Path.GetFileName(file).Substring(0, Path.GetFileName(file).Length - SnapshotSerializer.FileSuffix.Length);
                try
                {
                    WriteOutcome(SnapshotSerializer.Load(file), Path.Combine(outDir, stem), summary);
                    count++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.Error.WriteLine($"FAIL replaying {file}: {ex.Message}");
                }
            }
            File.WriteAllText(Path.Combine(outDir, "summary.csv"), summary.ToString());
            Console.Error.WriteLine($"{count} snapshots replayed into {outDir}; {failed} failures");
            return failed == 0 ? 0 : 2;
        }

        private static void WriteOutcome(UnitSnapshot snapshot, string pathStem, StringBuilder summary)
        {
            var outcome = UnitSnapshotOutcome.From(snapshot);
            File.WriteAllText(pathStem + ".outcome.json", SnapshotSerializer.Serialize(outcome));

            int Count(PartCheckStatus s) => outcome.Checks.Count(c => c.Status == s);
            summary.AppendLine(string.Join(",", Csv(snapshot.SourceIamPath), snapshot.Configs.Count, outcome.Checks.Count,
                Count(PartCheckStatus.Pass), Count(PartCheckStatus.Mismatch), Count(PartCheckStatus.NotChecked),
                Count(PartCheckStatus.NotInConfig), Count(PartCheckStatus.DisplayOnly), outcome.Checks.Count(c => c.IsModified)));
        }

        private static int Usage()
        {
            Console.Error.WriteLine("Usage: ucv-scan <folder> [<folder> ...] [--out <dir>]");
            Console.Error.WriteLine("       ucv-scan --replay <snapshot dir> [--out <dir>]");
            Console.Error.WriteLine("Reads every surface IAM under the folders (read-only) and writes one snapshot,");
            Console.Error.WriteLine("one outcome and a summary.csv row per unit folder. --replay re-runs the current");
            Console.Error.WriteLine("rules over saved snapshots without opening any Inventor files.");
            return 1;
        }

        /// <summary>Short, filesystem-safe name relative to the scanned root.</summary>
        private static string FileStem(string unitPath, List<string> roots)
        {
            string rel = unitPath;
            foreach (var root in roots)
            {
                string full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
                string parent = Path.GetDirectoryName(full) ?? full;
                if (unitPath.StartsWith(parent, StringComparison.OrdinalIgnoreCase))
                {
                    rel = unitPath.Substring(parent.Length).TrimStart(Path.DirectorySeparatorChar);
                    break;
                }
            }

            var sb = new StringBuilder();
            foreach (char c in rel)
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '.' ? c : '_');
            string stem = sb.ToString();
            if (stem.Length <= 60) return stem;

            uint hash = 2166136261;
            foreach (char c in stem) hash = (hash ^ c) * 16777619;
            return stem.Substring(0, 50) + "_" + hash.ToString("x8", CultureInfo.InvariantCulture);
        }

        private static string Csv(string value) => value.Contains(",") || value.Contains("\"") ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
