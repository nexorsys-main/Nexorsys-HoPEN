using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Nexorsys.NFC.APP.Services;
using Nexorsys.NFC.APP.Modeles;

namespace Nexorsys.NFC.APP.VueModeles
{
    public partial class EcranAccueilVueModele : ObservableObject
    {
        private readonly ILanceurApplicationService _lanceurService;
        private readonly INavigationService _navigationService;
        private readonly ApiClientService _apiClient;
        private readonly System.IServiceProvider _serviceProvider;

        [ObservableProperty]
        private string _nomUtilisateur = string.Empty;

        public ObservableCollection<ManagedApp> ApplicationsAutorisees { get; } = new ObservableCollection<ManagedApp>();

        public EcranAccueilVueModele(
            ILanceurApplicationService lanceurService,
            INavigationService navigationService,
            ApiClientService apiClient,
            System.IServiceProvider serviceProvider)
        {
            _lanceurService = lanceurService;
            _navigationService = navigationService;
            _apiClient = apiClient;
            _serviceProvider = serviceProvider;
        }

        public void Initialiser(string nomUtilisateur, System.Collections.Generic.List<ManagedApp> apps)
        {
            NomUtilisateur = nomUtilisateur;
            ApplicationsAutorisees.Clear();
            
            foreach (var app in apps)
            {
                ApplicationsAutorisees.Add(app);
            }
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task LancerApplication(ManagedApp app)
        {
            if (app != null)
            {
                await _lanceurService.LancerApplicationAsync(app);
            }
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task Deconnexion()
        {
            ApplicationsAutorisees.Clear();
            NomUtilisateur = string.Empty;
            await _apiClient.EndSessionAsync();
            var attenteVm = _serviceProvider.GetRequiredService<EcranAttenteVueModele>();
            _navigationService.NaviguerVers(attenteVm);
        }
    }
}
