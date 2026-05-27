using BarcodeScanning;
using BusCheckInV2.Constants;
using BusCheckInV2.Platforms.Android.Services;
using BusCheckInV2.Services;
using BusCheckInV2.ViewModels;
using BusCheckInV2.Views;
using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Plugin.Maui.Audio;
using Polly; // ✅ ESTO ES OBLIGATORIO
using Polly.Extensions.Http; // Opcional pero recomendado
using System;
using System.Net.Http;

namespace BusCheckInV2;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeScanning()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("AmsiPro-Regular.otf", "AmsiProRegular");
                fonts.AddFont("AmsiPro-Semibold.otf", "AmsiProSemibold");
            });

        // Configuración de servicios
        builder.Services.AddSingleton<IAlertService, AlertService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<IAudioService, AudioService>();

        // HttpClient con Polly
        builder.Services.AddHttpClient<IApiFleteService, ApiFleteService>(client =>
        {
            client.BaseAddress = new Uri(GetApiBaseUrl());
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Add("User-Agent", "BusCheckInV2-MAUI");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler())
        .AddPolicyHandler(GetRetryPolicy());

        // Servicios de datos
        builder.Services.AddSingleton<ISQLiteService, SQLiteService>();
        builder.Services.AddSingleton<IVersionService, VersionServiceAndroid>();
        builder.Services.AddSingleton<IAppUpdateService, AppUpdateService>();

        // ViewModels
        builder.Services.AddTransient<SeleccionDeFleteViewModel>();
        builder.Services.AddTransient<FletesPendientesViewModel>();
        builder.Services.AddTransient<EscaneoCodigoViewModel>();

        // Vistas
        builder.Services.AddTransient<SeleccionDeFlete>();
        builder.Services.AddTransient<FletesPendientes>();
        builder.Services.AddTransient<EscaneoCodigo>();

        // Logging
        builder.Services.AddLogging(configure => configure.AddDebug());

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static string GetApiBaseUrl()
    {
#if DEBUG
        return ApiConstants.BaseUrlDebug;
#else
        return ApiConstants.BaseUrlRelease;
#endif
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