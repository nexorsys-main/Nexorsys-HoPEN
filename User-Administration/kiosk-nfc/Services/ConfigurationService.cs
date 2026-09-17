using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using Nexorsys.NFC.APP.Modeles;

namespace Nexorsys.NFC.APP.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly string _cheminFichier = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        public ParametresApplication ChargerParametres()
        {
            if (!File.Exists(_cheminFichier))
                return new ParametresApplication();

            try
            {
                var json = File.ReadAllText(_cheminFichier);
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("ParametresApplication", out var paramElement))
                {
                    return JsonSerializer.Deserialize<ParametresApplication>(paramElement.GetRawText()) ?? new ParametresApplication();
                }
            }
            catch { }
            return new ParametresApplication();
        }

        public void SauvegarderParametres(ParametresApplication parametres)
        {
            try
            {
                var objetGlobal = new { ParametresApplication = parametres };
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(objetGlobal, jsonOptions);
                File.WriteAllText(_cheminFichier, json);

                // Gestion du lancement automatique dans le registre Windows
                GererRegistreDemarrageAuto(parametres.Securite.LancementAutomatique);
            }
            catch { }
        }

        private void GererRegistreDemarrageAuto(bool activer)
        {
            try
            {
                const string nomCle = "NexorSysKiosk";
                string? cheminExe = Environment.ProcessPath; // .NET 6+

                if (string.IsNullOrEmpty(cheminExe)) return;

                using RegistryKey? rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (rk == null) return;

                if (activer)
                    rk.SetValue(nomCle, $"\"{cheminExe}\"");
                else
                    rk.DeleteValue(nomCle, false);
            }
            catch { }
        }
    }
}
