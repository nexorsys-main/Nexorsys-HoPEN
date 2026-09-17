using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Nexorsys.NFC.APP.Modeles;
using Nexorsys.NFC.APP.Services;

namespace Nexorsys.NFC.APP.VueModeles
{
    public partial class EcranAttenteVueModele : ObservableObject, IDisposable
    {
        private readonly INfcService _nfcService;
        private readonly ApiClientService _apiClient;
        private readonly INavigationService _navigationService;
        private readonly IJournalisationService _journalisation;
        private readonly IServiceProvider _serviceProvider;


        public EcranAttenteVueModele(
            INfcService nfcService,
            ApiClientService apiClient,
            INavigationService navigationService,
            IJournalisationService journalisation,
            IServiceProvider serviceProvider)
        {
            _nfcService = nfcService;
            _apiClient = apiClient;
            _navigationService = navigationService;
            _journalisation = journalisation;
            _serviceProvider = serviceProvider;

            _nfcService.BadgeDetecte += SurBadgeDetecte;
            _nfcService.ErreurLecture += SurErreurNfc;
            _nfcService.DemarrerEcoute();
        }

        private async void SurBadgeDetecte(object? sender, string uidBadge)
        {
            _journalisation.EnregistrerInformation("Badge identified; requesting PIN verification.");

            var displayName = await _apiClient.IdentifierParBadgeAsync(uidBadge);
            App.Current.Dispatcher.Invoke(() =>
            {
                var authVm = _serviceProvider.GetRequiredService<EcranAuthentificationVueModele>();
                authVm.Initialiser(uidBadge, displayName); // Navigate to PIN prompt
                _navigationService.NaviguerVers(authVm);
            });
        }

        private void SurErreurNfc(object? sender, string erreur)
        {
            _journalisation.EnregistrerErreur($"Erreur lecteur NFC : {erreur}");
            App.Current.Dispatcher.Invoke(() =>
            {
                var erreurVm = _serviceProvider.GetRequiredService<EcranErreurVueModele>();
                erreurVm.Initialiser("Problème de lecteur", erreur);
                _navigationService.NaviguerVers(erreurVm);
            });
        }

        public void Dispose()
        {
            _nfcService.BadgeDetecte -= SurBadgeDetecte;
            _nfcService.ErreurLecture -= SurErreurNfc;
        }
    }
}
