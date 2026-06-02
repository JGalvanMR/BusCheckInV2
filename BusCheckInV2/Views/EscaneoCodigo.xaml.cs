using BusCheckInV2.ViewModels;
using Microsoft.Maui.Controls;

namespace BusCheckInV2.Views;

public partial class EscaneoCodigo : ContentPage
{
    // FIX 2026-06-01 (Problema #2 del usuario): cuando el usuario elige
    // "Salir de todas formas" en el diálogo de pendientes, se setea este
    // flag para que la próxima navegación NO se vuelva a cancelar.
    private bool _permitirSalida;

    public EscaneoCodigo(EscaneoCodigoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // CRÍTICO PARA V3: Solicita el permiso nativo de la cámara antes
        // de inicializar la lógica.
        await BarcodeScanning.Methods.AskForRequiredPermissionAsync();

        // Suscribimos al evento de navegación de Shell para interceptar
        // tanto el botón "Atrás" de Android como cualquier GoToAsync("..")
        // que intente sacar al usuario sin sincronizar.
        Shell.Current.Navigating += OnShellNavigating;

        if (BindingContext is EscaneoCodigoViewModel vm)
            await vm.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Shell.Current.Navigating -= OnShellNavigating;
    }

    private async void OnShellNavigating(object sender, ShellNavigatingEventArgs e)
    {
        if (_permitirSalida) return;

        // Solo nos interesa cuando se está yendo hacia atrás (pop) o al root
        if (e.Source != ShellNavigationSource.Pop &&
            e.Source != ShellNavigationSource.PopToRoot)
            return;

        if (BindingContext is not EscaneoCodigoViewModel vm)
            return;

        int pendientes = await vm.ContarPendientesSyncAsync();
        if (pendientes <= 0) return; // todo sincronizado, dejar salir

        // Hay pendientes: cancelar la navegación y mostrar opciones
        e.Cancel();

        var opcion = await DisplayActionSheet(
            $"Tienes {pendientes} registro(s) sin sincronizar con el servidor.\n\n" +
            $"Si sales ahora podrían perderse cuando cambies de teléfono, " +
            $"se cierre la app o se desinstale.",
            "Quedarme aquí",                  // opción 1 (cancelar)
            null,                              // destruction (no queremos)
            "Reintentar sincronización",      // opción 2
            "Salir de todas formas");         // opción 3

        if (opcion == "Reintentar sincronización")
        {
            await vm.IntentarSincronizarAsync();

            // Re-evaluar después del intento
            pendientes = await vm.ContarPendientesSyncAsync();
            if (pendientes <= 0)
            {
                _permitirSalida = true;
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlert("Aún hay pendientes",
                    $"Quedan {pendientes} registro(s) sin sincronizar. " +
                    $"Verifica tu conexión a internet o espera unos minutos " +
                    $"y vuelve a intentar.",
                    "OK");
            }
        }
        else if (opcion == "Salir de todas formas")
        {
            var confirmar = await DisplayAlert(
                "¿Seguro?",
                $"Se perderán {pendientes} registro(s) si no se sincronizan " +
                $"antes de cerrar la app o cambiar de dispositivo.\n\n" +
                $"¿Salir de todas formas?",
                "Sí, salir",
                "No, quedarme");

            if (confirmar)
            {
                _permitirSalida = true;
                await Shell.Current.GoToAsync("..");
            }
        }
        // "Quedarme aquí" o null (back gesture): no hacer nada, ya cancelamos e
    }

    private void ContentPage_Unloaded(object sender, EventArgs e)
    {
        if (BindingContext is EscaneoCodigoViewModel vm)
            vm.Dispose();
    }
}
