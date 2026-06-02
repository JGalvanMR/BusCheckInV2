using System;
using System.ComponentModel;

namespace BusCheckInV2.Models
{
    public class FletePendienteUI : INotifyPropertyChanged
    {
        private int _id;
        private int? _idFletePer;
        private string _ruta;
        private DateTime _fechaHora;
        private string _proveedor;
        private string _chofer;
        private string _estatus;
        private int? _cantidadEsperada;
        private int? _cantidadReal;
        private string _tipoFlete;
        private string _tipoViaje;
        private DateTime? _fechaInicio;
        private DateTime? _fechaFin;

        // FIX 2026-06-02 (Nivel 2 #19+#23):
        // Campo privado para EsPendiente. Lo hacemos settable para que
        // el FletesPendientesViewModel pueda asignarlo DIRECTAMENTE desde
        // el campo derivado que el backend ahora expone (f.EsPendiente).
        //
        // Antes EsPendiente era solo un getter que dependía de strings
        // ("Pendiente", "Iniciado", "Inconcluso") que el backend NUNCA
        // mandaba — el backend manda códigos de 1 char ('P','I','A','F','C').
        // Resultado: EsPendiente siempre era false en la UI aunque el
        // backend marcara el flete como pendiente → el botón "Cerrar
        // Flete" NUNCA aparecía.
        //
        // Nueva lógica alineada con el backend:
        //   EsPendiente = (Estatus en {P, I}) OR (TieneInicio y !TieneFin)
        //   PERO: el setter permite que el ViewModel lo sobreescriba
        //   con el valor derivado que viene del backend (más confiable).
        private bool _esPendiente;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public int? IdFletePer
        {
            get => _idFletePer;
            set { _idFletePer = value; OnPropertyChanged(); }
        }

        public string Ruta
        {
            get => _ruta;
            set { _ruta = value; OnPropertyChanged(); }
        }

        public DateTime FechaHora
        {
            get => _fechaHora;
            set { _fechaHora = value; OnPropertyChanged(); }
        }

        public string Proveedor
        {
            get => _proveedor;
            set { _proveedor = value; OnPropertyChanged(); }
        }

        public string Chofer
        {
            get => _chofer;
            set { _chofer = value; OnPropertyChanged(); }
        }

        public string Estatus
        {
            get => _estatus;
            set { _estatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(EsPendiente)); }
        }

        public int? CantidadEsperada
        {
            get => _cantidadEsperada;
            set { _cantidadEsperada = value; OnPropertyChanged(); }
        }

        public int? CantidadReal
        {
            get => _cantidadReal;
            set { _cantidadReal = value; OnPropertyChanged(); }
        }

        public string TipoFlete
        {
            get => _tipoFlete;
            set { _tipoFlete = value; OnPropertyChanged(); }
        }

        public string TipoViaje
        {
            get => _tipoViaje;
            set { _tipoViaje = value; OnPropertyChanged(); }
        }

        public DateTime? FechaInicio
        {
            get => _fechaInicio;
            set { _fechaInicio = value; OnPropertyChanged(); }
        }

        public DateTime? FechaFin
        {
            get => _fechaFin;
            set { _fechaFin = value; OnPropertyChanged(); }
        }

        // Propiedades calculadas
        //
        // FIX 2026-06-02: ahora settable y alineada con la lógica del
        // backend. Si el ViewModel asigna explícitamente (caso normal
        // desde la API), se respeta ese valor. Si nadie asigna, se
        // calcula como fallback usando la misma lógica del backend:
        //   - Estatus 'P' (Pendiente) o 'I' (Iniciado) → pendiente
        //   - Tiene inicio (FechaInicio) y no tiene fin (FechaFin) → pendiente
        //
        // Antes esta propiedad solo revisaba strings legacy
        // ("Pendiente", "Iniciado", "Inconcluso") que el backend nunca
        // manda, por lo que la UI nunca mostraba el botón "Cerrar Flete".
        public bool EsPendiente
        {
            get
            {
                // Si el ViewModel asignó explícitamente (caso normal),
                // usamos ese valor (es el derivado del backend).
                // Detectamos "asignado explícitamente" porque el setter
                // setea el flag _esPendiente.
                if (_esPendienteAsignado) return _esPendiente;

                // Fallback: lógica legacy alineada con backend
                if (!string.IsNullOrEmpty(Estatus))
                {
                    var s = Estatus.Trim();
                    if (s == "P" || s == "I" ||
                        s == "Pendiente" || s == "Iniciado" || s == "Inconcluso")
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
        private bool _esPendienteAsignado;

        public string DuracionViaje
        {
            get
            {
                if (FechaInicio.HasValue && FechaFin.HasValue)
                {
                    var duracion = FechaFin.Value - FechaInicio.Value;
                    return $"{duracion.Hours}h {duracion.Minutes}m";
                }
                return FechaInicio.HasValue ? "En curso..." : "No iniciado";
            }
        }

        public string DisplayInfo => $"{Ruta} - {FechaHora:dd/MM HH:mm}";

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}