using BusCheckInV2.Models;
using BusCheckInV2.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Networking;
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

        // FIX (2026-06-01): se eliminan los campos _apiFleteService e _connectivity.
        // Estaban declarados pero nunca se inyectaban en el constructor ni se
        // usaban en ningún método. Eran refactor incompleto de una versión
        // anterior que consultaba online; el flujo actual es 100% offline-first
        // hacia SQLite para el catálogo (proveedores/rutas) y solo sincroniza
        // el flete una vez creado. El connectivity del sistema se sigue
        // consultando vía la clase estática Microsoft.Maui.Networking.Connectivity.
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

            // Colecciones observables — inicializadas aquí, no en InitializeAsync,
            // para que el binding del XAML funcione desde el primer render.
            Proveedores = new ObservableCollection<Tb_Cat_Proveedor>();
            Rutas = new ObservableCollection<Tb_FlePer_Ruta>();
            TipoFleteOptions = new List<string> { "NORMAL", "MIXTO", "T.E.", "EXTRAORDINARIO" };
            TipoViajeOptions = new List<string> { "TRAER GENTE", "LLEVAR GENTE" };
        }

        public async Task InitializeAsync()
        {
            VersionText = $"Version: {AppInfo.VersionString}";
            await VerificarPermisosAsync();
            await CheckForUpdatesAsync();
            LimpiarControles();
            IsContinuarEnabled = true;
            await LoadProveedoresAsync();
            await _databaseService.LimpiarSyncLogAntiguoAsync(diasRetencion: 7);
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

        // BusCheckInV2/ViewModels/SeleccionDeFleteViewModel.cs
        [RelayCommand]
        private async Task ContinuarAsync()
        {
            IsContinuarEnabled = false;
            try
            {
                // Validación sin cambios
                if (string.IsNullOrEmpty(NombreChofer) ||
                    SelectedProveedor == null ||
                    SelectedRuta == null ||
                    SelectedTipoFlete == null ||
                    SelectedTipoViaje == null)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Error", "Todos los campos deben ser completados.", "OK");
                    return;
                }

                var fletePersonal = new Tb_FlePer_FletePersonal
                {
                    Fecha = DateTime.Now,
                    Hora = DateTime.Now.TimeOfDay,
                    ProvClave = SelectedProveedor.ProvClave,
                    IdDestFlete = SelectedRuta.IdDestFlete,
                    TipoFlete = SelectedTipoFlete,
                    TipoViaje = SelectedTipoViaje,
                    Chofer = NombreChofer,
                    IsSynced = false,
                };

                // ─── CAMBIO CRÍTICO ───────────────────────────────────────────────
                // sqlite-net-pcl popula fletePersonal.Id automáticamente después
                // del InsertAsync porque Id está marcado como [PrimaryKey, AutoIncrement]
                await _databaseService.InsertAsync(fletePersonal);

                // fletePersonal.Id ahora tiene el valor asignado por SQLite (ej: 7)
                // Lo pasamos como query parameter a EscaneoCodigo
                var fleteLocalId = fletePersonal.Id;

                // FIX 2026-06-02: guardar el flete activo en Preferences para
                // implementar "continuar con el último flete" en próximas
                // sesiones. Si el chofer cierra la app a medio viaje, al
                // reabrir la pantalla principal verá un diálogo ofreciendo
                // retomar este flete. Se limpia en EscaneoCodigoViewModel
                // cuando se finaliza o cancela el viaje.
                Preferences.Set("ultimo_flete_local_id", fleteLocalId);
                Preferences.Set("ultimo_flete_chofer", NombreChofer ?? "");
                Preferences.Set("ultimo_flete_ruta", SelectedRuta?.NomDestFlete ?? "");
                Preferences.Set("ultimo_flete_fecha", DateTime.Now.ToString("o"));

                await Application.Current.MainPage.DisplayAlert(
                    "Éxito", "Datos guardados correctamente.", "OK");

                // Navegamos con el ID como parámetro en la URL de Shell
                // Shell deserializará "fleteLocalId=7" y lo entregará al ViewModel
                await Shell.Current.GoToAsync(
                    $"{nameof(EscaneoCodigo)}?fleteLocalId={fleteLocalId}");
                // ─────────────────────────────────────────────────────────────────
            }
            finally
            {
                IsContinuarEnabled = true;
            }
        }
        private async Task ContinuarAsyncLEGACY()
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

                var provClave = SelectedProveedor.ProvClave;
                var idDestFlete = SelectedRuta.IdDestFlete;
                var fletePersonal = new Tb_FlePer_FletePersonal
                {
                    Fecha = DateTime.Now,
                    Hora = DateTime.Now.TimeOfDay,
                    ProvClave = provClave,
                    IdDestFlete = idDestFlete,
                    TipoFlete = SelectedTipoFlete,
                    TipoViaje = SelectedTipoViaje,
                    Chofer = NombreChofer,
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
                    .Where(r => r.Prov_Clave == proveedor.ProvClave)
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
                    "OK","");
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

        // FIX 2026-06-02: comando para retomar el último flete pendiente
        // guardado en Preferences. Lo invoca la pantalla principal cuando
        // detecta que hay un flete activo guardado (OnAppearing).
        //
        // Verifica primero que el flete SIGA EXISTIENDO en SQLite local,
        // porque la BD pudo haber sido limpiada o el chofer pudo haber
        // cambiado de dispositivo. Si no existe, limpia las preferences.
        [RelayCommand]
        public async Task RetomarUltimoFleteAsync()
        {
            int fleteLocalId = Preferences.Get("ultimo_flete_local_id", 0);
            if (fleteLocalId <= 0)
            {
                LimpiarUltimoFleteEnPreferences();
                return;
            }

            try
            {
                var fleteLocal = await _databaseService
                    .GetItemAsync<Tb_FlePer_FletePersonal>(fleteLocalId);

                if (fleteLocal == null)
                {
                    // La BD fue limpiada, no podemos retomar
                    LimpiarUltimoFleteEnPreferences();
                    return;
                }

                string chofer = Preferences.Get("ultimo_flete_chofer", "");
                string ruta = Preferences.Get("ultimo_flete_ruta", "");

                var confirmar = await Application.Current.MainPage.DisplayAlert(
                    "Flete en curso detectado",
                    $"Tienes un flete sin finalizar:\n\n" +
                    $"Ruta: {ruta}\n" +
                    $"Chofer: {chofer}\n\n" +
                    $"¿Deseas continuar donde lo dejaste?",
                    "Sí, continuar",
                    "No, empezar uno nuevo");

                if (confirmar)
                {
                    // Navegamos al EscaneoCodigo con el ID guardado
                    await Shell.Current.GoToAsync(
                        $"{nameof(EscaneoCodigo)}?fleteLocalId={fleteLocalId}");
                }
                else
                {
                    // El usuario quiere empezar de nuevo: limpiamos
                    // la preference y dejamos que cree un flete nuevo
                    LimpiarUltimoFleteEnPreferences();
                }
            }
            catch (Exception ex)
            {
                LimpiarUltimoFleteEnPreferences();
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"No se pudo retomar el flete anterior: {ex.Message}",
                    "OK");
            }
        }

        // FIX 2026-06-02: helper estático para limpiar las preferences
        // del último flete. Se invoca desde aquí (cuando el usuario
        // decide empezar nuevo) y desde EscaneoCodigoViewModel cuando
        // se finaliza/cancela el viaje exitosamente.
        public static void LimpiarUltimoFleteEnPreferences()
        {
            try
            {
                Preferences.Remove("ultimo_flete_local_id");
                Preferences.Remove("ultimo_flete_chofer");
                Preferences.Remove("ultimo_flete_ruta");
                Preferences.Remove("ultimo_flete_fecha");
            }
            catch { /* ignore: las prefs se limpian en próxima escritura */ }
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