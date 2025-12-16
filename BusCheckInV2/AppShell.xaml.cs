using BusCheckInV2.Views;
using BusCheckInV2.Services;
using Microsoft.Maui.Controls;

namespace BusCheckInV2
{
    public partial class AppShell : Shell
    {
        private readonly IAppUpdateService _appUpdateService;
        public AppShell(IAppUpdateService appUpdateService = null)
        {
            InitializeComponent();
            _appUpdateService = appUpdateService;

            // Registra rutas globales
            Routing.RegisterRoute("SeleccionDeFlete", typeof(SeleccionDeFlete));
            Routing.RegisterRoute("FletesPendientes", typeof(FletesPendientes));
            Routing.RegisterRoute("EscaneoCodigo", typeof(EscaneoCodigo));

            CurrentItem = shellSeleccionDeFlete;
        }
    }
}