using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProdazhaBiletov
{
    public partial class CashierProdazhaPage : UserControl
    {
        private DataTable _all;

        public CashierProdazhaPage()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadEvents();
        }

        private void LoadEvents()
        {
            string sql =
                "SELECT m.idMeropriyatiya, m.Nazvanie, t.NazvanieTipa, m.DataVremyaNachala, " +
                "p.NazvaniePloshchadki, z.NazvanieZala, mn.MinCena, " +
                "CASE WHEN z.Vmestimost - sb.Sold < 0 THEN 0 ELSE z.Vmestimost - sb.Sold END AS SvobodnoMest " +
                "FROM [322Meropriyatie] m " +
                "JOIN [322TipMeropriyatiya] t ON m.idTipa = t.idTipa " +
                "JOIN [322Zal] z ON m.idZala = z.idZala " +
                "JOIN [322Ploshchadka] p ON z.idPloshchadki = p.idPloshchadki " +
                "OUTER APPLY (SELECT MIN(c.Cena) AS MinCena FROM [322CenaBileta] c WHERE c.idMeropriyatiya = m.idMeropriyatiya) mn " +
                "OUTER APPLY (SELECT COUNT(*) AS Sold FROM [322Bilet] b JOIN [322Zakaz] zk ON zk.idZakaza = b.idZakaza " +
                "WHERE b.idMeropriyatiya = m.idMeropriyatiya AND (zk.StatusZakaza IN (N'Оплачен', N'Использован') " +
                "OR (zk.StatusZakaza = N'Забронирован' AND zk.SrokBroni > GETDATE()))) sb " +
                "WHERE m.StatusPublikacii = N'Опубликовано' AND m.DataVremyaNachala >= @now " +
                "ORDER BY m.DataVremyaNachala";

            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlCommand cmd = new SqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@now", DateTime.Now);
                    _all = new DataTable();
                    new SqlDataAdapter(cmd).Fill(_all);
                }
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки мероприятий: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            if (_all == null) return;

            string search = tbSearch.Text.Trim();
            DataTable filtered = _all.Clone();

            foreach (DataRow r in _all.Rows)
            {
                if (search != "" &&
                    r["Nazvanie"].ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    r["NazvaniePloshchadki"].ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;

                DateTime d = Convert.ToDateTime(r["DataVremyaNachala"]);
                if (dpFrom.SelectedDate.HasValue && d.Date < dpFrom.SelectedDate.Value.Date) continue;
                if (dpTo.SelectedDate.HasValue && d.Date > dpTo.SelectedDate.Value.Date) continue;

                filtered.ImportRow(r);
            }

            dgEvents.ItemsSource = filtered.DefaultView;
            tbCount.Text = "Мероприятий в продаже: " + filtered.Rows.Count;
        }

        private void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilter();
        }

        private void tbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilter();
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            tbSearch.Text = "";
            dpFrom.SelectedDate = null;
            dpTo.SelectedDate = null;
            ApplyFilter();
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadEvents();
        }

        private void btnSell_Click(object sender, RoutedEventArgs e)
        {
            DataRowView row = ((Button)sender).Tag as DataRowView;
            if (row != null) OpenSale(row);
        }

        private void dgEvents_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataRowView row = dgEvents.SelectedItem as DataRowView;
            if (row != null) OpenSale(row);
        }

        private void OpenSale(DataRowView row)
        {
            EventDetailsWindow w = new EventDetailsWindow(Convert.ToInt32(row["idMeropriyatiya"]), true);
            w.Owner = Window.GetWindow(this);
            w.ShowDialog();
            LoadEvents();
        }
    }
}