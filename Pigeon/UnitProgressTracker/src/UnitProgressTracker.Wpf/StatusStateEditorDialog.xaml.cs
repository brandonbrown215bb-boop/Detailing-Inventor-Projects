using System.Windows;
using UnitProgressTracker.Wpf.ViewModels;

namespace UnitProgressTracker.Wpf;

public partial class StatusStateEditorDialog : Window
{
    public StatusStateEditorViewModel ViewModel { get; }

    public StatusStateEditorDialog(StatusStateEditorViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;

        ViewModel.RequestClose += result =>
        {
            DialogResult = result;
            Close();
        };
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
