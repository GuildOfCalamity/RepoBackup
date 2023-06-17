using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace WinUIDemo;

class SizeDecreaseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double?)
        {
            double multiplier = 0.9d; // default is 10%
            var val = (double)value;
            if (parameter == null)
            {
                return val * multiplier; //return new Thickness(val);
            }
            else
            {
                string amount = parameter as string;
                if (Double.TryParse(amount, out multiplier))
                    return val * multiplier;
                else
                    return val * multiplier;
            }
        }
        return 10; // return some arbitrary value so we can see there was an issue
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}

class SizeIncreaseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double?)
        {
            double multiplier = 1.1d; // default is 10%
            var val = (double)value;
            if (parameter == null)
            {
                return val * multiplier; //return new Thickness(val);
            }
            else
            {
                string amount = parameter as string;
                if (Double.TryParse(amount, out multiplier))
                    return val * multiplier;
                else
                    return val * multiplier;
            }
        }
        return 10; // return some arbitrary value so we can see there was an issue
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return null;
    }
}
