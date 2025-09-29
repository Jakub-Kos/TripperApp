using System;
using System.Globalization;
using System.Windows.Data;

namespace TripPlanner.Wpf.Converters
{
    public sealed class BooleanNegationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            if (value == null) return true; // null treated as false -> !false = true
            try
            {
                if (value is string s && bool.TryParse(s, out var parsed)) return !parsed;
            }
            catch { }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
