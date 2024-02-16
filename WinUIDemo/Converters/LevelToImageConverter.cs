using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace WinUIDemo;

/// <summary>
/// To be used with an <see cref="Microsoft.UI.Xaml.Controls.Image"/> control.
/// </summary>
/// <example>
/// {Image Width="28" Height="28" Source="{x:Bind Time, Converter={StaticResource LevelToImage}}"/}
/// </example>
public class LevelToImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        ImageSource result = new BitmapImage(new Uri("ms-appx:///Assets/Check_Green.png", UriKind.Absolute));

        if (value == null)
            return result;

       /* 
        *   Time: "Soon", "A few days", "A week from now", "A month from now", "A year from now" 
        */
        switch ((string)value)
        {
            case string time when time.Contains("year", StringComparison.OrdinalIgnoreCase):
                result = new BitmapImage(new Uri("ms-appx:///Assets/Check_Purple.png", UriKind.Absolute));
                break;
            case string time when time.Contains("month", StringComparison.OrdinalIgnoreCase):
                result = new BitmapImage(new Uri("ms-appx:///Assets/Check_Blue.png", UriKind.Absolute));
                break;
            case string time when time.Contains("week", StringComparison.OrdinalIgnoreCase):
                result = new BitmapImage(new Uri("ms-appx:///Assets/Check_Green.png", UriKind.Absolute));
                break;
            case string time when time.Contains("days", StringComparison.OrdinalIgnoreCase):
                result = new BitmapImage(new Uri("ms-appx:///Assets/Check_Yellow.png", UriKind.Absolute));
                break;
            case string time when time.Contains("soon", StringComparison.OrdinalIgnoreCase):
                result = new BitmapImage(new Uri("ms-appx:///Assets/Check_Orange.png", UriKind.Absolute));
                break;
            default: // ???
                result = new BitmapImage(new Uri("ms-appx:///Assets/Check_Red.png", UriKind.Absolute));
                break;
        }

        return result;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
