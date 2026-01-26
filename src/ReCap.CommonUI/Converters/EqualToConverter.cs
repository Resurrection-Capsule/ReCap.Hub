using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ReCap.CommonUI.Converters
{
    public class CompareToConverter
        : IValueConverter
    {
        bool _trueIfMatch = true;
        public bool TrueIfMatch
        {
            get => _trueIfMatch;
            set => _trueIfMatch = value;
        }


        object _compareTo = null;
        public object CompareTo
        {
            get => _compareTo;
            set => _compareTo = value;
        }
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (_trueIfMatch)
                return value == _compareTo;
            else
                return value != _compareTo;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
