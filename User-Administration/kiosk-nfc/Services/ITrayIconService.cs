using System;

namespace Nexorsys.NFC.APP.Services
{
    public interface ITrayIconService
    {
        void Initialiser();
        void AfficherNotification(string titre, string message);
        void Nettoyer();
        event EventHandler RestaurerDemande;
    }
}
