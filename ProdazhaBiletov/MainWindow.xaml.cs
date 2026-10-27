using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ProdazhaBiletov
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _clockTimer;

        public MainWindow()
        {
            InitializeComponent();

            tbUser.Text = UserSession.ShortName;
            tbRole.Text = UserSession.Role;
            tbClock.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            if (UserSession.IsAdmin)
                btnAdmin.Visibility = Visibility.Visible;
            else if (UserSession.IsCashier)
                btnCashier.Visibility = Visibility.Visible;

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _clockTimer.Tick += (s, e) => tbClock.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            _clockTimer.Start();

            Loaded += (s, e) => ShowPage(new AfishaPage());
        }

        public void ShowPage(UserControl page)
        {
            pageHost.Content = page;
        }

        public void SetStatus(string text)
        {
            tbStatus.Text = text;
        }

        private void navAfisha_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) ShowPage(new AfishaPage());
        }

        private void navKalendar_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) ShowPage(new KalendarPage());
        }

        private void navZakazy_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) ShowPage(new MoiZakazyPage());
        }

        private void btnAdmin_Click(object sender, RoutedEventArgs e)
        {
            AdminWindow admin = new AdminWindow();
            admin.Owner = this;
            admin.Show();
        }

        private void btnCashier_Click(object sender, RoutedEventArgs e)
        {
            CashierWindow cashier = new CashierWindow();
            cashier.Owner = this;
            cashier.Show();
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Выйти из системы?", "Продажа билетов",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            UserSession.Clear();
            LoginWindow login = new LoginWindow();
            login.Show();
            Application.Current.MainWindow = login;
            Close();
        }
    }
}