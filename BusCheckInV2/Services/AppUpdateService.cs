using Android.Content;
using Android.OS;
using BusCheckInV2.Views.Popups;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    public class AppUpdateService : IAppUpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly IVersionService _versionService;
        private const string VersionUrl = "http://189.206.160.206:81/EmbarquesApk/BusCheckInV2/version.txt";
        private Popup? _loadingPopup;

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

                var currentVersionCode = _versionService.GetBuildNumber();
                return Convert.ToInt32(latestVersionCode) > Convert.ToInt32(currentVersionCode);
            }
            catch
            {
                return false;
            }
        }

        public async Task DownloadAndInstallAsync()
        {
#if ANDROID
            await DownloadAndInstallAndroidAsync();
#elif IOS
            await Launcher.OpenAsync(new Uri("itms-apps://itunes.apple.com/app/idTU_APP_ID"));
#else
            await Launcher.OpenAsync(new Uri("https://tu-sitio.com/download"));
#endif
        }

#if ANDROID
        private async Task DownloadAndInstallAndroidAsync()
        {
            try
            {
                // 1. Obtener la URL de descarga desde el mismo version.txt
                var versionJson = await _httpClient.GetStringAsync(VersionUrl);
                var json = JObject.Parse(versionJson);
                string apkUrl = json["downloadURL"]?.ToString();
                if (string.IsNullOrEmpty(apkUrl))
                    throw new Exception("No se encontró la URL de descarga en version.txt");

                var activity = Platform.CurrentActivity;
                if (activity == null) return;

                // 2. Mostrar Popup de carga
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _loadingPopup = new LoadingPopup();
                    Application.Current?.MainPage?.ShowPopup(_loadingPopup);
                });

                await Task.Delay(500);

                // 3. Descargar APK
                byte[] apkBytes = await Task.Run(() => _httpClient.GetByteArrayAsync(apkUrl));

                // 4. Guardar en caché externa
                var cacheDir = Android.App.Application.Context?.ExternalCacheDir?.AbsolutePath;
                if (string.IsNullOrEmpty(cacheDir))
                    throw new DirectoryNotFoundException("No se pudo acceder al caché externo.");
                string apkName = Path.GetFileName(new Uri(apkUrl).LocalPath);
                string apkPath = Path.Combine(cacheDir, apkName);
                if (File.Exists(apkPath)) File.Delete(apkPath);
                await File.WriteAllBytesAsync(apkPath, apkBytes);

                // 5. Cerrar Popup
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _loadingPopup?.CloseAsync();
                    _loadingPopup = null;
                });

                // 6. Verificar permisos de instalación (Android 8+)
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    if (!activity.PackageManager.CanRequestPackageInstalls())
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        var timeout = Task.Delay(30000);

                        var lifecycleCallbacks = new ActivityLifecycleAdapter();
                        lifecycleCallbacks.OnResumed = (act) =>
                        {
                            bool granted = act.PackageManager.CanRequestPackageInstalls();
                            tcs.TrySetResult(granted);
                            activity.Application.UnregisterActivityLifecycleCallbacks(lifecycleCallbacks);
                        };

                        activity.Application.RegisterActivityLifecycleCallbacks(lifecycleCallbacks);

                        var settingsIntent = new Intent(Android.Provider.Settings.ActionManageUnknownAppSources,
                            Android.Net.Uri.Parse($"package:{activity.PackageName}"));
                        activity.StartActivity(settingsIntent);

                        var completedTask = await Task.WhenAny(tcs.Task, timeout);
                        if (completedTask == timeout)
                        {
                            activity.Application.UnregisterActivityLifecycleCallbacks(lifecycleCallbacks);
                            await MainThread.InvokeOnMainThreadAsync(() =>
                                Application.Current?.MainPage?.DisplayAlert("Error", "Tiempo de espera agotado. No se pudo obtener permiso de instalación.", "OK"));
                            return;
                        }

                        bool permissionGranted = await tcs.Task;
                        if (!permissionGranted)
                        {
                            await MainThread.InvokeOnMainThreadAsync(() =>
                                Application.Current?.MainPage?.DisplayAlert("Permiso denegado", "No se puede instalar la actualización sin el permiso de fuentes desconocidas.", "OK"));
                            return;
                        }
                    }
                }

                // 7. Preparar URI del APK
                var apkFile = new Java.IO.File(apkPath);
                apkFile.SetReadable(true, false);

                Android.Net.Uri apkUri;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
                {
                    string authority = "com.mrlucky.buscheckinV2.fileprovider";
                    apkUri = AndroidX.Core.Content.FileProvider.GetUriForFile(activity, authority, apkFile);
                }
                else
                {
                    apkUri = Android.Net.Uri.FromFile(apkFile);
                }

                // 8. Intent de instalación
                var installIntent = new Intent(Intent.ActionView);
                installIntent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
                installIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission | ActivityFlags.ClearTop);

                activity.StartActivity(installIntent);
            }
            catch (Exception ex)
            {
                // Cerrar popup en caso de error
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _loadingPopup?.CloseAsync();
                    _loadingPopup = null;
                });

                System.Diagnostics.Debug.WriteLine($"[Update Error] {ex.Message}");
                await MainThread.InvokeOnMainThreadAsync(() =>
                    Application.Current?.MainPage?.DisplayAlert("Error", $"No se pudo descargar la actualización: {ex.Message}", "OK"));
            }
        }
#endif
    }

#if ANDROID
    public class ActivityLifecycleAdapter : Java.Lang.Object, Android.App.Application.IActivityLifecycleCallbacks
    {
        public Action<Android.App.Activity>? OnResumed { get; set; }

        public void OnActivityResumed(Android.App.Activity activity) => OnResumed?.Invoke(activity);

        public void OnActivityCreated(Android.App.Activity activity, Android.OS.Bundle? savedInstanceState) { }
        public void OnActivityDestroyed(Android.App.Activity activity) { }
        public void OnActivityPaused(Android.App.Activity activity) { }
        public void OnActivitySaveInstanceState(Android.App.Activity activity, Android.OS.Bundle outState) { }
        public void OnActivityStarted(Android.App.Activity activity) { }
        public void OnActivityStopped(Android.App.Activity activity) { }
    }
#endif
}