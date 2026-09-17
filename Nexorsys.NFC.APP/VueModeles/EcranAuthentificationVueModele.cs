using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Pinede.NFC.APP.Services;

namespace Pinede.NFC.APP.VueModeles
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

        [ObservableProperty]
        private string _messageSuccess = string.Empty;

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

        public async void Initialiser(string uidBadge)
        {
            NomCompletAffiche = "Vérification en cours...";
            _uidBadge = uidBadge;
            _tentativesRestantes = 3;
            MessageErreur = string.Empty;
            MessageSuccess = string.Empty;

            // Immediate check of account status
            var identResult = await _apiClient.IdentifierUserAsync(uidBadge);

            if (identResult != null && !string.IsNullOrEmpty(identResult.ErrorMessage))
            {
                NomCompletAffiche = "Accès Bloqué";
                MessageErreur = identResult.ErrorMessage;
                _journalisation.EnregistrerErreur($"Tentative d'accès bloquée : {identResult.ErrorMessage}", uidBadge);

                // Navigate to error screen after 3 seconds if it's a hard block
                await Task.Delay(3000);
                var erreurVm = _serviceProvider.GetRequiredService<EcranErreurVueModele>();
                erreurVm.Initialiser("Accès Refusé", identResult.ErrorMessage);
                _navigationService.NaviguerVers(erreurVm);
                return;
            }

            if (identResult != null && identResult.User != null)
            {
                var parametres = _serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Pinede.NFC.APP.Modeles.ParametresApplication>>();
                if (parametres.CurrentValue.Securite.SsoActif)
                {
                    string windowsUser = System.Environment.UserName;
                    if (string.Equals(identResult.User.SamAccountName, windowsUser, System.StringComparison.OrdinalIgnoreCase))
                    {
                        NomCompletAffiche = "Authentification SSO en cours...";
                        var authResult = await _apiClient.AuthentifierSsoAsync(uidBadge, identResult.User.Id, windowsUser);
                        if (authResult != null && !string.IsNullOrEmpty(authResult.Token))
                        {
                            _journalisation.EnregistrerInformation("Authentification SSO réussie", _uidBadge, authResult.User.SamAccountName);

                            if (authResult.MustChangePin)
                            {
                                var changementVm = _serviceProvider.GetRequiredService<EcranChangementPinVueModele>();
                                changementVm.Initialiser(authResult.User.Id, authResult.User.Name, authResult.Applications);
                                _navigationService.NaviguerVers(changementVm);
                            }
                            else
                            {
                                var accueilVm = _serviceProvider.GetRequiredService<EcranAccueilVueModele>();
                                accueilVm.Initialiser(authResult.User.Name, authResult.Applications);
                                _navigationService.NaviguerVers(accueilVm);

                                var fenetrePrincipale = _serviceProvider.GetRequiredService<Vues.FenetrePrincipale>();
                                if (fenetrePrincipale.IsKioskMode)
                                {
                                    fenetrePrincipale.DeverrouillerEcranEnPanneau();
                                }
                            }
                            return;
                        }
                        else
                        {
                            _journalisation.EnregistrerErreur($"Échec du SSO: {authResult?.ErrorMessage}", _uidBadge);
                        }
                    }
                }

                NomCompletAffiche = $"Bonjour, {identResult.User.Name}";
            }
            else
            {
                NomCompletAffiche = "Authentification Requise";
            }
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

                if (authResult.MustChangePin)
                {
                    var changementVm = _serviceProvider.GetRequiredService<EcranChangementPinVueModele>();
                    changementVm.Initialiser(authResult.User.Id, authResult.User.Name, authResult.Applications);
                    _navigationService.NaviguerVers(changementVm);
                }
                else
                {
                    var accueilVm = _serviceProvider.GetRequiredService<EcranAccueilVueModele>();
                    accueilVm.Initialiser(authResult.User.Name, authResult.Applications);
                    _navigationService.NaviguerVers(accueilVm);

                    var fenetrePrincipale = _serviceProvider.GetRequiredService<Vues.FenetrePrincipale>();
                    if (fenetrePrincipale.IsKioskMode)
                    {
                        fenetrePrincipale.DeverrouillerEcranEnPanneau();
                    }
                }
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

        [RelayCommand]
        private async Task DemanderReinitialisationPin()
        {
            MessageErreur = string.Empty;
            MessageSuccess = string.Empty;

            var result = await _apiClient.DemanderReinitialisationPinAsync("00000000-0000-0000-0000-000000000000", _uidBadge);
            // Note: The backend identifies the user by BadgeUid if UserId is Empty/Guid.Empty

            if (result)
            {
                MessageSuccess = "Votre demande de nouveau code PIN a été envoyée à l'administration.";
            }
            else
            {
                MessageErreur = "Impossible d'envoyer la demande. Veuillez contacter le support technique.";
            }
        }
    }
}
