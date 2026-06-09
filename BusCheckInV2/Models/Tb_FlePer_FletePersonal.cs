using SQLite;
using System;
using System.ComponentModel.DataAnnotations;

namespace BusCheckInV2.Models
{
    /// <summary>
    /// Modelo que mapea exactamente la tabla Tb_FlePer_FletePersonal de SQL Server
    /// Campos con [Ignore] son solo para uso local en SQLite
    /// </summary>
    public class Tb_FlePer_FletePersonal
    {
        // ID LOCAL para SQLite (PrimaryKey de la tabla SQLite)
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // ID del servidor (IDENTITY de SQL Server)
        [Column("IdFletePer")]
        public long? IdFletePer { get; set; }

        [Column("FlePer_Fecha")]
        public DateTime? Fecha { get; set; }

        [Column("FlePer_Hora")]
        public TimeSpan? Hora { get; set; }

        [Column("Prov_Clave"), SQLite.MaxLength(10)]
        public string? ProvClave { get; set; }

        [Column("IdDestFlete")]
        public long? IdDestFlete { get; set; }

        [Column("FlePer_Area"), SQLite.MaxLength(10)]
        public string? Area { get; set; }

        [Column("FlePer_Turno"), SQLite.MaxLength(10)]
        public string? Turno { get; set; }

        [Column("FlePer_TipoFlete"), SQLite.MaxLength(15)]
        public string? TipoFlete { get; set; } = "NORMAL";

        [Column("FlePer_TipoViaje"), SQLite.MaxLength(15)]
        public string? TipoViaje { get; set; } = "TRAER GENTE";

        [Column("FlePer_Cantidad")]
        public int? Cantidad { get; set; } = 0;

        [Column("FlePer_Costo")]
        public decimal? Costo { get; set; }

        [Column("FlePer_Status"), SQLite.MaxLength(1)]
        public string? Status { get; set; } = "P";

        [Column("FlePer_Captura"), SQLite.MaxLength(15)]
        public string? Captura { get; set; }

        [Column("FlePer_Semana")]
        public int? Semana { get; set; }

        [Column("FlePer_Chofer"), SQLite.MaxLength(30)]
        public string? Chofer { get; set; }

        [Column("FlePer_Correo"), SQLite.MaxLength(1)]
        public string? Correo { get; set; } = "N";

        [Column("FlePer_FechaFin")]
        public DateTime? FechaFin { get; set; }

        [Column("FlePer_Observaciones")]
        public string? Observaciones { get; set; }

        // Campo local para sincronización (NO existe en SQL Server)
        public bool IsSynced { get; set; } = false;

        // Propiedades calculadas solo para UI (no persisten)
        [Ignore]
        public string? NombreRuta { get; set; }

        [Ignore]
        public string? NombreProveedor { get; set; }
    }
}