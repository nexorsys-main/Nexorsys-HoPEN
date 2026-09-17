using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pinede.NFC.APP.Modeles;
using Pinede.NFC.APP.Services;

namespace Pinede.NFC.APP.VueModeles
{
    public partial class EcranAttenteVueModele : ObservableObject, IDisposable
    {
        private readonly INfcService _nfcService;
        private readonly ApiClientService _apiClient;
        private readonly INavigationService _navigationService;
        private readonly IJournalisationService _journalisation;
        private readonly IServiceProvider _serviceProvider;
        private readonly Vues.FenetrePrincipale _mainWindow;

        private readonly IOptionsMonitor<ParametresApplication> _parametresMonitor;

        [ObservableProperty]
        private bool _isModeEncodage;

        [ObservableProperty]
        private TerminalSettings _terminalConfig;
        public EcranAttenteVueModele(
            INfcService nfcService,
            ApiClientService apiClient,
            INavigationService navigationService,
            IOptionsMonitor<ParametresApplication> parametres,
            IJournalisationService journalisation,
            IServiceProvider serviceProvider,
            Vues.FenetrePrincipale mainWindow)
        {
            _nfcService = nfcService;
            _apiClient = apiClient;
            _navigationService = navigationService;
            _parametresMonitor = parametres;
            _journalisation = journalisation;
            _serviceProvider = serviceProvider;
            _mainWindow = mainWindow;

            _terminalConfig = _parametresMonitor.CurrentValue.Terminal;
            _nfcService.BadgeDetecte += SurBadgeDetecte;
            _nfcService.ErreurLecture += SurErreurNfc;
            _nfcService.ModeEncodageActive += SurModeEncodageActive;
            _nfcService.EncodageTermine += SurEncodageTermine;
            // NOTE: Do NOT call DemarrerEcoute() here.
            // The NfcService singleton manages its own monitor lifecycle via the 500ms polling timer.
            // Calling DemarrerEcoute() here would tear down and rebuild the monitor each time
            // a new EcranAttenteVueModele is created (it's registered as Transient).
        }

        private void SurBadgeDetecte(object? sender, string uidBadge)
        {
            _journalisation.EnregistrerInformation("Badge détecté, demande de code 2FA", uidBadge);

            App.Current.Dispatcher.Invoke(() =>
            {
                if (_mainWindow != null)
                {
                    // Restore from minimized state
                    if (_mainWindow.WindowState == System.Windows.WindowState.Minimized)
                        _mainWindow.WindowState = System.Windows.WindowState.Normal;

                    _mainWindow.Show();
                    _mainWindow.Activate();

                    // Force foreground: WPF Activate() can silently fail when
                    // another app holds focus (Windows focus-stealing prevention).
                    // SetForegroundWindow bypasses this restriction reliably.
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle;
                    if (hwnd != IntPtr.Zero)
                    {
                        NativeMethods.SetForegroundWindow(hwnd);
                        NativeMethods.ShowWindow(hwnd, 9); // SW_RESTORE
                    }

                    _mainWindow.Topmost = true;
                    _mainWindow.Topmost = false;
                    _mainWindow.Focus();
                }

                var authVm = _serviceProvider.GetRequiredService<EcranAuthentificationVueModele>();
                authVm.Initialiser(uidBadge);
                _navigationService.NaviguerVers(authVm);
            });
        }

        private void SurModeEncodageActive(object? sender, string targetUid)
        {
            App.Current.Dispatcher.Invoke(() => IsModeEncodage = true);
        }

        private void SurEncodageTermine(object? sender, bool success)
        {
            App.Current.Dispatcher.Invoke(() => IsModeEncodage = false);
            if (!success)
            {
                _journalisation.EnregistrerErreur("L'encodage du CUID a échoué.");
            }
        }

        private void SurErreurNfc(object? sender, string erreur)
        {
            _journalisation.EnregistrerErreur($"Erreur lecteur NFC : {erreur}");
            App.Current.Dispatcher.Invoke(() =>
            {
                if (_mainWindow != null)
                {
                    _mainWindow.Show();
                    if (_mainWindow.WindowState == System.Windows.WindowState.Minimized)
                        _mainWindow.WindowState = System.Windows.WindowState.Normal;
                    _mainWindow.Activate();
                    _mainWindow.Topmost = true;
                    _mainWindow.Topmost = false;
                    _mainWindow.Focus();
                }

                var erreurVm = _serviceProvider.GetRequiredService<EcranErreurVueModele>();
                erreurVm.Initialiser("Problème de lecteur", erreur);
                _navigationService.NaviguerVers(erreurVm);
            });
        }

        [RelayCommand]
        private void OuvrirParametres()
        {
            var authAdminVm = _serviceProvider.GetRequiredService<EcranAdminAuthVueModele>();
            authAdminVm.Initialiser();
            _navigationService.NaviguerVers(authAdminVm);
        }


        public void Dispose()
        {
            _nfcService.BadgeDetecte -= SurBadgeDetecte;
            _nfcService.ErreurLecture -= SurErreurNfc;
            _nfcService.ModeEncodageActive -= SurModeEncodageActive;
            _nfcService.EncodageTermine -= SurEncodageTermine;
        }
    }
}
