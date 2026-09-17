using System.Windows;
using System.Windows.Controls;
using Pinede.NFC.APP.VueModeles;

namespace Pinede.NFC.APP.Vues
{
    public partial class EcranAdminAuth : UserControl
    {
        public EcranAdminAuth()
        {
            InitializeComponent();
            this.Loaded += (s, e) => { AdminMdpBox.Focus(); };
        }

        private async void Accees_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is EcranAdminAuthVueModele vm)
            {
                await vm.TenterAuthentification(AdminIdentifiantBox.Text, AdminMdpBox.Password);
                AdminMdpBox.Clear();
            }
        }
    }
}
