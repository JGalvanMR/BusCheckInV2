using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace BusCheckInV2.Models
{
    public class Tb_FlePer_Ruta
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }  // ID local, no sincronizado

        public int? IdDestFlete { get; set; }
        public string NomDestFlete { get; set; }
        public int? FleteCant { get; set; }
        public decimal? FleteCosto { get; set; }
        public string DestStatus { get; set; }
        public int? RutaCupo { get; set; }
        public string RutaVehiculo { get; set; }

        public bool IsSynced { get; set; } = false;  // Campo para sincronización
    }
}
