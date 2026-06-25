using BusCheckInV2.Models;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using System.Windows.Input;

namespace BusCheckInV2.ViewModels.Popups;

public class FleteDetallePopupViewModel
{
    private readonly Popup _popup;
    public FletePendienteUI Flete { get; }
    public string TextoPasajeros { get; }
    public ICommand CerrarCommand { get; }

    public FleteDetallePopupViewModel(Popup popup, FletePendienteUI flete)
    {
        _popup = popup;
        Flete = flete;
        TextoPasajeros = $"{flete.CantidadEsperada} esperados / {flete.CantidadReal ?? 0} reales";
        CerrarCommand = new Command(Cerrar);
    }

    private async void Cerrar()
    {
        await Application.Current.MainPage.ClosePopupAsync(_popup);
    }
}