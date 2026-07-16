using System;
using System.IO;

namespace VisTog
{
    internal static class StartupLog
    {
        private static readonly object Sync = new object();
        private static string _path;

        private static string Path
        {
            get
            {
                if (_path == null)
                {
                    _path = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                        "Autodesk",
                        "Inventor 2020",
                        "Addins",
                        "VisTog-startup.log");
                }

                return _path;
            }
        }

        public static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + System.Environment.NewLine;
                    string dir = System.IO.Path.GetDirectoryName(Path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.AppendAllText(Path, line);
                }
            }
            catch
            {
            }
        }
    }
}
