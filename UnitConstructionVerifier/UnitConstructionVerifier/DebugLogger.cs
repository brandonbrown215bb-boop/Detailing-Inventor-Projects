using System;
using System.IO;

namespace UnitConstructionVerifier
{
    internal static class DebugLogger
    {
        internal static string LogDirectory { get; } = Path.Combine(Path.GetTempPath(), "UnitConstructionVerifier");
        private static readonly string LogPath = Path.Combine(LogDirectory, "ucv_debug.txt");

        private static void EnsureDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
            }
            catch {}
        }

        internal static void Clear()
        {
            try
            {
                EnsureDirectoryExists();
                if (File.Exists(LogPath))
                {
                    File.Delete(LogPath);
                }
            }
            catch {}
        }

        internal static void Log(string message)
        {
            try
            {
                EnsureDirectoryExists();
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n";
                File.AppendAllText(LogPath, line);
            }
            catch {}
        }

        internal static void Log(Exception ex, string context)
        {
            try
            {
                EnsureDirectoryExists();
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR in {context}: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}\r\n";
                File.AppendAllText(LogPath, line);
            }
            catch {}
        }

        internal static void CleanUpOldLogs(int maxAgeDays = 3)
        {
            try
            {
                if (!Directory.Exists(LogDirectory)) return;

                var threshold = DateTime.Now.AddDays(-maxAgeDays);

                // Clean up raw JSON dumps
                foreach (var file in Directory.GetFiles(LogDirectory, "raw_*.json"))
                {
                    var fi = new FileInfo(file);
                    if (fi.LastWriteTime < threshold)
                    {
                        fi.Delete();
                    }
                }

                // Clean up old log file if it hasn't been modified recently
                if (File.Exists(LogPath))
                {
                    var fi = new FileInfo(LogPath);
                    if (fi.LastWriteTime < threshold)
                    {
                        fi.Delete();
                    }
                }
            }
            catch {}
        }
    }
}
