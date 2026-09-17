using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Pinede.NFC.APP.Modeles;
using Pinede.NFC.APP.Services;

namespace Pinede.NFC.APP.VueModeles
{
    public partial class EcranParametresVueModele : ObservableObject
    {
        private readonly IConfigurationService _configurationService;
        private readonly IJournalisationService _journalisationService;
        private readonly ApiClientService _apiClientService;
        private readonly INavigationService _navigationService;
        private readonly IServiceChiffrement _serviceChiffrement;
        private readonly IServiceValidation _serviceValidation;
        private readonly System.IServiceProvider _serviceProvider;

        [ObservableProperty]
        private ParametresApplication _parametres = new ParametresApplication();

        [ObservableProperty]
        private ObservableCollection<EvenementJournal> _historiqueLogs = new ObservableCollection<EvenementJournal>();

        [ObservableProperty]
        private string _statutImport = string.Empty;

        [ObservableProperty]
        private string _machineName = Environment.MachineName;

        [ObservableProperty]
        private string _lecteurNfcStatut = "Vérification...";

        [ObservableProperty]
        private string _configHash = "Non synchronisé";

        [ObservableProperty]
        private TerminalSettings _terminalConfig = new();

        public ApiSettingsViewModel ApiSettings { get; } = new();
        public NfcSettingsViewModel NfcSettings { get; } = new();
        public ApplicationsSettingsViewModel AppSettings { get; } = new();
        public SecuritySettingsViewModel SecSettings { get; } = new();
        public LoggingSettingsViewModel LogSettings { get; } = new();

        public ExecutionMode[] ModesExecution { get; } = new[] { ExecutionMode.Production, ExecutionMode.Test, ExecutionMode.Debug };

        public EcranParametresVueModele(
            IConfigurationService configurationService,
            IJournalisationService journalisationService,
            ApiClientService apiClientService,
            IServiceChiffrement serviceChiffrement,
            IServiceValidation serviceValidation,
            INavigationService navigationService,
            System.IServiceProvider serviceProvider)
        {
            _configurationService = configurationService;
            _journalisationService = journalisationService;
            _apiClientService = apiClientService;
            _serviceChiffrement = serviceChiffrement;
            _serviceValidation = serviceValidation;
            _navigationService = navigationService;
            _serviceProvider = serviceProvider;
        }

        [ObservableProperty]
        private ObservableCollection<ManagedApp> _centrallyManagedApps = new ObservableCollection<ManagedApp>();

        public void Initialiser()
        {
            Parametres = _configurationService.ChargerParametres();

            ApiSettings.Charger(Parametres.Api);
            NfcSettings.Charger(Parametres.Nfc);
            AppSettings.Charger(Parametres.Applications);
            SecSettings.Charger(Parametres.Securite);
            LogSettings.Charger(Parametres.Journalisation);
            TerminalConfig = Parametres.Terminal;

            var logs = _journalisationService.ObtenirHistorique();
            HistoriqueLogs.Clear();
            foreach (var log in logs)
                HistoriqueLogs.Add(log);

            StatutImport = string.Empty;

            // Fetch Hardware Diagnostics
            MachineName = Environment.MachineName;

            var nfcService = _serviceProvider.GetService<INfcService>();
            LecteurNfcStatut = (nfcService != null && nfcService.IsReaderConnected)
                ? "Connecté et Prêt"
                : "Non Détecté";

            // Determine Config Hash
            string dest = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(dest))
            {
                ConfigHash = File.GetLastWriteTimeUtc(dest).Ticks.ToString();
            }
            else
            {
                ConfigHash = "Défaut usine";
            }
        }

        // ── Import appsettings.json ─────────────────────────────
        [RelayCommand]
        private void ImporterAppsettings()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Importer un fichier de configuration",
                Filter = "Configuration JSON|appsettings.json|JSON|*.json",
                FileName = "appsettings.json"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                string json = File.ReadAllText(dlg.FileName);
                using var doc = JsonDocument.Parse(json);

                var apiNode = doc.RootElement
                    .GetProperty("ParametresApplication")
                    .GetProperty("Api");

                string? baseUrl = apiNode.GetProperty("BaseUrl").GetString();
                string? apiKey = apiNode.GetProperty("ApiKey").GetString();

                if (!string.IsNullOrWhiteSpace(baseUrl)) ApiSettings.BaseUrl = baseUrl;
                if (!string.IsNullOrWhiteSpace(apiKey)) ApiSettings.ApiKey = apiKey;

                // Overwrite current appsettings.json on disk
                string dest = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                File.Copy(dlg.FileName, dest, overwrite: true);

                // Apply immediately — no restart needed
                _apiClientService.UpdateConfig(ApiSettings.BaseUrl, ApiSettings.ApiKey);

                // Full refresh of all internal properties
                Initialiser();

                _journalisationService.EnregistrerInformation($"Configuration importée depuis : {dlg.FileName}");

                StatutImport = $"✅ Connecté à : {ApiSettings.BaseUrl}";

                System.Windows.MessageBox.Show(
                    $"Configuration importée avec succès !\n\nServeur : {ApiSettings.BaseUrl}\nLa connexion et le mot de passe admin sont maintenant actifs.",
                    "Import réussi",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatutImport = $"❌ Erreur : {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"Impossible de lire le fichier :\n{ex.Message}",
                    "Erreur d'import",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void TesterConnexionApi()
        {
            ApiSettings.StatutTestApi = "Test de connexion à l'API en cours...";

            System.Threading.Tasks.Task.Run(async () =>
            {
                bool resultat = await _apiClientService.TesterConnexionAsync(ApiSettings.BaseUrl, ApiSettings.ApiKey);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ApiSettings.StatutTestApi = resultat
                        ? "✅ Connexion à l'API réussie."
                        : "❌ Échec de la connexion. Vérifiez l'URL.";
                });
            });
        }

        [RelayCommand]
        private void Sauvegarder()
        {
            bool valide = true;
            valide &= ApiSettings.Valider(_serviceValidation);
            valide &= NfcSettings.Valider();
            valide &= AppSettings.Valider(_serviceValidation);
            valide &= SecSettings.Valider(_serviceValidation);
            valide &= LogSettings.Valider(_serviceValidation);

            if (!valide)
            {
                var erreurs = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(ApiSettings.ErreurValidation)) erreurs.Add("API : " + ApiSettings.ErreurValidation);
                if (!string.IsNullOrEmpty(NfcSettings.ErreurValidation)) erreurs.Add("NFC : " + NfcSettings.ErreurValidation);
                if (!string.IsNullOrEmpty(AppSettings.ErreurValidation)) erreurs.Add("Apps : " + AppSettings.ErreurValidation);
                if (!string.IsNullOrEmpty(SecSettings.ErreurValidation)) erreurs.Add("Sécurité : " + SecSettings.ErreurValidation);
                if (!string.IsNullOrEmpty(LogSettings.ErreurValidation)) erreurs.Add("Logs : " + LogSettings.ErreurValidation);

                string detailMessage = string.Join("\n• ", erreurs);
                System.Windows.MessageBox.Show(
                    $"Certains paramètres saisis sont invalides :\n\n• {detailMessage}\n\nVeuillez corriger ces erreurs avant de sauvegarder.",
                    "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            ApiSettings.Sauvegarder(Parametres.Api);
            NfcSettings.Sauvegarder(Parametres.Nfc);
            AppSettings.Sauvegarder(Parametres.Applications);
            SecSettings.Sauvegarder(Parametres.Securite);
            LogSettings.Sauvegarder(Parametres.Journalisation);

            _configurationService.SauvegarderParametres(Parametres);
            _journalisationService.EnregistrerInformation("Configuration d'entreprise modifiée et sauvegardée.");

            _apiClientService.UpdateConfig(ApiSettings.BaseUrl, ApiSettings.ApiKey);

            System.Windows.MessageBox.Show(
                "L'environnement applicatif a été configuré avec succès.\nNote: Toute altération d'Active Directory ou système nécessite le redémarrage de l'application.",
                "Succès", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            Fermer();
        }

        [RelayCommand]
        private void Fermer()
        {
            var attenteVm = _serviceProvider.GetRequiredService<EcranAttenteVueModele>();
            _navigationService.NaviguerVers(attenteVm);
        }
    }
}
