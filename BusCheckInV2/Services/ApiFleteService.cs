using BusCheckInV2.Constants;
using BusCheckInV2.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    public class ApiFleteServiceReal : IApiFleteService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiFleteServiceReal> _logger;
        private readonly ISQLiteService _sqliteService;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiFleteServiceReal(
            HttpClient httpClient,
            ILogger<ApiFleteServiceReal> logger,
            ISQLiteService sqliteService)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sqliteService = sqliteService ?? throw new ArgumentNullException(nameof(sqliteService));

            // RECUPERADO DE LA VERSIÓN VIEJA: Timeout y Headers
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "BusCheckInV2-MAUI");

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
                    if (string.IsNullOrEmpty(content)) return default;
                    return JsonSerializer.Deserialize<T>(content, _jsonOptions);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error API {Operation}: {StatusCode} - {Error}", operationName, response.StatusCode, errorContent);
                    return default;
                }
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout al llamar a {Operation}", operationName);
                return default;
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
            var response = await ExecuteApiCallAsync<ApiResponse<List<UsuarioApi>>>(() => _httpClient.GetAsync(url), "ObtenerUsuarios");
            return response?.Success == true ? response.Data : new List<UsuarioApi>();
        }

        public async Task<ApiResponse<List<FleteResponse>>> ObtenerFletesPorChoferAsync(string chofer, int dias = 3, bool soloPendientes = false)
        {
            try
            {
                var queryString = $"?chofer={Uri.EscapeDataString(chofer)}&dias={dias}&soloPendientes={soloPendientes.ToString().ToLower()}";
                var url = GetApiUrl("/ObtenerFletesPorChofer") + queryString;

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<ApiResponse<List<FleteResponse>>>(content, _jsonOptions);
                }
                return new ApiResponse<List<FleteResponse>> { Success = false, Message = $"Error del servidor: {response.StatusCode}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consumir API de fletes");
                return new ApiResponse<List<FleteResponse>> { Success = false, Message = "Error de conexión con el servidor" };
            }
        }

        public async Task<bool> ValidarYFinalizarFleteAsync(int fleteId, int cantidadPasajeros, string observaciones, double latitud, double longitud)
        {
            var url = GetApiUrl("/ValidarYFinalizarFlete");
            var request = new { IdFletePer = fleteId, CantidadReal = cantidadPasajeros, Observaciones = observaciones, Latitud = latitud, Longitud = longitud, Usuario = "App MAUI" };
            var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");
            var response = await ExecuteApiCallAsync<ApiResponse<object>>(() => _httpClient.PostAsync(url, content), "ValidarYFinalizarFlete");
            return response?.Success == true;
        }

        public async Task<bool> CancelarFleteAsync(int fleteId, string motivo, string detalles)
        {
            var url = GetApiUrl("/CancelarFlete");
            var request = new { IdFletePer = fleteId, Motivo = motivo, Detalles = detalles, Usuario = "App MAUI" };
            var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");
            var response = await ExecuteApiCallAsync<ApiResponse<object>>(() => _httpClient.PostAsync(url, content), "CancelarFlete");
            return response?.Success == true;
        }

        public async Task<bool> ReanudarFleteAsync(int fleteId, double latitud, double longitud)
        {
            var url = GetApiUrl("/ReanudarFlete");
            var request = new { IdFletePer = fleteId, Latitud = latitud, Longitud = longitud };
            var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");
            var response = await ExecuteApiCallAsync<ApiResponse<object>>(() => _httpClient.PostAsync(url, content), "ReanudarFlete");
            return response?.Success == true;
        }

        public async Task<bool> SincronizarFletesAsync(List<FleteSincronizacion> fletes)
        {
            var url = GetApiUrl("/SincronizarFletes");
            var content = new StringContent(JsonSerializer.Serialize(fletes, _jsonOptions), Encoding.UTF8, "application/json");
            var response = await ExecuteApiCallAsync<ApiResponse<List<SincronizacionResult>>>(() => _httpClient.PostAsync(url, content), "SincronizarFletes");

            if (response?.Success == true && response.Data != null)
            {
                foreach (var result in response.Data.Where(r => r.Success))
                    await _sqliteService.MarcarComoSincronizadoAsync(result.IdFletePer);
                return true;
            }
            return false;
        }

        public async Task<bool> VerificarConexionAsync()
        {
            var url = GetApiUrl("/VerificarConexion");
            var response = await ExecuteApiCallAsync<ApiResponse<object>>(() => _httpClient.GetAsync(url), "VerificarConexion");
            return response?.Success == true;
        }

        public async Task<long> InsertarFletePersonal(FletePersonalRequest request)
        {
            var url = GetApiUrl("/InsertarFletePersonal");
            var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");
            var response = await ExecuteApiCallAsync<ApiResponse<long>>(() => _httpClient.PostAsync(url, content), "InsertarFletePersonal");
            return response?.Success == true ? response.Data : -1;
        }

        public async Task<bool> InsertarDetFlete(DetFleteRequest request)
        {
            var url = GetApiUrl("/InsertarDetFlete");
            var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");
            var response = await ExecuteApiCallAsync<ApiResponse<string>>(() => _httpClient.PostAsync(url, content), "InsertarDetFlete");
            return response?.Success == true && response.Data?.Contains("Insertado") == true;
        }

        public async Task<bool> InsertarInicioDetFlete(DetFleteRequest request)
        {
            var url = GetApiUrl("/InsertarInicioDetFlete");
            var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");
            var response = await ExecuteApiCallAsync<ApiResponse<string>>(() => _httpClient.PostAsync(url, content), "InsertarInicioDetFlete");
            return response?.Success == true && response.Data?.Contains("Insertado") == true;
        }

        public async Task<bool> InsertarFinDetFlete(DetFleteRequest request)
        {
            var url = GetApiUrl("/InsertarFinDetFlete");
            var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");
            var response = await ExecuteApiCallAsync<ApiResponse<string>>(() => _httpClient.PostAsync(url, content), "InsertarFinDetFlete");
            return response?.Success == true && response.Data?.Contains("Insertado") == true;
        }

        public async Task<bool> UpdateFletePersonal(UpdateFleteRequest request)
        {
            var url = GetApiUrl("/UpdateFletePersonal");
            var content = new StringContent(JsonSerializer.Serialize(request, _jsonOptions), Encoding.UTF8, "application/json");
            var response = await ExecuteApiCallAsync<ApiResponse<string>>(() => _httpClient.PutAsync(url, content), "UpdateFletePersonal");
            return response?.Success == true && response.Data?.Contains("Actualizado") == true;
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