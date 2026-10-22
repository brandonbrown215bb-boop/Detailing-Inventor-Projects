using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace UnitConstructionVerifier.Spike
{
    public enum DockKeyMode { Off, Shortcuts, Passthrough }

    /// <summary>
    /// Docking spike: the controls the verifier relies on (text box, editable/non-editable dropdowns, editable grid),
    /// plus a log of every key, character, focus and size change that actually reaches WPF. The log is also written to
    /// %TEMP%\UnitConstructionVerifier\ucv_docktest.txt.
    /// </summary>
    internal sealed class DockTestPanel : Border
    {
        internal static readonly string LogPath = Path.Combine(DebugLogger.LogDirectory, "ucv_docktest.txt");

        private readonly ListBox _log = new ListBox { FontFamily = new FontFamily("Consolas"), FontSize = 11 };
        private readonly Func<string> _stateReport;

        public event Action<DockKeyMode>? ModeChanged;

        public sealed class TestRow
        {
            public string Part { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }

        public DockTestPanel(Func<string> stateReport)
        {
            _stateReport = stateReport;
            Background = Brushes.White;
            Padding = new Thickness(8);

            var root = new DockPanel { LastChildFill = true };
            Child = root;

            var top = new StackPanel();
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);

            top.Children.Add(new TextBlock
            {
                Text = "UCV dock test (dev). Try each key mode: type in the box, edit a grid cell, use arrows / Tab / " +
                       "Backspace / Delete / Ctrl+C / Ctrl+V / Ctrl+Z / F8. Then dock left, right, bottom, float, resize, " +
                       "close and reopen. Everything that reaches this panel is logged below.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var modes = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            top.Children.Add(modes);
            modes.Children.Add(new TextBlock { Text = "Keys: ", VerticalAlignment = VerticalAlignment.Center });
            foreach (DockKeyMode mode in new[] { DockKeyMode.Off, DockKeyMode.Shortcuts, DockKeyMode.Passthrough })
            {
                var radio = new RadioButton
                {
                    Content = mode == DockKeyMode.Off ? "Off (as is)" : mode == DockKeyMode.Shortcuts ? "Shortcut hook" : "Full passthrough",
                    GroupName = "DockKeyMode",
                    IsChecked = mode == DockKeyMode.Off,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                radio.Checked += (_, __) => { Log($"--- key mode: {mode}"); ModeChanged?.Invoke(mode); };
                modes.Children.Add(radio);
            }

            top.Children.Add(Labeled("Text box", new TextBox { Text = "type here" }));
            top.Children.Add(Labeled("Editable dropdown", new ComboBox { IsEditable = true, ItemsSource = new[] { "18", "16", "14" }, Text = "16" }));
            top.Children.Add(Labeled("Dropdown", new ComboBox { ItemsSource = new[] { "STL GALV", "STL GALV PPC", "ALM EMB" }, SelectedIndex = 0 }));
            top.Children.Add(new CheckBox { Content = "Check box (Space toggles)", Margin = new Thickness(0, 4, 0, 4) });

            var grid = new DataGrid
            {
                AutoGenerateColumns = true,
                CanUserAddRows = false,
                Height = 110,
                ItemsSource = new ObservableCollection<TestRow>
                {
                    new TestRow { Part = "091Z010068-0105", Value = "16 GA STL GALV PPC" },
                    new TestRow { Part = "091Z010068-0119", Value = "18 GA STL GALV" },
                    new TestRow { Part = "091Z010068-0122", Value = "20 GA ALM EMB" },
                }
            };
            top.Children.Add(grid);

            var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 6) };
            top.Children.Add(buttons);
            buttons.Children.Add(Button("Report state", () => Log("state: " + _stateReport())));
            buttons.Children.Add(Button("Copy log", CopyLog));
            buttons.Children.Add(Button("Clear log", () => _log.Items.Clear()));

            root.Children.Add(_log);

            PreviewKeyDown += (_, e) => Log($"KeyDown {(e.Key == Key.System ? "Alt+" + e.SystemKey : e.Key.ToString())}  mods={Keyboard.Modifiers}  in={Describe(Keyboard.FocusedElement)}");
            PreviewTextInput += (_, e) => Log($"Text '{e.Text}'  in={Describe(Keyboard.FocusedElement)}");
            PreviewGotKeyboardFocus += (_, e) => Log($"Focus -> {Describe(e.NewFocus)}");
            SizeChanged += (_, e) => Log($"Size {e.NewSize.Width:0}x{e.NewSize.Height:0}");
            IsVisibleChanged += (_, __) => Log($"Visible {IsVisible}");
            CommandManager.AddPreviewExecutedHandler(this, (_, e) => Log($"Command {(e.Command as RoutedUICommand)?.Text ?? e.Command.ToString()}  in={Describe(e.OriginalSource as IInputElement)}"));
        }

        public void Log(string line)
        {
            string stamped = $"{DateTime.Now:HH:mm:ss.fff} {line}";
            try
            {
                _log.Items.Add(stamped);
                while (_log.Items.Count > 400) _log.Items.RemoveAt(0);
                _log.ScrollIntoView(stamped);
            }
            catch { }

            try
            {
                Directory.CreateDirectory(DebugLogger.LogDirectory);
                File.AppendAllText(LogPath, stamped + Environment.NewLine);
            }
            catch { }
        }

        private void CopyLog()
        {
            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, _log.Items.Cast<object>()));
                Log("(log copied to clipboard)");
            }
            catch (Exception ex)
            {
                Log("copy failed: " + ex.Message);
            }
        }

        private static string Describe(object? element) => element switch
        {
            null => "(nothing)",
            FrameworkElement fe when !string.IsNullOrEmpty(fe.Name) => $"{fe.GetType().Name}#{fe.Name}",
            _ => element.GetType().Name
        };

        private static FrameworkElement Labeled(string label, Control control)
        {
            var panel = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var text = new TextBlock { Text = label, Width = 120, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(text, Dock.Left);
            panel.Children.Add(text);
            panel.Children.Add(control);
            return panel;
        }

        private static Button Button(string text, Action onClick)
        {
            var button = new Button { Content = text, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 0, 6, 0) };
            button.Click += (_, __) => onClick();
            return button;
        }
    }
}
