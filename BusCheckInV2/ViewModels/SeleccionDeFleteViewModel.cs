using BusCheckInV2.Models;
using BusCheckInV2.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelliJ.Lang.Annotations;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BusCheckInV2.Views;

namespace BusCheckInV2.ViewModels
{
    public partial class SeleccionDeFleteViewModel : ObservableObject
    {
        private readonly ISQLiteService _databaseService;
        private readonly IAppUpdateService _appUpdateService;

        private readonly IApiFleteService _apiFleteService;
        private readonly IConnectivity _connectivity;
        private bool _permisosVerificados = false;

        #region PROPIEDADES BINDABLES
        public ObservableCollection<Tb_Cat_Proveedor> Proveedores { get; set; }
        [ObservableProperty]
        private Tb_Cat_Proveedor selectedProveedor;

        public ObservableCollection<Tb_FlePer_Ruta> Rutas { get; set; }
        [ObservableProperty]
        private Tb_FlePer_Ruta selectedRuta;

        [ObservableProperty]
        private List<string> tipoFleteOptions;

        [ObservableProperty]
        public string selectedTipoFlete;

        [ObservableProperty]
        private List<string> tipoViajeOptions;

        [ObservableProperty]
        public string selectedTipoViaje;

        [ObservableProperty]
        private string nombreChofer;

        [ObservableProperty]
        private string versionText;

        [ObservableProperty]
        private bool isRutaEnabled;

        [ObservableProperty]
        private bool isTipoFleteEnabled;

        [ObservableProperty]
        private bool isTipoViajeEnabled;

        [ObservableProperty]
        private bool isContinuarEnabled;
        #endregion

        public SeleccionDeFleteViewModel(ISQLiteService databaseService, IAppUpdateService appUpdateService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _appUpdateService = appUpdateService ?? throw new ArgumentNullException(nameof(appUpdateService));

            Proveedores = new ObservableCollection<Tb_Cat_Proveedor>();
            Rutas = new ObservableCollection<Tb_FlePer_Ruta>();
            TipoFleteOptions = new List<string> { "NORMAL", "MIXTO", "T.E.", "EXTRAORDINARIO" };
            TipoViajeOptions = new List<string> { "TRAER GENTE", "LLEVAR GENTE" };

            // Resuelve el servicio (ajusta si usas DI completo)
            _appUpdateService = Microsoft.Maui.Controls.Application.Current.Handler.MauiContext.Services.GetService<IAppUpdateService>();
        }

        public async Task InitializeAsync()
        {
            VersionText = $"Version: {AppInfo.VersionString}";
            await VerificarPermisosAsync();
            await CheckForUpdatesAsync();
            LimpiarControles();
            IsContinuarEnabled = true;
            await LoadProveedoresAsync();
        }

        partial void OnSelectedProveedorChanged(Tb_Cat_Proveedor value)
        {
            if (value != null)
            {
                _ = LoadRutasAsync(value); // Carga rutas asíncronamente
            }
        }

        partial void OnSelectedRutaChanged(Tb_FlePer_Ruta value)
        {
            if (value != null)
            {
                IsTipoFleteEnabled = true;
            }
        }

        partial void OnSelectedTipoFleteChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                IsTipoViajeEnabled = true;
            }
        }

        [RelayCommand]
        private async Task ContinuarAsync()
        {
            IsContinuarEnabled = false;
            //IsBusy = true;
            try
            {
                if (string.IsNullOrEmpty(NombreChofer) || SelectedProveedor == null || SelectedRuta == null || SelectedTipoFlete == null || SelectedTipoViaje == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Error", "Todos los campos deben ser completados.", "OK");
                    return;
                }

                var provClave = SelectedProveedor.prov_clave;
                var idDestFlete = SelectedRuta.IdDestFlete;
                var fletePersonal = new Tb_FlePer_FletePersonal
                {
                    FlePer_Fecha = DateTime.Now.ToString("MM/dd/yyyy"),
                    FlePer_Hora = DateTime.Now.ToString("HH:mm:ss"),
                    Prov_Clave = provClave,
                    IdDestFlete = idDestFlete,
                    FlePer_TipoFlete = SelectedTipoFlete,
                    FlePer_TipoViaje = SelectedTipoViaje,
                    FlePer_Chofer = NombreChofer,
                    IsSynced = false,
                };

                await _databaseService.InsertAsync(fletePersonal);
                await Application.Current.MainPage.DisplayAlert("Éxito", "Datos guardados correctamente.", "OK");

                //var escaneoCodigo = App.Current.Handler.MauiContext.Services.GetRequiredService<EscaneoCodigo>();
                //await Application.Current.MainPage.Navigation.PushAsync(escaneoCodigo);

                //await Shell.Current.GoToAsync("//escaneocodigo");
                await Shell.Current.GoToAsync(nameof(EscaneoCodigo));
                //await Shell.Current.Navigation.PushAsync(App.Current.Handler.MauiContext.Services.GetService<EscaneoCodigo>());
            }
            finally
            {
                IsContinuarEnabled = true;
                //IsBusy = false;
            }
        }

        private async Task LoadProveedoresAsync()
        {
            var proveedores = await _databaseService.GetItemsAsync<Tb_Cat_Proveedor>();
            if (proveedores != null && proveedores.Any())
            {
                Proveedores.Clear();
                foreach (var prov in proveedores)
                {
                    Proveedores.Add(prov);
                }
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Advertencia", "No se encontraron proveedores disponibles.", "OK");
            }
        }

        private async Task LoadRutasAsync(Tb_Cat_Proveedor proveedor)
        {
            try
            {
                var provRutas = await _databaseService.GetItemsAsync<Tb_FlePer_ProvRuta>();
                var todasRutas = await _databaseService.GetItemsAsync<Tb_FlePer_Ruta>();

                var rutasFiltradas = provRutas
                    .Where(r => r.Prov_Clave == proveedor.prov_clave)
                    .Where(r => r.RutaStatus == "A")
                    .Join(todasRutas,
                        provRuta => provRuta.IdDestFlete,
                        ruta => ruta.IdDestFlete,
                        (provRuta, ruta) => new Tb_FlePer_Ruta
                        {
                            IdDestFlete = ruta.IdDestFlete,
                            NomDestFlete = ruta.NomDestFlete
                        })
                    .ToList();

                Rutas.Clear();
                if (rutasFiltradas.Any())
                {
                    foreach (var ruta in rutasFiltradas)
                    {
                        Rutas.Add(ruta);
                    }
                    IsRutaEnabled = true;
                }
                else
                {
                    IsRutaEnabled = false;
                    await Application.Current.MainPage.DisplayAlert("Advertencia", "No se encontraron rutas disponibles para el proveedor seleccionado.", "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Ocurrió un error al cargar las rutas: {ex.Message}", "OK");
            }
        }

        private void LimpiarControles()
        {
            NombreChofer = string.Empty;
            SelectedProveedor = null;
            Rutas.Clear();
            IsRutaEnabled = false;
            SelectedRuta = null;
            SelectedTipoFlete = null;
            IsTipoFleteEnabled = false;
            SelectedTipoViaje = null;
            IsTipoViajeEnabled = false;
        }

        private async Task<bool> VerificarPermisosAsync()
        {
            if (_permisosVerificados) return true;

            var statusStorageRead = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            var statusStorageWrite = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
            var statusUbicacion = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            var statusUbicacionBack = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            var statusCamara = await Permissions.CheckStatusAsync<Permissions.Camera>();

            if (statusStorageRead != PermissionStatus.Granted || statusStorageWrite != PermissionStatus.Granted ||
                statusUbicacion != PermissionStatus.Granted || statusUbicacionBack != PermissionStatus.Granted ||
                statusCamara != PermissionStatus.Granted)
            {
                statusStorageRead = await Permissions.RequestAsync<Permissions.StorageRead>();
                statusStorageWrite = await Permissions.RequestAsync<Permissions.StorageWrite>();
                statusUbicacion = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                statusUbicacionBack = await Permissions.RequestAsync<Permissions.LocationAlways>();
                statusCamara = await Permissions.RequestAsync<Permissions.Camera>();

                if (statusStorageRead != PermissionStatus.Granted || statusStorageWrite != PermissionStatus.Granted ||
                    statusUbicacion != PermissionStatus.Granted || statusUbicacionBack != PermissionStatus.Granted ||
                    statusCamara != PermissionStatus.Granted)
                {
                    await Application.Current.MainPage.DisplayAlert("Permisos Denegado", "No se puede obtener la ubicación sin permisos.", "OK");
                    return false;
                }
            }

            _permisosVerificados = true;
            return true;
        }

        private async Task CheckForUpdatesAsync()
        {
            bool isUpdateAvailable = await _appUpdateService.IsUpdateAvailableAsync();
            if (isUpdateAvailable)
            {
                bool userWantsToUpdate = await Application.Current.MainPage.DisplayAlert(
                    "Actualización Disponible",
                    "Hay una nueva versión de la aplicación disponible. ¿Deseas actualizar?",
                    "Sí", "No");
                if (userWantsToUpdate)
                {
                    await _appUpdateService.DownloadAndInstallAsync();
                }
            }
        }

        // Agregar este comando en la clase SeleccionDeFleteViewModel
        [RelayCommand]
        private async Task IrAFletesPendientes()
        {
            await Shell.Current.GoToAsync(nameof(FletesPendientes));
        }
        [RelayCommand]
        private async Task Sincronizar()
        {
            // Opcional: Lógica de sincronización
            await Application.Current.MainPage.DisplayAlert(
                "Sincronización",
                "Sincronizando datos...",
                "OK");

            // Aquí podrías llamar a tu SyncService
            // await _syncService.SyncDataAsync();
        }
    }
}
