using System;
using System.Windows;
using System.Windows.Controls;
using QuestBoard.UI.ViewModels;

namespace QuestBoard.UI.Views
{
    public partial class QuestDetailModal : UserControl
    {
        public QuestDetailModal()
        {
            InitializeComponent();
        }

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
                ViewModel.IsDetailOpen = false;
        }

        private async void ClaimButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
                await ViewModel.ClaimSelectedQuestAsync();
        }

        private async void HandoffButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.SelectedQuest == null) return;
            string currentAction = ViewModel.SelectedQuest.NextAction;

            var inputDialog = new PromptDialog("Update Handoff & Next Action", "Current Next Action:", currentAction, "Progress Note:", "Updated card status");
            if (inputDialog.ShowDialog() == true)
            {
                await ViewModel.HandoffSelectedQuestAsync(inputDialog.NextActionText, inputDialog.NoteText);
            }
        }

        private async void BlockButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            var inputDialog = new PromptDialog("Mark Quest as Blocked", "Next Action once unblocked:", "Resolve blocker condition", "Blocker Reason:", "Missing requirement / external dependency");
            if (inputDialog.ShowDialog() == true)
            {
                await ViewModel.MoveSelectedQuestAsync("blocked", inputDialog.NextActionText, inputDialog.NoteText, inputDialog.NoteText);
            }
        }

        private async void ReviewButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            var inputDialog = new PromptDialog("Submit for Review", "Verification Next Action:", "Review completed implementation", "Review Note:", "Implementation ready for review");
            if (inputDialog.ShowDialog() == true)
            {
                await ViewModel.MoveSelectedQuestAsync("review", inputDialog.NextActionText, inputDialog.NoteText);
            }
        }

        private async void FinishButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            var result = MessageBox.Show("Are you sure you want to mark this quest as FINISHED?", "Complete Quest", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                await ViewModel.FinishSelectedQuestAsync("Quest completed by human actor.");
            }
        }
    }
}
