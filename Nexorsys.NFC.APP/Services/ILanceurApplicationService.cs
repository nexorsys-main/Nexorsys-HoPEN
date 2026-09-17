using Pinede.NFC.APP.Modeles;

namespace Pinede.NFC.APP.Services
{
    /// <summary>
    /// Service responsable de l'ouverture des applications métiers autorisées après l'authentification.
    /// </summary>
    public interface ILanceurApplicationService
    {
        void LancerApplication(ManagedApp app);
    }
}
