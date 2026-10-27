using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;

namespace ProdazhaBiletov
{
    public partial class AdminOtchetPage : UserControl
    {
        private DataTable _report;

        public AdminOtchetPage()
        {
            InitializeComponent();
            Loaded += (s, e) => Load();
        }

        private void Load()
        {
            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    cbMer.Items.Clear();
                    using (SqlDataReader r = new SqlCommand(
                        "SELECT idMeropriyatiya, Nazvanie FROM [322Meropriyatie] ORDER BY DataVremyaNachala DESC", con).ExecuteReader())
                        while (r.Read())
                            cbMer.Items.Add(new CItem(Convert.ToInt32(r[0]), r[1].ToString()));

                    string sql = "SELECT m.idMeropriyatiya, m.DataVremyaNachala, m.Nazvanie, z.NazvanieZala, z.Vmestimost, " +
                        "(SELECT COUNT(*) FROM [322Bilet] b WHERE b.idMeropriyatiya = m.idMeropriyatiya) AS Prodano, " +
                        "(SELECT ISNULL(SUM(b.CenaProdazhi), 0) FROM [322Bilet] b WHERE b.idMeropriyatiya = m.idMeropriyatiya) AS Vyrushka " +
                        "FROM [322Meropriyatie] m JOIN [322Zal] z ON m.idZala = z.idZala " +
                        "WHERE m.StatusPublikacii = N'Опубликовано' " +
                        "ORDER BY m.DataVremyaNachala DESC";
                    _report = new DataTable();
                    new SqlDataAdapter(new SqlCommand(sql, con)).Fill(_report);
                }

                _report.Columns.Add("Zanyatost", typeof(double));
                foreach (DataRow r in _report.Rows)
                {
                    int v = Convert.ToInt32(r["Vmestimost"]);
                    int p = Convert.ToInt32(r["Prodano"]);
                    r["Zanyatost"] = v > 0 ? (double)p / v * 100 : 0;
                }
                dgOtchet.ItemsSource = _report.DefaultView;

                if (cbMer.Items.Count > 0)
                    cbMer.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки отчёта: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void cbMer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CItem item = cbMer.SelectedItem as CItem;
            if (item == null) return;
            LoadDetails(item.Id);
        }

        private void LoadDetails(int id)
        {
            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlCommand cmd = new SqlCommand(
                        "SELECT z.Vmestimost, " +
                        "(SELECT COUNT(*) FROM [322Bilet] b JOIN [322Zakaz] zz ON zz.idZakaza = b.idZakaza " +
                        " WHERE b.idMeropriyatiya = @id AND zz.StatusZakaza = N'Оплачен') AS Prodano, " +
                        "(SELECT COUNT(*) FROM [322Bilet] b JOIN [322Zakaz] zz ON zz.idZakaza = b.idZakaza " +
                        " WHERE b.idMeropriyatiya = @id AND zz.StatusZakaza = N'Забронирован') AS Bronirovano, " +
                        "(SELECT ISNULL(SUM(b.CenaProdazhi), 0) FROM [322Bilet] b JOIN [322Zakaz] zz ON zz.idZakaza = b.idZakaza " +
                        " WHERE b.idMeropriyatiya = @id AND zz.StatusZakaza = N'Оплачен') AS Vyrushka " +
                        "FROM [322Meropriyatie] m JOIN [322Zal] z ON z.idZala = m.idZala " +
                        "WHERE m.idMeropriyatiya = @id", con);
                    cmd.Parameters.AddWithValue("@id", id);

                    using (SqlDataReader r = cmd.ExecuteReader())
                        if (r.Read())
                        {
                            int vmestimost = Convert.ToInt32(r["Vmestimost"]);
                            int prodano = Convert.ToInt32(r["Prodano"]);
                            int bron = Convert.ToInt32(r["Bronirovano"]);
                            decimal vyrushka = Convert.ToDecimal(r["Vyrushka"]);

                            tbProdano.Text = prodano.ToString();
                            tbBron.Text = bron.ToString();
                            tbZanyatost.Text = (vmestimost > 0 ? (double)prodano / vmestimost * 100 : 0).ToString("N1") + "%";
                            tbVyrushka.Text = vyrushka.ToString("N0") + " ₽";
                        }

                    LoadGrafik(con, id);
                    LoadKategorii(con, id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadGrafik(SqlConnection con, int id)
        {
            SqlCommand cmd = new SqlCommand(
                "SELECT CONVERT(date, z.DataZakaza) AS Den, COUNT(b.idBileta) AS Kolichestvo " +
                "FROM [322Bilet] b JOIN [322Zakaz] z ON z.idZakaza = b.idZakaza " +
                "WHERE b.idMeropriyatiya = @id AND z.StatusZakaza = N'Оплачен' " +
                "GROUP BY CONVERT(date, z.DataZakaza) ORDER BY Den", con);
            cmd.Parameters.AddWithValue("@id", id);

            DataTable dt = new DataTable();
            new SqlDataAdapter(cmd).Fill(dt);

            int max = dt.Rows.Count == 0 ? 0 : dt.Rows.Cast<DataRow>().Max(x => Convert.ToInt32(x["Kolichestvo"]));

            List<BarItem> bars = new List<BarItem>();
            foreach (DataRow r in dt.Rows)
            {
                int n = Convert.ToInt32(r["Kolichestvo"]);
                double share = max > 0 ? Math.Max(0.02, (double)n / max) : 0;
                bars.Add(new BarItem
                {
                    Den = Convert.ToDateTime(r["Den"]).ToString("dd.MM"),
                    Kolichestvo = n,
                    FillWidth = new GridLength(share, GridUnitType.Star),
                    RestWidth = new GridLength(1 - share, GridUnitType.Star)
                });
            }

            panelGrafik.ItemsSource = bars;
        }

        private void LoadKategorii(SqlConnection con, int id)
        {
            SqlCommand cmd = new SqlCommand(
                "SELECT ISNULL(k.NazvanieKategorii, N'Без категории') AS NazvanieKategorii, " +
                "COUNT(b.idBileta) AS Prodano, ISNULL(SUM(b.CenaProdazhi), 0) AS Summa " +
                "FROM [322Bilet] b " +
                "JOIN [322Zakaz] z ON z.idZakaza = b.idZakaza AND z.StatusZakaza = N'Оплачен' " +
                "LEFT JOIN [322Mesto] mo ON mo.idMesta = b.idMesta " +
                "LEFT JOIN [322KategoriyaMesta] k ON k.idKategorii = mo.idKategorii " +
                "WHERE b.idMeropriyatiya = @id GROUP BY k.NazvanieKategorii ORDER BY Summa DESC", con);
            cmd.Parameters.AddWithValue("@id", id);

            DataTable dt = new DataTable();
            new SqlDataAdapter(cmd).Fill(dt);
            dgKategorii.ItemsSource = dt.DefaultView;
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_report == null || _report.Rows.Count == 0)
            {
                MessageBox.Show("Нет данных для выгрузки.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "Текстовый файл (*.csv)|*.csv",
                FileName = "Otchet_prodazhi_" + DateTime.Now.ToString("yyyyMMdd") + ".csv"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                using (StreamWriter sw = new StreamWriter(dlg.FileName, false, Encoding.UTF8))
                {
                    sw.WriteLine("Отчёт по продажам билетов");
                    sw.WriteLine("Сформирован: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                    sw.WriteLine();
                    sw.WriteLine("Мероприятие;Зал;Вместимость;Продано;Занятость, %;Выручка, руб.");
                    foreach (DataRow r in _report.Rows)
                        sw.WriteLine(string.Join(";",
                            r["Nazvanie"], r["NazvanieZala"], r["Vmestimost"], r["Prodano"],
                            ((double)r["Zanyatost"]).ToString("N1"), Convert.ToDecimal(r["Vyrushka"]).ToString("N0")));
                }
                MessageBox.Show("Отчёт выгружен: " + dlg.FileName, "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить файл: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (_report == null || _report.Rows.Count == 0)
            {
                MessageBox.Show("Нет данных для печати.", "Печать", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            PrintDialog dlg = new PrintDialog();
            if (dlg.ShowDialog() != true) return;

            FlowDocument doc = BuildPrintDocument();
            doc.PageWidth = dlg.PrintableAreaWidth;
            doc.PageHeight = dlg.PrintableAreaHeight;
            doc.PagePadding = new Thickness(40);
            doc.ColumnWidth = Math.Max(100, dlg.PrintableAreaWidth - 80);

            dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Отчёт по продажам билетов");
        }

        private FlowDocument BuildPrintDocument()
        {
            FlowDocument doc = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 11 };
            doc.Blocks.Add(new Paragraph(new Run("Отчёт по продажам билетов")
            { FontSize = 18, FontWeight = FontWeights.Bold }));
            doc.Blocks.Add(new Paragraph(new Run("Сформирован: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm"))
            { FontSize = 10, Foreground = Brushes.Gray }));
            doc.Blocks.Add(new Paragraph(new Run(string.Empty)));

            System.Windows.Documents.Table table = new System.Windows.Documents.Table();
            table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
            table.Columns.Add(new TableColumn { Width = new GridLength(110) });
            table.Columns.Add(new TableColumn { Width = new GridLength(90) });
            table.Columns.Add(new TableColumn { Width = new GridLength(90) });
            table.Columns.Add(new TableColumn { Width = new GridLength(110) });

            TableRowGroup group = new TableRowGroup();
            table.RowGroups.Add(group);
            group.Rows.Add(HeaderRow("Мероприятие", "Зал", "Продано", "Занятость", "Выручка"));

            foreach (DataRow r in _report.Rows)
            {
                TableRow row = new TableRow();
                row.Cells.Add(Cell(r["Nazvanie"].ToString()));
                row.Cells.Add(Cell(r["NazvanieZala"].ToString()));
                row.Cells.Add(Cell(r["Prodano"].ToString()));
                row.Cells.Add(Cell(((double)r["Zanyatost"]).ToString("N1") + "%"));
                row.Cells.Add(Cell(Convert.ToDecimal(r["Vyrushka"]).ToString("N0") + " руб."));
                group.Rows.Add(row);
            }

            doc.Blocks.Add(table);
            return doc;
        }

        private static TableCell Cell(string text)
        {
            TableCell cell = new TableCell(new Paragraph(new Run(text) { FontSize = 10 }));
            cell.BorderThickness = new Thickness(0.5);
            cell.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDF, 0xE2, 0xEA));
            cell.Padding = new Thickness(4, 2, 4, 2);
            return cell;
        }

        private static TableRow HeaderRow(params string[] titles)
        {
            TableRow row = new TableRow();
            foreach (string t in titles)
            {
                TableCell cell = new TableCell(new Paragraph(new Run(t)
                { FontWeight = FontWeights.Bold, FontSize = 10 }));
                cell.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF7, 0xFB));
                cell.BorderThickness = new Thickness(0.5);
                cell.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDF, 0xE2, 0xEA));
                cell.Padding = new Thickness(4, 2, 4, 2);
                row.Cells.Add(cell);
            }
            return row;
        }

        public class BarItem
        {
            public string Den { get; set; }
            public int Kolichestvo { get; set; }
            public GridLength FillWidth { get; set; }
            public GridLength RestWidth { get; set; }
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