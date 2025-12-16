using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace BusCheckInV2.Models
{
    public class Tb_FlePer_FletePersonal
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }  // ID local, no sincronizado

        public int? IdFletePer { get; set; }
        public string? FlePer_Fecha { get; set; }
        public string? FlePer_Hora { get; set; }
        public string? Prov_Clave { get; set; }
        public int? IdDestFlete { get; set; }
        public string? FlePer_TipoFlete { get; set; }
        public string? FlePer_TipoViaje { get; set; }
        public string? FlePer_Chofer { get; set; }

        // NUEVOS CAMPOS para gestión de estado
        public string? FlePer_Status { get; set; } = "Pendiente"; // Pendiente, Iniciado, Completado, Cancelado, Inconcluso
        public int? FlePer_Cantidad { get; set; } = 0;
        public int? FlePer_CantidadReal { get; set; }
        public DateTime? FlePer_FechaInicio { get; set; }
        public DateTime? FlePer_FechaFin { get; set; }
        public string? FlePer_Observaciones { get; set; }

        public bool IsSynced { get; set; } = false;

        // Propiedades calculadas (no se persisten en DB)
        [Ignore]
        public string? NombreRuta { get; set; }
        [Ignore]
        public string? NombreProveedor { get; set; }
    }
}
