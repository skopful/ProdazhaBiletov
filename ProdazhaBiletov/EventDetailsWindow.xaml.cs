using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ProdazhaBiletov
{
    public partial class EventDetailsWindow : Window
    {
        private readonly int _id;
        private int _idZala;
        private int _vmestimost;
        private string _nazvanie = "";
        private string _dataVremya = "";
        private SeatMapControl _map;
        private bool _loaded;
        private bool _canOrder = true;
        private readonly bool _cashierMode;

        public EventDetailsWindow(int idMeropriyatiya, bool cashierMode = false)
        {
            InitializeComponent();
            _id = idMeropriyatiya;
            _cashierMode = cashierMode;

            if (_cashierMode)
            {
                btnBook.Visibility = Visibility.Collapsed;
                btnBuy.Content = "Продать билеты";
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_loaded) return;
            _loaded = true;
            LoadDetails();
        }

        private void LoadDetails()
        {
            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    string sql = "SELECT m.Nazvanie, t.NazvanieTipa, m.DataVremyaNachala, m.Prodolzhitelnost, " +
                        "m.VozrastnoeOgranichenie, m.Opisanie, m.Afisha, z.idZala, z.NazvanieZala, " +
                        "z.Vmestimost, z.EstSkhemaMest, p.NazvaniePloshchadki, p.Adres, g.NazvanieGoroda, " +
                        "(SELECT COUNT(*) FROM [322CenaBileta] c WHERE c.idMeropriyatiya = m.idMeropriyatiya) AS Cen " +
                        "FROM [322Meropriyatie] m " +
                        "JOIN [322TipMeropriyatiya] t ON m.idTipa = t.idTipa " +
                        "JOIN [322Zal] z ON m.idZala = z.idZala " +
                        "JOIN [322Ploshchadka] p ON z.idPloshchadki = p.idPloshchadki " +
                        "JOIN [322Gorod] g ON p.idGoroda = g.idGoroda " +
                        "WHERE m.idMeropriyatiya = @id";

                    SqlCommand cmd = new SqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@id", _id);

                    bool estShema = false;

                    using (SqlDataReader r = cmd.ExecuteReader())
                        if (!r.Read())
                        {
                            MessageBox.Show("Мероприятие не найдено.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            Close();
                            return;
                        }
                        else
                        {
                            DateTime when = Convert.ToDateTime(r["DataVremyaNachala"]);
                            _nazvanie = r["Nazvanie"].ToString();
                            _dataVremya = when.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
                            _idZala = Convert.ToInt32(r["idZala"]);
                            _vmestimost = Convert.ToInt32(r["Vmestimost"]);
                            estShema = Convert.ToBoolean(r["EstSkhemaMest"]);

                            Title = _nazvanie;
                            tbNazvanie.Text = _nazvanie;
                            tbTip.Text = r["NazvanieTipa"].ToString();
                            tbInfo.Text = string.Format(
                                "{0}   •   {1}, {2}, {3}\nПродолжительность: {4} мин.   •   Возраст: {5}+   •   Зал: «{6}»",
                                when.ToString("dddd, dd MMMM yyyy", CultureInfo.CurrentCulture),
                                r["NazvanieGoroda"], r["NazvaniePloshchadki"], r["Adres"],
                                r["Prodolzhitelnost"], r["VozrastnoeOgranichenie"], r["NazvanieZala"]);
                            tbOpisanie.Text = Convert.ToString(r["Opisanie"]);

                            if (Convert.ToInt32(r["Cen"]) == 0)
                            {
                                tbPodskazka.Text = "Цены на мероприятие не заданы — покупка недоступна.";
                                _canOrder = false;
                            }
                        }
                    _map = new SeatMapControl();
                    _map.SelectionChanged += (s, e) => RefreshSelected();
                    seatMap.Content = _map;
                    _map.Load(_idZala, _id, estShema);

                    if (!estShema)
                    {
                        tbPodskazka.Text = "Для этого зала схема мест не используется — продажа идёт по общей вместимости.";
                        _canOrder = false;
                    }

                    RefreshSelected();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить мероприятие: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void RefreshSelected()
        {
            if (_map == null) return;

            IList<SeatMapControl.SeatInfo> sel = _map.SelectedSeats;

            dgSelected.ItemsSource = sel.Count == 0
                ? null
                : sel.Select(s => new
                {
                    Adres = string.Format("Ряд {0}, место {1}", s.NomerRyada, s.NomerMesta),
                    Kategoriya = s.Kategoriya,
                    Cena = s.Cena
                }).ToList();

            tbNet.Visibility = sel.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            tbTotal.Text = _map.Total.ToString("N0") + " ₽";

            int zanyato = _vmestimost > 0 ? (int)Math.Round((double)(_vmestimost - _map.FreeCount) / _vmestimost * 100) : 0;
            tbSvobodno.Text = string.Format("Свободно мест: {0}", _map.FreeCount);
            tbZanyatost.Text = string.Format("Занятость зала: {0}%", zanyato);

            bool can = _canOrder && sel.Count > 0;
            btnBuy.IsEnabled = can;
            btnBook.IsEnabled = can;
        }

        private void btnBuy_Click(object sender, RoutedEventArgs e)
        {
            OpenOrderForm("Оплачен");
        }

        private void btnBook_Click(object sender, RoutedEventArgs e)
        {
            OpenOrderForm("Забронирован");
        }

        private void OpenOrderForm(string status)
        {
            IList<SeatMapControl.SeatInfo> sel = _map.SelectedSeats;
            if (sel.Count == 0) return;

            OformlenieZakazaWindow w = new OformlenieZakazaWindow(_id, _nazvanie, _dataVremya, sel, status, _cashierMode);
            w.Owner = this;
            if (w.ShowDialog() == true)
            {
                _map.Load(_idZala, _id, true);
                RefreshSelected();
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}