using Android.Content;
using Android.OS;
using Microsoft.Maui.ApplicationModel;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    public class AppUpdateService : IAppUpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly IVersionService _versionService;
        private const string VersionUrl = "http://189.206.160.206:81/EmbarquesApk/BusCheckInV2/version.txt"; // JSON con { "versionCode": "42", "versionName": "1.0.0", "downloadURL": "url" }

        public AppUpdateService(HttpClient httpClient, IVersionService versionService)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        }

        public async Task<bool> IsUpdateAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(VersionUrl);
                var json = JObject.Parse(response);
                var latestVersionCode = json["versionCode"]?.ToString();

                if (string.IsNullOrEmpty(latestVersionCode))
                    return false;

                var currentVersionCode = _versionService.GetBuildNumber(); // Usa interfaz inyectada

                return Convert.ToInt32(latestVersionCode) > Convert.ToInt32(currentVersionCode);
            }
            catch (Exception ex)
            {
                // Log o toast error
                Console.WriteLine($"Error checking update: {ex.Message}");
                return false;
            }
        }

        public async Task DownloadAndInstallAsync()
        {
#if ANDROID
            // Delega a lógica Android-specific (tu código original)
            await DownloadAndInstallAndroidAsync();
#elif IOS
            // Para iOS, abre App Store (no sideload posible)
            await Launcher.OpenAsync(new Uri("itms-apps://itunes.apple.com/app/idTU_APP_ID")); // Cambia por tu Apple ID
#else
            // Para Windows/Desktop, abre web o nothing
            await Launcher.OpenAsync(new Uri("https://tu-sitio.com/download"));
#endif
        }

        private async Task DownloadAndInstallAndroidAsync()
        {
            string apkUrl = "http://189.206.160.206:81/EmbarquesApk/BusCheckInV2/com.mrlucky.buscheckinV2.apk";
            string apkName = "com.mrlucky.buscheckinV2.apk";

            try
            {
                var activity = Platform.CurrentActivity;
                if (activity == null)
                {
                    Console.WriteLine("Actividad no disponible");
                    return;
                }

                // Progreso (usa ProgressDialog o un toast)
                Console.WriteLine("Descargando actualización...");

                byte[] apkBytes = await _httpClient.GetByteArrayAsync(apkUrl);

                var folderPath = Path.Combine(Android.App.Application.Context.GetExternalFilesDir(null).AbsolutePath, "BusCheckIn");
                Directory.CreateDirectory(folderPath);

                string apkPath = Path.Combine(folderPath, apkName);
                File.WriteAllBytes(apkPath, apkBytes);

                // Instala (tu código original)
                var apkFile = new Java.IO.File(apkPath);
                apkFile.SetReadable(true);

                Android.Net.Uri apkUri;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
                {
                    apkUri = AndroidX.Core.Content.FileProvider.GetUriForFile(activity, $"{activity.ApplicationContext.PackageName}.fileprovider", apkFile);
                }
                else
                {
                    apkUri = Android.Net.Uri.FromFile(apkFile);
                }

                var installIntent = new Intent(Intent.ActionView);
                installIntent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
                installIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);

                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    bool canInstall = activity.PackageManager.CanRequestPackageInstalls();
                    if (!canInstall)
                    {
                        var settingsIntent = new Intent(Android.Provider.Settings.ActionManageUnknownAppSources, Android.Net.Uri.Parse($"package:{activity.PackageName}"));
                        activity.StartActivity(settingsIntent);
                        return; // Espera que usuario habilite
                    }
                }

                activity.StartActivity(installIntent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
