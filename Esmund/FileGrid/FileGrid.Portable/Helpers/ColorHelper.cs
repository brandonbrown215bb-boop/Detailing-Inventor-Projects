using System.Globalization;
using System.Windows.Media;

namespace FileGrid.Portable.Helpers;

public static class ColorHelper
{
    public static Color ParseHex(string hex, byte fallbackA = 255)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Colors.White;
        hex = hex.Trim().TrimStart('#');
        if (hex.Length == 6 && byte.TryParse(hex[..2], NumberStyles.HexNumber, null, out var r)
                            && byte.TryParse(hex.AsSpan(2, 2), NumberStyles.HexNumber, null, out var g)
                            && byte.TryParse(hex.AsSpan(4, 2), NumberStyles.HexNumber, null, out var b))
            return Color.FromArgb(fallbackA, r, g, b);
        return Colors.White;
    }

    public static SolidColorBrush Brush(string hex, double opacity = 1)
    {
        var c = ParseHex(hex);
        c.A = (byte)(255 * opacity);
        return new SolidColorBrush(c);
    }
}
