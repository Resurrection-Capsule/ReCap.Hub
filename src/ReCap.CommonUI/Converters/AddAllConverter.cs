using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ReCap.CommonUI.Converters
{
    public class AddAllConverter
        : IMultiValueConverter
    {
        public static readonly AddAllConverter Instance = new();
        private AddAllConverter()
        {}

        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            double ret = 0;
            foreach (var value in values)
            {
                ret += NumberConvUtils.ObjectToDouble(value);
            }
            return ret;
        }
    }
}
