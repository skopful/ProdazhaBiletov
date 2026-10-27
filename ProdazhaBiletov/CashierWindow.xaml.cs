using System.Windows;

namespace ProdazhaBiletov
{
    public partial class CashierWindow : Window
    {
        public CashierWindow()
        {
            InitializeComponent();
            tbCashierUser.Text = UserSession.ShortName;
            Loaded += (s, e) => cashierHost.Content = new CashierProdazhaPage();
        }

        private void navProdazha_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) cashierHost.Content = new CashierProdazhaPage();
        }

        private void navZakazy_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) cashierHost.Content = new AdminZakazyPage();
        }

        private void navSmena_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) cashierHost.Content = new CashierSmenaPage();
        }

        private void btnCloseCashier_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}