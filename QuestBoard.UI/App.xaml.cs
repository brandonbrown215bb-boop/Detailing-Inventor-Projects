using System;
using System.IO;
using System.Windows;
using QuestBoard.UI.Views;

namespace QuestBoard.UI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                LogCrash("AppDomain UnhandledException", args.ExceptionObject as Exception);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                LogCrash("DispatcherUnhandledException", args.Exception);
                args.Handled = false;
            };

            try
            {
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                LogCrash("OnStartup MainWindow Init", ex);
                MessageBox.Show($"Failed to initialize Quest Board UI:\n\n{ex.Message}", "Quest Board Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void LogCrash(string source, Exception? ex)
        {
            try
            {
                string msg = $"[{DateTime.Now}] {source}:\n{ex?.ToString()}\n";
                string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "questboard_crash.log");
                File.AppendAllText(desktopPath, msg);
            }
            catch { }
        }
    }
}
