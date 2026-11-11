using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace UnitConstructionVerifier.Deployment
{
    /// <summary>
    /// Keeps a per-user install current from the team's release zip. When Inventor starts it copies a newer release
    /// into the add-in folder. Inventor keeps the running DLL locked, so the swap renames each old file to
    /// *.old_&lt;stamp&gt; (Windows allows renaming a loaded DLL) and the new version loads on the next Inventor start.
    /// </summary>
    public static class SelfUpdater
    {
        /// <summary>P:\Detailing\Detailing Tools, by server name so it works whatever letter P: is mapped to.</summary>
        public const string ReleaseZip =
            @"\\C4050MV010.go.johnsoncontrols.com\Public\Detailing\Detailing Tools\UnitConstructionVerifier_Dist.zip";

        /// <summary>Written into the zip by create_dist.bat: the DLL's file version.</summary>
        public const string VersionEntry = "version.txt";

        private const string MainDll = "UnitConstructionVerifier.dll";
        private static readonly string[] InstalledExtensions = { ".dll", ".addin", ".json" };

        /// <summary>The version copied in this Inventor session, waiting for a restart; null if none.</summary>
        public static Version? StagedVersion { get; private set; }

        /// <summary>Runs the check off Inventor's thread so a slow or unreachable share never delays start-up.</summary>
        public static void StartInBackground()
        {
            string? addinDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (addinDir == null) return;

            Task.Run(() =>
            {
                try
                {
                    RemoveOldCopies(addinDir);
                    StagedVersion = Stage(ReleaseZip, addinDir);
                    if (StagedVersion != null)
                        DebugLogger.Log($"Self-update: copied {StagedVersion} from {ReleaseZip}; it loads on the next Inventor start.");
                }
                catch (Exception ex)
                {
                    DebugLogger.Log(ex, "Self-update (the installed version is unchanged)");
                }
            });
        }

        /// <summary>
        /// Copies the release in <paramref name="zipPath"/> into <paramref name="addinDir"/> when it is newer than the
        /// DLL there. Returns the version copied, or null when there is nothing newer. Every file is extracted beside
        /// its target before any is replaced, so a dropped network leaves the install as it was; if a replace fails,
        /// the files already replaced are put back and the exception is rethrown.
        /// </summary>
        public static Version? Stage(string zipPath, string addinDir)
        {
            if (!File.Exists(zipPath)) return null;

            using var mutex = new Mutex(false, @"Local\UnitConstructionVerifier.SelfUpdate");
            if (!mutex.WaitOne(0)) return null; // another Inventor on this login is already updating
            try
            {
                using ZipArchive zip = ZipFile.OpenRead(zipPath);
                Version? offered = ReadVersion(zip);
                if (offered == null || offered <= InstalledVersion(addinDir)) return null;

                List<ZipArchiveEntry> files = zip.Entries
                    .Where(e => e.FullName == e.Name
                             && InstalledExtensions.Contains(Path.GetExtension(e.Name), StringComparer.OrdinalIgnoreCase))
                    .ToList();
                if (!files.Any(e => e.Name.Equals(MainDll, StringComparison.OrdinalIgnoreCase))) return null;

                var extracted = new List<string>();
                try
                {
                    foreach (ZipArchiveEntry entry in files)
                    {
                        string target = Path.Combine(addinDir, entry.Name);
                        extracted.Add(target);
                        entry.ExtractToFile(target + ".new", overwrite: true);
                    }
                    Swap(extracted, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                }
                finally
                {
                    foreach (string target in extracted) TryDelete(target + ".new");
                }
                return offered;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        /// <summary>The file version of the DLL in <paramref name="addinDir"/>; 0.0 if it is missing or unreadable.</summary>
        public static Version InstalledVersion(string addinDir)
        {
            string dll = Path.Combine(addinDir, MainDll);
            if (!File.Exists(dll)) return new Version(0, 0);
            return Version.TryParse(FileVersionInfo.GetVersionInfo(dll).FileVersion, out Version? v) ? v : new Version(0, 0);
        }

        /// <summary>Deletes *.old_* files left by earlier updates or install.bat. Ones still loaded stay until next time.</summary>
        public static void RemoveOldCopies(string addinDir)
        {
            foreach (string file in Directory.GetFiles(addinDir, "*.old_*")) TryDelete(file);
        }

        private static Version? ReadVersion(ZipArchive zip)
        {
            ZipArchiveEntry? entry = zip.GetEntry(VersionEntry);
            if (entry == null) return null;
            using var reader = new StreamReader(entry.Open());
            return Version.TryParse(reader.ReadToEnd().Trim(), out Version? v) ? v : null;
        }

        private static void Swap(List<string> targets, string stamp)
        {
            var done = new List<(string Target, string? Backup)>();
            try
            {
                foreach (string target in targets)
                {
                    string? backup = null;
                    if (File.Exists(target))
                    {
                        backup = target + ".old_" + stamp;
                        File.Move(target, backup);
                    }
                    done.Add((target, backup));
                    File.Move(target + ".new", target);
                }
            }
            catch
            {
                for (int i = done.Count - 1; i >= 0; i--)
                {
                    (string target, string? backup) = done[i];
                    TryDelete(target);
                    if (backup != null) File.Move(backup, target);
                }
                throw;
            }
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch { /* locked or gone: leave it */ }
        }
    }
}
