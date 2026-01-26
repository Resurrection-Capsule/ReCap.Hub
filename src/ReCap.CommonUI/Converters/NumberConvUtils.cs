using System;
using Avalonia.Data.Converters;

namespace ReCap.CommonUI.Converters
{
    internal static class NumberConvUtils
    {
        public static double ObjectToDouble(object value)
        {
            double inVal = 1;
            
            if (value == null)
                return inVal;


            if (value is double val)
                inVal = val;
            else if (!double.TryParse(value.ToString(), out inVal))
                inVal = 1;

            return inVal;
        }
    }
}
