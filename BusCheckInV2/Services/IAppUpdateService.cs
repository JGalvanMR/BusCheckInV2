using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    public interface IAppUpdateService
    {
        Task<bool> IsUpdateAvailableAsync();
        Task DownloadAndInstallAsync();
    }
}
