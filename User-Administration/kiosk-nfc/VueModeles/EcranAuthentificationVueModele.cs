using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Nexorsys.NFC.APP.Services;

namespace Nexorsys.NFC.APP.VueModeles
{
    public partial class EcranAuthentificationVueModele : ObservableObject
    {
        private readonly ApiClientService _apiClient;
        private readonly INavigationService _navigationService;
        private readonly IJournalisationService _journalisation;
        private readonly System.IServiceProvider _serviceProvider;

        private string _uidBadge = string.Empty;
        private int _tentativesRestantes = 3;

        [ObservableProperty]
        private string _nomCompletAffiche = string.Empty;

        [ObservableProperty]
        private string _messageErreur = string.Empty;

        public EcranAuthentificationVueModele(
            ApiClientService apiClient,
            INavigationService navigationService,
            IJournalisationService journalisation,
            System.IServiceProvider serviceProvider)
        {
            _apiClient = apiClient;
            _navigationService = navigationService;
            _journalisation = journalisation;
            _serviceProvider = serviceProvider;
        }

        public void Initialiser(string uidBadge, string? displayName = null)
        {
            NomCompletAffiche = string.IsNullOrWhiteSpace(displayName)
                ? "Authentification Requise"
                : displayName;
            _uidBadge = uidBadge;
            _tentativesRestantes = 3;
            MessageErreur = string.Empty;
        }

        public async void TenterAuthentification(SecureString motDePasse)
        {
            if (motDePasse == null || motDePasse.Length == 0)
            {
                MessageErreur = "Le code PIN ne peut pas être vide.";
                return;
            }

            System.IntPtr pointeurMotDePasse = System.IntPtr.Zero;
            string motDePasseClair = string.Empty;
            try
            {
                pointeurMotDePasse = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(motDePasse);
                motDePasseClair = System.Runtime.InteropServices.Marshal.PtrToStringUni(pointeurMotDePasse) ?? "";
            }
            finally
            {
                if (pointeurMotDePasse != System.IntPtr.Zero)
                    System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(pointeurMotDePasse);
            }

            var authResult = await _apiClient.AuthentifierParBadgeAsync(_uidBadge, motDePasseClair);

            if (authResult != null && !string.IsNullOrEmpty(authResult.Token))
            {
                _journalisation.EnregistrerInformation("Authentification 2FA API réussie", _uidBadge, authResult.User.SamAccountName);
                
                var accueilVm = _serviceProvider.GetRequiredService<EcranAccueilVueModele>();
                accueilVm.Initialiser(authResult.User.Name, authResult.Applications);
                _navigationService.NaviguerVers(accueilVm);
            }
            else
            {
                _tentativesRestantes--;
                _journalisation.EnregistrerErreur($"Échec d'authentification ({_tentativesRestantes} restantes)", _uidBadge);

                if (authResult != null && !string.IsNullOrEmpty(authResult.ErrorMessage))
                {
                    MessageErreur = authResult.ErrorMessage;
                }
                else if (_tentativesRestantes > 0)
                {
                    MessageErreur = $"Code PIN incorrect. Il vous reste {_tentativesRestantes} tentative(s).";
                }
                else
                {
                    var erreurVm = _serviceProvider.GetRequiredService<EcranErreurVueModele>();
                    erreurVm.Initialiser("Authentification bloquée", "Trop de tentatives échouées. Veuillez retirer et rebadger.");
                    _navigationService.NaviguerVers(erreurVm);
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
