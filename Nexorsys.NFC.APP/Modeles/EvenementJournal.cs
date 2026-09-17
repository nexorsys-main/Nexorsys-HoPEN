using System;

namespace Pinede.NFC.APP.Modeles
{
    public class EvenementJournal
    {
        public DateTime Horodatage { get; set; } = DateTime.Now;
        public string Niveau { get; set; } = "INFO";
        public string? UidBadge { get; set; }
        public string? Utilisateur { get; set; }
        public string? Message { get; set; }
    }
}
