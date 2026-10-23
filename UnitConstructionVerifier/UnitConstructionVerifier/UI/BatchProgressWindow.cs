using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace UnitConstructionVerifier.UI
{
    /// <summary>
    /// Small always-on-top progress window with a Stop button, on its own UI thread. YCShell's progress dialog is modal
    /// and disables every window on Inventor's thread, the verifier included, so Stop has to live elsewhere to stay
    /// clickable. Stop only takes effect between surfaces.
    /// </summary>
    internal sealed class BatchProgressWindow
    {
        private Window? _window;
        private TextBlock? _detail;
        private ProgressBar? _bar;
        private Button? _stop;
        private volatile bool _stopRequested;
        private bool _finished;

        public bool StopRequested => _stopRequested;

        public static BatchProgressWindow Open(string title)
        {
            var progress = new BatchProgressWindow();
            using var ready = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                progress.Build(title);
                progress._window!.Show();
                ready.Set();
                Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            ready.Wait();
            return progress;
        }

        public void Update(string detail, double fraction) =>
            _window?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_detail is not null) _detail.Text = detail;
                if (_bar is not null) _bar.Value = fraction;
            }));

        public void Close() =>
            _window?.Dispatcher.BeginInvoke(new Action(() =>
            {
                _finished = true;
                _window.Close();
                Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
            }));

        private void Build(string title)
        {
            _detail = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 10), Foreground = Brushes.DimGray };
            _bar = new ProgressBar { Height = 6, Minimum = 0, Maximum = 1 };
            _stop = new Button { Content = "Stop after this surface", Padding = new Thickness(12, 4, 12, 4), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            _stop.Click += (_, _) => RequestStop();

            var panel = new StackPanel { Margin = new Thickness(16, 12, 16, 14) };
            panel.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(_detail);
            panel.Children.Add(_bar);
            panel.Children.Add(new TextBlock
            {
                Text = "YCShell shows its own progress for each surface. Answer its Yes/No questions as usual; UCV closes its information boxes.",
                TextWrapping = TextWrapping.Wrap, FontSize = 11.5, Foreground = Brushes.DimGray, Margin = new Thickness(0, 10, 0, 0),
            });
            panel.Children.Add(_stop);

            _window = new Window
            {
                Title = "UCV - " + title,
                Content = panel,
                Width = 420,
                SizeToContent = SizeToContent.Height,
                Topmost = true,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = SystemParameters.WorkArea.Right - 440,
                Top = SystemParameters.WorkArea.Top + 20,
            };
            _window.Closing += (_, e) =>
            {
                if (_finished) return;
                e.Cancel = true;     // closing mid-batch means Stop
                RequestStop();
            };
        }

        private void RequestStop()
        {
            _stopRequested = true;
            if (_stop is not null)
            {
                _stop.IsEnabled = false;
                _stop.Content = "Stopping after this surface…";
            }
        }
    }
}
