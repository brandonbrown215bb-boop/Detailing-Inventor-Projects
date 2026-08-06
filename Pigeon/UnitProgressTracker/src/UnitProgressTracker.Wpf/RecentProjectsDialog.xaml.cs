using System;
using System.Collections.Generic;
using System.Windows;
using UnitProgressTracker.Wpf.ViewModels;

namespace UnitProgressTracker.Wpf;

public partial class RecentProjectsDialog : Window
{
    public RecentProjectItemViewModel? SelectedProject { get; private set; }
    public bool ClearRequested { get; private set; }

    public RecentProjectsDialog(IEnumerable<RecentProjectItemViewModel> recentProjects)
    {
        InitializeComponent();
        RecentListBox.ItemsSource = recentProjects;
    }

    private void OnRecentSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SelectedProject = RecentListBox.SelectedItem as RecentProjectItemViewModel;
    }

    private void OnOpenSelectedClick(object sender, RoutedEventArgs e)
    {
        if (SelectedProject != null)
        {
            DialogResult = true;
            Close();
        }
    }

    private void OnClearHistoryClick(object sender, RoutedEventArgs e)
    {
        ClearRequested = true;
        DialogResult = true;
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
