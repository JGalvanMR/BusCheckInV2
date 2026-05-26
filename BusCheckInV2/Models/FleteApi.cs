using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusCheckInV2.Models
{
    public class FleteApi
    {
        public int IdFletePer { get; set; }
        public DateTime Fecha { get; set; }
        public TimeSpan Hora { get; set; }
        public string ProveedorClave { get; set; }
        public string ProveedorNombre { get; set; }
        public int IdDestFlete { get; set; }
        public string RutaNombre { get; set; }
        public string TipoFlete { get; set; }
        public string TipoViaje { get; set; }
        public int Cantidad { get; set; }
        public string Estatus { get; set; }
        public string Chofer { get; set; }
        public int? CantidadReal { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string Observaciones { get; set; }
        public int PuntosRegistrados { get; set; }
    }
}
