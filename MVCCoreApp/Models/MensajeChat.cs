using MongoDB.Bson.Serialization.Attributes;
using MVCCoreApp.Models.Base;

namespace MVCCoreApp.Models;

public enum RolMensaje
{
    Usuario,
    Asistente,
}

public enum EstadoPropuesta
{
    Ninguna,
    Pendiente,
    Confirmada,
    Rechazada,
    Expirada,
}

/// <summary>Mensaje del chat. Los del asistente pueden traer una propuesta de acción pendiente de confirmación.</summary>
public class MensajeChat : AuditableDocument
{
    [BsonElement("hiloId")]
    public string HiloId { get; set; } = string.Empty;

    [BsonElement("rol")]
    public RolMensaje Rol { get; set; }

    [BsonElement("contenido")]
    public string Contenido { get; set; } = string.Empty;

    /// <summary>Acción propuesta en JSON (herramienta + argumentos + texto legible). Solo una por mensaje.</summary>
    [BsonElement("propuestaJson")]
    public string? PropuestaJson { get; set; }

    [BsonElement("propuestaEstado")]
    public EstadoPropuesta PropuestaEstado { get; set; } = EstadoPropuesta.Ninguna;

    [BsonElement("propuestaExpira")]
    public DateTime? PropuestaExpira { get; set; }
}
