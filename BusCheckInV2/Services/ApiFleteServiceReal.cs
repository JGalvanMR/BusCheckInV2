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

        public async Task<List<FletePendienteUI>> ObtenerFletesPorChoferAsync(string chofer, DateTime fechaDesde)
        {
            try
            {
                // Calcular los días desde la fechaDesde
                int dias = (DateTime.Today - fechaDesde.Date).Days;
                if (dias <= 0)
                    dias = 1; // Mínimo 1 día

                var url = $"{GetBaseUrl()}/ObtenerFletesPorChofer?chofer={Uri.EscapeDataString(chofer)}&dias={dias}";

                var response = await ExecuteApiCallAsync<ApiResponse<List<FleteResponse>>>(
                    () => _httpClient.GetAsync(url), "ObtenerFletesPorChofer");

                if (response?.Success == true)
                {
                    // Convertir FleteResponse a FletePendienteUI
                    var fletesUI = response.Data.Select(f => new FletePendienteUI
                    {
                        IdFletePer = f.IdFletePer,
                        Fecha = f.Fecha,
                        Hora = f.Hora,
                        ProveedorClave = f.ProveedorClave,
                        ProveedorNombre = f.ProveedorNombre,
                        IdDestFlete = f.IdDestFlete,
                        RutaNombre = f.RutaNombre,
                        TipoFlete = f.TipoFlete,
                        TipoViaje = f.TipoViaje,
                        Cantidad = f.Cantidad,
                        Estatus = f.Estatus,
                        Chofer = f.Chofer,
                        CantidadReal = f.CantidadReal,
                        Observaciones = f.Observaciones,
                        FechaInicio = f.FechaInicio,
                        FechaFin = f.FechaFin,
                        PuntosRegistrados = f.PuntosRegistrados,
                        EsPendiente = f.Estatus == "Pendiente" || f.Estatus == "Iniciado" || f.Estatus == "Inconcluso"
                    }).ToList();

                    return fletesUI;
                }

                return new List<FletePendienteUI>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en ObtenerFletesPorChoferAsync");
                return new List<FletePendienteUI>();
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
            foreach (var flete in fletes)
            {
                // Convertir de FleteApi a Tb_FlePer_FletePersonal
                var fleteLocal = new Tb_FlePer_FletePersonal
                {
                    IdFletePer = flete.IdFletePer,
                    FlePer_Fecha = flete.Fecha,
                    FlePer_Hora = flete.Hora,
                    Prov_Clave = flete.ProveedorClave,
                    IdDestFlete = flete.IdDestFlete,
                    FlePer_TipoFlete = flete.TipoFlete,
                    FlePer_TipoViaje = flete.TipoViaje,
                    FlePer_Cantidad = flete.Cantidad,
                    FlePer_Status = flete.Estatus,
                    FlePer_Chofer = flete.Chofer,
                    FlePer_CantidadReal = flete.CantidadReal,
                    FlePer_Observaciones = flete.Observaciones,
                    IsSynced = true // Ya están sincronizados desde API
                };

                // Verificar si ya existe
                var existe = await _sqliteService.ExisteFleteAsync(flete.IdFletePer);
                if (existe)
                {
                    await _sqliteService.UpdateAsync(fleteLocal);
                }
                else
                {
                    await _sqliteService.InsertAsync(fleteLocal);
                }
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