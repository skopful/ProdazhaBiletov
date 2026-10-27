using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace ProdazhaBiletov
{
    public partial class CashierSmenaPage : UserControl
    {
        public CashierSmenaPage()
        {
            InitializeComponent();
            tbDate.Text = "Продажи кассира " + UserSession.ShortName + " за " + DateTime.Now.ToString("dd.MM.yyyy");
            Loaded += (s, e) => LoadSmena();
        }

        private void LoadSmena()
        {
            if (UserSession.Id == 0) return;

            string sql =
                "SELECT z.NomerZakaza, z.DataZakaza, z.StatusZakaza, z.SummaZakaza, z.SposobOplaty, " +
                "MIN(m.Nazvanie) AS Meropriyatie, COUNT(b.idBileta) AS Kolichestvo " +
                "FROM [322Zakaz] z " +
                "LEFT JOIN [322Bilet] b ON b.idZakaza = z.idZakaza " +
                "LEFT JOIN [322Meropriyatie] m ON m.idMeropriyatiya = b.idMeropriyatiya " +
                "WHERE z.idPolzovatelya = @id AND CAST(z.DataZakaza AS date) = CAST(GETDATE() AS date) " +
                "GROUP BY z.idZakaza, z.NomerZakaza, z.DataZakaza, z.StatusZakaza, z.SummaZakaza, z.SposobOplaty " +
                "ORDER BY z.DataZakaza DESC";

            try
            {
                DataTable table = new DataTable();
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlCommand cmd = new SqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@id", UserSession.Id);
                    new SqlDataAdapter(cmd).Fill(table);
                }

                int orders = 0, tickets = 0;
                decimal revenue = 0;
                foreach (DataRow r in table.Rows)
                {
                    string status = r["StatusZakaza"].ToString();
                    if (status != "Оплачен" && status != "Использован") continue;

                    orders++;
                    tickets += Convert.ToInt32(r["Kolichestvo"]);
                    revenue += Convert.ToDecimal(r["SummaZakaza"]);
                }

                tbOrders.Text = orders.ToString();
                tbTickets.Text = tickets.ToString();
                tbRevenue.Text = revenue.ToString("N0") + " ₽";
                dgSmena.ItemsSource = table.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки смены: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadSmena();
        }
    }
}