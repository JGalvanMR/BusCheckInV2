using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    public interface IAudioService
    {
        Task PlayBeepAsync();
        Task PlayErrorAsync();
        void Dispose();
    }
}