using Npgsql;
using System;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml;

namespace MerchLoad
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
        }

        private string[] GetMerchantInfo(string account) 
        {
            string[] str = new string[4];

            string strPath = System.IO.Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            INIManager manager = new INIManager(strPath + @"\settings.ini");

            NpgsqlConnection pgsqlConnection = new NpgsqlConnection(@"Server=" + manager.GetPrivateString("Database", "id") + ";Port=" +
                manager.GetPrivateString("Database", "port") + ";User ID=" + manager.GetPrivateString("Database", "login") +
                ";Password=" + manager.GetPrivateString("Database", "password") + ";Database=" + manager.GetPrivateString("Database", "db"));

            try
            {
                pgsqlConnection.Open();
                NpgsqlCommand pgsqlCommand = pgsqlConnection.CreateCommand();
                pgsqlCommand.CommandText = $@"select p.id as payee, p.agent_id as partner
                    from pr_agent_info a, pr_point p
                    where a.number_40702 = '{account}'
                    and a.id = p.agent_info_id and p.agent_id != '119000119'";

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
                pgsqlCommand.CommandText = $@"select h.agent_kd from pr_agent h 
                where parent_agent_id = '{str[1]}'*1000000 and h.agent_kd like 'PS%'
                ";
                using (DbDataReader reader = pgsqlCommand.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        reader.Read();
                        string[] arrS = reader.GetValue(0).ToString().Split('S');
                        str[3] = arrS[1];
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
            str[2] = account;
            return str;
        }

        private void regMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RegWindow regWindow = new RegWindow();
            regWindow.Owner = this;
            regWindow.ShowDialog();
        }

        private static string HashHmac(string message, string secret)
        {
            Encoding encoding = Encoding.UTF8;
            using (HMACSHA1 hmac = new HMACSHA1(encoding.GetBytes(secret)))
            {
                var msg = encoding.GetBytes(message);
                var hash = hmac.ComputeHash(msg);
                return BitConverter.ToString(hash).ToLower().Replace("-", string.Empty);
            }
        }

        private void loadButton_Click(object sender, RoutedEventArgs e)
        {
            loadButton.IsEnabled = false;
            try
            {
                string[] merhinfo;

                string regex = @"\A[0-9]{20,20}";
                if (Regex.IsMatch(tbAccount.Text, regex))
                {
                    merhinfo = GetMerchantInfo(tbAccount.Text);
                }
                else
                {
                    throw new Exception("Поле 'Лицевой счет' не должно быть пустым и должно содержать 20 цифр");
                }

                Merchant merchant = new Merchant(merhinfo[0], merhinfo[1], merhinfo[2], merhinfo[3]);

                regex = @"\A[0-9]{1,10}(?:[.][0-9]{2,2})?\z";
                if (Regex.IsMatch(tbAmount.Text, regex))
                {
                    merchant.amount = tbAmount.Text;
                }
                else
                {
                    throw new Exception("Поле 'Сумма' не должно быть пустым и должно содержать данные от 1 до 10 цифр и необязательный остаток длинной 2 с разделителем '.'");
                }

                merchant.date = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");
                merchant.orderID = merchant.date.Replace("-", "");
                merchant.orderID = merchant.orderID.Replace(" ", "");
                merchant.orderID = merchant.orderID.Replace(":", "");

                if (merchant.partnerID == "")
                {
                    throw new Exception("Свойству 'PartnerID' не присвоено значение");
                } 
                else if (merchant.paymentSystem == "") 
                {
                    throw new Exception("Свойству 'PaymentSystem' не присвоено значение");
                }

                System.Threading.Thread.Sleep(1000);

                X509Certificate2 ca = new X509Certificate2("ca.crt");
                X509Certificate2 cert = new X509Certificate2();

                using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadOnly);

                    foreach (X509Certificate2 item in store.Certificates)
                    {
                        if (item.Issuer == ca.Issuer)
                        {
                            if(item.NotAfter > DateTime.Now) 
                            {
                                cert = item;
                                break;
                            }
                            else 
                            {
                                throw new ArgumentException("Сертификат просрочен");
                            }
                        }
                    }
                }

                Random rand = new Random();
                string salt = rand.Next(123458, 987654).ToString();
                string key = "key";
                string uri_path = "path";

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://host");

                request.ClientCertificates.Add(cert);
;
                string postData = $@"
                <requests>
	                <request>
		                <datetime>{merchant.date}</datetime>
		                <order_id>{merchant.orderID}</order_id>
		                <payment_system>{merchant.paymentSystem}</payment_system>
		                <payee>{merchant.merchantID}</payee>
		                <amount>{merchant.amount}</amount>
		                <commission></commission>
                        <scenario>merchant</scenario>
	                </request>
                </requests>";

                request.Method = "POST";
                request.Accept = "text/xml";
                request.ContentType = "text/xml;charset=\"utf-8\"";
                request.ContentType = "text/xml";
                request.Headers.Add("X-Partner-ID", merchant.partnerID);
                request.Headers.Add("X-Salt", salt);
                request.Headers.Add("X-Sign", HashHmac(uri_path + ";" + postData + ";" + salt, key));

                StreamWriter sw = new StreamWriter(request.GetRequestStream());
                sw.Write(postData);
                sw.Close();

                WebResponse response = request.GetResponse();
                StreamReader reader = new StreamReader(response.GetResponseStream());

                XmlDocument xDoc = new XmlDocument();
                xDoc.Load(reader);
                XmlElement xRoot = xDoc.DocumentElement;
                XmlNode node = xRoot.SelectSingleNode("//response/transaction/status");

                tbAccount.Text = "";
                tbAmount.Text = "";

                MessageBox.Show(node.InnerText);

            }
            catch (Exception ex) 
            {
                MessageBox.Show(ex.Message);
            }
            loadButton.IsEnabled = true;
        }

        private void tbAccount_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            Regex rx = new Regex(@"^\d+");
            Match match = rx.Match(e.Text);
            if (e.Text.Length >= 1 && !match.Success)
            {
                e.Handled = true;
            }
        }

        private void tbAmount_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
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

        private void cancelMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CancelWindow cancelWindow = new CancelWindow();
            cancelWindow.Owner = this;
            cancelWindow.ShowDialog();
        }
    }
}
