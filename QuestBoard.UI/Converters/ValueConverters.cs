using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuestBoard.UI
{
    public class FilterToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null)
            {
                return parameter.ToString()!;
            }
            return Binding.DoNothing;
        }
    }
}
