using SQLite;
using System;

namespace BusCheckInV2.Models
{
    public class Tb_FlePer_DetFlete
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Column("IdFletePer")]
        public long? IdFletePer { get; set; }

        [Column("FlePer_CveNomina")]
        public int? CveNomina { get; set; }

        [Column("FlePer_Latitud")]
        public double? Latitud { get; set; }

        [Column("FlePer_Longitud")]
        public double? Longitud { get; set; }

        [Column("FlePer_Fecha")]
        public DateTime? Fecha { get; set; }

        // Campo local para sincronización
        public bool IsSynced { get; set; } = false;
    }
}