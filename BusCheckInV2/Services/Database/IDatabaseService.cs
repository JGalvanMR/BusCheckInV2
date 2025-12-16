using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusCheckInV2.Models;

namespace BusCheckInV2.Services.Database
{
    public interface IDatabaseService
    {
        Task<List<Tb_FlePer_DetFlete>> ObtenerRegistrosNoSincronizadosAsync();
        Task ActualizarRegistroAsync(Tb_FlePer_DetFlete registro);
        // Otros métodos de acceso a la base de datos
    }
}
