using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusCheckInV2.Services.Database;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.Maui.Networking;
using BusCheckInV2.Models;

namespace BusCheckInV2.Services.Sync
{
    public class SyncService : ISyncService
    {
        private readonly IDatabaseService _databaseService;
        private readonly IHttpClientFactory _httpClientFactory;

        public SyncService(IDatabaseService databaseService, IHttpClientFactory httpClientFactory)
        {
            _databaseService = databaseService;
            _httpClientFactory = httpClientFactory;
        }

        public async Task SyncDataAsync()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                return;
            }

            var httpClient = _httpClientFactory.CreateClient();
            var registrosNoSincronizados = await _databaseService.ObtenerRegistrosNoSincronizadosAsync();

            foreach (var registro in registrosNoSincronizados)
            {
                var resultado = await SincronizarRegistroAsync(registro, httpClient);
                if (resultado)
                {
                    registro.IsSynced = true;
                    await _databaseService.ActualizarRegistroAsync(registro);
                }
            }
        }

        private async Task<bool> SincronizarRegistroAsync(Tb_FlePer_DetFlete registro, HttpClient httpClient)
        {
            var content = new StringContent(JsonConvert.SerializeObject(registro), Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync("https://your-server-url/api/your-endpoint", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
