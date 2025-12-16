using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BusCheckInV2.Models;
using Microsoft.Extensions.Logging;

namespace BusCheckInV2.Services
{
    public class ApiFleteService : IApiFleteService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiFleteService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiFleteService(HttpClient httpClient, ILogger<ApiFleteService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Configurar timeout y headers
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BusCheckInV2-MAUI");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        private string GetBaseUrl()
        {

            return "http://189.206.160.206:82/BusCheckInV2/api/BusCheckInV2";

#if DEBUG
            // Para desarrollo local
            // Android Emulator usa 10.0.2.2 para localhost
            //return "http://189.206.160.206:81/api/WSBusCheckInV2";

            // Para dispositivo físico (cambia por tu IP)
            // return "http://192.168.1.xxx:5000/api/WSBusCheckInV2";
#else
            // Para producción
            return "https://tudominio.com/api/WSBusCheckInV2";
#endif
        }

        public async Task<List<FletePendienteUI>> ObtenerFletesPorUsuarioAsync(int usuarioId, DateTime fechaDesde)
        {
            try
            {
                var url = $"{GetBaseUrl()}/ObtenerFletesPorUsuario?FlePer_Chofer={usuarioId}&fechaDesde={fechaDesde:dd/mm/yyyy}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var fletes = JsonSerializer.Deserialize<List<FletePendienteUI>>(content, _jsonOptions);
                    return fletes ?? new List<FletePendienteUI>();
                }

                _logger.LogError($"Error al obtener fletes: {response.StatusCode}");
                return new List<FletePendienteUI>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerFletesPorUsuarioAsync");
                return new List<FletePendienteUI>();
            }
        }

        public async Task<List<string>> ObtenerUsuariosAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{GetBaseUrl()}/ObtenerUsuarios");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<string>>(content, _jsonOptions) ?? new List<string>();
                }

                return new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerUsuariosAsync");
                return new List<string>();
            }
        }

        public async Task<bool> CancelarFleteAsync(int fleteId, string motivo)
        {
            try
            {
                var request = new { FleteId = fleteId, Motivo = motivo };
                var content = new StringContent(
                    JsonSerializer.Serialize(request, _jsonOptions),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync($"{GetBaseUrl()}/CancelarFlete", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CancelarFleteAsync");
                return false;
            }
        }

        public async Task<bool> ValidarYFinalizarFleteAsync(int fleteId, int cantidadPasajeros, string observaciones)
        {
            try
            {
                var request = new
                {
                    FleteId = fleteId,
                    CantidadPasajeros = cantidadPasajeros,
                    Observaciones = observaciones
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request, _jsonOptions),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync($"{GetBaseUrl()}/ValidarYFinalizarFlete", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ValidarYFinalizarFleteAsync");
                return false;
            }
        }

        // Métodos existentes de tu API (ya los tienes)
        public async Task<int> InsertarFletePersonal(FletePersonalRequest request)
        {
            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(request, _jsonOptions),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync($"{GetBaseUrl()}/InsertarFletePersonal", content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    return int.TryParse(result, out int id) ? id : -1;
                }
                return -1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en InsertarFletePersonal");
                return -1;
            }
        }

        // Implementa los otros métodos de forma similar...
        public Task<bool> InsertarDetFlete(DetFleteRequest request) => Task.FromResult(true);
        public Task<bool> InsertarInicioDetFlete(DetFleteRequest request) => Task.FromResult(true);
        public Task<bool> UpdateFletePersonal(UpdateFleteRequest request) => Task.FromResult(true);
        public Task<bool> InsertarFinDetFlete(DetFleteRequest request) => Task.FromResult(true);
        public Task<string> HelloWorld() => Task.FromResult("API Conectada");
    }
}