using System;

namespace Nexorsys.NFC.APP.Services
{
    /// <summary>
    /// Interface du service de lecture de badge NFC.
    /// </summary>
    public interface INfcService
    {
        bool LecteurConnecte { get; }

        /// <summary>
        /// Événement déclenché lorsqu'un badge est détecté. 
        /// Le payload de l'événement est l'UID (Identifiant unique) de la carte en string héxadécimal.
        /// </summary>
        event EventHandler<string> BadgeDetecte;

        /// <summary>
        /// Événement déclenché en cas d'erreur de lecteur ou de lecture NFC.
        /// </summary>
        event EventHandler<string> ErreurLecture;

        /// <summary>
        /// Initialise et démarre l'écoute matérielle (ou la simulation).
        /// </summary>
        void DemarrerEcoute();

        /// <summary>
        /// Arrête l'écoute du lecteur NFC.
        /// </summary>
        void ArreterEcoute();

        /// <summary>
        /// Permet de simuler un scan de badge pour le développement.
        /// </summary>
        /// <param name="uidBadge">L'UID simulé du badge</param>
        void SimulerScanBadge(string uidBadge);

        /// <summary>
        /// Écrit un nouvel UID sur un badge physique (MIFARE Classic Gen2).
        /// </summary>
        bool EcrireUid(string nouvelUidHex);
    }
}
