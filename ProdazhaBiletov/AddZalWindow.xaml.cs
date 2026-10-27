using System;
using System.Data.SqlClient;
using System.Windows;

namespace ProdazhaBiletov
{
    public partial class AddZalWindow : Window
    {
        public AddZalWindow()
        {
            InitializeComponent();

            using (SqlConnection con = DbHelper.OpenConnection())
            using (SqlDataReader r = new SqlCommand(
                "SELECT p.idPloshchadki, p.NazvaniePloshchadki FROM [322Ploshchadka] p ORDER BY p.NazvaniePloshchadki", con).ExecuteReader())
                while (r.Read())
                    cbPloshchadka.Items.Add(new Item(Convert.ToInt32(r[0]), r[1].ToString()));

            if (cbPloshchadka.Items.Count > 0)
                cbPloshchadka.SelectedIndex = 0;
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
                MessageBox.Show("Укажите название зала", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (cbPloshchadka.SelectedItem == null)
            {
                MessageBox.Show("Выберите площадку", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int vm;
            if (!int.TryParse(tbVmestimost.Text.Trim(), out vm) || vm <= 0)
            {
                MessageBox.Show("Вместимость должна быть положительным числом", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlCommand cmd = new SqlCommand(
                        "INSERT INTO [322Zal] (NazvanieZala, idPloshchadki, Vmestimost, EstSkhemaMest) " +
                        "VALUES (@naz, @pl, @vm, @sh)", con);
                    cmd.Parameters.AddWithValue("@naz", tbNazvanie.Text.Trim());
                    cmd.Parameters.AddWithValue("@pl", ((Item)cbPloshchadka.SelectedItem).Id);
                    cmd.Parameters.AddWithValue("@vm", vm);
                    cmd.Parameters.AddWithValue("@sh", cbShema.IsChecked == true);
                    cmd.ExecuteNonQuery();
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private class Item
        {
            public int Id;
            public string Name;

            public Item(int id, string name)
            {
                Id = id;
                Name = name;
            }

            public override string ToString() { return Name; }
        }
    }
}