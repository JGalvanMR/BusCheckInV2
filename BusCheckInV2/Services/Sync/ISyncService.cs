using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusCheckInV2.Services.Sync
{
    public interface ISyncService
    {
        Task SyncDataAsync();
    }
}
