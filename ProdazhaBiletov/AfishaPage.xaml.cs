using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProdazhaBiletov
{
    public partial class AfishaPage : UserControl
    {
        private const int PageSize = 8;

        private DataTable _all;
        private int _page = 1;
        private int _pageCount = 1;
        private bool _loading = true;
        private List<CheckBox> _tipChecks = new List<CheckBox>();

        public AfishaPage()
        {
            InitializeComponent();
            Loaded += (s, e) => InitialLoad();
        }

        private MainWindow Shell
        {
            get { return Window.GetWindow(this) as MainWindow; }
        }

        private void InitialLoad()
        {
            if (_all != null)
                return;

            _loading = true;
            try
            {
                LoadTipi();
                LoadGoroda();
                LoadPloshchadki();
                _loading = false;
                Reload();
            }
            catch (Exception ex)
            {
                _loading = false;
                if (Shell != null) Shell.SetStatus("Ошибка загрузки: " + ex.Message);
                MessageBox.Show("Ошибка подключения к базе данных: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTipi()
        {
            panelTipi.Children.Clear();
            _tipChecks.Clear();

            using (SqlConnection con = DbHelper.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand("SELECT idTipa, NazvanieTipa FROM [322TipMeropriyatiya] ORDER BY NazvanieTipa", con);
                using (SqlDataReader r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        CheckBox cb = new CheckBox
                        {
                            Content = r["NazvanieTipa"].ToString(),
                            Tag = r["idTipa"],
                            Style = (Style)FindResource("CheckBoxFont")
                        };
                        cb.Checked += filter_Changed;
                        cb.Unchecked += filter_Changed;
                        _tipChecks.Add(cb);
                        panelTipi.Children.Add(cb);
                    }
            }
        }

        private void LoadGoroda()
        {
            cbGorod.Items.Add(new ComboItem(0, "Все города"));
            using (SqlConnection con = DbHelper.OpenConnection())
                FillCombo(con, "SELECT idGoroda, NazvanieGoroda FROM [322Gorod] ORDER BY NazvanieGoroda", cbGorod);
            cbGorod.SelectedIndex = 0;
        }

        private void LoadPloshchadki()
        {
            cbPloshchadka.Items.Add(new ComboItem(0, "Все площадки"));
            using (SqlConnection con = DbHelper.OpenConnection())
                FillCombo(con, "SELECT idPloshchadki, NazvaniePloshchadki FROM [322Ploshchadka] ORDER BY NazvaniePloshchadki", cbPloshchadka);
            cbPloshchadka.SelectedIndex = 0;
        }

        private static void FillCombo(SqlConnection con, string sql, ComboBox box)
        {
            SqlCommand cmd = new SqlCommand(sql, con);
            using (SqlDataReader r = cmd.ExecuteReader())
                while (r.Read())
                    box.Items.Add(new ComboItem(Convert.ToInt32(r[0]), r[1].ToString()));
        }

        private void Reload()
        {
            if (_loading)
                return;

            List<int> tipi = _tipChecks.Where(c => c.IsChecked == true).Select(c => Convert.ToInt32(c.Tag)).ToList();
            int gorodId = (cbGorod.SelectedItem as ComboItem) == null ? 0 : ((ComboItem)cbGorod.SelectedItem).Id;
            int ploshchadkaId = (cbPloshchadka.SelectedItem as ComboItem) == null ? 0 : ((ComboItem)cbPloshchadka.SelectedItem).Id;

            decimal priceFrom, priceTo;
            bool hasFrom = decimal.TryParse(tbPriceFrom.Text.Trim(), out priceFrom);
            bool hasTo = decimal.TryParse(tbPriceTo.Text.Trim(), out priceTo);

            if (hasFrom && hasTo && priceFrom > priceTo)
            {
                tbCount.Text = "Цена «от» больше цены «до» — проверьте диапазон";
                listMeropriyatiya.ItemsSource = null;
                tbEmpty.Visibility = Visibility.Visible;
                return;
            }

            string where = "WHERE m.StatusPublikacii = N'Опубликовано' AND m.DataVremyaNachala >= @now";
            SqlCommand cmd = new SqlCommand();
            cmd.Parameters.AddWithValue("@now", DateTime.Now);

            if (tbSearch.Text.Trim() != "")
            {
                where += " AND m.Nazvanie LIKE @search";
                cmd.Parameters.AddWithValue("@search", "%" + tbSearch.Text.Trim() + "%");
            }
            if (tipi.Count > 0)
            {
                where += " AND m.idTipa IN (" + string.Join(",", tipi) + ")";
            }
            if (dpFrom.SelectedDate.HasValue)
            {
                where += " AND m.DataVremyaNachala >= @from";
                cmd.Parameters.AddWithValue("@from", dpFrom.SelectedDate.Value.Date);
            }
            if (dpTo.SelectedDate.HasValue)
            {
                where += " AND m.DataVremyaNachala < @to";
                cmd.Parameters.AddWithValue("@to", dpTo.SelectedDate.Value.Date.AddDays(1));
            }
            if (gorodId > 0)
            {
                where += " AND p.idGoroda = @gorod";
                cmd.Parameters.AddWithValue("@gorod", gorodId);
            }
            if (ploshchadkaId > 0)
            {
                where += " AND p.idPloshchadki = @ploshchadka";
                cmd.Parameters.AddWithValue("@ploshchadka", ploshchadkaId);
            }
            if (hasFrom)
            {
                where += " AND mn.MinCena >= @priceFrom";
                cmd.Parameters.AddWithValue("@priceFrom", priceFrom);
            }
            if (hasTo)
            {
                where += " AND mn.MinCena <= @priceTo";
                cmd.Parameters.AddWithValue("@priceTo", priceTo);
            }

            string sql =
                "SELECT m.idMeropriyatiya, m.Nazvanie, t.NazvanieTipa, m.DataVremyaNachala, " +
                "p.NazvaniePloshchadki, p.Adres, g.NazvanieGoroda AS Gorod, m.Prodolzhitelnost, m.VozrastnoeOgranichenie, " +
                "z.Vmestimost, z.EstSkhemaMest, mn.MinCena, sb.Sold AS ProdanoBiletov " +
                "FROM [322Meropriyatie] m " +
                "JOIN [322TipMeropriyatiya] t ON m.idTipa = t.idTipa " +
                "JOIN [322Zal] z ON m.idZala = z.idZala " +
                "JOIN [322Ploshchadka] p ON z.idPloshchadki = p.idPloshchadki " +
                "JOIN [322Gorod] g ON p.idGoroda = g.idGoroda " +
                "OUTER APPLY (SELECT MIN(c.Cena) AS MinCena FROM [322CenaBileta] c WHERE c.idMeropriyatiya = m.idMeropriyatiya) mn " +
                "OUTER APPLY (SELECT COUNT(*) AS Sold FROM [322Bilet] b WHERE b.idMeropriyatiya = m.idMeropriyatiya) sb " +
                where;

            using (SqlConnection con = DbHelper.OpenConnection())
            {
                cmd.CommandText = sql;
                cmd.Connection = con;
                _all = new DataTable();
                new SqlDataAdapter(cmd).Fill(_all);
            }

            _all.Columns.Add("SvobodnoMest", typeof(int));
            _all.Columns.Add("EstMesta", typeof(bool));
            foreach (DataRow row in _all.Rows)
            {
                int vmestimost = Convert.ToInt32(row["Vmestimost"]);
                int prodano = row["ProdanoBiletov"] is DBNull ? 0 : Convert.ToInt32(row["ProdanoBiletov"]);
                row["SvobodnoMest"] = Math.Max(0, vmestimost - prodano);
                row["EstMesta"] = Convert.ToBoolean(row["EstSkhemaMest"]);
            }

            SortAll();
            _page = 1;
            ApplyPage();

            if (Shell != null)
                Shell.SetStatus("Загружено мероприятий: " + _all.Rows.Count);
        }

        private int CountFreeSeats(int idMeropriyatiya)
        {
            using (SqlConnection con = DbHelper.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM [322Bilet] WHERE idMeropriyatiya = @id", con);
                cmd.Parameters.AddWithValue("@id", idMeropriyatiya);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private void SortAll()
        {
            DataView view = _all.DefaultView;
            switch (cbSort.SelectedIndex)
            {
                case 1: view.Sort = "MinCena ASC, DataVremyaNachala ASC"; break;
                case 2: view.Sort = "MinCena DESC, DataVremyaNachala ASC"; break;
                case 3: view.Sort = "Nazvanie ASC"; break;
                default: view.Sort = "DataVremyaNachala ASC"; break;
            }
        }

        private void ApplyPage()
        {
            int total = _all.Rows.Count;
            _pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            if (_page > _pageCount) _page = _pageCount;

            DataTable page = _all.Clone();
            foreach (DataRow r in _all.DefaultView.Cast<DataRowView>().Select(v => v.Row)
                                  .Skip((_page - 1) * PageSize).Take(PageSize))
                page.ImportRow(r);

            listMeropriyatiya.ItemsSource = page.Rows.Count == 0 ? null : page.DefaultView;
            tbCount.Text = string.Format("Найдено мероприятий: {0}", total);
            tbEmpty.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
            BuildPages();
        }

        private void BuildPages()
        {
            panelPages.Children.Clear();

            for (int i = 1; i <= _pageCount; i++)
            {
                int n = i;
                Button b = new Button
                {
                    Content = i.ToString(),
                    Style = (Style)FindResource("PageButton"),
                    Tag = i,
                    FontWeight = i == _page ? FontWeights.Bold : FontWeights.Normal
                };
                if (i == _page)
                {
                    b.Background = (System.Windows.Media.Brush)FindResource("MainBrush");
                    b.Foreground = System.Windows.Media.Brushes.White;
                }
                b.Click += (s, e) => { _page = n; ApplyPage(); };
                panelPages.Children.Add(b);
            }

            btnPrev.IsEnabled = _page > 1;
            btnNext.IsEnabled = _page < _pageCount;
        }

        private void filter_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _page = 1;
            Reload();
        }

        private void tbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            _page = 1;
            Reload();
        }

        private void cbSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _all == null) return;
            SortAll();
            _page = 1;
            ApplyPage();
        }

        private void btnShow_Click(object sender, RoutedEventArgs e)
        {
            _page = 1;
            Reload();
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            _loading = true;
            tbSearch.Text = "";
            foreach (CheckBox cb in _tipChecks) cb.IsChecked = false;
            dpFrom.SelectedDate = null;
            dpTo.SelectedDate = null;
            cbGorod.SelectedIndex = 0;
            cbPloshchadka.SelectedIndex = 0;
            tbPriceFrom.Text = "";
            tbPriceTo.Text = "";
            _loading = false;
            _page = 1;
            Reload();
        }

        private void btnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_page > 1) { _page--; ApplyPage(); }
        }

        private void btnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_page < _pageCount) { _page++; ApplyPage(); }
        }

        private void Card_Click(object sender, MouseButtonEventArgs e)
        {
            Border card = sender as Border;
            if (card == null || card.DataContext == null) return;
            var row = card.DataContext as DataRowView;
            if (row == null) return;
            OpenDetails(Convert.ToInt32(row["idMeropriyatiya"]));
        }

        private void btnDetails_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            var row = btn.Tag as DataRowView;
            if (row == null) return;
            OpenDetails(Convert.ToInt32(row["idMeropriyatiya"]));
        }

        private void OpenDetails(int id)
        {
            EventDetailsWindow w = new EventDetailsWindow(id);
            w.Owner = Window.GetWindow(this);
            w.ShowDialog();
            Reload();
        }

        private class ComboItem
        {
            public int Id;
            public string Name;

            public ComboItem(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public override string ToString()
            {
                return Name;
            }
        }
    }
}