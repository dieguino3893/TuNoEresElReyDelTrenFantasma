using MongoDB.Bson.Serialization.Attributes;
using MVCCoreApp.Models.Base;

namespace MVCCoreApp.Models;

public class Subscriber : AuditableDocument
{
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("source")]
    public string Source { get; set; } = "web";

    [BsonElement("country")]
    public string? Country { get; set; }

    [BsonElement("region")]
    public string? Region { get; set; }

    [BsonElement("unsubscribedAt")]
    public DateTime? UnsubscribedAt { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;
}
