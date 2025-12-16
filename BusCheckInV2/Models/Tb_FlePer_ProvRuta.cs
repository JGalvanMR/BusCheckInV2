using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace BusCheckInV2.Models
{
    public class Tb_FlePer_ProvRuta
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }  // ID local, no sincronizado

        public decimal IdRutaProv { get; set; }
        public string? Prov_Clave { get; set; }
        public decimal? IdDestFlete { get; set; }
        public DateTime? FechaAlta { get; set; }
        public decimal? Costo { get; set; }
        public string RutaStatus { get; set; }

        public bool IsSynced { get; set; } = false;  // Campo para sincronización
    }
}
