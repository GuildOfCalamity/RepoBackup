using System;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml.Data;

namespace WinUIDemo;

public class FullPathToFileConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string result = string.Empty;

        //Debug.WriteLine($"Shortening '{value}'...");
        if (!string.IsNullOrEmpty((string)value))
        {
            result = Path.GetFileNameWithoutExtension((string)value);
        }

        return result;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
