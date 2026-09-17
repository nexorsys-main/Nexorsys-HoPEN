using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace Pinede.NFC.APP.VueModeles
{
    /// <summary>
    /// Vue-modèle principal qui gère la navigation entre les différents écrans.
    /// </summary>
    public partial class FenetrePrincipaleVueModele : ObservableObject
    {
        [ObservableProperty]
        private object? _vueCourante;

        /// <summary>
        /// Gère la libération des ressources de l'ancien écran si nécessaire.
        /// </summary>
        partial void OnVueCouranteChanged(object? oldValue, object? newValue)
        {
            if (oldValue is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private readonly Services.INfcService _nfcService;
        private readonly Services.SignalRBridgeService _signalRService;
        private readonly System.IServiceProvider _serviceProvider;
        private readonly Services.ApiClientService _apiClient;

        public FenetrePrincipaleVueModele(
            Services.INfcService nfcService,
            Services.SignalRBridgeService signalRService,
            System.IServiceProvider serviceProvider,
            Services.ApiClientService apiClient)
        {
            _nfcService = nfcService;
            _signalRService = signalRService;
            _serviceProvider = serviceProvider;
            _apiClient = apiClient;

            _nfcService.BadgeRetire += SurBadgeRetire;
            _signalRService.UserStatusChanged += SurStatutUtilisateurChange;
        }

        private void SurStatutUtilisateurChange(string userId, bool isActive)
        {
            if (!isActive && _apiClient.CurrentUserId == userId)
            {
                // Account blocked while active session! Force logout.
                SurBadgeRetire(this, EventArgs.Empty);
            }
        }

        private void SurBadgeRetire(object? sender, EventArgs e)
        {
            // Terminer la session sur le serveur backend si une session existe
            _ = _apiClient.TerminerSessionAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var attenteVm = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<EcranAttenteVueModele>(_serviceProvider);
                VueCourante = attenteVm;

                var fenetre = _serviceProvider.GetService<Vues.FenetrePrincipale>();
                if (fenetre != null)
                {
                    fenetre.WindowState = System.Windows.WindowState.Minimized;
                    fenetre.Hide();
                }
            });
        }
    }
}
