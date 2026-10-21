using System.Windows;

namespace FileGrid.Portable.Views;

public partial class NamePromptWindow : Window
{
    public string? Result { get; private set; }

    public NamePromptWindow(string title, string prompt, string initial)
    {
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = initial;
        Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(Result)) return;
        DialogResult = true;
    }

    public static string? Show(Window owner, string title, string prompt, string initial)
    {
        var dlg = new NamePromptWindow(title, prompt, initial) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg.Result : null;
    }
}
