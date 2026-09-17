using System;
using System.IO;

namespace Nexorsys.NFC.APP.Services
{
    public interface IServiceValidation
    {
        bool ValiderUrl(string url);
        bool ValiderFichier(string chemin);
        bool ValiderDossier(string chemin);
        bool ValiderEntierPositif(string valeur, out int resultat);
    }

    public class ServiceValidation : IServiceValidation
    {
        public bool ValiderUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            // Si l'URL n'a pas de protocole, on teste avec https:// pour la validation
            string testUrl = url;
            if (!url.Contains("://")) testUrl = "https://" + url;

            return Uri.TryCreate(testUrl, UriKind.Absolute, out var uriResult) 
                   && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        public bool ValiderFichier(string chemin)
        {
            if (string.IsNullOrWhiteSpace(chemin)) return true;
            
            try 
            {
                // On accepte les chemins qui ont un format valide
                var info = new FileInfo(chemin);
                return true; 
            } 
            catch { return false; }
        }

        public bool ValiderDossier(string chemin)
        {
            if (string.IsNullOrWhiteSpace(chemin)) return false;
            try {
                return Directory.Exists(chemin) || (!Path.HasExtension(chemin) && Directory.GetParent(chemin)?.Exists == true);
            } catch { return false; }
        }

        public bool ValiderEntierPositif(string valeur, out int resultat)
        {
            if (int.TryParse(valeur, out resultat))
            {
                return resultat > 0;
            }
            resultat = 0;
            return false;
        }
    }
}
