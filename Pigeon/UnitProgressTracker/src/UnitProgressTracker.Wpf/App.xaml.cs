using System.Windows;
using UnitProgressTracker.Core.Models;
using UnitProgressTracker.Core.Services;
using UnitProgressTracker.Wpf.Services;

namespace UnitProgressTracker.Wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ThemeManager.Instance.Initialize(AppSettingsService.LoadSettings().ThemeOptions);
        }
    }
}
