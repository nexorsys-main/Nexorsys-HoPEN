using System.Collections.Generic;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pinede.NFC.APP.Modeles;
using Pinede.NFC.APP.Services;
using System.Threading.Tasks;

namespace Pinede.NFC.APP.VueModeles
{
    public partial class EcranChangementPinVueModele : ObservableObject
    {
        private readonly ApiClientService _apiClient;
        private readonly INavigationService _navigationService;
        private readonly IJournalisationService _journalisation;
        private readonly System.IServiceProvider _serviceProvider;

        private string _userId = string.Empty;
        private List<ManagedApp> _apps = new();

        [ObservableProperty]
        private string _nomComplet = string.Empty;

        [ObservableProperty]
        private string _messageErreur = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        public EcranChangementPinVueModele(
            ApiClientService apiClient,
            INavigationService navigationService,
            IJournalisationService journalisation,
            System.IServiceProvider serviceProvider)
        {
            _apiClient = apiClient;
            _navigationService = navigationService;
            _journalisation = journalisation;
            _serviceProvider = serviceProvider;
        }

        public void Initialiser(string userId, string nomComplet, List<ManagedApp> apps)
        {
            _userId = userId;
            NomComplet = nomComplet;
            _apps = apps;
            MessageErreur = string.Empty;
            IsBusy = false;
        }

        [RelayCommand]
        private async Task EnregistrerNouveauPin(object parameter)
        {
            if (parameter is not SecureString securePin || securePin.Length == 0)
            {
                MessageErreur = "Veuillez saisir un nouveau code PIN.";
                return;
            }

            if (securePin.Length < 4)
            {
                MessageErreur = "Le code PIN doit comporter au moins 4 chiffres.";
                return;
            }

            IsBusy = true;
            MessageErreur = string.Empty;

            string pinClair = ConvertToUnsecureString(securePin);

            var success = await _apiClient.ChangerPinAsync(_userId, pinClair);

            if (success)
            {
                _journalisation.EnregistrerInformation("Changement de PIN réussi (premier login)", _userId);

                var accueilVm = _serviceProvider.GetRequiredService<EcranAccueilVueModele>();
                accueilVm.Initialiser(NomComplet, _apps);
                _navigationService.NaviguerVers(accueilVm);
            }
            else
            {
                MessageErreur = "Erreur lors du changement de PIN. Veuillez réessayer.";
            }

            IsBusy = false;
        }

        private string ConvertToUnsecureString(SecureString secureString)
        {
            if (secureString == null) return string.Empty;
            System.IntPtr unmanagedString = System.IntPtr.Zero;
            try
            {
                unmanagedString = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return System.Runtime.InteropServices.Marshal.PtrToStringUni(unmanagedString) ?? "";
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }

        [RelayCommand]
        private void Annuler()
        {
            var attenteVm = _serviceProvider.GetRequiredService<EcranAttenteVueModele>();
            _navigationService.NaviguerVers(attenteVm);
        }
    }
}
