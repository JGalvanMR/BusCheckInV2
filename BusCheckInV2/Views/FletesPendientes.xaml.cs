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

        //protected override void OnDisappearing()
        //{
        //    base.OnDisappearing();
        //    // Al salir de la pantalla liberamos el ViewModel
        //    // para cancelar suscripciones a Connectivity
        //    if (BindingContext is FletesPendientesViewModel viewModel)
        //    {
        //        viewModel.Dispose();
        //    }
        //}
    }
}