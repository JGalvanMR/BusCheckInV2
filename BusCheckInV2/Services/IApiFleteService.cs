using BusCheckInV2.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    /// <summary>
    /// Interfaz para el servicio de API de fletes
    /// </summary>
    public interface IApiFleteService
    {
        /// <summary>
        /// Obtiene la lista de usuarios/choferes desde el servidor
        /// </summary>
        Task<List<UsuarioApi>> ObtenerUsuariosAsync();

        /// <summary>
        /// Obtiene fletes por chofer con filtros
        /// </summary>
        Task<ApiResponse<List<FleteResponse>>> ObtenerFletesPorChoferAsync(string chofer, int dias = 3, bool soloPendientes = false);

        /// <summary>
        /// Finaliza un flete en el servidor
        /// </summary>
        Task<bool> ValidarYFinalizarFleteAsync(int fleteId, int cantidadPasajeros, string observaciones, double latitud, double longitud);

        /// <summary>
        /// Cancela un flete en el servidor
        /// </summary>
        Task<bool> CancelarFleteAsync(int fleteId, string motivo, string detalles);

        /// <summary>
        /// Reanuda un flete inconcluso
        /// </summary>
        Task<bool> ReanudarFleteAsync(int fleteId, double latitud, double longitud);

        /// <summary>
        /// Sincroniza fletes pendientes con el servidor
        /// </summary>
        Task<bool> SincronizarFletesAsync(List<FleteSincronizacion> fletes);

        /// <summary>
        /// Verifica conexión con el servidor
        /// </summary>
        Task<bool> VerificarConexionAsync();

        /// <summary>
        /// Inserta un flete personal en el servidor
        /// </summary>
        Task<long> InsertarFletePersonal(FletePersonalRequest request);

        /// <summary>
        /// Inserta detalle de flete en el servidor
        /// </summary>
        Task<bool> InsertarDetFlete(DetFleteRequest request);

        /// <summary>
        /// Inserta inicio de detalle en el servidor
        /// </summary>
        Task<bool> InsertarInicioDetFlete(DetFleteRequest request);

        /// <summary>
        /// Inserta fin de detalle en el servidor
        /// </summary>
        Task<bool> InsertarFinDetFlete(DetFleteRequest request);

        /// <summary>
        /// Actualiza un flete personal en el servidor
        /// </summary>
        Task<bool> UpdateFletePersonal(UpdateFleteRequest request);

        /// <summary>
        /// Endpoint de prueba HelloWorld
        /// </summary>
        Task<string> HelloWorld();
    }
}