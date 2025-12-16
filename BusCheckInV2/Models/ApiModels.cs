using System.Text.Json.Serialization;

namespace BusCheckInV2.Models
{
    // Modelos para request/responses de API
    public class ApiFleteResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("data")]
        public object Data { get; set; }
    }

    public class CancelarFleteRequest
    {
        public int FleteId { get; set; }
        public string Motivo { get; set; }
    }

    public class FinalizarFleteRequest
    {
        public int FleteId { get; set; }
        public int CantidadPasajeros { get; set; }
        public string Observaciones { get; set; }
    }
}