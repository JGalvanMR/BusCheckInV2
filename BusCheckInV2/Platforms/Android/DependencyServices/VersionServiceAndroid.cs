using Android.Content.PM;
using BusCheckInV2.Services;
using Microsoft.Maui.ApplicationModel;

[assembly: Dependency(typeof(BusCheckInV2.Platforms.Android.Services.VersionServiceAndroid))]
namespace BusCheckInV2.Platforms.Android.Services
{
    public class VersionServiceAndroid : IVersionService
    {
        public string GetVersionNumber()
        {
            return AppInfo.VersionString; // Usa MAUI's AppInfo para cross-platform
        }

        public string GetBuildNumber()
        {
            var context = global::Android.App.Application.Context;
            var packageInfo = context.PackageManager.GetPackageInfo(context.PackageName, 0);
            return packageInfo.VersionCode.ToString();
        }
    }
}
