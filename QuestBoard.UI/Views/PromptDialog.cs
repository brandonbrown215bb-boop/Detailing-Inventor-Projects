using System.Windows;
using System.Windows.Controls;

namespace QuestBoard.UI.Views
{
    public class PromptDialog : Window
    {
        private readonly TextBox _nextActionBox;
        private readonly TextBox _noteBox;

        public string NextActionText => _nextActionBox.Text.Trim();
        public string NoteText => _noteBox.Text.Trim();

        public PromptDialog(string title, string nextLabel, string defaultNext, string noteLabel, string defaultNote)
        {
            Title = title;
            Width = 480;
            Height = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = System.Windows.Media.Brushes.OldLace;

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.SaddleBrown,
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(titleBlock, 0);
            grid.Children.Add(titleBlock);

            var lbl1 = new TextBlock { Text = nextLabel, FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.SaddleBrown };
            Grid.SetRow(lbl1, 1);
            grid.Children.Add(lbl1);

            _nextActionBox = new TextBox { Text = defaultNext, Margin = new Thickness(0, 4, 0, 10), Padding = new Thickness(4) };
            Grid.SetRow(_nextActionBox, 2);
            grid.Children.Add(_nextActionBox);

            var lbl2 = new TextBlock { Text = noteLabel, FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.SaddleBrown };
            Grid.SetRow(lbl2, 3);
            grid.Children.Add(lbl2);

            _noteBox = new TextBox { Text = defaultNote, Margin = new Thickness(0, 4, 0, 16), Padding = new Thickness(4), Height = 60, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
            Grid.SetRow(_noteBox, 4);
            grid.Children.Add(_noteBox);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "Submit", Width = 90, Height = 30, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Background = System.Windows.Media.Brushes.SaddleBrown, Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.Bold };
            okBtn.Click += (s, e) => { DialogResult = true; Close(); };
            var cancelBtn = new Button { Content = "Cancel", Width = 90, Height = 30, IsCancel = true };
            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);

            Grid.SetRow(btnPanel, 5);
            grid.Children.Add(btnPanel);

            Content = grid;
        }
    }
}
