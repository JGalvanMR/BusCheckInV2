using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusCheckInV2.Models
{
    public class FleteResponse
    {
        public int IdFletePer { get; set; }
        public string Fecha { get; set; }
        public string Hora { get; set; }
        public string ProveedorClave { get; set; }
        public string ProveedorNombre { get; set; }
        public int IdDestFlete { get; set; }
        public string RutaNombre { get; set; }
        public string TipoFlete { get; set; }
        public string TipoViaje { get; set; }
        public int Cantidad { get; set; }
        public string Estatus { get; set; }
        public string Chofer { get; set; }
        public int? CantidadReal { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string Observaciones { get; set; }
        public int PuntosRegistrados { get; set; }

        // ── FIX 2026-06-03 (Opción A) ──────────────────────────────────────
        // El backend ahora expone 3 campos derivados de los detalles del
        // flete. Si tu backend está actualizado, los vas a recibir
        // deserializados automáticamente. Si no, quedan en sus defaults
        // (0, null, "Activo") y la UI puede calcularlos localmente.

        // Cantidad de pasajeros escaneados (CveNomina NOT IN (0, 9999))
        public int CantPasajeros { get; set; }

        // Timestamp del último registro de detalle (cualquiera)
        public DateTime? UltimaFechaDetalle { get; set; }

        // Estado conceptual derivado (Activo/En curso/Pendiente/Finalizado/Cancelado)
        // Es el que la UI debe mostrar, NO Estatus (que vale 'A' o 'C')
        public string EstadoCalculado { get; set; } = "Activo";
    }
}
