using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pinede.NFC.APP.Services;
using Pinede.NFC.APP.Modeles;

namespace Pinede.NFC.APP.VueModeles
{
    public partial class EcranAccueilVueModele : ObservableObject
    {
        private readonly ILanceurApplicationService _lanceurService;
        private readonly INavigationService _navigationService;
        private readonly System.IServiceProvider _serviceProvider;

        [ObservableProperty]
        private string _nomUtilisateur = string.Empty;

        public ObservableCollection<ManagedApp> ApplicationsAutorisees { get; } = new ObservableCollection<ManagedApp>();

        public EcranAccueilVueModele(
            ILanceurApplicationService lanceurService,
            INavigationService navigationService,
            System.IServiceProvider serviceProvider)
        {
            _lanceurService = lanceurService;
            _navigationService = navigationService;
            _serviceProvider = serviceProvider;
        }

        public void Initialiser(string nomUtilisateur, System.Collections.Generic.List<ManagedApp> apps)
        {
            NomUtilisateur = nomUtilisateur;
            ApplicationsAutorisees.Clear();

            string appNames = "";
            foreach (var app in apps)
            {
                ApplicationsAutorisees.Add(app);
                appNames += "- " + app.Name + "\n";
            }

            // POPUP DE DIAGNOSTIC AUTO-FERMABLE (6 SECONDES)
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var diag = new Vues.EcranDiagnostic(nomUtilisateur, appNames);
                diag.Show();
            });
        }

        [RelayCommand]
        private void LancerApplication(ManagedApp app)
        {
            if (app != null)
            {
                _lanceurService.LancerApplication(app);
            }
        }

        [RelayCommand]
        private void Deconnexion()
        {
            var attenteVm = _serviceProvider.GetRequiredService<EcranAttenteVueModele>();
            _navigationService.NaviguerVers(attenteVm);
        }
    }
}
