using System;

namespace Nexorsys.NFC.APP.Modeles
{
    public class ParametresApplication
    {
        public ApiSettings Api { get; set; } = new();
        public NfcSettings Nfc { get; set; } = new();
        public ApplicationPaths Applications { get; set; } = new();
        public SecuritySettings Securite { get; set; } = new();
        public LoggingSettings Journalisation { get; set; } = new();
        public ExecutionMode ModeExecution { get; set; } = ExecutionMode.Production;
        
        public string MotDePasseAdmin { get; set; } = string.Empty;

        // Pointers for legacy mapping gracefully
        public bool ModeSimulationNfc { get => Nfc.ActiverSimulation; set => Nfc.ActiverSimulation = value; }
        public string CheminEmed { get => Applications.CheminEmed; set => Applications.CheminEmed = value; }
        public string CheminSigems { get => Applications.CheminSigems; set => Applications.CheminSigems = value; }
        public string CheminBlueKango { get => Applications.CheminBlueKango; set => Applications.CheminBlueKango = value; }
    }

    public class ApiSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string OrganizationId { get; set; } = string.Empty;
        public string WorkstationId { get; set; } = string.Empty;
        public string ClientCertificateThumbprint { get; set; } = string.Empty;
    }

    public class NfcSettings
    {
        public string TypeLecteur { get; set; } = "Auto"; // Auto, ACR122U, Personne
        public bool ActiverSimulation { get; set; } = false;
        public string UidSimule { get; set; } = "A1B2C3D4";
        public System.Collections.Generic.List<string> BadgesSimules { get; set; } = new() { "A1B2C3D4", "TEST-BADGE-99" };
    }

    public class ApplicationPaths
    {
        public string CheminEmed { get; set; } = string.Empty;
        public string CheminHestia { get; set; } = string.Empty;
        public string CheminSigems { get; set; } = string.Empty;
        public string CheminBlueKango { get; set; } = string.Empty;
    }

    public class SecuritySettings
    {
        public int MaxTentatives { get; set; } = 3;
        public int DureeBlocageMinutes { get; set; } = 5;
        public int DureeExpirationSessionMinutes { get; set; } = 15;
        public bool VerrouillageAuto { get; set; } = true;
        public bool LancementAutomatique { get; set; } = false;
    }

    public class LoggingSettings
    {
        public string CheminDossier { get; set; } = "Journaux";
        public string NiveauJournalisation { get; set; } = "Info"; // Info, Warning, Error, Debug
        public bool JournalisationDetaillee { get; set; } = false;
    }

    public enum ExecutionMode
    {
        Production,
        Test,
        Debug
    }
}
