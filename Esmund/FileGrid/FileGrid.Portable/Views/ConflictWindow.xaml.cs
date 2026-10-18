using System.IO;
using System.Windows;
using FileGrid.Portable.Services;

namespace FileGrid.Portable.Views;

public partial class ConflictWindow : Window
{
    public ConflictChoice Choice { get; private set; } = ConflictChoice.Skip;
    public bool ApplyToAll => ApplyAll.IsChecked == true;

    private ConflictWindow(MoveConflictInfo info)
    {
        InitializeComponent();
        Headline.Text = $"\"{info.Name}\" is already in the destination folder.";
        IncomingInfo.Text = FormatSide("Moving", info.SourcePath);
        ExistingInfo.Text = FormatSide("Existing", info.ExistingPath);
    }

    private static string FormatSide(string label, string path)
    {
        try
        {
            if (Directory.Exists(path))
                return $"{label}: {path}\n  Folder · {Directory.GetLastWriteTime(path):g}";
            var fi = new FileInfo(path);
            return $"{label}: {path}\n  {fi.Length:N0} bytes · {fi.LastWriteTime:g}";
        }
        catch
        {
            return $"{label}: {path}";
        }
    }

    private void Replace_Click(object sender, RoutedEventArgs e) { Choice = ConflictChoice.Replace; DialogResult = true; }
    private void KeepBoth_Click(object sender, RoutedEventArgs e) { Choice = ConflictChoice.KeepBoth; DialogResult = true; }
    private void Skip_Click(object sender, RoutedEventArgs e) { Choice = ConflictChoice.Skip; DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) { Choice = ConflictChoice.Cancel; DialogResult = true; }

    public static (ConflictChoice choice, bool applyToAll)? Show(Window owner, MoveConflictInfo info)
    {
        var dlg = new ConflictWindow(info) { Owner = owner };
        return dlg.ShowDialog() == true ? (dlg.Choice, dlg.ApplyToAll) : null;
    }
}
