// ============================================================
//  NUEVO MODELO: BusCheckInV2/Models/ClearTablesResult.cs
//  INSTRUCCIÓN: Crea este archivo.
// ============================================================

using BusCheckInV2.Models;

namespace BusCheckInV2.Models
{
    /// <summary>
    /// Resultado de ClearAllTablesAsync. Permite a la UI mostrar
    /// el motivo del rechazo sin atrapar excepciones.
    /// </summary>
    public class ClearTablesResult
    {
        public bool Success { get; init; }
        public int RegistrosBorrados { get; init; }
        public int PendientesSinSync { get; init; }
        public string Message { get; init; } = string.Empty;

        public static ClearTablesResult Ok(int borrados) => new()
        {
            Success = true,
            RegistrosBorrados = borrados,
            Message = $"{borrados} registros eliminados correctamente"
        };

        public static ClearTablesResult Rejected(int pendientes) => new()
        {
            Success = false,
            PendientesSinSync = pendientes,
            Message = $"Operación cancelada: hay {pendientes} registros sin sincronizar"
        };

        public static ClearTablesResult Error(string mensaje) => new()
        {
            Success = false,
            Message = mensaje
        };
    }
}






