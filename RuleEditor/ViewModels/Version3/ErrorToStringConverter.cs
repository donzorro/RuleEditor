using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace RuleEditor.ViewModels.Version3
{
    public class ErrorListToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is List<string> errorList && errorList.Any())
            {
                // Join all error messages with newlines
                return string.Join(Environment.NewLine, errorList);
            }
            
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // We don't need to convert back for this use case
            return null;
        }
    }
}