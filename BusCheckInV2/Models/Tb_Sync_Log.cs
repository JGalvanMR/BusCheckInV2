using SQLite;
using System;

namespace BusCheckInV2.Models
{
    /// <summary>
    /// Registro de cada intento de sincronización con el servidor.
    /// Solo existe en SQLite local — es una herramienta de diagnóstico.
    /// </summary>
    public class Tb_Sync_Log
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Fecha y hora del intento de sync.</summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>Tipo: "FLETE_PADRE" | "DETALLE_BATCH" | "SINCRONIZACION_FLETES"</summary>
        [SQLite.MaxLength(30)]
        public string TipoOperacion { get; set; } = string.Empty;

        /// <summary>IdFletePer local (antes de sync) o del servidor (después).</summary>
        public long? IdFletePer { get; set; }

        /// <summary>true si el servidor respondió 200 OK con Success=true.</summary>
        public bool Exitoso { get; set; }

        /// <summary>Mensaje de resultado o error.</summary>
        public string? Mensaje { get; set; }

        /// <summary>Cuántos registros se afectaron (ej: pasajeros en batch).</summary>
        public int RegistrosAfectados { get; set; }

        /// <summary>Duración de la llamada HTTP en ms.</summary>
        public int DuracionMs { get; set; }
    }
}