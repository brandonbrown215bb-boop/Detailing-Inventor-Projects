using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

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
