using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace BusCheckInV2.Models
{
    public class Tb_FlePer_DetFlete
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }  // ID local, no sincronizado

        public int? IdFletePer { get; set; }
        public int? FlePer_CveNomina { get; set; }
        public string? FlePer_Latitud { get; set; }
        public string? FlePer_Longitud { get; set; }
        public DateTime? FlePer_Fecha { get; set; }

        public bool IsSynced { get; set; } = false;  // Campo para sincronización
    }
}
