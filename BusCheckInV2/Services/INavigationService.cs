using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    public interface INavigationService
    {
        Task GoToAsync(string route);
        Task GoToAsync(string route, bool animate);
        Task GoBackAsync();
        Task<bool> CanGoBackAsync();
    }
}