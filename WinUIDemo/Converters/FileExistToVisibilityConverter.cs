using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace WinUIDemo;

public class FileExistToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var path = value as string;
        if (!string.IsNullOrEmpty(path))
        {
            if (System.IO.File.Exists(path))
                return Visibility.Visible;
            else
                return Visibility.Collapsed;
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
