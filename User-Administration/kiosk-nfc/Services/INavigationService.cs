namespace Nexorsys.NFC.APP.Services
{
    public interface INavigationService
    {
        void NaviguerVers<TVueModele>() where TVueModele : CommunityToolkit.Mvvm.ComponentModel.ObservableObject;
        void NaviguerVers<TVueModele>(TVueModele vueModele) where TVueModele : CommunityToolkit.Mvvm.ComponentModel.ObservableObject;
    }
}
