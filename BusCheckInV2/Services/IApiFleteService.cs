using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusCheckInV2.Models;

namespace BusCheckInV2.Services
{
    public interface IApiFleteService
    {
        #region FLETES PENDIENTES
        Task<List<FletePendienteUI>> ObtenerFletesPorChoferAsync(string chofer, DateTime fechaDesde);
        #endregion
        #region ESCANEO CODIGO
        Task<int> InsertarFletePersonal(FletePersonalRequest request);
        Task<bool> InsertarDetFlete(DetFleteRequest request);
        Task<bool> InsertarInicioDetFlete(DetFleteRequest request);
        Task<bool> UpdateFletePersonal(UpdateFleteRequest request);
        Task<bool> InsertarFinDetFlete(DetFleteRequest request);
        #endregion
        Task<string> HelloWorld();

        Task<List<UsuarioApi>> ObtenerUsuariosAsync();
        Task<bool> ValidarYFinalizarFleteAsync(int fleteId, int cantidadPasajeros, string observaciones, double latitud, double longitud);
        Task<bool> CancelarFleteAsync(int fleteId, string motivo, string detalles);
        Task<bool> ReanudarFleteAsync(int fleteId, double latitud, double longitud);
        Task<bool> SincronizarFletesAsync(List<FleteSincronizacion> fletes);
        Task<bool> VerificarConexionAsync();
    }

    // Modelos para la API
}