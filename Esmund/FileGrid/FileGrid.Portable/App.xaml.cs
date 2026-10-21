using System.IO;
using System.Windows;
using System.Windows.Threading;
using FileGrid.Portable.Services;

namespace FileGrid.Portable;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogCrash("AppDomain", args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash("UI", args.Exception);
            MessageBox.Show(
                $"File Grid encountered an error:\n\n{args.Exception.Message}",
                "File Grid",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
        base.OnStartup(e);
    }

    private static void LogCrash(string source, Exception? ex)
    {
        if (ex == null) return;
        try
        {
            var path = Path.Combine(AppPaths.DataRoot, "crash.log");
            Directory.CreateDirectory(AppPaths.DataRoot);
            File.AppendAllText(path, $"[{DateTime.Now:u}] {source}: {ex}\n\n");
        }
        catch { /* ignore */ }
    }
}
