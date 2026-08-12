using System.Windows;
using System.Windows.Input;
using UnitProgressTracker.Wpf.ViewModels;

namespace UnitProgressTracker.Wpf;

public partial class OptionsDialog : Window
{
    public OptionsViewModel ViewModel { get; }

    public OptionsDialog(OptionsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
    }

    private void OnAddChecklistItemClick(object sender, RoutedEventArgs e)
    {
        AddChecklistItem();
    }

    private void OnNewChecklistTemplateKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddChecklistItem();
        }
    }

    private void AddChecklistItem()
    {
        string text = NewChecklistTemplateBox.Text.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            ViewModel.AddChecklistTemplateItemCommand.Execute(text);
            NewChecklistTemplateBox.Clear();
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        ViewModel.PrepareForSave();
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
