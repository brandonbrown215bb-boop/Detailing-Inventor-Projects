using System.Windows.Media;

namespace FileGrid.Portable.Helpers;

public static class FontHelper
{
    public static FontFamily Safe(string? family)
    {
        if (string.IsNullOrWhiteSpace(family)) return new FontFamily("Segoe UI");
        try
        {
            return new FontFamily(family);
        }
        catch
        {
            var first = family.Split(',')[0].Trim().Trim('\'', '"');
            try { return new FontFamily(first); }
            catch { return new FontFamily("Segoe UI"); }
        }
    }
}
