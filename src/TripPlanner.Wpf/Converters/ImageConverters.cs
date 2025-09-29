using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace TripPlanner.Wpf.Converters
{
    public sealed class StringToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            if (string.IsNullOrWhiteSpace(s)) return null;
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(s, UriKind.RelativeOrAbsolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                return bi;
            }
            catch { return null; }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}