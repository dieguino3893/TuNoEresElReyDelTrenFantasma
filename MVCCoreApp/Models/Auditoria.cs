using MongoDB.Bson.Serialization.Attributes;
using MVCCoreApp.Models.Base;

namespace MVCCoreApp.Models;

/// <summary>Bitácora de acciones sensibles (login, usuarios, asistente). Solo lectura desde código.</summary>
public class Auditoria : AuditableDocument
{
    [BsonElement("fecha")]
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    [BsonElement("usuario")]
    public string Usuario { get; set; } = string.Empty;

    [BsonElement("accion")]
    public string Accion { get; set; } = string.Empty;

    [BsonElement("detalle")]
    public string Detalle { get; set; } = string.Empty;

    [BsonElement("ip")]
    public string? Ip { get; set; }
}
