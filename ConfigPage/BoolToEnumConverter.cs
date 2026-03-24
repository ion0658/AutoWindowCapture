using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace ConfigPage;

public class BoolToEnumConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string culture)
    {
        if ((value is null) || (parameter is not string ParameterString))
        {
            return DependencyProperty.UnsetValue;
        }

        if (Enum.IsDefined(value.GetType(), value) == false)
        {
            return DependencyProperty.UnsetValue;
        }

        object paramvalue = Enum.Parse(value.GetType(), ParameterString);

        return (int)paramvalue == (int)value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string culture)
    {
        return parameter is not string ParameterString ? DependencyProperty.UnsetValue : Enum.Parse(targetType, ParameterString);

    }

}

