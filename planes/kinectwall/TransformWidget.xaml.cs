using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace kinectwall
{
    /// <summary>
    /// Interaction logic for TransformWidget.xaml
    /// </summary>
    public partial class TransformWidget : UserControl
    {
        public TransformWidget()
        {
            InitializeComponent();
        }

        private void LimitSet_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            int idx = (int)b.Tag;


        }
    }

    [ValueConversion(typeof(object), typeof(string))]
    public class StringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            float v = (float)value;
            return v.ToString("N2");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}