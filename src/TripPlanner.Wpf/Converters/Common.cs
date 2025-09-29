using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TripPlanner.Wpf.Converters
{
    /// <summary>
    /// Inverts a boolean value. Non-boolean inputs return Binding.DoNothing.
    /// </summary>
    public sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) => !(value is bool b && b);
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    /// <summary>
    /// Compares value.ToString() to ConverterParameter.ToString(); useful for enums in bindings.
    /// </summary>
    public sealed class EnumEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value?.ToString() == parameter?.ToString();
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>
    /// Maps a simple join mode string to Visibility. Parameter must be "Claim" or "Join".
    /// </summary>
    public sealed class JoinModeToVisibility : IValueConverter
    {
        public object Convert(object value, Type t, object parameter, CultureInfo c)
            => (value?.ToString() == "ClaimPlaceholder" && (string?)parameter == "Claim") ||
               (value?.ToString() == "JoinAsMe"        && (string?)parameter == "Join")
                ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
    
    /// <summary>
    /// Returns Collapsed for null/empty strings, Visible otherwise.
    /// </summary>
    public sealed class StringNullOrEmptyToCollapsed : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
    
    /// <summary>
    /// Turns a small integer flag into user-facing text: 1 -> "Shared", anything else -> "Per person".
    /// </summary>
    public sealed class GearProvisioningToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is int i && i == 1) ? "Shared" : "Per person";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => 0;
    }

    /// <summary>
    /// Returns true if the bound integer equals 1.
    /// </summary>
    public sealed class BooleanWhenEqualsOneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is int i) && i == 1;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => 0;
    }
}