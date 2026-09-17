using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Nexorsys.NFC.APP.VueModeles;

namespace Nexorsys.NFC.APP.Services
{
    public class NavigationService : INavigationService
    {
        private readonly FenetrePrincipaleVueModele _vueModelePrincipal;
        private readonly IServiceProvider _serviceProvider;

        public NavigationService(FenetrePrincipaleVueModele vueModelePrincipal, IServiceProvider serviceProvider)
        {
            _vueModelePrincipal = vueModelePrincipal;
            _serviceProvider = serviceProvider;
        }

        public void NaviguerVers<TVueModele>() where TVueModele : ObservableObject
        {
            var vueModele = _serviceProvider.GetRequiredService<TVueModele>();
            _vueModelePrincipal.VueCourante = vueModele;
        }

        public void NaviguerVers<TVueModele>(TVueModele vueModele) where TVueModele : ObservableObject
        {
            _vueModelePrincipal.VueCourante = vueModele;
        }
    }
}
