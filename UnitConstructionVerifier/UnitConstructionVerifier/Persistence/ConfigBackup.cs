using System;
using System.IO;

namespace UnitConstructionVerifier.Persistence
{
    /// <summary>
    /// Copies of surface configs as they were before UCV changed them, beside the surface IAM:
    /// <c>&lt;surface folder&gt;\.ucv_config_backups\&lt;IAM name&gt;_&lt;yyyyMMdd-HHmmss&gt;.json</c>.
    /// </summary>
    public static class ConfigBackup
    {
        public const string FolderName = ".ucv_config_backups";

        /// <summary>Writes the backup and returns its path. Throws if it cannot, so nothing is changed without one.</summary>
        public static string Save(string surfaceIamPath, string configJson, DateTime now)
        {
            string folder = Path.Combine(Path.GetDirectoryName(surfaceIamPath) ?? string.Empty, FolderName);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(surfaceIamPath)}_{now:yyyyMMdd-HHmmss}.json");
            File.WriteAllText(path, configJson);
            return path;
        }
    }
}
