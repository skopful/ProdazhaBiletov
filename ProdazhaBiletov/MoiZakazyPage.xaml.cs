using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace ProdazhaBiletov
{
    public partial class MoiZakazyPage : UserControl
    {
        private DataTable _all;

        public MoiZakazyPage()
        {
            InitializeComponent();

            cbStatus.Items.Add("Все статусы");
            cbStatus.Items.Add("Оплачен");
            cbStatus.Items.Add("Забронирован");
            cbStatus.Items.Add("Использован");
            cbStatus.Items.Add("Отменен");
            cbStatus.SelectedIndex = 0;

            Loaded += (s, e) => LoadOrders();
        }

        private void LoadOrders()
        {
            if (UserSession.Id == 0)
                return;

            string sql = "SELECT z.idZakaza, z.NomerZakaza, z.DataZakaza, z.StatusZakaza, z.SummaZakaza, " +
                "MIN(m.DataVremyaNachala) AS DataVremyaNachala, MIN(m.Nazvanie) AS NazvanieMeropriyatiya, COUNT(b.idBileta) AS Kolichestvo " +
                "FROM [322Zakaz] z " +
                "JOIN [322Bilet] b ON b.idZakaza = z.idZakaza " +
                "JOIN [322Meropriyatie] m ON b.idMeropriyatiya = m.idMeropriyatiya " +
                "WHERE z.idPolzovatelya = @id " +
                "GROUP BY z.idZakaza, z.NomerZakaza, z.DataZakaza, z.StatusZakaza, z.SummaZakaza " +
                "ORDER BY z.DataZakaza DESC";

            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlCommand cmd = new SqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@id", UserSession.Id);
                    _all = new DataTable();
                    new SqlDataAdapter(cmd).Fill(_all);
                }
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки заказов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            if (_all == null) return;

            DataTable filtered = _all.Clone();
            string status = cbStatus.SelectedIndex > 0 ? cbStatus.SelectedItem.ToString() : null;
            DateTime? from = dpFromZ.SelectedDate;
            DateTime? to = dpToZ.SelectedDate;

            foreach (DataRow r in _all.Rows)
            {
                if (status != null && r["StatusZakaza"].ToString() != status) continue;
                DateTime d = Convert.ToDateTime(r["DataZakaza"]);
                if (from.HasValue && d.Date < from.Value.Date) continue;
                if (to.HasValue && d.Date > to.Value.Date) continue;
                filtered.ImportRow(r);
            }

            dgZakazy.ItemsSource = filtered.DefaultView;
        }

        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) ApplyFilter();
        }

        private void btnResetZ_Click(object sender, RoutedEventArgs e)
        {
            cbStatus.SelectedIndex = 0;
            dpFromZ.SelectedDate = null;
            dpToZ.SelectedDate = null;
            ApplyFilter();
        }
    }
}