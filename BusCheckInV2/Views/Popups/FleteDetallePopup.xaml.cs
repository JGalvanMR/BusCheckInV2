using BusCheckInV2.Models;
using BusCheckInV2.ViewModels.Popups;
using CommunityToolkit.Maui.Views;

namespace BusCheckInV2.Views.Popups;

public partial class FleteDetallePopup : Popup
{
    public FleteDetallePopup(FletePendienteUI flete)
    {
        InitializeComponent();
        BindingContext = new FleteDetallePopupViewModel(this,flete);
    }
}