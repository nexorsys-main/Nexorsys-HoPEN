using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Nexorsys.NFC.APP.Services;

namespace Nexorsys.NFC.APP.VueModeles
{
    public partial class EcranErreurVueModele : ObservableObject
    {
        private readonly INavigationService _navigationService;
        private readonly System.IServiceProvider _serviceProvider;

        [ObservableProperty]
        private string _titreErreur = "Accès refusé";

        [ObservableProperty]
        private string _messageErreur = string.Empty;

        public EcranErreurVueModele(INavigationService navigationService, System.IServiceProvider serviceProvider)
        {
            _navigationService = navigationService;
            _serviceProvider = serviceProvider;
        }

        public void Initialiser(string titre, string message)
        {
            TitreErreur = titre;
            MessageErreur = message;
        }

        [RelayCommand]
        private void RetourAccueil()
        {
            var attenteVm = _serviceProvider.GetRequiredService<EcranAttenteVueModele>();
            _navigationService.NaviguerVers(attenteVm);
        }
    }
}
