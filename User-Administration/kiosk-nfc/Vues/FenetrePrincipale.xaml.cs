using System;
using System.Windows;
using Nexorsys.NFC.APP.VueModeles;

namespace Nexorsys.NFC.APP.Vues
{
    public partial class FenetrePrincipale : Window
    {
        public FenetrePrincipale(FenetrePrincipaleVueModele vueModele)
        {
            InitializeComponent();
            DataContext = vueModele;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
            base.OnStateChanged(e);
        }
    }
}
