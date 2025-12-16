using BusCheckInV2.Models;
using BusCheckInV2.Services;
using BusCheckInV2.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Networking;

namespace BusCheckInV2.ViewModels
{
    public partial class FletesPendientesViewModel : ObservableObject
    {
        private readonly ISQLiteService _databaseService;
        private readonly IApiFleteService _apiFleteService;
        private readonly IConnectivity _connectivity;
        private bool _permisosVerificados = false;

        [ObservableProperty]
        private ObservableCollection<UsuarioApi> _listaUsuarios = new();

        [ObservableProperty]
        private ObservableCollection<FletePendienteUI> _fletesPendientes = new();

        [ObservableProperty]
        private string _choferSeleccionado;

        [ObservableProperty]
        private bool _estaCargando;

        [ObservableProperty]
        private bool _mostrarSoloPendientes = true;

        [ObservableProperty]
        private string _mensajeEstado = "Seleccione un chofer";

        [ObservableProperty]
        private int _diasFiltro = 3;

        [ObservableProperty]
        private FletePendienteUI _fleteSeleccionado;

        [ObservableProperty]
        private string _versionText;

        [ObservableProperty]
        private bool _hayConexionInternet = true;

        [ObservableProperty]
        private Color _colorEstadoConexion = Colors.Green;

        [ObservableProperty]
        private string _textoEstadoConexion = "Conectado";

        // Comandos
        public ICommand CargarChoferesComando { get; }
        public ICommand CargarFletesComando { get; }
        public ICommand ValidarYFinalizarComando { get; }
        public ICommand CancelarFleteComando { get; }
        public ICommand ReanudarFleteComando { get; }
        public ICommand VerDetalleComando { get; }
        public ICommand SincronizarComando { get; }
        public ICommand ProbarConexionComando { get; }


        [ObservableProperty]
        private ObservableCollection<string> _listaChoferes = new();
        public ICommand SincronizarConServidorComando { get; }

        public FletesPendientesViewModel(
        ISQLiteService databaseService,
        IApiFleteService apiFleteService,
        IConnectivity connectivity)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _apiFleteService = apiFleteService;
            _connectivity = connectivity;

            // Inicializar comandos
            CargarChoferesComando = new AsyncRelayCommand(CargarChoferesAsync);
            CargarFletesComando = new AsyncRelayCommand(CargarFletesPendientesAsync);
            ValidarYFinalizarComando = new AsyncRelayCommand<FletePendienteUI>(ValidarYFinalizarFleteAsync);
            CancelarFleteComando = new AsyncRelayCommand<FletePendienteUI>(CancelarFleteAsync);
            ReanudarFleteComando = new AsyncRelayCommand<FletePendienteUI>(ReanudarFleteAsync);
            VerDetalleComando = new AsyncRelayCommand<FletePendienteUI>(VerDetalleFleteAsync);
            SincronizarComando = new AsyncRelayCommand(SincronizarAsync);
            ProbarConexionComando = new AsyncRelayCommand(ProbarConexionAsync);

            VersionText = $"Versión: {AppInfo.VersionString}";

            // Monitorear conectividad
            _connectivity.ConnectivityChanged += OnConnectivityChanged;
            VerificarConectividad();

            // Cargar choferes al iniciar
            CargarChoferesAsync().ConfigureAwait(false);
        }

        private void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                HayConexionInternet = e.NetworkAccess == NetworkAccess.Internet;
                ColorEstadoConexion = HayConexionInternet ? Colors.Green : Colors.Red;
                TextoEstadoConexion = HayConexionInternet ? "Conectado" : "Sin conexión";

                if (HayConexionInternet)
                {
                    // Si se recupera la conexión, intentar sincronizar
                    SincronizarAsync().ConfigureAwait(false);
                }
            });
        }

        private void VerificarConectividad()
        {
            HayConexionInternet = _connectivity.NetworkAccess == NetworkAccess.Internet;
            ColorEstadoConexion = HayConexionInternet ? Colors.Green : Colors.Red;
            TextoEstadoConexion = HayConexionInternet ? "Conectado" : "Sin conexión";
        }

        private async Task CargarChoferesAsync()
        {
            try
            {
                EstaCargando = true;
                await VerificarPermisosAsync();

                List<UsuarioApi> usuarios = new();

                if (HayConexionInternet && _apiFleteService != null)
                {
                    // Intentar cargar desde API
                    usuarios = await _apiFleteService.ObtenerUsuariosAsync();

                    if (!usuarios.Any())
                    {
                        // Si API no devuelve datos, usar locales
                        var choferesLocales = await _databaseService.ObtenerChoferesUnicosAsync();
                        usuarios = choferesLocales.Select(c => new UsuarioApi { Nombre = c }).ToList();
                        MensajeEstado = "Usando datos locales";
                    }
                    else
                    {
                        MensajeEstado = "Datos actualizados desde servidor";
                    }
                }
                else
                {
                    // Sin conexión, usar locales
                    var choferesLocales = await _databaseService.ObtenerChoferesUnicosAsync();
                    usuarios = choferesLocales.Select(c => new UsuarioApi { Nombre = c }).ToList();
                    MensajeEstado = "Modo offline - datos locales";
                }

                ListaUsuarios.Clear();
                foreach (var usuario in usuarios.OrderBy(u => u.Nombre))
                {
                    ListaUsuarios.Add(usuario);
                }

                if (ListaUsuarios.Any())
                {
                    ChoferSeleccionado = ListaUsuarios.First().Nombre;
                }
                else
                {
                    MensajeEstado = "No se encontraron choferes";
                }
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

        private async Task CargarFletesPendientesAsync()
        {
            if (string.IsNullOrEmpty(ChoferSeleccionado)) return;

            try
            {
                EstaCargando = true;
                MensajeEstado = "Cargando fletes...";

                FletesPendientes.Clear();

                List<FletePendienteUI> fletes = new();

                if (HayConexionInternet && _apiFleteService != null)
                {
                    // Intentar cargar desde API
                    var fletesApi = await _apiFleteService.ObtenerFletesPorUsuarioAsync(ChoferSeleccionado, DateTime.Now.AddDays(-DiasFiltro));

                    if (fletesApi.Any())
                    {
                        // Convertir FleteApi a FletePendienteUI
                        fletes = fletesApi.Select(f => new FletePendienteUI
                        {
                            Id = 0, // Temporal, se asigna desde cache
                            IdFletePer = f.IdFletePer,
                            Ruta = f.RutaNombre,
                            FechaHora = DateTime.Parse($"{f.Fecha} {f.Hora}"),
                            Proveedor = f.ProveedorNombre,
                            Chofer = f.Chofer,
                            Estatus = f.Estatus,
                            CantidadEsperada = f.Cantidad,
                            CantidadReal = f.CantidadReal,
                            TipoFlete = f.TipoFlete,
                            TipoViaje = f.TipoViaje,
                            FechaInicio = f.FechaInicio,
                            FechaFin = f.FechaFin
                        }).ToList();

                        MensajeEstado = "Datos desde servidor";
                    }
                    else
                    {
                        // Si API no devuelve datos, usar cache local
                        fletes = await _databaseService.ObtenerFletesPendientesDesdeCacheAsync(ChoferSeleccionado, DiasFiltro);
                        MensajeEstado = "Datos locales (cache)";
                    }
                }
                else
                {
                    // Sin conexión, usar cache local
                    fletes = await _databaseService.ObtenerFletesPendientesDesdeCacheAsync(ChoferSeleccionado, DiasFiltro);
                    MensajeEstado = "Modo offline - datos locales";
                }

                // Filtrar y mostrar
                var fletesFiltrados = MostrarSoloPendientes
                    ? fletes.Where(f => f.EsPendiente)
                    : fletes;

                foreach (var flete in fletesFiltrados.OrderByDescending(f => f.FechaHora))
                {
                    FletesPendientes.Add(flete);
                }

                var total = FletesPendientes.Count;
                var pendientes = FletesPendientes.Count(f => f.EsPendiente);
                MensajeEstado += $"\nMostrando {total} fletes ({pendientes} pendientes)";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
                FletesPendientes.Clear();
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task ValidarYFinalizarFleteAsync(FletePendienteUI flete)
        {
            if (flete == null) return;

            try
            {
                // Pedir cantidad real
                var cantidad = await Application.Current.MainPage.DisplayPromptAsync(
                    "Validar Flete",
                    $"Ingrese la cantidad real de pasajeros para:\n{flete.Ruta}\n(Esperados: {flete.CantidadEsperada})",
                    "Finalizar",
                    "Cancelar",
                    flete.CantidadEsperada?.ToString() ?? "0",
                    Keyboard.Numeric,
                    -1);

                if (string.IsNullOrEmpty(cantidad) || !int.TryParse(cantidad, out int cantidadValidada))
                    return;

                // Pedir observaciones
                var observaciones = await Application.Current.MainPage.DisplayPromptAsync(
                    "Observaciones",
                    "Ingrese observaciones (opcional):",
                    "Continuar",
                    "Saltar",
                    "",
                    -1,
                    "Observaciones del viaje...");

                // Obtener ubicación
                Location location = null;
                try
                {
                    location = await Geolocation.GetLocationAsync();
                }
                catch (Exception locEx)
                {
                    Console.WriteLine($"Error ubicación: {locEx.Message}");
                }

                // Confirmar
                var confirmar = await Application.Current.MainPage.DisplayAlert(
                    "Confirmar Finalización",
                    $"¿Finalizar flete con {cantidadValidada} pasajeros?\n{flete.Ruta}",
                    "Sí, Finalizar",
                    "Cancelar");

                if (!confirmar) return;

                EstaCargando = true;

                bool exito = false;

                if (HayConexionInternet && _apiFleteService != null)
                {
                    // Intentar con API
                    exito = await _apiFleteService.ValidarYFinalizarFleteAsync(
                        flete.IdFletePer ?? 0,
                        cantidadValidada,
                        observaciones ?? "",
                        location?.Latitude ?? 0,
                        location?.Longitude ?? 0);

                    if (exito)
                    {
                        // Actualizar localmente
                        await _databaseService.ActualizarEstadoFleteAsync(
                            flete.Id,
                            "Completado",
                            cantidadValidada,
                            observaciones);
                    }
                }
                else
                {
                    // Solo actualizar localmente (se sincronizará después)
                    exito = await _databaseService.ActualizarEstadoFleteAsync(
                        flete.Id,
                        "Completado",
                        cantidadValidada,
                        observaciones);

                    if (exito)
                    {
                        // Insertar detalle local
                        if (location != null)
                        {
                            await _databaseService.InsertarDetalleFleteAsync(
                                flete.Id,
                                9999,
                                location.Latitude,
                                location.Longitude,
                                $"Fin - {flete.Chofer}");
                        }
                    }
                }

                if (exito)
                {
                    // Actualizar UI
                    flete.Estatus = "Completado";
                    flete.CantidadReal = cantidadValidada;
                    flete.FechaFin = DateTime.Now;

                    await Application.Current.MainPage.DisplayAlert(
                        "Éxito",
                        HayConexionInternet ? "Flete finalizado en servidor" : "Flete finalizado localmente (se sincronizará después)",
                        "OK");

                    await CargarFletesPendientesAsync();
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Error",
                        "No se pudo finalizar el flete",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Error: {ex.Message}",
                    "OK");
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task SincronizarAsync()
        {
            try
            {
                EstaCargando = true;
                MensajeEstado = "Sincronizando...";

                if (!HayConexionInternet)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Sin conexión",
                        "No hay conexión a internet para sincronizar",
                        "OK");
                    return;
                }

                if (_apiFleteService == null)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Error",
                        "Servicio API no disponible",
                        "OK");
                    return;
                }

                // 1. Sincronizar fletes locales hacia el servidor
                var sincronizados = await _databaseService.SincronizarConApiAsync(_apiFleteService);

                // 2. Actualizar datos desde servidor
                if (!string.IsNullOrEmpty(ChoferSeleccionado))
                {
                    await CargarFletesPendientesAsync();
                }

                await Application.Current.MainPage.DisplayAlert(
                    "Sincronización",
                    $"Completada. {sincronizados} fletes sincronizados",
                    "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Error sincronizando: {ex.Message}",
                    "OK");
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task ProbarConexionAsync()
        {
            try
            {
                EstaCargando = true;

                if (!HayConexionInternet)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Sin conexión",
                        "No hay conexión a internet",
                        "OK");
                    return;
                }

                if (_apiFleteService == null)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Error",
                        "Servicio API no configurado",
                        "OK");
                    return;
                }

                var resultado = await _apiFleteService.VerificarConexionAsync();

                await Application.Current.MainPage.DisplayAlert(
                    "Prueba de conexión",
                    resultado ? "✅ Conexión API exitosa" : "❌ Error conectando a API",
                    "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Error: {ex.Message}",
                    "OK");
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task CancelarFleteAsync(FletePendienteUI flete)
        {
            if (flete == null) return;

            try
            {
                // Mostrar opciones de cancelación
                var motivo = await Application.Current.MainPage.DisplayActionSheet(
                    $"Cancelar flete: {flete.Ruta}",
                    "Volver",
                    null,
                    "Error en aplicación",
                    "Falla de conexión",
                    "Usuario olvidó finalizar",
                    "Cambio de ruta",
                    "Problema mecánico",
                    "Otro motivo");

                if (motivo == "Volver") return;

                // Si seleccionó "Otro motivo", pedir texto - CORREGIDO
                if (motivo == "Otro motivo")
                {
                    motivo = await Application.Current.MainPage.DisplayPromptAsync(
                        title: "Motivo de cancelación",
                        message: "Especifique el motivo:",
                        accept: "Cancelar",
                        cancel: "Volver",
                        placeholder: "Motivo específico...",
                        keyboard: Keyboard.Default,
                        maxLength: -1);

                    if (string.IsNullOrEmpty(motivo)) return;
                }

                // Confirmar
                var confirmar = await Application.Current.MainPage.DisplayAlert(
                    "Confirmar Cancelación",
                    $"¿Cancelar flete?\nMotivo: {motivo}",
                    "Sí, Cancelar",
                    "No");

                if (!confirmar) return;

                EstaCargando = true;

                // Actualizar en base de datos local
                var exito = await _databaseService.ActualizarEstadoFleteAsync(
                    flete.Id,
                    "Cancelado",
                    null,
                    $"Motivo: {motivo}");

                if (exito)
                {
                    // Actualizar en la API si está disponible
                    if (_apiFleteService != null && flete.IdFletePer.HasValue)
                    {
                        try
                        {
                            // Aquí necesitarías implementar un método para cancelar en la API
                            // Por ahora solo marcamos como no sincronizado
                        }
                        catch (Exception apiEx)
                        {
                            Console.WriteLine($"Error al sincronizar con API: {apiEx.Message}");
                        }
                    }

                    // Actualizar UI
                    flete.Estatus = "Cancelado";

                    await Application.Current.MainPage.DisplayAlert(
                        "Flete Cancelado",
                        "El flete ha sido cancelado",
                        "OK");

                    await CargarFletesPendientesAsync();
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Error",
                        "No se pudo cancelar el flete",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Error al cancelar flete: {ex.Message}",
                    "OK");
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task ReanudarFleteAsync(FletePendienteUI flete)
        {
            if (flete == null) return;

            try
            {
                var confirmar = await Application.Current.MainPage.DisplayAlert(
                    "Reanudar Flete",
                    $"¿Reanudar flete inconcluso?\n{flete.Ruta}",
                    "Sí, Reanudar",
                    "No");

                if (!confirmar) return;

                EstaCargando = true;

                // Actualizar estado a "Iniciado"
                var exito = await _databaseService.ActualizarEstadoFleteAsync(
                    flete.Id,
                    "Iniciado");

                if (exito)
                {
                    // Insertar detalle de reanudación
                    try
                    {
                        var location = await Geolocation.GetLocationAsync();
                        if (location != null)
                        {
                            await _databaseService.InsertarDetalleFleteAsync(
                                flete.Id,
                                0, // Código para inicio/reanudación
                                location.Latitude,
                                location.Longitude,
                                $"Reanudado - {flete.Chofer}");
                        }
                    }
                    catch (Exception locEx)
                    {
                        Console.WriteLine($"Error al obtener ubicación: {locEx.Message}");
                    }

                    flete.Estatus = "Iniciado";
                    flete.FechaInicio = DateTime.Now;

                    await Application.Current.MainPage.DisplayAlert(
                        "Flete Reanudado",
                        "El flete ha sido reanudado",
                        "Continuar");

                    // Navegar a pantalla de escaneo
                    await Shell.Current.GoToAsync(nameof(EscaneoCodigo));
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Error al reanudar flete: {ex.Message}",
                    "OK");
            }
            finally
            {
                EstaCargando = false;
            }
        }

        private async Task VerDetalleFleteAsync(FletePendienteUI flete)
        {
            if (flete == null) return;

            var detalle = $"ID Flete: {flete.IdFletePer?.ToString() ?? "No sincronizado"}\n" +
                         $"Ruta: {flete.Ruta}\n" +
                         $"Fecha/Hora: {flete.FechaHora:dd/MM/yyyy HH:mm}\n" +
                         $"Chofer: {flete.Chofer}\n" +
                         $"Proveedor: {flete.Proveedor}\n" +
                         $"Estado: {flete.Estatus}\n" +
                         $"Tipo: {flete.TipoFlete} - {flete.TipoViaje}\n" +
                         $"Pasajeros esperados: {flete.CantidadEsperada}\n" +
                         $"Pasajeros reales: {flete.CantidadReal?.ToString() ?? "No validado"}\n" +
                         $"Duración: {flete.DuracionViaje}";

            await Application.Current.MainPage.DisplayAlert(
                "Detalle del Flete",
                detalle,
                "Cerrar");
        }

        private async Task SincronizarConServidorAsync()
        {
            try
            {
                EstaCargando = true;
                MensajeEstado = "Sincronizando con servidor...";

                // Aquí podrías llamar a tu SyncService
                // Por ahora solo refrescamos
                await Task.Delay(1000);

                await CargarFletesPendientesAsync();

                await Application.Current.MainPage.DisplayAlert(
                    "Sincronización",
                    "Datos actualizados correctamente",
                    "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Error de sincronización: {ex.Message}",
                    "OK");
            }
            finally
            {
                EstaCargando = false;
            }
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

        partial void OnChoferSeleccionadoChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                CargarFletesPendientesAsync().ConfigureAwait(false);
            }
        }

        partial void OnMostrarSoloPendientesChanged(bool value)
        {
            CargarFletesPendientesAsync().ConfigureAwait(false);
        }

        partial void OnDiasFiltroChanged(int value)
        {
            CargarFletesPendientesAsync().ConfigureAwait(false);
        }
        // Agregar este comando en la clase FletesPendientesViewModel:

        [RelayCommand]
        private async Task Volver()
        {
            await Shell.Current.GoToAsync(".."); // Regresar a la página anterior
                                                 // O usar: await Shell.Current.GoToAsync(nameof(SeleccionDeFlete));
        }
    }
}