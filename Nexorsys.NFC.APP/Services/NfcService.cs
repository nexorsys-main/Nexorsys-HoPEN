using PCSC;
using PCSC.Iso7816;
using PCSC.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pinede.NFC.APP.Services
{
    public class NfcService : INfcService
    {
        private readonly IJournalisationService _journalisation;
        private ISCardMonitor? _monitor;
        private bool _isReaderConnected;

        public event EventHandler<string>? ModeEncodageActive;
        public event EventHandler<bool>? EncodageTermine;

        public event EventHandler<string>? BadgeDetecte;
        public event EventHandler<string>? ErreurLecture;
        public event EventHandler? BadgeRetire;
        public event EventHandler<bool>? ReaderStatusChanged;

        private string? _encodageEnAttenteUid = null;

        public bool IsReaderConnected => _isReaderConnected;

        private System.Timers.Timer? _rechercheTimer;

        public NfcService(IJournalisationService journalisation)
        {
            _journalisation = journalisation;

            // Initialisation du timer de recherche automatique (hot-plug)
            _rechercheTimer = new System.Timers.Timer(500);
            _rechercheTimer.Elapsed += (s, e) => TentativeDemarrage();
            _rechercheTimer.AutoReset = true;
        }

        public void DemarrerEcoute()
        {
            TentativeDemarrage();
        }

        private void TentativeDemarrage()
        {
            lock (this)
            {
                if (_isReaderConnected && _monitor != null) return;

                try
                {
                    // Fallback strategy: Try System scope, then User scope
                    string[] nomsLecteurs = Array.Empty<string>();

                    try
                    {
                        using var ctxSys = ContextFactory.Instance.Establish(SCardScope.System);
                        nomsLecteurs = ctxSys.GetReaders();
                    }
                    catch { }

                    if (nomsLecteurs == null || nomsLecteurs.Length == 0)
                    {
                        try
                        {
                            using var ctxUser = ContextFactory.Instance.Establish(SCardScope.User);
                            nomsLecteurs = ctxUser.GetReaders();
                        }
                        catch { }
                    }

                    if (nomsLecteurs == null || nomsLecteurs.Length == 0)
                    {
                        if (_isReaderConnected)
                        {
                            _isReaderConnected = false;
                            ReaderStatusChanged?.Invoke(this, false);
                        }

                        if (!_rechercheTimer!.Enabled) _rechercheTimer.Start();
                        return;
                    }

                    // Lecteurs trouvÃ©s : on arrête la recherche active et on démarre le moniteur
                    _rechercheTimer?.Stop();

                    _isReaderConnected = true;
                    ReaderStatusChanged?.Invoke(this, true);

                    // Use System scope for monitoring if possible, else User
                    SCardScope monitorScope = SCardScope.System;
                    try { using var test = ContextFactory.Instance.Establish(SCardScope.System); }
                    catch { monitorScope = SCardScope.User; }

                    _monitor = MonitorFactory.Instance.Create(monitorScope);
                    _monitor.StatusChanged += SurChangementStatut;
                    _monitor.CardInserted += (s, e) =>
                    {
                        _journalisation.EnregistrerInformation($"EVENEMENT: Carte insÃ©rÃ©e dans {e.ReaderName}");
                        LireUidBadge(e.ReaderName, e.Atr);
                    };
                    _monitor.MonitorException += (s, e) =>
                    {
                        _journalisation.EnregistrerErreur($"Erreur moniteur NFC (dÃ©branchement ?): {e.Message}");
                        _isReaderConnected = false;
                        ReaderStatusChanged?.Invoke(this, false);
                        _monitor?.Cancel();
                        _monitor = null;
                        _rechercheTimer?.Start();
                    };
                    _monitor.Start(nomsLecteurs);

                    _journalisation.EnregistrerInformation($"Surveillance NFC active ({monitorScope}) sur {nomsLecteurs.Length} lecteurs.");
                }
                catch (Exception ex)
                {
                    _journalisation.EnregistrerErreur($"Erreur tentative initialisation NFC: {ex.Message}");
                    _isReaderConnected = false;
                    ReaderStatusChanged?.Invoke(this, false);
                    _rechercheTimer?.Start();
                }
            }
        }

        private void SurChangementStatut(object sender, StatusChangeEventArgs e)
        {
            if (e.NewState.HasFlag(SCRState.Present))
            {
                _journalisation.EnregistrerInformation($"Carte insérée dans: {e.ReaderName}");
                if (!string.IsNullOrEmpty(_encodageEnAttenteUid))
                {
                    string target = _encodageEnAttenteUid;
                    _encodageEnAttenteUid = null; // Reset avant encodage pour éviter les boucles
                    EncoderCuidBadge(e.ReaderName, target);
                }
                else
                {
                    LireUidBadge(e.ReaderName, e.Atr);
                }
            }
            else if (e.NewState.HasFlag(SCRState.Empty))
            {
                _journalisation.EnregistrerInformation($"Carte retirÃ©e de: {e.ReaderName}");
                BadgeRetire?.Invoke(this, EventArgs.Empty);
            }
        }

        private string? TryReadWithFreshContext(string nomLecteur, Func<IsoReader, string?> strategy)
        {
            try
            {
                using var ctx = ContextFactory.Instance.Establish(SCardScope.System);
                using var reader = new IsoReader(ctx, nomLecteur, SCardShareMode.Shared, SCardProtocol.Any, false);
                return strategy(reader);
            }
            catch (Exception e)
            {
                _journalisation.EnregistrerInformation($"Strategy failed: {e.Message}");
                return null;
            }
        }

        public void PreparerEncodageCuid(string targetUid)
        {
            _encodageEnAttenteUid = targetUid;
            ModeEncodageActive?.Invoke(this, targetUid);
            _journalisation.EnregistrerInformation($"Lecteur en mode encodage pour {targetUid}. En attente du badge...");
        }

        private void EncoderCuidBadge(string nomLecteur, string newUidHex)
        {
            try
            {
                // Convert string hex to 4 bytes
                byte[] newUid = new byte[4];
                for (int i = 0; i < 4; i++) newUid[i] = Convert.ToByte(newUidHex.Substring(i * 2, 2), 16);

                byte bcc = (byte)(newUid[0] ^ newUid[1] ^ newUid[2] ^ newUid[3]);

                System.Threading.Thread.Sleep(800); // Wait for card to settle

                string result = TryReadWithFreshContext(nomLecteur, r =>
                {
                    try
                    {
                        // 1. Load Key (FF FF FF FF FF FF)
                        r.Transmit(new CommandApdu(IsoCase.Case3Short, r.ActiveProtocol) { CLA = 0xFF, Instruction = (InstructionCode)0x82, P1 = 0x00, P2 = 0x00, Data = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF } });

                        // 2. Auth Block 0 with Key A
                        var auth = r.Transmit(new CommandApdu(IsoCase.Case3Short, r.ActiveProtocol) { CLA = 0xFF, Instruction = (InstructionCode)0x86, P1 = 0x00, P2 = 0x00, Data = new byte[] { 0x01, 0x00, 0x00, 0x60, 0x00 } });
                        if (auth.SW1 != 0x90) return "AUTH_FAIL";

                        // 3. Read Block 0 to get SAK/ATQA and manufacturer data
                        var read = r.Transmit(new CommandApdu(IsoCase.Case2Short, r.ActiveProtocol) { CLA = 0xFF, Instruction = (InstructionCode)0xB0, P1 = 0x00, P2 = 0x00, Le = 16 });
                        if (read.SW1 != 0x90 || !read.HasData) return "READ_FAIL";

                        byte[] block0 = read.GetData();

                        // 4. Modify block 0 (first 4 bytes = UID, 5th = BCC)
                        Array.Copy(newUid, 0, block0, 0, 4);
                        block0[4] = bcc;

                        // 5. Write Block 0
                        var write = r.Transmit(new CommandApdu(IsoCase.Case3Short, r.ActiveProtocol) { CLA = 0xFF, Instruction = (InstructionCode)0xD6, P1 = 0x00, P2 = 0x00, Data = block0 });
                        if (write.SW1 == 0x90) return "SUCCESS";
                        return "WRITE_FAIL";
                    }
                    catch (Exception ex) { return "EXCEPTION_" + ex.Message; }
                }) ?? "NULL_RESULT";

                if (result == "SUCCESS")
                {
                    _journalisation.EnregistrerInformation($"Badge encodé avec succès avec le CUID : {newUidHex}");
                    EncodageTermine?.Invoke(this, true);
                    BadgeDetecte?.Invoke(this, newUidHex); // Broadcast the new UID back as a detection event for UI update
                }
                else
                {
                    _journalisation.EnregistrerErreur($"Échec de l'encodage du CUID : {result}");
                    EncodageTermine?.Invoke(this, false);
                }
            }
            catch (Exception ex)
            {
                _journalisation.EnregistrerErreur($"Exception critique lors de l'encodage : {ex.Message}");
                EncodageTermine?.Invoke(this, false);
            }
        }

        private void LireUidBadge(string nomLecteur, byte[] atrCarte)
        {
            string finalUid = string.Empty;
            string methodUsed = "Aucune";
            int retries = 3;

            while (retries > 0 && string.IsNullOrEmpty(finalUid))
            {
                System.Threading.Thread.Sleep(800);

                var cpsUid = TryReadWithFreshContext(nomLecteur, r =>
                {
                    try
                    {
                        var selectCps = r.Transmit(new CommandApdu(IsoCase.Case3Short, r.ActiveProtocol) { CLA = 0x00, Instruction = InstructionCode.SelectFile, P1 = 0x04, P2 = 0x0C, Data = new byte[] { 0xA0, 0x00, 0x00, 0x00, 0x30, 0x29, 0x05, 0x70, 0x00, 0x30, 0x1F } });
                        if (selectCps.SW1 == 0x90 || selectCps.SW1 == 0x61)
                        {
                            var readId = r.Transmit(new CommandApdu(IsoCase.Case2Short, r.ActiveProtocol) { CLA = 0x00, Instruction = InstructionCode.ReadBinary, P1 = 0x00, P2 = 0x00, Le = 0x08 });
                            if (readId.HasData && readId.SW1 == 0x90)
                                return "CPS-" + BitConverter.ToString(readId.GetData()!).Replace("-", "").ToUpper();
                        }
                        else
                        {
                            _journalisation.EnregistrerInformation($"CPS APDU Select failed with SW1={selectCps.SW1:X2}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _journalisation.EnregistrerInformation($"CPS APDU Exception: {ex.Message}");
                    }
                    return null;
                });

                if (cpsUid != null) { finalUid = cpsUid; methodUsed = "CPS-APDU"; break; }

                // Try Standard NFC UID (Mifare / CUID)
                var standardUid = TryReadWithFreshContext(nomLecteur, r =>
                {
                    try
                    {
                        var getUid = r.Transmit(new CommandApdu(IsoCase.Case2Short, r.ActiveProtocol) { CLA = 0xFF, Instruction = (InstructionCode)0xCA, P1 = 0x00, P2 = 0x00, Le = 0x00 });
                        if (getUid.HasData && getUid.SW1 == 0x90)
                        {
                            return "NFC-" + BitConverter.ToString(getUid.GetData()!).Replace("-", "").ToUpper();
                        }
                        else
                        {
                            _journalisation.EnregistrerInformation($"Standard APDU GetUid failed with SW1={getUid.SW1:X2}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _journalisation.EnregistrerInformation($"Standard APDU Exception: {ex.Message}");
                    }
                    return null;
                });

                if (standardUid != null) { finalUid = standardUid; methodUsed = "Standard-APDU"; break; }

                try
                {
                    var uniqueId = WinScardHelper.GetCardUniqueId(nomLecteur, msg => _journalisation.EnregistrerInformation(msg));
                    if (!string.IsNullOrEmpty(uniqueId))
                    {
                        finalUid = uniqueId; methodUsed = "WinSCard"; break;
                    }
                }
                catch { }

                retries--;
                if (retries > 0) System.Threading.Thread.Sleep(400);
            }

            if (string.IsNullOrEmpty(finalUid) && atrCarte != null && atrCarte.Length > 0)
            {
                string atrHex = BitConverter.ToString(atrCarte).Replace("-", "").ToUpper();
                finalUid = "UNSECURE-ATR-COLLISION-" + atrHex;
                methodUsed = "Monitor-ATR-Fallback";
            }

            if (string.IsNullOrEmpty(finalUid))
            {
                ErreurLecture?.Invoke(this, "Could not identify card. Hardware might be busy.");
                return;
            }

            _journalisation.EnregistrerInformation($"Badge {finalUid} identified via {methodUsed}");
            BadgeDetecte?.Invoke(this, finalUid);
        }

        public void ArreterEcoute()
        {
            _rechercheTimer?.Stop();
            _monitor?.Cancel();
            _monitor = null;
        }

        public void ForcerScan()
        {
            _journalisation.EnregistrerInformation("Scan manuel forcé demandé.");

            // First, ensure we are actually "listening"
            if (!_isReaderConnected)
            {
                TentativeDemarrage();
            }

            try
            {
                SCardScope scope = SCardScope.System;
                try { using var test = ContextFactory.Instance.Establish(SCardScope.System); }
                catch { scope = SCardScope.User; }

                using (var context = ContextFactory.Instance.Establish(scope))
                {
                    var nomsLecteurs = context.GetReaders();
                    if (nomsLecteurs == null || nomsLecteurs.Length == 0)
                    {
                        _journalisation.EnregistrerInformation("Scan manuel: Aucun lecteur trouvé.");
                        return;
                    }

                    _isReaderConnected = true;
                    ReaderStatusChanged?.Invoke(this, true);

                    foreach (var nom in nomsLecteurs)
                    {
                        var readerStates = context.GetReaderStatus(new string[] { nom });
                        if (readerStates != null && readerStates.Length > 0)
                        {
                            var state = readerStates[0];
                            if (state.EventState.HasFlag(SCRState.Present))
                            {
                                _journalisation.EnregistrerInformation($"Scan manuel: Carte présente dans {nom}");
                                LireUidBadge(nom, state.Atr);
                            }
                            else
                            {
                                _journalisation.EnregistrerInformation($"Scan manuel: Lecteur {nom} vide.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _journalisation.EnregistrerErreur($"Erreur lors du scan manuel: {ex.Message}");
            }
        }
    }
}
