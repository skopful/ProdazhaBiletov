using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ProdazhaBiletov
{
    public partial class MeropriyatieEditWindow : Window
    {
        private readonly int _id;
        private ObservableCollection<CenaItem> _ceny;

        public ObservableCollection<ComboItem> Kategorii { get; private set; }

        public MeropriyatieEditWindow(int idMeropriyatiya)
        {
            InitializeComponent();
            _id = idMeropriyatiya;
            _ceny = new ObservableCollection<CenaItem>();
            Kategorii = new ObservableCollection<ComboItem>();

            LoadCategories();
            LoadTipi();
            LoadZaly();
            dgCeny.ItemsSource = _ceny;

            if (_id > 0)
            {
                tbTitle.Text = "Изменение мероприятия";
                LoadMeropriyatie();
                cbStatus.SelectedIndex = StatusIndex(_currentStatus);
            }
            else
            {
                cbStatus.SelectedIndex = 0;
                dpData.SelectedDate = DateTime.Today.AddDays(7);
                AddRow(0);
            }
        }

        private string _currentStatus = "Черновик";

        private int StatusIndex(string status)
        {
            if (status == "Опубликовано") return 1;
            if (status == "Архив") return 2;
            return 0;
        }

        private void LoadCategories()
        {
            using (SqlConnection con = DbHelper.OpenConnection())
            using (SqlDataReader r = new SqlCommand("SELECT idKategorii, NazvanieKategorii FROM [322KategoriyaMesta] ORDER BY NazvanieKategorii", con).ExecuteReader())
                while (r.Read())
                    Kategorii.Add(new ComboItem(Convert.ToInt32(r[0]), r[1].ToString()));
        }

        private void LoadTipi()
        {
            using (SqlConnection con = DbHelper.OpenConnection())
            using (SqlDataReader r = new SqlCommand(
                "SELECT idTipa, NazvanieTipa FROM [322TipMeropriyatiya] ORDER BY NazvanieTipa", con).ExecuteReader())
                while (r.Read())
                    cbTip.Items.Add(new ComboItem(Convert.ToInt32(r[0]), r[1].ToString()));
        }

        private void LoadZaly()
        {
            using (SqlConnection con = DbHelper.OpenConnection())
                using (SqlDataReader r = new SqlCommand("SELECT z.idZala, z.NazvanieZala, z.Vmestimost FROM [322Zal] z ORDER BY z.NazvanieZala", con).ExecuteReader())
                    while (r.Read())
                        cbZal.Items.Add(new ComboItem(Convert.ToInt32(r[0]), r[1].ToString(), r[2].ToString()));

            cbZal.SelectionChanged += (s, e) => UpdateVmestimost();
            if (cbZal.Items.Count > 0)
                cbZal.SelectedIndex = 0;
        }

        private void UpdateVmestimost()
        {
            ComboItem item = cbZal.SelectedItem as ComboItem;
            tbVmestimost.Text = item == null ? "" : item.Capacity;
        }

        private void LoadMeropriyatie()
        {
            using (SqlConnection con = DbHelper.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand(
                    "SELECT Nazvanie, idTipa, idZala, DataVremyaNachala, Prodolzhitelnost, VozrastnoeOgranichenie, " +
                    "Opisanie, Afisha, StatusPublikacii FROM [322Meropriyatie] WHERE idMeropriyatiya = @id", con);
                cmd.Parameters.AddWithValue("@id", _id);
                using (SqlDataReader r = cmd.ExecuteReader())
                    if (r.Read())
                    {
                        tbNazvanie.Text = r["Nazvanie"].ToString();
                        SelectTip(Convert.ToInt32(r["idTipa"]));
                        SelectZal(Convert.ToInt32(r["idZala"]));
                        DateTime d = Convert.ToDateTime(r["DataVremyaNachala"]);
                        dpData.SelectedDate = d.Date;
                        tbVremya.Text = d.ToString("HH:mm", CultureInfo.InvariantCulture);
                        tbProdolzhitelnost.Text = r["Prodolzhitelnost"].ToString();
                        tbVozrast.Text = r["VozrastnoeOgranichenie"].ToString();
                        tbOpisanie.Text = Convert.ToString(r["Opisanie"]);
                        tbAfisha.Text = Convert.ToString(r["Afisha"]);
                        _currentStatus = r["StatusPublikacii"].ToString();
                    }

                SqlCommand pc = new SqlCommand(
                    "SELECT idKategorii, Cena FROM [322CenaBileta] WHERE idMeropriyatiya = @id", con);
                pc.Parameters.AddWithValue("@id", _id);
                using (SqlDataReader r = pc.ExecuteReader())
                    while (r.Read())
                        _ceny.Add(new CenaItem { IdKategorii = Convert.ToInt32(r["idKategorii"]), Cena = Convert.ToDecimal(r["Cena"]) });
            }

            if (_ceny.Count == 0)
                AddRow(0);
        }

        private void SelectTip(int id)
        {
            for (int i = 0; i < cbTip.Items.Count; i++)
                if (((ComboItem)cbTip.Items[i]).Id == id) { cbTip.SelectedIndex = i; return; }
        }

        private void SelectZal(int id)
        {
            for (int i = 0; i < cbZal.Items.Count; i++)
                if (((ComboItem)cbZal.Items[i]).Id == id) { cbZal.SelectedIndex = i; return; }
        }

        private void AddRow(decimal cena)
        {
            _ceny.Add(new CenaItem
            {
                IdKategorii = Kategorii.Count > 0 ? Kategorii.First().Id : 0,
                Cena = cena
            });
        }

        private void btnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            AddRow(0);
        }

        private void btnDelCategory_Click(object sender, RoutedEventArgs e)
        {
            CenaItem item = ((Button)sender).Tag as CenaItem;
            if (item != null)
                _ceny.Remove(item);
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (tbNazvanie.Text.Trim() == "")
            {
                MessageBox.Show("Укажите название мероприятия", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                tbNazvanie.Focus();
                return;
            }
            if (cbTip.SelectedItem == null || cbZal.SelectedItem == null)
            {
                MessageBox.Show("Выберите тип и зал", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!dpData.SelectedDate.HasValue)
            {
                MessageBox.Show("Укажите дату мероприятия", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime time;
            if (!DateTime.TryParseExact(tbVremya.Text.Trim(), "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
                && !DateTime.TryParse(tbVremya.Text.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out time))
            {
                MessageBox.Show("Время указано в неверном формате. Пример: 19:00", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                tbVremya.Focus();
                return;
            }

            int prod;
            if (!int.TryParse(tbProdolzhitelnost.Text.Trim(), out prod) || prod <= 0)
            {
                MessageBox.Show("Продолжительность должна быть положительным числом", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            byte vozr;
            if (!byte.TryParse(tbVozrast.Text.Trim(), out vozr) || vozr > 30)
            {
                MessageBox.Show("Возрастное ограничение — от 0 до 30", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_ceny.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одну категорию мест с ценой", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (CenaItem c in _ceny)
            {
                if (c.IdKategorii <= 0)
                {
                    MessageBox.Show("Выберите категорию мест во всех строках", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (c.Cena < 0)
                {
                    MessageBox.Show("Цена не может быть отрицательной", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            DateTime start = dpData.SelectedDate.Value.Date.Add(time.TimeOfDay);
            string status = (cbStatus.SelectedItem as ComboBoxItem) == null ? "Черновик" : ((ComboBoxItem)cbStatus.SelectedItem).Content.ToString();

            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlTransaction tx = con.BeginTransaction();
                    SqlCommand cmd;
                    if (_id > 0)
                    {
                        cmd = new SqlCommand(
                            "UPDATE [322Meropriyatie] SET Nazvanie=@naz, idTipa=@tip, idZala=@zal, DataVremyaNachala=@data, " +
                            "Prodolzhitelnost=@prod, VozrastnoeOgranichenie=@vozr, Opisanie=@opis, Afisha=@afisha, StatusPublikacii=@stat " +
                            "WHERE idMeropriyatiya=@id", con, tx);
                        cmd.Parameters.AddWithValue("@id", _id);
                    }
                    else
                    {
                        cmd = new SqlCommand(
                            "INSERT INTO [322Meropriyatie] (Nazvanie, idTipa, idZala, DataVremyaNachala, Prodolzhitelnost, " +
                            "VozrastnoeOgranichenie, Opisanie, Afisha, StatusPublikacii) " +
                            "VALUES (@naz, @tip, @zal, @data, @prod, @vozr, @opis, @afisha, @stat); SELECT CAST(SCOPE_IDENTITY() AS int);", con, tx);
                    }

                    cmd.Parameters.AddWithValue("@naz", tbNazvanie.Text.Trim());
                    cmd.Parameters.AddWithValue("@tip", ((ComboItem)cbTip.SelectedItem).Id);
                    cmd.Parameters.AddWithValue("@zal", ((ComboItem)cbZal.SelectedItem).Id);
                    cmd.Parameters.AddWithValue("@data", start);
                    cmd.Parameters.AddWithValue("@prod", prod);
                    cmd.Parameters.AddWithValue("@vozr", vozr);
                    cmd.Parameters.AddWithValue("@opis", tbOpisanie.Text.Trim());
                    cmd.Parameters.AddWithValue("@afisha", string.IsNullOrWhiteSpace(tbAfisha.Text) ? (object)DBNull.Value : tbAfisha.Text.Trim());
                    cmd.Parameters.AddWithValue("@stat", status);

                    int id = _id;
                    if (_id == 0)
                        id = Convert.ToInt32(cmd.ExecuteScalar());
                    else
                        cmd.ExecuteNonQuery();

                    SqlCommand del = new SqlCommand("DELETE FROM [322CenaBileta] WHERE idMeropriyatiya=@id", con, tx);
                    del.Parameters.AddWithValue("@id", id);
                    del.ExecuteNonQuery();

                    foreach (CenaItem c in _ceny)
                    {
                        SqlCommand ins = new SqlCommand(
                            "INSERT INTO [322CenaBileta] (idMeropriyatiya, idKategorii, Cena) VALUES (@id, @kat, @cena)", con, tx);
                        ins.Parameters.AddWithValue("@id", id);
                        ins.Parameters.AddWithValue("@kat", c.IdKategorii);
                        ins.Parameters.AddWithValue("@cena", c.Cena);
                        ins.ExecuteNonQuery();
                    }

                    tx.Commit();
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public class CenaItem
        {
            public int IdKategorii { get; set; }
            public decimal Cena { get; set; }
        }

        public class ComboItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Capacity { get; set; }

            public ComboItem(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public ComboItem(int id, string name, string capacity) : this(id, name)
            {
                Capacity = capacity;
            }

            public override string ToString()
            {
                return Capacity == null ? Name : Name + " (" + Capacity + ")";
            }
        }
    }
}