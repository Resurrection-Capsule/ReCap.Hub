using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ReCap.CommonUI.Converters
{
    public class LessThanConverter
        : IValueConverter
    {
        public static readonly LessThanConverter Instance = new();
        private LessThanConverter()
            : base()
        {}


        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double val = NumberConvUtils.ObjectToDouble(value);
            double param = NumberConvUtils.ObjectToDouble(parameter);
            return val < param;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
