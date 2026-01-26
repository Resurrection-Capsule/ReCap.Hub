using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ReCap.CommonUI.Converters
{
    public class MultiplyConverter
        : IValueConverter
    {
        public static readonly MultiplyConverter Instance = new();
        private MultiplyConverter()
            : base()
        {}


        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double val = NumberConvUtils.ObjectToDouble(value);
            double param = NumberConvUtils.ObjectToDouble(parameter);

            return val * param;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
