using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusCheckInV2.Models
{
    public class SincronizacionResult
    {
        public int IdFletePer { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
