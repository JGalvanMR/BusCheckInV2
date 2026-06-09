using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using BusCheckInV2.Services;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using AndroidX.Core.Content;
using AndroidX.Core.App;
using Android.Content.PM;
using AndroidApp = Android.App;
using AndroidOS = Android.OS;
using AndroidNet = Android.Net;
using AndroidProvider = Android.Provider;
using Newtonsoft.Json.Linq;
using Org.Apache.Http.Client;

[assembly: Dependency(typeof(BusCheckInV2.Platforms.Android.Services.AppUpdateServiceAndroid))]
namespace BusCheckInV2.Platforms.Android.Services
{
    public class AppUpdateServiceAndroid : IAppUpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly Context _context;

        public AppUpdateServiceAndroid()
        {
            _httpClient = new HttpClient();
            _context = AndroidApp.Application.Context;
        }

        public async Task<bool> IsUpdateAvailableAsync()
        {
            // URL del servicio que proporciona la última versión
            var url = "http://189.206.160.206:81/EmbarquesApk/BusCheckInV2/version.txt";

            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var json = JObject.Parse(response);
                var latestVersionCode = json["versionCode"]?.ToString();
                var latestVersionName = json["versionName"]?.ToString();
                var downloadURL = json["downloadURL"]?.ToString();

                if (string.IsNullOrEmpty(latestVersionCode))
                    return false;

                var currentVersionCode = AppInfo.BuildString;

                if (Convert.ToInt32(latestVersionCode.ToString()) > Convert.ToInt32(currentVersionCode.ToString()))
                {
                    return true;
                }
                // Comparar versiones
                return new Version(latestVersionCode).CompareTo(new Version(currentVersionCode)) > 0;
            }
            catch
            {
                // Maneja excepciones según sea necesario
                return false;
            }
        }

        public async Task DownloadAndInstallAsync()
        {
            string apkUrl = "http://189.206.160.206:81/EmbarquesApk/BusCheckIn/com.mrlucky.buscheckinV2.apk";
            string apkName = "com.mrlucky.buscheckinV2.apk";

            try
            {
                var activity = Platform.CurrentActivity;
                if (activity == null)
                {
                    Toast.MakeText(AndroidApp.Application.Context, "Actividad no disponible", ToastLength.Long).Show();
                    return;
                }

                // Mostrar un diálogo de progreso
                ProgressDialog progressDialog = new ProgressDialog(activity);
                progressDialog.SetMessage("Descargando actualización...");
                progressDialog.SetCancelable(false);
                progressDialog.Show();

                // Descargar el APK
                var httpClient = new HttpClient();
                byte[] apkBytes = await httpClient.GetByteArrayAsync(apkUrl);

                // Guardar el APK en el almacenamiento externo
                var folderPath = System.IO.Path.Combine(AndroidApp.Application.Context.GetExternalFilesDir(null).AbsolutePath, "BusCheckIn");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string apkPath = Path.Combine(folderPath, apkName);
                File.WriteAllBytes(apkPath, apkBytes);

                progressDialog.Hide();

                // Crear el Intent para instalar el APK
                Java.IO.File apkFile = new Java.IO.File(apkPath);
                apkFile.SetReadable(true);

                AndroidNet.Uri apkUri;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
                {
                    apkUri = AndroidX.Core.Content.FileProvider.GetUriForFile(activity, $"{activity.ApplicationContext.PackageName}.fileprovider", apkFile);
                }
                else
                {
                    apkUri = AndroidNet.Uri.FromFile(apkFile);
                }

                Intent installIntent = new Intent(Intent.ActionView);
                installIntent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
                installIntent.SetFlags(ActivityFlags.NewTask);
                installIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

                // Verificar permisos para instalar paquetes desde fuentes desconocidas
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    bool canInstallPackages = AndroidApp.Application.Context.PackageManager.CanRequestPackageInstalls();
                    if (!canInstallPackages)
                    {
                        // Solicitar permiso si no está concedido
                        Intent settingsIntent = new Intent(AndroidProvider.Settings.ActionManageUnknownAppSources, AndroidNet.Uri.Parse($"package:{activity.PackageName}"));
                        activity.StartActivity(settingsIntent);
                        Toast.MakeText(activity, "Permite la instalación de aplicaciones de fuentes desconocidas", ToastLength.Long).Show();

                        // Evitar interrumpir el flujo, continuar después de que el usuario regrese a la aplicación
                        //return;
                    }
                }

                activity.StartActivity(installIntent);
            }
            catch (Exception ex)
            {
                Toast.MakeText(AndroidApp.Application.Context, $"Error: {ex.Message}", ToastLength.Long).Show();
            }
        }


    }
}
