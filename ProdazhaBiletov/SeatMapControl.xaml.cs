using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProdazhaBiletov
{
    public partial class SeatMapControl : UserControl
    {
        public enum SeatState
        {
            Free,
            Selected,
            Booked,
            Sold
        }

        public class SeatInfo
        {
            public int IdMesta { get; set; }
            public int NomerRyada { get; set; }
            public int NomerMesta { get; set; }
            public int IdKategorii { get; set; }
            public string Kategoriya { get; set; }
            public decimal Cena { get; set; }
            public SeatState Sostoyanie { get; set; }
            public Border Element { get; set; }
        }

        private int _idZala;
        private bool _interactive = true;
        private readonly Dictionary<int, SeatInfo> _seats = new Dictionary<int, SeatInfo>();

        public IList<SeatInfo> SelectedSeats
        {
            get { return _seats.Values.Where(s => s.Sostoyanie == SeatState.Selected).OrderBy(s => s.NomerRyada).ThenBy(s => s.NomerMesta).ToList(); }
        }

        public decimal Total
        {
            get { return SelectedSeats.Sum(s => s.Cena); }
        }

        public int FreeCount
        {
            get { return _seats.Values.Count(s => s.Sostoyanie == SeatState.Free); }
        }
        public event EventHandler SelectionChanged;

        public SeatMapControl()
        {
            InitializeComponent();
        }
        public void Load(int idZala, int idMeropriyatiya, bool interactive)
        {
            _idZala = idZala;
            _interactive = interactive;
            _seats.Clear();
            panelRyady.Children.Clear();

            try
            {
                if (idMeropriyatiya > 0)
                    EnsureEventSeats(idZala, idMeropriyatiya);

                string source = idMeropriyatiya > 0
                    ? "[322MestoMeropriyatiya] em JOIN [322Mesto] mo ON mo.idMesta = em.idMesta"
                    : "[322Mesto] mo";
                string seatId = idMeropriyatiya > 0 ? "em.idMesta" : "mo.idMesta";
                string categoryId = idMeropriyatiya > 0 ? "em.idKategorii" : "mo.idKategorii";
                string rowNumber = idMeropriyatiya > 0 ? "em.NomerRyada" : "mo.NomerRyada";
                string seatNumber = idMeropriyatiya > 0 ? "em.NomerMesta" : "mo.NomerMesta";
                string sql =
                    "SELECT " + seatId + " AS idMesta, " + categoryId + " AS idKategorii, " +
                    rowNumber + " AS NomerRyada, " + seatNumber + " AS NomerMesta, " +
                    "       k.NazvanieKategorii, " +
                    "       ISNULL(c.Cena, 0) AS Cena, " +
                    "       ISNULL(b.Sostoyanie, 0) AS Sostoyanie " +
                    "FROM " + source + " " +
                    "LEFT JOIN [322KategoriyaMesta] k ON k.idKategorii = " + categoryId + " " +
                    "LEFT JOIN [322CenaBileta] c ON c.idKategorii = " + categoryId + " AND c.idMeropriyatiya = @mer " +
                    "OUTER APPLY (SELECT TOP 1 CASE WHEN z.StatusZakaza = N'Оплачен' THEN 3 " +
                    "                             WHEN z.StatusZakaza = N'Забронирован' AND z.SrokBroni > GETDATE() THEN 2 " +
                    "                             ELSE 0 END AS Sostoyanie " +
                    "              FROM [322Bilet] bb JOIN [322Zakaz] z ON z.idZakaza = bb.idZakaza " +
                    "              WHERE bb.idMesta = " + seatId + " AND bb.idMeropriyatiya = @mer " +
                    "              ORDER BY z.DataZakaza DESC) b " +
                    "WHERE mo.idZala = @zal " +
                    "ORDER BY " + rowNumber + ", " + seatNumber;

                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlCommand cmd = new SqlCommand(sql, con);
                    cmd.Parameters.AddWithValue("@zal", idZala);
                    cmd.Parameters.AddWithValue("@mer", idMeropriyatiya);

                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            SeatInfo s = new SeatInfo
                            {
                                IdMesta = Convert.ToInt32(r["idMesta"]),
                                IdKategorii = Convert.ToInt32(r["idKategorii"]),
                                NomerRyada = Convert.ToInt32(r["NomerRyada"]),
                                NomerMesta = Convert.ToInt32(r["NomerMesta"]),
                                Kategoriya = Convert.ToString(r["NazvanieKategorii"]),
                                Cena = r["Cena"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Cena"]),
                                Sostoyanie = (SeatState)Convert.ToInt32(r["Sostoyanie"])
                            };
                            _seats[s.IdMesta] = s;
                        }
                }

                Build();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить схему зала: " + ex.Message, "Схема зала",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static void EnsureEventSeats(int idZala, int idMeropriyatiya)
        {
            using (SqlConnection con = DbHelper.OpenConnection())
            {
                SqlCommand count = new SqlCommand(
                    "SELECT COUNT(*) FROM [322MestoMeropriyatiya] WHERE idMeropriyatiya=@mer", con);
                count.Parameters.AddWithValue("@mer", idMeropriyatiya);
                if (Convert.ToInt32(count.ExecuteScalar()) > 0) return;

                SqlCommand copy = new SqlCommand(
                    "INSERT INTO [322MestoMeropriyatiya] (idMeropriyatiya,idMesta,idZala,idKategorii,NomerRyada,NomerMesta) " +
                    "SELECT @mer,idMesta,idZala,idKategorii,NomerRyada,NomerMesta FROM [322Mesto] WHERE idZala=@zal", con);
                copy.Parameters.AddWithValue("@mer", idMeropriyatiya);
                copy.Parameters.AddWithValue("@zal", idZala);
                copy.ExecuteNonQuery();
            }
        }

        private void Build()
        {
            bool empty = _seats.Count == 0;
            tbEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            scrollSeats.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            UpdateCounter();
            if (empty) return;
            int maxSeats = _seats.Values.Max(s => s.NomerMesta);
            int perRow = Math.Max(1, maxSeats);

            foreach (IGrouping<int, SeatInfo> row in _seats.Values.OrderBy(s => s.NomerRyada).GroupBy(s => s.NomerRyada))
            {
                StackPanel line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

                TextBlock num = new TextBlock
                {
                    Text = row.Key.ToString(),
                    Style = (Style)FindResource("SeatNumber")
                };
                line.Children.Add(num);

                int last = 0;
                foreach (SeatInfo s in row.OrderBy(x => x.NomerMesta))
                {
                    if (last > 0 && s.NomerMesta - last > 1)
                        line.Children.Add(MakeAisle(s.NomerMesta - last - 1));
                    else if (last > 0 && s.NomerMesta - last == 1 && perRow > 8 && s.NomerMesta % (perRow / 2 + 1) == 0)
                        line.Children.Add(MakeAisle(1));

                    line.Children.Add(MakeSeat(s));
                    last = s.NomerMesta;
                }

                panelRyady.Children.Add(line);
            }
        }

        private Border MakeAisle(int width)
        {
            return new Border
            {
                Width = width * 24 + (width - 1) * 4,
                Height = 20,
                Background = (Brush)FindResource("AisleBrush"),
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(2, 0, 2, 0)
            };
        }

        private Border MakeSeat(SeatInfo s)
        {
            Border box = new Border
            {
                Style = (Style)FindResource("SeatBox"),
                Child = new TextBlock
                {
                    Text = s.NomerMesta.ToString(),
                    Style = (Style)FindResource("SeatLabel"),
                    Foreground = Brushes.White
                },
                Tag = s
            };
            s.Element = box;

            if (_interactive && s.Sostoyanie == SeatState.Free)
                box.MouseLeftButtonUp += Seat_Click;

            Paint(box, s.Sostoyanie);
            box.ToolTip = string.Format("Ряд {0}, место {1}\n{2}\n{3:N0} ₽",
                s.NomerRyada, s.NomerMesta, s.Kategoriya, s.Cena);
            return box;
        }

        private void Seat_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Border box = sender as Border;
            if (box == null) return;

            SeatInfo s = box.Tag as SeatInfo;
            if (s == null || !_interactive) return;
            if (s.Sostoyanie == SeatState.Sold || s.Sostoyanie == SeatState.Booked) return;

            s.Sostoyanie = s.Sostoyanie == SeatState.Selected ? SeatState.Free : SeatState.Selected;
            Paint(box, s.Sostoyanie);
            UpdateCounter();

            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        private void Paint(Border box, SeatState state)
        {
            switch (state)
            {
                case SeatState.Free:
                    box.Background = (Brush)FindResource("SeatFreeBrush");
                    box.BorderBrush = (Brush)FindResource("SeatFreeBorderBrush");
                    box.Cursor = System.Windows.Input.Cursors.Hand;
                    SetLabel(box, (Brush)FindResource("TextBrush"));
                    break;
                case SeatState.Selected:
                    box.Background = (Brush)FindResource("SeatSelectedBrush");
                    box.BorderBrush = (Brush)FindResource("SeatSelectedBrush");
                    box.Cursor = System.Windows.Input.Cursors.Hand;
                    SetLabel(box, Brushes.White);
                    break;
                case SeatState.Booked:
                    box.Background = (Brush)FindResource("SeatBookedBrush");
                    box.BorderBrush = (Brush)FindResource("SeatBookedBrush");
                    box.Cursor = System.Windows.Input.Cursors.No;
                    SetLabel(box, (Brush)FindResource("TextBrush"));
                    break;
                case SeatState.Sold:
                    box.Background = (Brush)FindResource("SeatSoldBrush");
                    box.BorderBrush = (Brush)FindResource("SeatSoldBrush");
                    box.Cursor = System.Windows.Input.Cursors.No;
                    SetLabel(box, Brushes.White);
                    break;
            }
        }

        private static void SetLabel(Border box, Brush brush)
        {
            TextBlock tb = box.Child as TextBlock;
            if (tb != null) tb.Foreground = brush;
        }

        private void UpdateCounter()
        {
            int sold = _seats.Values.Count(s => s.Sostoyanie == SeatState.Sold);
            int booked = _seats.Values.Count(s => s.Sostoyanie == SeatState.Booked);
            int free = FreeCount;
            int sel = SelectedSeats.Count;
            tbCounter.Text = string.Format("Свободно {0}   •   Забронировано {1}   •   Продано {2}   •   Выбрано {3}",
                free, booked, sold, sel);
        }
    }
}