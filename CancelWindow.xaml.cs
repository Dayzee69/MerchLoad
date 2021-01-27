using Npgsql;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MerchLoad
{
    /// <summary>
    /// Логика взаимодействия для CancelWindow.xaml
    /// </summary>
    public partial class CancelWindow : Window
    {
        public CancelWindow()
        {
            InitializeComponent();
        }

        private void loadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Merchant merchant = new Merchant(); 
                string regex = @"\A[0-9]{20,20}";
                if (Regex.IsMatch(tbTrans.Text, regex))
                {
                    merchant.merchantID = tbTrans.Text;
                }
                else
                {
                    throw new Exception("Поле 'ID Транзакции' не должно быть пустым и должно содержать 20 цифр");
                }

                regex = @"\A[0-9]{1,10}(?:[.][0-9]{2,2})?\z";
                if (Regex.IsMatch(tbAmount.Text, regex))
                {
                    merchant.amount = tbAmount.Text;
                }
                else
                {
                    throw new Exception("Поле 'Сумма' не должно быть пустым и должно содержать данные от 1 до 10 цифр и необязательный остаток длинной 2 с разделителем '.'");
                }

                //https://endpoint/v1/operator/cancel
                //12139186
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private string[] GetMerchantInfo(string trans)
        {
            string[] str = new string[3];

            string strPath = System.IO.Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            INIManager manager = new INIManager(strPath + @"\settings.ini");

            NpgsqlConnection pgsqlConnection = new NpgsqlConnection(@"Server=" + manager.GetPrivateString("Database", "id") + ";Port=" +
                manager.GetPrivateString("Database", "port") + ";User ID=" + manager.GetPrivateString("Database", "login") +
                ";Password=" + manager.GetPrivateString("Database", "password") + ";Database=" + manager.GetPrivateString("Database", "db"));

            try
            {
                pgsqlConnection.Open();
                NpgsqlCommand pgsqlCommand = pgsqlConnection.CreateCommand();
                pgsqlCommand.CommandText = $@"select auth_agent_id from pr_trn where id = {trans}";
                using (DbDataReader reader = pgsqlCommand.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        reader.Read();
                        str[0] = reader.GetValue(0).ToString();
                        string[] arrS = reader.GetValue(1).ToString().Split(new string[] { "000" }, StringSplitOptions.None);
                        str[1] = arrS[0];

                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                pgsqlConnection.Close();
                pgsqlConnection.Dispose();
            }
            str[2] = trans;
            return str;
        }

        private void tbTrans_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex rx = new Regex(@"^\d+");
            Match match = rx.Match(e.Text);
            if (e.Text.Length >= 1 && !match.Success)
            {
                e.Handled = true;
            }
        }

        private void tbAmount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex rx = new Regex(@"^\d+");
            Regex rx2 = new Regex(@"[.]");
            Match match = rx.Match(e.Text);
            Match match2 = rx2.Match(e.Text);
            if (!match.Success && !match2.Success)
            {
                e.Handled = true;
            }
        }
    }
}
