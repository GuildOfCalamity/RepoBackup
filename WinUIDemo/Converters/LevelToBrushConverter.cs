using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace WinUIDemo;

public class LevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        SolidColorBrush scb = new SolidColorBrush((Windows.UI.Color.FromArgb(255, 240, 240, 240)));

        if (value == null)
            return scb;

        switch ((LogLevel)value)
        {
            case LogLevel.Off:
                scb = new SolidColorBrush(Colors.DimGray);
                break;
            case LogLevel.Debug:
                scb = new SolidColorBrush(Colors.Gray);
                break;
            case LogLevel.Important: // To be used in tandem with the LevelToBackgroundConverter.
                scb = new SolidColorBrush(Colors.White);
                break;
            case LogLevel.Notice: // To be used in tandem with the LevelToBackgroundConverter.
                scb = new SolidColorBrush(Colors.Black);
                break;
            case LogLevel.Info:
                scb = new SolidColorBrush(Colors.Turquoise);
                break;
            case LogLevel.Success:
                scb = new SolidColorBrush(Colors.SpringGreen);
                break;
            case LogLevel.Warning:
                scb = new SolidColorBrush(Colors.Gold);
                break;
            case LogLevel.Error:
                scb = new SolidColorBrush(Colors.OrangeRed);
                break;
            case LogLevel.Critical:
                scb = new SolidColorBrush(Colors.Red);
                break;
            default: // ???
                scb = new SolidColorBrush((Windows.UI.Color.FromArgb(255, 240, 240, 240)));
                break;
        }

        return scb;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}

public class LevelToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        SolidColorBrush scb = new SolidColorBrush((Windows.UI.Color.FromArgb(255, 240, 240, 240)));

        if (value == null)
            return scb;

        switch ((LogLevel)value)
        {
            case LogLevel.Off:
                scb = new SolidColorBrush(Colors.Transparent);
                break;
            case LogLevel.Debug:
                scb = new SolidColorBrush(Colors.Transparent);
                break;
            case LogLevel.Important: // We want theses to stand out.
                scb = new SolidColorBrush(Colors.DarkOrchid);
                break;
            case LogLevel.Notice: // We want theses to stand out.
                scb = new SolidColorBrush(Colors.Wheat);
                break;
            case LogLevel.Info:
                scb = new SolidColorBrush(Colors.Transparent);
                break;
            case LogLevel.Success:
                scb = new SolidColorBrush(Colors.Transparent);
                break;
            case LogLevel.Warning:
                scb = new SolidColorBrush(Colors.Transparent);
                break;
            case LogLevel.Error:
                scb = new SolidColorBrush(Colors.Transparent);
                break;
            case LogLevel.Critical:
                scb = new SolidColorBrush(Colors.Transparent);
                break;
            default: // ???
                scb = new SolidColorBrush(Colors.Transparent);
                break;
        }

        return scb;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
