using BusCheckInV2.ViewModels;
using Microsoft.Maui.Controls;

namespace BusCheckInV2.Views;

public partial class SeleccionDeFlete : ContentPage
{
    // FIX 2026-06-02: guardamos el VM en un campo para que OnAppearing
    // pueda consultar el estado de Proveedores (para detectar "primera
    // carga" vs "retorno desde otra pantalla") sin tener que hacer
    // cast del BindingContext.
    private readonly SeleccionDeFleteViewModel _viewModel;

    public SeleccionDeFlete(SeleccionDeFleteViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is SeleccionDeFleteViewModel viewModel)
        {
            await viewModel.InitializeAsync(); // Inicializa datos

            // FIX 2026-06-02: si hay un flete en curso guardado en
            // Preferences, preguntarle al chofer si quiere continuar
            // donde lo dejó.
            //
            // La heurística "no es primera carga" es: si la lista de
            // proveedores ya está cargada, el usuario ya interactuó
            // con esta pantalla antes. En la primera carga, los
            // proveedores aún no se han poblado, así que no
            // interrumpimos el flujo de selección normal.
            //
            // El Task.Delay de 200ms es para que el Shell termine
            // su animación de entrada antes de mostrar el diálogo;
            // si no, el diálogo aparece sobre la animación y se ve
            // raro en Android.
            int ultimoFleteId = Preferences.Get("ultimo_flete_local_id", 0);
            if (ultimoFleteId > 0 && _viewModel.Proveedores != null
                && _viewModel.Proveedores.Count > 0)
            {
                await Task.Delay(200);
                try
                {
                    if (_viewModel.RetomarUltimoFleteCommand.CanExecute(null))
                    {
                        await _viewModel.RetomarUltimoFleteAsync();
                    }
                }
                catch (Exception ex)
                {
                    // No queremos que un error aquí bloquee la app.
                    // Limpiamos la preference y seguimos.
                    SeleccionDeFleteViewModel.LimpiarUltimoFleteEnPreferences();
                    System.Diagnostics.Debug.WriteLine(
                        $"[SeleccionDeFlete.OnAppearing] {ex.Message}");
                }
            }
        }
    }
}
