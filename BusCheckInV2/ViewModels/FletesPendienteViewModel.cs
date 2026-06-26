// BusCheckInV2/ViewModels/FletesPendientesViewModel.cs
// Reemplaza el archivo completo

using BusCheckInV2.Models;
using BusCheckInV2.Services;
using BusCheckInV2.Views;
using BusCheckInV2.Views.Popups;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Networking;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.Maui.Graphics;
using System.Threading.Tasks;

namespace BusCheckInV2.ViewModels
{
    public partial class FletesPendientesViewModel : ObservableObject, IDisposable
    {
        private readonly ISQLiteService _databaseService;
        private readonly IApiFleteService _apiService;
        private readonly IAlertService _alertService;
        private readonly INavigationService _navigationService;
        private readonly CancellationTokenSource _gpsTokenSource = new();
        private bool _disposed;
        private bool _finalizandoEnCurso;
        [ObservableProperty]
        private string _tituloSemanaActual = string.Empty;

        // FIX 2026-06-01 (Problema #3b del usuario): previene que el botón
        // Actualizar y el pull-to-refresh del RefreshView disparen dos cargas
        // en paralelo. La primera ganaba el Clear() y la segunda le metía sus
        // mismos datos al ObservableCollection ya vacío, duplicando filas.
        private bool _cargaFletesEnCurso;

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

            // FIX 2026-06-01 (Problema #3a del usuario): auto-cargar fletes
            // del chofer en cuanto se selecciona. Antes había que pulsar
            // Actualizar, Sincronizar o deslizar hacia abajo obligatoriamente.
            // El check ListaUsuarios.Any() evita disparar la carga durante
            // inicializaciones extrañas (ej. durante el deserializado del VM).
            // _cargaFletesEnCurso evita loop si la carga dispara SelectedItem
            // de nuevo (no debería, pero por si acaso).
            if (!string.IsNullOrEmpty(ChoferSeleccionado) &&
                ListaUsuarios.Any() &&
                !_cargaFletesEnCurso)
            {
                _ = SafeAutoLoadAsync();
            }
        }

        // FIX 2026-06-01: wrapper fire-and-forget seguro para la auto-carga.
        // OnUsuarioSeleccionadoChanged es void (lo genera el toolkit), así que
        // no podemos await directamente; pero tampoco queremos tragarnos
        // excepciones silenciosas como antes.
        private async Task SafeAutoLoadAsync()
        {
            try
            {
                await CargarFletesPendientesAsync();
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    MensajeEstado = $"Error auto-carga: {ex.Message}";
                });
            }
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
            var (inicioSemana, finSemana) = ObtenerSemanaActual();
            var cultura = new System.Globalization.CultureInfo("es-ES");
            // Aplicamos la misma lógica de formato con control de cambio de mes
            if (inicioSemana.Month == finSemana.Month)
            {
                TituloSemanaActual = $"📅 {inicioSemana:dd} AL {finSemana:dd} DE {inicioSemana.ToString("MMMM yyyy", cultura)}".ToUpper();
            }
            else
            {
                // Si cruza de mes (Ej: 29 DE MARZO AL 04 DE ABRIL 2026)
                TituloSemanaActual = $"📅 {inicioSemana.ToString("dd 'DE' MMMM", cultura)} AL {finSemana.ToString("dd 'DE' MMMM yyyy", cultura)}".ToUpper();
            }
            // ChoferSeleccionado se actualiza automáticamente desde UsuarioSeleccionado
            if (string.IsNullOrEmpty(ChoferSeleccionado))
            {
                MensajeEstado = "Seleccione un chofer primero";
                return;
            }

            // FIX 2026-06-01 (Problema #3b): anti-carrera. Si ya hay una
            // carga en curso (botón Actualizar + pull-to-refresh simultáneos,
            // o auto-load durante carga manual), la segunda llamada sale
            // inmediatamente para no duplicar ni corromper el ObservableCollection.
            if (_cargaFletesEnCurso) return;
            _cargaFletesEnCurso = true;

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
                        fletes = apiResponse.Data.Select(f =>
                        {
                            // FIX 2026-06-04 (Problema fecha 01/01/0001):
                            // El backend emite DOS campos separados:
                            //   - f.Fecha:  "02/06/2026 12:00:00 a. m."  (cultura es-MX con sufijo AM/PM)
                            //   - f.Hora:   "09:18:02"                       (HH:mm:ss, sin fecha)
                            //
                            // El nombre del campo engaña: "Fecha" NO es solo la
                            // fecha, es un DATETIME completo en formato es-MX.
                            // Y "Hora" NO es la hora de creación, es la hora
                            // del día que el chofer tenía planificada.
                            //
                            // La fecha/hora que el chofer quiere VER en la
                            // lista es: la fecha planificada (de f.Fecha) + la
                            // hora planificada (de f.Hora).
                            //
                            // Ejemplo real del JSON del usuario:
                            //   f.Fecha = "02/06/2026 12:00:00 a. m."  → tomar solo "02/06/2026"
                            //   f.Hora  = "09:18:02"                   → concatenar
                            //   Resultado: "02/06/2026 09:18:02"        → parsear OK
                            //
                            // ANTES concatenaba todo y el sufijo "a. m."
                            // rompía el parseo, devolviendo MinValue y
                            // mostrándose 01/01/0001 00:00 en la UI.
                            DateTime fechaHora = DateTime.MinValue;

                            // Estrategia 1: extraer solo la parte de fecha
                            // de f.Fecha (primeros 10 chars = "dd/MM/yyyy")
                            // y concatenar con f.Hora. Parsear con cultura
                            // es-MX para entender el sufijo AM/PM si quedó.
                            string soloFecha = f.Fecha ?? string.Empty;
                            if (soloFecha.Length >= 10)
                                soloFecha = soloFecha.Substring(0, 10);

                            string combinadoLimpio = $"{soloFecha} {f.Hora ?? ""}".Trim();

                            if (!string.IsNullOrWhiteSpace(combinadoLimpio))
                            {
                                // Cultura es-MX entiende "dd/MM/yyyy" + "HH:mm:ss"
                                var esMX = CultureInfo.GetCultureInfo("es-MX");
                                if (!DateTime.TryParse(combinadoLimpio, esMX,
                                        DateTimeStyles.AssumeLocal, out fechaHora))
                                {
                                    // Fallback 1: InvariantCulture (ISO 8601)
                                    if (!DateTime.TryParse(combinadoLimpio,
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.AssumeLocal, out fechaHora))
                                    {
                                        // Fallback 2: formatos exactos conocidos
                                        string[] formatos = {
                                            "dd/MM/yyyy HH:mm:ss",
                                            "dd/MM/yyyy HH:mm",
                                            "yyyy-MM-dd HH:mm:ss",
                                            "yyyy-MM-dd HH:mm",
                                            "yyyy-MM-dd",
                                            "yyyy-MM-ddTHH:mm:ss"
                                        };
                                        DateTime.TryParseExact(combinadoLimpio, formatos,
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.AssumeLocal,
                                            out fechaHora);
                                    }
                                }
                            }

                            return new FletePendienteUI
                            {
                                IdFletePer = f.IdFletePer,
                                Ruta = f.RutaNombre,
                                FechaHora = fechaHora,
                                Proveedor = f.ProveedorNombre,
                                Chofer = f.Chofer,
                                Estatus = f.Estatus,
                                CantidadEsperada = f.Cantidad,
                                TipoFlete = f.TipoFlete,
                                TipoViaje = f.TipoViaje,
                                CantidadReal = f.CantidadReal,
                                FechaInicio = f.FechaInicio,
                                FechaFin = f.FechaFin,
                                // FIX 2026-06-03 (Opción A): el backend ahora
                                // expone EstadoCalculado (uno de: Activo,
                                // En curso, Pendiente, Finalizado, Cancelado).
                                // Lo usamos directamente si viene; si no, fallback
                                // a calcularlo localmente con los campos que SÍ
                                // deserializa FleteResponse.
                                EstadoCalculado = !string.IsNullOrWhiteSpace(f.EstadoCalculado)
                                    ? f.EstadoCalculado
                                    : CalcularEstadoDesdeFleteResponse(f),
                                CantPasajeros = f.CantPasajeros,
                                UltimaFechaDetalle = f.UltimaFechaDetalle,
                                // FIX 2026-06-02 (Nivel 2 #19+#23):
                                // El backend expone EsPendiente derivado,
                                // pero la clase FleteResponse del cliente
                                // (en BusCheckInV2/Models/) todavía no tiene
                                // esa propiedad → error CS1061 en compilación.
                                // Lo calculamos AQUÍ con los campos que SÍ
                                // deserializa FleteResponse (Estatus,
                                // FechaInicio, FechaFin). Es exactamente la
                                // misma lógica que el backend, así que el
                                // resultado es equivalente.
                                EsPendiente = !string.IsNullOrEmpty(f.Estatus)
                                    && (f.Estatus.Trim().Equals("P", StringComparison.OrdinalIgnoreCase)
                                        || f.Estatus.Trim().Equals("I", StringComparison.OrdinalIgnoreCase))
                                    || (f.FechaInicio.HasValue && f.FechaInicio > DateTime.MinValue
                                        && (!f.FechaFin.HasValue || f.FechaFin == DateTime.MinValue))
                            };
                        }).ToList();
                    }
                    else
                    {
                        fletes = await _databaseService.ObtenerFletesPendientesDesdeCacheAsync(
                            ChoferSeleccionado, DiasFiltro);
                        MensajeEstado = "API sin datos. Mostrando locales.";
                    }
                }
                else
                {
                    fletes = await _databaseService.ObtenerFletesPendientesDesdeCacheAsync(
                        ChoferSeleccionado, DiasFiltro);
                    MensajeEstado = "Modo offline — datos locales";
                }

                // Aplicar filtro de pendientes usando el helper robusto
                var fletesFiltrados = MostrarSoloPendientes
                    ? fletes.Where(EsFletePendiente).ToList()
                    : fletes;

                // FIX 2026-06-01 (Problema #3b): deduplicar por IdFletePer.
                // Si por algún motivo (doble click + pull, o cache local
                // que solapa con respuesta API tras reconexión) el mismo
                // flete viene dos veces, nos quedamos solo con el primero.
                var fletesUnicos = fletesFiltrados
                    .GroupBy(f => f.IdFletePer ?? 0)
                    .Select(g => g.First())
                    .OrderByDescending(f => f.FechaHora)
                    .ToList();

                foreach (var flete in fletesUnicos)
                    FletesPendientes.Add(flete);

                var total = FletesPendientes.Count;
                var pendientes = FletesPendientes.Count(EsFletePendiente);
                MensajeEstado = $"Mostrando {total} fletes ({pendientes} pendientes)";
            }
            catch (Exception ex)
            {
                MensajeEstado = $"Error: {ex.Message}";
            }
            finally
            {
                EstaCargando = false;
                _cargaFletesEnCurso = false;
            }
        }

        // FIX 2026-06-03 (Opción A): simplificación del helper de pendiente.
        // Ahora la fuente de verdad es EstadoCalculado (string fino que
        // viene del backend o se calcula localmente en SQLiteService).
        // Un flete es "pendiente de acción del chofer" si su estado es
        // Activo / En curso / Pendiente. NO si es Finalizado o Cancelado.
        private static bool EsFletePendiente(FletePendienteUI f)
        {
            if (f == null) return false;

            // Camino 1 (preferido): usar EstadoCalculado si el modelo lo
            // tiene seteado (que debería ser el caso normal post-fix).
            var estado = f.EstadoCalculado?.Trim() ?? "";
            if (!string.IsNullOrEmpty(estado))
            {
                return estado.Equals("Activo", StringComparison.OrdinalIgnoreCase)
                    || estado.Equals("En curso", StringComparison.OrdinalIgnoreCase)
                    || estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase);
            }

            // Camino 2 (fallback): calcular con Estatus (códigos 1 char).
            // Compatible con versiones viejas del backend que no mandan
            // EstadoCalculado.
            bool estatusP = !string.IsNullOrEmpty(f.Estatus) &&
                            (f.Estatus.Trim().Equals("P", StringComparison.OrdinalIgnoreCase) ||
                             f.Estatus.Trim().Equals("I", StringComparison.OrdinalIgnoreCase) ||
                             f.Estatus.Trim().Equals("A", StringComparison.OrdinalIgnoreCase));
            bool tieneInicio = f.FechaInicio.HasValue && f.FechaInicio > DateTime.MinValue;
            bool tieneFin = f.FechaFin.HasValue && f.FechaFin > DateTime.MinValue;

            return estatusP || (tieneInicio && !tieneFin);
        }

        // FIX 2026-06-03 (Opción A): helper de fallback para cuando el
        // backend NO manda EstadoCalculado (versión vieja). Replica la
        // misma lógica que SQLiteService.CalcularEstadoCalculado pero
        // con los datos que SÍ llegan en FleteResponse.
        private static string CalcularEstadoDesdeFleteResponse(FleteResponse f)
        {
            string status = f.Estatus?.Trim() ?? "A";
            int cantPasajeros = f.CantPasajeros;
            bool tieneInicio = f.FechaInicio.HasValue && f.FechaInicio > DateTime.MinValue;
            bool tieneFin = f.FechaFin.HasValue && f.FechaFin > DateTime.MinValue;
            bool ultimas5h = f.UltimaFechaDetalle.HasValue &&
                             (DateTime.Now - f.UltimaFechaDetalle.Value).TotalHours < 5;

            if (status == "C" && cantPasajeros == 0) return "Cancelado";
            if (status == "C") return "Pendiente";
            if (status == "A" && tieneFin) return "Finalizado";
            if (status == "A" && tieneInicio && cantPasajeros > 0 && ultimas5h) return "En curso";
            if (status == "A" && tieneInicio && cantPasajeros > 0) return "Pendiente";
            if (status == "A" && tieneInicio) return "Pendiente";
            return "Activo";
        }

        // ─── COMANDOS RESTANTES SIN CAMBIOS DE LÓGICA ────────────────────

        [RelayCommand]
        private async Task ValidarYFinalizarFleteAsyncOG(FletePendienteUI flete)
        {
            if (flete == null || _finalizandoEnCurso) return;
            _finalizandoEnCurso = true;

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

                // FIX (2026-06-01): se obtiene la posición real del GPS en lugar
                // de enviar 0,0 (Null Island). El backend inserta un punto "FIN"
                // con estas coordenadas, así que un (0,0) deja el viaje finalizado
                // en mitad del Atlántico en lugar de en la parada real del chofer.
                var (lat, lon) = await GetLatLonOrZeroAsync();

                var exito = HayConexionInternet && _apiService != null
                    ? await _apiService.ValidarYFinalizarFleteAsync(
                        flete.IdFletePer ?? 0, cantidadValidada, observaciones ?? "", lat, lon)
                    : true;

                if (exito)
                {
                    await _databaseService.ActualizarEstadoFleteAsync((int)flete.Id, "Completado", cantidadValidada, observaciones);
                    flete.Estatus = "Completado";
                    flete.CantidadReal = cantidadValidada;
                    flete.FechaFin = DateTime.Now;

                    // FIX: recalcular o forzar EsPendiente a false
                    // Opción A: si tienes setter en EsPendiente, haz flete.EsPendiente = false;
                    // Opción B: removerlo de la lista visible porque ya no es pendiente.
                    if (MostrarSoloPendientes)
                    {
                        FletesPendientes.Remove(flete);
                    }
                    else
                    {
                        flete.EsPendiente = false; // Asegúrate de que la propiedad tenga setter público.
                    }


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
                _finalizandoEnCurso = false;
            }
        }

        [RelayCommand]
        private async Task ValidarYFinalizarFleteAsync(FletePendienteUI flete)
        {
            if (flete == null || _finalizandoEnCurso) return;
            _finalizandoEnCurso = true;

            try
            {
                // 1. Verificar que el flete tenga ID del backend (indispensable)
                if (!flete.IdFletePer.HasValue)
                {
                    await _alertService.ShowAlertAsync("Error", "El flete no tiene ID en el servidor.");
                    return;
                }

                // 2. Pedir cantidad
                var cantidad = await _alertService.ShowPromptAsync(
                    "Validar Flete",
                    $"Ingrese cantidad real para:\n{flete.Ruta}\n(Esperados: {flete.CantidadEsperada})",
                    keyboard: Keyboard.Numeric);

                if (string.IsNullOrEmpty(cantidad) || !int.TryParse(cantidad, out int cantidadValidada))
                    return;

                var observaciones = await _alertService.ShowPromptAsync(
                    "Observaciones",
                    "Ingrese observaciones (opcional):",
                    placeholder: "Observaciones del viaje...");

                var confirmar = await _alertService.ShowConfirmationAsync(
                    "Confirmar Finalización",
                    $"¿Finalizar flete con {cantidadValidada} pasajeros?\n{flete.Ruta}");

                if (!confirmar) return;

                EstaCargando = true;

                // 3. Asegurar que el flete tiene ID local (crearlo si no existe)
                if (!flete.Id.HasValue)
                {
                    // Intentar buscar en BD local por IdFletePer
                    var localId = await _databaseService.ObtenerIdLocalPorIdFletePerAsync(flete.IdFletePer.Value);
                    if (localId.HasValue)
                    {
                        flete.Id = localId.Value;
                    }
                    else
                    {
                        // Insertar nuevo registro local desde el objeto UI
                        var nuevoId = await _databaseService.InsertarFleteDesdeUIAsync(flete);
                        if (nuevoId > 0)
                            flete.Id = nuevoId;
                        else
                        {
                            await _alertService.ShowAlertAsync("Error", "No se pudo guardar el flete localmente.");
                            return;
                        }
                    }
                }

                // 4. Obtener GPS
                var (lat, lon) = await GetLatLonOrZeroAsync();

                // 5. Finalizar en servidor (si hay conexión)
                bool exito = true;
                if (HayConexionInternet && _apiService != null)
                {
                    exito = await _apiService.ValidarYFinalizarFleteAsync(
                        flete.IdFletePer.Value,
                        cantidadValidada,
                        observaciones ?? "",
                        lat,
                        lon);
                }

                if (exito)
                {
                    // 6. Actualizar BD local (usando Id local ya seguro)
                    await _databaseService.ActualizarEstadoFleteAsync(
                        flete.Id.Value,
                        "Completado",
                        cantidadValidada,
                        observaciones);

                    // 7. Actualizar objeto UI
                    flete.Estatus = "Completado";
                    flete.CantidadReal = cantidadValidada;
                    flete.FechaFin = DateTime.Now;
                    flete.EstadoCalculado = "Finalizado";
                    flete.EsPendiente = false;

                    // 8. Si estamos en "Solo pendientes", remover de la lista
                    if (MostrarSoloPendientes)
                        FletesPendientes.Remove(flete);

                    await _alertService.ShowAlertAsync(
                        "Éxito",
                        HayConexionInternet
                            ? "Flete finalizado en servidor y localmente"
                            : "Flete finalizado localmente (se sincronizará después)");
                }
                else
                {
                    await _alertService.ShowAlertAsync("Error", "No se pudo finalizar el flete en el servidor.");
                }
            }
            catch (Exception ex)
            {
                await _alertService.ShowAlertAsync("Error", $"Error al finalizar: {ex.Message}");
            }
            finally
            {
                EstaCargando = false;
                _finalizandoEnCurso = false;
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
                await _alertService.ShowAlertAsync("Sincronización", $"Completada. {sincronizados} fletes sincronizados");

                // NUEVO: Recargar fletes automáticamente
                if (!_cargaFletesEnCurso)
                {
                    await CargarFletesPendientesAsync();
                }
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
        private async Task VerDetalleFleteAsyncOG(FletePendienteUI flete)
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
        private async Task VerDetalleFleteAsync(FletePendienteUI flete)
        {
            if (flete == null) return;

            var popup = new FleteDetallePopup(flete);
            // No pasar PopupOptions para usar el overlay oscuro por defecto
            await Application.Current.MainPage.ShowPopupAsync(popup);
        }

        [RelayCommand]
        private async Task VolverAsync() => await _navigationService.GoBackAsync();

        // FIX (2026-06-01): helper para no enviar 0,0 al backend.
        // Devuelve (0,0) solo si el GPS no responde (sin permiso, sin señal, etc.)
        // para mantener la firma de la API; pero al menos el caso normal
        // (GPS disponible) registra la posición real del cierre.
        private async Task<(double Lat, double Lon)> GetLatLonOrZeroAsync()
        {
            try
            {
                var req = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8));
                var loc = await Geolocation.GetLocationAsync(req, _gpsTokenSource.Token);
                if (loc != null)
                    return (loc.Latitude, loc.Longitude);
            }
            catch
            {
                // Permiso denegado, sin hardware, timeout, etc. Caer a (0,0)
                // es aceptable: el backend registrará el FIN con timestamp.
            }
            return (0, 0);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;
            // FIX (2026-06-01): cancelar y liberar el token del GPS para que
            // cualquier Geolocation.GetLocationAsync en curso se detenga limpio
            // cuando el ViewModel sea disposed por DI al cerrar la app.
            try
            {
                _gpsTokenSource.Cancel();
                _gpsTokenSource.Dispose();
            }
            catch { /* ya disposed, ignorar */ }
        }

        // ─── NUEVO MÉTODO: Obtener semana actual (Lunes a Domingo) ────────
        private (DateTime Inicio, DateTime Fin) ObtenerSemanaActual()
        {
            var hoy = DateTime.Now.Date;
            int diff = (int)hoy.DayOfWeek - (int)DayOfWeek.Monday;
            if (diff < 0) diff += 7;
            DateTime lunes = hoy.AddDays(-diff);
            DateTime domingo = lunes.AddDays(7).AddSeconds(-1);
            return (lunes, domingo);
        }

        private DateTime ObtenerInicioSemana(DateTime fecha)
        {
            int diff = (int)fecha.DayOfWeek - (int)DayOfWeek.Monday;
            if (diff < 0) diff += 7;
            return fecha.AddDays(-diff).Date;
        }
        private DateTime ParseFechaHora(string fechaStr, string horaStr)
        {
            if (DateTime.TryParse($"{fechaStr} {horaStr}", out var result))
                return result;
            return DateTime.Now;
        }
        private string ObtenerTituloSemana(DateTime inicio)
        {
            var fin = inicio.AddDays(6);
            //return $"📅 \n Semana del {inicio:dd/MM/yyyy} al {fin:dd/MM/yyyy}";
            // Se adaptará automáticamente según la cultura del dispositivo
            return $"📅 Semana del {inicio:d} al {fin:d}";
        }
    }
    public class GrupoFlete : ObservableCollection<FletePendienteUI>
    {
        public string Titulo { get; set; }

        public GrupoFlete(string titulo, IEnumerable<FletePendienteUI> items) : base(items)
        {
            Titulo = titulo;
        }
    }
}