using BusCheckInV2.ViewModels;
using Microsoft.Maui.Controls;

namespace BusCheckInV2.Views;

public partial class SeleccionDeFlete : ContentPage
{
	public SeleccionDeFlete(SeleccionDeFleteViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SeleccionDeFleteViewModel viewModel)
        {
            await viewModel.InitializeAsync(); // Inicializa datos
        }
    }
}