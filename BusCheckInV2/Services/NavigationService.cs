using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    public class NavigationService : INavigationService
    {
        public async Task GoToAsync(string route)
        {
            await Shell.Current.GoToAsync(route);
        }

        public async Task GoToAsync(string route, bool animate)
        {
            await Shell.Current.GoToAsync(route, animate);
        }

        public async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        public Task<bool> CanGoBackAsync()
        {
            return Task.FromResult(Shell.Current.Navigation.NavigationStack.Count > 1);
        }
    }
}