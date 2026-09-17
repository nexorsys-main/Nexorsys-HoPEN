using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexorsys.NFC.APP.VueModeles
{
    /// <summary>
    /// Vue-modèle principal qui gère la navigation entre les différents écrans.
    /// </summary>
    public partial class FenetrePrincipaleVueModele : ObservableObject
    {
        [ObservableProperty]
        private object? _vueCourante;

        /// <summary>
        /// Gère la libération des ressources de l'ancien écran si nécessaire.
        /// </summary>
        partial void OnVueCouranteChanged(object? oldValue, object? newValue)
        {
            if (oldValue is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public FenetrePrincipaleVueModele()
        {
            // La vue par défaut sera définie ultérieurement par la navigation (ex: EcranAttenteVueModele)
        }
    }
}
