using System.Windows;
using System.Windows.Controls;
using Pinede.NFC.APP.VueModeles;

namespace Pinede.NFC.APP.Vues
{
    public partial class EcranAuthentification : UserControl
    {
        public EcranAuthentification()
        {
            InitializeComponent();
            this.Loaded += (s, e) => { MdpBox.Focus(); };
        }

        private void SeConnecter_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is EcranAuthentificationVueModele vm)
            {
                vm.TenterAuthentification(MdpBox.SecurePassword);
                MdpBox.Clear(); // Action strictement dictée par le besoin de sécurité: le MDP est purgé après sa transmission structurée.
            }
        }
    }
}
