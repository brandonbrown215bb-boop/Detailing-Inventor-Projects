using System.Windows;
using UnitProgressTracker.Wpf.ViewModels;

namespace UnitProgressTracker.Wpf;

public partial class BomAddEntryDialog : Window
{
    public BomAddEntryViewModel ViewModel { get; }

    public BomAddEntryDialog(BomAddEntryViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
    }

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.PartNumber))
        {
            MessageBox.Show("Please enter a part number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
