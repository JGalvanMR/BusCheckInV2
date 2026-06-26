using System.Windows.Input;
using BusCheckInV2.Models;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;

namespace BusCheckInV2.ViewModels.Popups;

public class FleteDetallePopupViewModel
{
    public FletePendienteUI Flete { get; }
    public ICommand CerrarCommand { get; }
    private readonly Popup _popup;
    public string TipoCompleto => $"{Flete.TipoFlete} - {Flete.TipoViaje}";

    public FleteDetallePopupViewModel(FletePendienteUI flete, Popup popup)
    {
        Flete = flete;
        _popup = popup;
        CerrarCommand = new Command(Cerrar);
    }

    private async void Cerrar()
    {
        if (_popup != null)
            await _popup.CloseAsync();
        else
            await Application.Current.MainPage.Navigation.PopModalAsync();
    }
}