using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Threading;
using Microsoft.Maui.Networking;

namespace BusCheckInV2.Services.Sync
{
    public class SyncBackgroundService : BackgroundService
    {
        private readonly ISyncService _syncService;

        public SyncBackgroundService(ISyncService syncService)
        {
            _syncService = syncService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    await _syncService.SyncDataAsync();
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Espera 5 minutos entre sincronizaciones
            }
        }
    }
}
