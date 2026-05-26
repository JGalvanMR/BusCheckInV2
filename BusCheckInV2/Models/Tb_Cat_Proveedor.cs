using SQLite;
using System.ComponentModel.DataAnnotations;

namespace BusCheckInV2.Models
{
    public class Tb_Cat_Proveedor
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Column("prov_clave"), SQLite.MaxLength(10), Indexed]
        public string? ProvClave { get; set; }

        [Column("prov_nombre"), SQLite.MaxLength(100)]
        public string? ProvNombre { get; set; }

        public bool IsSynced { get; set; } = false;
    }
}