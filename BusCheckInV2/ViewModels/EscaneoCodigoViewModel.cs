using BarcodeScanning;
using BusCheckInV2.Models;
using BusCheckInV2.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Networking;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BusCheckInV2.ViewModels
{
    public partial class EscaneoCodigoViewModel : BaseViewModel, IQueryAttributable, IDisposable
    {
        private readonly ISQLiteService _databaseService;
        private readonly IApiFleteService _apiService;
        private readonly IAlertService _alertService;
        private readonly INavigationService _navigationService;
        private readonly IAudioService _audioService;

        private readonly SemaphoreSlim _syncSemaphore = new(1, 1);
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        // ── Propiedades de UI con Binding ───────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<Tb_FlePer_DetFlete> _pasajeros = new();

        [ObservableProperty]
        private string _totalPasajerosText = "Pasajeros: 0";

        [ObservableProperty]
        private string _textoBotonEscaneo = "Escanear";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanScan))]
        private bool _isSyncing;

        [ObservableProperty]
        private string _syncStatus = "Pendiente";

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private bool _isTorchOn;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isProcessingBarcode;

        // Si está sincronizando, apaga la cámara para liberar recursos físicos
        public bool CanScan => !IsSyncing;

        // ── Estado interno ─────────────────────────────────────────────────
        private int _fleteLocalId;
        private long _serverIdFletePer;
        private bool _isSyncInProgress;
        private bool _initialized;
        private bool _inicioRegistrado;

        private const string PatronNumeros = @"^[0-9]+$";
        private const int SyncIntervalSegundos = 15;

        // ── Navegación y Parámetros ────────────────────────────────────────
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("fleteLocalId", out var val) &&
                int.TryParse(val?.ToString(), out int id) && id > 0)
                _fleteLocalId = id;
        }

        public EscaneoCodigoViewModel(
            ISQLiteService databaseService,
            IApiFleteService apiService,
            IAlertService alertService,
            INavigationService navigationService,
            IAudioService audioService)
        {
            _databaseService = databaseService;
            _apiService = apiService;
            _alertService = alertService;
            _navigationService = navigationService;
            _audioService = audioService;
        }

        // ── Inicialización de la Pantalla ──────────────────────────────────
        public async Task InitializeAsync()
        {
            if (_initialized) return;
            _initialized = true;

            if (_fleteLocalId == 0)
            {
                await _alertService.ShowAlertAsync("Error", "Flete inválido. Regresa e intenta de nuevo.");
                return;
            }

            // Monitoreo de Red
            Connectivity.ConnectivityChanged += OnConnectivityChanged;
            IsConnected = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

            // Carga de Datos SQLite
            await CargarPasajerosExistentesAsync();
            await InsertarInicioSiNecesarioAsync();
            ActualizarTotalPasajeros();

            // Sincronización Inicial Activa si hay red
            if (IsConnected)
                _ = Task.Run(TrySyncDataAsync);

            // Arrancar el Demonio en segundo plano
            StartSyncService();
        }

        private void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            IsConnected = e.NetworkAccess == NetworkAccess.Internet;
            if (IsConnected)
                _ = Task.Run(TrySyncDataAsync);
        }

        private async Task CargarPasajerosExistentesAsync()
        {
            var flete = await _databaseService.GetItemAsync<Tb_FlePer_FletePersonal>(_fleteLocalId);
            if (flete?.IdFletePer > 0)
                _serverIdFletePer = flete.IdFletePer.Value;

            var todosDetalles = await _databaseService.GetItemsAsync<Tb_FlePer_DetFlete>();

            var deEsteViaje = todosDetalles
                .Where(d => d.FleteLocalId == _fleteLocalId ||
                            (d.FleteLocalId == 0 && (d.IdFletePer == (long)_fleteLocalId ||
                             (_serverIdFletePer > 0 && d.IdFletePer == _serverIdFletePer))))
                .OrderBy(d => d.Fecha)
                .ToList();

            // Migración de datos (One-shot)
            foreach (var d in deEsteViaje.Where(d => d.FleteLocalId == 0))
            {
                d.FleteLocalId = _fleteLocalId;
                await _databaseService.UpdateAsync(d);
            }

            Pasajeros.Clear();
            foreach (var d in deEsteViaje)
                Pasajeros.Add(d);

            if (deEsteViaje.Any(d => d.CveNomina == 0))
                _inicioRegistrado = true;
        }

        private async Task InsertarInicioSiNecesarioAsync()
        {
            if (_inicioRegistrado) return;

            var location = await GetCurrentLocationAsync();
            var inicio = new Tb_FlePer_DetFlete
            {
                FleteLocalId = _fleteLocalId,
                IdFletePer = _serverIdFletePer > 0 ? _serverIdFletePer : (long)_fleteLocalId,
                CveNomina = 0,
                Latitud = location?.Latitude ?? 0,
                Longitud = location?.Longitude ?? 0,
                Fecha = DateTime.Now,
                Nombre = "INICIO",
                IsSynced = false
            };

            await _databaseService.InsertAsync(inicio);
            Pasajeros.Insert(0, inicio);
            _inicioRegistrado = true;
        }

        // ── Comandos del Escáner (BarcodeScanning V3 Nativo) ──────────────────
        [RelayCommand]
        private void ToggleTorch() => IsTorchOn = !IsTorchOn;

        [RelayCommand]
        private void ToggleScan()
        {
            IsScanning = !IsScanning;
            if (!IsScanning) IsTorchOn = false; // Apagar flash por cortesía
            TextoBotonEscaneo = IsScanning ? "Detener" : "Escanear";
        }

        [RelayCommand]
        public async Task ProcessBarcodeResultsAsync(HashSet<BarcodeResult> barcodeResults)
        {
            // Modificado para aceptar HashSet según tu código fuente
            if (barcodeResults == null || barcodeResults.Count == 0 || _isProcessingBarcode)
                return;

            try
            {
                _isProcessingBarcode = true;
                IsScanning = false; // Detiene la lectura reactiva en UI

                // Se usa .First() del namespace System.Linq
                var valor = barcodeResults.First().RawValue?.Trim();
                if (!string.IsNullOrWhiteSpace(valor))
                    await ProcessBarcodeValueAsync(valor);
            }
            finally
            {
                _isProcessingBarcode = false;

                // Si el botón sigue en modo "Detener", reanudamos el escáner
                if (TextoBotonEscaneo == "Detener")
                {
                    IsScanning = true;
                }
            }
        }

        private async Task ProcessBarcodeValueAsync(string barcodeValue)
        {
            if (!ValidarCodigo(barcodeValue, out int nomina)) return;

            // FIX 2026-06-01 (Problema #1 del usuario):
            // Antes, si _databaseService.InsertAsync lanzaba una excepción
            // (modelo desincronizado con la tabla, columna faltante, FK
            // violada, etc.), la UI seguía actualizándose porque el
            // Pasajeros.Add quedaba después del throw. Resultado: el chofer
            // veía el pasajero "registrado" pero la fila JAMÁS llegaba a
            // SQLite. Ahora:
            //   1) envolvemos el InsertAsync en try/catch con log a Consola
            //   2) mostramos alerta al usuario para que sepa que NO se guardó
            //   3) si falla, NO actualizamos la UI (no engañamos al chofer)
            //   4) aislamos GPS y audio en bloques try/catch separados para
            //      que un fallo de hardware no impida la inserción.

            Location? location = null;
            try { location = await GetCurrentLocationAsync(); }
            catch { location = null; /* GPS best-effort */ }

            try { await _audioService.PlayBeepAsync(); }
            catch { /* audio best-effort */ }

            var registro = new Tb_FlePer_DetFlete
            {
                FleteLocalId = _fleteLocalId,
                IdFletePer = _serverIdFletePer > 0 ? _serverIdFletePer : (long)_fleteLocalId,
                CveNomina = nomina,
                Latitud = location?.Latitude ?? 0,
                Longitud = location?.Longitude ?? 0,
                Fecha = DateTime.Now,
                Nombre = null,
                IsSynced = false
            };

            try
            {
                await _databaseService.InsertAsync(registro);

                // Verificación de lectura: si el wrapper InsertAsync no
                // garantiza persistencia, releer para confirmar. Algunos
                // wrappers de sqlite-net devuelven el Id autogen; otros
                // solo garantizan que la operación se enqueó.
                var verificacion = await _databaseService
                    .GetItemsAsync<Tb_FlePer_DetFlete>();
                var existe = verificacion.Any(d =>
                    d.FleteLocalId == _fleteLocalId &&
                    d.CveNomina == nomina &&
                    d.Fecha == registro.Fecha);

                if (!existe)
                {
                    Console.WriteLine(
                        $"[Scan] ADVERTENCIA: InsertAsync regresó OK pero " +
                        $"el registro {nomina} no aparece al releer. " +
                        $"Posible problema del wrapper ISQLiteService.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Scan] ERROR insertando pasajero {nomina}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                try
                {
                    await _alertService.ShowAlertAsync(
                        "Error de guardado",
                        $"No se pudo guardar el pasajero con nómina {nomina} " +
                        $"en la base de datos local. EL REGISTRO NO FUE GUARDADO.\n\n" +
                        $"Vuelve a escanearlo. Si el problema persiste, anota la " +
                        $"nómina y repórtalo.\n\nDetalle técnico: {ex.Message}");
                }
                catch { /* si ni la alerta funciona, al menos quedó en consola */ }
                return; // crítico: NO actualizar UI si no se guardó
            }

            // Solo llegamos aquí si la inserción fue exitosa
            var idxFin = -1;
            for (int i = 0; i < Pasajeros.Count; i++)
                if (Pasajeros[i].CveNomina == 9999) { idxFin = i; break; }

            if (idxFin >= 0) Pasajeros.Insert(idxFin, registro);
            else Pasajeros.Add(registro);

            ActualizarTotalPasajeros();

            if (IsConnected) _ = Task.Run(TrySyncDataAsync);
            else SyncStatus = "Pendiente (Offline)";
        }

        private bool ValidarCodigo(string valor, out int nomina)
        {
            nomina = 0;
            if (string.IsNullOrWhiteSpace(valor) ||
                !Regex.IsMatch(valor, PatronNumeros) ||
                !int.TryParse(valor, out nomina))
            {
                _audioService.PlayErrorAsync().FireAndForgetSafeAsync(_alertService);
                _alertService.ShowAlertAsync("Error", "Código inválido").FireAndForgetSafeAsync(_alertService);
                return false;
            }

            int nominaCapturada = nomina;
            if (Pasajeros.Any(p => p.CveNomina == nominaCapturada && p.Nombre == null))
            {
                _audioService.PlayErrorAsync().FireAndForgetSafeAsync(_alertService);
                _alertService.ShowAlertAsync("Advertencia", "Pasajero ya registrado").FireAndForgetSafeAsync(_alertService);
                return false;
            }

            return true;
        }

        // ── Lógica de Sincronización Remota ──────────────────────────────────

        /// <summary>
        /// FIX 2026-06-01 (Problema #2 del usuario): cuenta los registros
        /// pendientes de sincronización para que la vista pueda bloquear
        /// la salida del usuario hasta que todo esté en el servidor.
        /// Cuenta tanto el flete padre (si nunca se subió) como los
        /// detalles (pasajeros) con IsSynced = false.
        /// </summary>
        public async Task<int> ContarPendientesSyncAsync()
        {
            try
            {
                if (_fleteLocalId == 0) return 0;

                int count = 0;
                var flete = await _databaseService
                    .GetItemAsync<Tb_FlePer_FletePersonal>(_fleteLocalId);
                if (flete != null && !flete.IsSynced) count++;

                var detalles = await _databaseService
                    .GetItemsAsync<Tb_FlePer_DetFlete>();
                count += detalles.Count(d =>
                    d.FleteLocalId == _fleteLocalId && !d.IsSynced);

                return count;
            }
            catch
            {
                return 0; // si falla, no bloquear al usuario por error de lectura
            }
        }

        /// <summary>
        /// FIX 2026-06-01 (Problema #2 del usuario): expone un intento de
        /// sincronización para que la vista lo dispare cuando el usuario
        /// elige "Reintentar sincronización" en el diálogo de salida.
        /// </summary>
        public async Task IntentarSincronizarAsync()
        {
            try
            {
                await TrySyncDataAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SyncManual] {ex.Message}");
            }
        }

        private async Task<bool> TienePendientesAsync()
        {
            try
            {
                var flete = await _databaseService.GetItemAsync<Tb_FlePer_FletePersonal>(_fleteLocalId);
                if (flete == null) return false;
                if (!flete.IsSynced) return true;

                var detalles = await _databaseService.GetItemsAsync<Tb_FlePer_DetFlete>();
                return detalles.Any(d => d.FleteLocalId == _fleteLocalId && !d.IsSynced);
            }
            catch { return false; }
        }

        private async Task TrySyncDataAsync()
        {
            if (_isSyncInProgress || !IsConnected) return;

            await _syncSemaphore.WaitAsync();
            try
            {
                _isSyncInProgress = true;
                await MainThread.InvokeOnMainThreadAsync(() => IsSyncing = true);

                await SincronizarFletePadreAsync();
                await SincronizarDetallesAsync();

                await MainThread.InvokeOnMainThreadAsync(() => SyncStatus = "Sincronizado ✓");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sync] Error: {ex.Message}");
                await MainThread.InvokeOnMainThreadAsync(() => SyncStatus = "Pendiente");
            }
            finally
            {
                _isSyncInProgress = false;
                await MainThread.InvokeOnMainThreadAsync(() => IsSyncing = false);
                _syncSemaphore.Release();
            }
        }

        private async Task SincronizarFletePadreAsync()
        {
            if (_fleteLocalId == 0) return;

            var fleteLocal = await _databaseService.GetItemAsync<Tb_FlePer_FletePersonal>(_fleteLocalId);
            if (fleteLocal == null) return;

            if (fleteLocal.IsSynced && fleteLocal.IdFletePer > 0)
            {
                _serverIdFletePer = fleteLocal.IdFletePer.Value;
                return;
            }

            var request = new FletePersonalRequest
            {
                Fecha = fleteLocal.Fecha?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Hora = fleteLocal.Hora?.ToString(@"hh\:mm\:ss") ?? DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss"),
                ClaveProveedor = fleteLocal.ProvClave ?? string.Empty,
                IdDestFlete = (int)(fleteLocal.IdDestFlete ?? 0),
                TipoFlete = fleteLocal.TipoFlete ?? "NORMAL",
                TipoViaje = fleteLocal.TipoViaje ?? "TRAER GENTE",
                Cantidad = fleteLocal.Cantidad ?? 0,
                Estatus = fleteLocal.Status ?? "P",
                Chofer = fleteLocal.Chofer ?? string.Empty
            };

            var serverIdFletePer = await _apiService.InsertarFletePersonal(request);
            if (serverIdFletePer <= 0)
                throw new Exception("El servidor rechazó el flete padre.");

            fleteLocal.IdFletePer = serverIdFletePer;
            fleteLocal.IsSynced = true;
            await _databaseService.UpdateAsync(fleteLocal);

            _serverIdFletePer = serverIdFletePer;
            await ActualizarServerIdEnDetallesAsync(_fleteLocalId, serverIdFletePer);
        }

        private async Task ActualizarServerIdEnDetallesAsync(int localId, long serverId)
        {
            var detalles = await _databaseService.GetItemsAsync<Tb_FlePer_DetFlete>();
            var aActualizar = detalles.Where(d => d.FleteLocalId == localId && d.IdFletePer != serverId).ToList();

            foreach (var d in aActualizar)
            {
                d.IdFletePer = serverId;
                await _databaseService.UpdateAsync(d);
            }
        }

        private async Task SincronizarDetallesAsync()
        {
            if (_fleteLocalId == 0) return;

            var flete = await _databaseService.GetItemAsync<Tb_FlePer_FletePersonal>(_fleteLocalId);
            if (flete?.IdFletePer is null or 0) return;

            var serverIdFletePer = flete.IdFletePer.Value;
            var detalles = await _databaseService.GetItemsAsync<Tb_FlePer_DetFlete>();
            var pendientes = detalles.Where(d => d.FleteLocalId == _fleteLocalId && !d.IsSynced).ToList();

            if (!pendientes.Any()) return;

            foreach (var det in pendientes)
            {
                try
                {
                    var req = new DetFleteRequest
                    {
                        IdFletePer = (int)serverIdFletePer,
                        FlePer_CveNomina = det.CveNomina ?? 0,
                        FlePer_Latitud = det.Latitud ?? 0,
                        FlePer_Longitud = det.Longitud ?? 0,
                        FlePer_Fecha = det.Fecha ?? DateTime.Now,

                        // FIX 2026-06-02 (CRÍTICO encontrado en log):
                        // El backend tiene [Required] en FlePer_Nombre y
                        // rechaza con 400 BadRequest si llega null/vacío.
                        // Log real visto en el dispositivo del usuario:
                        //   "errors":{"FlePer_Nombre":["The FlePer_Nombre
                        //    field is required."]}
                        //   Status: 400 en /InsertarDetFlete
                        //
                        // Curiosidad del backend: aunque el campo es
                        // obligatorio para validación, el SQL de
                        // InsertarDetFlete IGNORA el valor enviado y hace
                        // CONCAT(...) desde tb_cat_empleados usando
                        // @FlePer_CveNomina. O sea: el nombre "real" lo
                        // calcula la BD; el campo FlePer_Nombre en el
                        // request es decorativo.
                        //
                        // Por eso mandamos un placeholder que pasa la
                        // validación pero el backend descarta:
                        //   - Para INICIO/FIN: "INICIO" / "FIN"
                        //   - Para pasajeros escaneados (det.Nombre == null):
                        //     "(pendiente)" que la BD sobreescribirá con
                        //     el nombre real del empleado
                        FlePer_Nombre = det.CveNomina switch
                        {
                            0 => "INICIO",
                            9999 => "FIN",
                            _ => string.IsNullOrWhiteSpace(det.Nombre)
                                ? "(pendiente)"
                                : det.Nombre
                        }
                    };

                    bool success = det.CveNomina switch
                    {
                        0 => await _apiService.InsertarInicioDetFlete(req),
                        9999 => await _apiService.InsertarFinDetFlete(req),
                        _ => await _apiService.InsertarDetFlete(req)
                    };

                    if (success)
                    {
                        det.IsSynced = true;
                        await _databaseService.UpdateAsync(det);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Sync] Detalle {det.Id} error: {ex.Message}");
                }
            }
        }

        // ── Cierre de Viaje ──────────────────────────────────────────────────
        [RelayCommand]
        public async Task FinalizarViajeAsync()
        {
            if (IsSyncing)
            {
                await _alertService.ShowAlertAsync("Espera", "Sincronización en curso. Intenta en un momento.");
                return;
            }

            var totalEmpleados = Pasajeros.Count(p => p.CveNomina != 0 && p.CveNomina != 9999);
            var confirmar = await _alertService.ShowConfirmationAsync("Finalizar Viaje", $"Se registraron {totalEmpleados} pasajeros.\n¿Confirmar cierre del viaje?");
            if (!confirmar) return;

            if (!Pasajeros.Any(p => p.CveNomina == 9999))
            {
                var loc = await GetCurrentLocationAsync();
                var fin = new Tb_FlePer_DetFlete
                {
                    FleteLocalId = _fleteLocalId,
                    IdFletePer = _serverIdFletePer > 0 ? _serverIdFletePer : (long)_fleteLocalId,
                    CveNomina = 9999,
                    Latitud = loc?.Latitude ?? 0,
                    Longitud = loc?.Longitude ?? 0,
                    Fecha = DateTime.Now,
                    Nombre = "FIN",
                    IsSynced = false
                };
                await _databaseService.InsertAsync(fin);
                Pasajeros.Add(fin);
            }

            var fleteLocal = await _databaseService.GetItemAsync<Tb_FlePer_FletePersonal>(_fleteLocalId);
            if (fleteLocal != null)
            {
                fleteLocal.Cantidad = totalEmpleados;
                fleteLocal.FechaFin = DateTime.Now;
                await _databaseService.UpdateAsync(fleteLocal);
            }

            if (IsConnected)
            {
                await TrySyncDataAsync();

                if (_serverIdFletePer > 0)
                {
                    try
                    {
                        await _apiService.UpdateFletePersonal(new UpdateFleteRequest
                        {
                            IdFletePer = (int)_serverIdFletePer,
                            FlePer_Cantidad = totalEmpleados
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Finalizar] Error: {ex.Message}");
                    }
                }
            }

            var msg = IsConnected
                ? $"{totalEmpleados} pasajeros registrados y enviados al servidor."
                : $"{totalEmpleados} pasajeros guardados localmente.";

            await _alertService.ShowAlertAsync("Viaje Finalizado", msg);

            // FIX 2026-06-02: limpiar la preference de "último flete"
            // porque ESTE viaje ya terminó exitosamente. Si el chofer
            // reabre la app, no verá el diálogo de "continuar flete"
            // porque ya no hay nada que continuar.
            //
            // Lo limpiamos ANTES del GoBackAsync para que si el usuario
            // mata la app durante la alerta, la preference ya esté
            // consistente con el estado real.
            try
            {
                Preferences.Remove("ultimo_flete_local_id");
                Preferences.Remove("ultimo_flete_chofer");
                Preferences.Remove("ultimo_flete_ruta");
                Preferences.Remove("ultimo_flete_fecha");
            }
            catch { /* ignore: las prefs se limpian en próxima escritura */ }

            await _navigationService.GoBackAsync();
        }

        // ── Helpers Técnicos ─────────────────────────────────────────────────
        private void ActualizarTotalPasajeros()
        {
            var empleados = Pasajeros.Count(p => p.CveNomina != 0 && p.CveNomina != 9999);
            TotalPasajerosText = $"Pasajeros: {empleados}";
        }

        private async Task<Location?> GetCurrentLocationAsync()
        {
            try
            {
                var req = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8));
                return await Geolocation.GetLocationAsync(req, _cancellationTokenSource.Token);
            }
            catch
            {
                return null;
            }
        }

        private void StartSyncService()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(SyncIntervalSegundos), _cancellationTokenSource.Token);

                        if (IsConnected && await TienePendientesAsync())
                            await TrySyncDataAsync();
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SyncService] {ex.Message}");
                        await SafeDelayAsync(TimeSpan.FromSeconds(30));
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        private async Task SafeDelayAsync(TimeSpan delay)
        {
            try { await Task.Delay(delay, _cancellationTokenSource.Token); }
            catch (OperationCanceledException) { }
        }

        public override void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;

            // FIX (2026-06-01): se elimina _audioService.Dispose().
            // El wrapper IAudioService (SoundPlayer / Plugin.Maui.Audio / etc.)
            // normalmente se registra como SINGLETON en MauiProgram.cs, porque
            // reutiliza el SoundPool del sistema. Disponerlo aquí rompe la
            // siguiente vez que cualquier pantalla reproduzca un beep/error,
            // lanzando ObjectDisposedException en la próxima sesión de escaneo.
            // Si IAudioService fuera TRANSIENT, sería seguro disposearlo, pero
            // ese patrón es raro y debe decidirse explícitamente.
            // Se deja un _audioService?. referencia por si en el futuro se
            // quiere liberar buffers; por ahora se omite.

            base.Dispose();
        }
    }
    // Copia esto al final de EscaneoCodigoViewModel.cs
    public static class TaskExtensions
    {
        /// <summary>
        /// Ejecuta una tarea en fire-and-forget con manejo seguro de excepciones
        /// </summary>
        public static async void FireAndForgetSafeAsync(this Task task, IAlertService alertService)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                // Loggear el error
                Console.WriteLine($"[FireAndForget] Error: {ex.Message}");

                // Mostrar alerta si el servicio está disponible
                if (alertService != null)
                {
                    try
                    {
                        await alertService.ShowAlertAsync("Error Inesperado", ex.Message);
                    }
                    catch { /* Ignorar errores de UI */ }
                }
            }
        }
    }
}