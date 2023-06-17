using System;
using Microsoft.UI.Xaml.Data;

namespace WinUIDemo;

/// <summary>
/// Converts a <see cref="TimeSpan"/> into a human-readable string.
/// If we couldn't get TimeSpan data, we return an empty string.
/// </summary>
public class TimeSpanConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value == null)
        {
            return null;
        }

        string returnValue = string.Empty;
        TimeSpan tmp = (TimeSpan)value;
        if (tmp != TimeSpan.MinValue)
        {
            returnValue = $"{value:h\\:mm\\:ss}";
        }
        return returnValue;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
