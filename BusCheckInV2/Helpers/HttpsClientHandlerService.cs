#if ANDROID
using Xamarin.Android.Net;
#endif

namespace BusCheckInV2.Helpers;

public class HttpsClientHandlerService
{
    public HttpMessageHandler GetPlatformMessageHandler()
    {
#if ANDROID
        var handler = new AndroidMessageHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
        {
            if (cert != null)
            {
                System.Diagnostics.Debug.WriteLine($"Subject: {cert.Subject}");
                System.Diagnostics.Debug.WriteLine($"Issuer:  {cert.Issuer}");
                System.Diagnostics.Debug.WriteLine($"Errors:  {errors}");
                System.Diagnostics.Debug.WriteLine($"Thumbprint: {cert.GetCertHashString()}");
            }
            return true; // TEMPORAL - solo para ver el log
        };
        return handler;
#else
        return new HttpClientHandler();
#endif
    }
}