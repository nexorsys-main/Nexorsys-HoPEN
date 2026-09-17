using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using Pinede.NFC.APP.Modeles;

namespace Pinede.NFC.APP.Services
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

                JsonElement paramElement = default;
                bool found = false;
                foreach (var prop in document.RootElement.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "ParametresApplication", StringComparison.OrdinalIgnoreCase))
                    {
                        paramElement = prop.Value;
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize<ParametresApplication>(paramElement.GetRawText(), options) ?? new ParametresApplication();
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

                // Enregistrement du protocole pinedenfc://
                EnregistrerProtocoleLancement();
            }
            catch { }
        }

        private void EnregistrerProtocoleLancement()
        {
            try
            {
                string? cheminExe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(cheminExe)) return;

                using RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\pinedenfc");
                key.SetValue("", "URL:Pinede NFC Protocol");
                key.SetValue("URL Protocol", "");

                using RegistryKey shellKey = key.CreateSubKey(@"shell\open\command");
                shellKey.SetValue("", $"\"{cheminExe}\" \"%1\"");
            }
            catch { }
        }

        public string ObtenirUrlApi()
        {
            var p = ChargerParametres();
            return p.Api.BaseUrl;
        }

        private void GererRegistreDemarrageAuto(bool activer)
        {
            try
            {
                const string nomCle = "PinedeNFCKiosque";
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
