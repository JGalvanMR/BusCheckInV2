using BusCheckInV2.Models;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace BusCheckInV2.Services
{
    public interface ISQLiteService
    {
        Task InitializeAsync();
        Task<int> InsertAsync<T>(T item) where T : class;
        Task<int> UpdateAsync<T>(T item) where T : class;
        Task<int> DeleteAsync<T>(T item) where T : class;
        Task<List<T>> GetItemsAsync<T>() where T : class, new();
        Task<T> GetItemAsync<T>(int id) where T : class, new();
        Task ClearAllTablesAsync();

        // ✅ CORREGIDO: Añadir 'new()' OBLIGATORIO para sqlite-net-pcl
        Task<int> DeleteByPredicateAsync<T>(Expression<Func<T, bool>> predicate) where T : class, new();

        // Fletes
        Task<List<FletePendienteUI>> ObtenerFletesPendientesAsync(string chofer = null, int diasAtras = 3);
        Task<List<string>> ObtenerChoferesUnicosAsync();
        Task<bool> ActualizarEstadoFleteAsync(int idFleteLocal, string nuevoEstatus, int? cantidadReal = null, string observaciones = null);
        Task<int> InsertarDetalleFleteAsync(int idFletePer, int cveNomina, double latitud, double longitud, string nombre);

        // Sincronización
        Task<List<Tb_FlePer_FletePersonal>> ObtenerFletesNoSincronizadosAsync();
        Task<bool> ExisteFleteAsync(int idFletePer);
        Task MarcarComoSincronizadoAsync(int idFletePer);
        Task<int> SincronizarConApiAsync(IApiFleteService apiService);

        // Cache
        Task<List<FletePendienteUI>> ObtenerFletesPendientesDesdeCacheAsync(string chofer, int dias);
        Task<bool> GuardarFleteEnCacheAsync(FleteApi flete);
        Task LimpiarCacheAsync();
    }
}