using System;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Windows;


namespace MerchLoad
{
    /// <summary>
    /// Логика взаимодействия для RegWindow.xaml
    /// </summary>
    public partial class RegWindow : Window
    {
        public RegWindow()
        {
            InitializeComponent();
        }       

        private void regButton_Click(object sender, RoutedEventArgs e)
        {
            regButton.IsEnabled = false;

            Merchant merchant = new Merchant(merchantTb.Text, partnerTb.Text, accountTb.Text, psTb.Text);

            string strPath = System.IO.Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            INIManager manager = new INIManager(strPath + @"\settings.ini");

            SqlConnection sqlConnection = new SqlConnection(@"Data Source=" + manager.GetPrivateString("Database", "id") + ";Initial Catalog=" +
            manager.GetPrivateString("Database", "db") + ";User ID=" + manager.GetPrivateString("Database", "login") + ";Password=" +
            manager.GetPrivateString("Database", "password"));

            try
            {                
                sqlConnection.Open();
                SqlCommand sqlCommand = sqlConnection.CreateCommand();
                sqlCommand.CommandText = "INSERT INTO MerchInfo(PartnerID,MerchID,Account) VALUES ('" + merchant.partnerID + "','" + 
                    merchant.merchantID + "','" + merchant.account + "')";
                sqlCommand.ExecuteNonQuery();
                MessageBox.Show("Данные добавлены успешно");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                sqlConnection.Close();
                sqlConnection.Dispose();
            }

            partnerTb.Text = "";
            merchantTb.Text = "";
            accountTb.Text = "";
            regButton.IsEnabled = true;
        }
    }
}
