using System.Windows;
using System.Windows.Controls;

namespace ProdazhaBiletov
{
    public partial class AdminWindow : Window
    {
        public AdminWindow()
        {
            InitializeComponent();
            tbAdminUser.Text = UserSession.ShortName;
            Loaded += (s, e) => adminHost.Content = new AdminMeropriyatiyaPage();
        }

        private void navMeropriyatiya_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) adminHost.Content = new AdminMeropriyatiyaPage();
        }

        private void navZaly_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) adminHost.Content = new AdminZalyPage();
        }

        private void navPolzovateli_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) adminHost.Content = new AdminPolzovateliPage();
        }

        private void navZakazy_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) adminHost.Content = new AdminZakazyPage();
        }

        private void navOtchet_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) adminHost.Content = new AdminOtchetPage();
        }

        private void btnExitAdmin_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}