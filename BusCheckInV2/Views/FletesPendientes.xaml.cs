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

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is FletesPendientesViewModel viewModel)
            {
                if (!string.IsNullOrEmpty(viewModel.ChoferSeleccionado))
                {
                    // Usar el comando en lugar de llamar directamente al método
                    if (viewModel.CargarFletesPendientesCommand.CanExecute(null))
                    {
                        viewModel.CargarFletesPendientesCommand.Execute(null);
                    }
                }
            }
        }
    }
}