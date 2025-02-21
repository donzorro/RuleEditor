using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace RuleEditor.Converters
{
    public class ValidationColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isValid)
            {
                return isValid
                    ? Brushes.Green     // Valid: Green color
                    : Brushes.Red;      // Invalid: Red color
            }

            return Brushes.Gray;        // Default: Gray color
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}