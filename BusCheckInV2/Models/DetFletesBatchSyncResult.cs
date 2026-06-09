using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusCheckInV2.Models
{
    public class DetFletesBatchSyncResult
    {
        public bool Success { get; set; }
        public int TotalInsertados { get; set; }
        public List<int> LocalIdsFallidos { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}
