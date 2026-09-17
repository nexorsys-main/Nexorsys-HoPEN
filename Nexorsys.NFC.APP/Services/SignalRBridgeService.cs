using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Pinede.NFC.APP.Services
{
    public class SignalRBridgeService
    {
        private HubConnection? _connection;
        private readonly IJournalisationService _journalisation;
        private readonly IConfigurationService _config;
        private readonly INfcService _nfcService;
        private bool _isConnected = false;

        private System.Timers.Timer? _heartbeatTimer;

        private readonly ApiClientService _apiClient;

        public SignalRBridgeService(
            IJournalisationService journalisation,
            IConfigurationService config,
            INfcService nfcService,
            ApiClientService apiClient)
        {
            _journalisation = journalisation;
            _config = config;
            _nfcService = nfcService;
            _apiClient = apiClient;

            // Souscrire aux événements matériels
            _nfcService.BadgeDetecte += (s, uid) => _ = NotifyHardwareDetected(uid, "NFC");
            _nfcService.ReaderStatusChanged += (s, connected) => _ = NotifyReaderStatus(connected);

            InitializeConnection();
            StartHeartbeat();
        }

        private void StartHeartbeat()
        {
            _heartbeatTimer = new System.Timers.Timer(1000);
            _heartbeatTimer.Elapsed += async (s, e) =>
            {
                if (_connection?.State == HubConnectionState.Connected)
                {
                    await NotifyReaderStatus(_nfcService.IsReaderConnected);
                }
            };
            _heartbeatTimer.AutoReset = true;
            _heartbeatTimer.Start();
        }

        public event Action<string, bool>? UserStatusChanged;

        private void InitializeConnection()
        {
            string baseUrl = _config.ObtenirUrlApi(); // "http://localhost:5000/api"
            string hubUrl = baseUrl.Replace("/api", "").TrimEnd('/') + "/hubs/identity";

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            _connection.On<StatusChangeData>("OnUserStatusChanged", (data) =>
            {
                _journalisation.EnregistrerInformation($"SignalR: Status changed for {data.SamAccountName} -> IsActive={data.IsActive}");
                UserStatusChanged?.Invoke(data.UserId, data.IsActive);
            });

            _connection.On("RequestHardwareScan", () =>
            {
                _journalisation.EnregistrerInformation("SignalR: Requête de scan manuel reçue.");
                _nfcService.ForcerScan();
            });

            _connection.On<WriteBadgeData>("OnWriteBadgeRequested", (data) =>
            {
                if (string.Equals(data.TargetMachine, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                {
                    _journalisation.EnregistrerInformation($"SignalR: Ordre d'encodage reçu pour UID: {data.NewUid}");
                    _nfcService.PreparerEncodageCuid(data.NewUid);
                }
            });

            _connection.On<string>("UpdateAdminPassword", (newPassword) =>
            {
                _journalisation.EnregistrerInformation("SignalR: Mise à jour du mot de passe admin reçue.");
                var p = _config.ChargerParametres();
                p.MotDePasseAdmin = newPassword;
                _config.SauvegarderParametres(p);
            });

            _connection.Closed += async (error) =>
            {
                _isConnected = false;
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await ConnectWithRetryAsync();
            };

            _connection.Reconnected += (connectionId) =>
            {
                _isConnected = true;
                return Task.CompletedTask;
            };
        }

        public class StatusChangeData
        {
            [System.Text.Json.Serialization.JsonPropertyName("userId")]
            public string UserId { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("isActive")]
            public bool IsActive { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("samAccountName")]
            public string SamAccountName { get; set; } = string.Empty;
        }

        public class WriteBadgeData
        {
            [System.Text.Json.Serialization.JsonPropertyName("targetMachine")]
            public string TargetMachine { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("newUid")]
            public string NewUid { get; set; } = string.Empty;
        }

        public async Task ConnectWithRetryAsync()
        {
            if (_connection == null) return;
            if (_connection.State == HubConnectionState.Connected) return;

            try
            {
                await _connection.StartAsync();
                _isConnected = true;
                _journalisation.EnregistrerInformation("SignalR Bridge connected to IdentityHub");

                // Immediate status report on connection
                await NotifyReaderStatus(_nfcService.IsReaderConnected);
            }
            catch (Exception ex)
            {
                _journalisation.EnregistrerErreur("SignalR Connection failed", ex.Message);
            }
        }

        public async Task NotifyHardwareDetected(string identifier, string type)
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
            {
                await ConnectWithRetryAsync();
            }
            if (_connection != null && _connection.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("NotifyHardwareDetected", identifier, type, Environment.MachineName);
                }
                catch (Exception ex)
                {
                    _journalisation.EnregistrerErreur("Failed to send hardware notification", ex.Message);
                }
            }
        }

        public async Task NotifyReaderStatus(bool isConnected)
        {
            if (_connection != null && _connection.State == HubConnectionState.Connected)
            {
                try
                {
                    await _connection.InvokeAsync("NotifyReaderStatus", Environment.MachineName, isConnected);
                }
                catch (Exception ex)
                {
                    _journalisation.EnregistrerErreur("Failed to send reader status notification via SignalR", ex.Message);
                }
            }

            await _apiClient.PostReaderStatusAsync(isConnected);
        }
    }
}
