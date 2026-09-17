using Pinede.NFC.APP.Modeles;

namespace Pinede.NFC.APP.Services
{
    public interface IConfigurationService
    {
        ParametresApplication ChargerParametres();
        void SauvegarderParametres(ParametresApplication parametres);
        string ObtenirUrlApi();
    }
}
