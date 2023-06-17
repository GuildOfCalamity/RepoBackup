using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml;

namespace WinUIDemo;

public class NullToVisibilityConverter : IValueConverter
{
    public Visibility NullValue { get; set; } = Visibility.Collapsed;
    public Visibility NonNullValue { get; set; } = Visibility.Visible;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value == null) ? NullValue : NonNullValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
