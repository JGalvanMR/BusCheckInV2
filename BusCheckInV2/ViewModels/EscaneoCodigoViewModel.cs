using BarcodeScanning;
using BusCheckInV2.Models;
using BusCheckInV2.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BusCheckInV2.ViewModels
{
    public partial class EscaneoCodigoViewModel : BaseViewModel, IQueryAttributable
    {
        private readonly ISQLiteService _databaseService;
        private readonly IApiFleteService _apiService;
        private readonly IAlertService _alertService;
        private readonly INavigationService _navigationService;
        private readonly IAudioService _audioService;
        private readonly SemaphoreSlim _syncSemaphore = new(1, 1);
        private readonly ConcurrentQueue<string> _pendingOperations = new();

        [ObservableProperty]
        private ObservableCollection<Tb_FlePer_DetFlete> _pasajeros = new();

        [ObservableProperty]
        private ObservableCollection<Tb_FlePer_DetFlete> _allPasajeros = new();

        [ObservableProperty]
        private string _totalPasajerosText = "Total: 0";

        [ObservableProperty]
        private string _textoBotonEscaneo = "Escanear";

        [ObservableProperty]
        private bool _isSyncing;

        [ObservableProperty]
        private bool _isFinalizarEnabled = true;

        [ObservableProperty]
        private string _syncStatus = "Sincronizado";

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private bool _isTorchOn;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private bool _isProcessingBarcode;

        [ObservableProperty]
        private string _manualEntryText = string.Empty;

        private int? _folioFlete;
        private int _fleteLocalId = 0;
        private readonly string _patronNumeros = @"^[0-9]+$";
        private const int SyncIntervalSeconds = 15;

        public bool CanScan => !IsSyncing;
        public bool HasPendingSyncOperations => !_pendingOperations.IsEmpty || Pasajeros.Any(p => !p.IsSynced);
        private bool _isSyncInProgress = false;

        // ─── NUEVO MÉTODO: Recibe parámetros de navegación Shell ──────────────
        // MAUI llama a este método automáticamente ANTES de que la página
        // aparezca en pantalla, cuando se navega con query parameters.
        // El diccionario contiene los pares clave=valor de la URL.
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            // Verificamos que el parámetro "fleteLocalId" exista en la URL
            if (query.TryGetValue("fleteLocalId", out var value))
            {
                // Convertimos el valor (llega como string) a int
                if (int.TryParse(value?.ToString(), out int id) && id > 0)
                {
                    _fleteLocalId = id;
                    Console.WriteLine($"[EscaneoVM] Flete local ID recibido: {_fleteLocalId}");
                }
                else
                {
                    // Log de seguridad: el parámetro llegó pero no es válido
                    Console.WriteLine($"[EscaneoVM] ADVERTENCIA: fleteLocalId inválido: {value}");
                }
            }
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

        // ─── CAMBIO: Guard corregido ──────────────────────────────────────────
        // El guard original era incorrecto:
        //   if (_folioFlete.HasValue) return;   ← devuelve si YA tiene valor
        // Lo que queremos es NO inicializar si NO tenemos flete
        public async Task InitializeAsync()
        {
            // Si no tenemos un flete válido, no tiene sentido inicializar el escaneo
            if (_fleteLocalId == 0)
            {
                Console.WriteLine("[EscaneoVM] ERROR: InitializeAsync sin fleteLocalId");
                await _alertService.ShowAlertAsync(
                    "Error",
                    "No se pudo iniciar el escaneo. Regresa y selecciona un flete.");
                return;
            }

            IsConnected = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            await UpdateTotalPasajerosAsync();
            await CargarPasajerosExistentesAsync(); // ← NUEVO: carga escaneos previos de este flete

            if (IsConnected)
                await TrySyncDataAsync();

            StartSyncService();
        }
        public async Task InitializeAsyncLEGACY()
        {
            if (_folioFlete.HasValue) return;

            IsConnected = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            await UpdateTotalPasajerosAsync();

            _pendingOperations.Enqueue("FLETE");
            _pendingOperations.Enqueue("INICIO");

            if (IsConnected)
                await TrySyncDataAsync();

            StartSyncService();
        }

        // ─── NUEVO MÉTODO: Carga pasajeros ya escaneados si se regresa al flete
        private async Task CargarPasajerosExistentesAsync()
        {
            try
            {
                // Obtiene todos los detalles de este flete específico desde SQLite
                var todosDetalles = await _databaseService.GetItemsAsync<Tb_FlePer_DetFlete>();
                var detallesDeEsteFlete = todosDetalles
                    .Where(d => d.IdFletePer == _fleteLocalId)
                    .ToList();

                Pasajeros.Clear();
                AllPasajeros.Clear();

                foreach (var detalle in detallesDeEsteFlete)
                {
                    Pasajeros.Add(detalle);
                    AllPasajeros.Add(detalle);
                }

                await UpdateTotalPasajerosAsync();
                Console.WriteLine($"[EscaneoVM] Cargados {detallesDeEsteFlete.Count} pasajeros existentes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EscaneoVM] Error cargando pasajeros existentes: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ToggleTorch() => IsTorchOn = !IsTorchOn;

        [RelayCommand]
        private void ToggleScan()
        {
            IsScanning = !IsScanning;
            IsTorchOn = IsScanning;
            TextoBotonEscaneo = IsScanning ? "Detener" : "Escanear";
            if (IsScanning) SearchText = string.Empty;
        }

        [RelayCommand]
        public async Task ProcessBarcodeResultsAsync(IReadOnlySet<BarcodeResult> barcodeResults)
        {
            if (barcodeResults?.Count is 0 || _isProcessingBarcode) return;

            try
            {
                _isProcessingBarcode = true;
                IsScanning = false;

                var barcodeValue = barcodeResults.First().RawValue?.Trim();
                if (!string.IsNullOrWhiteSpace(barcodeValue))
                    await ProcessBarcodeValueAsync(barcodeValue);
            }
            finally
            {
                _isProcessingBarcode = false;
                IsScanning = true;
            }
        }

        // ─── CAMBIO: ProcessBarcodeValueAsync usa _fleteLocalId ───────────────
        private async Task ProcessBarcodeValueAsync(string barcodeValue)
        {
            if (!ValidateBarcode(barcodeValue, out int numeroNomina)) return;

            var location = await GetCurrentLocationAsync();
            if (location == null) return;

            await _audioService.PlayBeepAsync();

            var nuevoPasajero = new Tb_FlePer_DetFlete
            {
                IdFletePer = _fleteLocalId, // ← AHORA TIENE VALOR
                CveNomina = numeroNomina,
                Latitud = location.Latitude,
                Longitud = location.Longitude,
                Fecha = DateTime.Now,
                IsSynced = false
            };

            Pasajeros.Add(nuevoPasajero);
            AllPasajeros.Add(nuevoPasajero);
            await UpdateTotalPasajerosAsync();

            await _databaseService.InsertAsync(nuevoPasajero);

            if (IsConnected)
                await TrySyncDataAsync();
            else
                SyncStatus = "Pendiente (Offline)";
        }
        private async Task ProcessBarcodeValueAsyncLEGACY(string barcodeValue)
        {
            if (!ValidateBarcode(barcodeValue, out int numeroNomina)) return;

            var location = await GetCurrentLocationAsync();
            if (location == null) return;

            await _audioService.PlayBeepAsync();

            var nuevoPasajero = new Tb_FlePer_DetFlete
            {
                IdFletePer = _folioFlete,
                CveNomina = numeroNomina,
                Latitud = location.Latitude,
                Longitud = location.Longitude,
                Fecha = DateTime.Now,
                IsSynced = false
            };

            Pasajeros.Add(nuevoPasajero);
            AllPasajeros.Add(nuevoPasajero);
            await UpdateTotalPasajerosAsync();

            // Guardar en BD
            await _databaseService.InsertAsync(nuevoPasajero);

            if (IsConnected)
            {
                _pendingOperations.Enqueue("REGISTROS");
                await TrySyncDataAsync();
            }
            else
            {
                SyncStatus = "Pendiente (Offline)";
            }
        }

        private bool ValidateBarcode(string barcodeValue, out int numeroNomina)
        {
            numeroNomina = 0;

            // Primera validación: formato del código
            if (string.IsNullOrWhiteSpace(barcodeValue) ||
                !Regex.IsMatch(barcodeValue, _patronNumeros) ||
                !int.TryParse(barcodeValue, out numeroNomina))
            {
                _audioService.PlayErrorAsync().FireAndForgetSafeAsync(_alertService);
                _alertService.ShowAlertAsync("Error", "Código inválido").FireAndForgetSafeAsync(_alertService);
                return false;
            }

            // ✅ SOLUCIÓN: Capturar el valor en una variable local
            int nominaCapturada = numeroNomina;

            // Segunda validación: duplicados
            if (Pasajeros.Any(p => p.CveNomina == nominaCapturada)) // ✅ Usar variable local
            {
                _audioService.PlayErrorAsync().FireAndForgetSafeAsync(_alertService);
                _alertService.ShowAlertAsync("Advertencia", "Pasajero ya registrado").FireAndForgetSafeAsync(_alertService);
                return false;
            }

            return true;
        }


        #region SINCRONIZACION CON BASE DE DATOS REMOTA
        // BusCheckInV2/ViewModels/EscaneoCodigoViewModel.cs
        // Reemplaza completamente el método TrySyncDataAsync y agrega los métodos de apoyo

        private async Task TrySyncDataAsync()
        {
            // Guard: no sincronizar si ya hay una en curso o no hay internet
            if (_isSyncInProgress || !IsConnected) return;

            await _syncSemaphore.WaitAsync();
            try
            {
                _isSyncInProgress = true;
                IsSyncing = true;
                IsFinalizarEnabled = false;

                Console.WriteLine($"[Sync] Iniciando sincronización para flete local {_fleteLocalId}");

                // Fase 1: Sincronizar el flete padre
                // Si el flete ya tiene IdFletePer (fue sincronizado antes), esta fase se salta
                await SincronizarFletePadreAsync();

                // Fase 2: Sincronizar los detalles (pasajeros escaneados)
                // Solo se ejecuta si el flete padre ya tiene un IdFletePer válido del servidor
                await SincronizarDetallesAsync();

                SyncStatus = "Sincronizado";
                Console.WriteLine("[Sync] Sincronización completada exitosamente");
            }
            catch (Exception ex)
            {
                // No crashear — simplemente marcamos como pendiente
                SyncStatus = "Pendiente";
                Console.WriteLine($"[Sync] Error durante sincronización: {ex.Message}");
            }
            finally
            {
                _isSyncInProgress = false;
                IsSyncing = false;
                IsFinalizarEnabled = true;
                _syncSemaphore.Release();
            }
        }

        // ─── FASE 1: Sincronizar el flete padre ──────────────────────────────────────
        private async Task SincronizarFletePadreAsync()
        {
            if (_fleteLocalId == 0) return;

            // Obtener el flete local desde SQLite
            var fleteLocal = await _databaseService.GetItemAsync<Tb_FlePer_FletePersonal>(_fleteLocalId);

            if (fleteLocal == null)
            {
                Console.WriteLine($"[Sync] No se encontró flete local con Id={_fleteLocalId}");
                return;
            }

            // Si ya fue sincronizado y tiene ID del servidor, no hacemos nada
            if (fleteLocal.IsSynced && fleteLocal.IdFletePer.HasValue && fleteLocal.IdFletePer > 0)
            {
                Console.WriteLine($"[Sync] Flete padre ya sincronizado. IdFletePer servidor={fleteLocal.IdFletePer}");
                return;
            }

            Console.WriteLine($"[Sync] Sincronizando flete padre Id={_fleteLocalId}...");

            // Construir el request para la API con los datos del flete local
            var request = new FletePersonalRequest
            {
                // Formateamos la fecha como string (la API espera string según FletePersonalRequest)
                Fecha = fleteLocal.Fecha?.ToString("yyyy-MM-dd")
                        ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Hora = fleteLocal.Hora?.ToString(@"hh\:mm\:ss")
                       ?? DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss"),
                ClaveProveedor = fleteLocal.ProvClave ?? string.Empty,
                IdDestFlete = (int)(fleteLocal.IdDestFlete ?? 0),
                TipoFlete = fleteLocal.TipoFlete ?? "NORMAL",
                TipoViaje = fleteLocal.TipoViaje ?? "TRAER GENTE",
                Cantidad = fleteLocal.Cantidad ?? 0,
                Estatus = fleteLocal.Status ?? "P",
                Chofer = fleteLocal.Chofer ?? string.Empty
            };

            // Llamada a la API — InsertarFletePersonal devuelve el ID del servidor (long)
            // Si devuelve -1 o 0, significa que falló
            var serverIdFletePer = await _apiService.InsertarFletePersonal(request);

            if (serverIdFletePer > 0)
            {
                // Actualizamos el registro local con el ID que nos asignó el servidor
                // y marcamos como sincronizado
                fleteLocal.IdFletePer = serverIdFletePer;
                fleteLocal.IsSynced = true;
                await _databaseService.UpdateAsync(fleteLocal);

                Console.WriteLine($"[Sync] Flete padre sincronizado. IdFletePer servidor={serverIdFletePer}");

                // CRÍTICO: Actualizar todos los DetFlete de este flete que tienen
                // el ID local como referencia, para que apunten al ID real del servidor
                await ActualizarIdFleteEnDetallesAsync(_fleteLocalId, serverIdFletePer);
            }
            else
            {
                Console.WriteLine($"[Sync] API rechazó el flete padre. Respuesta={serverIdFletePer}");
                throw new Exception("El servidor no pudo insertar el flete padre.");
            }
        }

        // ─── Actualiza el IdFletePer en los detalles del servidor ID recibido ─────────
        private async Task ActualizarIdFleteEnDetallesAsync(int fleteLocalId, long serverIdFletePer)
        {
            // Obtenemos todos los detalles que tienen como referencia el ID local del flete
            var todosDetalles = await _databaseService.GetItemsAsync<Tb_FlePer_DetFlete>();

            // Filtramos los que todavía tienen el ID local (no el del servidor)
            // Usamos (long)fleteLocalId porque IdFletePer es long? en el modelo
            var detallesAActualizar = todosDetalles
                .Where(d => d.IdFletePer == (long)fleteLocalId)
                .ToList();

            Console.WriteLine($"[Sync] Actualizando {detallesAActualizar.Count} detalles con IdFletePer={serverIdFletePer}");

            foreach (var detalle in detallesAActualizar)
            {
                detalle.IdFletePer = serverIdFletePer;
                await _databaseService.UpdateAsync(detalle);
            }
        }

        // ─── FASE 2: Sincronizar los detalles (pasajeros escaneados) ─────────────────
        private async Task SincronizarDetallesAsync()
        {
            if (_fleteLocalId == 0) return;

            // Obtenemos el flete actualizado para tener el IdFletePer del servidor
            var fleteLocal = await _databaseService.GetItemAsync<Tb_FlePer_FletePersonal>(_fleteLocalId);

            // Solo sincronizamos si el padre ya fue sincronizado y tiene ID del servidor
            if (fleteLocal == null || !fleteLocal.IdFletePer.HasValue || fleteLocal.IdFletePer <= 0)
            {
                Console.WriteLine("[Sync] No se sincronizan detalles: flete padre sin IdFletePer");
                return;
            }

            var serverIdFletePer = fleteLocal.IdFletePer.Value;

            // Obtenemos todos los detalles de este flete que NO están sincronizados
            var todosDetalles = await _databaseService.GetItemsAsync<Tb_FlePer_DetFlete>();
            var detallesPendientes = todosDetalles
                .Where(d => d.IdFletePer == serverIdFletePer && !d.IsSynced)
                .ToList();

            Console.WriteLine($"[Sync] Sincronizando {detallesPendientes.Count} detalles pendientes...");

            int sincronizados = 0;
            int fallidos = 0;

            foreach (var detalle in detallesPendientes)
            {
                try
                {
                    var request = new DetFleteRequest
                    {
                        IdFletePer = (int)serverIdFletePer,
                        FlePer_CveNomina = detalle.CveNomina ?? 0,
                        FlePer_Latitud = detalle.Latitud ?? 0,
                        FlePer_Longitud = detalle.Longitud ?? 0,
                        FlePer_Fecha = detalle.Fecha ?? DateTime.Now,
                        FlePer_Nombre = string.Empty // El servidor no lo requiere según el modelo
                    };

                    var success = await _apiService.InsertarDetFlete(request);

                    if (success)
                    {
                        // Marcar como sincronizado en SQLite
                        detalle.IsSynced = true;
                        await _databaseService.UpdateAsync(detalle);
                        sincronizados++;
                    }
                    else
                    {
                        fallidos++;
                        Console.WriteLine($"[Sync] Detalle {detalle.Id} rechazado por API");
                    }
                }
                catch (Exception ex)
                {
                    fallidos++;
                    Console.WriteLine($"[Sync] Error sincronizando detalle {detalle.Id}: {ex.Message}");
                    // Continuamos con el siguiente — no abortamos todo el lote por un fallo
                }
            }

            Console.WriteLine($"[Sync] Detalles: {sincronizados} sincronizados, {fallidos} fallidos");

            // Actualizamos el status visual según el resultado
            if (fallidos > 0)
                SyncStatus = $"Pendiente ({fallidos} sin sync)";
            else
                SyncStatus = "Sincronizado";
        }
        #endregion


        private async Task UpdateTotalPasajerosAsync()
        {
            TotalPasajerosText = $"Total: {Pasajeros.Count}";
            await Task.CompletedTask;
        }

        private async Task<Location> GetCurrentLocationAsync()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8));
                return await Geolocation.GetLocationAsync(request, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                await _alertService.ShowAlertAsync("Error", $"No se pudo obtener ubicación: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> TryFinalSyncBeforeExit()
        {
            if (!IsConnected)
                return false; // No hay conexión, no se puede sincronizar

            try
            {
                // Cancelar sincronización automática en curso
                //                _syncCancellationTokenSource?.Cancel();

                // Esperar un momento para que se cancelen las operaciones en curso
                await Task.Delay(500);

                // Intentar una sincronización manual final
                await TrySyncDataAsync();

                // Verificar si quedan operaciones pendientes
                return !HasPendingSyncOperations;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en sincronización final: {ex}");
                return false;
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
                        if (HasPendingSyncOperations && IsConnected)
                            await TrySyncDataAsync();

                        await Task.Delay(TimeSpan.FromSeconds(SyncIntervalSeconds), _cancellationTokenSource.Token);
                    }
                    catch (TaskCanceledException) { break; }
                    catch { /* Ignorar */ }
                }
            }, _cancellationTokenSource.Token);
        }

        private async Task TrySyncDataAsyncLEGACY()
        {
            if (_isSyncInProgress || !IsConnected) return;

            await _syncSemaphore.WaitAsync();
            try
            {
                _isSyncInProgress = true;
                IsSyncing = true;
                IsFinalizarEnabled = false;

                bool success = true;
                foreach (var op in _pendingOperations)
                {
                    try
                    {
                        // Procesar operación
                        _pendingOperations.TryDequeue(out _);
                    }
                    catch
                    {
                        success = false;
                    }
                }

                SyncStatus = success ? "Sincronizado" : "Pendiente";
            }
            finally
            {
                _isSyncInProgress = false;
                IsSyncing = false;
                IsFinalizarEnabled = true;
                _syncSemaphore.Release();
            }
        }

        [RelayCommand]
        public async Task FinalizarViajeAsync()
        {
            if (IsSyncing)
            {
                await _alertService.ShowAlertAsync("Advertencia", "Sincronización en curso. Por favor espera.");
                return;
            }

            await TrySyncDataAsync();
            await _alertService.ShowAlertAsync("Viaje finalizado", $"Se registraron {Pasajeros.Count} pasajeros correctamente.");
            await _navigationService.GoBackAsync();
        }

        [RelayCommand]
        public async Task AddManualAsync()
        {
            if (string.IsNullOrWhiteSpace(ManualEntryText)) return;
            await ProcessBarcodeValueAsync(ManualEntryText.Trim());
            ManualEntryText = string.Empty;
        }

        partial void OnSearchTextChanged(string value) => FilterPasajeros(value);

        public void FilterPasajeros(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                Pasajeros = new ObservableCollection<Tb_FlePer_DetFlete>(AllPasajeros);
            else
                Pasajeros = new ObservableCollection<Tb_FlePer_DetFlete>(
                    AllPasajeros.Where(p => p.CveNomina.ToString().Contains(query)));
        }

        public override void Dispose()
        {
            _audioService.Dispose();
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