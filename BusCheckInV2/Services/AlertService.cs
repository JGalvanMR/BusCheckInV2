using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    public class AlertService : IAlertService
    {
        public async Task ShowAlertAsync(string title, string message, string button = "OK")
        {
            if (Application.Current?.MainPage != null)
                await Application.Current.MainPage.DisplayAlert(title, message, button);
        }

        public async Task<bool> ShowConfirmationAsync(string title, string message, string accept = "Sí", string cancel = "No")
        {
            if (Application.Current?.MainPage != null)
                return await Application.Current.MainPage.DisplayAlert(title, message, accept, cancel);
            return false;
        }

        public async Task<string> ShowPromptAsync(string title, string message, string accept = "OK", string cancel = "Cancelar", string placeholder = "", int maxLength = -1, Keyboard keyboard = null)
        {
            if (Application.Current?.MainPage != null)
                return await Application.Current.MainPage.DisplayPromptAsync(title, message, accept, cancel, placeholder, maxLength, keyboard);
            return null;
        }

        public async Task<string> ShowActionSheetAsync(string title, string cancel, string destruction, params string[] buttons)
        {
            if (Application.Current?.MainPage != null)
                return await Application.Current.MainPage.DisplayActionSheet(title, cancel, destruction, buttons);
            return cancel;
        }
    }
}