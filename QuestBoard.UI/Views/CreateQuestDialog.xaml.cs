using System.Windows;
using System.Windows.Controls;
using QuestBoard.UI.ViewModels;

namespace QuestBoard.UI.Views
{
    public partial class CreateQuestDialog : UserControl
    {
        public CreateQuestDialog()
        {
            InitializeComponent();
        }

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
                ViewModel.IsCreateOpen = false;
        }

        private async void PostQuest_Click(object sender, RoutedEventArgs e)
        {
            string title = TitleTextBox.Text.Trim();
            string context = ContextTextBox.Text.Trim();
            string nextAction = NextActionTextBox.Text.Trim();
            string priority = ((ComboBoxItem)PriorityComboBox.SelectedItem)?.Content?.ToString() ?? "medium";

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(context) || string.IsNullOrWhiteSpace(nextAction))
            {
                MessageBox.Show("Please fill out Title, Context, and Next Action fields.", "Missing Fields", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ViewModel != null)
            {
                await ViewModel.CreateNewQuestAsync(title, context, nextAction, priority);
                TitleTextBox.Clear();
                ContextTextBox.Clear();
                NextActionTextBox.Clear();
            }
        }
    }
}
