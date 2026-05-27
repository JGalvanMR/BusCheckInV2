using BusCheckInV2.ViewModels;

namespace BusCheckInV2.Views
{
    public partial class FletesPendientes : ContentPage
    {
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
                // RECUPERADO DE LA VIEJA: Si ya hay un chofer seleccionado (ej. el usuario volvió de escanear),
                // forzamos la recarga de la lista para ver los cambios actualizados.
                if (!string.IsNullOrEmpty(viewModel.ChoferSeleccionado) && viewModel.FletesPendientes.Any())
                {
                    await viewModel.CargarFletesPendientesCommand.ExecuteAsync(null);
                }
                // Si la lista está vacía (primera vez), carga los choferes
                else if (!viewModel.ListaUsuarios.Any())
                {
                    await viewModel.CargarChoferesCommand.ExecuteAsync(null);
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // MANTENIDO DE LA NUEVA: Prevenir Memory Leaks
            if (BindingContext is FletesPendientesViewModel viewModel)
            {
                viewModel.Dispose();
            }
        }
    }
}