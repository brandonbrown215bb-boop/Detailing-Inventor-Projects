using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using QuestBoard.UI.ViewModels;

namespace QuestBoard.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }
        public ICommand SelectQuestCommand { get; }

        public MainWindow()
        {
            InitializeComponent();

            string repoRoot = FindRepoRoot();
            ViewModel = new MainViewModel(repoRoot);
            DataContext = ViewModel;

            SelectQuestCommand = new RelayCommand(param =>
            {
                if (param is QuestCardViewModel card)
                {
                    ViewModel.SelectedQuest = card;
                }
            });

            Loaded += (s, e) =>
            {
                try
                {
                    Activate();
                    Focus();
                    Dispatcher.BeginInvoke(new Action(() => LoadBackgroundImage(repoRoot)), System.Windows.Threading.DispatcherPriority.Background);
                }
                catch { }
            };
        }

        private static string FindRepoRoot()
        {
            var candidates = new[]
            {
                Environment.CurrentDirectory,
                AppDomain.CurrentDomain.BaseDirectory,
                AppContext.BaseDirectory,
                Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "")
            };

            foreach (var startDir in candidates)
            {
                if (string.IsNullOrEmpty(startDir)) continue;
                string? current = startDir;
                while (!string.IsNullOrEmpty(current))
                {
                    if (Directory.Exists(Path.Combine(current, ".questboard")))
                    {
                        return current;
                    }
                    var parent = Directory.GetParent(current);
                    if (parent == null) break;
                    current = parent.FullName;
                }
            }

            return @"c:\Users\jbrow263\OneDrive - Johnson Controls\Documents\Inventor Projects";
        }

        private void LoadBackgroundImage(string repoRoot)
        {
            try
            {
                string imgPath = Path.Combine(repoRoot, ".questboard", "Questboard Template.jpg");
                if (File.Exists(imgPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imgPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    BoardBackgroundImage.Source = bitmap;
                }
            }
            catch { }
        }

        private void FilterTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && btn.Tag is string filter)
            {
                ViewModel.SelectedFilter = filter;
            }
        }

        private void PostQuest_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsCreateOpen = true;
        }

        private void AudioToggle_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsAudioMuted = !ViewModel.IsAudioMuted;
            AudioToggleButton.Content = ViewModel.IsAudioMuted ? "🔇" : "🔊";
        }

        private void QuestCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is QuestCardViewModel card)
            {
                ViewModel.SelectedQuest = card;
            }
        }

        private void CorkboardContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ViewModel == null) return;

            // Slot size per card (width 200 + margin 10 = 210, height 150 + margin 10 = 160)
            double cardSlotWidth = 210.0;
            double cardSlotHeight = 160.0;

            // Available height for cards area (subtracting bottom pagination bar height ~36px and container safety padding ~44px)
            double availableHeight = Math.Max(100.0, e.NewSize.Height - 80.0);
            double availableWidth = Math.Max(100.0, e.NewSize.Width - 40.0);

            int cols = Math.Max(1, (int)Math.Floor(availableWidth / cardSlotWidth));
            int rows = Math.Max(1, (int)Math.Floor(availableHeight / cardSlotHeight));

            ViewModel.SetPageCapacity(cols, rows);
        }
    }
}
