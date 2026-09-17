using System;
using System.Security.Cryptography;
using System.Text;

namespace Pinede.NFC.APP.Services
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
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(texteEnClair), null, DataProtectionScope.CurrentUser));
        }

        public string Dechiffrer(string texteChiffre)
        {
            if (string.IsNullOrEmpty(texteChiffre)) return string.Empty;
            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(texteChiffre), null, DataProtectionScope.CurrentUser));
            }
            catch (CryptographicException) { return string.Empty; }
            catch (FormatException) { return string.Empty; }
        }
    }
}
