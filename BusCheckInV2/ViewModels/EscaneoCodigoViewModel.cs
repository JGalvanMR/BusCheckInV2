using Android.Content.Res;
using Android.Net;
using BarcodeScanning;
using BusCheckInV2.Models;
using BusCheckInV2.Services;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graph;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using Plugin.Maui.Audio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BusCheckInV2.ViewModels
{
    public partial class EscaneoCodigoViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<Tb_FlePer_DetFlete> pasajeros;
        [ObservableProperty]
        private string totalPasajerosText;
        [ObservableProperty]
        public string textoBotonEscaneo;
        [ObservableProperty]
        private bool isSyncing;
        [ObservableProperty]
        private bool isFinalizarEnabled = true;
        [ObservableProperty]
        private string syncStatus = "Sincronizado";
        [ObservableProperty]
        private string searchText;
        [ObservableProperty]
        private bool _isScanning = false;
        [ObservableProperty]
        private bool hasPasajeros;

        private int? folioFle = null;
        private ObservableCollection<Tb_FlePer_FletePersonal> provRutas;
        private CancellationTokenSource syncTokenSource;
        private const int SyncIntervalSeconds = 15; // 45 O 60
        private const int MaxRetryAttempts = 3;
        private readonly object _playerLock = new object();
        private bool _isPlaying = false;
        private readonly string patronNumeros = @"^[0-9]+$";
        private readonly ISQLiteService _databaseService;
        //private readonly IWSBusCheckInClient _wsClient;
        private bool permisosVerificados = false;
        //private readonly ApiService apiService = new ApiService();
        private Queue<string> pendingOperations = new Queue<string>();
        private ObservableCollection<Tb_FlePer_DetFlete> allPasajeros = new();
        private Location? cachedLocation;
        private DateTime lastLocationTime;
        public bool CanScan => !IsSyncing;
        private readonly IAudioPlayer _beepPlayer;
        private readonly IAudioPlayer _errorPlayer;
        [ObservableProperty]
        private bool isConnected = true;
        [ObservableProperty]
        private bool _isTorchOn = false;
        [ObservableProperty]
        private bool _isSyncInProgress = false;
        private bool _isInitialized = false;

        // Propiedad para verificar operaciones pendientes
        public bool HasPendingSyncOperations => pendingOperations.Count > 0 || Pasajeros.Any(p => !p.IsSynced) || _isSyncInProgress;

        private readonly SemaphoreSlim _syncSemaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _syncCancellationTokenSource;

        public EscaneoCodigoViewModel(ISQLiteService databaseService)
        {
            Pasajeros = new ObservableCollection<Tb_FlePer_DetFlete>();
            TotalPasajerosText = "Total: 0";
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            //_wsClient = wsClient ?? throw new ArgumentNullException(nameof(wsClient));


            var beepStream = FileSystem.OpenAppPackageFileAsync("scanner.mp3").Result;
            var beepMemory = new MemoryStream();
            beepStream.CopyTo(beepMemory);
            beepMemory.Position = 0;
            _beepPlayer = AudioManager.Current.CreatePlayer(beepMemory);

            var errorStream = FileSystem.OpenAppPackageFileAsync("error.mp3").Result;
            var errorMemory = new MemoryStream();
            errorStream.CopyTo(errorMemory);
            errorMemory.Position = 0;
            _errorPlayer = AudioManager.Current.CreatePlayer(errorMemory);
        }

        // También modificar el método de limpieza para mejor manejo
        public async Task CleanupForExit()
        {
            try
            {
                // Detener sincronización automática
                _syncCancellationTokenSource?.Cancel();

                // Si hay datos pendientes y hay conexión, intentar una última sincronización
                if (HasPendingSyncOperations && IsConnected)
                {
                    await TryFinalSyncBeforeExit();
                }

                // Limpiar recursos
                Cleanup();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en cleanup para salida: {ex}");
                Cleanup(); // Limpiar de todas formas
            }
        }

        public void Cleanup()
        {
            CleanupNonCriticalResources();
            //_syncCancellationTokenSource?.Cancel();
            //_syncCancellationTokenSource?.Dispose();
            //_syncSemaphore?.Dispose();

            Connectivity.ConnectivityChanged -= OnConnectivityChanged;

            _beepPlayer?.Dispose();
            _errorPlayer?.Dispose();

            pendingOperations.Clear();
            //IsScanning = false;
            //IsTorchOn = false;
            //_isSyncInProgress = false;
        }
        public void CleanupNonCriticalResources()
        {
            // Liberar solo recursos no críticos, mantener datos pendientes
            _syncCancellationTokenSource?.Cancel();

            IsScanning = false;
            IsTorchOn = false;
            _isSyncInProgress = false;

            // NO limpiar: pendingOperations, Pasajeros, allPasajeros, folioFle, etc.
        }
        [RelayCommand]
        private void ToggleTorch()
        {
            IsTorchOn = !IsTorchOn;
        }
        [RelayCommand]
        private void ToggleScan()
        {
            IsScanning = !IsScanning;
            // Opcional: controlar torch automáticamente
            IsTorchOn = IsScanning;
            if (IsScanning)
            {
                textoBotonEscaneo = "Detener";
                // Reiniciar búsqueda al iniciar escaneo
                SearchText = string.Empty;
            }
            else
            {
                textoBotonEscaneo = "Escanear";
            }
        }
        //public string TextoBotonEscaneo => IsScanning ? "Detener" : "Escanear";

        // CORREGIR: Mejorar el método de inicialización
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;
            #region VERIFICAR CONEXION
            IsConnected = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            if (!IsConnected)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Sin conexión",
                    "No hay conexión a internet. El escaneo funcionará en modo offline.",
                    "OK");
            }
            #endregion

            IsSyncing = true;

            try
            {
                UpdateTotalPasajeros();
                provRutas = new ObservableCollection<Tb_FlePer_FletePersonal>(
                    await _databaseService.GetItemsAsync<Tb_FlePer_FletePersonal>());

                await BarcodeScanning.Methods.AskForRequiredPermissionAsync();

                // Forzar creación del flete y obtener folio antes de permitir escanear
                pendingOperations.Enqueue("FLETE");
                pendingOperations.Enqueue("INICIO");

                // Solo sincronizar si hay conexión
                if (IsConnected)
                {
                    await TrySyncDataAsync();
                }

                if (folioFle == null || folioFle == 0)
                {
                    // Si no hay conexión, crear un folio temporal local
                    if (!IsConnected)
                    {
                        // Crear un folio temporal para modo offline
                        var tempFlete = provRutas.FirstOrDefault();
                        if (tempFlete != null)
                        {
                            folioFle = tempFlete.IdFletePer;
                        }
                    }
                    else
                    {
                        Application.Current.MainPage.DisplayAlert(
                            "Error",
                            "No se pudo crear el viaje en el servidor. Intenta nuevamente.",
                            "OK");
                        await Shell.Current.GoToAsync("//selecciondeflete");
                        return;
                    }
                }

                allPasajeros = new ObservableCollection<Tb_FlePer_DetFlete>(Pasajeros);

                // Iniciar servicio de sincronización solo si hay conexión
                if (IsConnected)
                {
                    StartSyncService();
                }
            }
            finally
            {
                IsSyncing = false;
            }

            // Suscribirse a cambios de conectividad
            Connectivity.ConnectivityChanged += OnConnectivityChanged;
        }
        public async Task InitializeAsyncOG()
        {
            #region VERIFICAR CONEXION
            IsConnected = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            if (!IsConnected)
            {
                await Application.Current.MainPage.DisplayAlert("Sin conexión", "No hay conexion a internet. El escaneo funcionar'a en modo offline.", "OK");
            }
            #endregion

            IsSyncing = true;

            UpdateTotalPasajeros();
            provRutas = new ObservableCollection<Tb_FlePer_FletePersonal>(
                await _databaseService.GetItemsAsync<Tb_FlePer_FletePersonal>());

            await BarcodeScanning.Methods.AskForRequiredPermissionAsync();
            // Forzar creación del flete y obtener folio antes de permitir escanear
            pendingOperations.Enqueue("FLETE");
            pendingOperations.Enqueue("INICIO");
            await TrySyncDataAsync(); // espera a que termine

            if (folioFle == null || folioFle == 0)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "No se pudo crear el viaje en el servidor. Intenta nuevamente.", "OK");
                await Shell.Current.GoToAsync("//selecciondeflete");
                return;
            }

            allPasajeros = new ObservableCollection<Tb_FlePer_DetFlete>(Pasajeros);
            IsSyncing = false;
            StartSyncService();

            // Suscribirse a cambios de conectividad
            Connectivity.ConnectivityChanged += OnConnectivityChanged;
        }
        // CORREGIR: Mejorar el manejo de cambios de conectividad
        private async void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            var wasConnected = IsConnected;
            IsConnected = e.NetworkAccess == NetworkAccess.Internet;

            if (IsConnected && !wasConnected)
            {
                // Se restauró la conexión, intentar sincronizar
                SyncStatus = "Sincronizando...";
                await TrySyncDataAsync();

                // Reiniciar servicio de sincronización si no está activo
                if (_syncCancellationTokenSource == null || _syncCancellationTokenSource.IsCancellationRequested)
                {
                    StartSyncService();
                }
            }
            else if (!IsConnected)
            {
                SyncStatus = "Sin conexión";
            }
        }

        [ObservableProperty]
        private bool _isProcessingBarcode = false;
        [RelayCommand]
        public async Task ProcessBarcodeResultsAsync(IReadOnlySet<BarcodeResult> barcodeResults)
        {
            if (barcodeResults?.Count is null or 0 || _isProcessingBarcode) return;

            try
            {
                _isProcessingBarcode = true;
                IsScanning = false;
                var barcodeValue = barcodeResults.First().RawValue?.Trim();

                if (!string.IsNullOrWhiteSpace(barcodeValue))
                {

                    bool success = await ProcessBarcodeValueAsync(barcodeValue);

                    // Si hubo algún error en el procesamiento, detener el escaneo
                    if (!success)
                    {
                        IsScanning = false;
                        return;
                    }
                }



                // Reactivar escaneo automáticamente si se desea
                // IsScanning = true;
            }
            catch (Exception ex)
            {
                Application.Current.MainPage.DisplayAlert("Error", $"Error procesando código: {ex.Message}", "OK");
                return;
            }
            finally
            {
                _isProcessingBarcode = false;
                IsScanning = true; // ← REACTIVAR ESCANEO
            }
        }

        [RelayCommand]
        public async Task ProcessBarcodeAsync(string barcodeValue)
        {
            if (string.IsNullOrWhiteSpace(barcodeValue))
            {
                Application.Current.MainPage.DisplayAlert("Error", "Ingrese un código válido", "OK");
                return;
            }

            if (_isProcessingBarcode)
            {
                Application.Current.MainPage.DisplayAlert("Espere", "Procesando código...", "OK");
                return;
            }

            await ProcessBarcodeValueAsync(barcodeValue.Trim());
            ManualEntryText = string.Empty; // Limpiar siempre
        }
        private async Task<bool> ProcessBarcodeValueAsync(string barcodeValue)
        {
            #region VALIDACIONES RÁPIDAS
            if (string.IsNullOrWhiteSpace(barcodeValue) ||
                !Regex.IsMatch(barcodeValue, patronNumeros) ||
                !int.TryParse(barcodeValue, out int numeroNomina))
            {
                PlayAudioError();
                Application.Current.MainPage.DisplayAlert("Error", "Código inválido", "OK");
                return false;
            }

            // Verificar duplicado (esto es instantáneo)
            if (Pasajeros.Any(p => p.FlePer_CveNomina == numeroNomina))
            {
                PlayAudioError();
                Application.Current.MainPage.DisplayAlert("Advertencia", "Pasajero ya registrado", "OK");
                return false;
            }
            #endregion

            #region OBTIENE UBICACION
            var location = await GetCurrentLocationAsync();
            if (location == null)
            {
                PlayAudioError();
                Application.Current.MainPage.DisplayAlert("Error", "No se pudo obtener la ubicación.", "OK");
                return false;
            }
            #endregion

            try
            {
                await PlayAudio();

                var nuevoPasajero = new Tb_FlePer_DetFlete
                {
                    IdFletePer = folioFle,
                    FlePer_CveNomina = numeroNomina,
                    FlePer_Latitud = location.Latitude.ToString("F6"),
                    FlePer_Longitud = location.Longitude.ToString("F6"),
                    FlePer_Fecha = DateTime.Now,
                    IsSynced = !IsConnected // Si no hay conexión, marcar como no sincronizado
                };

                Pasajeros.Add(nuevoPasajero);
                allPasajeros.Add(nuevoPasajero);
                UpdateTotalPasajeros();

                // GUARDAR EN BD EN SEGUNDO PLANO (no esperar)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _databaseService.InsertAsync(nuevoPasajero);

                        // SINCRONIZAR SI HAY CONEXIÓN
                        if (IsConnected)
                        {
                            pendingOperations.Enqueue("REGISTROS");
                            pendingOperations.Enqueue("UPDATE");
                            await TrySyncDataAsync();
                        }
                        else
                        {
                            SyncStatus = "Pendiente (Offline)";
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error guardando: {ex}");
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                PlayAudioError();
                Application.Current.MainPage.DisplayAlert("Error", $"Error al guardar pasajero: {ex.Message}", "OK");
                return false;
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            FilterPasajeros(value);
        }

        private void UpdateTotalPasajeros()
        {
            TotalPasajerosText = $"Total: {Pasajeros.Count}";
        }

        #region Audio
        public Task PlayAudio() => PlaySound(_beepPlayer);
        public Task PlayAudioError() => PlaySound(_errorPlayer);

        private Task PlaySound(IAudioPlayer player)
        {
            player.Stop();
            player.Seek(0);
            player.Play();
            return Task.CompletedTask;
        }
        #endregion

        #region Ubicación
        private async Task<bool> VerificarPermisosAsync()
        {
            if (permisosVerificados) return true;
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    return false;
                }
            }
            permisosVerificados = true;
            return true;
        }

        private async Task<Location?> GetCurrentLocationAsync()
        {
            try
            {
                if (cachedLocation != null && (DateTime.UtcNow - lastLocationTime) < TimeSpan.FromSeconds(5)) return cachedLocation;

                if (!await VerificarPermisosAsync()) return null;

                var quickLocation = await Geolocation.GetLastKnownLocationAsync();
                if (quickLocation != null && quickLocation.Timestamp > DateTimeOffset.UtcNow.AddSeconds(-5))
                {
                    cachedLocation = quickLocation;
                    lastLocationTime = DateTime.UtcNow;
                    return quickLocation;
                }
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8));
                cachedLocation = await Geolocation.GetLocationAsync(request);
                lastLocationTime = DateTime.UtcNow;
                return await Geolocation.GetLocationAsync(request);
            }
            catch (FeatureNotSupportedException)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "GPS no soportado en este dispositivo.", "OK");
            }
            catch (FeatureNotEnabledException)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Activa el GPS en ajustes.", "OK");
            }
            catch (PermissionException)
            {
                await Application.Current.MainPage.DisplayAlert("Error", "Permisos de ubicación denegados.", "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error inesperado", $"Error al obtener ubicación: {ex.Message}", "OK");
            }
            return null;
        }
        #endregion

        #region WSBusCheckIn con Retry
        private async Task<bool> WSExecuteWithRetryAsync(Func<Task> action, int attempt = 1)
        {
            try
            {
                await action();
                return true;
            }
            catch
            {
                if (attempt < MaxRetryAttempts)
                {
                    await Task.Delay(2000);
                    return await WSExecuteWithRetryAsync(action, attempt + 1);
                }
                return false;
            }
        }

        /*private async Task WSProcessFleteAsync(string tipo) // Método definido aquí
        {
            switch (tipo)
            {
                case "FLETE":
                    foreach (var f in provRutas.Where(p => !p.IsSynced))
                        await WSExecuteWithRetryAsync(async () =>
                        {
                            var result = await _wsClient.InsertarTb_FlePer_FletePersonalAsync(
                                f.FlePer_Fecha, f.FlePer_Hora, f.Prov_Clave,
                                f.IdDestFlete.Value, f.FlePer_TipoFlete,
                                f.FlePer_TipoViaje, 0, "C", f.FlePer_Chofer);
                            f.IdFletePer = Convert.ToInt32(result);
                            f.IsSynced = true;
                            folioFle = f.IdFletePer;

                            await _databaseService.UpdateAsync(f);
                        });
                    break;
                case "INICIO":
                    await WSExecuteWithRetryAsync(async () =>
                    {
                        var loc = await GetCurrentLocationAsync();
                        if (loc == null) return;
                        await _wsClient.InsertarInicioTb_FlePer_DetFleteAsync(
                            folioFle.GetValueOrDefault(), 0,
                            loc.Latitude, loc.Longitude,
                            DateTime.Now, "INICIO");
                    });
                    break;
                case "REGISTROS":
                    foreach (var p in Pasajeros.Where(p => !p.IsSynced))
                        await WSExecuteWithRetryAsync(async () =>
                        {
                            await _wsClient.InsertarTb_FlePer_DetFleteAsync(
                                folioFle.GetValueOrDefault(),
                                p.FlePer_CveNomina.Value,
                                double.Parse(p.FlePer_Latitud),
                                double.Parse(p.FlePer_Longitud),
                                p.FlePer_Fecha.Value, "");
                            p.IsSynced = true;
                        });
                    break;
                case "FIN":
                    await WSExecuteWithRetryAsync(async () =>
                    {
                        var loc = await GetCurrentLocationAsync();
                        if (loc == null) return;
                        await _wsClient.InsertarFinTb_FlePer_DetFleteAsync(
                            folioFle.GetValueOrDefault(), 9999,
                            loc.Latitude, loc.Longitude,
                            DateTime.Now, "FIN");
                    });
                    break;
                case "UPDATE":
                    await WSExecuteWithRetryAsync(async () =>
                    {
                        await _wsClient.UpdateTb_FlePer_FletePersonalAsync(folioFle.GetValueOrDefault(), Pasajeros.Count);
                    });
                    break;
            }
        }*/
        #endregion

        #region SINCRONIZACION
        // CORREGIR: Este método tenía una condición contradictoria
        private async Task TrySyncDataAsync()
        {
            // Verificar si ya hay una sincronización en progreso
            if (_isSyncInProgress || Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;

            try
            {
                // Adquirir el semáforo para evitar sincronizaciones concurrentes
                await _syncSemaphore.WaitAsync();
                _isSyncInProgress = true;

                // Actualizar UI de forma segura
                Device.BeginInvokeOnMainThread(() =>
                {
                    IsSyncing = true;
                    IsFinalizarEnabled = false;
                });

                bool success = true;
                try
                {
                    // Crear una copia de las operaciones pendientes para evitar modificación concurrente
                    var operationsToProcess = new List<string>();
                    while (pendingOperations.Count > 0)
                    {
                        operationsToProcess.Add(pendingOperations.Dequeue());
                    }

                    // Procesar cada operación
                    foreach (var op in operationsToProcess)
                    {
                        try
                        {
                            //await WSProcessFleteAsync(op);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error en operación {op}: {ex}");
                            success = false;
                            // Re-encolar operación fallida
                            pendingOperations.Enqueue(op);
                        }
                    }

                    // Sincronizar pasajeros pendientes en lote
                    var pendingPasajeros = Pasajeros.Where(p => !p.IsSynced).ToList();
                    foreach (var pasajero in pendingPasajeros)
                    {
                        try
                        {
                            await WSExecuteWithRetryAsync(async () =>
                            {
                                /*await _wsClient.InsertarTb_FlePer_DetFleteAsync(
                                    folioFle.GetValueOrDefault(),
                                    pasajero.FlePer_CveNomina.Value,
                                    double.Parse(pasajero.FlePer_Latitud),
                                    double.Parse(pasajero.FlePer_Longitud),
                                    pasajero.FlePer_Fecha.Value, "");*/
                                pasajero.IsSynced = true;
                                await _databaseService.UpdateAsync(pasajero);
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error sincronizando pasajero: {ex}");
                            success = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en sincronización: {ex.Message}");
                    success = false;
                    // No mostrar alerta aquí para no interrumpir el flujo
                }
                finally
                {
                    SyncStatus = success ? "Sincronizado" : "Pendiente (Reintentar)";

                    // Solo mostrar alerta si hay un error crítico
                    if (!success && pendingOperations.Count > 0)
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            "Advertencia",
                            "Algunos datos no se pudieron sincronizar. Se reintentará automáticamente.",
                            "OK");
                    }

                    _isSyncInProgress = false;
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        IsSyncing = false;
                        IsFinalizarEnabled = true;
                    });
                }
            }
            finally
            {
                _syncSemaphore.Release();
            }
        }
        // CORREGIR: Mejorar el servicio de sincronización en segundo plano
        private void StartSyncService()
        {
            _syncCancellationTokenSource = new CancellationTokenSource();
            var token = _syncCancellationTokenSource.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Verificar si hay datos pendientes para sincronizar
                        bool hasPendingData = pendingOperations.Count > 0 ||
                                             Pasajeros.Any(p => !p.IsSynced) ||
                                             provRutas?.Any(f => !f.IsSynced) == true;

                        if (hasPendingData && Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                        {
                            await TrySyncDataAsync();
                        }

                        // Esperar antes del próximo intento
                        await Task.Delay(TimeSpan.FromSeconds(SyncIntervalSeconds), token);
                    }
                    catch (TaskCanceledException)
                    {
                        // Salir silenciosamente si la tarea fue cancelada
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error en servicio de sincronización: {ex.Message}");
                        // Esperar antes de reintentar
                        await Task.Delay(TimeSpan.FromSeconds(30), token);
                    }
                }
            }, token);
        }

        [RelayCommand]
        public async Task<bool> TryFinalSyncBeforeExit()
        {
            if (!IsConnected)
                return false; // No hay conexión, no se puede sincronizar

            try
            {
                // Cancelar sincronización automática en curso
                _syncCancellationTokenSource?.Cancel();

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

        private async Task TrySyncDataAsyncOG()
        {
            // Verificar si ya hay una sincronización en progreso
            if (_isSyncInProgress || Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return;

            IsSyncing = true;
            IsFinalizarEnabled = false;
            bool success = true;
            try
            {
                if (!pendingOperations.Contains("FLETE")) pendingOperations.Enqueue("FLETE");
                if (!pendingOperations.Contains("INICIO")) pendingOperations.Enqueue("INICIO");
                if (!pendingOperations.Contains("REGISTROS")) pendingOperations.Enqueue("REGISTROS");
                if (!pendingOperations.Contains("UPDATE")) pendingOperations.Enqueue("UPDATE");

                while (pendingOperations.Count > 0)
                {
                    var op = pendingOperations.Peek();
                    //await WSProcessFleteAsync(op);
                    pendingOperations.Dequeue();
                }
            }
            catch
            {
                success = false;
            }
            finally
            {
                SyncStatus = success ? "Sincronizado" : "Pendiente (Reintentar)";
                if (!success)
                {
                    await Application.Current.MainPage.DisplayAlert("Advertencia", "Sincronización fallida. Datos guardados localmente.", "OK");
                }
                IsFinalizarEnabled = true;
                IsSyncing = false;
            }
        }
        private void StartSyncServiceOG()
        {
            syncTokenSource = new CancellationTokenSource();
            var token = syncTokenSource.Token;
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (pendingOperations.Count > 0 || Pasajeros.Any(p => !p.IsSynced))
                    {
                        await TrySyncDataAsync();
                        await Task.Delay(8000, token); // sync agresivo solo si hay datos
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(45), token);
                    }
                }
            }, token);
            //Task.Run(async () =>
            //{
            //    while (!token.IsCancellationRequested)
            //    {
            //        await TrySyncDataAsync();
            //        await Task.Delay(TimeSpan.FromSeconds(SyncIntervalSeconds), token);
            //    }
            //}, token);
        }
        #endregion

        [RelayCommand]
        public async Task FinalizarViajeAsync()
        {
            IsFinalizarEnabled = false;
            try
            {
                if (IsSyncing)
                {
                    Application.Current.MainPage.DisplayAlert("Advertencia", "Sincronización en curso. Por favor espera.", "OK");
                    return;
                }
                await TrySyncDataAsync();
                pendingOperations.Enqueue("FIN");
                await TrySyncDataAsync(); // Procesar FIN
                await Application.Current.MainPage.DisplayAlert(
                    "Viaje finalizado",
                    $"Se registraron {Pasajeros.Count} pasajeros correctamente.",
                    "Aceptar");
                await LimpiarTablasLocalesAsync();
                await Shell.Current.GoToAsync("//selecciondeflete"); // Navega de vuelta
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Error al finalizar viaje: {ex.Message}", "OK");
            }
            finally
            {
                IsFinalizarEnabled = true;
            }
        }

        private async Task LimpiarTablasLocalesAsync()
        {
            try
            {
                if (folioFle == null)
                {
                    // Si no hay folio, solo limpia colecciones en memoria
                    Pasajeros.Clear();
                    allPasajeros.Clear();
                    UpdateTotalPasajeros();
                    return;
                }

                // Elimina solo los items del viaje actual (usando el nuevo método)
                var deletedDet = await _databaseService.DeleteByPredicateAsync<Tb_FlePer_DetFlete>(p => p.IdFletePer == folioFle);
                var deletedFlete = await _databaseService.DeleteByPredicateAsync<Tb_FlePer_FletePersonal>(f => f.IdFletePer == folioFle);

                // Limpia colecciones en memoria
                Pasajeros.Clear();
                allPasajeros.Clear();
                UpdateTotalPasajeros();

                var toast = CommunityToolkit.Maui.Alerts.Toast.Make($"Datos del viaje limpiados: {deletedDet} pasajeros y {deletedFlete} fletes eliminados.", ToastDuration.Short);
                await toast.Show();
            }
            catch (Exception ex)
            {
                var toast = CommunityToolkit.Maui.Alerts.Toast.Make($"Error al limpiar tablas locales: {ex.Message}", ToastDuration.Long);
                await toast.Show();
            }
        }


        #region GUARDAR CONSULTAS EN TXT
        private async Task GuardarCadenaAsync(Tb_FlePer_DetFlete pasajero, string nombreArchivo = "Pasajeros.txt")
        {
            try
            {
                string directorio = FileSystem.Current.AppDataDirectory;
                string rutaCompleta = Path.Combine(directorio, nombreArchivo);
                bool archivoExiste = File.Exists(rutaCompleta);
                using (StreamWriter writer = new StreamWriter(rutaCompleta, append: true))
                {
                    if (!archivoExiste)
                    {
                        await writer.WriteLineAsync("Fecha,Hora,Nómina,Latitud,Longitud,Sincronizado");
                        await writer.WriteLineAsync(new string('-', 80));
                    }
                    string linea = $"{DateTime.Now:yyyy-MM-dd},{DateTime.Now:HH:mm:ss}," +
                                  $"{pasajero.FlePer_CveNomina}," +
                                  $"{pasajero.FlePer_Latitud}," +
                                  $"{pasajero.FlePer_Longitud}," +
                                  $"{pasajero.IsSynced}";
                    await writer.WriteLineAsync(linea);
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error", $"Error al guardar en archivo: {ex.Message}", "OK");
            }
        }
        #endregion

        #region PASAJEROS FILTRADOS
        [ObservableProperty]
        private ObservableCollection<Tb_FlePer_DetFlete> pasajerosFiltrados;

        partial void OnPasajerosChanged(ObservableCollection<Tb_FlePer_DetFlete> value)
        {
            PasajerosFiltrados = new ObservableCollection<Tb_FlePer_DetFlete>(value ?? new());
            HasPasajeros = (value?.Count ?? 0) > 0;
        }

        public void FilterPasajeros(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                PasajerosFiltrados = new ObservableCollection<Tb_FlePer_DetFlete>(allPasajeros);
            }
            else
            {
                //Pasajeros = new ObservableCollection<Tb_FlePer_DetFlete>(
                //    allPasajeros.Where(p => p.FlePer_CveNomina.ToString().Contains(query)));
                var filtrados = allPasajeros
                    .Where(p => p.FlePer_CveNomina.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                PasajerosFiltrados = new ObservableCollection<Tb_FlePer_DetFlete>(filtrados);
            }
        }
        #endregion
        #region ENTRADA MANUAL
        [RelayCommand]
        private async Task AddManualAsync()
        {
            if (string.IsNullOrWhiteSpace(ManualEntryText)) return;

            await ProcessBarcodeValueAsync(ManualEntryText.Trim());
            ManualEntryText = string.Empty;
        }

        [ObservableProperty]
        private string manualEntryText;
        #endregion
    }
}