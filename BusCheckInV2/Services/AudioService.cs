using Plugin.Maui.Audio;
using System.IO;
using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    public class AudioService : IAudioService
    {
        private readonly IAudioPlayer _beepPlayer;
        private readonly IAudioPlayer _errorPlayer;

        public AudioService()
        {
            var beepStream = FileSystem.OpenAppPackageFileAsync("scanner.mp3").GetAwaiter().GetResult();
            var beepMemory = new MemoryStream();
            beepStream.CopyTo(beepMemory);
            beepMemory.Position = 0;
            _beepPlayer = AudioManager.Current.CreatePlayer(beepMemory);

            var errorStream = FileSystem.OpenAppPackageFileAsync("error.mp3").GetAwaiter().GetResult();
            var errorMemory = new MemoryStream();
            errorStream.CopyTo(errorMemory);
            errorMemory.Position = 0;
            _errorPlayer = AudioManager.Current.CreatePlayer(errorMemory);
        }

        public Task PlayBeepAsync()
        {
            _beepPlayer.Stop();
            _beepPlayer.Seek(0);
            _beepPlayer.Play();
            return Task.CompletedTask;
        }

        public Task PlayErrorAsync()
        {
            _errorPlayer.Stop();
            _errorPlayer.Seek(0);
            _errorPlayer.Play();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _beepPlayer?.Dispose();
            _errorPlayer?.Dispose();
        }
    }
}