using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FaceID
{
    /// <summary>
    /// Interaction logic for InputNumber.xaml
    /// </summary>
    public partial class InputNumber : Window
    {
        public InputNumber()
        {
            InitializeComponent();

           
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Regex obj = new Regex(@"^\d{4}$");
            if (!obj.IsMatch(PhoneNumber.Text.Trim()))
            {
                System.Windows.MessageBox.Show("请输入正确的4位数字");
            }

            /**
            string connstr = System.Configuration.ConfigurationManager.ConnectionStrings["mysqlcon"].ConnectionString.ToString();
            MySqlParameter[] param = {new MySqlParameter("@phone4number",PhoneNumber.Text.Trim())};
            DataSet ds = FacePay.Tools.helper.MysqlHelper.GetDataSet("SELECT phone,faceID FROM `coodellsho_memberuserinfo` WHERE RIGHT(phone,4)=@phone4number ", CommandType.Text, param
                 );
            **/

            MySqlConnection mysqlcon = new MySqlConnection("server=rdss35a0i331e6661w95o.mysql.rds.aliyuncs.com ;user id=coodelladmin ;password=Coodell2016;database=coodellshop");
            MySqlCommand mysqlcom = new MySqlCommand(string.Format("SELECT phone,faceID FROM `coodellsho_memberuserinfo` WHERE RIGHT(phone,4)='{0}'", PhoneNumber.Text.Trim()), mysqlcon);
            mysqlcon.Open();
            MySqlDataReader mysqlread = mysqlcom.ExecuteReader(CommandBehavior.CloseConnection);
            while (mysqlread.Read())
            {

                if (mysqlread["faceID"].ToString() != "")
                {
                    MainWindow window = new MainWindow(mysqlread["phone"].ToString(), mysqlread["faceID"].ToString());
                    window.Show();
                }
                else
                {
                    System.Windows.MessageBox.Show("此尾号有多人相同,无法实现刷脸快捷支付!");
                }
            }

            mysqlcom.Dispose();
            mysqlcon.Close();
            mysqlcon.Dispose();


        }
    }
}
