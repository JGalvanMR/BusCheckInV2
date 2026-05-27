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

        [ObservableProperty]
        private ObservableCollection<UsuarioApi> _listaUsuarios = new();

        [ObservableProperty]
        private ObservableCollection<FletePendienteUI> _fletesPendientes = new();

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

        [ObservableProperty]
        private UsuarioApi _usuarioSeleccionado;

        [ObservableProperty]
        private Color _colorEstadoConexion = Colors.Orange;

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

        private void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                bool teniaConexion = HayConexionInternet;
                HayConexionInternet = e.NetworkAccess == NetworkAccess.Internet;
                TextoEstadoConexion = HayConexionInternet ? "Conectado" : "Offline";
                ColorEstadoConexion = HayConexionInternet ? Colors.Green : Colors.Orange;

                // RECUPERADO DE LA VIEJA: Sincronizar automático al recuperar internet
                if (!teniaConexion && HayConexionInternet && FletesPendientes.Any())
                {
                    await SincronizarAsync();
                }
            });
        }

        private void VerificarConectividad()
        {
            HayConexionInternet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            TextoEstadoConexion = HayConexionInternet ? "Conectado" : "Offline";
            ColorEstadoConexion = HayConexionInternet ? Colors.Green : Colors.Orange;
        }

        partial void OnUsuarioSeleccionadoChanged(UsuarioApi value)
        {
            ChoferSeleccionado = value?.Nombre ?? string.Empty;
        }

        // RECUPERADO DE LA VIEJA: Auto-recarga al cambiar chofer
        partial void OnChoferSeleccionadoChanged(string value)
        {
            if (!string.IsNullOrEmpty(value) && ListaUsuarios.Any())
            {
                CargarFletesPendientesCommand.Execute(null);
            }
        }

        // RECUPERADO DE LA VIEJA: Auto-recarga al cambiar switch
        partial void OnMostrarSoloPendientesChanged(bool value)
        {
            if (!string.IsNullOrEmpty(ChoferSeleccionado))
            {
                CargarFletesPendientesCommand.Execute(null);
            }
        }

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
                    usuarios = choferesLocales.Select(c => new UsuarioApi { Nombre = c }).ToList();
                    MensajeEstado = "Usando datos locales";
                }

                ListaUsuarios.Clear();
                foreach (var usuario in usuarios.OrderBy(u => u.Nombre))
                    ListaUsuarios.Add(usuario);

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

        [RelayCommand]
        private async Task CargarFletesPendientesAsync()
        {
            if (string.IsNullOrEmpty(ChoferSeleccionado)) return;

            try
            {
                EstaCargando = true;
                MensajeEstado = "Cargando fletes...";
                FletesPendientes.Clear();

                var fletes = new System.Collections.Generic.List<FletePendienteUI>();

                if (HayConexionInternet && _apiService != null)
                {
                    var apiResponse = await _apiService.ObtenerFletesPorChoferAsync(ChoferSeleccionado, DiasFiltro, MostrarSoloPendientes);

                    if (apiResponse?.Success == true && apiResponse.Data?.Any() == true)
                    {
                        fletes = apiResponse.Data.Select(f => new FletePendienteUI
                        {
                            IdFletePer = f.IdFletePer,
                            Ruta = f.RutaNombre,
                            FechaHora = DateTime.TryParse($"{f.Fecha} {f.Hora}", out var fecha) ? fecha : DateTime.MinValue,
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
                        fletes = await _databaseService.ObtenerFletesPendientesDesdeCacheAsync(ChoferSeleccionado, DiasFiltro);
                        MensajeEstado = $"API sin datos. Mostrando locales.";
                    }
                }
                else
                {
                    fletes = await _databaseService.ObtenerFletesPendientesDesdeCacheAsync(ChoferSeleccionado, DiasFiltro);
                    MensajeEstado = "Modo offline — datos locales";
                }

                var fletesFiltrados = MostrarSoloPendientes ? fletes.Where(f => f.EsPendiente).ToList() : fletes;

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

        [RelayCommand]
        private async Task ValidarYFinalizarFleteAsync(FletePendienteUI flete)
        {
            if (flete == null) return;

            var cantidad = await _alertService.ShowPromptAsync("Validar Flete", $"Ingrese cantidad real para:\n{flete.Ruta}\n(Esperados: {flete.CantidadEsperada})", keyboard: Keyboard.Numeric);
            if (string.IsNullOrEmpty(cantidad) || !int.TryParse(cantidad, out int cantidadValidada)) return;

            var observaciones = await _alertService.ShowPromptAsync("Observaciones", "Ingrese observaciones (opcional):", placeholder: "Observaciones del viaje...");

            var confirmar = await _alertService.ShowConfirmationAsync("Confirmar Finalización", $"¿Finalizar flete con {cantidadValidada} pasajeros?\n{flete.Ruta}");
            if (!confirmar) return;

            try
            {
                EstaCargando = true;

                // RECUPERADO DE LA VIEJA: Obtener ubicación real
                double latitud = 0, longitud = 0;
                try
                {
                    var location = await Geolocation.GetLocationAsync();
                    if (location != null)
                    {
                        latitud = location.Latitude;
                        longitud = location.Longitude;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error GPS: {ex.Message}");
                }

                bool exito = false;
                if (HayConexionInternet && _apiService != null)
                {
                    exito = await _apiService.ValidarYFinalizarFleteAsync(flete.IdFletePer ?? 0, cantidadValidada, observaciones ?? "", latitud, longitud);
                }

                if (exito || !HayConexionInternet)
                {
                    await _databaseService.ActualizarEstadoFleteAsync(flete.Id, "Completado", cantidadValidada, observaciones);
                    flete.Estatus = "Completado";
                    flete.CantidadReal = cantidadValidada;
                    flete.FechaFin = DateTime.Now;

                    if (!exito) // Si está offline, guardar detalle localmente
                    {
                        await _databaseService.InsertarDetalleFleteAsync(flete.Id, 9999, latitud, longitud, $"Fin - {flete.Chofer}");
                    }

                    await _alertService.ShowAlertAsync("Éxito", HayConexionInternet ? "Flete finalizado en servidor" : "Flete finalizado localmente (sincronizará después)");

                    // Refrescar lista
                    await CargarFletesPendientesAsync();
                }
                else
                {
                    await _alertService.ShowAlertAsync("Error", "No se pudo finalizar el flete en el servidor");
                }
            }
            finally
            {
                EstaCargando = false;
            }
        }

        // RECUPERADO DE LA VIEJA: Flujo de Cancelar Flete
        [RelayCommand]
        private async Task CancelarFleteAsync(FletePendienteUI flete)
        {
            if (flete == null) return;

            var motivo = await _alertService.ShowActionSheetAsync($"Cancelar flete: {flete.Ruta}", "Volver", null, "Error en aplicación", "Falla de conexión", "Usuario olvidó finalizar", "Cambio de ruta", "Otro motivo");

            if (motivo == "Volver") return;

            if (motivo == "Otro motivo")
            {
                motivo = await _alertService.ShowPromptAsync("Motivo de cancelación", "Especifique el motivo:", placeholder: "Motivo específico...");
                if (string.IsNullOrEmpty(motivo)) return;
            }

            var confirmar = await _alertService.ShowConfirmationAsync("Confirmar Cancelación", $"¿Cancelar flete?\nMotivo: {motivo}");
            if (!confirmar) return;

            try
            {
                EstaCargando = true;

                var exitoLocal = await _databaseService.ActualizarEstadoFleteAsync(flete.Id, "Cancelado", null, $"Motivo: {motivo}");

                if (exitoLocal)
                {
                    flete.Estatus = "Cancelado";

                    if (HayConexionInternet && _apiService != null && flete.IdFletePer.HasValue)
                    {
                        await _apiService.CancelarFleteAsync(flete.IdFletePer.Value, motivo, "Cancelado desde la App");
                    }

                    await _alertService.ShowAlertAsync("Flete Cancelado", "El flete ha sido cancelado");
                    await CargarFletesPendientesAsync();
                }
            }
            finally
            {
                EstaCargando = false;
            }
        }

        // RECUPERADO DE LA VIEJA: Flujo de Reanudar Flete
        [RelayCommand]
        private async Task ReanudarFleteAsync(FletePendienteUI flete)
        {
            if (flete == null) return;

            var confirmar = await _alertService.ShowConfirmationAsync("Reanudar Flete", $"¿Reanudar flete inconcluso?\n{flete.Ruta}");
            if (!confirmar) return;

            try
            {
                EstaCargando = true;

                double latitud = 0, longitud = 0;
                try { var loc = await Geolocation.GetLocationAsync(); if (loc != null) { latitud = loc.Latitude; longitud = loc.Longitude; } } catch { }

                bool exito = false;
                if (HayConexionInternet && _apiService != null)
                {
                    exito = await _apiService.ReanudarFleteAsync(flete.IdFletePer ?? 0, latitud, longitud);
                }

                if (exito || !HayConexionInternet)
                {
                    await _databaseService.ActualizarEstadoFleteAsync(flete.Id, "Iniciado", null, "Reanudado");
                    if (!exito) await _databaseService.InsertarDetalleFleteAsync(flete.Id, 0, latitud, longitud, $"Reanudado - {flete.Chofer}");

                    flete.Estatus = "Iniciado";
                    await _alertService.ShowAlertAsync("Flete Reanudado", "El flete ha sido reanudado. Diríjase a la pantalla de escaneo.");

                    // Navegar a escaneo (Asegúrate de tener la ruta registrada en AppShell.xaml.cs)
                    await _navigationService.GoToAsync("//EscaneoCodigo");
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
                    await _alertService.ShowAlertAsync("Sin conexión", "No hay conexión a internet para sincronizar");
                    return;
                }

                var sincronizados = await _databaseService.SincronizarConApiAsync(_apiService);
                await _alertService.ShowAlertAsync("Sincronización", $"Completada. {sincronizados} fletes sincronizados");

                if (!string.IsNullOrEmpty(ChoferSeleccionado))
                    await CargarFletesPendientesAsync();
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
            var detalle = $"ID Flete: {flete.IdFletePer?.ToString() ?? "No sincronizado"}\nRuta: {flete.Ruta}\nFecha/Hora: {flete.FechaHora:dd/MM/yyyy HH:mm}\nChofer: {flete.Chofer}\nEstado: {flete.Estatus}\nPasajeros: {flete.CantidadEsperada} esperados, {flete.CantidadReal ?? 0} reales";
            await _alertService.ShowAlertAsync("Detalle del Flete", detalle);
        }

        [RelayCommand]
        private async Task VolverAsync() => await _navigationService.GoBackAsync();

        [RelayCommand]
        private async Task ProbarConexionAsync()
        {
            try
            {
                EstaCargando = true;
                if (!HayConexionInternet)
                {
                    await _alertService.ShowAlertAsync("Sin conexión", "No hay conexión a internet");
                    return;
                }

                var resultado = await _apiService.VerificarConexionAsync();
                await _alertService.ShowAlertAsync("Prueba de conexión", resultado ? "✅ Conexión API exitosa" : "❌ Error conectando a API");
            }
            finally
            {
                EstaCargando = false;
            }
        }

        public void Dispose()
        {
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;
        }
    }
}