using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BusCheckInV2.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;

namespace BusCheckInV2.Services
{
    public class ApiFleteServiceReal : IApiFleteService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiFleteServiceReal> _logger;
        private readonly ISQLiteService _sqliteService;
        private readonly JsonSerializerOptions _jsonOptions;

        // Configuración
        private const string ApiBaseUrl = "http://189.206.160.206:5000"; // Cambia por tu IP
        private const string ApiPath = "/api/WSBusCheckInV2";

        // Para desarrollo con emulador Android
#if DEBUG
        private const string DevBaseUrl = "http://189.206.160.206:81";
#else
        private const string DevBaseUrl = "http://189.206.160.206:5000";
#endif

        public ApiFleteServiceReal(HttpClient httpClient, ILogger<ApiFleteServiceReal> logger, ISQLiteService sqliteService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _sqliteService = sqliteService;

            // Configurar HttpClient
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        private string GetBaseUrl()
        {
#if DEBUG
            return DevBaseUrl + ApiPath;
#else
            return ApiBaseUrl + ApiPath;
#endif
        }

        private async Task<T> ExecuteApiCallAsync<T>(Func<Task<HttpResponseMessage>> apiCall, string operationName)
        {
            try
            {
                // Verificar conexión
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    _logger.LogWarning($"Sin conexión para {operationName}");
                    return default;
                }

                var response = await apiCall();

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"API {operationName} exitoso");

                    if (string.IsNullOrEmpty(content))
                        return default;

                    return JsonSerializer.Deserialize<T>(content, _jsonOptions);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Error API {operationName}: {response.StatusCode} - {errorContent}");
                    return default;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción en {operationName}");
                return default;
            }
        }

        // Métodos de la API

        public async Task<List<UsuarioApi>> ObtenerUsuariosAsync()
        {
            var url = $"{GetBaseUrl()}/ObtenerUsuarios";
            var response = await ExecuteApiCallAsync<ApiResponse<List<UsuarioApi>>>(
                () => _httpClient.GetAsync(url), "ObtenerUsuarios");

            return response?.Success == true ? response.Data : new List<UsuarioApi>();
        }

        public async Task<ApiResponse<List<FleteResponse>>> ObtenerFletesPorChoferAsync(
        string chofer,
        int dias = 3,
        bool soloPendientes = false)
        {
            try
            {
                // Construir parámetros de consulta
                var parameters = new Dictionary<string, string>
                {
                    ["chofer"] = chofer,
                    ["dias"] = dias.ToString(),
                    ["soloPendientes"] = soloPendientes.ToString().ToLower()
                };

                var queryString = new FormUrlEncodedContent(parameters).ReadAsStringAsync().Result;
                var url = $"{GetBaseUrl()}/ObtenerFletesPorChofer?{queryString}";

                _logger.LogInformation($"Llamando API: {url}");

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ApiResponse<List<FleteResponse>>>(
                        jsonResponse,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return result;
                }
                else
                {
                    _logger.LogError($"Error API: {response.StatusCode}");
                    return new ApiResponse<List<FleteResponse>>
                    {
                        Success = false,
                        Message = $"Error del servidor: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consumir el API de fletes");
                return new ApiResponse<List<FleteResponse>>
                {
                    Success = false,
                    Message = "Error de conexión con el servidor"
                };
            }
        }

        /*public async Task<List<FleteApi>> ObtenerFletesPorChoferAsync(string chofer, DateTime fechaDesde)
        {
            var url = $"{GetBaseUrl()}/ObtenerFletesPorChofer?chofer={Uri.EscapeDataString(chofer)}&dias=3";
            var response = await ExecuteApiCallAsync<ApiResponse<List<FleteApi>>>(
                () => _httpClient.GetAsync(url), "ObtenerFletesPorUsuario");

            if (response?.Success == true)
            {
                // Guardar en cache local
                await GuardarFletesEnCache(response.Data);
                return response.Data;
            }

            return new List<FleteApi>();
        }*/

        public async Task<bool> ValidarYFinalizarFleteAsync(int fleteId, int cantidadPasajeros, string observaciones, double latitud, double longitud)
        {
            var url = $"{GetBaseUrl()}/ValidarYFinalizarFlete";
            var request = new
            {
                IdFletePer = fleteId,
                CantidadReal = cantidadPasajeros,
                Observaciones = observaciones,
                Latitud = latitud,
                Longitud = longitud,
                Usuario = "App MAUI"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await ExecuteApiCallAsync<ApiResponse<object>>(
                () => _httpClient.PostAsync(url, content), "ValidarYFinalizarFlete");

            return response?.Success == true;
        }

        public async Task<bool> CancelarFleteAsync(int fleteId, string motivo, string detalles)
        {
            var url = $"{GetBaseUrl()}/CancelarFlete";
            var request = new
            {
                IdFletePer = fleteId,
                Motivo = motivo,
                Detalles = detalles,
                Usuario = "App MAUI"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await ExecuteApiCallAsync<ApiResponse<object>>(
                () => _httpClient.PostAsync(url, content), "CancelarFlete");

            return response?.Success == true;
        }

        public async Task<bool> ReanudarFleteAsync(int fleteId, double latitud, double longitud)
        {
            var url = $"{GetBaseUrl()}/ReanudarFlete";
            var request = new
            {
                IdFletePer = fleteId,
                Latitud = latitud,
                Longitud = longitud
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await ExecuteApiCallAsync<ApiResponse<object>>(
                () => _httpClient.PostAsync(url, content), "ReanudarFlete");

            return response?.Success == true;
        }

        public async Task<bool> SincronizarFletesAsync(List<FleteSincronizacion> fletes)
        {
            var url = $"{GetBaseUrl()}/SincronizarFletes";
            var content = new StringContent(
                JsonSerializer.Serialize(fletes, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await ExecuteApiCallAsync<ApiResponse<List<SincronizacionResult>>>(
                () => _httpClient.PostAsync(url, content), "SincronizarFletes");

            if (response?.Success == true)
            {
                // Actualizar estado de sincronización local
                foreach (var result in response.Data)
                {
                    if (result.Success)
                    {
                        await _sqliteService.MarcarComoSincronizadoAsync(result.IdFletePer);
                    }
                }
                return true;
            }

            return false;
        }

        public async Task<bool> VerificarConexionAsync()
        {
            var url = $"{GetBaseUrl()}/VerificarConexion";
            var response = await ExecuteApiCallAsync<ApiResponse<object>>(
                () => _httpClient.GetAsync(url), "VerificarConexion");

            return response?.Success == true;
        }

        // Métodos de caché local
        private async Task GuardarFletesEnCache(List<FleteApi> fletes)
        {
            try
            {
                foreach (var flete in fletes)
                {
                    // Parsear fecha/hora de la API (viene como string)
                    DateTime fecha = DateTime.TryParse(flete.Fecha, out var f) ? f : DateTime.Now;
                    TimeSpan hora = TimeSpan.TryParse(flete.Hora, out var h) ? h : TimeSpan.Zero;

                    // Convertir de FleteApi a Tb_FlePer_FletePersonal
                    var fleteLocal = new Tb_FlePer_FletePersonal
                    {
                        IdFletePer = flete.IdFletePer,              // long? 
                        Fecha = fecha,                              // DateTime? (mapeado a FlePer_Fecha)
                        Hora = hora,                                // TimeSpan? (mapeado a FlePer_Hora)
                        ProvClave = flete.ProveedorClave,           // string? (mapeado a Prov_Clave)
                        IdDestFlete = flete.IdDestFlete,            // long? (mapeado a IdDestFlete)
                        TipoFlete = flete.TipoFlete,                // string? (mapeado a FlePer_TipoFlete)
                        TipoViaje = flete.TipoViaje,                // string? (mapeado a FlePer_TipoViaje)
                        Cantidad = flete.Cantidad,                  // int? (mapeado a FlePer_Cantidad)
                        Status = flete.Estatus,                     // string? (mapeado a FlePer_Status)
                        Chofer = flete.Chofer,                      // string? (mapeado a FlePer_Chofer)
                        //CantidadReal = flete.CantidadReal,          // int? (mapeado a FlePer_CantidadReal)
                        Observaciones = flete.Observaciones,        // string? (mapeado a FlePer_Observaciones)
                        IsSynced = true                             // Campo local
                    };

                    // Verificar si ya existe
                    var existe = await _sqliteService.ExisteFleteAsync(flete.IdFletePer);
                    if (existe)
                    {
                        await _sqliteService.UpdateAsync(fleteLocal);
                        _logger.LogDebug("Flete {IdFletePer} actualizado en caché", flete.IdFletePer);
                    }
                    else
                    {
                        await _sqliteService.InsertAsync(fleteLocal);
                        _logger.LogDebug("Flete {IdFletePer} insertado en caché", flete.IdFletePer);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando fletes en caché");
            }
        }

        // Implementar métodos existentes de tu API original
        public async Task<int> InsertarFletePersonal(FletePersonalRequest request)
        {
            var url = $"{GetBaseUrl()}/InsertarFletePersonal";
            var content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await ExecuteApiCallAsync<string>(
                () => _httpClient.PostAsync(url, content), "InsertarFletePersonal");

            return int.TryParse(response, out int id) ? id : -1;
        }

        public async Task<bool> InsertarDetFlete(DetFleteRequest request)
        {
            var url = $"{GetBaseUrl()}/InsertarDetFlete";
            var content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await ExecuteApiCallAsync<string>(
                () => _httpClient.PostAsync(url, content), "InsertarDetFlete");

            return response?.Contains("Insertado correctamente") ?? false;
        }

        public async Task<bool> InsertarInicioDetFlete(DetFleteRequest request)
        {
            var url = $"{GetBaseUrl()}/InsertarInicioDetFlete";
            var content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await ExecuteApiCallAsync<string>(
                () => _httpClient.PostAsync(url, content), "InsertarInicioDetFlete");

            return response?.Contains("Insertado correctamente") ?? false;
        }

        public async Task<bool> InsertarFinDetFlete(DetFleteRequest request)
        {
            var url = $"{GetBaseUrl()}/InsertarFinDetFlete";
            var content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await ExecuteApiCallAsync<string>(
                () => _httpClient.PostAsync(url, content), "InsertarFinDetFlete");

            return response?.Contains("Insertado correctamente") ?? false;
        }

        public async Task<bool> UpdateFletePersonal(UpdateFleteRequest request)
        {
            var url = $"{GetBaseUrl()}/UpdateFletePersonal";
            var content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await ExecuteApiCallAsync<string>(
                () => _httpClient.PutAsync(url, content), "UpdateFletePersonal");

            return response?.Contains("Actualizado correctamente") ?? false;
        }

        public async Task<string> HelloWorld()
        {
            var url = $"{GetBaseUrl()}/HelloWorld";
            var response = await ExecuteApiCallAsync<string>(
                () => _httpClient.GetAsync(url), "HelloWorld");

            return response ?? "No se pudo conectar";
        }
    }


}