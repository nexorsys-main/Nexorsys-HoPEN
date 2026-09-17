using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Pinede.NFC.APP.Services
{
    /// <summary>
    /// Reads a unique per-card identifier using a chain of Windows smart card APIs.
    /// Each strategy is isolated — failure in one doesn't block the next.
    /// </summary>
    internal static class WinScardHelper
    {
        // ── winscard.dll ──────────────────────────────────────────────────────
        [DllImport("winscard.dll", CharSet = CharSet.Auto)]
        private static extern int SCardEstablishContext(uint dwScope, IntPtr r1, IntPtr r2, out IntPtr phCtx);

        [DllImport("winscard.dll", CharSet = CharSet.Auto)]
        private static extern int SCardConnect(IntPtr hCtx, string reader, uint share, uint proto, out IntPtr phCard, out uint pdwProto);

        [DllImport("winscard.dll", CharSet = CharSet.Auto)]
        private static extern int SCardGetAttrib(IntPtr hCard, uint attrId, byte[]? buf, ref uint len);

        [DllImport("winscard.dll", CharSet = CharSet.Auto)]
        private static extern int SCardListCards(IntPtr hCtx, byte[]? atr, IntPtr guids, uint cGuids, char[]? cards, ref uint pcchCards);

        [DllImport("winscard.dll", CharSet = CharSet.Auto)]
        private static extern int SCardGetCardTypeProviderName(IntPtr hCtx, string cardName, uint providerId, StringBuilder provider, ref uint pcchProvider);

        [DllImport("winscard.dll")]
        private static extern int SCardDisconnect(IntPtr hCard, uint disp);

        [DllImport("winscard.dll")]
        private static extern int SCardReleaseContext(IntPtr hCtx);

        // ── crypt32.dll ───────────────────────────────────────────────────────
        [DllImport("crypt32.dll", SetLastError = true)]
        private static extern bool CertGetCertificateContextProperty(IntPtr pCertCtx, uint propId, IntPtr pvData, ref uint pcbData);

        // ── advapi32.dll ──────────────────────────────────────────────────────
        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool CryptAcquireContext(out IntPtr hProv, string? container, string? provider, uint provType, uint flags);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool CryptGetProvParam(IntPtr hProv, uint param, byte[]? buf, ref uint len, uint flags);

        [DllImport("advapi32.dll")]
        private static extern bool CryptReleaseContext(IntPtr hProv, uint flags);

        // ── ncrypt.dll ────────────────────────────────────────────────────────
        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int NCryptOpenStorageProvider(out IntPtr hProv, string name, uint flags);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int NCryptEnumKeys(IntPtr hProv, string? scope, out IntPtr ppKeyName, ref IntPtr ppEnumState, uint flags);

        [DllImport("ncrypt.dll")]
        private static extern int NCryptFreeBuffer(IntPtr p);

        [DllImport("ncrypt.dll")]
        private static extern int NCryptFreeObject(IntPtr p);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NCryptKeyName { public string pszName; public string pszAlgid; public uint dwLegacyKeySpec; public uint dwFlags; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CRYPT_KEY_PROV_INFO
        {
            public string pwszContainerName;
            public string pwszProvName;
            public uint dwProvType;
            public uint dwFlags;
            public uint cProvParam;
            public IntPtr rgProvParam;
            public uint dwKeySpec;
        }

        private const uint SCARD_SCOPE_SYSTEM = 2;
        private const uint SCARD_SHARE_SHARED = 2;
        private const uint SCARD_PROTOCOL_ANY = 3;
        private const uint SCARD_LEAVE_CARD = 0;
        private const uint SCARD_ATTR_ATR_STRING = 0x00090303;
        private const uint SCARD_PROVIDER_KSP = 3;
        private const uint SCARD_PROVIDER_CSP = 2;
        private const uint CERT_KEY_PROV_INFO_PROP_ID = 2;
        private const uint PP_UNIQUE_CONTAINER = 36;
        private const uint PP_CONTAINER = 6;
        private const uint PROV_RSA_FULL = 1;
        private const uint CRYPT_SILENT = 0x40;
        private const uint NCRYPT_SILENT_FLAG = 0x40;

        public static string? GetCardUniqueId(string readerName, Action<string>? log = null)
        {
            // Step 1: get the card's ATR from the reader
            byte[]? atr = GetAtr(readerName, log);

            // Step 2: find which Windows card-type this ATR maps to, get its CSP/KSP
            string? providerName = null;
            if (atr != null)
                providerName = GetProviderForAtr(atr, log);

            // Step 3: try NCrypt with the discovered (or fallback) provider, scoped to this reader
            var result = TryNcryptScopedToReader(readerName, providerName, log);
            if (result != null) return result;

            // Step 4: try CryptoAPI with the discovered CSP
            result = TryCryptoApiContainer(readerName, providerName, log);
            if (result != null) return result;

            // Step 5: cert store — filter by reader via CRYPT_KEY_PROV_INFO
            result = TryCertStoreFilteredByReader(readerName, log);
            if (result != null) return result;

            log?.Invoke("All WinSCard strategies exhausted.");
            return null;
        }

        private static byte[]? GetAtr(string readerName, Action<string>? log)
        {
            try
            {
                if (SCardEstablishContext(SCARD_SCOPE_SYSTEM, IntPtr.Zero, IntPtr.Zero, out var hCtx) != 0) return null;
                try
                {
                    if (SCardConnect(hCtx, readerName, SCARD_SHARE_SHARED, SCARD_PROTOCOL_ANY, out var hCard, out _) != 0) return null;
                    try
                    {
                        uint len = 0;
                        SCardGetAttrib(hCard, SCARD_ATTR_ATR_STRING, null, ref len);
                        if (len > 0)
                        {
                            var buf = new byte[len];
                            if (SCardGetAttrib(hCard, SCARD_ATTR_ATR_STRING, buf, ref len) == 0)
                            { log?.Invoke($"ATR: {BitConverter.ToString(buf, 0, (int)len)}"); return buf; }
                        }
                    }
                    finally { SCardDisconnect(hCard, SCARD_LEAVE_CARD); }
                }
                finally { SCardReleaseContext(hCtx); }
            }
            catch (Exception e) { log?.Invoke($"GetAtr error: {e.Message}"); }
            return null;
        }

        private static string? GetProviderForAtr(byte[] atr, Action<string>? log)
        {
            try
            {
                if (SCardEstablishContext(SCARD_SCOPE_SYSTEM, IntPtr.Zero, IntPtr.Zero, out var hCtx) != 0) return null;
                try
                {
                    // Find card name(s) matching this ATR
                    uint cch = 0;
                    SCardListCards(hCtx, atr, IntPtr.Zero, 0, null, ref cch);
                    if (cch < 2) return null;

                    var cards = new char[cch];
                    if (SCardListCards(hCtx, atr, IntPtr.Zero, 0, cards, ref cch) != 0) return null;

                    // SCardListCards returns a multi-string (null-separated, double-null terminated)
                    var all = new string(cards).TrimEnd('\0');
                    var cardName = all.Split('\0')[0];
                    log?.Invoke($"Card type: '{cardName}'");
                    if (string.IsNullOrEmpty(cardName)) return null;

                    // Try KSP first (CNG), then CSP (legacy)
                    var sb = new StringBuilder(256);
                    uint sbLen = 256;

                    if (SCardGetCardTypeProviderName(hCtx, cardName, SCARD_PROVIDER_KSP, sb, ref sbLen) == 0)
                    { var ksp = sb.ToString(); log?.Invoke($"KSP: {ksp}"); return ksp; }

                    sbLen = 256;
                    if (SCardGetCardTypeProviderName(hCtx, cardName, SCARD_PROVIDER_CSP, sb, ref sbLen) == 0)
                    { var csp = sb.ToString(); log?.Invoke($"CSP: {csp}"); return csp; }
                }
                finally { SCardReleaseContext(hCtx); }
            }
            catch (Exception e) { log?.Invoke($"GetProvider error: {e.Message}"); }
            return null;
        }

        private static string? TryNcryptScopedToReader(string readerName, string? providerName, Action<string>? log)
        {
            var providers = providerName != null
                ? new[] { providerName, "Microsoft Smart Card Key Storage Provider" }
                : new[] { "Microsoft Smart Card Key Storage Provider" };

            foreach (var prov in providers)
            {
                try
                {
                    if (NCryptOpenStorageProvider(out var hProv, prov, 0) != 0) continue;
                    try
                    {
                        var scope = $"\\\\.\\{readerName}\\";
                        IntPtr enumState = IntPtr.Zero;
                        if (NCryptEnumKeys(hProv, scope, out var pKey, ref enumState, NCRYPT_SILENT_FLAG) == 0 && pKey != IntPtr.Zero)
                        {
                            var key = Marshal.PtrToStructure<NCryptKeyName>(pKey);
                            NCryptFreeBuffer(pKey);
                            if (enumState != IntPtr.Zero) NCryptFreeBuffer(enumState);
                            if (!string.IsNullOrEmpty(key.pszName))
                            {
                                log?.Invoke($"NCrypt key [{prov}]: {key.pszName}");
                                return "KEY-" + key.pszName.Replace(" ", "-").ToUpper();
                            }
                        }
                        else { log?.Invoke($"NCrypt [{prov}] enum failed or empty"); }
                    }
                    finally { NCryptFreeObject(hProv); }
                }
                catch (Exception e) { log?.Invoke($"NCrypt [{prov}] error: {e.Message}"); }
            }
            return null;
        }

        private static string? TryCryptoApiContainer(string readerName, string? providerName, Action<string>? log)
        {
            var providers = providerName != null
                ? new[] { providerName, "Microsoft Base Smart Card Crypto Provider" }
                : new[] { "Microsoft Base Smart Card Crypto Provider" };

            string readerPath = $"\\\\.\\{readerName}\\";

            foreach (var prov in providers)
            {
                try
                {
                    if (!CryptAcquireContext(out var hProv, readerPath, prov, PROV_RSA_FULL, CRYPT_SILENT)) continue;
                    try
                    {
                        uint len = 0;
                        CryptGetProvParam(hProv, PP_UNIQUE_CONTAINER, null, ref len, 0);
                        if (len > 0)
                        {
                            var buf = new byte[len];
                            if (CryptGetProvParam(hProv, PP_UNIQUE_CONTAINER, buf, ref len, 0))
                            {
                                var name = Encoding.ASCII.GetString(buf, 0, (int)len).Trim('\0').Trim();
                                if (!string.IsNullOrEmpty(name) && name.Length > 3)
                                { log?.Invoke($"CSP container [{prov}]: {name}"); return "CSP-" + name.Replace(" ", "-").ToUpper(); }
                            }
                        }
                    }
                    finally { CryptReleaseContext(hProv, 0); }
                }
                catch (Exception e) { log?.Invoke($"CSP [{prov}] error: {e.Message}"); }
            }
            return null;
        }

        /// <summary>
        /// Reads MY cert store but filters by CRYPT_KEY_PROV_INFO to find certs
        /// whose key container is associated with the specific reader slot.
        /// Also uses the container name (not cert serial) as the unique ID.
        /// </summary>
        private static string? TryCertStoreFilteredByReader(string readerName, Action<string>? log)
        {
            try
            {
                using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                foreach (var cert in store.Certificates)
                {
                    try
                    {
                        // Get CRYPT_KEY_PROV_INFO to find which reader/container this cert is on
                        var ctx = cert.Handle;
                        uint propLen = 0;
                        CertGetCertificateContextProperty(ctx, CERT_KEY_PROV_INFO_PROP_ID, IntPtr.Zero, ref propLen);

                        if (propLen > 0)
                        {
                            var ptr = Marshal.AllocHGlobal((int)propLen);
                            try
                            {
                                if (CertGetCertificateContextProperty(ctx, CERT_KEY_PROV_INFO_PROP_ID, ptr, ref propLen))
                                {
                                    var info = Marshal.PtrToStructure<CRYPT_KEY_PROV_INFO>(ptr);
                                    log?.Invoke($"Cert: serial={cert.SerialNumber} container={info.pwszContainerName} prov={info.pwszProvName}");

                                    // The container name for smart card certs often includes the reader name
                                    // or is a GUID that is per-card. Use it as the unique ID.
                                    if (!string.IsNullOrEmpty(info.pwszContainerName) && info.pwszContainerName.Length > 3)
                                    {
                                        // If it references our reader or is a GUID-like container, use it
                                        var container = info.pwszContainerName.Trim();
                                        if (container.StartsWith("{") || cert.HasPrivateKey)
                                            return "CONT-" + container.Replace("{", "").Replace("}", "").Replace(" ", "-").ToUpper();
                                    }
                                }
                            }
                            finally { Marshal.FreeHGlobal(ptr); }
                        }
                    }
                    catch { /* skip this cert */ }
                }
            }
            catch (Exception e) { log?.Invoke($"CertStore error: {e.Message}"); }
            return null;
        }
    }
}
