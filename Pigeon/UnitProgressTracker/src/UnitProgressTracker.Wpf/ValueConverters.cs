using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace UnitProgressTracker.Wpf;

public class IntToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int val = value is int i ? i : 0;
        int target = int.TryParse(parameter?.ToString(), out int p) ? p : 0;
        return val == target;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return int.TryParse(parameter?.ToString(), out int p) ? p : 0;
    }
}

public class TabToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int val = value is int i ? i : 0;
        int target = int.TryParse(parameter?.ToString(), out int p) ? p : 0;
        return val == target ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>Converts WireframeVisible bool to button label text.</summary>
public class BoolToWireframeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        return b ? "Wireframe: On" : "Wireframe: Off";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>Converts IsHidden bool to a short visibility icon label for the surface list button.</summary>
public class BoolToVisibilityTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hidden = value is bool b && b;
        return hidden ? "👁‍🗨" : "👁";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>Converts IsHidden bool to "Show" / "Hide" for the detail panel toggle button.</summary>
public class BoolToVisibilityToggleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hidden = value is bool b && b;
        return hidden ? "Show in Viewport" : "Hide in Viewport";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>Converts Hex Color string (e.g. #38bdf8) to WPF SolidColorBrush.</summary>
public class ColorHexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                {
                    byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
                    return new SolidColorBrush(Color.FromRgb(r, g, b));
                }
                else if (hex.Length == 8)
                {
                    byte a = System.Convert.ToByte(hex.Substring(0, 2), 16);
                    byte r = System.Convert.ToByte(hex.Substring(2, 2), 16);
                    byte g = System.Convert.ToByte(hex.Substring(4, 2), 16);
                    byte b = System.Convert.ToByte(hex.Substring(6, 2), 16);
                    return new SolidColorBrush(Color.FromArgb(a, r, g, b));
                }
                else if (hex.Length == 3)
                {
                    byte r = System.Convert.ToByte($"{hex[0]}{hex[0]}", 16);
                    byte g = System.Convert.ToByte($"{hex[1]}{hex[1]}", 16);
                    byte b = System.Convert.ToByte($"{hex[2]}{hex[2]}", 16);
                    return new SolidColorBrush(Color.FromRgb(r, g, b));
                }
            }
            catch { }
        }
        return new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Default slate fallback #94a3b8
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SolidColorBrush brush)
        {
            return $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
        }
        return "#94a3b8";
    }
}
