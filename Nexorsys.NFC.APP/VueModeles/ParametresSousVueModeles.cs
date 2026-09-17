using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pinede.NFC.APP.Modeles;

namespace Pinede.NFC.APP.VueModeles
{
    public partial class ApiSettingsViewModel : ObservableObject
    {
        [ObservableProperty] private string _baseUrl = string.Empty;
        [ObservableProperty] private string _apiKey = string.Empty;
        [ObservableProperty] private string _statutTestApi = string.Empty;
        [ObservableProperty] private string _erreurValidation = string.Empty;

        public void Charger(ApiSettings param)
        {
            BaseUrl = param.BaseUrl ?? string.Empty;
            ApiKey = param.ApiKey ?? string.Empty;
            StatutTestApi = string.Empty;
        }

        public void Sauvegarder(ApiSettings param)
        {
            param.BaseUrl = BaseUrl;
            param.ApiKey = ApiKey;
        }

        public bool Valider(Services.IServiceValidation validation)
        {
            ErreurValidation = string.Empty;
            if (!validation.ValiderUrl(BaseUrl)) { ErreurValidation = "L'URL de l'API est invalide."; return false; }
            return true;
        }
    }

    public partial class NfcSettingsViewModel : ObservableObject
    {
        [ObservableProperty] private string _typeLecteur = "Auto";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ModeReelActif))]
        private bool _activerSimulation;

        public bool ModeReelActif
        {
            get => !ActiverSimulation;
            set => ActiverSimulation = !value;
        }

        [ObservableProperty] private string _uidSimule = string.Empty;
        [ObservableProperty] private string _erreurValidation = string.Empty;

        [ObservableProperty] private ObservableCollection<string> _badgesSimules = new();
        [ObservableProperty] private string _nouveauBadgeUid = string.Empty;

        public string[] TypesLecteur { get; } = new[] { "Auto", "ACR122U", "Personnalisé" };

        public void Charger(NfcSettings param)
        {
            TypeLecteur = param.TypeLecteur ?? "Auto";
            ActiverSimulation = param.ActiverSimulation;
            UidSimule = param.UidSimule ?? string.Empty;

            BadgesSimules.Clear();
            if (param.BadgesSimules != null)
            {
                foreach (var b in param.BadgesSimules) BadgesSimules.Add(b);
            }
        }

        public void Sauvegarder(NfcSettings param)
        {
            param.TypeLecteur = TypeLecteur;
            param.ActiverSimulation = ActiverSimulation;
            param.UidSimule = UidSimule;
            param.BadgesSimules = new System.Collections.Generic.List<string>(BadgesSimules);
        }

        [RelayCommand]
        private void AjouterBadge()
        {
            if (!string.IsNullOrWhiteSpace(NouveauBadgeUid))
            {
                string cleanUid = NouveauBadgeUid.Trim().ToUpper();
                if (!BadgesSimules.Contains(cleanUid))
                {
                    BadgesSimules.Add(cleanUid);
                    UidSimule = cleanUid; // Sélectionne automatiquement le nouveau
                }
                NouveauBadgeUid = string.Empty;
            }
        }

        [RelayCommand]
        private void SupprimerBadge(string badge)
        {
            if (badge != null && BadgesSimules.Contains(badge))
            {
                BadgesSimules.Remove(badge);
                if (UidSimule == badge)
                    UidSimule = BadgesSimules.Count > 0 ? BadgesSimules[0] : string.Empty;
            }
        }

        public bool Valider()
        {
            ErreurValidation = string.Empty;
            if (ActiverSimulation && string.IsNullOrWhiteSpace(UidSimule)) { ErreurValidation = "L'UID simulé est requis."; return false; }
            return true;
        }
    }

    public partial class ApplicationsSettingsViewModel : ObservableObject
    {
        [ObservableProperty] private string _cheminEmed = string.Empty;
        [ObservableProperty] private string _cheminHestia = string.Empty;
        [ObservableProperty] private string _cheminBlueKango = string.Empty;
        [ObservableProperty] private string _cheminSigems = string.Empty;
        [ObservableProperty] private string _erreurValidation = string.Empty;

        public void Charger(ApplicationPaths param)
        {
            CheminEmed = param.CheminEmed ?? string.Empty;
            CheminHestia = param.CheminHestia ?? string.Empty;
            CheminBlueKango = param.CheminBlueKango ?? string.Empty;
            CheminSigems = param.CheminSigems ?? string.Empty;
        }

        public void Sauvegarder(ApplicationPaths param)
        {
            param.CheminEmed = CheminEmed;
            param.CheminHestia = CheminHestia;
            param.CheminBlueKango = CheminBlueKango;
            param.CheminSigems = CheminSigems;
        }

        private bool ValiderFlexible(string input, Services.IServiceValidation validation)
        {
            if (string.IsNullOrWhiteSpace(input)) return true;
            return validation.ValiderUrl(input) || validation.ValiderFichier(input);
        }

        public bool Valider(Services.IServiceValidation validation)
        {
            ErreurValidation = string.Empty;
            if (!ValiderFlexible(CheminEmed, validation)) { ErreurValidation = "EMED: URL ou Chemin invalide."; return false; }
            if (!ValiderFlexible(CheminHestia, validation)) { ErreurValidation = "Hestia: URL ou Chemin invalide."; return false; }
            if (!ValiderFlexible(CheminBlueKango, validation)) { ErreurValidation = "BlueKango: URL ou Chemin invalide."; return false; }
            if (!ValiderFlexible(CheminSigems, validation)) { ErreurValidation = "SIGEMS: URL ou Chemin invalide."; return false; }
            return true;
        }
    }

    public partial class SecuritySettingsViewModel : ObservableObject
    {
        [ObservableProperty] private string _maxTentatives = "3";
        [ObservableProperty] private string _dureeBlocage = "5";
        [ObservableProperty] private string _dureeExpiration = "15";
        [ObservableProperty] private bool _verrouillageAuto;
        [ObservableProperty] private bool _lancementAutomatique;
        [ObservableProperty] private string _erreurValidation = string.Empty;

        public void Charger(SecuritySettings param)
        {
            MaxTentatives = param.MaxTentatives.ToString();
            DureeBlocage = param.DureeBlocageMinutes.ToString();
            DureeExpiration = param.DureeExpirationSessionMinutes.ToString();
            VerrouillageAuto = param.VerrouillageAuto;
            LancementAutomatique = param.LancementAutomatique;
        }

        public void Sauvegarder(SecuritySettings param)
        {
            if (int.TryParse(MaxTentatives, out int m)) param.MaxTentatives = m;
            if (int.TryParse(DureeBlocage, out int d)) param.DureeBlocageMinutes = d;
            if (int.TryParse(DureeExpiration, out int e)) param.DureeExpirationSessionMinutes = e;
            param.VerrouillageAuto = VerrouillageAuto;
            param.LancementAutomatique = LancementAutomatique;

            AppliquerRegistreWindows();
        }

        private void AppliquerRegistreWindows()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    string appName = "PinedeNfcKiosque";
                    if (LancementAutomatique)
                    {
                        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                            key.SetValue(appName, $"\"{exePath}\"");
                    }
                    else
                    {
                        key.DeleteValue(appName, false);
                    }
                }
            }
            catch { /* Silencieux si droits restreints */ }
        }

        public bool Valider(Services.IServiceValidation validation)
        {
            ErreurValidation = string.Empty;
            if (!validation.ValiderEntierPositif(MaxTentatives, out _)) { ErreurValidation = "Max Tentatives doit être > 0."; return false; }
            if (!validation.ValiderEntierPositif(DureeBlocage, out _)) { ErreurValidation = "Durée blocage doit être > 0."; return false; }
            if (!validation.ValiderEntierPositif(DureeExpiration, out _)) { ErreurValidation = "Durée expiration doit être > 0."; return false; }
            return true;
        }
    }

    public partial class LoggingSettingsViewModel : ObservableObject
    {
        [ObservableProperty] private string _cheminDossier = string.Empty;
        [ObservableProperty] private string _niveauJournalisation = "Info";
        [ObservableProperty] private bool _journalisationDetaillee;
        [ObservableProperty] private string _erreurValidation = string.Empty;

        public string[] Niveaux { get; } = new[] { "Info", "Warning", "Error", "Debug" };

        public void Charger(LoggingSettings param)
        {
            CheminDossier = param.CheminDossier ?? string.Empty;
            NiveauJournalisation = param.NiveauJournalisation ?? "Info";
            JournalisationDetaillee = param.JournalisationDetaillee;
        }

        public void Sauvegarder(LoggingSettings param)
        {
            param.CheminDossier = CheminDossier;
            param.NiveauJournalisation = NiveauJournalisation;
            param.JournalisationDetaillee = JournalisationDetaillee;
        }

        public bool Valider(Services.IServiceValidation validation)
        {
            ErreurValidation = string.Empty;
            if (string.IsNullOrWhiteSpace(CheminDossier)) { ErreurValidation = "Le dossier est requis."; return false; }
            return true;
        }
    }
}
