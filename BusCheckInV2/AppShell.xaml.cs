using BusCheckInV2.Views;
using BusCheckInV2.Services;
using Microsoft.Maui.Controls;

namespace BusCheckInV2
{
    public partial class AppShell : Shell
    {
        private readonly IAppUpdateService _appUpdateService;
        public AppShell(IAppUpdateService appUpdateService)
        {
            InitializeComponent();
            _appUpdateService = appUpdateService;

            // Registra rutas globales
            Routing.RegisterRoute("SeleccionDeFlete", typeof(SeleccionDeFlete));
            Routing.RegisterRoute("FletesPendientes", typeof(FletesPendientes));
            Routing.RegisterRoute("EscaneoCodigo", typeof(EscaneoCodigo));

            CurrentItem = shellSeleccionDeFlete;
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // Ejemplo: Chequea updates si inyectaste el servicio
            if (_appUpdateService != null)
            {
                await _appUpdateService.IsUpdateAvailableAsync(); // Asume un método en el servicio
            }
        }
    }
}