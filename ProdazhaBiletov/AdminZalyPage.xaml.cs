using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace ProdazhaBiletov
{
    public partial class AdminZalyPage : UserControl
    {
        public AdminZalyPage()
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
                    DataTable z = new DataTable();
                    new SqlDataAdapter(new SqlCommand(
                        "SELECT idZala, NazvanieZala, Vmestimost, EstSkhemaMest, " +
                        "(SELECT COUNT(*) FROM [322Mesto] m WHERE m.idZala = z.idZala) AS KolMest " +
                        "FROM [322Zal] z ORDER BY NazvanieZala", con)).Fill(z);
                    dgZaly.ItemsSource = z.DefaultView;

                    DataTable p = new DataTable();
                    new SqlDataAdapter(new SqlCommand(
                        "SELECT p.idPloshchadki, p.NazvaniePloshchadki, p.Adres, g.NazvanieGoroda, " +
                        "(SELECT COUNT(*) FROM [322Zal] z WHERE z.idPloshchadki = p.idPloshchadki) AS KolZalov " +
                        "FROM [322Ploshchadka] p JOIN [322Gorod] g ON p.idGoroda = g.idGoroda " +
                        "ORDER BY p.NazvaniePloshchadki", con)).Fill(p);
                    dgPloshchadki.ItemsSource = p.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnAddZal_Click(object sender, RoutedEventArgs e)
        {
            AddZalWindow w = new AddZalWindow();
            w.Owner = Window.GetWindow(this);
            if (w.ShowDialog() == true)
                Load();
        }

        private void btnShema_Click(object sender, RoutedEventArgs e)
        {
            DataRowView row = ((Button)sender).Tag as DataRowView;
            if (row == null) return;

            ShemaZalaWindow w = new ShemaZalaWindow(Convert.ToInt32(row["idZala"]), row["NazvanieZala"].ToString());
            w.Owner = Window.GetWindow(this);
            w.ShowDialog();
            Load();
        }

        private void btnDelZal_Click(object sender, RoutedEventArgs e)
        {
            DataRowView row = ((Button)sender).Tag as DataRowView;
            if (row == null) return;

            int id = Convert.ToInt32(row["idZala"]);
            string name = row["NazvanieZala"].ToString();

            using (SqlConnection con = DbHelper.OpenConnection())
            {
                SqlCommand chk = new SqlCommand("SELECT COUNT(*) FROM [322Meropriyatie] WHERE idZala = @id", con);
                chk.Parameters.AddWithValue("@id", id);
                if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                {
                    MessageBox.Show("Зал «" + name + "» используется в мероприятиях — удалить его нельзя.",
                        "Удаление невозможно", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            if (MessageBox.Show("Удалить зал «" + name + "»?", "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlTransaction tx = con.BeginTransaction();
                    SqlCommand cmd = new SqlCommand(
                        "IF OBJECT_ID(N'[dbo].[322MestoMeropriyatiya]', N'U') IS NOT NULL " +
                        "DELETE FROM [322MestoMeropriyatiya] WHERE idZala = @id", con, tx);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();

                    cmd = new SqlCommand("DELETE FROM [322Mesto] WHERE idZala = @id", con, tx);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();

                    cmd = new SqlCommand("DELETE FROM [322Zal] WHERE idZala = @id", con, tx);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                    tx.Commit();
                }

                Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось удалить зал: " + ex.Message, "Удаление",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}