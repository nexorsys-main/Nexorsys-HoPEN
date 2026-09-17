using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Pinede.NFC.APP.Modeles;

namespace Pinede.NFC.APP.Services
{
    public class ApiClientService
    {
        private HttpClient _httpClient;
        private readonly INfcService _nfcService;
        private readonly IJournalisationService _journalisation;

        private string _currentConfigHash = "initial";
        private string _apiKey = "";

        public string? JwtToken { get; private set; }
        public string? CurrentUserId { get; private set; }
        public List<string> ApplicationsAutorisees { get; private set; } = new List<string>();

        public ApiClientService(IOptions<ParametresApplication> options, INfcService nfcService, IJournalisationService journalisation)
        {
            _nfcService = nfcService;
            _journalisation = journalisation;

            // Forward logs to server
            _journalisation.EvenementEnregistre += (evt) => _ = TraceAsync(evt.Niveau, evt.Message, evt.UidBadge);
            _httpClient = new HttpClient();
            var baseUrl = options.Value.Api.BaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = "http://localhost:5000/api/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            _httpClient.BaseAddress = new Uri(baseUrl);

            var apiKey = options.Value.Api.ApiKey;
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _apiKey = apiKey;
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            }
            _httpClient.DefaultRequestHeaders.Add("X-Machine-Name", Environment.MachineName);

            // Load last applied config hash from a dedicated file to avoid content mismatch bugs
            try
            {
                string hashFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory, ".config_hash");
                if (System.IO.File.Exists(hashFile))
                {
                    _currentConfigHash = System.IO.File.ReadAllText(hashFile).Trim();
                }
                else
                {
                    _currentConfigHash = "initial";
                }
            }
            catch
            {
                _currentConfigHash = "initial";
            }

            StartHeartbeat();
        }

        public void UpdateConfig(string baseUrl, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = "http://localhost:5000/api/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";

            var newClient = new HttpClient();
            newClient.BaseAddress = new Uri(baseUrl);

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                newClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            }
            newClient.DefaultRequestHeaders.Add("X-Machine-Name", Environment.MachineName);

            _httpClient = newClient;
        }

        private void StartHeartbeat()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var content = JsonContent.Create(new
                        {
                            Hostname = Environment.MachineName,
                            AppVersion = "1.5.0",
                            ConfigHash = _currentConfigHash,
                            ApiKey = _apiKey
                        });

                        var response = await _httpClient.PostAsync("fleet/heartbeat", content);
                        if (response.IsSuccessStatusCode)
                        {
                            var data = await response.Content.ReadFromJsonAsync<JsonElement>();
                            if (data.TryGetProperty("latestConfigHash", out var hashElem))
                            {
                                string latestHash = hashElem.GetString() ?? "";
                                if (latestHash != _currentConfigHash && !string.IsNullOrEmpty(latestHash))
                                {
                                    // Config changed! Download it and restart.
                                    var configResponse = await _httpClient.GetAsync($"fleet/config?apiKey={Uri.EscapeDataString(_apiKey)}");
                                    if (configResponse.IsSuccessStatusCode)
                                    {
                                        var jsonConfig = await configResponse.Content.ReadAsStringAsync();

                                        // Try parse to ensure it's valid JSON before saving
                                        JsonDocument.Parse(jsonConfig);

                                        string dest = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory, "appsettings.json");
                                        string hashFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory, ".config_hash");

                                        System.IO.File.WriteAllText(dest, jsonConfig);
                                        System.IO.File.WriteAllText(hashFile, latestHash);

                                        _journalisation.EnregistrerInformation("Nouvelle configuration reçue depuis le serveur. Redémarrage du kiosque...");

                                        // Restart App with robust method
                                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            try
                                            {
                                                var exePath = System.Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
                                                var psi = new System.Diagnostics.ProcessStartInfo
                                                {
                                                    FileName = exePath,
                                                    UseShellExecute = true
                                                };
                                                System.Diagnostics.Process.Start(psi);
                                                System.Windows.Application.Current.Shutdown();
                                            }
                                            catch (Exception ex)
                                            {
                                                _journalisation.EnregistrerErreur("Échec du redémarrage auto: " + ex.Message);
                                                // Fallback: just shutdown, the task scheduler or user will restart
                                                System.Windows.Application.Current.Shutdown();
                                            }
                                        });
                                        return; // Exit loop
                                    }
                                }
                            }
                        }
                    }
                    catch { /* Ignore heartbeat errors */ }

                    await Task.Delay(TimeSpan.FromSeconds(60));
                }
            });
        }

        public async Task<bool> TesterConnexionAsync(string url, string apiKey)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var uri = new Uri(url.EndsWith("/") ? url : url + "/");
                client.BaseAddress = uri;

                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey.Trim());
                }

                var content = JsonContent.Create(new { MachineName = "Test_Connection" });
                var response = await client.PostAsync("kiosk/heartbeat", content);

                if (!response.IsSuccessStatusCode)
                {
                    var msg = await response.Content.ReadAsStringAsync();
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        System.Windows.MessageBox.Show($"Erreur API ({response.StatusCode}): {msg}", "Test de connexion", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error)
                    );
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    System.Windows.MessageBox.Show($"Exception réseau : {ex.Message}", "Test de connexion", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error)
                );
                return false;
            }
        }

        public async Task<KioskAuthResult?> IdentifierUserAsync(string badgeUid)
        {
            try
            {
                var identifyContent = JsonContent.Create(new { BadgeUid = badgeUid, MachineName = Environment.MachineName });
                var identifyResponse = await _httpClient.PostAsync("kiosk/identify", identifyContent);

                if (!identifyResponse.IsSuccessStatusCode)
                {
                    var msg = await identifyResponse.Content.ReadAsStringAsync();
                    try
                    {
                        var errorData = JsonSerializer.Deserialize<JsonElement>(msg);
                        if (errorData.TryGetProperty("message", out var m1)) msg = m1.GetString() ?? msg;
                        else if (errorData.TryGetProperty("Message", out var m2)) msg = m2.GetString() ?? msg;
                    }
                    catch { }
                    return new KioskAuthResult { ErrorMessage = msg };
                }

                var identityData = await identifyResponse.Content.ReadFromJsonAsync<JsonElement>();

                // Helper for case-insensitive lookup
                string GetProp(string name)
                {
                    if (identityData.TryGetProperty(name, out var p)) return p.GetString() ?? "";
                    // Try PascalCase
                    string pascal = char.ToUpper(name[0]) + name.Substring(1);
                    if (identityData.TryGetProperty(pascal, out var p2)) return p2.GetString() ?? "";
                    return "";
                }

                return new KioskAuthResult
                {
                    User = new KioskUser
                    {
                        Id = GetProp("userId"),
                        Name = GetProp("displayName")
                    }
                };
            }
            catch (Exception ex)
            {
                return new KioskAuthResult { ErrorMessage = $"Erreur réseau: {ex.Message}" };
            }
        }

        public async Task<KioskAuthResult?> AuthentifierAdminAsync(string username, string password)
        {
            try
            {
                var content = JsonContent.Create(new { Username = username, Password = password, MachineName = Environment.MachineName });
                var response = await _httpClient.PostAsync("kiosk/admin-login", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    try
                    {
                        var errorData = JsonSerializer.Deserialize<JsonElement>(errorMsg);
                        if (errorData.TryGetProperty("message", out var m1)) errorMsg = m1.GetString() ?? errorMsg;
                        else if (errorData.TryGetProperty("Message", out var m2)) errorMsg = m2.GetString() ?? errorMsg;
                    }
                    catch { }
                    return new KioskAuthResult { ErrorMessage = errorMsg ?? "Accès refusé" };
                }

                return new KioskAuthResult { User = new KioskUser { Name = username } };
            }
            catch (Exception ex)
            {
                return new KioskAuthResult { ErrorMessage = $"Erreur réseau: {ex.Message}" };
            }
        }

        public async Task<KioskAuthResult?> AuthentifierParBadgeAsync(string badgeUid, string password)
        {
            try
            {
                // 1. Identify first to check status
                var identResult = await IdentifierUserAsync(badgeUid);
                if (identResult == null) return null;
                if (!string.IsNullOrEmpty(identResult.ErrorMessage)) return identResult;

                var userId = identResult.User.Id;

                // 2. Validate PIN
                var pinContent = JsonContent.Create(new { UserId = userId, Pin = password, BadgeUid = badgeUid, NfcUid = badgeUid });
                var pinResponse = await _httpClient.PostAsync("kiosk/validate-pin", pinContent);

                if (!pinResponse.IsSuccessStatusCode)
                {
                    var errorMsg = await pinResponse.Content.ReadAsStringAsync();
                    return new KioskAuthResult { ErrorMessage = errorMsg };
                }

                var pinData = await pinResponse.Content.ReadFromJsonAsync<JsonElement>();

                bool success = true;
                if (pinData.TryGetProperty("success", out var s1)) success = s1.GetBoolean();
                else if (pinData.TryGetProperty("Success", out var s2)) success = s2.GetBoolean();

                if (!success)
                    return new KioskAuthResult { ErrorMessage = "Réponse invalide du serveur." };

                JwtToken = pinData.TryGetProperty("token", out var t1) ? t1.GetString() :
                           (pinData.TryGetProperty("Token", out var t2) ? t2.GetString() :
                           (pinData.TryGetProperty("sessionToken", out var t3) ? t3.GetString() : Guid.NewGuid().ToString()));

                CurrentUserId = userId;

                bool mustChangePin = false;
                if (pinData.TryGetProperty("mustChangePin", out var mcp1)) mustChangePin = mcp1.GetBoolean();
                else if (pinData.TryGetProperty("MustChangePin", out var mcp2)) mustChangePin = mcp2.GetBoolean();

                var apps = new List<ManagedApp>();
                if (pinData.TryGetProperty("applications", out var a1) || pinData.TryGetProperty("Applications", out var a2))
                {
                    var appsElement = pinData.TryGetProperty("applications", out var a3) ? a3 : pinData.GetProperty("Applications");
                    apps = JsonSerializer.Deserialize<List<ManagedApp>>(appsElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ManagedApp>();
                }

                return new KioskAuthResult
                {
                    Token = JwtToken ?? Guid.NewGuid().ToString(),
                    MustChangePin = mustChangePin,
                    User = identResult.User,
                    Applications = apps
                };
            }
            catch (Exception ex)
            {
                return new KioskAuthResult { ErrorMessage = $"Erreur réseau: {ex.Message}" };
            }
        }

        public async Task<KioskAuthResult?> AuthentifierSsoAsync(string badgeUid, string userId, string windowsUserName)
        {
            try
            {
                var ssoContent = JsonContent.Create(new { UserId = Guid.Parse(userId), BadgeUid = badgeUid, WindowsUserName = windowsUserName, MachineName = Environment.MachineName });
                var ssoResponse = await _httpClient.PostAsync("kiosk/validate-sso", ssoContent);

                if (!ssoResponse.IsSuccessStatusCode)
                {
                    var errorMsg = await ssoResponse.Content.ReadAsStringAsync();
                    return new KioskAuthResult { ErrorMessage = errorMsg };
                }

                var data = await ssoResponse.Content.ReadFromJsonAsync<JsonElement>();

                JwtToken = data.TryGetProperty("token", out var t1) ? t1.GetString() :
                           (data.TryGetProperty("Token", out var t2) ? t2.GetString() :
                           (data.TryGetProperty("sessionToken", out var t3) ? t3.GetString() : Guid.NewGuid().ToString()));

                CurrentUserId = userId;

                bool mustChangePin = false;
                if (data.TryGetProperty("mustChangePin", out var mcp1)) mustChangePin = mcp1.GetBoolean();
                else if (data.TryGetProperty("MustChangePin", out var mcp2)) mustChangePin = mcp2.GetBoolean();

                var apps = new List<ManagedApp>();
                if (data.TryGetProperty("applications", out var a1) || data.TryGetProperty("Applications", out var a2))
                {
                    var appsElement = data.TryGetProperty("applications", out var a3) ? a3 : data.GetProperty("Applications");
                    apps = JsonSerializer.Deserialize<List<ManagedApp>>(appsElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ManagedApp>();
                }

                var userElem = data.TryGetProperty("user", out var u1) ? u1 : (data.TryGetProperty("User", out var u2) ? u2 : new JsonElement());
                string GetUserProp(string name)
                {
                    if (userElem.ValueKind != JsonValueKind.Undefined)
                    {
                        if (userElem.TryGetProperty(name, out var p)) return p.GetString() ?? "";
                        string pascal = char.ToUpper(name[0]) + name.Substring(1);
                        if (userElem.TryGetProperty(pascal, out var p2)) return p2.GetString() ?? "";
                    }
                    return "";
                }

                var user = new KioskUser
                {
                    Id = GetUserProp("userId") == "" ? GetUserProp("id") : GetUserProp("userId"),
                    Name = GetUserProp("displayName") == "" ? GetUserProp("name") : GetUserProp("displayName"),
                    SamAccountName = GetUserProp("samAccountName"),
                    Department = GetUserProp("department")
                };

                return new KioskAuthResult
                {
                    Token = JwtToken ?? Guid.NewGuid().ToString(),
                    MustChangePin = mustChangePin,
                    User = user,
                    Applications = apps
                };
            }
            catch (Exception ex)
            {
                return new KioskAuthResult { ErrorMessage = $"Erreur SSO: {ex.Message}" };
            }
        }

        public async Task<KioskAuthResult?> ObtenirSessionAccueilAsync(string samAccountName)
        {
            try
            {
                var response = await _httpClient.GetAsync($"kiosk/welcome-session/{samAccountName}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    return new KioskAuthResult { ErrorMessage = errorMsg };
                }

                var data = await response.Content.ReadFromJsonAsync<JsonElement>();

                JwtToken = data.TryGetProperty("token", out var t1) ? t1.GetString() :
                           (data.TryGetProperty("Token", out var t2) ? t2.GetString() : Guid.NewGuid().ToString());

                bool mustChangePin = false;
                if (data.TryGetProperty("mustChangePin", out var mcp1)) mustChangePin = mcp1.GetBoolean();
                else if (data.TryGetProperty("MustChangePin", out var mcp2)) mustChangePin = mcp2.GetBoolean();

                var apps = new List<ManagedApp>();
                if (data.TryGetProperty("applications", out var a1) || data.TryGetProperty("Applications", out var a2))
                {
                    var appsElement = data.TryGetProperty("applications", out var a3) ? a3 : data.GetProperty("Applications");
                    apps = JsonSerializer.Deserialize<List<ManagedApp>>(appsElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ManagedApp>();
                }

                var userElem = data.TryGetProperty("user", out var u1) ? u1 : (data.TryGetProperty("User", out var u2) ? u2 : new JsonElement());
                string GetUserProp(string name)
                {
                    if (userElem.ValueKind != JsonValueKind.Undefined)
                    {
                        if (userElem.TryGetProperty(name, out var p)) return p.GetString() ?? "";
                        string pascal = char.ToUpper(name[0]) + name.Substring(1);
                        if (userElem.TryGetProperty(pascal, out var p2)) return p2.GetString() ?? "";
                    }
                    return "";
                }

                var user = new KioskUser
                {
                    Id = GetUserProp("userId") == "" ? GetUserProp("id") : GetUserProp("userId"),
                    Name = GetUserProp("displayName") == "" ? GetUserProp("name") : GetUserProp("displayName"),
                    SamAccountName = GetUserProp("samAccountName"),
                    Department = GetUserProp("department")
                };

                CurrentUserId = user.Id;

                return new KioskAuthResult
                {
                    Token = JwtToken ?? Guid.NewGuid().ToString(),
                    MustChangePin = mustChangePin,
                    User = user,
                    Applications = apps
                };
            }
            catch (Exception ex)
            {
                return new KioskAuthResult { ErrorMessage = $"Erreur réseau: {ex.Message}" };
            }
        }

        public async Task TraceAsync(string level, string message, string? badgeUid = null)
        {
            try
            {
                var content = JsonContent.Create(new
                {
                    Level = level,
                    Message = message,
                    BadgeUid = badgeUid,
                    MachineName = Environment.MachineName
                });
                await _httpClient.PostAsync("kiosk/trace", content);
            }
            catch { /* Silent fail */ }
        }

        public async Task TerminerSessionAsync()
        {
            try
            {
                if (!string.IsNullOrEmpty(JwtToken))
                {
                    // You can call an endpoint to invalidate the session here if the backend supports it
                    // e.g. await _httpClient.PostAsync("kiosk/session/end", null);
                    JwtToken = null;
                    CurrentUserId = null;
                }
            }
            catch { /* Silent fail */ }
        }

        public async Task<bool> DemanderReinitialisationPinAsync(string userId, string badgeUid)
        {
            try
            {
                var content = JsonContent.Create(new { BadgeUid = badgeUid, Reason = "Code PIN Oublié" });
                var response = await _httpClient.PostAsync("pin-reset/request", content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> ChangerPinAsync(string userId, string nouveauPin)
        {
            try
            {
                var content = JsonContent.Create(new { UserId = Guid.Parse(userId), NewPin = nouveauPin });
                var response = await _httpClient.PostAsync("kiosk/reset-pin", content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task BadgeRemovedAsync(string sessionToken)
        {
            try
            {
                var content = JsonContent.Create(new { SessionToken = sessionToken });
                await _httpClient.PostAsync("windows-auth/badge-removed", content);
            }
            catch (Exception ex)
            {
                _journalisation.EnregistrerErreur("Erreur réseau BadgeRemoved: " + ex.Message);
            }
        }

        public async Task<Guid> StartAppSessionAsync(string applicationName)
        {
            if (string.IsNullOrEmpty(JwtToken)) return Guid.Empty;
            try
            {
                var content = JsonContent.Create(new { SessionToken = JwtToken, ApplicationName = applicationName, MachineName = Environment.MachineName });
                var response = await _httpClient.PostAsync("kiosk/app-session/start", content);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (data.TryGetProperty("appSessionId", out var idElem) && idElem.TryGetGuid(out var id))
                        return id;
                }
            }
            catch { }
            return Guid.Empty;
        }

        public async Task StopAppSessionAsync(Guid appSessionId)
        {
            if (appSessionId == Guid.Empty) return;
            try
            {
                var content = JsonContent.Create(new { AppSessionId = appSessionId });
                await _httpClient.PostAsync("kiosk/app-session/stop", content);
            }
            catch { }
        }

        public async Task<KioskAuthResult?> ValidateWindowsSessionAsync(string sessionToken)
        {
            try
            {
                var content = JsonContent.Create(new { SessionToken = sessionToken });
                var response = await _httpClient.PostAsync("windows-auth/session-validate", content);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (data.TryGetProperty("isValid", out var validElem) && validElem.GetBoolean())
                    {
                        var name = "Utilisateur";
                        var userId = "";
                        if (data.TryGetProperty("user", out var userElem))
                        {
                            if (userElem.TryGetProperty("name", out var nameElem))
                                name = nameElem.GetString() ?? name;
                            if (userElem.TryGetProperty("id", out var idElem))
                                userId = idElem.GetString() ?? "";
                        }

                        CurrentUserId = userId;

                        var apps = new List<ManagedApp>();
                        if (data.TryGetProperty("applications", out var a1) || data.TryGetProperty("Applications", out var a2))
                        {
                            var appsElement = data.TryGetProperty("applications", out var a3) ? a3 : data.GetProperty("Applications");
                            apps = JsonSerializer.Deserialize<List<ManagedApp>>(appsElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ManagedApp>();
                        }
                        else
                        {
                            // Default fallback
                            apps = new List<ManagedApp>
                            {
                                new ManagedApp { Id = "app1", Name = "Dossier Médical (EMED)", Path = "", Description = "EMED" },
                                new ManagedApp { Id = "app2", Name = "Qualité (BlueKango)", Path = "", Description = "BlueKango" }
                            };
                        }

                        return new KioskAuthResult
                        {
                            Token = sessionToken,
                            User = new KioskUser { Name = name, Id = userId },
                            Applications = apps
                        };
                    }
                }
                return new KioskAuthResult { ErrorMessage = "Session invalide ou expirée." };
            }
            catch (Exception ex)
            {
                _journalisation.EnregistrerErreur("Erreur validation session Windows: " + ex.Message);
                return new KioskAuthResult { ErrorMessage = "Erreur de connexion au serveur d'authentification." };
            }
        }

        public async Task<KioskAuthResult?> AutoConnectFromWindowsSessionAsync()
        {
            try
            {
                var userName = Environment.UserName;
                var possibleKeys = new[] { userName, $"PINEDE\\{userName}" };
                
                string? token = null;
                // Requires Microsoft.Win32.Registry but we are in WPF full .NET
                using (var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64))
                {
                    using (var key = baseKey.OpenSubKey(@"SOFTWARE\PinedeIdentity\Sessions"))
                    {
                        if (key != null)
                        {
                            foreach (var pk in possibleKeys)
                            {
                                var value = key.GetValue(pk);
                                if (value is byte[] bytes)
                                {
                                    token = System.Text.Encoding.UTF8.GetString(bytes);
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(token))
                {
                    _journalisation.EnregistrerInformation("Session Windows trouvée, tentative de validation SSO automatique...");
                    var result = await ValidateWindowsSessionAsync(token);
                    if (result != null && string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        JwtToken = result.Token;
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                _journalisation.EnregistrerErreur("Erreur lors de l'AutoConnect: " + ex.Message);
            }
            return null;
        }

        public async Task<string?> AutoCatchUrlAsync(IConfigurationService configService)
        {
            var p = configService.ChargerParametres();
            string configuredUrl = p.Api.BaseUrl;
            string apiKey = p.Api.ApiKey;

            // List of potential backend URLs to probe
            var candidateUrls = new List<string>
            {
                configuredUrl,
                "http://localhost:5000/api/",
                "http://127.0.0.1:5000/api/",
                "http://Innovera:5000/api/"
            };

            // Deduplicate candidate URLs (casing & trailing slash normalized)
            var cleanCandidates = new List<string>();
            foreach (var url in candidateUrls)
            {
                if (string.IsNullOrWhiteSpace(url)) continue;
                string testUrl = url.Trim();
                if (!testUrl.EndsWith("/")) testUrl += "/";
                if (!cleanCandidates.Exists(c => string.Equals(c, testUrl, StringComparison.OrdinalIgnoreCase)))
                {
                    cleanCandidates.Add(testUrl);
                }
            }

            _journalisation.EnregistrerInformation($"[Auto-Catch] Début de la détection automatique du backend. {cleanCandidates.Count} URLs candidates.");

            // Probe each URL concurrently or in sequence with a short timeout
            foreach (var url in cleanCandidates)
            {
                try
                {
                    _journalisation.EnregistrerInformation($"[Auto-Catch] Test de connexion : {url}");

                    using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2.5) })
                    {
                        // Probe using fleet/config?apiKey=... which is anonymous but requires correct API Key to return 200, 
                        // but if we get 401, it STILL proves that the server is online and running at that port!
                        var response = await client.GetAsync(new Uri(new Uri(url), $"fleet/config?apiKey={Uri.EscapeDataString(apiKey)}"));

                        // Any HTTP response from the endpoint proves the server is online at this candidate URL
                        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            _journalisation.EnregistrerInformation($"[Auto-Catch] Serveur API en ligne détecté à : {url}");

                            // If the working URL is different from the currently configured one, update configuration!
                            if (!string.Equals(configuredUrl, url, StringComparison.OrdinalIgnoreCase))
                            {
                                _journalisation.EnregistrerInformation($"[Auto-Catch] Mise à jour automatique de la configuration locale vers : {url}");
                                p.Api.BaseUrl = url;
                                configService.SauvegarderParametres(p);
                                UpdateConfig(url, apiKey);
                            }
                            return url;
                        }
                    }
                }
                catch
                {
                    // Ignore failure for this candidate and try the next one
                }
            }

            _journalisation.EnregistrerErreur("[Auto-Catch] Aucune URL candidate de l'API n'a répondu. Lancement du scan réseau...");

            // ----------------------------------------------------
            // ULTIMATE FALLBACK: Scan the local /24 subnet
            // ----------------------------------------------------
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        var ipBytes = ip.GetAddressBytes();
                        var tasks = new List<Task<string?>>();

                        _journalisation.EnregistrerInformation($"[Auto-Catch] Scan du sous-réseau {ipBytes[0]}.{ipBytes[1]}.{ipBytes[2]}.*");

                        for (int i = 1; i <= 254; i++)
                        {
                            string targetUrl = $"http://{ipBytes[0]}.{ipBytes[1]}.{ipBytes[2]}.{i}:5000/api/";
                            tasks.Add(Task.Run(async () =>
                            {
                                try
                                {
                                    using (var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(800) })
                                    {
                                        var response = await client.GetAsync(new Uri(new Uri(targetUrl), $"fleet/config?apiKey={Uri.EscapeDataString(apiKey)}"));
                                        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                                        {
                                            return targetUrl;
                                        }
                                    }
                                }
                                catch { }
                                return null;
                            }));
                        }

                        // Wait for any to succeed
                        var results = await Task.WhenAll(tasks);
                        var foundUrl = Array.Find(results, r => r != null);

                        if (foundUrl != null)
                        {
                            _journalisation.EnregistrerInformation($"[Auto-Catch] Serveur trouvé via scan réseau à : {foundUrl}");
                            p.Api.BaseUrl = foundUrl;
                            configService.SauvegarderParametres(p);
                            UpdateConfig(foundUrl, apiKey);
                            return foundUrl;
                        }
                    }
                }
            }
            catch { }

            _journalisation.EnregistrerErreur("[Auto-Catch] Le scan réseau a échoué. Impossible de trouver le serveur.");
            return null;
        }

        public async Task PostReaderStatusAsync(bool isConnected)
        {
            try
            {
                var content = System.Net.Http.Json.JsonContent.Create(new { isReaderConnected = isConnected });
                var response = await _httpClient.PostAsync("kiosk/heartbeat", content);
                if (!response.IsSuccessStatusCode)
                {
                    _journalisation.EnregistrerErreur($"Failed to post reader status. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _journalisation.EnregistrerErreur("Failed to post reader status", ex.Message);
            }
        }
    }

    public class KioskAuthResult
    {
        public string Token { get; set; } = string.Empty;
        public bool MustChangePin { get; set; }
        public KioskUser User { get; set; } = new KioskUser();
        public List<ManagedApp> Applications { get; set; } = new List<ManagedApp>();
        public string? ErrorMessage { get; set; }
    }

    public class KioskUser
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string SamAccountName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
    }
}
