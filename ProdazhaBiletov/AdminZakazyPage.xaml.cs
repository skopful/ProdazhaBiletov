using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace ProdazhaBiletov
{
    public partial class AdminZakazyPage : UserControl
    {
        private DataTable _all;

        public AdminZakazyPage()
        {
            InitializeComponent();

            cbStatus.Items.Add("Все статусы");
            cbStatus.Items.Add("Оплачен");
            cbStatus.Items.Add("Забронирован");
            cbStatus.Items.Add("Отменен");
            cbStatus.SelectedIndex = 0;

            Loaded += (s, e) => Load();
        }

        private void Load()
        {
            string sql = "SELECT z.idZakaza, z.NomerZakaza, z.DataZakaza, z.StatusZakaza, z.SummaZakaza, z.SposobOplaty, " +
                "u.Familiya + N' ' + u.Imya AS Polzovatel, " +
                "MIN(m.Nazvanie) AS Meropriyatie, COUNT(b.idBileta) AS Kolichestvo " +
                "FROM [322Zakaz] z " +
                "JOIN [322Polzovatel] u ON u.idPolzovatelya = z.idPolzovatelya " +
                "LEFT JOIN [322Bilet] b ON b.idZakaza = z.idZakaza " +
                "LEFT JOIN [322Meropriyatie] m ON b.idMeropriyatiya = m.idMeropriyatiya " +
                "GROUP BY z.idZakaza, z.NomerZakaza, z.DataZakaza, z.StatusZakaza, z.SummaZakaza, z.SposobOplaty, " +
                "u.Familiya, u.Imya ORDER BY z.DataZakaza DESC";

            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    _all = new DataTable();
                    new SqlDataAdapter(new SqlCommand(sql, con)).Fill(_all);
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

            string status = cbStatus.SelectedIndex > 0 ? cbStatus.SelectedItem.ToString() : null;
            string search = tbSearch.Text.Trim();
            DataTable filtered = _all.Clone();

            foreach (DataRow r in _all.Rows)
            {
                if (status != null && r["StatusZakaza"].ToString() != status) continue;
                if (search != "" &&
                    r["NomerZakaza"].ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    r["Polzovatel"].ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                DateTime d = Convert.ToDateTime(r["DataZakaza"]);
                if (dpFrom.SelectedDate.HasValue && d.Date < dpFrom.SelectedDate.Value.Date) continue;
                if (dpTo.SelectedDate.HasValue && d.Date > dpTo.SelectedDate.Value.Date) continue;
                filtered.ImportRow(r);
            }
            dgOrders.ItemsSource = filtered.DefaultView;
        }

        private void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilter();
        }

        private void tbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilter();
        }

        private void btnMarkPaid_Click(object sender, RoutedEventArgs e)
        {
            DataRowView row = ((Button)sender).Tag as DataRowView;
            if (row == null) return;
            if (row["StatusZakaza"].ToString() == "Оплачен") return;

            SetStatus(row, "Оплачен");
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DataRowView row = ((Button)sender).Tag as DataRowView;
            if (row == null) return;
            if (row["StatusZakaza"].ToString() == "Отменен") return;

            if (MessageBox.Show("Отменить заказ " + row["NomerZakaza"] + "?", "Отмена заказа",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            SetStatus(row, "Отменен");
        }

        private void SetStatus(DataRowView row, string status)
        {
            int id = Convert.ToInt32(row["idZakaza"]);

            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlCommand cmd = new SqlCommand(
                        "UPDATE [322Zakaz] SET StatusZakaza = @st, DataOplaty = @dt WHERE idZakaza = @id", con);
                    cmd.Parameters.AddWithValue("@st", status);
                    cmd.Parameters.AddWithValue("@dt", status == "Оплачен" ? (object)DateTime.Now : DBNull.Value);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
                Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}