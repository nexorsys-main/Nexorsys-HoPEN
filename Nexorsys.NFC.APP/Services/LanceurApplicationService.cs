using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Pinede.NFC.APP.Modeles;

namespace Pinede.NFC.APP.Services
{
    public class LanceurApplicationService : ILanceurApplicationService
    {
        private readonly IOptionsMonitor<ParametresApplication> _optionsMonitor;
        private readonly IJournalisationService _journalisation;
        private readonly ApiClientService _apiClient;

        public LanceurApplicationService(
            IOptionsMonitor<ParametresApplication> optionsMonitor,
            IJournalisationService journalisation,
            ApiClientService apiClient)
        {
            _optionsMonitor = optionsMonitor;
            _journalisation = journalisation;
            _apiClient = apiClient;
        }

        public async void LancerApplication(ManagedApp app)
        {
            await DemarrerProcessusAsync(app.Path, app.Name);
        }

        private async Task DemarrerProcessusAsync(string chemin, string nomApplication)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(chemin))
                {
                    _journalisation.EnregistrerErreur($"Le chemin pour {nomApplication} n'est pas configuré.");
                    return;
                }

                Guid sessionId = await _apiClient.StartAppSessionAsync(nomApplication);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = chemin,
                    UseShellExecute = true // Requis pour lancer des raccourcis, URL ou exécutables Windows
                };

                var process = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true };
                process.Exited += async (sender, e) =>
                {
                    await _apiClient.StopAppSessionAsync(sessionId);
                    _journalisation.EnregistrerInformation($"Application {nomApplication} fermée.");
                };

                if (process.Start())
                {
                    _journalisation.EnregistrerInformation($"Application {nomApplication} démarrée avec succès.");
                }
            }
            catch (Exception ex)
            {
                _journalisation.EnregistrerErreur($"Impossible de lancer {nomApplication} à l'emplacement '{chemin}'. Erreur: {ex.Message}");
            }
        }
    }
}
