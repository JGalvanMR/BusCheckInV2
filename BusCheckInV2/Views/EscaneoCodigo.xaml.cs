using AndroidX.Lifecycle;
using BarcodeScanning;
using BusCheckInV2.ViewModels;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;
using static Android.App.Assist.AssistStructure;
using BusCheckInV2.Converters;

namespace BusCheckInV2.Views
{
    public partial class EscaneoCodigo : ContentPage
    {
        private CameraView _cameraView;
        private EscaneoCodigoViewModel? _viewModel;
        private bool _isCameraInitialized = false;
        public EscaneoCodigo(EscaneoCodigoViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
        }

        private void InitializeCamera()
        {
            try
            {
                if (_cameraView != null) return;

                _cameraView = new CameraView
                {
                    AimMode = true,
                    BarcodeSymbologies = BarcodeFormats.Code39 | BarcodeFormats.QRCode, // SOLO Code39 para mejor performance
                    CaptureQuality = CaptureQuality.Low, // CALIDAD BAJA para máxima velocidad
                    ForceInverted = false,
                    TapToFocusEnabled = false, // DESACTIVAR para mejor performance
                    VibrationOnDetected = true, // DESACTIVAR vibración
                    ViewfinderMode = true,
                    TorchOn = false,
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    VerticalOptions = LayoutOptions.FillAndExpand,
                    BackgroundColor = Colors.Black
                };

                _cameraView.OnDetectionFinished += OnBarcodeDetected;

                _cameraView.SetBinding(CameraView.CameraEnabledProperty,
                    new Binding(nameof(EscaneoCodigoViewModel.IsScanning), source: _viewModel));
                _cameraView.SetBinding(CameraView.TorchOnProperty,
                    new Binding(nameof(EscaneoCodigoViewModel.IsTorchOn), source: _viewModel));

                CameraContainer.Children.Insert(0, _cameraView);
                _isCameraInitialized = true;

                Console.WriteLine("Cámara inicializada correctamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error inicializando cámara: {ex}");
                _isCameraInitialized = false;
            }
        }

        private void CrearCameraView()
        {
            if (_cameraView != null)
            {
                CameraContainer.Children.Remove(_cameraView);
                _cameraView = null;
                _cameraView.IsEnabled = false;  // Detiene escaneo y torch
                _cameraView.CameraEnabled = false;  // Desactiva cámara nativa
                _cameraView.TorchOn = false;  // Apaga flash si está encendido
            }

            _cameraView = new CameraView
            {
                AimMode = true,
                BarcodeSymbologies = BarcodeFormats.Code39 | BarcodeFormats.QRCode,
                CaptureQuality = CaptureQuality.Highest,
                ForceInverted = false,
                TapToFocusEnabled = false,
                VibrationOnDetected = true,
                ViewfinderMode = true,
                TorchOn = false,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                VerticalOptions = LayoutOptions.FillAndExpand,
                BackgroundColor = Colors.Black
            };

            // Configurar eventos en lugar de bindings para mayor control
            _cameraView.OnDetectionFinished += OnBarcodeDetected;

            // Bindings directos al ViewModel
            _cameraView.SetBinding(CameraView.CameraEnabledProperty, new Binding(nameof(EscaneoCodigoViewModel.IsScanning), source: _viewModel));
            _cameraView.SetBinding(CameraView.TorchOnProperty, new Binding(nameof(EscaneoCodigoViewModel.IsTorchOn), source: _viewModel));

            // Añadimos al contenedor
            CameraContainer.Children.Insert(0, _cameraView);
        }

        private async void OnBarcodeDetected(object sender, OnDetectionFinishedEventArg e)
        {
            if (e?.BarcodeResults == null || e.BarcodeResults.Count == 0)
                return;

            try
            {
                await _viewModel.ProcessBarcodeResultsAsync(e.BarcodeResults);
            }
            catch (Exception ex)
            {
                // En caso de error fatal, detener escaneo y mostrar alerta
                _viewModel.IsScanning = false;
                DisplayAlert("Error", $"Error en escáner: {ex.Message}", "OK");
                return;
            }

        }

        #region Ciclo de Vida
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                if (_viewModel is null) return;
                AttachBarcodeReader();
                // Inicializar ViewModel PRIMERO
                await _viewModel.InitializeAsync();

                // Inicializar cámara SOLO si no está inicializada
                if (!_isCameraInitialized)
                {
                    InitializeCamera();
                }

                // Activar escaneo después de un breve delay
                await Task.Delay(1000);
                _viewModel.IsScanning = true;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error al inicializar: {ex.Message}", "OK");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            try
            {
                if (_viewModel is null) return;
                // Desactivar escaneo PERO NO LIBERAR CÁMARA completamente
                _viewModel.IsScanning = false;
                _viewModel.IsTorchOn = false;

                // Solo desactivar cámara, no liberar recursos
                if (_cameraView != null)
                {
                    _cameraView.CameraEnabled = false;
                }

                // Cleanup del ViewModel (pero no demasiado agresivo si hay datos pendientes)
                if (_viewModel.HasPendingSyncOperations)
                {
                    // Solo limpiar recursos no críticos, mantener datos pendientes
                    //_viewModel.CleanupNonCriticalResources();
                }
                else
                {
                    // Limpiar completamente
                    //_viewModel.Cleanup();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en OnDisappearing: {ex}");
            }
        }
        private void LiberarCameraCompletamente()
        {
            try
            {
                if (_cameraView != null)
                {
                    _cameraView.OnDetectionFinished -= OnBarcodeDetected;
                    _cameraView.CameraEnabled = false;
                    _cameraView.TorchOn = false;

                    if (CameraContainer.Children.Contains(_cameraView))
                    {
                        CameraContainer.Children.Remove(_cameraView);
                    }

                    _cameraView = null;
                    _isCameraInitialized = false;

                    Console.WriteLine("Cámara liberada completamente");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error liberando cámara: {ex}");
                _cameraView = null;
                _isCameraInitialized = false;
            }
        }
        // SOLO liberar cámara completamente cuando sea absolutamente necesario
        public void ForceCleanup()
        {
            LiberarCameraCompletamente();
            //_viewModel.Cleanup();
        }
        private void LiberarCamera()
        {
            if (_cameraView != null)
            {
                // Remover evento primero
                _cameraView.OnDetectionFinished -= OnBarcodeDetected;

                // Deshabilitar cámara
                _cameraView.CameraEnabled = false;
                _cameraView.TorchOn = false;
                _cameraView.IsEnabled = false;

                // Remover de UI y liberar
                CameraContainer.Children.Remove(_cameraView);
                _cameraView = null;
            }
        }
        private void ContentPage_Unloaded(object sender, EventArgs e)
        {
            // Si es necesario, desconecta handlers específicos de UI
            //LiberarCamera();
            _viewModel?.Dispose();
            _viewModel = null;
            BindingContext = null;
        }

        private void AttachBarcodeReader()
        {
            if (_viewModel is null) return;

            // Busca el Grid marcado x:Name="CameraContainer" en el XAML
            var container = this.FindByName<Grid>("CameraContainer");
            if (container is null) return;

            // Evita doble-inyección: si ya existe un CameraView no lo agreguemos de nuevo
            bool cameraYaExiste = container.Children
                .OfType<CameraView>()
                .Any();

            if (cameraYaExiste) return;

            // CameraView debe ser el primer hijo para que ocupe todo el espacio
            var cameraView = new CameraView
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            };

            // Binding: IsDetecting controla si la cámara analiza frames
            cameraView.SetBinding(CameraView.PauseScanningProperty,
    new Binding(nameof(_viewModel.IsScanning), converter: new InvertedBoolConverter()));


            // Binding: TorchOn para el flash
            cameraView.SetBinding(CameraView.TorchOnProperty,
                new Binding(nameof(_viewModel.IsTorchOn)));

            // Evento: cuando se detecta un barcode -> ProcessBarcodeResultsCommand
            cameraView.OnDetectionFinished += OnCameraDetectionFinished;

            // Insertar al inicio (index 0) para que quede detrás del flash button
            container.Insert(0, cameraView);
        }

        private async void OnCameraDetectionFinished(object? sender,
        OnDetectionFinishedEventArg e)
        {
            if (_viewModel is null) return;

            // ProcessBarcodeResultsCommand es un AsyncRelayCommand que ya tiene
            // guard interno (_isProcessingBarcode) para evitar re-entrancia.
            if (_viewModel.ProcessBarcodeResultsCommand.CanExecute(e.BarcodeResults))
                await _viewModel.ProcessBarcodeResultsCommand.ExecuteAsync(e.BarcodeResults);
        }

        protected override bool OnBackButtonPressed()
        {
            try
            {
                // Manejo síncrono inmediato - no podemos usar await aquí
                if (_viewModel.IsSyncing)
                {
                    // Usar BeginInvokeOnMainThread para operaciones async sin bloquear
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert(
                            "Sincronización en curso",
                            "No puedes salir mientras se están sincronizando datos. Espera a que termine la sincronización.",
                            "Entendido");
                    });
                    return true; // Bloquear retroceso
                }

                // Si hay datos pendientes, manejar de forma asíncrona
                if (_viewModel.HasPendingSyncOperations)
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await HandleBackButtonWithPendingData();
                    });
                    return true; // Bloquear temporalmente, la lógica async decidirá después
                }

                // Si hay pasajeros pero está todo sincronizado
                if (_viewModel.Pasajeros?.Count > 0)
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await HandleBackButtonWithPassengers();
                    });
                    return true; // Bloquear temporalmente
                }

                // No hay datos, permitir salida inmediata
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en OnBackButtonPressed: {ex}");
                // En caso de error, bloquear por seguridad
                return true;
            }
        }

        private async Task HandleBackButtonWithPendingData()
        {
            var result = await DisplayAlert(
                "Datos pendientes de sincronizar",
                $"Tienes {_viewModel.Pasajeros.Count} pasajeros registrados que no se han sincronizado completamente. " +
                "¿Estás seguro de que quieres salir? Los datos se guardaron localmente y se sincronizarán después.",
                "Sí, salir",
                "No, esperar");

            if (result)
            {
                // Usuario confirmó salir - intentar sincronización final rápida
                await HandleExitWithPendingData();

                // Navegar de regreso programáticamente
                await Shell.Current.Navigation.PopModalAsync();
            }
        }

        private async Task HandleBackButtonWithPassengers()
        {
            var result = await DisplayAlert(
                "Viaje en progreso",
                "Tienes pasajeros registrados. ¿Estás seguro de que quieres salir sin finalizar el viaje? " +
                "Puedes continuar más tarde.",
                "Sí, salir",
                "No, continuar");

            if (result)
            {
                // Navegar de regreso programáticamente
                await Shell.Current.Navigation.PopModalAsync();
            }
        }

        private async Task HandleExitWithPendingData()
        {
            try
            {
                // Mostrar indicador de progreso
                var loadingAlert = DisplayAlert(
                    "Sincronizando...",
                    "Guardando datos pendientes antes de salir.",
                    "Esperar");

                // Intentar una sincronización rápida final
                var syncTask = _viewModel.TryFinalSyncBeforeExit();

                // Esperar máximo 5 segundos para la sincronización (más corto para UX)
                var completedTask = await Task.WhenAny(syncTask, Task.Delay(5000));

                await loadingAlert; // Cerrar el alert anterior

                if (completedTask == syncTask && await syncTask)
                {
                    await DisplayAlert("Éxito", "Datos sincronizados correctamente.", "OK");
                }
                else
                {
                    await DisplayAlert(
                        "Advertencia",
                        "No se pudieron sincronizar todos los datos. Los datos se guardaron localmente.",
                        "Entendido");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en sincronización final: {ex}");
                await DisplayAlert(
                    "Información",
                    "Los datos se guardaron localmente. Se sincronizarán automáticamente cuando haya conexión.",
                    "Entendido");
            }
        }

        private async Task HandleExitWithPendingDataOG()
        {
            try
            {
                // Mostrar indicador de progreso
                var loadingTask = DisplayAlert(
                    "Sincronizando...",
                    "Guardando datos pendientes antes de salir.",
                    "Esperar");

                // Intentar una sincronización rápida final
                var syncTask = _viewModel.TryFinalSyncBeforeExit();

                // Esperar máximo 10 segundos para la sincronización
                var completedTask = await Task.WhenAny(syncTask, Task.Delay(10000));

                if (completedTask == syncTask && await syncTask)
                {
                    await loadingTask; // Cerrar el alert anterior
                    await DisplayAlert(
                        "Éxito",
                        "Datos sincronizados correctamente. Puedes salir.",
                        "OK");
                }
                else
                {
                    await loadingTask; // Cerrar el alert anterior
                    await DisplayAlert(
                        "Advertencia",
                        "No se pudieron sincronizar todos los datos. Los datos se guardaron localmente y se sincronizarán cuando haya conexión.",
                        "Entendido");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en sincronización final: {ex}");
                await DisplayAlert(
                    "Información",
                    "Los datos se guardaron localmente. Se sincronizarán automáticamente cuando haya conexión.",
                    "Entendido");
            }
        }
        #endregion

        #region UI y Barcode
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            if (BindingContext is EscaneoCodigoViewModel vm)
            {
                vm.FilterPasajeros(e.NewTextValue);
            }
        }
        private async void OnAgregarManualClicked(object sender, EventArgs e)
        {
            if (BindingContext is EscaneoCodigoViewModel vm && !string.IsNullOrWhiteSpace(ManualEntry.Text))
            {
                try
                {
                    await vm.AddManualAsync();
                    // Solo limpiar si fue exitoso (ahora se maneja en el ViewModel)
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Error al agregar manualmente: {ex.Message}", "OK");
                }
            }
            else
            {
                await DisplayAlert("Error", "Ingrese un código válido", "OK");
            }
        }
        #endregion
    }
}