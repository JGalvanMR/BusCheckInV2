using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusCheckInV2.Models
{
    public class DetFleteRequest
    {
        public int IdFletePer { get; set; }
        public int FlePer_CveNomina { get; set; }
        public double FlePer_Latitud { get; set; }
        public double FlePer_Longitud { get; set; }
        public DateTime FlePer_Fecha { get; set; }
        public string? FlePer_Nombre { get; set; }
    }
}
