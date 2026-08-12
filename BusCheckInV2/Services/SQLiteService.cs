using BusCheckInV2.Models;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Toast = CommunityToolkit.Maui.Alerts.Toast;
using System.Threading;

namespace BusCheckInV2.Services
{
    public class SQLiteService : ISQLiteService
    {
        private SQLiteAsyncConnection _database;
        private readonly ILogger<SQLiteService> _logger;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _initialized;
        // Constantes para configuración (best practice de Microsoft Learn)
        private const string DatabaseFilename = "BusCheckInV2.db3";
        private const SQLiteOpenFlags Flags =
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.SharedCache; // SharedCache para multi-threaded access

        private string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);

        // ─── CAMBIO 2: Constructor con logger opcional ────────────────────────
        // El parámetro es opcional (default null) para mantener compatibilidad
        // con cualquier lugar que construya SQLiteService directamente.
        // Cuando se resuelve desde DI, el contenedor inyecta ILogger<SQLiteService>
        // automáticamente porque AddLogging() ya registra loggers genéricos.
        // NullLogger.Instance es un logger que no hace nada — nunca lanza excepciones.
        public SQLiteService(ILogger<SQLiteService> logger = null)
        {
            // Si DI no inyecta logger (o si se construye manualmente), usamos NullLogger
            // NullLogger.Instance implementa ILogger y sus métodos son no-ops seguros
            _logger = logger ?? NullLogger<SQLiteService>.Instance;
        }

        public async Task InitializeAsync()
        {
            if (_initialized) return; // fast path sin lock

            await _initLock.WaitAsync();
            try
            {
                if (_initialized) return; // double-check dentro del lock

                _database = new SQLiteAsyncConnection(DatabasePath, Flags);
                await CreateTablesAsync();
                await _database.EnableWriteAheadLoggingAsync();
                await SeedDataAsync();
                _initialized = true;

                _logger.LogInformation("SQLiteService inicializado en: {Path}", DatabasePath);
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task InitializeAsyncLEGACY()
        {
            try
            {
                if (_database != null) return; // Lazy init: solo una vez

                _database = new SQLiteAsyncConnection(DatabasePath, Flags);
                await CreateTablesAsync();
                await _database.EnableWriteAheadLoggingAsync(); // Habilita WAL para concurrency (recomendado en .NET 9+)
                await SeedDataAsync();

                // ─── NUEVO: Ahora sí podemos loggear correctamente ─────────
                _logger.LogInformation("SQLiteService inicializado en: {Path}", DatabasePath);
                // ────────────────────────────────────────────────────────────
            }
            catch (Exception ex)
            {
                // ─── CAMBIO: Log antes del toast para que quede en Debug Output
                _logger.LogCritical(ex, "Fallo crítico al inicializar SQLiteService");
                // ────────────────────────────────────────────────────────────
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
            await _database.CreateTableAsync<Tb_Sync_Log>();
            // Agrega más tablas si es necesario
        }

        // Método para insertar datos iniciales (solo si la tabla está vacía)
        private async Task SeedDataAsync()
        {
            await InsertInitialDataIfNeededAsync(new List<Tb_Cat_Proveedor>
            {
                new Tb_Cat_Proveedor { ProvClave = "TURISTICOS", ProvNombre = "JUAN CARLOS ACOSTA CABRERA" },
                //new Tb_Cat_Proveedor { ProvClave = "10810", ProvNombre = "RIVERA MONTESINO MARGARITA JACQUELINE" },
                new Tb_Cat_Proveedor { ProvClave = "RAMIROGE", ProvNombre = "RAMIRO GARCIA ESTRADA" },
                new Tb_Cat_Proveedor { ProvClave = "11958", ProvNombre = "OMNIBUS DEL CENTRO" }
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
                new Tb_FlePer_Ruta { IdDestFlete = 19, NomDestFlete = "Cardenas-DoñaRosa-San Vicente", FleteCant = 2, FleteCosto = 1155.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 20, NomDestFlete = "Soledad", FleteCant = 2, FleteCosto = 850.00m, DestStatus = "A", RutaCupo = 40, RutaVehiculo = "CAMION" },
                new Tb_FlePer_Ruta { IdDestFlete = 21, NomDestFlete = "Ruta Personal Administrativo", FleteCant = 2, FleteCosto = 556.50m, DestStatus = "A", RutaCupo = 14, RutaVehiculo = "COMBI" },
                new Tb_FlePer_Ruta { IdDestFlete = 22, NomDestFlete = "Ruta Guardería 1", FleteCant = 2, FleteCosto = 400.00m, DestStatus = "A", RutaCupo = 14, RutaVehiculo = "COMBI" },
                new Tb_FlePer_Ruta { IdDestFlete = 23, NomDestFlete = "Ruta Guardería 2", FleteCant = 2, FleteCosto = 400.00m, DestStatus = "A", RutaCupo = 14, RutaVehiculo = "COMBI" },
                new Tb_FlePer_Ruta { IdDestFlete = 24, NomDestFlete = "Ruta Guardería 3", FleteCant = 2, FleteCosto = 400.00m, DestStatus = "A", RutaCupo = 14, RutaVehiculo = "COMBI" }
                // Otros rutas
            });

            await InsertInitialDataIfNeededAsync(new List<Tb_FlePer_ProvRuta>
            {
                new Tb_FlePer_ProvRuta { IdRutaProv = 12, Prov_Clave = "TURISTICOS", IdDestFlete = 12, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 14, Prov_Clave = "TURISTICOS", IdDestFlete = 14, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 16, Prov_Clave = "RAMIROGE", IdDestFlete = 16, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 17, Prov_Clave = "RAMIROGE", IdDestFlete = 17, RutaStatus = "A" },
                new Tb_FlePer_ProvRuta { IdRutaProv = 25, Prov_Clave = "TURISTICOS", IdDestFlete = 17, RutaStatus = "A" },
                
                new Tb_FlePer_ProvRuta { IdRutaProv = 31, Prov_Clave = "11958", IdDestFlete = 1, RutaStatus = "A"},
                new Tb_FlePer_ProvRuta { IdRutaProv = 32, Prov_Clave = "11958", IdDestFlete = 2, RutaStatus = "A"},
                new Tb_FlePer_ProvRuta { IdRutaProv = 33, Prov_Clave = "11958", IdDestFlete = 3, RutaStatus = "A"},
                new Tb_FlePer_ProvRuta { IdRutaProv = 34, Prov_Clave = "11958", IdDestFlete = 4, RutaStatus = "A"},
                new Tb_FlePer_ProvRuta { IdRutaProv = 35, Prov_Clave = "11958", IdDestFlete = 19, RutaStatus = "A"},
                new Tb_FlePer_ProvRuta { IdRutaProv = 36, Prov_Clave = "11958", IdDestFlete = 8, RutaStatus = "A"},
                new Tb_FlePer_ProvRuta { IdRutaProv = 37, Prov_Clave = "11958", IdDestFlete = 10, RutaStatus = "A"},
                new Tb_FlePer_ProvRuta { IdRutaProv = 38, Prov_Clave = "11958", IdDestFlete = 11, RutaStatus = "A"},
                new Tb_FlePer_ProvRuta { IdRutaProv = 39, Prov_Clave = "11958", IdDestFlete = 21, RutaStatus = "A"},
                new Tb_FlePer_ProvRuta { IdRutaProv = 40, Prov_Clave = "11958", IdDestFlete = 22, RutaStatus = "A"},
                new Tb_FlePer_ProvRuta { IdRutaProv = 41, Prov_Clave = "11958", IdDestFlete = 23, RutaStatus = "A"},
                new Tb_FlePer_ProvRuta { IdRutaProv = 42, Prov_Clave = "11958", IdDestFlete = 24, RutaStatus = "A"},
                new Tb_FlePer_ProvRuta { IdRutaProv = 43, Prov_Clave = "11958", IdDestFlete = 18, RutaStatus = "A"},

                new Tb_FlePer_ProvRuta { IdRutaProv = 18, Prov_Clave = "RAMIROGE", IdDestFlete = 18, RutaStatus = "A" },
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
        // ============================================================
        //  ARCHIVO: BusCheckInV2/Services/SQLiteService.cs
        //  INSTRUCCIÓN: Reemplaza el método ClearAllTablesAsync completo.
        // ============================================================

        public async Task<ClearTablesResult> ClearAllTablesAsync(bool forceDelete = false)
        {
            try
            {
                // ── GUARDIA: verificar pendientes antes de borrar ──────────────────
                // Esta verificación es la diferencia entre perder datos de producción
                // y mantenerlos seguros. NUNCA se omite sin forceDelete explícito.
                if (!forceDelete)
                {
                    var pendientesFletes = await _database
                        .Table<Tb_FlePer_FletePersonal>()
                        .Where(f => !f.IsSynced)
                        .CountAsync();

                    var pendientesDetalles = await _database
                        .Table<Tb_FlePer_DetFlete>()
                        .Where(d => !d.IsSynced)
                        .CountAsync();

                    int totalPendientes = pendientesFletes + pendientesDetalles;

                    if (totalPendientes > 0)
                    {
                        _logger.LogWarning(
                            "ClearAllTablesAsync bloqueado: {Total} registros sin sync " +
                            "({Fletes} fletes, {Detalles} detalles)",
                            totalPendientes, pendientesFletes, pendientesDetalles);

                        return ClearTablesResult.Rejected(totalPendientes);
                    }
                }

                // ── Conteo para el reporte ─────────────────────────────────────────
                int countFletes = await _database.Table<Tb_FlePer_FletePersonal>().CountAsync();
                int countDetalles = await _database.Table<Tb_FlePer_DetFlete>().CountAsync();

                // ── Borrado en orden correcto (detalles antes que padres) ──────────
                await _database.DeleteAllAsync<Tb_FlePer_DetFlete>();
                await _database.DeleteAllAsync<Tb_FlePer_FletePersonal>();

                int totalBorrados = countFletes + countDetalles;

                _logger.LogWarning(
                    "ClearAllTablesAsync ejecutado. Borrados: {Fletes} fletes, " +
                    "{Detalles} detalles. ForceDelete={Force}",
                    countFletes, countDetalles, forceDelete);

                return ClearTablesResult.Ok(totalBorrados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ClearAllTablesAsync");
                return ClearTablesResult.Error($"Error al limpiar tablas: {ex.Message}");
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
                // FIX 2026-06-03 (A7): con el nuevo modelo, Status solo vale 'A' o 'C'.
                // 'F' ya no existe (Finalizado usa 'A'). Excluimos solo 'C'
                // aquí; el helper de abajo se encarga de distinguir entre
                // Finalizado y Activo/En curso/Pendiente según los detalles.
                .Where(f => f.Status != "C")
                .ToList();

                // Obtener todas las rutas y proveedores para mapeo
                var todasRutas = await _database.Table<Tb_FlePer_Ruta>().ToListAsync();
                var todosProveedores = await _database.Table<Tb_Cat_Proveedor>().ToListAsync();

                // FIX 2026-06-03 (A4): cargar TODOS los detalles una sola vez
                // (en vez de un query por flete) y agruparlos por IdFletePer
                // para calcular EstadoCalculado localmente.
                var todosDetalles = await _database.Table<Tb_FlePer_DetFlete>().ToListAsync();
                var detallesPorFlete = todosDetalles
                    .GroupBy(d => d.FleteLocalId)
                    .ToDictionary(g => g.Key, g => g.ToList());

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
                        // FIX 2026-06-02: preservamos el código 1 char (P/I/A/F/C)
                        // tal como está en SQLite, sin fallback a "Pendiente"
                        // que ensucia la UI. El helper EsFletePendiente del
                        // FletesPendientesViewModel ya entiende los códigos.
                        Id = flete.Id,
                        IdFletePer = (int?)flete.IdFletePer,
                        Ruta = nombreRuta,
                        FechaHora = fechaHora,
                        Proveedor = nombreProveedor,
                        Chofer = flete.Chofer ?? "Desconocido",
                        Estatus = flete.Status ?? "P",
                        CantidadEsperada = (int)flete.Cantidad,
                        CantidadReal = flete.Cantidad,
                        TipoFlete = flete.TipoFlete ?? "NORMAL",
                        TipoViaje = flete.TipoViaje ?? "TRAER GENTE",
                        FechaInicio = flete.Fecha,
                        FechaFin = flete.FechaFin,
                        // FIX 2026-06-03 (A4): calcular EstadoCalculado + CantPasajeros
                        // localmente con los detalles cacheados arriba.
                        EstadoCalculado = CalcularEstadoCalculado(
                            flete.Status, detallesPorFlete.GetValueOrDefault(flete.Id, new List<Tb_FlePer_DetFlete>())),
                        CantPasajeros = detallesPorFlete.GetValueOrDefault(flete.Id, new List<Tb_FlePer_DetFlete>())
                            .Count(d => d.CveNomina.HasValue && d.CveNomina != 0 && d.CveNomina != 9999),
                        // FIX 2026-06-03: la UltimaFechaDetalle también debe
                        // exponerse al UI para casos de uso futuro (ej. mostrar
                        // "hace 3 horas" en la tarjeta del flete).
                        UltimaFechaDetalle = detallesPorFlete.GetValueOrDefault(flete.Id, new List<Tb_FlePer_DetFlete>())
                            .Where(d => d.Fecha.HasValue)
                            .Select(d => d.Fecha!.Value)
                            .DefaultIfEmpty(DateTime.MinValue)
                            .Max()
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

                // FIX 2026-06-02: aceptamos tanto los códigos 1 char del
                // backend nuevo (F=Finalizado, C=Cancelado) como los
                // strings legacy (Completado, Cancelado) por compatibilidad
                // con código que ya guardó valores en mayúsculas.
                if (nuevoEstatus == "F" || nuevoEstatus == "C" ||
                    nuevoEstatus == "Completado" || nuevoEstatus == "Cancelado")
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
            // FIX 2026-06-04: reescritura completa. El batch
            // /SincronizarFletes del backend NO está confirmado y la
            // firma exige un List<FleteSincronizacion> que no sabemos
            // si el contrato del backend acepta. En cambio,
            // /InsertarFletePersonal SÍ existe y ya lo usa el
            // EscaneoCodigoViewModel con éxito (ronda 2026-06-02).
            //
            // Estrategia: recorrer los fletes locales con IsSynced=false
            // y subirlos uno por uno al endpoint individual. Los que
            // suban OK se marcan IsSynced=true en la BD local; los que
            // fallen se dejan como están para el próximo intento.
            //
            // Devuelve la cantidad de fletes sincronizados con éxito
            // (no la cantidad intentada, para que el VM muestre un
            // número honesto al chofer).
            try
            {
                if (apiService == null)
                {
                    _logger.LogWarning("SincronizarConApiAsync: apiService es null");
                    return 0;
                }

                var fletesNoSincronizados = await ObtenerFletesNoSincronizadosAsync();

                if (!fletesNoSincronizados.Any())
                {
                    _logger.LogInformation("SincronizarConApiAsync: no hay fletes pendientes");
                    return 0;
                }

                int sincronizados = 0;
                int fallidos = 0;

                foreach (var flete in fletesNoSincronizados)
                {
                    try
                    {
                        // FIX 2026-06-04: si el flete YA tiene un IdFletePer
                        // del servidor, NO lo re-insertamos (sería un duplicado
                        // en la BD). Solo lo marcamos como sincronizado.
                        // Esto pasa cuando el flete se creó en el server y
                        // se bajó al cache local, pero por algún motivo el
                        // flag IsSynced quedó en false.
                        if (flete.IdFletePer.HasValue && flete.IdFletePer.Value > 0)
                        {
                            flete.IsSynced = true;
                            await _database.UpdateAsync(flete);
                            sincronizados++;
                            continue;
                        }

                        // Armar el request que espera /InsertarFletePersonal.
                        // Coincide 1:1 con la firma que usa EscaneoCodigoViewModel
                        // (línea 535 de fe1d280d__EscaneoCodigoViewModel.cs).
                        var request = new FletePersonalRequest
                        {
                            Fecha = flete.Fecha?.ToString("yyyy-MM-dd")
                                ?? DateTime.Now.ToString("yyyy-MM-dd"),
                            Hora = flete.Hora?.ToString(@"hh\:mm\:ss")
                                ?? DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss"),
                            ClaveProveedor = flete.ProvClave ?? string.Empty,
                            IdDestFlete = (int)(flete.IdDestFlete ?? 0),
                            TipoFlete = flete.TipoFlete ?? "NORMAL",
                            TipoViaje = flete.TipoViaje ?? "TRAER GENTE",
                            Cantidad = flete.Cantidad ?? 0,
                            Estatus = flete.Status ?? "P",
                            Chofer = flete.Chofer ?? string.Empty
                        };

                        long serverId = await apiService.InsertarFletePersonal(request);

                        if (serverId > 0)
                        {
                            // Subió OK: guardar el ID del server y marcar synced
                            flete.IdFletePer = serverId;
                            flete.IsSynced = true;
                            await _database.UpdateAsync(flete);
                            sincronizados++;
                        }
                        else
                        {
                            _logger.LogWarning(
                                "SincronizarConApiAsync: server rechazó el flete local Id={LocalId}",
                                flete.Id);
                            fallidos++;
                        }
                    }
                    catch (Exception exFlete)
                    {
                        // NO abortar el batch: un flete que falla no debe
                        // impedir subir los demás. Solo loguear y seguir.
                        _logger.LogError(exFlete,
                            "SincronizarConApiAsync: error con flete local Id={LocalId}",
                            flete.Id);
                        fallidos++;
                    }
                }

                _logger.LogInformation(
                    "SincronizarConApiAsync: {Ok} sincronizados, {Fail} fallaron de {Total} totales",
                    sincronizados, fallidos, fletesNoSincronizados.Count);

                return sincronizados;
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
                // FIX 2026-06-03 (A7): con el nuevo modelo, Status solo vale 'A' o 'C'.
                // 'F' ya no existe (Finalizado usa 'A'). Excluimos solo 'C'
                // aquí; el helper de abajo se encarga de distinguir entre
                // Finalizado y Activo/En curso/Pendiente según los detalles.
                .Where(f => f.Status != "C")
                .ToList();

                // Obtener información de rutas y proveedores
                var todasRutas = await _database.Table<Tb_FlePer_Ruta>().ToListAsync();
                var todosProveedores = await _database.Table<Tb_Cat_Proveedor>().ToListAsync();

                // FIX 2026-06-03 (A4): cachear todos los detalles para
                // calcular EstadoCalculado + CantPasajeros localmente.
                var todosDetallesCache = await _database.Table<Tb_FlePer_DetFlete>().ToListAsync();
                var detallesPorFleteCache = todosDetallesCache
                    .GroupBy(d => d.FleteLocalId)
                    .ToDictionary(g => g.Key, g => g.ToList());

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
                        // FIX 2026-06-02: preservamos el código 1 char (P/I/A/F/C)
                        // tal como está en SQLite, sin fallback a "Pendiente"
                        // que ensucia la UI. El helper EsFletePendiente del
                        // FletesPendientesViewModel ya entiende los códigos.
                        Id = flete.Id,
                        IdFletePer = (int?)flete.IdFletePer,
                        Ruta = nombreRuta,
                        FechaHora = fechaHora,
                        Proveedor = nombreProveedor,
                        Chofer = flete.Chofer ?? "Desconocido",
                        Estatus = flete.Status ?? "P",
                        CantidadEsperada = (int)flete.Cantidad,
                        CantidadReal = flete.Cantidad,
                        TipoFlete = flete.TipoFlete ?? "NORMAL",
                        TipoViaje = flete.TipoViaje ?? "TRAER GENTE",
                        FechaInicio = flete.Fecha,
                        FechaFin = flete.FechaFin,
                        // FIX 2026-06-03 (A4): calcular EstadoCalculado + CantPasajeros
                        // localmente con los detalles cacheados arriba.
                        EstadoCalculado = CalcularEstadoCalculado(
                            flete.Status, detallesPorFleteCache.GetValueOrDefault(flete.Id, new List<Tb_FlePer_DetFlete>())),
                        CantPasajeros = detallesPorFleteCache.GetValueOrDefault(flete.Id, new List<Tb_FlePer_DetFlete>())
                            .Count(d => d.CveNomina.HasValue && d.CveNomina != 0 && d.CveNomina != 9999),
                        // FIX 2026-06-03: la UltimaFechaDetalle también debe
                        // exponerse al UI para casos de uso futuro.
                        UltimaFechaDetalle = detallesPorFleteCache.GetValueOrDefault(flete.Id, new List<Tb_FlePer_DetFlete>())
                            .Where(d => d.Fecha.HasValue)
                            .Select(d => d.Fecha!.Value)
                            .DefaultIfEmpty(DateTime.MinValue)
                            .Max()
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

        #region LOG DE SINCRONIZACION
        public async Task RegistrarSyncLogAsync(
    string tipoOperacion,
    long? idFletePer,
    bool exitoso,
    string mensaje,
    int registrosAfectados = 0,
    int duracionMs = 0)
        {
            try
            {
                var entry = new Tb_Sync_Log
                {
                    Timestamp = DateTime.Now,
                    TipoOperacion = tipoOperacion,
                    IdFletePer = idFletePer,
                    Exitoso = exitoso,
                    Mensaje = mensaje,
                    RegistrosAfectados = registrosAfectados,
                    DuracionMs = duracionMs
                };
                await _database.InsertAsync(entry);
            }
            catch (Exception ex)
            {
                // El log nunca debe interrumpir el flujo principal
                _logger.LogWarning(ex, "No se pudo escribir sync log");
            }
        }

        public async Task<List<Tb_Sync_Log>> ObtenerSyncLogAsync(int ultimos = 50)
        {
            try
            {
                return await _database.Table<Tb_Sync_Log>()
                    .OrderByDescending(l => l.Timestamp)
                    .Take(ultimos)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leyendo sync log");
                return new List<Tb_Sync_Log>();
            }
        }

        public async Task LimpiarSyncLogAntiguoAsync(int diasRetencion = 7)
        {
            try
            {
                var limite = DateTime.Now.AddDays(-diasRetencion);
                await _database.Table<Tb_Sync_Log>()
                    .Where(l => l.Timestamp < limite)
                    .DeleteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error limpiando sync log antiguo");
            }
        }

        // FIX 2026-06-03 (A4): helper privado que replica la misma
        // logica que el backend en WSBusCheckInV2Controller.ObtenerFletesPorChofer
        // para calcular EstadoCalculado. Esto es necesario porque
        // (a) la BD local NO tiene el campo EstadoCalculado (es derivado)
        // (b) la UI debe mostrar el estado fino (Activo/En curso/Pendiente/
        //     Finalizado/Cancelado), NO el codigo 1 char de Status
        // (c) cuando el flete viene del cache local, no tenemos el derivado
        //     del backend, asi que lo calculamos aca
        //
        // Reglas (las mismas que el backend):
        //   - Status='C' sin pasajeros (solo INICIO o INICIO+FIN sin medio) → Cancelado
        //   - Status='C' con pasajeros                                              → Pendiente
        //   - Status='A' con FIN (CveNomina=9999, Nombre='FIN')                 → Finalizado
        //   - Status='A' con INICIO + >=1 pasajero + ultimo registro <5h         → En curso
        //   - Status='A' con INICIO + >=1 pasajero (>=5h o sin UltimaFecha)     → Pendiente
        //   - Status='A' con INICIO sin pasajeros                                  → Pendiente
        //   - Status='A' sin INICIO                                                → Activo
        private static string CalcularEstadoCalculado(
            string status,
            List<Tb_FlePer_DetFlete> detalles)
        {
            if (detalles == null) detalles = new List<Tb_FlePer_DetFlete>();

            int cantPasajeros = detalles.Count(d =>
                d.CveNomina.HasValue && d.CveNomina != 0 && d.CveNomina != 9999);

            bool tieneInicio = detalles.Any(d => d.CveNomina == 0);
            bool tieneFin = detalles.Any(d =>
                d.CveNomina == 9999 && d.Nombre == "FIN");

            DateTime? ultimaFecha = detalles
                .Where(d => d.Fecha.HasValue)
                .Select(d => d.Fecha!.Value)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            bool ultimas5h = ultimaFecha.HasValue &&
                             (DateTime.Now - ultimaFecha.Value).TotalHours < 5;

            if (status == "C" && cantPasajeros == 0)
                return "Cancelado";
            if (status == "C")
                return "Pendiente";
            if (status == "A" && tieneFin)
                return "Finalizado";
            if (status == "A" && tieneInicio && cantPasajeros > 0 && ultimas5h)
                return "En curso";
            if (status == "A" && tieneInicio && cantPasajeros > 0)
                return "Pendiente";
            if (status == "A" && tieneInicio)
                return "Pendiente";
            return "Activo";
        }
        #endregion

        public async Task<int?> ObtenerIdLocalPorIdFletePerAsync(int idFletePer)
        {
            try
            {
                var flete = await _database.Table<Tb_FlePer_FletePersonal>()
                    .FirstOrDefaultAsync(f => f.IdFletePer == idFletePer);
                return flete?.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo ID local para IdFletePer={Id}", idFletePer);
                return null;
            }
        }

        public async Task<int> InsertarFleteDesdeUIAsync(FletePendienteUI fleteUI)
        {
            try
            {
                var flete = new Tb_FlePer_FletePersonal
                {
                    IdFletePer = fleteUI.IdFletePer,
                    Fecha = fleteUI.FechaHora.Date,
                    Hora = fleteUI.FechaHora.TimeOfDay,
                    ProvClave = fleteUI.Proveedor,  // Ajusta según tu mapeo
                    IdDestFlete = 0, // Debes obtenerlo de alguna relación, si no, dejar 0
                    TipoFlete = fleteUI.TipoFlete,
                    TipoViaje = fleteUI.TipoViaje,
                    Cantidad = fleteUI.CantidadEsperada,
                    Status = fleteUI.Estatus,
                    Chofer = fleteUI.Chofer,
                    IsSynced = true // Porque viene de la API, ya está sincronizado
                };
                await _database.InsertAsync(flete);
                return flete.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error insertando flete desde UI");
                return 0;
            }
        }
    }
}