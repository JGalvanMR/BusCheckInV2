using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    public interface IAlertService
    {
        Task ShowAlertAsync(string title, string message, string button = "OK");
        Task<bool> ShowConfirmationAsync(string title, string message, string accept = "Sí", string cancel = "No");
        Task<string> ShowPromptAsync(string title, string message, string accept = "OK", string cancel = "Cancelar", string placeholder = "", int maxLength = -1, Keyboard keyboard = null);
        Task<string> ShowActionSheetAsync(string title, string cancel, string destruction, params string[] buttons);
    }
}