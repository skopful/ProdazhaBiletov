using System;
using System.Data.SqlClient;
using System.Windows;

namespace ProdazhaBiletov
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            tbError.Text = "";

            string email = tbEmail.Text.Trim();
            string password = pbPassword.Password;

            if (email == "" || password == "")
            {
                tbError.Text = "Введите электронную почту и пароль";
                return;
            }

            if (!email.Contains("@") || !email.Contains("."))
            {
                tbError.Text = "Электронная почта указана неверно";
                return;
            }

            try
            {
                using (SqlConnection connection = DbHelper.OpenConnection())
                {
                    SqlCommand cmd = new SqlCommand(
                        "SELECT p.idPolzovatelya, p.Familiya, p.Imya, p.Otchestvo, p.Zablokirovan, r.NazvanieRoli, r.idRoli " +
                        "FROM [322Polzovatel] p JOIN [322Rol] r ON r.idRoli = p.idRoli " +
                        "WHERE p.Email = @email AND p.ParolHash = @hash", connection);
                    cmd.Parameters.AddWithValue("@email", email);
                    cmd.Parameters.AddWithValue("@hash", DbHelper.HashPassword(password));

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            tbError.Text = "Неверный логин или пароль";
                            return;
                        }

                        if (Convert.ToBoolean(reader["Zablokirovan"]))
                        {
                            tbError.Text = "Учетная запись заблокирована";
                            return;
                        }

                        UserSession.Id = Convert.ToInt32(reader["idPolzovatelya"]);
                        UserSession.Familiya = reader["Familiya"].ToString();
                        UserSession.Imya = reader["Imya"].ToString();
                        UserSession.Otchestvo = Convert.ToString(reader["Otchestvo"]);
                        UserSession.RoleId = Convert.ToInt32(reader["idRoli"]);
                        UserSession.Role = reader["NazvanieRoli"].ToString();
                    }
                }

                MainWindow main = new MainWindow();
                main.Show();
                Close();
                Application.Current.MainWindow = main;
            }
            catch (Exception ex)
            {
                tbError.Text = "Не удалось связаться с сервером: " + ex.Message;
            }
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            RegisterWindow register = new RegisterWindow();
            register.ShowDialog();
        }
    }
}