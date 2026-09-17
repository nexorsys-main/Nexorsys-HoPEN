using Nexorsys.NFC.APP.Modeles;
using System.Threading.Tasks;

namespace Nexorsys.NFC.APP.Services
{
    /// <summary>
    /// Service responsable de l'ouverture des applications métiers autorisées après l'authentification.
    /// </summary>
    public interface ILanceurApplicationService
    {
        Task<bool> LancerApplicationAsync(ManagedApp app);
    }
}
