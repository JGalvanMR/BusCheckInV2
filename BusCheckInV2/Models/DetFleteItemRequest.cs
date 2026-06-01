using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusCheckInV2.Models
{
    public class DetFleteItemRequest
    {
        public int FlePer_CveNomina { get; set; }
        public double FlePer_Latitud { get; set; }
        public double FlePer_Longitud { get; set; }
        public DateTime FlePer_Fecha { get; set; }
        public int LocalId { get; set; }  // Id local en SQLite para saber cuál marcar
    }
}
