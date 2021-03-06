using System;
using System.Windows.Data;

namespace WeiZhi3.Converters
{
    [ValueConversion(typeof(string), typeof(Uri))]
    public class ImageUrlConverter : IValueConverter
    {
        // Methods
        public object Convert(object value, Type target_type, object parameter, System.Globalization.CultureInfo culture)
        {
            var url = (string) value;
            return string.IsNullOrEmpty(url) ? null : new Uri(url);
        }

        public object ConvertBack(object value, Type target_type, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}