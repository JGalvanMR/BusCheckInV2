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
        public bool EsPendiente => Estatus == "Pendiente" || Estatus == "Iniciado" || Estatus == "Inconcluso";

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