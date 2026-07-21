using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DevEnvStudio.Converters
{
    /// <summary>
    /// Returns Visible when the bound value is empty (null, 0, or empty string);
    /// otherwise returns Collapsed. Used for placeholder text overlays on TextBox.
    /// Supports: int (Text.Length), string, null.
    /// </summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public sealed class EmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Visible;

            if (value is int i)        return i == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (value is string s)    return string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
