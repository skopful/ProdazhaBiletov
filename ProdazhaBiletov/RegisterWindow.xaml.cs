using System;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace ProdazhaBiletov
{
    public partial class RegisterWindow : Window
    {
        public RegisterWindow()
        {
            InitializeComponent();
            tbTelefon.Text = "9";
        }

        private void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            tbError.Text = "";

            string familiya = tbFamiliya.Text.Trim();
            string imya = tbImya.Text.Trim();
            string otchestvo = tbOtchestvo.Text.Trim();
            string email = tbEmail.Text.Trim();
            string telefon = "+7" + tbTelefon.Text.Trim();
            string password = pbPassword.Password;

            if (familiya == "" || imya == "" || email == "" || tbTelefon.Text.Trim() == "" || password == "")
            {
                tbError.Text = "Заполните все поля, отмеченные звёздочкой";
                return;
            }
            if (familiya.Any(ch => !char.IsLetter(ch) && ch != ' ') || imya.Any(ch => !char.IsLetter(ch) && ch != ' '))
            {
                tbError.Text = "Фамилия и имя могут содержать только буквы";
                return;
            }
            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$"))
            {
                tbError.Text = "Электронная почта указана неверно";
                tbEmail.Focus();
                return;
            }
            if (!Regex.IsMatch(tbTelefon.Text.Trim(), @"^\d{10}$"))
            {
                tbError.Text = "Телефон должен содержать 10 цифр после +7";
                tbTelefon.Focus();
                return;
            }
            if (!IsPasswordValid(password))
            {
                tbError.Text = "Пароль: не менее 8 символов, латинские буквы и цифры";
                pbPassword.Focus();
                return;
            }
            if (password != pbPassword2.Password)
            {
                tbError.Text = "Пароли не совпадают";
                pbPassword2.Focus();
                return;
            }
            if (cbAgreement.IsChecked != true)
            {
                tbError.Text = "Необходимо согласие на обработку персональных данных";
                return;
            }

            try
            {
                using (SqlConnection connection = DbHelper.OpenConnection())
                {
                    SqlCommand check = new SqlCommand(
                        "SELECT COUNT(*) FROM [322Polzovatel] WHERE Email = @email OR Telefon = @telefon", connection);
                    check.Parameters.AddWithValue("@email", email);
                    check.Parameters.AddWithValue("@telefon", telefon);
                    if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                    {
                        tbError.Text = "Пользователь с такой почтой или телефоном уже есть";
                        return;
                    }

                    SqlCommand cmd = new SqlCommand(
                        "INSERT INTO [322Polzovatel] (Familiya, Imya, Otchestvo, Email, Telefon, ParolHash, idRoli, Zablokirovan) " +
                        "VALUES (@familiya, @imya, @otchestvo, @email, @telefon, @hash, 3, 0)", connection);
                    cmd.Parameters.AddWithValue("@familiya", familiya);
                    cmd.Parameters.AddWithValue("@imya", imya);
                    cmd.Parameters.AddWithValue("@otchestvo",
                        otchestvo == "" ? (object)DBNull.Value : otchestvo);
                    cmd.Parameters.AddWithValue("@email", email);
                    cmd.Parameters.AddWithValue("@telefon", telefon);
                    cmd.Parameters.AddWithValue("@hash", DbHelper.HashPassword(password));
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Регистрация прошла успешно. Теперь войдите в систему.", "Готово",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (Exception ex)
            {
                tbError.Text = "Ошибка подключения к базе данных: " + ex.Message;
            }
        }

        private static bool IsPasswordValid(string password)
        {
            if (password.Length < 8)
                return false;

            bool hasLetter = false;
            bool hasDigit = false;

            foreach (char c in password)
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                    hasLetter = true;
                else if (c >= '0' && c <= '9')
                    hasDigit = true;
                else
                    return false;
            }

            return hasLetter && hasDigit;
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}