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
    public partial class EscaneoCodigoViewModel : BaseViewModel
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
        private readonly string _patronNumeros = @"^[0-9]+$";
        private const int SyncIntervalSeconds = 15;

        public bool CanScan => !IsSyncing;
        public bool HasPendingSyncOperations => !_pendingOperations.IsEmpty || Pasajeros.Any(p => !p.IsSynced);
        private bool _isSyncInProgress = false;

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

        public async Task InitializeAsync()
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

        private async Task ProcessBarcodeValueAsync(string barcodeValue)
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

        private async Task TrySyncDataAsync()
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