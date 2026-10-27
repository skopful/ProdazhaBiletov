using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace ProdazhaBiletov
{
    public partial class AdminMeropriyatiyaPage : UserControl
    {
        private DataTable _all;
        private bool _loading = true;

        public AdminMeropriyatiyaPage()
        {
            InitializeComponent();
            Loaded += (s, e) => Init();
        }

        private void Init()
        {
            if (_all != null) return;

            try
            {
                LoadCombos();
                _loading = false;
                LoadData();
            }
            catch (Exception ex)
            {
                _loading = false;
                MessageBox.Show("Ошибка загрузки: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCombos()
        {
            cbTip.Items.Add(new CItem(0, "Все типы"));
            cbZal.Items.Add(new CItem(0, "Все залы"));
            cbStatus.Items.Add("Все статусы");
            cbStatus.Items.Add("Опубликовано");
            cbStatus.Items.Add("Черновик");
            cbStatus.Items.Add("Архив");

            using (SqlConnection con = DbHelper.OpenConnection())
            {
                using (SqlDataReader r = new SqlCommand("SELECT idTipa, NazvanieTipa FROM [322TipMeropriyatiya] ORDER BY NazvanieTipa", con).ExecuteReader())
                    while (r.Read())
                        cbTip.Items.Add(new CItem(Convert.ToInt32(r[0]), r[1].ToString()));

                using (SqlDataReader r = new SqlCommand("SELECT idZala, NazvanieZala FROM [322Zal] ORDER BY NazvanieZala", con).ExecuteReader())
                    while (r.Read())
                        cbZal.Items.Add(new CItem(Convert.ToInt32(r[0]), r[1].ToString()));
            }

            cbTip.SelectedIndex = 0;
            cbZal.SelectedIndex = 0;
            cbStatus.SelectedIndex = 0;
        }

        private void LoadData()
        {
            string sql = "SELECT m.idMeropriyatiya, m.Nazvanie, t.NazvanieTipa, m.idTipa, " +
                "m.DataVremyaNachala, z.NazvanieZala, m.idZala, m.StatusPublikacii, " +
                "mn.MinCena, mn.MaxCena, sb.Prodano " +
                "FROM [322Meropriyatie] m " +
                "JOIN [322TipMeropriyatiya] t ON m.idTipa = t.idTipa " +
                "JOIN [322Zal] z ON m.idZala = z.idZala " +
                "OUTER APPLY (SELECT MIN(c.Cena) AS MinCena, MAX(c.Cena) AS MaxCena FROM [322CenaBileta] c WHERE c.idMeropriyatiya = m.idMeropriyatiya) mn " +
                "OUTER APPLY (SELECT COUNT(*) AS Prodano FROM [322Bilet] b WHERE b.idMeropriyatiya = m.idMeropriyatiya) sb " +
                "ORDER BY m.DataVremyaNachala DESC";

            using (SqlConnection con = DbHelper.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(sql, con);
                _all = new DataTable();
                new SqlDataAdapter(cmd).Fill(_all);
            }
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_all == null) return;

            int tipId = (cbTip.SelectedItem as CItem) == null ? 0 : ((CItem)cbTip.SelectedItem).Id;
            int zalId = (cbZal.SelectedItem as CItem) == null ? 0 : ((CItem)cbZal.SelectedItem).Id;
            string status = cbStatus.SelectedIndex > 0 ? cbStatus.SelectedItem.ToString() : null;
            string search = tbSearch.Text.Trim();

            DataTable filtered = _all.Clone();
            foreach (DataRow r in _all.Rows)
            {
                if (tipId > 0 && Convert.ToInt32(r["idTipa"]) != tipId) continue;
                if (zalId > 0 && Convert.ToInt32(r["idZala"]) != zalId) continue;
                if (status != null && r["StatusPublikacii"].ToString() != status) continue;
                if (search != "" && r["Nazvanie"].ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                filtered.ImportRow(r);
            }
            dgMer.ItemsSource = filtered.DefaultView;
        }

        private void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loading && IsLoaded) ApplyFilter();
        }

        private void tbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_loading && IsLoaded) ApplyFilter();
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            _loading = true;
            tbSearch.Text = "";
            cbTip.SelectedIndex = 0;
            cbZal.SelectedIndex = 0;
            cbStatus.SelectedIndex = 0;
            _loading = false;
            ApplyFilter();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            MeropriyatieEditWindow w = new MeropriyatieEditWindow(0);
            w.Owner = Window.GetWindow(this);
            if (w.ShowDialog() == true)
                LoadData();
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            DataRowView row = ((Button)sender).Tag as DataRowView;
            if (row == null) return;
            MeropriyatieEditWindow w = new MeropriyatieEditWindow(Convert.ToInt32(row["idMeropriyatiya"]));
            w.Owner = Window.GetWindow(this);
            if (w.ShowDialog() == true)
                LoadData();
        }

        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            DataRowView row = ((Button)sender).Tag as DataRowView;
            if (row == null) return;

            if (MessageBox.Show("Создать копию мероприятия «" + row["Nazvanie"] + "»?", "Копирование",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            int newId;
            using (SqlConnection con = DbHelper.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(
                    "INSERT INTO [322Meropriyatie] (Nazvanie, idTipa, idZala, DataVremyaNachala, Prodolzhitelnost, " +
                    "VozrastnoeOgranichenie, Opisanie, Afisha, StatusPublikacii) " +
                    "SELECT Nazvanie + N' (копия)', idTipa, idZala, DATEADD(day, 7, DataVremyaNachala), Prodolzhitelnost, " +
                    "VozrastnoeOgranichenie, Opisanie, Afisha, N'Черновик' " +
                    "FROM [322Meropriyatie] WHERE idMeropriyatiya = @id; SELECT SCOPE_IDENTITY();", con);
                cmd.Parameters.AddWithValue("@id", Convert.ToInt32(row["idMeropriyatiya"]));
                newId = Convert.ToInt32(cmd.ExecuteScalar());

                SqlCommand pc = new SqlCommand(
                    "INSERT INTO [322CenaBileta] (idMeropriyatiya, idKategorii, Cena) " +
                    "SELECT @new, idKategorii, Cena FROM [322CenaBileta] WHERE idMeropriyatiya = @id", con);
                pc.Parameters.AddWithValue("@new", newId);
                pc.Parameters.AddWithValue("@id", Convert.ToInt32(row["idMeropriyatiya"]));
                pc.ExecuteNonQuery();
            }

            MessageBox.Show("Копия создана как черновик.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadData();
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            DataRowView row = ((Button)sender).Tag as DataRowView;
            if (row == null) return;

            int id = Convert.ToInt32(row["idMeropriyatiya"]);
            string name = row["Nazvanie"].ToString();

            using (SqlConnection con = DbHelper.OpenConnection())
            {
                SqlCommand chk = new SqlCommand(
                    "SELECT COUNT(*) FROM [322Bilet] WHERE idMeropriyatiya = @id", con);
                chk.Parameters.AddWithValue("@id", id);
                int prodano = Convert.ToInt32(chk.ExecuteScalar());

                if (prodano > 0)
                {
                    MessageBox.Show("Нельзя удалить мероприятие: по нему продано " + prodano + " билет(ов).",
                        "Удаление невозможно", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (MessageBox.Show("Удалить мероприятие «" + name + "»?", "Удаление",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            using (SqlConnection con = DbHelper.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand("DELETE FROM [322CenaBileta] WHERE idMeropriyatiya = @id", con);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();

                cmd = new SqlCommand("DELETE FROM [322Meropriyatie] WHERE idMeropriyatiya = @id", con);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }

            LoadData();
        }

        private class CItem
        {
            public int Id;
            public string Name;

            public CItem(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public override string ToString() { return Name; }
        }
    }
}