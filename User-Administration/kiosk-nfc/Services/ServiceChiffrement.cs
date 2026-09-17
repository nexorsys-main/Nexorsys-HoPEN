using System;
using System.Security.Cryptography;
using System.Text;

namespace Nexorsys.NFC.APP.Services
{
    public interface IServiceChiffrement
    {
        string Chiffrer(string texteEnClair);
        string Dechiffrer(string texteChiffre);
    }

    public sealed class ServiceChiffrement : IServiceChiffrement
    {
        public string Chiffrer(string texteEnClair)
        {
            if (string.IsNullOrEmpty(texteEnClair)) return string.Empty;
            var clearBytes = Encoding.UTF8.GetBytes(texteEnClair);
            return Convert.ToBase64String(ProtectedData.Protect(clearBytes, null, DataProtectionScope.CurrentUser));
        }

        public string Dechiffrer(string texteChiffre)
        {
            if (string.IsNullOrEmpty(texteChiffre)) return string.Empty;
            try
            {
                var protectedBytes = Convert.FromBase64String(texteChiffre);
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser));
            }
            catch (CryptographicException)
            {
                return string.Empty;
            }
            catch (FormatException)
            {
                return string.Empty;
            }
        }
    }
}
