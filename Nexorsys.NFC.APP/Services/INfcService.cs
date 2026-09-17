using System;

namespace Pinede.NFC.APP.Services
{
    /// <summary>
    /// Interface du service de lecture de badge NFC.
    /// </summary>
    public interface INfcService
    {
        /// <summary>
        /// Événement déclenché lorsqu'un badge est détecté. 
        /// Le payload de l'événement est l'UID (Identifiant unique) de la carte en string héxadécimal.
        /// </summary>
        event EventHandler<string> BadgeDetecte;

        /// <summary>
        /// Événement déclenché lorsqu'un badge est retiré du lecteur.
        /// </summary>
        event EventHandler BadgeRetire;

        /// <summary>
        /// Événement déclenché en cas d'erreur de lecteur ou de lecture NFC.
        /// </summary>
        event EventHandler<string> ErreurLecture;

        /// <summary>
        /// Événement déclenché lorsque le statut de connexion du lecteur matériel change.
        /// </summary>
        event EventHandler<bool> ReaderStatusChanged;

        /// <summary>
        /// Indique si un lecteur matériel est actuellement connecté.
        /// </summary>
        bool IsReaderConnected { get; }

        /// <summary>
        /// Initialise et démarre l'écoute matérielle (ou la simulation).
        /// </summary>
        void DemarrerEcoute();

        /// <summary>
        /// Arrête l'écoute du lecteur NFC.
        /// </summary>
        void ArreterEcoute();

        /// <summary>
        /// Indique que le lecteur est entré en mode encodage.
        /// </summary>
        event EventHandler<string> ModeEncodageActive;

        /// <summary>
        /// Événement déclenché lorsque l'encodage est terminé (succès ou échec).
        /// </summary>
        event EventHandler<bool> EncodageTermine;

        /// <summary>
        /// Force une détection immédiate sur tous les lecteurs connectés.
        /// </summary>
        void ForcerScan();

        /// <summary>
        /// Prépare le service à encoder la prochaine carte présentée.
        /// </summary>
        void PreparerEncodageCuid(string targetUid);
    }
}
