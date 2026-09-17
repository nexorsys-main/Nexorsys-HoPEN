using System;
using System.Linq;
using System.Threading;
using PCSC;
using PCSC.Exceptions;
using PCSC.Iso7816;
using PCSC.Monitoring;

namespace Nexorsys.NFC.APP.Services
{
    /// <summary>
    /// Implémentation du service NFC utilisant la librairie PCSC-Sharp pour Windows.
    /// Gère la détection de l'insertion de cartes (badges) et la lecture de l'UID.
    /// </summary>
    public class NfcService : INfcService, IDisposable
    {
        private readonly ISCardContext? _context;
        private readonly ISCardMonitor? _monitor;
        private readonly object _startGate = new();
        private Timer? _readerRetryTimer;
        private bool _disposed;

        public bool LecteurConnecte { get; private set; }

        public event EventHandler<string>? BadgeDetecte;
        public event EventHandler<string>? ErreurLecture;

        public NfcService()
        {
            try
            {
                var contextFactory = ContextFactory.Instance;
                _context = contextFactory.Establish(SCardScope.System);
                
                var monitorFactory = MonitorFactory.Instance;
                _monitor = monitorFactory.Create(SCardScope.System);
                
                _monitor.CardInserted += Monitor_CardInserted;
                _monitor.MonitorException += Monitor_MonitorException;
            }
            catch
            {
                // Permet le lancement de l'app si le service PC/SC est désactivé
                _context = null;
                _monitor = null;
            }
        }

        public void DemarrerEcoute()
        {
            TenterDemarrage(signalerErreur: true);
        }

        private void TenterDemarrage(bool signalerErreur)
        {
            try
            {
                if (_context == null || _monitor == null)
                {
                    LecteurConnecte = false;
                    if (signalerErreur)
                    {
                        ErreurLecture?.Invoke(this, "Le service PC/SC Windows est arrêté ou aucun lecteur NFC n'est accessible.");
                    }
                    return;
                }

                var readers = _context.GetReaders();
                if (readers == null || readers.Length == 0)
                {
                    LecteurConnecte = false;
                    if (signalerErreur)
                    {
                        ErreurLecture?.Invoke(this, "Aucun lecteur NFC matériel n'a été détecté. Recherche en cours...");
                    }
                    lock (_startGate)
                    {
                        _readerRetryTimer ??= new Timer(_ => TenterDemarrage(signalerErreur: false), null, 3000, 3000);
                    }
                    return;
                }

                lock (_startGate)
                {
                    _readerRetryTimer?.Dispose();
                    _readerRetryTimer = null;
                    if (!_monitor.Monitoring)
                    {
                        _monitor.Start(readers);
                    }
                    LecteurConnecte = true;
                }
            }
            catch (Exception ex)
            {
                LecteurConnecte = false;
                if (signalerErreur)
                {
                    ErreurLecture?.Invoke(this, $"Erreur lors du démarrage du lecteur NFC: {ex.Message}");
                }
            }
        }

        public void ArreterEcoute()
        {
            try
            {
                if (_monitor != null && _monitor.Monitoring)
                {
                    _monitor.Cancel();
                }
            }
            catch (Exception ex)
            {
                ErreurLecture?.Invoke(this, $"Erreur lors de l'arrêt du lecteur: {ex.Message}");
            }
        }

        public void SimulerScanBadge(string uidBadge)
        {
            throw new NotSupportedException("Simulated badge authentication is disabled in the commercial Kiosk.");
        }

        private void Monitor_CardInserted(object sender, CardStatusEventArgs e)
        {
            LireUidBadge(e.ReaderName);
        }

        private void Monitor_MonitorException(object sender, PCSCException ex)
        {
            LecteurConnecte = false;
            ErreurLecture?.Invoke(this, $"Exception du moniteur NFC: {ex.Message}");
            TenterDemarrage(signalerErreur: false);
        }

        private void LireUidBadge(string nomLecteur)
        {
            try
            {
                using var context = ContextFactory.Instance.Establish(SCardScope.System);
                using var isoReader = new IsoReader(
                    context, 
                    nomLecteur, 
                    SCardShareMode.Shared, 
                    SCardProtocol.Any, 
                    false);

                // APDU standard pour lire l'UID des badges Mifare/NFC: FF CA 00 00 00
                var readUidCommand = new CommandApdu(IsoCase.Case2Short, isoReader.ActiveProtocol)
                {
                    CLA = 0xFF,
                    Instruction = InstructionCode.GetData,
                    P1 = 0x00,
                    P2 = 0x00,
                    Le = 0 // Auto/Toute la réponse attendue
                };

                var response = isoReader.Transmit(readUidCommand);

                if (response.HasData && response.SW1 == 0x90 && response.SW2 == 0x00)
                {
                    string uidHex = BitConverter.ToString(response.GetData()!).Replace("-", "").ToUpper();
                    BadgeDetecte?.Invoke(this, uidHex);
                }
                else
                {
                    ErreurLecture?.Invoke(this, "Impossible de lire l'UID de la carte (badge non compatible ou mal placé).");
                }
            }
            catch (Exception ex)
            {
                ErreurLecture?.Invoke(this, $"Erreur lors de la lecture du badge: {ex.Message}");
            }
        }

        public bool EcrireUid(string nouvelUidHex)
        {
            if (string.IsNullOrWhiteSpace(nouvelUidHex) || nouvelUidHex.Length != 8) return false;
            
            try
            {
                var readers = _context?.GetReaders();
                if (readers == null || readers.Length == 0) return false;
                
                string nomLecteur = readers.First();

                using var context = ContextFactory.Instance.Establish(SCardScope.System);
                using var isoReader = new IsoReader(
                    context, 
                    nomLecteur, 
                    SCardShareMode.Shared, 
                    SCardProtocol.Any, 
                    false);

                // 1. Authenticate to Block 0 using Key A (Default: FF FF FF FF FF FF)
                var loadKeyCmd = new CommandApdu(IsoCase.Case3Short, isoReader.ActiveProtocol)
                {
                    CLA = 0xFF, Instruction = (InstructionCode)0x82, P1 = 0x00, P2 = 0x00,
                    Data = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }
                };
                isoReader.Transmit(loadKeyCmd);

                var authCmd = new CommandApdu(IsoCase.Case3Short, isoReader.ActiveProtocol)
                {
                    CLA = 0xFF, Instruction = (InstructionCode)0x86, P1 = 0x00, P2 = 0x00,
                    Data = new byte[] { 0x01, 0x00, 0x00, 0x60, 0x00 } // Auth Block 0, Key Type A (0x60), Key Loc 0
                };
                var authResp = isoReader.Transmit(authCmd);
                if (authResp.SW1 != 0x90) return false;

                // 2. Read Block 0 to keep Manufacturer Data
                var readCmd = new CommandApdu(IsoCase.Case2Short, isoReader.ActiveProtocol)
                {
                    CLA = 0xFF, Instruction = (InstructionCode)0xB0, P1 = 0x00, P2 = 0x00, Le = 0x10
                };
                var readResp = isoReader.Transmit(readCmd);
                if (readResp.SW1 != 0x90 || !readResp.HasData || readResp.GetData().Length < 16) return false;

                byte[] block0 = readResp.GetData()!;

                // 3. Prepare new Block 0
                byte[] newUidBytes = Convert.FromHexString(nouvelUidHex);
                newUidBytes.CopyTo(block0, 0);
                
                // Calculate BCC (Block Check Character) - XOR of the 4 UID bytes
                block0[4] = (byte)(block0[0] ^ block0[1] ^ block0[2] ^ block0[3]);

                // 4. Write Block 0
                var writeCmd = new CommandApdu(IsoCase.Case3Short, isoReader.ActiveProtocol)
                {
                    CLA = 0xFF, Instruction = (InstructionCode)0xD6, P1 = 0x00, P2 = 0x00,
                    Data = block0
                };
                var writeResp = isoReader.Transmit(writeCmd);

                return (writeResp.SW1 == 0x90);
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                ArreterEcoute();
                LecteurConnecte = false;
                _readerRetryTimer?.Dispose();
                _readerRetryTimer = null;
                if (_monitor != null)
                {
                    _monitor.CardInserted -= Monitor_CardInserted;
                    _monitor.MonitorException -= Monitor_MonitorException;
                    _monitor.Dispose();
                }
                _context?.Dispose();
                _disposed = true;
            }
        }
    }
}
