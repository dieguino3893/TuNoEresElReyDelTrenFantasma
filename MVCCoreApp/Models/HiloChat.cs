using MongoDB.Bson.Serialization.Attributes;
using MVCCoreApp.Models.Base;

namespace MVCCoreApp.Models;

/// <summary>Hilo de conversación con el asistente. Uno por tema; guarda el historial.</summary>
public class HiloChat : AuditableDocument
{
    [BsonElement("usuario")]
    public string Usuario { get; set; } = string.Empty;

    [BsonElement("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [BsonElement("modificadoEn")]
    public DateTime ModificadoEn { get; set; } = DateTime.UtcNow;
}
