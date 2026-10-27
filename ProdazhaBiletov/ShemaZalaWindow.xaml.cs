using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ProdazhaBiletov
{
    public partial class ShemaZalaWindow : Window
    {
        private readonly int _idZala;
        public ObservableCollection<KategoriyaItem> Kategorii { get; private set; }

        public ShemaZalaWindow(int idZala, string nazvanieZala)
        {
            InitializeComponent();
            _idZala = idZala;
            Kategorii = new ObservableCollection<KategoriyaItem>();

            tbZal.Text = nazvanieZala;
            Title = "Схема зала — " + nazvanieZala;

            LoadKategorii();
            Load();
            UpdateItog();
        }

        private void LoadKategorii()
        {
            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                using (SqlDataReader r = new SqlCommand(
                    "SELECT idKategorii, NazvanieKategorii FROM [322KategoriyaMesta] ORDER BY NazvanieKategorii", con).ExecuteReader())
                    while (r.Read())
                        Kategorii.Add(new KategoriyaItem
                        {
                            Id = Convert.ToInt32(r[0]),
                            NazvanieKategorii = r[1].ToString()
                        });
                if (Kategorii.Count > 0) Kategorii[0].IsChecked = true;

                panelKategorii.ItemsSource = Kategorii;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки категорий: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Load()
        {
            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlCommand cnt = new SqlCommand(
                        "SELECT COUNT(*), ISNULL(MAX(NomerRyada), 0) FROM [322Mesto] WHERE idZala = @zal", con);
                    cnt.Parameters.AddWithValue("@zal", _idZala);
                    using (SqlDataReader r = cnt.ExecuteReader())
                        if (r.Read())
                            tbInfo.Text = string.Format(
                                "В шаблоне зала: {0} мест, рядов до {1}.\nКаждое мероприятие получает отдельную копию этой схемы.",
                                Convert.ToInt32(r[0]), Convert.ToInt32(r[1]));
                }

                SeatMapControl map = new SeatMapControl();
                map.Load(_idZala, 0, false);
                preview.Content = map;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки схемы: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void tbRyady_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateItog();
        }

        private void tbMesta_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateItog();
        }

        private void UpdateItog()
        {
            if (tbRyady == null || tbMesta == null || tbItog == null)
                return;

            int ryady, mesta;
            if (!int.TryParse(tbRyady.Text.Trim(), out ryady) || !int.TryParse(tbMesta.Text.Trim(), out mesta) || ryady <= 0 || mesta <= 0)
            {
                tbItog.Text = "Укажите положительные числа";
                return;
            }
            tbItog.Text = string.Format("Всего мест: {0}", ryady * mesta);
        }

        private void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            int ryady, mesta;
            if (!int.TryParse(tbRyady.Text.Trim(), out ryady) || ryady <= 0)
            {
                MessageBox.Show("Число рядов должно быть положительным.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(tbMesta.Text.Trim(), out mesta) || mesta <= 0)
            {
                MessageBox.Show("Число мест в ряду должно быть положительным.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ryady > 200 || mesta > 100)
            {
                MessageBox.Show("Слишком большая схема: максимум 200 рядов и 100 мест в ряду.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            List<KategoriyaItem> selected = Kategorii.Where(k => k.IsChecked).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Отметьте хотя бы одну категорию мест.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            using (SqlConnection con = DbHelper.OpenConnection())
            {
                SqlCommand chk = new SqlCommand(
                    "SELECT COUNT(*) FROM [322Bilet] b JOIN [322Meropriyatie] m ON m.idMeropriyatiya = b.idMeropriyatiya " +
                    "WHERE m.idZala = @zal", con);
                chk.Parameters.AddWithValue("@zal", _idZala);
                if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                {
                    MessageBox.Show("По местам этого зала уже оформлены билеты — схему изменить нельзя.",
                        "Изменение невозможно", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (MessageBox.Show(
                    string.Format("Создать {0} мест ({1} рядов по {2})?\nСуществующие места зала будут заменены.", ryady * mesta, ryady, mesta),
                    "Создание схемы", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlTransaction tx = con.BeginTransaction();
                    SqlCommand delEvent = new SqlCommand(
                        "IF OBJECT_ID(N'[dbo].[322MestoMeropriyatiya]', N'U') IS NOT NULL " +
                        "DELETE FROM [322MestoMeropriyatiya] WHERE idZala = @zal", con, tx);
                    delEvent.Parameters.AddWithValue("@zal", _idZala);
                    delEvent.ExecuteNonQuery();

                    SqlCommand del = new SqlCommand("DELETE FROM [322Mesto] WHERE idZala = @zal", con, tx);
                    del.Parameters.AddWithValue("@zal", _idZala);
                    del.ExecuteNonQuery();

                    SqlCommand ins = new SqlCommand(
                        "INSERT INTO [322Mesto] (idZala, idKategorii, NomerRyada, NomerMesta) VALUES (@zal, @kat, @ryad, @mesto)", con, tx);
                    ins.Parameters.AddWithValue("@zal", _idZala);
                    SqlParameter pKat = ins.Parameters.AddWithValue("@kat", 0);
                    SqlParameter pRyad = ins.Parameters.AddWithValue("@ryad", 0);
                    SqlParameter pMesto = ins.Parameters.AddWithValue("@mesto", 0);
                    for (int r = 1; r <= ryady; r++)
                    {
                        int idx = (r - 1) * selected.Count / ryady;
                        if (idx >= selected.Count) idx = selected.Count - 1;

                        for (int m = 1; m <= mesta; m++)
                        {
                            pKat.Value = selected[idx].Id;
                            pRyad.Value = r;
                            pMesto.Value = m;
                            ins.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                }

                Load();
                MessageBox.Show("Схема зала создана.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось создать схему: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            using (SqlConnection con = DbHelper.OpenConnection())
            {
                SqlCommand chk = new SqlCommand(
                    "SELECT COUNT(*) FROM [322Bilet] b JOIN [322Meropriyatie] m ON m.idMeropriyatiya = b.idMeropriyatiya " +
                    "WHERE m.idZala = @zal", con);
                chk.Parameters.AddWithValue("@zal", _idZala);
                if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                {
                    MessageBox.Show("По местам этого зала уже оформлены билеты — удалить их нельзя.",
                        "Удаление невозможно", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (MessageBox.Show("Удалить все места зала?", "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            using (SqlConnection con = DbHelper.OpenConnection())
            {
                SqlCommand delEvent = new SqlCommand(
                    "IF OBJECT_ID(N'[dbo].[322MestoMeropriyatiya]', N'U') IS NOT NULL " +
                    "DELETE FROM [322MestoMeropriyatiya] WHERE idZala = @id", con);
                delEvent.Parameters.AddWithValue("@id", _idZala);
                delEvent.ExecuteNonQuery();

                SqlCommand cmd = new SqlCommand("DELETE FROM [322Mesto] WHERE idZala = @zal", con);
                cmd.Parameters.AddWithValue("@zal", _idZala);
                cmd.ExecuteNonQuery();
            }

            Load();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        public class KategoriyaItem
        {
            public int Id { get; set; }
            public string NazvanieKategorii { get; set; }
            public bool IsChecked { get; set; }
        }
    }
}