using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusCheckInV2.Models
{
    public class UsuarioApi
    {
        public string Nombre { get; set; }
        public int TotalFletes { get; set; }
        public int FletesPendientes { get; set; }
    }
}
