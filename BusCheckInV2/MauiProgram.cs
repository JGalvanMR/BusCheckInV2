using BarcodeScanning;
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

        // FIX 2026-06-02 (C5): el BaseAddress/Timeout que se setean aquí
        // son IGNORADOS por ApiFleteService porque usa GetApiUrl() que
        // concatena ApiConstants.BaseUrlDebug + ApiConstants.ApiPath. Las
        // URLs finales del log del usuario (http://189.206.160.206:82/
        // BusCheckInV2/api/WSBusCheckInV2/...) confirman que se usa
        // ApiConstants, no el BaseAddress del HttpClient.
        //
        // Por seguridad, mantenemos el User-Agent y el retry policy (esos
        // SÍ se respetan), pero comentamos BaseAddress y Timeout porque
        // dan una falsa sensación de control sobre algo que en realidad
        // no controlan.
        // HttpClient con Polly
        builder.Services.AddHttpClient<IApiFleteService, ApiFleteService>(client =>
        {
            //client.BaseAddress = new Uri(GetApiBaseUrl());  // IGNORADO por ApiFleteService
            //client.Timeout = TimeSpan.FromSeconds(15);     // IGNORADO por ApiFleteService
            client.DefaultRequestHeaders.Add("User-Agent", "BusCheckInV2-MAUI");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler())
        .AddPolicyHandler(GetRetryPolicy());

        // Servicios de datos
        builder.Services.AddSingleton<ISQLiteService, SQLiteService>();
        builder.Services.AddSingleton<IVersionService, VersionServiceAndroid>();
        builder.Services.AddSingleton<IAppUpdateService, AppUpdateService>();

        // FIX 2026-06-02 (C4): SyncService tiene URL hardcodeada
        // "https://your-server-url/api/your-endpoint" (placeholder) y
        // SyncBackgroundService la ejecuta en loop infinito cada 5 min.
        // Si en el futuro se arregla SyncService, va a duplicar
        // sincronizaciones con las que ya hace EscaneoCodigoViewModel
        // cada 15 segundos. Por ahora, lo desactivamos.
        // builder.Services.AddSingleton<ISyncService, SyncService>();
        // builder.Services.AddHostedService<SyncBackgroundService>();

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

    // FIX 2026-06-02 (C5): este método ya no se usa porque BaseAddress
    // se ignora (ver comentario arriba). Lo dejamos comentado para
    // referencia futura: si en algún momento se quiere usar
    // BaseAddress de verdad, hay que cambiar ApiFleteService.GetApiUrl
    // para usar client.BaseAddress en vez de ApiConstants.
    //private static string GetApiBaseUrl()
    //{
    //#if DEBUG
    //    return ApiConstants.BaseUrlDebug;
    //#else
    //    return ApiConstants.BaseUrlRelease;
    //#endif
    //}

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
            .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}