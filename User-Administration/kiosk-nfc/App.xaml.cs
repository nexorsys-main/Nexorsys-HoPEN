using System;
using System.Configuration;
using System.Data;
using System.Windows;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexorsys.NFC.APP.Vues;
using Nexorsys.NFC.APP.VueModeles;
using Nexorsys.NFC.APP.Services;

namespace Nexorsys.NFC.APP;

public partial class App : Application
{
    public static IHost? AppHost { get; private set; }

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            // A custom URI protocol is launched by Windows with System32 as the
            // current directory. Always resolve appsettings and local paths next
            // to the installed executable instead of relying on that directory.
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((hostContext, services) =>
            {
                // VuePrincipale et son VueModele
                services.AddSingleton<FenetrePrincipale>();
                services.AddSingleton<FenetrePrincipaleVueModele>();

                // Configuration
                services.Configure<Nexorsys.NFC.APP.Modeles.ParametresApplication>(
                    hostContext.Configuration.GetSection("ParametresApplication"));

                // Services métiers
                services.AddSingleton<IServiceChiffrement, ServiceChiffrement>();
                services.AddSingleton<IServiceValidation, ServiceValidation>();
                services.AddSingleton<INfcService, NfcService>();
                services.AddSingleton<IJournalisationService, JournalisationService>();
                services.AddSingleton<IAgentLaunchAuthorizationClient, AgentLaunchAuthorizationClient>();
                services.AddSingleton<IApplicationProcessLauncher, WindowsApplicationProcessLauncher>();
                services.AddSingleton<ILanceurApplicationService, LanceurApplicationService>();
                services.AddSingleton<IConfigurationService, ConfigurationService>();
                services.AddSingleton<ITrayIconService, TrayIconService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<ApiClientService>();
                services.AddSingleton<IKioskApplicationSessionClient>(sp => sp.GetRequiredService<ApiClientService>());
                
                // Vues métiers
                services.AddTransient<EcranAttenteVueModele>();
                services.AddTransient<EcranAuthentificationVueModele>();
                services.AddTransient<EcranAccueilVueModele>();
                services.AddTransient<EcranErreurVueModele>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try 
        {
            await AppHost!.StartAsync();
            
            var navigation = AppHost.Services.GetRequiredService<INavigationService>();
            var attenteVm = AppHost.Services.GetRequiredService<EcranAttenteVueModele>();
            navigation.NaviguerVers(attenteVm);
            
            var apiClient = AppHost.Services.GetRequiredService<ApiClientService>();
            var nfcService = AppHost.Services.GetRequiredService<INfcService>();
            apiClient.WriteCommandReceived += (s, uid) => nfcService.EcrireUid(uid);
            
            var fenetrePrincipale = AppHost.Services.GetRequiredService<FenetrePrincipale>();
            var trayService = AppHost.Services.GetRequiredService<ITrayIconService>();

            trayService.Initialiser();
            trayService.RestaurerDemande += (s, ev) => 
            {
                fenetrePrincipale.Show();
                fenetrePrincipale.WindowState = WindowState.Normal;
                fenetrePrincipale.Activate();
            };

            fenetrePrincipale.Show();
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "crash_log.txt"),
                $"CRASH FATAL AU DEMARRAGE: {ex.Message}\n{ex.StackTrace}");
            MessageBox.Show($"Erreur critique au démarrage de l'application :\n{ex.Message}", "Erreur Fatale", MessageBoxButton.OK, MessageBoxImage.Error);
            this.Shutdown();
        }
        
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        var host = AppHost;
        if (host is null) { base.OnExit(e); return; }
        var tray = host.Services.GetService<ITrayIconService>();
        tray?.Nettoyer();

        await host.StopAsync();
        host.Dispose();
        
        base.OnExit(e);
    }
}

