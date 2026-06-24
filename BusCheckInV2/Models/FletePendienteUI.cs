// BusCheckInV2/Models/FletePendienteUI.cs
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BusCheckInV2.Models
{
    /// <summary>
    /// Modelo de interfaz de usuario para un flete pendiente.
    /// Unifica la lógica de estado y pendiente para API y BD local.
    /// </summary>
    public partial class FletePendienteUI : ObservableObject
    {
        // ─── CAMPOS PRIVADOS PARA PROPIEDADES CALCULADAS ────────────────
        private bool _esPendiente;
        private bool _esPendienteAsignado;

        // ─── PROPIEDADES DE IDENTIFICACIÓN ──────────────────────────────
        [ObservableProperty]
        private int? _id;                      // ID local (SQLite)

        [ObservableProperty]
        private int? _idFletePer;              // ID del backend

        // ─── DATOS DEL VIAJE ─────────────────────────────────────────────
        [ObservableProperty]
        private string _ruta = string.Empty;

        [ObservableProperty]
        private DateTime _fechaHora;           // Fecha y hora planificada

        [ObservableProperty]
        private string _proveedor = string.Empty;

        [ObservableProperty]
        private string _chofer = string.Empty;

        [ObservableProperty]
        private string _estatus = string.Empty;   // Código corto (A, C, P, etc.)

        [ObservableProperty]
        private int _cantidadEsperada;

        [ObservableProperty]
        private string _tipoFlete = string.Empty;

        [ObservableProperty]
        private string _tipoViaje = string.Empty;

        // ─── DATOS DE EJECUCIÓN ──────────────────────────────────────────
        [ObservableProperty]
        private int? _cantidadReal;

        [ObservableProperty]
        private DateTime? _fechaInicio;

        [ObservableProperty]
        private DateTime? _fechaFin;

        [ObservableProperty]
        private int _cantPasajeros;            // Número de pasajeros reales (desde detalles)

        [ObservableProperty]
        private DateTime? _ultimaFechaDetalle; // Último escaneo registrado

        // ─── ESTADO CALCULADO (FUENTE DE VERDAD) ────────────────────────
        /// <summary>
        /// Estado textual calculado: "Activo", "En curso", "Pendiente", "Finalizado", "Cancelado".
        /// Se asigna desde el ViewModel (API) o desde SQLiteService (BD local con detalles).
        /// </summary>
        [ObservableProperty]
        private string _estadoCalculado = "Activo";

        // ─── PROPIEDAD CALCULADA: EsPendiente ───────────────────────────
        /// <summary>
        /// Indica si el flete está pendiente de acción del chofer.
        /// La lógica está centralizada aquí y usa EstadoCalculado como fuente primaria.
        /// Puede ser forzada externamente (ej. al finalizar) mediante el setter.
        /// </summary>
        public bool EsPendiente
        {
            get
            {
                // 1. Si se asignó explícitamente (desde el ViewModel, ej. al finalizar),
                //    respetar ese valor (permite ocultar el botón inmediatamente).
                if (_esPendienteAsignado)
                    return _esPendiente;

                // 2. Fuente principal: EstadoCalculado (recomendado).
                //    Unifica la lógica entre API y BD local.
                if (!string.IsNullOrEmpty(EstadoCalculado))
                {
                    var estado = EstadoCalculado.Trim();
                    return estado.Equals("Activo", StringComparison.OrdinalIgnoreCase)
                        || estado.Equals("En curso", StringComparison.OrdinalIgnoreCase)
                        || estado.Equals("Pendiente", StringComparison.OrdinalIgnoreCase);
                }

                // 3. Fallback legacy (seguridad): usar Estatus y fechas.
                //    Solo se ejecuta si EstadoCalculado no está disponible.
                if (!string.IsNullOrEmpty(Estatus))
                {
                    var s = Estatus.Trim();
                    if (s == "P" || s == "I" || s == "Pendiente" || s == "Iniciado")
                        return true;
                }

                bool tieneInicio = FechaInicio.HasValue && FechaInicio > DateTime.MinValue;
                bool tieneFin = FechaFin.HasValue && FechaFin > DateTime.MinValue;
                return tieneInicio && !tieneFin;
            }
            set
            {
                _esPendiente = value;
                _esPendienteAsignado = true;
                OnPropertyChanged();
            }
        }

        public Color ColorDiaSemana => ObtenerColorPorDia(FechaHora.DayOfWeek);

        private static Color ObtenerColorPorDia(DayOfWeek dia)
        {
            return dia switch
            {
                DayOfWeek.Monday => Color.FromArgb("#FF3B30"),  // Rojo
                DayOfWeek.Tuesday => Color.FromArgb("#FF9500"),  // Naranja
                DayOfWeek.Wednesday => Color.FromArgb("#FFCC00"),  // Amarillo
                DayOfWeek.Thursday => Color.FromArgb("#34C759"),  // Verde
                DayOfWeek.Friday => Color.FromArgb("#007AFF"),  // Azul
                DayOfWeek.Saturday => Color.FromArgb("#5856D6"),  // Índigo
                DayOfWeek.Sunday => Color.FromArgb("#AF52DE"),  // Violeta
                _ => Colors.Gray,
            };
        }

        public string DiaSemana => FechaHora > DateTime.MinValue ? FechaHora.ToString("ddd", new CultureInfo("es-ES")) : "---";

        // ─── PROPIEDAD CALCULADA: Duración del viaje ────────────────────
        public string DuracionViaje
        {
            get
            {
                if (!FechaInicio.HasValue)
                    return "Sin iniciar";
                var fin = FechaFin ?? DateTime.Now;
                var diff = fin - FechaInicio.Value;
                return $"{diff.Hours}h {diff.Minutes}m";
            }
        }

        // ─── MÉTODOS ESTÁTICOS DE CÁLCULO DE ESTADO ─────────────────────
        // (Se mantienen aquí para que la lógica esté centralizada en el modelo)

        /// <summary>
        /// Calcula el estado textual a partir de los datos resumidos de la API (FleteResponse).
        /// Útil cuando no se dispone de la lista completa de detalles.
        /// </summary>
        public static string CalcularEstadoDesdeApi(
            string estatus,
            int cantPasajeros,
            DateTime? fechaInicio,
            DateTime? fechaFin,
            DateTime? ultimaFechaDetalle)
        {
            string status = estatus?.Trim() ?? "A";
            bool tieneInicio = fechaInicio.HasValue && fechaInicio > DateTime.MinValue;
            bool tieneFin = fechaFin.HasValue && fechaFin > DateTime.MinValue;
            bool ultimas5h = ultimaFechaDetalle.HasValue &&
                             (DateTime.Now - ultimaFechaDetalle.Value).TotalHours < 5;

            if (status == "C" && cantPasajeros == 0)
                return "Cancelado";
            if (status == "C")
                return "Pendiente";
            if (status == "A" && tieneFin)
                return "Finalizado";
            if (status == "A" && tieneInicio && cantPasajeros > 0 && ultimas5h)
                return "En curso";
            if (status == "A" && tieneInicio && cantPasajeros > 0)
                return "Pendiente";
            if (status == "A" && tieneInicio)
                return "Pendiente";
            return "Activo";
        }

        /// <summary>
        /// Calcula el estado textual a partir de la lista completa de detalles (BD local).
        /// Usa la lógica de negocio completa: CveNomina 0 (inicio), 9999 (fin), etc.
        /// </summary>
        public static string CalcularEstadoDesdeDetalles(
            string estatus,
            IEnumerable<Tb_FlePer_DetFlete> detalles)
        {
            if (detalles == null)
                detalles = new List<Tb_FlePer_DetFlete>();

            var lista = detalles.ToList();

            int cantPasajeros = lista.Count(d =>
                d.CveNomina.HasValue && d.CveNomina != 0 && d.CveNomina != 9999);

            bool tieneInicio = lista.Any(d => d.CveNomina == 0);
            bool tieneFin = lista.Any(d =>
                d.CveNomina == 9999 && d.Nombre == "FIN");

            DateTime? ultimaFecha = lista
                .Where(d => d.Fecha.HasValue)
                .Select(d => d.Fecha!.Value)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            bool ultimas5h = ultimaFecha.HasValue &&
                             (DateTime.Now - ultimaFecha.Value).TotalHours < 5;

            string status = estatus?.Trim() ?? "A";

            if (status == "C" && cantPasajeros == 0)
                return "Cancelado";
            if (status == "C")
                return "Pendiente";
            if (status == "A" && tieneFin)
                return "Finalizado";
            if (status == "A" && tieneInicio && cantPasajeros > 0 && ultimas5h)
                return "En curso";
            if (status == "A" && tieneInicio && cantPasajeros > 0)
                return "Pendiente";
            if (status == "A" && tieneInicio)
                return "Pendiente";
            return "Activo";

        }
    }
}