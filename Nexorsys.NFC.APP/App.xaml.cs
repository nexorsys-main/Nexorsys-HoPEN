using System;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Runtime.InteropServices;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pinede.NFC.APP.Vues;
using Pinede.NFC.APP.VueModeles;
using Pinede.NFC.APP.Services;

namespace Pinede.NFC.APP;

public partial class App : Application
{
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool LockWorkStation();

    public static IHost? AppHost { get; private set; }

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((hostContext, services) =>
            {
                // VuePrincipale et son VueModele
                services.AddSingleton<FenetrePrincipale>();
                services.AddSingleton<FenetrePrincipaleVueModele>();

                // Configuration
                services.Configure<Pinede.NFC.APP.Modeles.ParametresApplication>(
                    hostContext.Configuration.GetSection("ParametresApplication"));

                // Services métiers
                services.AddSingleton<IServiceChiffrement, ServiceChiffrement>();
                services.AddSingleton<IServiceValidation, ServiceValidation>();
                services.AddSingleton<INfcService, NfcService>();
                services.AddSingleton<IJournalisationService, JournalisationService>();
                services.AddSingleton<ILanceurApplicationService, LanceurApplicationService>();
                services.AddSingleton<IConfigurationService, ConfigurationService>();
                services.AddSingleton<ITrayIconService, TrayIconService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<ApiClientService>();
                services.AddSingleton<SignalRBridgeService>();
                services.AddHttpClient();
                services.AddSingleton<ILicenseService, LicenseService>();

                // Vues métiers
                services.AddTransient<EcranAttenteVueModele>();
                services.AddTransient<EcranAuthentificationVueModele>();
                services.AddTransient<EcranChangementPinVueModele>();
                services.AddTransient<EcranAccueilVueModele>();
                services.AddTransient<EcranErreurVueModele>();
                services.AddTransient<EcranParametresVueModele>();
                services.AddTransient<EcranAdminAuthVueModele>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            await AppHost!.StartAsync();

            var isWelcomeMode = false;
            var isKioskMode = false;
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.Equals("--welcome-mode", StringComparison.OrdinalIgnoreCase))
                {
                    isWelcomeMode = true;
                }
                if (arg.Equals("--kiosk-mode", StringComparison.OrdinalIgnoreCase))
                {
                    isKioskMode = true;
                }
            }

            var navigation = AppHost.Services.GetRequiredService<INavigationService>();
            var fenetrePrincipale = AppHost.Services.GetRequiredService<FenetrePrincipale>();
            App.Current.MainWindow = fenetrePrincipale;

            // 1. SHOW UI IMMEDIATELY
            var attenteVm = AppHost.Services.GetRequiredService<EcranAttenteVueModele>();
            navigation.NaviguerVers(attenteVm);

            var trayService = AppHost.Services.GetRequiredService<ITrayIconService>();
            trayService.Initialiser();
            trayService.RestaurerDemande += (s, ev) =>
            {
                fenetrePrincipale.Show();
                fenetrePrincipale.WindowState = WindowState.Normal;
                fenetrePrincipale.Activate();
                fenetrePrincipale.Topmost = true;
                fenetrePrincipale.Topmost = false;
                fenetrePrincipale.Focus();
            };

            if (isWelcomeMode)
            {
                fenetrePrincipale.IsWelcomeMode = true;
                fenetrePrincipale.Topmost = true;
                fenetrePrincipale.WindowStyle = WindowStyle.None;
                fenetrePrincipale.ResizeMode = ResizeMode.NoResize;
                fenetrePrincipale.WindowState = WindowState.Maximized;
                fenetrePrincipale.Show();
            }
            else if (isKioskMode)
            {
                fenetrePrincipale.IsKioskMode = true;
                fenetrePrincipale.Show();
                fenetrePrincipale.VerrouillerEcran();
            }
            else
            {
                fenetrePrincipale.WindowState = WindowState.Minimized;
                fenetrePrincipale.Show();
            }

            // 2. START NFC LISTENER IMMEDIATELY (So removing badge locks system right away)
            var nfcService = AppHost.Services.GetRequiredService<INfcService>();
            var apiClient = AppHost.Services.GetRequiredService<ApiClientService>();
            nfcService.DemarrerEcoute();

            if (isWelcomeMode)
            {
                nfcService.BadgeRetire += async (s, ev) =>
                {
                    if (!string.IsNullOrEmpty(apiClient.JwtToken))
                    {
                        await apiClient.BadgeRemovedAsync(apiClient.JwtToken);
                    }
                    LockWorkStation();
                };
            }
            else if (isKioskMode)
            {
                nfcService.BadgeRetire += async (s, ev) =>
                {
                    if (!string.IsNullOrEmpty(apiClient.JwtToken))
                    {
                        await apiClient.BadgeRemovedAsync(apiClient.JwtToken);
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var attenteVm = AppHost.Services.GetRequiredService<EcranAttenteVueModele>();
                        navigation.NaviguerVers(attenteVm);
                        fenetrePrincipale.VerrouillerEcran();
                        LockWorkStation(); // Fast User Switching
                    });
                };
            }

            // 3. DO SLOW NETWORK CALLS IN BACKGROUND
            var licenseService = AppHost.Services.GetRequiredService<ILicenseService>();
            bool isLicenseValid = await licenseService.ValidateLicenseAsync();

            if (!isLicenseValid)
            {
                var errorVm = AppHost.Services.GetRequiredService<EcranErreurVueModele>();
                errorVm.MessageErreur = "Licence d'utilisation expirée ou révoquée. Veuillez contacter le support NexorSys.";
                navigation.NaviguerVers(errorVm);
                return;
            }

            var configService = AppHost.Services.GetRequiredService<IConfigurationService>();

            // 4. CHECK UNIFIED WINDOWS SESSION
            string? windowsSessionToken = null;
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\PinedeIdentity\Sessions", true))
                {
                    if (key != null)
                    {
                        var userName = Environment.UserName;
                        var possibleKeys = new[] { userName, $"PINEDE\\{userName}" };
                        foreach (var pk in possibleKeys)
                        {
                            var val = key.GetValue(pk) as byte[];
                            if (val != null)
                            {
                                windowsSessionToken = System.Text.Encoding.UTF8.GetString(val);
                                key.DeleteValue(pk, false); // Clear token
                                break;
                            }
                        }
                    }
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(windowsSessionToken))
            {
                var authResult = await apiClient.ValidateWindowsSessionAsync(windowsSessionToken);
                if (authResult != null && string.IsNullOrEmpty(authResult.ErrorMessage))
                {
                    // Update internal JWT
                    apiClient.GetType().GetProperty("JwtToken")?.SetValue(apiClient, windowsSessionToken);

                    var accueilVm = AppHost.Services.GetRequiredService<EcranAccueilVueModele>();
                    accueilVm.Initialiser(authResult.User.Name, authResult.Applications);
                    navigation.NaviguerVers(accueilVm);

                    if (isKioskMode)
                    {
                        fenetrePrincipale.DeverrouillerEcranEnPanneau();
                    }
                }
            }

            try
            {
                await apiClient.AutoCatchUrlAsync(configService);
            }
            catch { }

            var signalR = AppHost.Services.GetRequiredService<SignalRBridgeService>();
            _ = signalR.ConnectWithRetryAsync();

            var initialParams = configService.ChargerParametres();
            configService.SauvegarderParametres(initialParams);

            if (isWelcomeMode)
            {
                var userName = Environment.UserName;
                var authResult = await apiClient.ObtenirSessionAccueilAsync(userName);
                if (authResult != null && string.IsNullOrEmpty(authResult.ErrorMessage))
                {
                    var accueilVm = AppHost.Services.GetRequiredService<EcranAccueilVueModele>();
                    accueilVm.Initialiser(authResult.User.Name, authResult.Applications);
                    navigation.NaviguerVers(accueilVm);
                }
                else
                {
                    var errorVm = AppHost.Services.GetRequiredService<EcranErreurVueModele>();
                    errorVm.MessageErreur = "Impossible de charger le profil de bienvenue : " + (authResult?.ErrorMessage ?? "Erreur inconnue");
                    navigation.NaviguerVers(errorVm);
                }
            }
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText("crash_log.txt", $"CRASH FATAL AU DEMARRAGE: {ex.Message}\n{ex.StackTrace}");
            MessageBox.Show($"Erreur critique au démarrage de l'application :\n{ex.Message}", "Erreur Fatale", MessageBoxButton.OK, MessageBoxImage.Error);
            this.Shutdown();
        }

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        var tray = AppHost.Services.GetService<ITrayIconService>();
        tray?.Nettoyer();

        await AppHost!.StopAsync();
        AppHost.Dispose();

        base.OnExit(e);
    }
}

