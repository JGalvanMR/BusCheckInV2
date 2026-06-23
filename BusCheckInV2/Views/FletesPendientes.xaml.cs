// BusCheckInV2/Views/FletesPendientes.xaml.cs

using BusCheckInV2.ViewModels;
using Microsoft.Maui.Controls;

namespace BusCheckInV2.Views
{
    public partial class FletesPendientes : ContentPage
    {
        // El ViewModel se inyecta desde DI igual que en SeleccionDeFlete
        public FletesPendientes(FletesPendientesViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is FletesPendientesViewModel viewModel)
            {
                // Solo cargamos los choferes si la lista está vacía.
                // Evita recargar innecesariamente si el usuario navega
                // hacia atrás y regresa a esta pantalla.
                if (!viewModel.ListaUsuarios.Any())
                {
                    await viewModel.CargarChoferesCommand.ExecuteAsync(null);
                }
            }
        }

        // FIX 2026-06-04: handler del Switch "Solo pendientes".
        // El binding TwoWay actualiza MostrarSoloPendientes en el VM,
        // pero NO dispara el comando de carga. Sin este handler el
        // chofer tenía que pulsar "Actualizar" cada vez que tocaba
        // el switch, lo cual era una pesadilla de UX.
        //
        // El _cargaFletesEnCurso del VM actúa como anti-carrera:
        // si ya hay una carga en curso, CargarFletesPendientesAsync
        // sale inmediatamente sin duplicar ni corromper la lista.
        private async void OnMostrarSoloPendientesChanged(object sender, ToggledEventArgs e)
        {
            if (BindingContext is FletesPendientesViewModel viewModel)
            {
                await viewModel.CargarFletesPendientesCommand.ExecuteAsync(null);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            if (BindingContext is FletesPendientesViewModel viewModel)
            {
                viewModel.Dispose();
            }
        }
    }
}