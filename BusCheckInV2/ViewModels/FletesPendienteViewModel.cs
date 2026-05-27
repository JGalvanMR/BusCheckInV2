// BusCheckInV2/ViewModels/FletesPendientesViewModel.cs
// Reemplaza el archivo completo

using BusCheckInV2.Models;
using BusCheckInV2.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace BusCheckInV2.ViewModels
{
    public partial class FletesPendientesViewModel : ObservableObject, IDisposable
    {
        private readonly ISQLiteService _databaseService;
        private readonly IApiFleteService _apiService;
        private readonly IAlertService _alertService;
        private readonly INavigationService _navigationService;
        private bool _disposed;

        // ─── PROPIEDADES SIN CAMBIOS ──────────────────────────────────────

        [ObservableProperty]
        private ObservableCollection<UsuarioApi> _listaUsuarios = new();

        [ObservableProperty]
        private ObservableCollection<FletePendienteUI> _fletesPendientes = new();

        // ChoferSeleccionado permanece como string interno para las llamadas API
        // Se actualiza automáticamente cuando cambia UsuarioSeleccionado
        [ObservableProperty]
        private string _choferSeleccionado = string.Empty;

        [ObservableProperty]
        private bool _estaCargando;

        [ObservableProperty]
        private bool _mostrarSoloPendientes = true;

        [ObservableProperty]
        private string _mensajeEstado = "Seleccione un chofer";

        [ObservableProperty]
        private int _diasFiltro = 3;

        [ObservableProperty]
        private string _versionText = string.Empty;

        [ObservableProperty]
        private bool _hayConexionInternet;

        [ObservableProperty]
        private string _textoEstadoConexion = "Conectado";

        // ─── PROPIEDADES NUEVAS ───────────────────────────────────────────

        // ─── NUEVO 1: UsuarioSeleccionado ─────────────────────────────────
        // El Picker necesita un SelectedItem del mismo tipo que ItemsSource.
        // ItemsSource es ObservableCollection<UsuarioApi>, por lo tanto
        // SelectedItem debe ser UsuarioApi, no string.
        // Cuando cambia, actualizamos ChoferSeleccionado automáticamente.
        [ObservableProperty]
        private UsuarioApi _usuarioSeleccionado;

        // Este método lo genera CommunityToolkit automáticamente cuando
        // la propiedad cambia. Es el equivalente a OnPropertyChanged manual.
        partial void OnUsuarioSeleccionadoChanged(UsuarioApi value)
        {
            // Extraemos el nombre del usuario seleccionado para usarlo
            // en las llamadas a la API que esperan un string de chofer
            ChoferSeleccionado = value?.Nombre ?? string.Empty;
        }
        // ─────────────────────────────────────────────────────────────────

        // ─── NUEVO 2: ColorEstadoConexion ─────────────────────────────────
        // El XAML bindea directamente a este Color para colorear el indicador
        // de conexión. Lo mantenemos sincronizado con HayConexionInternet.
        [ObservableProperty]
        private Color _colorEstadoConexion = Colors.Orange;
        // ─────────────────────────────────────────────────────────────────

        // ─── CONSTRUCTOR SIN CAMBIOS ──────────────────────────────────────
        public FletesPendientesViewModel(
            ISQLiteService databaseService,
            IApiFleteService apiService,
            IAlertService alertService,
            INavigationService navigationService)
        {
            _databaseService = databaseService;
            _apiService = apiService;
            _alertService = alertService;
            _navigationService = navigationService;

            VersionText = $"Versión: {AppInfo.VersionString}";
            Connectivity.ConnectivityChanged += OnConnectivityChanged;
            VerificarConectividad();
        }

        // ─── CAMBIO: OnConnectivityChanged actualiza ColorEstadoConexion ──
        private void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                HayConexionInternet = e.NetworkAccess == NetworkAccess.Internet;
                TextoEstadoConexion = HayConexionInternet ? "Conectado" : "Offline";

                // Actualizamos el color junto con el texto
                ColorEstadoConexion = HayConexionInternet ? Colors.Green : Colors.Orange;
            });
        }

        // ─── CAMBIO: VerificarConectividad también actualiza Color ─────────
        private void VerificarConectividad()
        {
            HayConexionInternet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            TextoEstadoConexion = HayConexionInternet ? "Conectado" : "Offline";
            ColorEstadoConexion = HayConexionInternet ? Colors.Green : Colors.Orange;
        }

        // ─── COMANDO: Cargar choferes ─────────────────────────────────────
        // Sin cambios en la lógica, solo verificamos que el nombre coincida
        [RelayCommand]
        private async Task CargarChoferesAsync()
        {
            try
            {
                EstaCargando = true;
                var usuarios = HayConexionInternet
                    ? await _apiService.ObtenerUsuariosAsync()
                    : new System.Collections.Generic.List<UsuarioApi>();

                if (!usuarios.Any())
                {
                    var choferesLocales = await _databaseService.ObtenerChoferesUnicosAsync();
                    usuarios = choferesLocales
                        .Select(c => new UsuarioApi { Nombre = c })
                        .ToList();
                    MensajeEstado = "Usando datos locales";
                }

                ListaUsuarios.Clear();
                foreach (var usuario in usuarios.OrderBy(u => u.Nombre))
                    ListaUsuarios.Add(usuario);

                // ─── CAMBIO: Seleccionamos el primer usuario en UsuarioSeleccionado
                // (no en ChoferSeleccionado directamente)
                // OnUsuarioSeleccionadoChanged actualizará ChoferSeleccionado
                if (ListaUsuarios.Any())
                    UsuarioSeleccionado = ListaUsuarios.First();
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
            }
            finally
            {
                EstaCargando = false;
            }
        }

        // ─── COMANDO: Cargar fletes pendientes ────────────────────────────
        // Nombre del método: CargarFletesPendientesAsync
        // Genera el comando: CargarFletesPendientesCommand
        // El XAML debe usar: CargarFletesPendientesCommand
        [RelayCommand]
        private async Task CargarFletesPendientesAsync()
        {
            // ChoferSeleccionado se actualiza automáticamente desde UsuarioSeleccionado
            if (string.IsNullOrEmpty(ChoferSeleccionado))
            {
                MensajeEstado = "Seleccione un chofer primero";
                //return;
            }

            try
            {
                EstaCargando = true;
                MensajeEstado = "Cargando fletes...";
                FletesPendientes.Clear();

                var fletes = new System.Collections.Generic.List<FletePendienteUI>();

                if (HayConexionInternet && _apiService != null)
                {
                    var apiResponse = await _apiService.ObtenerFletesPorChoferAsync(
                        ChoferSeleccionado, DiasFiltro, MostrarSoloPendientes);

                    if (apiResponse?.Success == true && apiResponse.Data?.Any() == true)
                    {
                        fletes = apiResponse.Data.Select(f => new FletePendienteUI
                        {
                            IdFletePer = f.IdFletePer,
                            Ruta = f.RutaNombre,
                            FechaHora = DateTime.TryParse(
                                $"{f.Fecha} {f.Hora}", out var fecha) ? fecha : DateTime.MinValue,
                            Proveedor = f.ProveedorNombre,
                            Chofer = f.Chofer,
                            Estatus = f.Estatus,
                            CantidadEsperada = f.Cantidad,
                            TipoFlete = f.TipoFlete,
                            TipoViaje = f.TipoViaje,
                            CantidadReal = f.CantidadReal,
                            FechaInicio = f.FechaInicio,
                            FechaFin = f.FechaFin
                        }).ToList();
                    }
                    else
                    {
                        fletes = await _databaseService.ObtenerFletesPendientesDesdeCacheAsync(
                            ChoferSeleccionado, DiasFiltro);
                        MensajeEstado = $"API sin datos. Mostrando locales.";
                    }
                }
                else
                {
                    fletes = await _databaseService.ObtenerFletesPendientesDesdeCacheAsync(
                        ChoferSeleccionado, DiasFiltro);
                    MensajeEstado = "Modo offline — datos locales";
                }

                var fletesFiltrados = MostrarSoloPendientes
                    ? fletes.Where(f => f.EsPendiente).ToList()
                    : fletes;

                foreach (var flete in fletesFiltrados.OrderByDescending(f => f.FechaHora))
                    FletesPendientes.Add(flete);

                var total = FletesPendientes.Count;
                var pendientes = FletesPendientes.Count(f => f.EsPendiente);
                MensajeEstado = $"Mostrando {total} fletes ({pendientes} pendientes)";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
            }
            finally
            {
                EstaCargando = false;
            }
        }

        // ─── COMANDOS RESTANTES SIN CAMBIOS DE LÓGICA ────────────────────

        [RelayCommand]
        private async Task ValidarYFinalizarFleteAsync(FletePendienteUI flete)
        {
            if (flete == null) return;

            var cantidad = await _alertService.ShowPromptAsync(
                "Validar Flete",
                $"Ingrese cantidad real para:\n{flete.Ruta}\n(Esperados: {flete.CantidadEsperada})",
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrEmpty(cantidad) ||
                !int.TryParse(cantidad, out int cantidadValidada)) return;

            var observaciones = await _alertService.ShowPromptAsync(
                "Observaciones",
                "Ingrese observaciones (opcional):",
                placeholder: "Observaciones del viaje...");

            var confirmar = await _alertService.ShowConfirmationAsync(
                "Confirmar Finalización",
                $"¿Finalizar flete con {cantidadValidada} pasajeros?\n{flete.Ruta}");

            if (!confirmar) return;

            try
            {
                EstaCargando = true;

                var exito = HayConexionInternet && _apiService != null
                    ? await _apiService.ValidarYFinalizarFleteAsync(
                        flete.IdFletePer ?? 0, cantidadValidada, observaciones ?? "", 0, 0)
                    : true;

                if (exito)
                {
                    await _databaseService.ActualizarEstadoFleteAsync(
                        flete.Id, "Completado", cantidadValidada, observaciones);
                    flete.Estatus = "Completado";
                    flete.CantidadReal = cantidadValidada;
                    flete.FechaFin = DateTime.Now;

                    await _alertService.ShowAlertAsync(
                        "Éxito",
                        HayConexionInternet
                            ? "Flete finalizado en servidor"
                            : "Flete finalizado localmente");
                }
            }
            finally
            {
                EstaCargando = false;
            }
        }

        [RelayCommand]
        private async Task SincronizarAsync()
        {
            try
            {
                EstaCargando = true;

                if (!HayConexionInternet)
                {
                    await _alertService.ShowAlertAsync(
                        "Sin conexión",
                        "No hay conexión a internet para sincronizar");
                    return;
                }

                var sincronizados = await _databaseService.SincronizarConApiAsync(_apiService);
                await _alertService.ShowAlertAsync(
                    "Sincronización",
                    $"Completada. {sincronizados} fletes sincronizados");
            }
            catch (Exception ex)
            {
                await _alertService.ShowAlertAsync("Error", $"Error sincronizando: {ex.Message}");
            }
            finally
            {
                EstaCargando = false;
            }
        }

        [RelayCommand]
        private async Task VerDetalleFleteAsync(FletePendienteUI flete)
        {
            if (flete == null) return;

            var detalle =
                $"ID Flete: {flete.IdFletePer?.ToString() ?? "No sincronizado"}\n" +
                $"Ruta: {flete.Ruta}\n" +
                $"Fecha/Hora: {flete.FechaHora:dd/MM/yyyy HH:mm}\n" +
                $"Chofer: {flete.Chofer}\n" +
                $"Proveedor: {flete.Proveedor}\n" +
                $"Estado: {flete.Estatus}\n" +
                $"Tipo: {flete.TipoFlete} - {flete.TipoViaje}\n" +
                $"Pasajeros: {flete.CantidadEsperada} esperados, " +
                $"{flete.CantidadReal ?? 0} reales\n" +
                $"Duración: {flete.DuracionViaje}";

            await _alertService.ShowAlertAsync("Detalle del Flete", detalle);
        }

        [RelayCommand]
        private async Task VolverAsync() => await _navigationService.GoBackAsync();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;
        }
        //public override void Dispose()
        //{
        //    Connectivity.ConnectivityChanged -= OnConnectivityChanged;
        //    base.Dispose();
        //}
    }
}