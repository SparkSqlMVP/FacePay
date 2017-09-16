using System;
using System.Collections.Generic;
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

namespace FaceID
{
    /// <summary>
    /// Interaction logic for NumericKeyboard.xaml
    /// </summary>
    public partial class NumericKeyboard : UserControl
    {
        public string numbers = "";
        public NumericKeyboard()
        {
            InitializeComponent();
        }

     


      

        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Value.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(int), typeof(NumericKeyboard), new UIPropertyMetadata(0));



        public bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsChecked.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register("IsChecked", typeof(bool), typeof(NumericKeyboard), new UIPropertyMetadata(false));


        private void btnMin_Click(object sender, RoutedEventArgs e)
        {
            //if (Item != null && Item.Quantity1 > 0)
            //{
            //    Item.Quantity2 = Item.Quantity1;
            //}
            numbers = "";
        }

        private string AddNumber(string num)
        {
            //if (Item != null)
            //{
            //    int cnt = Item.Quantity2 * 10 + num;
            //    if (Item.Quantity1 > 0 && cnt > Item.Quantity1)
            //    {
            //        Item.Quantity2 = Item.Quantity1;
            //    }
            //    else
            //    {
            //        Item.Quantity2 = cnt;
            //    }
            //}
            //if (HasMaxValue)
            //{
            //    Value = Math.Min(MaxValue, Value * 10 + num);
            //}
            //else
            //{
            //    Value = Value * 10 + num;
            //}

            return numbers=numbers + num;
        }


        private void button2_Click(object sender, RoutedEventArgs e)
        {
            AddNumber("1");
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            AddNumber("3");
        }

        private void button4_Click(object sender, RoutedEventArgs e)
        {
            AddNumber("4");
        }

        private void button5_Click(object sender, RoutedEventArgs e)
        {
            AddNumber("5");
        }

        private void button6_Click(object sender, RoutedEventArgs e)
        {
            AddNumber("6");
        }

        private void button7_Click(object sender, RoutedEventArgs e)
        {
            AddNumber("7");
        }

        private void button8_Click(object sender, RoutedEventArgs e)
        {
            AddNumber("8");
        }

        private void button9_Click(object sender, RoutedEventArgs e)
        {
            AddNumber("9");
        }

        private void button0_Click(object sender, RoutedEventArgs e)
        {
            AddNumber("0");
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            AddNumber("1");
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(numbers.ToString());
            IsChecked = false;
        }
    }
}
