using CommunityToolkit.Maui.Views;
using BusCheckInV2.Models;
using BusCheckInV2.ViewModels.Popups;

namespace BusCheckInV2.Views.Popups;

public partial class FleteDetallePopup : Popup
{
    public FleteDetallePopup(FletePendienteUI flete)
    {
        InitializeComponent();
        BindingContext = new FleteDetallePopupViewModel(flete, this);
    }
}