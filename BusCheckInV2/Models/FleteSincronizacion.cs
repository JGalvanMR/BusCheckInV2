using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusCheckInV2.Models
{
    public class FleteSincronizacion
    {
        public int IdFletePer { get; set; }
        public DateTime Fecha { get; set; }
        public TimeSpan Hora { get; set; }
        public string ProvClave { get; set; }
        public int IdDestFlete { get; set; }
        public string TipoFlete { get; set; }
        public string TipoViaje { get; set; }
        public int Cantidad { get; set; }
        public string Status { get; set; }
        public string Chofer { get; set; }
        public int? CantidadReal { get; set; }
        public string Observaciones { get; set; }
        public bool IsSynced { get; set; }
    }
}
