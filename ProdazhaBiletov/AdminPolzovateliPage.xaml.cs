using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace ProdazhaBiletov
{
    public partial class AdminPolzovateliPage : UserControl
    {
        private DataTable _all;
        private bool _loading = true;

        public AdminPolzovateliPage()
        {
            InitializeComponent();

            cbRol.Items.Add("Все роли");
            cbRol.SelectedIndex = 0;

            cbState.Items.Add("Все");
            cbState.Items.Add("Активные");
            cbState.Items.Add("Заблокированные");
            cbState.SelectedIndex = 0;

            _loading = false;
            Loaded += (s, e) => Load();
        }

        private void Load()
        {
            string sql = "SELECT p.idPolzovatelya, p.Familiya, p.Imya, p.Otchestvo, p.Telefon, p.Email, " +
                "r.NazvanieRoli, p.idRoli, p.DataRegistracii, p.Zablokirovan, " +
                "(SELECT COUNT(*) FROM [322Zakaz] z WHERE z.idPolzovatelya = p.idPolzovatelya) AS KolZakazov " +
                "FROM [322Polzovatel] p JOIN [322Rol] r ON r.idRoli = p.idRoli ORDER BY p.DataRegistracii DESC";

            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    _all = new DataTable();
                    new SqlDataAdapter(new SqlCommand(sql, con)).Fill(_all);
                    FillRoleFilter(con);
                }
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FillRoleFilter(SqlConnection con)
        {
            if (cbRol.Items.Count > 1) return;

            bool prev = _loading;
            _loading = true;
            using (SqlDataReader r = new SqlCommand("SELECT NazvanieRoli FROM [322Rol] ORDER BY idRoli", con).ExecuteReader())
                while (r.Read())
                    cbRol.Items.Add(r["NazvanieRoli"].ToString());
            cbRol.SelectedIndex = 0;
            _loading = prev;
        }

        private void ApplyFilter()
        {
            if (_all == null) return;

            string rol = cbRol.SelectedIndex > 0 ? cbRol.SelectedItem.ToString() : null;
            int state = cbState.SelectedIndex;
            string search = tbSearch.Text.Trim();

            DataTable filtered = _all.Clone();
            foreach (DataRow r in _all.Rows)
            {
                if (rol != null && r["NazvanieRoli"].ToString() != rol) continue;
                bool blocked = Convert.ToBoolean(r["Zablokirovan"]);
                if (state == 1 && blocked) continue;
                if (state == 2 && !blocked) continue;
                if (search != "")
                {
                    string fio = r["Familiya"].ToString() + " " + r["Imya"].ToString() + " " + Convert.ToString(r["Otchestvo"]);
                    if (fio.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                        && r["Email"].ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                        && Convert.ToString(r["Telefon"]).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }
                filtered.ImportRow(r);
            }
            dgUsers.ItemsSource = filtered.DefaultView;
        }

        private void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loading && IsLoaded) ApplyFilter();
        }

        private void tbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_loading && IsLoaded) ApplyFilter();
        }

        private void btnRole_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            DataRowView row = btn.Tag as DataRowView;
            if (row == null) return;

            int id = Convert.ToInt32(row["idPolzovatelya"]);
            int cur = Convert.ToInt32(row["idRoli"]);
            string fio = row["Familiya"].ToString() + " " + row["Imya"].ToString();

            if (id == UserSession.Id)
            {
                MessageBox.Show("Нельзя изменить роль собственной учётной записи.", "Действие невозможно",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DataTable roles = new DataTable();
            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                    new SqlDataAdapter(new SqlCommand("SELECT idRoli, NazvanieRoli FROM [322Rol] ORDER BY idRoli", con)).Fill(roles);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить роли: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ContextMenu menu = new ContextMenu();
            foreach (DataRow r in roles.Rows)
            {
                int roleId = Convert.ToInt32(r["idRoli"]);
                string roleName = r["NazvanieRoli"].ToString();

                MenuItem item = new MenuItem { Header = roleName, IsChecked = roleId == cur };
                item.Click += (s, a) => ChangeRole(id, roleId, roleName, fio, cur);
                menu.Items.Add(item);
            }

            menu.PlacementTarget = btn;
            menu.IsOpen = true;
        }

        private void ChangeRole(int userId, int newRoleId, string roleName, string fio, int currentRoleId)
        {
            if (newRoleId == currentRoleId) return;

            if (MessageBox.Show("Назначить пользователю " + fio + " роль «" + roleName + "»?", "Смена роли",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlCommand cmd = new SqlCommand("UPDATE [322Polzovatel] SET idRoli = @r WHERE idPolzovatelya = @id", con);
                    cmd.Parameters.AddWithValue("@r", newRoleId);
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось изменить роль: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Load();
        }

        private void btnBlock_Click(object sender, RoutedEventArgs e)
        {
            DataRowView row = ((Button)sender).Tag as DataRowView;
            if (row == null) return;

            int id = Convert.ToInt32(row["idPolzovatelya"]);
            if (id == UserSession.Id)
            {
                MessageBox.Show("Нельзя заблокировать собственную учётную запись.", "Действие невозможно",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool blocked = Convert.ToBoolean(row["Zablokirovan"]);
            string fio = row["Familiya"].ToString() + " " + row["Imya"].ToString();

            if (!blocked)
            {
                int active = 0;
                using (SqlConnection con = DbHelper.OpenConnection())
                {
                    SqlCommand cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM [322Zakaz] WHERE idPolzovatelya = @id AND StatusZakaza = N'Забронирован'", con);
                    cmd.Parameters.AddWithValue("@id", id);
                    active = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (active > 0)
                {
                    MessageBox.Show("У пользователя " + fio + " есть активные брони (" + active + "). Отмените их перед блокировкой.",
                        "Действие невозможно", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            string question = blocked
                ? "Разблокировать пользователя " + fio + "?"
                : "Заблокировать пользователя " + fio + "? Он не сможет войти в систему и оформить заказ.";

            if (MessageBox.Show(question, "Управление доступом", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            using (SqlConnection con = DbHelper.OpenConnection())
            {
                SqlCommand cmd = new SqlCommand("UPDATE [322Polzovatel] SET Zablokirovan = @b WHERE idPolzovatelya = @id", con);
                cmd.Parameters.AddWithValue("@b", !blocked);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }

            Load();
        }
    }
}