using Android.Widget;
using BusCheckInV2.Models;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Toast = CommunityToolkit.Maui.Alerts.Toast;

namespace BusCheckInV2.Services
{
    public class SQLiteService : ISQLiteService
    {
        private SQLiteAsyncConnection _database;
        private readonly ILogger<ApiFleteServiceReal> _logger;
        // Constantes para configuración (best practice de Microsoft Learn)
        private const string DatabaseFilename = "BusCheckInV2.db3";
        private const SQLiteOpenFlags Flags =
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.SharedCache; // SharedCache para multi-threaded access

        private string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);

        public SQLiteService() { } // Constructor vacío; inicialización lazy en InitializeAsync

        public async Task InitializeAsync()
        {
            try
            {
                if (_database != null) return; // Lazy init: solo una vez

                _database = new SQLiteAsyncConnection(DatabasePath, Flags);
                await CreateTablesAsync();
                await _database.EnableWriteAheadLoggingAsync(); // Habilita WAL para concurrency (recomendado en .NET 9+)
                await SeedDataAsync();
            }
            catch (Exception ex)
            {
                var toast = Toast.Make($"Error al inicializar la base de datos: {ex.Message}", ToastDuration.Long);
                await toast.Show();
            }
        }

        // Método para crear tablas
        private async Task CreateTablesAsync()
        {
            await _database.CreateTableAsync<Tb_Cat_Proveedor>();
            await _database.CreateTableAsync<Tb_FlePer_DetFlete>();
            await _database.CreateTableAsync<Tb_FlePer_FletePersonal>();
            await _database.CreateTableAsync<Tb_FlePer_ProvRuta>();
            await _database.CreateTableAsync<Tb_FlePer_Ruta>();
            // Agrega más tablas si es necesario
        }

        // Método para insertar datos iniciales (solo si la tabla está vacía)
        private async Task SeedDataAsync()
        {
            await InsertInitialDataIfNeededAsync(new List<Tb_Cat_Proveedor>
            {
                new Tb_Cat_Proveedor { ProvClave = "TURISTICOS", ProvNombre = "JUAN CARLOS ACOSTA CABRERA" },
                new Tb_Cat_Proveedor { ProvClave = "10810", ProvNombre = "RIVERA MONTESINO MARGARITA JACQUELINE" },
                new Tb_Cat_Proveedor { ProvClave = "RAMIROGE", ProvNombre = "RAMIRO GARCIA ESTRADA" }
                // Otros proveedores
            });

            await InsertInitialDataIfNeededAsync(new List<Tb_FlePer_Ruta>
            {
                new Tb_FlePer_Ruta { IdDestFlete = 1, NomDestFlete = "Yostiro", FleteCant = 4, FleteCosto = 900.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 2, NomDestFlete = "Carrizal - Peñuelas", FleteCant = 4, FleteCosto = 825.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 3, NomDestFlete = "4ta Brigada", FleteCant = 2, FleteCosto = 530.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 4, NomDestFlete = "Estanco - La Mesa", FleteCant = 2, FleteCosto = 530.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 5, NomDestFlete = "Doña Rosa - San Vicente", FleteCant = 2, FleteCosto = 550.00m, DestStatus = "B", RutaCupo = 20, RutaVehiculo = "CAMIONETA" },
                new Tb_FlePer_Ruta { IdDestFlete = 6, NomDestFlete = "Soledad", FleteCant = 0, FleteCosto = 530.00m, DestStatus = "B", RutaCupo = 38, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 7, NomDestFlete = "Zona Centro", FleteCant = 0, FleteCosto = 530.00m, DestStatus = "B", RutaCupo = 0, RutaVehiculo = "" },
                new Tb_FlePer_Ruta { IdDestFlete = 8, NomDestFlete = "Tomelopitos", FleteCant = 2, FleteCosto = 530.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 9, NomDestFlete = "Cardenas", FleteCant = 2, FleteCosto = 530.00m, DestStatus = "A", RutaCupo = 20, RutaVehiculo = "CAMIONETA" },
                new Tb_FlePer_Ruta { IdDestFlete = 10, NomDestFlete = "Oreja - Mocha", FleteCant = 4, FleteCosto = 530.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 11, NomDestFlete = "Mendoza Temascatio", FleteCant = 2, FleteCosto = 530.00m, DestStatus = "A", RutaCupo = 38, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 12, NomDestFlete = "San Cayetano - Apatzingan", FleteCant = 2, FleteCosto = 530.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMIONETA" },
                new Tb_FlePer_Ruta { IdDestFlete = 13, NomDestFlete = "Purísima - Malvas", FleteCant = 1, FleteCosto = 530.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMIONETA" },
                new Tb_FlePer_Ruta { IdDestFlete = 14, NomDestFlete = "San Juan - Nicolas", FleteCant = 4, FleteCosto = 650.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMIONETA" },
                new Tb_FlePer_Ruta { IdDestFlete = 15, NomDestFlete = "Soledad - Centro", FleteCant = 2, FleteCosto = 1150.00m, DestStatus = "A", RutaCupo = 38, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 16, NomDestFlete = "LIMPIEZA NOCTURNA", FleteCant = 2, FleteCosto = 530.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 17, NomDestFlete = "LOMA DE FLORES", FleteCant = 2, FleteCosto = 530.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 18, NomDestFlete = "LOMA DE FLORES - MENDOZA", FleteCant = 2, FleteCosto = 530.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 19, NomDestFlete = "Cardenas-DoñaRosa-San Vicente", FleteCant = 2, FleteCosto = 1100.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMION" }
                // Otros rutas
            });

            await InsertInitialDataIfNeededAsync(new List<Tb_FlePer_ProvRuta>
            {
                new Tb_FlePer_ProvRuta { IdRutaProv = 1, Prov_Clave = "10810", IdDestFlete = 1, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 2, Prov_Clave = "10810", IdDestFlete = 2, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 3, Prov_Clave = "10810", IdDestFlete = 3, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 4, Prov_Clave = "10810", IdDestFlete = 4, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 5, Prov_Clave = "10810", IdDestFlete = 5, RutaStatus = "B" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 6, Prov_Clave = "10810", IdDestFlete = 6, RutaStatus = "B" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 7, Prov_Clave = "10810", IdDestFlete = 7, RutaStatus = "B" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 8, Prov_Clave = "10810", IdDestFlete = 8, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 9, Prov_Clave = "10810", IdDestFlete = 9, RutaStatus = "B" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 10, Prov_Clave = "10810", IdDestFlete = 10, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 11, Prov_Clave = "10810", IdDestFlete = 11, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 12, Prov_Clave = "TURISTICOS", IdDestFlete = 12, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 13, Prov_Clave = "TURISTICOS", IdDestFlete = 13, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 14, Prov_Clave = "TURISTICOS", IdDestFlete = 14, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 15, Prov_Clave = "10810", IdDestFlete = 15, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 16, Prov_Clave = "RAMIROGE", IdDestFlete = 16, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 17, Prov_Clave = "RAMIROGE", IdDestFlete = 17, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 18, Prov_Clave = "RAMIROGE", IdDestFlete = 18, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 23, Prov_Clave = "10810", IdDestFlete = 19, RutaStatus = "A" }
                // Otros prov rutas
            });
        }

        private async Task InsertInitialDataIfNeededAsync<T>(List<T> initialData) where T : new()
        {
            var count = await _database.Table<T>().CountAsync();
            if (count == 0)
            {
                await _database.InsertAllAsync(initialData);
            }
        }

        // Insertar datos
        public Task<int> InsertAsync<T>(T item) where T : class
        {
            return _database.InsertAsync(item);
        }

        // Actualizar datos
        public Task<int> UpdateAsync<T>(T item) where T : class
        {
            return _database.UpdateAsync(item);
        }

        // Eliminar datos
        public Task<int> DeleteAsync<T>(T item) where T : class
        {
            return _database.DeleteAsync(item);
        }

        // Obtener datos
        public Task<List<T>> GetItemsAsync<T>() where T : class, new()
        {
            return _database.Table<T>().ToListAsync();
        }

        // Obtener un ítem por id
        public Task<T> GetItemAsync<T>(int id) where T : class, new()
        {
            return _database.FindAsync<T>(id);
        }

        // Método para limpiar todas las tablas
        public async Task ClearAllTablesAsync()
        {
            try
            {
                await _database.DeleteAllAsync<Tb_FlePer_DetFlete>();
                await _database.DeleteAllAsync<Tb_FlePer_FletePersonal>();
                // Descomenta si necesitas limpiar estas (estaban comentadas en tu código)
                // await _database.DeleteAllAsync<Tb_Cat_Proveedor>();
                // await _database.DeleteAllAsync<Tb_FlePer_ProvRuta>();
                // await _database.DeleteAllAsync<Tb_FlePer_Ruta>();

                var toast = Toast.Make("Todas las tablas se han limpiado correctamente.", ToastDuration.Short);
                await toast.Show();
            }
            catch (Exception ex)
            {
                var toast = Toast.Make($"Error al limpiar las tablas: {ex.Message}", ToastDuration.Long);
                await toast.Show();
            }
        }

        // Elimina items por predicado
        public async Task<int> DeleteByPredicateAsync<T>(Expression<Func<T, bool>> predicate) where T : class, new()
        {
            try
            {
                var count = await _database.Table<T>().Where(predicate).DeleteAsync();
                return count; // Retorna el número de items eliminados
            }
            catch (Exception ex)
            {
                var toast = Toast.Make($"Error al eliminar items: {ex.Message}", ToastDuration.Long);
                await toast.Show();
                return 0;
            }
        }


        // Agregar estos métodos a la clase SQLiteService:

        public async Task<List<FletePendienteUI>> ObtenerFletesPendientesAsync(string chofer = null, int diasAtras = 3)
        {
            try
            {
                var fechaLimite = DateTime.Now.AddDays(-diasAtras);

                var query = _database.Table<Tb_FlePer_FletePersonal>()
                    .Where(f => f.Fecha != null);

                // Filtrar por chofer si se especifica
                if (!string.IsNullOrEmpty(chofer))
                {
                    query = query.Where(f => f.Chofer == chofer);
                }

                var fletes = await query.ToListAsync();

                // Filtrar por fecha manualmente
                var fletesFiltrados = fletes.Where(f =>
                {
                    return f.Fecha >= fechaLimite;
                    //if (DateTime.TryParse(f.Fecha, out DateTime fechaFlete))
                    //{
                    //    return fechaFlete >= fechaLimite;
                    //}
                    //return false;
                })
                .Where(f => f.Status != "Completado" && f.Status != "Cancelado")
                .ToList();

                // Obtener todas las rutas y proveedores para mapeo
                var todasRutas = await _database.Table<Tb_FlePer_Ruta>().ToListAsync();
                var todosProveedores = await _database.Table<Tb_Cat_Proveedor>().ToListAsync();

                // Convertir a FletePendienteUI
                var resultado = new List<FletePendienteUI>();

                foreach (var flete in fletesFiltrados)
                {
                    // Buscar nombre de ruta
                    string nombreRuta = "Ruta desconocida";
                    if (flete.IdDestFlete.HasValue)
                    {
                        var ruta = todasRutas.FirstOrDefault(r => r.IdDestFlete == flete.IdDestFlete);
                        nombreRuta = ruta?.NomDestFlete ?? "Ruta desconocida";
                    }

                    // Buscar nombre del proveedor
                    string nombreProveedor = flete.ProvClave ?? "Desconocido";
                    if (!string.IsNullOrEmpty(flete.ProvClave))
                    {
                        var proveedor = todosProveedores.FirstOrDefault(p => p.ProvClave == flete.ProvClave);
                        nombreProveedor = proveedor?.ProvNombre ?? flete.ProvClave;
                    }

                    // Parsear fecha y hora
                    DateTime fechaHora = DateTime.Now;
                    if (DateTime.TryParse($"{flete.Fecha} {flete.Hora}", out DateTime parsedFecha))
                    {
                        fechaHora = parsedFecha;
                    }

                    resultado.Add(new FletePendienteUI
                    {
                        Id = flete.Id,
                        IdFletePer = (int?)flete.IdFletePer,
                        Ruta = nombreRuta,
                        FechaHora = fechaHora,
                        Proveedor = nombreProveedor,
                        Chofer = flete.Chofer ?? "Desconocido",
                        Estatus = flete.Status ?? "Pendiente",
                        CantidadEsperada = flete.Cantidad,
                        CantidadReal = flete.Cantidad,
                        TipoFlete = flete.TipoFlete ?? "NORMAL",
                        TipoViaje = flete.TipoViaje ?? "TRAER GENTE",
                        FechaInicio = flete.Fecha,
                        FechaFin = flete.FechaFin
                    });
                }

                return resultado.OrderByDescending(f => f.FechaHora).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener fletes pendientes: {ex.Message}");
                return new List<FletePendienteUI>();
            }
        }

        public async Task<List<string>> ObtenerChoferesUnicosAsync()
        {
            try
            {
                var choferes = await _database.Table<Tb_FlePer_FletePersonal>()
                    .Where(f => !string.IsNullOrEmpty(f.Chofer))
                    .OrderBy(f => f.Chofer)
                    .ToListAsync();

                return choferes
                    .Select(f => f.Chofer)
                    .Distinct()
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener choferes: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<bool> ActualizarEstadoFleteAsync(int idFleteLocal, string nuevoEstatus, int? cantidadReal = null, string observaciones = null)
        {
            try
            {
                var flete = await _database.Table<Tb_FlePer_FletePersonal>()
                    .FirstOrDefaultAsync(f => f.Id == idFleteLocal);

                if (flete == null) return false;

                flete.Status = nuevoEstatus;
                flete.IsSynced = false; // Marcar para sincronizar

                if (cantidadReal.HasValue)
                {
                    flete.Cantidad = cantidadReal.Value;
                }

                if (!string.IsNullOrEmpty(observaciones))
                {
                    flete.Observaciones = observaciones;
                }

                if (nuevoEstatus == "Completado" || nuevoEstatus == "Cancelado")
                {
                    flete.FechaFin = DateTime.Now;
                }

                var resultado = await _database.UpdateAsync(flete);
                return resultado > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar flete: {ex.Message}");
                return false;
            }
        }

        public async Task<int> InsertarDetalleFleteAsync(int idFletePer, int cveNomina, double latitud, double longitud, string nombre)
        {
            try
            {
                var detalle = new Tb_FlePer_DetFlete
                {
                    IdFletePer = idFletePer,
                    CveNomina = cveNomina,
                    Latitud = latitud,
                    Longitud = longitud,
                    Fecha = DateTime.Now,
                    IsSynced = false
                };

                return await _database.InsertAsync(detalle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al insertar detalle flete: {ex.Message}");
                return 0;
            }
        }

        #region METODODS PARA SINCRONIZACION
        // Agregar estos métodos dentro de la clase SQLiteService

        public async Task<List<Tb_FlePer_FletePersonal>> ObtenerFletesNoSincronizadosAsync()
        {
            try
            {
                return await _database.Table<Tb_FlePer_FletePersonal>()
                    .Where(f => !f.IsSynced)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo fletes no sincronizados");
                return new List<Tb_FlePer_FletePersonal>();
            }
        }

        public async Task<bool> ExisteFleteAsync(int idFletePer)
        {
            try
            {
                var count = await _database.Table<Tb_FlePer_FletePersonal>()
                    .Where(f => f.IdFletePer == idFletePer)
                    .CountAsync();
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando existencia de flete");
                return false;
            }
        }

        public async Task MarcarComoSincronizadoAsync(int idFletePer)
        {
            try
            {
                var flete = await _database.Table<Tb_FlePer_FletePersonal>()
                    .FirstOrDefaultAsync(f => f.IdFletePer == idFletePer);

                if (flete != null)
                {
                    flete.IsSynced = true;
                    await _database.UpdateAsync(flete);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marcando flete como sincronizado");
            }
        }

        public async Task<int> SincronizarConApiAsync(IApiFleteService apiService)
        {
            try
            {
                var fletesNoSincronizados = await ObtenerFletesNoSincronizadosAsync();

                if (!fletesNoSincronizados.Any())
                    return 0;

                // Convertir a formato de sincronización
                var fletesSync = fletesNoSincronizados.Select(f => new FleteSincronizacion
                {
                    IdFletePer = (int)(f.IdFletePer ?? 0),
                    Fecha = (DateTime)f.Fecha,
                    Hora = (TimeSpan)f.Hora,
                    ProvClave = f.ProvClave,
                    IdDestFlete = (int)(f.IdDestFlete ?? 0),
                    TipoFlete = f.TipoFlete,
                    TipoViaje = f.TipoViaje,
                    Cantidad = f.Cantidad ?? 0,
                    Status = f.Status,
                    Chofer = f.Chofer,
                    CantidadReal = f.Cantidad,
                    Observaciones = f.Observaciones,
                    IsSynced = f.IsSynced
                }).ToList();

                var resultado = await apiService.SincronizarFletesAsync(fletesSync);

                if (resultado)
                {
                    // Marcar todos como sincronizados
                    foreach (var flete in fletesNoSincronizados)
                    {
                        flete.IsSynced = true;
                        await _database.UpdateAsync(flete);
                    }

                    return fletesNoSincronizados.Count;
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en sincronización");
                return 0;
            }
        }

        public async Task<List<FletePendienteUI>> ObtenerFletesPendientesDesdeCacheAsync(string chofer, int dias)
        {
            try
            {
                var fechaLimite = DateTime.Now.AddDays(-dias);

                var query = _database.Table<Tb_FlePer_FletePersonal>()
                    .Where(f => f.Chofer == chofer && f.Fecha != null);

                var fletes = await query.ToListAsync();

                // Filtrar por fecha y estado
                var fletesFiltrados = fletes.Where(f =>
                {
                    return f.Fecha >= fechaLimite;
                    //if (DateTime.TryParse(f.Fecha, out DateTime fechaFlete))
                    //{
                    //    return fechaFlete >= fechaLimite;
                    //}
                    //return false;
                })
                .Where(f => f.Status != "Completado" && f.Status != "Cancelado")
                .ToList();

                // Obtener información de rutas y proveedores
                var todasRutas = await _database.Table<Tb_FlePer_Ruta>().ToListAsync();
                var todosProveedores = await _database.Table<Tb_Cat_Proveedor>().ToListAsync();

                // Convertir a FletePendienteUI
                var resultado = new List<FletePendienteUI>();

                foreach (var flete in fletesFiltrados)
                {
                    // Buscar información relacionada
                    string nombreRuta = "Ruta desconocida";
                    if (flete.IdDestFlete.HasValue)
                    {
                        var ruta = todasRutas.FirstOrDefault(r => r.IdDestFlete == flete.IdDestFlete);
                        nombreRuta = ruta?.NomDestFlete ?? "Ruta desconocida";
                    }

                    string nombreProveedor = flete.ProvClave ?? "Desconocido";
                    if (!string.IsNullOrEmpty(flete.ProvClave))
                    {
                        var proveedor = todosProveedores.FirstOrDefault(p => p.ProvClave == flete.ProvClave);
                        nombreProveedor = proveedor?.ProvNombre ?? flete.ProvClave;
                    }

                    // Parsear fecha y hora
                    DateTime fechaHora = DateTime.Now;
                    if (DateTime.TryParse($"{flete.Fecha} {flete.Hora}", out DateTime parsedFecha))
                    {
                        fechaHora = parsedFecha;
                    }

                    resultado.Add(new FletePendienteUI
                    {
                        Id = flete.Id,
                        IdFletePer = (int?)flete.IdFletePer,
                        Ruta = nombreRuta,
                        FechaHora = fechaHora,
                        Proveedor = nombreProveedor,
                        Chofer = flete.Chofer ?? "Desconocido",
                        Estatus = flete.Status ?? "Pendiente",
                        CantidadEsperada = flete.Cantidad,
                        CantidadReal = flete.Cantidad,
                        TipoFlete = flete.TipoFlete ?? "NORMAL",
                        TipoViaje = flete.TipoViaje ?? "TRAER GENTE",
                        FechaInicio = flete.Fecha,
                        FechaFin = flete.FechaFin
                    });
                }

                return resultado.OrderByDescending(f => f.FechaHora).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo fletes desde caché");
                return new List<FletePendienteUI>();
            }
        }

        public async Task<bool> GuardarFleteEnCacheAsync(FleteApi flete)
        {
            try
            {
                var fleteLocal = new Tb_FlePer_FletePersonal
                {
                    IdFletePer = flete.IdFletePer,
                    Fecha = flete.Fecha,
                    Hora = flete.Hora,
                    ProvClave = flete.ProveedorClave,
                    IdDestFlete = flete.IdDestFlete,
                    TipoFlete = flete.TipoFlete,
                    TipoViaje = flete.TipoViaje,
                    Cantidad = flete.Cantidad,
                    Status = flete.Estatus,
                    Chofer = flete.Chofer,
                    //FlePer_CantidadReal = flete.CantidadReal,
                    Observaciones = flete.Observaciones,
                    IsSynced = true
                };

                // Verificar si ya existe
                var existe = await ExisteFleteAsync(flete.IdFletePer);
                if (existe)
                {
                    await _database.UpdateAsync(fleteLocal);
                }
                else
                {
                    await _database.InsertAsync(fleteLocal);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando flete en caché");
                return false;
            }
        }

        public async Task LimpiarCacheAsync()
        {
            try
            {
                await _database.DeleteAllAsync<Tb_FlePer_FletePersonal>();
                await _database.DeleteAllAsync<Tb_FlePer_DetFlete>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error limpiando caché");
            }
        }
        #endregion
    }
}