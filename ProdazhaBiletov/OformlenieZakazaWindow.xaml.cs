using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace ProdazhaBiletov
{
    public partial class OformlenieZakazaWindow : Window
    {
        private readonly int _idMeropriyatiya;
        private readonly string _status;
        private readonly IList<SeatMapControl.SeatInfo> _seats;
        private decimal _summa;
        private readonly bool _cashier;

        public OformlenieZakazaWindow(int idMeropriyatiya, string nazvanie, string dataVremya,
            IList<SeatMapControl.SeatInfo> seats, string status, bool cashierMode = false)
        {
            InitializeComponent();

            _cashier = cashierMode;

            _idMeropriyatiya = idMeropriyatiya;
            _seats = seats;
            _status = status;

            tbMeropriyatie.Text = nazvanie;
            tbData.Text = dataVremya;
            Title = status == "Забронирован" ? "Бронирование билетов" : "Оформление заказа";

            if (_cashier)
                Title = "Продажа билетов в кассе";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_cashier)
                ApplyCashierMode();
            else
                LoadPolzovatel();
            FillSostav();
            UpdateRekvizity();
        }
        private void ApplyCashierMode()
        {
            bdPokupatel.Visibility = Visibility.Collapsed;
            cbSoglasie.Content = "Покупатель ознакомлен с условиями продажи и возврата билетов";
            rbCash.IsChecked = true;
        }

        private void LoadPolzovatel()
        {
            tbFamiliya.Text = UserSession.Familiya ?? "";
            tbImya.Text = UserSession.Imya ?? "";
            tbOtchestvo.Text = UserSession.Otchestvo ?? "";

            if (UserSession.Id == 0) return;

            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlCommand cmd = new SqlCommand(
                        "SELECT Familiya, Imya, Otchestvo, Email, Telefon FROM [322Polzovatel] WHERE idPolzovatelya = @id", con);
                    cmd.Parameters.AddWithValue("@id", UserSession.Id);
                    using (SqlDataReader r = cmd.ExecuteReader())
                        if (r.Read())
                        {
                            tbFamiliya.Text = r["Familiya"].ToString();
                            tbImya.Text = r["Imya"].ToString();
                            tbOtchestvo.Text = Convert.ToString(r["Otchestvo"]);
                            tbEmail.Text = r["Email"].ToString();
                            tbTelefon.Text = r["Telefon"].ToString();
                        }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить профиль: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FillSostav()
        {
            panelSostav.ItemsSource = _seats.Select(s => new
            {
                Adres = string.Format("Ряд {0}, место {1}", s.NomerRyada, s.NomerMesta),
                Kategoriya = s.Kategoriya,
                CenaText = s.Cena.ToString("N0") + " ₽"
            }).ToList();

            _summa = _seats.Sum(s => s.Cena);
            tbItogo.Text = _summa.ToString("N0") + " ₽";
            tbSumma.Text = "Мест: " + _seats.Count;
            tbCount.Text = "Билетов: " + _seats.Count;
        }

        private void rbOplata_Changed(object sender, RoutedEventArgs e)
        {
            UpdateRekvizity();
        }

        private void UpdateRekvizity()
        {
            if (_cashier && rbCash != null && rbCard != null && rbSbp != null &&
                bdRekvizity != null && btnPay != null && tbRekvizity != null)
            {
                bdRekvizity.Visibility = Visibility.Visible;
                btnPay.Content = "Принять оплату и выдать билеты";
                if (rbCash.IsChecked == true)
                    tbRekvizity.Text = "Примите наличные от покупателя и при необходимости выдайте сдачу, затем подтвердите продажу.";
                else if (rbCard.IsChecked == true)
                    tbRekvizity.Text = "Проведите оплату через кассовый терминал и подтвердите продажу после успешной операции.";
                else
                    tbRekvizity.Text = "Покажите покупателю QR-код СБП и подтвердите продажу после поступления оплаты.";
                return;
            }

            if (rbCash != null && rbCash.IsChecked == true)
            {
                bdRekvizity.Visibility = Visibility.Collapsed;
                btnPay.Content = "Подтвердить бронь";
                return;
            }
            if (rbCard == null || rbSbp == null) return;

            bdRekvizity.Visibility = Visibility.Visible;
            btnPay.Content = _status == "Забронирован" ? "Забронировать места" : "Перейти к оплате";
            tbRekvizity.Text = rbCard.IsChecked == true
                ? "Оплата картой через защищённый платёжный шлюз. Данные карты вводятся на стороне банка и не сохраняются."
                : "Оплата по QR-коду через СБП. QR-код формируется после подтверждения заказа.";
        }

        private void cbSoglasie_Changed(object sender, RoutedEventArgs e)
        {
            btnPay.IsEnabled = cbSoglasie.IsChecked == true && _seats.Count > 0;
        }

        private void btnPay_Click(object sender, RoutedEventArgs e)
        {
            if (!Validate()) return;

            string error = SozdatZakaz();
            if (error != null)
            {
                MessageBox.Show(error, "Заказ не создан", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private bool Validate()
        {
            if (_cashier)
            {
                if (cbSoglasie.IsChecked != true)
                {
                    MessageBox.Show("Подтвердите, что покупатель ознакомлен с условиями продажи и возврата билетов.",
                        "Необходимо согласие", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                return true;
            }

            if (string.IsNullOrWhiteSpace(tbFamiliya.Text))
            {
                Warn(tbFamiliya, "Укажите фамилию покупателя.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(tbImya.Text))
            {
                Warn(tbImya, "Укажите имя покупателя.");
                return false;
            }
            if (!Regex.IsMatch(tbEmail.Text.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                Warn(tbEmail, "Укажите корректный адрес электронной почты.");
                return false;
            }
            if (Regex.Replace(tbTelefon.Text.Trim(), @"\D", "").Length < 10)
            {
                Warn(tbTelefon, "Укажите телефон в формате +7 (XXX) XXX-XX-XX.");
                return false;
            }
            if (cbSoglasie.IsChecked != true)
            {
                MessageBox.Show("Подтвердите согласие с условиями продажи и возврата билетов.",
                    "Необходимо согласие", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private static void Warn(Control field, string message)
        {
            field.Focus();
            field.BorderBrush = (System.Windows.Media.Brush)Application.Current.FindResource("ErrorBrush");
            MessageBox.Show(message, "Проверьте данные", MessageBoxButton.OK, MessageBoxImage.Warning);
            field.BorderBrush = (System.Windows.Media.Brush)Application.Current.FindResource("BorderSoftBrush");
        }

        private string SozdatZakaz()
        {
            List<int> taken = new List<int>();
            string nomer = "Z-" + DateTime.Now.ToString("yyMMdd") + "-" +
                Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
            string sposob = rbCash.IsChecked == true ? "Наличные в кассе" : (rbSbp.IsChecked == true ? "СБП" : "Банковская карта");
            bool oplata = _status == "Оплачен";

            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlTransaction tx = con.BeginTransaction();
                    foreach (SeatMapControl.SeatInfo s in _seats)
                    {
                        SqlCommand chk = new SqlCommand(
                            "SELECT COUNT(*) FROM [322Bilet] b JOIN [322Zakaz] z ON z.idZakaza = b.idZakaza " +
                            "WHERE b.idMesta = @mesto AND b.idMeropriyatiya = @mer " +
                            "AND (z.StatusZakaza = N'Оплачен' OR (z.StatusZakaza = N'Забронирован' AND z.SrokBroni > GETDATE()))",
                            con, tx);
                        chk.Parameters.AddWithValue("@mesto", s.IdMesta);
                        chk.Parameters.AddWithValue("@mer", _idMeropriyatiya);
                        if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                            taken.Add(s.NomerMesta);
                    }

                    if (taken.Count > 0)
                    {
                        tx.Rollback();
                        return "Пока вы заполняли форму, места № " + string.Join(", ", taken) +
                            " уже заняты. Выберите их заново.";
                    }
                    SqlCommand chistka = new SqlCommand(
                        "DELETE b FROM [322Bilet] b JOIN [322Zakaz] z ON z.idZakaza = b.idZakaza " +
                        "WHERE b.idMeropriyatiya = @mer AND z.StatusZakaza = N'Забронирован' " +
                        "AND z.SrokBroni <= GETDATE() AND b.idMesta IN (" + Placeholders(_seats.Count) + ")", con, tx);
                    chistka.Parameters.AddWithValue("@mer", _idMeropriyatiya);
                    for (int i = 0; i < _seats.Count; i++)
                        chistka.Parameters.AddWithValue("@m" + i, _seats[i].IdMesta);
                    chistka.ExecuteNonQuery();

                    SqlCommand zakaz = new SqlCommand(
                        "INSERT INTO [322Zakaz] (NomerZakaza, idPolzovatelya, DataZakaza, StatusZakaza, SposobOplaty, SummaZakaza, SrokBroni, DataOplaty) " +
                        "VALUES (@nom, @pol, GETDATE(), @status, @sposob, @summa, @srok, @dataOplaty); SELECT CAST(SCOPE_IDENTITY() AS int);", con, tx);
                    zakaz.Parameters.AddWithValue("@nom", nomer);
                    zakaz.Parameters.AddWithValue("@pol", UserSession.Id);
                    zakaz.Parameters.AddWithValue("@status", _status);
                    zakaz.Parameters.AddWithValue("@sposob", sposob);
                    zakaz.Parameters.AddWithValue("@summa", _summa);
                    zakaz.Parameters.AddWithValue("@srok", oplata ? (object)DBNull.Value : DateTime.Now.AddMinutes(30));
                    zakaz.Parameters.AddWithValue("@dataOplaty", oplata ? (object)DateTime.Now : DBNull.Value);
                    int idZakaza = Convert.ToInt32(zakaz.ExecuteScalar());
                    foreach (SeatMapControl.SeatInfo s in _seats)
                    {
                        SqlCommand bilet = new SqlCommand(
                            "INSERT INTO [322Bilet] (idZakaza, idMeropriyatiya, idMesta, CenaProdazhi, KodBileta, Ispolzovan) " +
                            "VALUES (@zak, @mer, @mesto, @cena, @kod, 0)", con, tx);
                        bilet.Parameters.AddWithValue("@zak", idZakaza);
                        bilet.Parameters.AddWithValue("@mer", _idMeropriyatiya);
                        bilet.Parameters.AddWithValue("@mesto", s.IdMesta);
                        bilet.Parameters.AddWithValue("@cena", s.Cena);
                        bilet.Parameters.AddWithValue("@kod", NewKodBileta());
                        bilet.ExecuteNonQuery();
                    }
                    if (!_cashier)
                    {
                        SqlCommand upd = new SqlCommand(
                            "UPDATE [322Polzovatel] SET Familiya = @f, Imya = @i, Otchestvo = @o, Email = @e, Telefon = @t " +
                            "WHERE idPolzovatelya = @id", con, tx);
                        upd.Parameters.AddWithValue("@f", tbFamiliya.Text.Trim());
                        upd.Parameters.AddWithValue("@i", tbImya.Text.Trim());
                        upd.Parameters.AddWithValue("@o", string.IsNullOrWhiteSpace(tbOtchestvo.Text) ? (object)DBNull.Value : tbOtchestvo.Text.Trim());
                        upd.Parameters.AddWithValue("@e", tbEmail.Text.Trim());
                        upd.Parameters.AddWithValue("@t", tbTelefon.Text.Trim());
                        upd.Parameters.AddWithValue("@id", UserSession.Id);
                        upd.ExecuteNonQuery();
                    }

                    tx.Commit();

                    if (!_cashier)
                    {
                        UserSession.Familiya = tbFamiliya.Text.Trim();
                        UserSession.Imya = tbImya.Text.Trim();
                        UserSession.Otchestvo = tbOtchestvo.Text.Trim();
                    }

                    ShowResult(idZakaza, nomer, sposob, oplata);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("2601") || ex.Message.Contains("2627"))
                    return "Место только что купили или забронировали. Обновите схему и выберите места заново.";
                return "Не удалось сохранить заказ: " + ex.Message;
            }
            return null;
        }

        private static string Placeholders(int count)
        {
            return string.Join(",", Enumerable.Range(0, count).Select(i => "@m" + i));
        }

        private static string NewKodBileta()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
        }

        private void ShowResult(int idZakaza, string nomer, string sposob, bool oplata)
        {
            string srok = oplata
                ? "Заказ оплачен."
                : "Места забронированы до " + DateTime.Now.AddMinutes(30).ToString("HH:mm", CultureInfo.CurrentCulture) +
                  ". Оплатите заказ, чтобы подтвердить бронь.";

            MessageBox.Show(
                "Заказ № " + nomer + "\n" +
                "Билетов: " + _seats.Count + "\n" +
                "Сумма: " + _summa.ToString("N0") + " ₽\n" +
                "Оплата: " + sposob + "\n\n" + srok +
                "\nКод заказа (идентификатор): " + idZakaza,
                oplata ? "Покупка завершена" : "Места забронированы",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}