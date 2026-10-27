using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace ProdazhaBiletov
{
    public partial class KalendarPage : UserControl
    {
        public KalendarPage()
        {
            InitializeComponent();
            dpKalendarFrom.SelectedDate = DateTime.Today;
            dpKalendarTo.SelectedDate = DateTime.Today.AddDays(90);
            LoadCalendar();
        }

        private void DatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
                LoadCalendar();
        }

        private void LoadCalendar()
        {
            if (!dpKalendarFrom.SelectedDate.HasValue || !dpKalendarTo.SelectedDate.HasValue)
                return;

            DateTime from = dpKalendarFrom.SelectedDate.Value.Date;
            DateTime to = dpKalendarTo.SelectedDate.Value.Date.AddDays(1);

            string sql = "SELECT m.idMeropriyatiya, m.DataVremyaNachala, t.NazvanieTipa, m.Nazvanie, " +
                "p.NazvaniePloshchadki, g.NazvanieGoroda, MIN(c.Cena) AS MinCena " +
                "FROM [322Meropriyatie] m " +
                "JOIN [322TipMeropriyatiya] t ON m.idTipa = t.idTipa " +
                "JOIN [322Zal] z ON m.idZala = z.idZala " +
                "JOIN [322Ploshchadka] p ON z.idPloshchadki = p.idPloshchadki " +
                "JOIN [322Gorod] g ON p.idGoroda = g.idGoroda " +
                "LEFT JOIN [322CenaBileta] c ON m.idMeropriyatiya = c.idMeropriyatiya " +
                "WHERE m.StatusPublikacii = N'Опубликовано' AND m.DataVremyaNachala >= @from AND m.DataVremyaNachala < @to " +
                "GROUP BY m.idMeropriyatiya, m.DataVremyaNachala, t.NazvanieTipa, m.Nazvanie, p.NazvaniePloshchadki, g.NazvanieGoroda " +
                "ORDER BY m.DataVremyaNachala";

            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlCommand cmd = new SqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@from", from);
                    cmd.Parameters.AddWithValue("@to", to);
                    DataTable dt = new DataTable();
                    new SqlDataAdapter(cmd).Fill(dt);
                    dgKalendar.ItemsSource = dt.DefaultView;
                    tbCountK.Text = string.Format("Найдено мероприятий: {0}", dt.Rows.Count);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки календаря: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}