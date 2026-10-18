using System.IO;

namespace FileGrid.Portable.Services;

public static class AppPaths
{
    public static string DataRoot
    {
        get
        {
            var exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var portableData = Path.Combine(exeDir, "data");
            // Portable: store next to the exe when data folder exists or portable.txt is present
            if (File.Exists(Path.Combine(exeDir, "portable.txt")) || Directory.Exists(portableData))
                return portableData;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileGridPortable");
        }
    }

    public static string LayoutsDir => Path.Combine(DataRoot, "layouts");
    public static string SettingsFile => Path.Combine(DataRoot, "settings.json");
    public static string SessionFile => Path.Combine(DataRoot, "session.json");

    public static void EnsureDataDirs()
    {
        Directory.CreateDirectory(LayoutsDir);
    }
}
