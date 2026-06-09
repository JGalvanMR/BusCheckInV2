using SQLite;
using System;

namespace BusCheckInV2.Models
{
    public class Tb_FlePer_DetFlete
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// ID del servidor (Tb_FlePer_FletePersonal.IdFletePer).
        /// Se establece con el ID local y se actualiza al sincronizar.
        /// </summary>
        [Column("IdFletePer")]
        public long? IdFletePer { get; set; }

        /// <summary>
        /// FK ESTABLE al PK local de Tb_FlePer_FletePersonal.Id.
        /// NUNCA cambia. Se usa para todas las consultas locales.
        /// sqlite-net-pcl agrega esta columna automáticamente en la migración.
        /// </summary>
        [Column("FleteLocalId")]
        public int FleteLocalId { get; set; }

        [Column("FlePer_CveNomina")]
        public int? CveNomina { get; set; }

        [Column("FlePer_Latitud")]
        public double? Latitud { get; set; }

        [Column("FlePer_Longitud")]
        public double? Longitud { get; set; }

        [Column("FlePer_Fecha")]
        public DateTime? Fecha { get; set; }

        /// <summary>"INICIO", "FIN" o null para empleados regulares.</summary>
        [Column("FlePer_Nombre")]
        public string? Nombre { get; set; }

        public bool IsSynced { get; set; } = false;

        /// <summary>Texto para mostrar en UI.</summary>
        [Ignore]
        public string DisplayText
        {
            get
            {
                if (Nombre == "INICIO") return "🚌  Inicio del viaje";
                if (Nombre == "FIN") return "🏁  Fin del viaje";
                if (CveNomina.HasValue)
                {
                    var s = CveNomina.Value.ToString();
                    return s.Length > 4
                        ? $"👤  Empleado ***{s.Substring(s.Length - 4)}"
                        : $"👤  Empleado {s}";
                }
                return "👤  Desconocido";
            }
        }
    }
}