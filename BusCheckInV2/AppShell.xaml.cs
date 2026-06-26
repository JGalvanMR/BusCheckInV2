using BusCheckInV2.Views;
using Microsoft.Maui.Controls;

namespace BusCheckInV2
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("SeleccionDeFlete", typeof(SeleccionDeFlete));
            Routing.RegisterRoute("FletesPendientes", typeof(FletesPendientes));
            Routing.RegisterRoute("EscaneoCodigo", typeof(EscaneoCodigo));

            CurrentItem = shellSeleccionDeFlete;
        }
    }
}