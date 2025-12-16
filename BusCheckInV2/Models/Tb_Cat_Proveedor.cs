using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace BusCheckInV2.Models
{
    public class Tb_Cat_Proveedor
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }  // ID local, no sincronizado

        public string prov_clave { get; set; }
        public string prov_nombre { get; set; }

        public bool IsSynced { get; set; } = false;  // Campo para sincronización
    }
}
