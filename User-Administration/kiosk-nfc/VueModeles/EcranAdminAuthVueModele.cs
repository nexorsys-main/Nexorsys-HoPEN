using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pinede.NFC.APP.Modeles;
using Pinede.NFC.APP.Services;

namespace Pinede.NFC.APP.VueModeles
{
    public partial class EcranAdminAuthVueModele : ObservableObject
    {
        private readonly INavigationService _navigationService;
        private readonly IConfigurationService _configurationService;
        private readonly System.IServiceProvider _serviceProvider;

        [ObservableProperty]
        private string _messageErreur = string.Empty;

        public EcranAdminAuthVueModele(
            INavigationService navigationService,
            System.IServiceProvider serviceProvider,
            IConfigurationService configurationService)
        {
            _navigationService = navigationService;
            _serviceProvider = serviceProvider;
            _configurationService = configurationService;
        }

        public void Initialiser()
        {
            MessageErreur = string.Empty;
        }

        public async System.Threading.Tasks.Task TenterAuthentification(string identifiant, string motDePasse)
        {
            MessageErreur = string.Empty;

            if (string.IsNullOrWhiteSpace(identifiant))
            {
                // Fallback local authentication
                var config = _configurationService.ChargerParametres();
                if (!string.IsNullOrWhiteSpace(config.MotDePasseAdmin) && motDePasse == config.MotDePasseAdmin)
                {
                    var parametresVm = _serviceProvider.GetRequiredService<EcranParametresVueModele>();
                    parametresVm.Initialiser();
                    _navigationService.NaviguerVers(parametresVm);
                }
                else
                {
                    MessageErreur = "Mot de passe local incorrect.";
                }
            }
            else
            {
                // Backend authentication
                var apiClient = _serviceProvider.GetRequiredService<ApiClientService>();
                var result = await apiClient.AuthentifierAdminAsync(identifiant, motDePasse);
                if (result != null && string.IsNullOrEmpty(result.ErrorMessage))
                {
                    var parametresVm = _serviceProvider.GetRequiredService<EcranParametresVueModele>();
                    parametresVm.Initialiser();
                    _navigationService.NaviguerVers(parametresVm);
                }
                else
                {
                    MessageErreur = result?.ErrorMessage ?? "Échec de l'authentification serveur.";
                }
            }
        }

        [RelayCommand]
        private void Annuler()
        {
            var attenteVm = _serviceProvider.GetRequiredService<EcranAttenteVueModele>();
            _navigationService.NaviguerVers(attenteVm);
        }
    }
}
