using BusCheckInV2.Constants;
using BusCheckInV2.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    /// <summary>
    /// Implementación real del servicio de API para comunicación con el servidor
    /// </summary>
    public class ApiFleteService : IApiFleteService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiFleteService> _logger;
        private readonly ISQLiteService _sqliteService;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiFleteService(
            HttpClient httpClient,
            ILogger<ApiFleteService> logger,
            ISQLiteService sqliteService)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sqliteService = sqliteService ?? throw new ArgumentNullException(nameof(sqliteService));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        private string GetApiUrl(string endpoint) => $"{ApiConstants.BaseUrlDebug}{ApiConstants.ApiPath}{endpoint}";

        private async Task<T> ExecuteApiCallAsync<T>(Func<Task<HttpResponseMessage>> apiCall, string operationName)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    _logger.LogWarning("Sin conexión a internet para {Operation}", operationName);
                    return default;
                }

                var response = await apiCall();

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("API {Operation} exitosa", operationName);

                    if (string.IsNullOrEmpty(content))
                        return default;

                    return JsonSerializer.Deserialize<T>(content, _jsonOptions);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error API {Operation}: {StatusCode} - {Error}", operationName, response.StatusCode, errorContent);
                    return default;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción en {Operation}", operationName);
                return default;
            }
        }

        #region Métodos Principales

        public async Task<List<UsuarioApi>> ObtenerUsuariosAsync()
        {
            var url = GetApiUrl("/ObtenerUsuarios");
            var response = await ExecuteApiCallAsync<ApiResponse<List<UsuarioApi>>>(
                () => _httpClient.GetAsync(url), "ObtenerUsuarios");

            return response?.Success == true ? response.Data : new List<UsuarioApi>();
        }

        public async Task<ApiResponse<List<FleteResponse>>> ObtenerFletesPorChoferAsync(
            string chofer, int dias = 3, bool soloPendientes = false)
        {
            try
            {
                var queryString = $"?chofer={Uri.EscapeDataString(chofer)}&dias={dias}&soloPendientes={soloPendientes.ToString().ToLower()}";
                var url = GetApiUrl("/ObtenerFletesPorChofer") + queryString;

                _logger.LogInformation("Llamando API: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ApiResponse<List<FleteResponse>>>(content, _jsonOptions);
                }
                else
                {
                    return new ApiResponse<List<FleteResponse>>
                    {
                        Success = false,
                        Message = $"Error del servidor: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consumir API de fletes");
                return new ApiResponse<List<FleteResponse>>
                {
                    Success = false,
                    Message = "Error de conexión con el servidor"
                };
            }
        }

        public async Task<bool> ValidarYFinalizarFleteAsync(
            int fleteId, int cantidadPasajeros, string observaciones, double latitud, double longitud)
        {
            var url = GetApiUrl("/ValidarYFinalizarFlete");
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
            var url = GetApiUrl("/CancelarFlete");
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
            var url = GetApiUrl("/ReanudarFlete");
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
            var url = GetApiUrl("/SincronizarFletes");
            var content = new StringContent(
                JsonSerializer.Serialize(fletes, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await ExecuteApiCallAsync<ApiResponse<List<SincronizacionResult>>>(
                () => _httpClient.PostAsync(url, content), "SincronizarFletes");

            if (response?.Success == true)
            {
                foreach (var result in response.Data.Where(r => r.Success))
                {
                    await _sqliteService.MarcarComoSincronizadoAsync(result.IdFletePer);
                }
                return true;
            }

            return false;
        }

        public async Task<bool> VerificarConexionAsync()
        {
            var url = GetApiUrl("/VerificarConexion");
            var response = await ExecuteApiCallAsync<ApiResponse<object>>(
                () => _httpClient.GetAsync(url), "VerificarConexion");

            return response?.Success == true;
        }

        public async Task<long> InsertarFletePersonal(FletePersonalRequest request)
        {
            var url = GetApiUrl("/InsertarFletePersonal");
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await ExecuteApiCallAsync<ApiResponse<long>>(
                () => _httpClient.PostAsync(url, content), "InsertarFletePersonal");

            return response?.Success == true ? response.Data : -1;
        }

        public async Task<bool> InsertarDetFlete(DetFleteRequest request)
        {
            // FIX 2026-06-02 (CRÍTICO): el backend tiene [Required] en
            // FlePer_Nombre y rechaza con 400 si llega null/vacío.
            // Como det.Nombre es null para pasajeros escaneados, el
            // DetFleteRequest llega con FlePer_Nombre=null → 400.
            // Sanitizamos AQUÍ antes de serializar.
            request = SanitizarDetFleteRequest(request);
            var url = GetApiUrl("/InsertarDetFlete");
            var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");

            var response = await ExecuteApiCallAsync<ApiResponse<string>>(
                () => _httpClient.PostAsync(url, content), "InsertarDetFlete");

            return response?.Success == true && response.Data?.Contains("Insertado") == true;
        }

        public async Task<bool> InsertarInicioDetFlete(DetFleteRequest request)
        {
            request = SanitizarDetFleteRequest(request);
            var url = GetApiUrl("/InsertarInicioDetFlete");
            var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");

            var response = await ExecuteApiCallAsync<ApiResponse<string>>(
                () => _httpClient.PostAsync(url, content), "InsertarInicioDetFlete");

            return response?.Success == true && response.Data?.Contains("Insertado") == true;
        }

        public async Task<bool> InsertarFinDetFlete(DetFleteRequest request)
        {
            request = SanitizarDetFleteRequest(request);
            var url = GetApiUrl("/InsertarFinDetFlete");
            var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");

            var response = await ExecuteApiCallAsync<ApiResponse<string>>(
                () => _httpClient.PostAsync(url, content), "InsertarFinDetFlete");

            return response?.Success == true && response.Data?.Contains("Insertado") == true;
        }

        // FIX 2026-06-02: helper que garantiza que FlePer_Nombre nunca
        // llegue null/vacío al backend. El SQL del backend calcula el
        // nombre real desde tb_cat_empleados usando FlePer_CveNomina,
        // así que el valor que mandamos es decorativo: solo necesitamos
        // pasar la validación [Required].
        private static DetFleteRequest SanitizarDetFleteRequest(DetFleteRequest request)
        {
            if (request == null) return request;
            if (string.IsNullOrWhiteSpace(request.FlePer_Nombre))
            {
                request.FlePer_Nombre = "(pendiente)";
            }
            return request;
        }

        public async Task<bool> UpdateFletePersonal(UpdateFleteRequest request)
        {
            var url = GetApiUrl("/UpdateFletePersonal");
            var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");

            var response = await ExecuteApiCallAsync<ApiResponse<string>>(
                () => _httpClient.PutAsync(url, content), "UpdateFletePersonal");

            return response?.Success == true && response.Data?.Contains("Actualizado") == true;
        }

        public async Task<DetFletesBatchSyncResult> SincronizarDetFletesAsync(
    int serverIdFletePer,
    List<DetFleteItemRequest> items)
        {
            // FIX 2026-06-02 (CRÍTICO encontrado en log del usuario):
            // El backend (WSBusCheckInV2Controller.InsertarDetFlete y
            // SincronizarDetFletes) tiene [Required] en FlePer_Nombre y
            // rechaza con 400 BadRequest si llega null/vacío. Log real:
            //   "errors":{"FlePer_Nombre":["The FlePer_Nombre field is required."]}
            //   Status: 400 en /InsertarDetFlete
            //
            // Curiosidad: el SQL del backend IGNORA el valor enviado
            // y hace CONCAT(...) desde tb_cat_empleados usando
            // @FlePer_CveNomina. El campo FlePer_Nombre es decorativo,
            // pero la validación [Required] se ejecuta antes.
            //
            // Por eso mandamos un placeholder que pasa la validación
            // pero el backend descarta. El endpoint batch NO acepta
            // FlePer_Nombre en su payload (solo en el endpoint individual),
            // así que este fix solo aplica al endpoint individual.
            //
            // Acción recomendada (no aplicada): renombrar la clase
            // DetFletesBatchSyncResult a DetFletesBatchResult para
            // coincidir 1:1 con el backend, o agregar
            // [JsonPropertyName("totalInsertados")] explícito. No rompe
            // mientras el wire format (nombres de propiedad JSON) coincida.
            var url = GetApiUrl("/SincronizarDetFletes");

            var payload = new
            {
                IdFletePer = serverIdFletePer,
                Items = items.Select(i => new
                {
                    i.FlePer_CveNomina,
                    i.FlePer_Latitud,
                    i.FlePer_Longitud,
                    i.FlePer_Fecha,
                    i.LocalId
                }).ToList()
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await ExecuteApiCallAsync<ApiResponse<DetFletesBatchSyncResult>>(
                () => _httpClient.PostAsync(url, content),
                "SincronizarDetFletes");

            if (response?.Success == true && response.Data != null)
                return response.Data;

            return new DetFletesBatchSyncResult
            {
                Success = false,
                Message = "Error al sincronizar batch de detalles"
            };
        }

        public async Task<string> HelloWorld()
        {
            var url = GetApiUrl("/HelloWorld");
            var response = await ExecuteApiCallAsync<string>(() => _httpClient.GetAsync(url), "HelloWorld");
            return response ?? "No se pudo conectar";
        }

        #endregion
    }
}