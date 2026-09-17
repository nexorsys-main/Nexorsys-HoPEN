using System;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Security.Cryptography;
using Nexorsys.NFC.APP.Modeles;

namespace Nexorsys.NFC.APP.Services
{
    public sealed class LanceurApplicationService : ILanceurApplicationService
    {
        private readonly IJournalisationService _journalisation;
        private readonly IKioskApplicationSessionClient _apiClient;
        private readonly IAgentLaunchAuthorizationClient _agent;
        private readonly IApplicationProcessLauncher _processLauncher;

        public LanceurApplicationService(IJournalisationService journalisation, IKioskApplicationSessionClient apiClient,
            IAgentLaunchAuthorizationClient agent, IApplicationProcessLauncher processLauncher)
        {
            _journalisation = journalisation;
            _apiClient = apiClient;
            _agent = agent;
            _processLauncher = processLauncher;
        }

        public async Task<bool> LancerApplicationAsync(ManagedApp app)
        {
            if (app is null || string.IsNullOrWhiteSpace(app.Id)) return false;
            var applicationSessionId = await _apiClient.StartApplicationSessionAsync(app.Id);
            if (!applicationSessionId.HasValue)
            {
                _journalisation.EnregistrerErreur("Server did not authorize an application session.");
                return false;
            }

            try
            {
                var launch = await _agent.AuthorizeAsync(applicationSessionId.Value, CancellationToken.None);
                if (launch is null || !ExecutableLaunchGrantValidator.IsValid(launch, applicationSessionId.Value, DateTimeOffset.UtcNow))
                {
                    await _apiClient.StopApplicationSessionAsync(applicationSessionId.Value);
                    _journalisation.EnregistrerErreur("Windows Agent denied or could not validate the application launch.");
                    return false;
                }

                // Never use ManagedApp.Path, shell execution, a URL, or caller-controlled arguments.
                if (!_processLauncher.TryStart(launch.ExecutablePath,
                    () => _apiClient.StopApplicationSessionAsync(applicationSessionId.Value)))
                {
                    await _apiClient.StopApplicationSessionAsync(applicationSessionId.Value);
                    _journalisation.EnregistrerErreur("Authorized application process could not be started.");
                    return false;
                }
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or
                UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
            {
                await _apiClient.StopApplicationSessionAsync(applicationSessionId.Value);
                _journalisation.EnregistrerErreur("Application launch failed closed; no command-line arguments were supplied.");
                return false;
            }
        }

    }
}
