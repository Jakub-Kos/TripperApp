using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TripPlanner.Wpf.Converters
{
    public sealed class PercentToBrushConverter : IValueConverter
    {
        // Expects double in 0..1. Returns white -> green gradient.
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double pct = 0.0;
            if (value is double d) pct = Math.Max(0, Math.Min(1, d));

            // Interpolate between White (no votes) and a pleasant green.
            byte r = (byte)(255 - (int)(pct * 180)); // 255 -> ~75
            byte g = (byte)(255 - (int)(pct * 60));  // 255 -> ~195
            byte b = (byte)(255 - (int)(pct * 120)); // 255 -> ~135
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public sealed class BoolToThicknessConverter : IValueConverter
    {
        public Thickness TrueThickness { get; set; } = new Thickness(2);
        public Thickness FalseThickness { get; set; } = new Thickness(0);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool v && v;
            return b ? TrueThickness : FalseThickness;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}