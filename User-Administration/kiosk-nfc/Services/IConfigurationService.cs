using Nexorsys.NFC.APP.Modeles;

namespace Nexorsys.NFC.APP.Services
{
    public interface IConfigurationService
    {
        ParametresApplication ChargerParametres();
        void SauvegarderParametres(ParametresApplication parametres);
    }
}
