using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ReCap.CommonUI.Converters
{
    public class EnumToBoolConverter
        : IValueConverter
    {
        bool _trueIfMatch = true;
        public bool TrueIfMatch
        {
            get => _trueIfMatch;
            set => _trueIfMatch = value;
        }
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (_trueIfMatch)
                return value == parameter;
            else
                return value != parameter;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
