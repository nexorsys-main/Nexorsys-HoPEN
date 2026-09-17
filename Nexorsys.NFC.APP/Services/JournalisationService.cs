using System;
using System.IO;
using System.Text.Json;
using Pinede.NFC.APP.Modeles;

namespace Pinede.NFC.APP.Services
{
    public class JournalisationService : IJournalisationService
    {
        private readonly string _dossierJournaux;
        private readonly object _verrouEcriture = new object();
        public event Action<EvenementJournal> EvenementEnregistre;

        public JournalisationService()
        {
            _dossierJournaux = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Journaux");
            if (!Directory.Exists(_dossierJournaux))
            {
                Directory.CreateDirectory(_dossierJournaux);
            }
        }

        public void EnregistrerInformation(string message, string? uidBadge = null, string? utilisateur = null)
        {
            var evenement = new EvenementJournal
            {
                Niveau = "INFO",
                Message = message,
                UidBadge = uidBadge,
                Utilisateur = utilisateur
            };
            EnregistrerEvenement(evenement);
        }

        public void EnregistrerErreur(string message, string? uidBadge = null, string? utilisateur = null)
        {
            var evenement = new EvenementJournal
            {
                Niveau = "ERREUR",
                Message = message,
                UidBadge = uidBadge,
                Utilisateur = utilisateur
            };
            EnregistrerEvenement(evenement);
        }

        public void EnregistrerEvenement(EvenementJournal evenement)
        {
            lock (_verrouEcriture)
            {
                try
                {
                    string nomFichier = $"evenements_{DateTime.Now:yyyyMMdd}.json";
                    string cheminFichier = Path.Combine(_dossierJournaux, nomFichier);

                    string ligneJson = JsonSerializer.Serialize(evenement, new JsonSerializerOptions
                    {
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = false
                    });

                    // On ajoute une ligne de l'objet JSON sérialisé au fichier (concept dit JSONL)
                    File.AppendAllText(cheminFichier, ligneJson + Environment.NewLine);

                    // PGSSI-S: Traçabilité en temps réel déportée via événement (sera capté par ApiClientService)
                    EvenementEnregistre?.Invoke(evenement);
                }
                catch
                {
                    // En cas d'impossibilité d'écrire le log, l'application métier ne doit pas planter.
                }
            }
        }

        public System.Collections.Generic.List<EvenementJournal> ObtenirHistorique()
        {
            var historique = new System.Collections.Generic.List<EvenementJournal>();
            try
            {
                if (Directory.Exists(_dossierJournaux))
                {
                    // On liste tous les fichiers de log (du plus récent au plus ancien par nom)
                    var fichiers = Directory.GetFiles(_dossierJournaux, "*.json");
                    foreach (var fichier in System.Linq.Enumerable.OrderDescending(fichiers))
                    {
                        var lignes = File.ReadAllLines(fichier);
                        foreach (var ligne in lignes)
                        {
                            if (!string.IsNullOrWhiteSpace(ligne))
                            {
                                var evt = JsonSerializer.Deserialize<EvenementJournal>(ligne);
                                if (evt != null) historique.Add(evt);
                            }
                        }
                    }
                }
            }
            catch { }

            // On trie le tout du plus récent au plus ancien
            historique.Sort((a, b) => b.Horodatage.CompareTo(a.Horodatage));
            return historique;
        }
    }
}
