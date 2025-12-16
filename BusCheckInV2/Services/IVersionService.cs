using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    public interface IVersionService
    {
        string GetVersionNumber(); // e.g., "1.0.0"
        string GetBuildNumber(); // e.g., "42"
    }
}
