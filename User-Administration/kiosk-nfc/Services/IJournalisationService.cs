using Nexorsys.NFC.APP.Modeles;

namespace Nexorsys.NFC.APP.Services
{
    /// <summary>
    /// Service responsable de la trace de l'activité, des succès et échecs de connexion, 
    /// enregistré localement au format JSON pour l'administrateur système.
    /// </summary>
    public interface IJournalisationService
    {
        void EnregistrerInformation(string message, string? uidBadge = null, string? utilisateur = null);
        void EnregistrerErreur(string message, string? uidBadge = null, string? utilisateur = null);
        void EnregistrerEvenement(EvenementJournal evenement);
        System.Collections.Generic.List<EvenementJournal> ObtenirHistorique();
    }
}
