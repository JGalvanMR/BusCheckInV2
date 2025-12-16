using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;
using BusCheckInV2.Models;

namespace BusCheckInV2.Services.Database
{
    public class DatabaseService : IDatabaseService
    {
        private readonly SQLiteAsyncConnection _db;

        public DatabaseService(string dbPath)
        {
            _db = new SQLiteAsyncConnection(dbPath);
            _db.CreateTableAsync<Tb_FlePer_DetFlete>().Wait();
        }

        public async Task<List<Tb_FlePer_DetFlete>> ObtenerRegistrosNoSincronizadosAsync()
        {
            return await _db.Table<Tb_FlePer_DetFlete>().Where(r => !r.IsSynced).ToListAsync();
        }

        public async Task ActualizarRegistroAsync(Tb_FlePer_DetFlete registro)
        {
            await _db.UpdateAsync(registro);
        }

        // Otros métodos de acceso a la base de datos
    }
}
