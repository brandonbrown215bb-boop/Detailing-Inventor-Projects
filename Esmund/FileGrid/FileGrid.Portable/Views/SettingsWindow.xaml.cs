using System.Windows;
using FileGrid.Portable.Models;

namespace FileGrid.Portable.Views;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; private set; }
    public string? RenamedLayoutName { get; private set; }
    public string? DuplicateLayoutName { get; private set; }

    public SettingsWindow(AppSettings settings, LayoutState layout)
    {
        Settings = settings;
        InitializeComponent();
        TextScale.Value = settings.TextScale;
        ShowPath.IsChecked = settings.ShowFilePath;
        ShowGrid.IsChecked = settings.ShowGrid;
        BgColor.Text = settings.BackgroundColor;
        GridColor.Text = settings.GridColor;
        GridOpacity.Value = settings.GridOpacity;
        FontColor.Text = settings.FontColor;
        FontFamilyBox.ItemsSource = new[] { "Segoe UI", "Arial", "Tahoma", "Verdana", "Lexend", "Consolas" };
        FontFamilyBox.Text = settings.FontFamily;
        LayoutName.Text = layout.Name;
        DuplicateName.Text = $"{layout.Name} copy";
    }

    private void SaveName_Click(object sender, RoutedEventArgs e) => RenamedLayoutName = LayoutName.Text.Trim();

    private void Duplicate_Click(object sender, RoutedEventArgs e) => DuplicateLayoutName = DuplicateName.Text.Trim();

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        Settings = new AppSettings
        {
            TextScale = TextScale.Value,
            ShowFilePath = ShowPath.IsChecked == true,
            ShowGrid = ShowGrid.IsChecked == true,
            BackgroundColor = BgColor.Text.Trim(),
            GridColor = GridColor.Text.Trim(),
            GridOpacity = GridOpacity.Value,
            FontFamily = FontFamilyBox.Text.Trim(),
            FontColor = FontColor.Text.Trim()
        };
        DialogResult = true;
    }
}
