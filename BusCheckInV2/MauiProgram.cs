using BarcodeScanning;
using BusCheckInV2.Platforms.Android.Services;
using BusCheckInV2.Services;
using BusCheckInV2.Services.Database;
using BusCheckInV2.Services.Sync;
using BusCheckInV2.ViewModels;
using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Plugin.Maui.Audio;
//using WSBusCheckIn;
using BusCheckInV2.Views;
using Polly.Extensions.Http;
using Polly;

namespace BusCheckInV2
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseBarcodeScanning()
                .UseMauiCommunityToolkit()
                .AddAudio()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("AmsiPro-Regular.otf", "AmsiProRegular");
                    fonts.AddFont("AmsiPro-Semibold.otf", "AmsiProSemibold");
                });

            #region SERVICIOS
            // Configurar HttpClient con políticas de reintento
            builder.Services.AddHttpClient<IApiFleteService, ApiFleteServiceReal>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "BusCheckInV2-MAUI");

#if DEBUG
                // Para desarrollo con emulador
                client.BaseAddress = new Uri("http://189.206.160.206:81");
#else
            // Para producción
            client.BaseAddress = new Uri("http://189.206.160.206:5000");
#endif
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
#if ANDROID
                // Permitir HTTP en desarrollo
                var handler = new HttpClientHandler();
                if (builder.Configuration["AllowUnsafeSSL"] == "true")
                {
                    handler.ServerCertificateCustomValidationCallback =
                        (message, cert, chain, errors) => true;
                }
                return handler;
#else
            return new HttpClientHandler();
#endif
            })
            .AddPolicyHandler(GetRetryPolicy());

            // Servicios existentes
            builder.Services.AddSingleton<ISQLiteService, SQLiteService>();
            builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
            builder.Services.AddSingleton<ISyncService, SyncService>();
            builder.Services.AddHttpClient<IAppUpdateService, AppUpdateService>();
            builder.Services.AddSingleton<IVersionService, VersionServiceAndroid>();
            builder.Services.AddSingleton<IConnectivity>(Connectivity.Current);
            #endregion

            #region VIEWMODELS
            builder.Services.AddTransient<SeleccionDeFleteViewModel>();
            builder.Services.AddTransient<FletesPendientesViewModel>();
            #endregion

            #region VISTAS  
            builder.Services.AddTransient<SeleccionDeFlete>();
            builder.Services.AddTransient<EscaneoCodigo>();
            builder.Services.AddTransient<FletesPendientes>();
            #endregion

            #region LOGGING
            builder.Services.AddLogging(configure =>
            {
                configure.AddDebug();
                configure.AddConsole();
            });
            #endregion

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }
    }
}
