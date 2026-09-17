using System.Windows;
using System.Windows.Controls;
using Pinede.NFC.APP.VueModeles;

namespace Pinede.NFC.APP.Vues
{
    public partial class EcranChangementPin : UserControl
    {
        public EcranChangementPin()
        {
            InitializeComponent();
        }

        private void Enregistrer_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is EcranChangementPinVueModele viewModel)
            {
                if (NewPinBox.Password != ConfirmPinBox.Password)
                {
                    viewModel.MessageErreur = "Les codes PIN ne correspondent pas.";
                    return;
                }

                viewModel.EnregistrerNouveauPinCommand.Execute(NewPinBox.SecurePassword);
            }
        }
    }
}
